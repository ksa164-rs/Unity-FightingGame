using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    /// <summary>
    /// Unity 6000.3 / URP 17.3 の VFX Graph 用静的素材を生成します。
    /// 動き・発生・消散は Graph 側が担当し、このクラスは Editor 内でのみ使用します。
    /// </summary>
    public static class CombatVFXArtAssets
    {
        private const float Tau = Mathf.PI * 2f;
        private const int AssetCount = 10;

        /// <summary>
        /// 明示した Assets 配下へテクスチャ 5 枚とメッシュ 5 個を生成します。
        /// 同名のファイル・アセット・メタデータが存在するときは上書きしません。
        /// </summary>
        public static void Generate(string rootFolder)
        {
            string root = ValidateRoot(rootFolder);
            EnsureFolder(root + "/Textures");
            EnsureFolder(root + "/Meshes");

            int processed = 0;
            int created = 0;
            int skipped = 0;
            List<string> failures = new List<string>();

            void GenerateOne(string relativePath, Action<string> generate)
            {
                string assetPath = root + "/" + relativePath;
                EditorUtility.DisplayProgressBar("GASG / VFX 素材を生成", relativePath,
                    (float)processed / AssetCount);
                try
                {
                    if (Exists(assetPath))
                    {
                        skipped++;
                        Debug.Log("[GASG VFX Art][SKIP] 既存素材を保持: " + assetPath);
                        return;
                    }

                    generate(assetPath);
                    if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                        throw new InvalidOperationException("Unity に素材が読み込まれていません: " + assetPath);
                    created++;
                    Debug.Log("[GASG VFX Art][SUCCESS] " + assetPath);
                }
                catch (Exception exception)
                {
                    failures.Add(assetPath + ": " + exception.Message);
                    Debug.LogError("[GASG VFX Art][FAIL] " + assetPath + "\n" + exception);
                }
                finally
                {
                    processed++;
                }
            }

            try
            {
                GenerateOne("Textures/T_Filament.png", path => SaveTexture(path, 1024, 256, Filament, true));
                GenerateOne("Textures/T_Soft.png", path => SaveTexture(path, 256, 256, Soft, false));
                GenerateOne("Textures/T_Spark.png", path => SaveTexture(path, 128, 512, Spark, false));
                GenerateOne("Textures/T_Flash.png", path => SaveTexture(path, 512, 512, Flash, false));
                GenerateOne("Textures/T_Smoke.png", path => SaveTexture(path, 512, 512, Smoke, false));
                GenerateOne("Meshes/M_Ring.asset", path => SaveMesh(path, BuildRing()));
                GenerateOne("Meshes/M_Crescent.asset", path => SaveMesh(path, BuildCrescent()));
                GenerateOne("Meshes/M_Helix.asset", path => SaveMesh(path, BuildHelix()));
                GenerateOne("Meshes/M_OrbRibbon.asset", path => SaveMesh(path, BuildOrbRibbon()));
                GenerateOne("Meshes/M_PunchArc.asset", path => SaveMesh(path, BuildPunchArc()));
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[GASG VFX Art] 成功 {created} / スキップ {skipped} / 失敗 {failures.Count} | {root}");
            if (failures.Count > 0)
                throw new InvalidOperationException("VFX 素材生成でエラーが発生しました。\n" + string.Join("\n", failures));
        }

        private static string ValidateRoot(string rootFolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new ArgumentException("出力先には Assets 配下の専用フォルダを指定してください。", nameof(rootFolder));

            string normalized = rootFolder.Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                normalized.IndexOf(':') >= 0)
                throw new ArgumentException("出力先は Assets/ で始まる専用フォルダに限定しています。", nameof(rootFolder));

            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                    throw new ArgumentException("出力先に空要素や相対移動を含めないでください。", nameof(rootFolder));
            }

            string absoluteAssets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string absoluteOutput = Path.GetFullPath(Path.Combine(Application.dataPath, normalized.Substring("Assets/".Length)));
            if (!absoluteOutput.StartsWith(absoluteAssets, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("出力先が Assets フォルダ外です。", nameof(rootFolder));
            return normalized;
        }

        private static string AbsolutePath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        private static bool Exists(string assetPath)
        {
            string absolute = AbsolutePath(assetPath);
            return File.Exists(absolute) || Directory.Exists(absolute) || File.Exists(absolute + ".meta") ||
                AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            int separator = folder.LastIndexOf('/');
            if (separator < 0)
                throw new InvalidOperationException("Assets フォルダが見つかりません。");
            string parent = folder.Substring(0, separator);
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, folder.Substring(separator + 1));
            if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(folder))
                throw new IOException("出力フォルダを作成できません: " + folder);
        }

        private static void SaveTexture(string assetPath, int width, int height,
            Func<float, float, Color> sample, bool repeatU)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                // RGB は非乗算グレー、Alpha が形状です。煙以外は Graph の HDR 色で着色します。
                Color[] pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float u = (x + 0.5f) / width;
                        float v = (y + 0.5f) / height;
                        pixels[y * width + x] = sample(u, v);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                    throw new IOException("PNG エンコードに失敗しました。");

                // CreateNew により、存在確認と書き込みの間にできたファイルも保護します。
                using (FileStream stream = new FileStream(AbsolutePath(assetPath), FileMode.CreateNew, FileAccess.Write))
                    stream.Write(png, 0, png.Length);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("TextureImporter が見つかりません。");
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = repeatU ? 4 : 1;
                importer.maxTextureSize = 1024;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void SaveMesh(string assetPath, Mesh mesh)
        {
            try
            {
                mesh.name = Path.GetFileNameWithoutExtension(assetPath);
                AssetDatabase.CreateAsset(mesh, assetPath);
                if (!AssetDatabase.Contains(mesh))
                    throw new IOException("メッシュアセットを作成できませんでした。");
            }
            catch
            {
                if (!AssetDatabase.Contains(mesh))
                    UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        private static Color Mask(float alpha, float luminance = 1f)
        {
            return new Color(luminance, luminance, luminance, Mathf.Clamp01(alpha));
        }

        private static float Gaussian(float value, float width)
        {
            float scaled = value / Mathf.Max(0.0001f, width);
            return Mathf.Exp(-scaled * scaled);
        }

        private static float Noise(float x, float y)
        {
            // 固定した座標で再現性を確保します。UnityEngine.Random の状態を変更しません。
            return 0.54f * Mathf.PerlinNoise(x, y) +
                0.27f * Mathf.PerlinNoise(x * 2.07f + 13.7f, y * 2.07f + 7.9f) +
                0.13f * Mathf.PerlinNoise(x * 4.19f + 6.3f, y * 4.19f + 17.1f) +
                0.06f * Mathf.PerlinNoise(x * 8.31f + 41.1f, y * 8.31f + 2.7f);
        }

        private static Color Filament(float u, float v)
        {
            // 整数周波数の調和波を使い、U の継ぎ目で光の筋が途切れないようにします。
            float phase = u * Tau;
            float strandSum = 0f;
            for (int strand = 0; strand < 9; strand++)
            {
                float offset = strand * 1.713f;
                float center = 0.16f + strand * 0.082f +
                    0.045f * Mathf.Sin(phase * (1 + strand % 3) + offset) +
                    0.017f * Mathf.Sin(phase * (5 + strand % 2) - offset);
                float width = 0.0045f + 0.009f * (0.5f + 0.5f * Mathf.Sin(phase * 2f + offset));
                float pulse = 0.25f + 0.75f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(phase * (2 + strand % 3) + offset), 1.2f);
                float sharp = Gaussian(v - center, width);
                float fringe = Gaussian(v - center, width * 4.2f) * 0.14f;
                strandSum += (sharp + fringe) * pulse;
            }

            float broadWarp = v - 0.5f - 0.065f * Mathf.Sin(phase * 2f);
            float broad = Gaussian(broadWarp, 0.17f) * (0.11f + 0.07f * Mathf.Sin(phase * 3f + v * 23f));
            float feather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.12f)) *
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.12f));
            float alpha = Mathf.Clamp01((strandSum * 0.86f + broad) * feather);
            return Mask(alpha, Mathf.Lerp(0.64f, 1f, Mathf.Sqrt(alpha)));
        }

        private static Color Soft(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float radius = Mathf.Sqrt(x * x + y * y);
            float alpha = Gaussian(radius, 0.24f) * 0.8f + Gaussian(radius, 0.53f) * 0.22f;
            return Mask(alpha * Mathf.Pow(Mathf.Clamp01(1f - radius), 0.55f));
        }

        private static Color Spark(float u, float v)
        {
            // 細い先端を持つ縦向き火花。Graph の速度方向へ長軸を向けて使用します。
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float envelope = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 0.8f);
            float width = 0.014f + 0.12f * envelope;
            float core = Gaussian(x, width * 0.25f);
            float glow = Gaussian(x, width) * 0.31f;
            float alpha = (core + glow) * envelope;
            return Mask(alpha);
        }

        private static Color Flash(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float radius = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            float rays = 0f;
            // 長さと太さが異なる 18 本の非対称な放射線を、単純な十字から分離します。
            for (int ray = 0; ray < 18; ray++)
            {
                float target = ray * Tau / 18f + 0.066f * Mathf.Sin(ray * 2.73f);
                float length = 0.38f + 0.52f * (0.5f + 0.5f * Mathf.Sin(ray * 3.11f + 0.3f));
                float difference = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float width = Mathf.Lerp(0.11f, 0.012f, Mathf.Clamp01(radius / length));
                float rayShape = Gaussian(difference, width) * Mathf.Pow(Mathf.Clamp01(1f - radius / length), 1.4f);
                rays = Mathf.Max(rays, rayShape);
            }

            float core = Gaussian(radius, 0.065f);
            float halo = Gaussian(radius, 0.23f) * 0.18f;
            float alpha = Mathf.Clamp01(core + rays * 0.9f + halo) * Mathf.Clamp01((1f - radius) * 8f);
            return Mask(alpha);
        }

        private static Color Smoke(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float warpX = Noise(u * 3.6f + 23.3f, v * 3.6f + 12.5f) - 0.5f;
            float warpY = Noise(u * 3.2f + 65.2f, v * 3.2f + 38.1f) - 0.5f;
            float broad = Noise(u * 5.5f + warpX * 1.9f + 8.1f, v * 5.5f + warpY * 1.9f + 5.7f);
            float detail = Noise(u * 16f + warpX * 2.4f + 27.2f, v * 16f + warpY * 2.4f + 11.4f);
            float radius = Mathf.Sqrt(x * x + y * y);
            float edgeRadius = radius + (broad - 0.52f) * 0.38f;
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.91f - edgeRadius) / 0.48f));
            float density = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((broad * 0.76f + detail * 0.24f - 0.2f) / 0.6f));
            float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radius) * 9f));
            float alpha = envelope * density * edgeFade * 0.72f;
            // 黒い四角を作らないため、密度は Alpha に格納し RGB は明るい煙の陰影だけにします。
            return Mask(alpha, Mathf.Lerp(0.46f, 0.95f, density));
        }

        private struct RibbonPoint
        {
            public Vector3 Center;
            public Vector3 Across;
            public float Width;

            public RibbonPoint(Vector3 center, Vector3 across, float width)
            {
                Center = center;
                Across = across.normalized;
                Width = width;
            }
        }

        private sealed class RibbonMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void Add(int segments, float repeats, Func<float, RibbonPoint> point)
            {
                int start = vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    RibbonPoint p = point(t);
                    Vector3 halfWidth = p.Across * (p.Width * 0.5f);
                    vertices.Add(p.Center - halfWidth);
                    vertices.Add(p.Center + halfWidth);
                    uv.Add(new Vector2(t * repeats, 0f));
                    uv.Add(new Vector2(t * repeats, 1f));
                    if (i == segments)
                        continue;
                    int a = start + i * 2;
                    triangles.Add(a);
                    triangles.Add(a + 2);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(a + 2);
                    triangles.Add(a + 3);
                }
            }

            public Mesh Finish()
            {
                // VFX Output の Cull Mode は Off 推奨。裏面の重複三角形は作りません。
                Mesh mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static float Taper(float t, float exponent = 0.6f)
        {
            return Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI)), exponent);
        }

        private static Mesh BuildRing()
        {
            RibbonMesh ribbon = new RibbonMesh();
            ribbon.Add(256, 3f, t =>
            {
                float angle = t * Tau;
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                float ripple = 0.012f * Mathf.Sin(angle * 7f) + 0.008f * Mathf.Sin(angle * 13f);
                float width = 0.10f + 0.037f * Mathf.Sin(angle * 3f + 0.5f) + 0.015f * Mathf.Sin(angle * 11f);
                return new RibbonPoint(radial * (1f + ripple), radial, width);
            });
            // 小さな切れた外周線を添え、拡大時の工業製品のような均一さを弱めます。
            for (int arc = 0; arc < 3; arc++)
            {
                float phase = arc * Tau / 3f;
                ribbon.Add(40, 1f, t =>
                {
                    float angle = phase + t * 1.15f;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    return new RibbonPoint(radial * 1.045f, radial, 0.018f * Taper(t));
                });
            }
            return ribbon.Finish();
        }

        private static Mesh BuildCrescent()
        {
            RibbonMesh ribbon = new RibbonMesh();
            for (int band = 0; band < 3; band++)
            {
                int index = band;
                ribbon.Add(160, 1.75f, t =>
                {
                    float angle = Mathf.Lerp(-1.65f, 1.65f, t);
                    float layer = 1f + index * 0.068f;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Vector3 center = new Vector3(Mathf.Cos(angle) * 0.54f, Mathf.Sin(angle) * 0.93f, 0f) * layer;
                    float width = (index == 0 ? 0.19f : 0.035f) * Taper(t, 0.75f);
                    return new RibbonPoint(center, radial, width);
                });
            }
            return ribbon.Finish();
        }

        private static Mesh BuildHelix()
        {
            RibbonMesh ribbon = new RibbonMesh();
            for (int strand = 0; strand < 3; strand++)
            {
                int index = strand;
                ribbon.Add(224, 2f, t =>
                {
                    float angle = t * Tau * 1.28f + index * 1.76f;
                    float radius = Mathf.Lerp(0.67f, 0.055f, Mathf.Pow(t, 1.6f));
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 center = radial * radius + new Vector3(0.36f * t * t, 2.4f * t, 0f);
                    float width = (index == 0 ? 0.32f : 0.09f) * Taper(t, 0.45f);
                    // 半径方向と上下方向を混ぜ、横から見ても帯の面が読める断面にします。
                    Vector3 across = (radial * 0.76f + Vector3.up * 0.66f).normalized;
                    return new RibbonPoint(center, across, width);
                });
            }
            return ribbon.Finish();
        }

        private static Mesh BuildOrbRibbon()
        {
            RibbonMesh ribbon = new RibbonMesh();
            for (int strand = 0; strand < 4; strand++)
            {
                int index = strand;
                ribbon.Add(192, 2f, t =>
                {
                    float angle = t * Tau * (0.88f + index * 0.075f) + index * Tau / 4f;
                    float x = Mathf.Lerp(-1.8f + index * 0.11f, 0.52f, t);
                    // 前端を一点へ収束し、球の前面と細長い後流が連続する輪郭にします。
                    float radius = 0.49f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.52f);
                    radius *= Mathf.Lerp(0.6f, 1.08f, t);
                    Vector3 radial = new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector3 across = new Vector3(0f, -Mathf.Sin(angle), Mathf.Cos(angle));
                    Vector3 center = new Vector3(x, 0f, 0f) + radial * radius;
                    float width = (index == 0 ? 0.25f : 0.115f) * Taper(t, 0.62f);
                    return new RibbonPoint(center, across, width);
                });
            }
            return ribbon.Finish();
        }

        private static Mesh BuildPunchArc()
        {
            RibbonMesh ribbon = new RibbonMesh();
            for (int strand = 0; strand < 3; strand++)
            {
                int index = strand;
                ribbon.Add(112, 1.3f, t =>
                {
                    float x = Mathf.Lerp(-0.78f + index * 0.035f, 0.52f, t);
                    float y = Mathf.Sin(t * Mathf.PI) * (0.29f + index * 0.047f);
                    Vector3 center = new Vector3(x, y, index * 0.008f);
                    Vector3 tangent = new Vector3(1.3f, Mathf.Cos(t * Mathf.PI) * Mathf.PI * (0.29f + index * 0.047f), 0f);
                    Vector3 across = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                    float width = (index == 0 ? 0.14f : 0.027f) * Taper(t, 0.7f) * Mathf.Lerp(0.6f, 1.15f, t);
                    return new RibbonPoint(center, across, width);
                });
            }
            return ribbon.Finish();
        }
    }
}
