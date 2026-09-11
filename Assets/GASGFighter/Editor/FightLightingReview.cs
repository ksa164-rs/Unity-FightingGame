using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GASG.Fighting.Editor
{
    // Unity 6000.3 / URP 17.3。元データを保護する照明検証用ツール。
    public static class FightLightingReview
    {
        private const string ReportFolder = "ReviewValidation/StageLighting";
        private static string pendingScene;
        private static string outputFolder;
        private static T[] Components<T>() where T : Component => SceneManager.GetActiveScene()
            .GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        [MenuItem("GASG/ステージ照明（レビュー）/03. 調整/レビュー用の床奥行きを調整")]
        public static void RefineFloorDepth()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || SceneManager.sceneCount != 1 ||
                    !scene.path.StartsWith("Assets/GASGFighter/LightingReviews/BrightEdges_"))
                    throw new InvalidOperationException("BrightEdges版の編集状態で実行してください。");
                outputFolder = Path.GetDirectoryName(scene.path).Replace('\\', '/');
                var stage = Components<MeshRenderer>().Single(r => r.name == "SF6_TrainingRoom_Optimized");
                var floor = Components<Light>().Single(l => l.name == "Floor_Softbox_Baked");
                Undo.RecordObjects(new UnityEngine.Object[] { floor, floor.transform }, "Refine floor depth");
                floor.areaSize = new Vector2(stage.bounds.size.x * 0.91f, 3.5f);
                floor.transform.position = new Vector3(stage.bounds.center.x, stage.bounds.max.y - 0.6f, stage.bounds.max.z - 2.4f);
                floor.intensity = 3.5f;
                // 反射にだけ明るい天井面を足す。GI・影の光源は既存の面光源2灯が担当する。
                if (!Components<Renderer>().Any(r => r.name == "Ceiling_ReflectionCard"))
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (!shader) throw new InvalidOperationException("URP Unlitシェーダーが見つかりません。");
                    var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    card.name = "Ceiling_ReflectionCard";
                    Undo.RegisterCreatedObjectUndo(card, "Create ceiling reflection card");
                    card.transform.SetParent(floor.transform.parent);
                    card.transform.position = new Vector3(stage.bounds.center.x, stage.bounds.max.y - 0.1f, stage.bounds.max.z - 1.3f);
                    card.transform.rotation = Quaternion.FromToRotation(card.GetComponent<MeshFilter>().sharedMesh.normals[0], Vector3.down);
                    card.transform.localScale = new Vector3(stage.bounds.size.x * 0.9f, 0.8f, 1f);
                    UnityEngine.Object.DestroyImmediate(card.GetComponent<Collider>());
                    var material = new Material(shader) { name = "Ceiling_ReflectionWhite" };
                    material.SetColor("_BaseColor", new Color(3f, 3f, 2.94f, 1f));
                    AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/Ceiling_ReflectionWhite.mat"));
                    var renderer = card.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    GameObjectUtility.SetStaticEditorFlags(card, StaticEditorFlags.ReflectionProbeStatic);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                SaveReviewAssets();
                StartBake();
            }
            catch (Exception e) { Debug.LogError("[Stage Lighting][失敗] " + e); }
        }

        [MenuItem("GASG/ステージ照明（レビュー）/02. 複製/明るい縁取り用コピーを作成")]
        public static void CreateBrightEdgesCopy()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || SceneManager.sceneCount != 1 ||
                    !scene.path.StartsWith("Assets/GASGFighter/LightingReviews/"))
                    throw new InvalidOperationException("調整版シーンを単独で開き、再生とベイクを停止してください。");
                var stage = Components<MeshRenderer>().Single(r => r.name == "SF6_TrainingRoom_Optimized");
                var wall = Components<Light>().Single(l => l.name == "Wall_Softbox_Baked");
                var floor = Components<Light>().Single(l => l.name == "Floor_Softbox_Baked");
                var key = Components<Light>().Single(l => l.name == "Character_Key_Realtime");
                Directory.CreateDirectory(ReportFolder);
                CaptureViews("Previous");
                outputFolder = "Assets/GASGFighter/LightingReviews/BrightEdges_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                EnsureFolder(outputFolder);
                if (!EditorSceneManager.SaveScene(scene, outputFolder + "/Before.unity", true)) throw new IOException("比較元を保存できません。");
                pendingScene = outputFolder + "/LightingReview.unity";
                if (!EditorSceneManager.SaveScene(scene, pendingScene)) throw new IOException("調整コピーを保存できません。");

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Bright stage edges");
                Undo.RecordObjects(new UnityEngine.Object[] { wall, floor, key, wall.transform, floor.transform, stage }, "Refine lighting");
                var bounds = stage.bounds;
                // 横幅を部屋全体に広げ、中央だけに集中していた照明を左右まで回す。
                wall.areaSize = new Vector2(bounds.size.x * 0.93f, 2.4f);
                wall.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.65f, bounds.max.z - 3.5f);
                wall.transform.rotation = Quaternion.LookRotation(new Vector3(bounds.center.x, bounds.max.y - 2f, bounds.max.z) - wall.transform.position);
                wall.intensity = 5.3f;
                floor.areaSize = new Vector2(bounds.size.x * 0.91f, 7f);
                floor.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.8f, bounds.max.z - 4.2f);
                floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                floor.intensity = 2.6f;
                wall.color = floor.color = new Color(1f, 0.995f, 0.975f);
                key.color = new Color(1f, 0.99f, 0.97f);
                key.intensity = 1.1f;
                key.shadowStrength = 0.85f;

                var mats = stage.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]) { name = "Stage_Surface_" + i };
                    bool isWall = mats[i].name.Contains("Wall");
                    m.SetFloat("_Smoothness", isWall ? 0.14f : 0.38f);
                    AssetDatabase.CreateAsset(m, outputFolder + "/" + m.name + ".mat");
                    mats[i] = m;
                }
                stage.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
                var lighting = UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);
                lighting.name = "BrightEdges_Lighting";
                lighting.lightmapResolution = 20f;
                lighting.directSampleCount = 64;
                lighting.indirectSampleCount = 256;
                AssetDatabase.CreateAsset(lighting, outputFolder + "/BrightEdges_Lighting.lighting");
                Lightmapping.lightingSettings = lighting;
                foreach (var p in Components<ReflectionProbe>())
                {
                    if (p.name != "Stage_Reflection_Baked") continue;
                    Undo.RecordObject(p, "Refine floor reflections");
                    p.resolution = 256;
                    p.intensity = 0.8f;
                }
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                SaveReviewAssets();
                File.WriteAllText(ReportFolder + "/OutputPath.txt", pendingScene);
                StartBake();
            }
            catch (Exception e) { Debug.LogError("[Stage Lighting][失敗] " + e); }
        }

        // 比較画像用にカメラを一時移動し、必ず元に戻す。シーンへの保存は行わない。
        private static void CaptureViews(string prefix)
        {
            var camera = Components<Camera>().First(c => c.CompareTag("MainCamera"));
            var controller = camera.GetComponent<FightCameraController>();
            var settings = controller ? controller.Settings : null;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float fov = camera.fieldOfView;
            var characters = Components<SkinnedMeshRenderer>();
            var enabledStates = characters.Select(r => r.enabled).ToArray();
            try
            {
                foreach (var view in new[] { ("Center", 0f), ("Left", -6f), ("Right", 6f) })
                {
                    // 端の画像は背景確認用。見切れるTポーズを一時的に非表示にする。
                    for (int i = 0; i < characters.Length; i++) characters[i].enabled = enabledStates[i] && view.Item1 == "Center";
                    Vector3 target = new Vector3(view.Item2, settings ? settings.LookAtHeight : 1.26f, 0f);
                    float distance = settings ? Mathf.Clamp(settings.BaseDistance + 2.5f * settings.DistancePerSeparation, settings.BaseDistance, settings.MaximumDistance) : 6.265f;
                    camera.transform.position = target + new Vector3(settings ? settings.HorizontalOffset : 0f, settings ? settings.CameraHeight : 0.5f, -distance);
                    camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position);
                    camera.fieldOfView = settings ? settings.FieldOfView : 35f;
                    Capture(prefix + "_" + view.Item1 + ".png");
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov;
                for (int i = 0; i < characters.Length; i++) characters[i].enabled = enabledStates[i];
            }
        }

        [MenuItem("GASG/ステージ照明（レビュー）/01. 確認（変更なし）/現在のシーンを確認")]
        public static void Inspect()
        {
            Directory.CreateDirectory(ReportFolder);
            var s = new StringBuilder();
            s.AppendLine($"Scene: {SceneManager.GetActiveScene().path}; dirty={SceneManager.GetActiveScene().isDirty}");
            foreach (var r in Components<Renderer>())
            {
                s.AppendLine($"Renderer {r.name}: bounds={r.bounds}; materials={string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null"))}");
                var f = r.GetComponent<MeshFilter>();
                if (f && f.sharedMesh) s.AppendLine($"Mesh {f.sharedMesh.name}: vertices={f.sharedMesh.vertexCount} uv2={f.sharedMesh.uv2.Length}");
            }
            foreach (var l in Components<Light>()) s.AppendLine($"Light {l.name}: enabled={l.isActiveAndEnabled} type={l.type} position={l.transform.position} rotation={l.transform.eulerAngles} intensity={l.intensity}");
            foreach (var c in Components<Camera>()) s.AppendLine($"Camera {c.name}: position={c.transform.position} rotation={c.transform.eulerAngles} ortho={c.orthographic} size={c.orthographicSize}");
            File.WriteAllText(Path.Combine(ReportFolder, "Inspection.txt"), s.ToString());
            Capture("Before.png");
            Debug.Log("[Stage Lighting][成功] 読み取り検証と比較画像を保存しました。");
        }

        [MenuItem("GASG/ステージ照明（レビュー）/02. 複製/コピーを作成してベイク")]
        public static void CreateCopyAndBake()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || SceneManager.sceneCount != 1)
                    throw new InvalidOperationException("再生・ベイクを停止し、対象シーンだけを開いてください。");
                var stage = Components<MeshRenderer>().SingleOrDefault(r => r.name == "SF6_TrainingRoom_Optimized");
                if (!stage || !stage.GetComponent<MeshFilter>()?.sharedMesh)
                    throw new InvalidOperationException("対象ステージのメッシュが見つかりません。");
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                    throw new InvalidOperationException("このツールは URP 用です。");

                // 未保存の編集状態も別名で保護し、元のシーンファイルには保存しない。
                outputFolder = "Assets/GASGFighter/LightingReviews/Stage_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                EnsureFolder(outputFolder);
                var scene = SceneManager.GetActiveScene();
                if (!EditorSceneManager.SaveScene(scene, outputFolder + "/Before.unity", true))
                    throw new IOException("比較用シーンの保存に失敗しました。");
                pendingScene = outputFolder + "/LightingReview.unity";
                if (!EditorSceneManager.SaveScene(scene, pendingScene)) throw new IOException("調整用コピーの保存に失敗しました。");

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Stage lighting prototype");
                foreach (var l in Components<Light>())
                {
                    Undo.RecordObject(l, "Disable original light in review");
                    l.enabled = false;
                }

                // 元FBXにはUV2がないため、複製したメッシュにだけライトマップUVを作る。
                var filter = stage.GetComponent<MeshFilter>();
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                mesh.name = "TrainingRoom_LightmapUV";
                if (!Unwrapping.GenerateSecondaryUVSet(mesh)) throw new InvalidOperationException("UV2生成に失敗しました。");
                AssetDatabase.CreateAsset(mesh, outputFolder + "/TrainingRoom_LightmapUV.asset");
                Undo.RecordObject(filter, "Assign review mesh");
                filter.sharedMesh = mesh;
                Undo.RecordObject(stage, "Configure baked stage");
                var mats = stage.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]) { name = mats[i].name + "_Review_" + i };
                    // グリッド等の既存テクスチャは保持し、床と壁の光沢を分ける。
                    bool wall = m.name.Contains("Wall");
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", wall ? 0.12f : 0.3f);
                    AssetDatabase.CreateAsset(m, outputFolder + "/" + m.name + ".mat");
                    mats[i] = m;
                }
                stage.sharedMaterials = mats;
                GameObjectUtility.SetStaticEditorFlags(stage.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(stage.gameObject) | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                stage.receiveGI = ReceiveGI.Lightmaps;
                stage.scaleInLightmap = 1f;

                var bounds = stage.bounds;
                var rig = new GameObject("StageLighting_Review");
                Undo.RegisterCreatedObjectUndo(rig, "Create lighting rig");
                float wallZ = bounds.max.z;
                float floorY = bounds.min.y;
                float height = bounds.size.y;
                float cx = bounds.center.x;
                CreateArea(rig.transform, "Wall_Softbox_Baked", new Vector3(cx, floorY + height * 0.88f, wallZ - 3.2f),
                    new Vector3(cx, floorY + height * 0.6f, wallZ), new Vector2(bounds.size.x * 0.45f, height * 0.3f), 8f);
                CreateArea(rig.transform, "Floor_Softbox_Baked", new Vector3(cx, floorY + height * 0.8f, wallZ - 4.5f),
                    new Vector3(cx, floorY, wallZ - 2f), new Vector2(bounds.size.x * 0.65f, 4f), 4f);
                var key = new GameObject("Character_Key_Realtime").AddComponent<Light>();
                key.transform.SetParent(rig.transform);
                key.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
                key.type = LightType.Directional;
                key.lightmapBakeType = LightmapBakeType.Realtime;
                key.color = new Color(1f, 0.97f, 0.91f);
                key.intensity = 0.65f;
                key.shadows = LightShadows.Soft;
                key.shadowStrength = 0.65f;
                key.shadowBias = 0.025f;
                key.shadowNormalBias = 0.15f;
                key.gameObject.AddComponent<UniversalAdditionalLightData>();
                RenderSettings.sun = key;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.34f);
                RenderSettings.reflectionIntensity = 0.65f;

                var probes = new GameObject("Character_LightProbes").AddComponent<LightProbeGroup>();
                probes.transform.SetParent(rig.transform);
                var positions = new System.Collections.Generic.List<Vector3>();
                for (int x = -4; x <= 4; x++)
                    foreach (float y in new[] { 0.25f, 1.4f, 3f, 4.5f })
                        foreach (float z in new[] { -2f, 0f, 2f }) positions.Add(new Vector3(cx + x * 2f, floorY + y, z));
                probes.probePositions = positions.ToArray();
                foreach (var r in Components<SkinnedMeshRenderer>()) r.lightProbeUsage = LightProbeUsage.BlendProbes;

                var reflection = new GameObject("Stage_Reflection_Baked").AddComponent<ReflectionProbe>();
                reflection.transform.SetParent(rig.transform);
                reflection.transform.position = new Vector3(cx, floorY + 2f, 0f);
                reflection.mode = ReflectionProbeMode.Baked;
                reflection.resolution = 128;
                reflection.boxProjection = true;
                reflection.size = bounds.size + Vector3.one * 2f;
                reflection.center = bounds.center - reflection.transform.position;
                reflection.intensity = 0.65f;

                // Volumeも複製してから調整し、既存プロファイルを変更しない。
                int volumeIndex = 0;
                foreach (var volume in Components<Volume>())
                {
                    if (!volume.sharedProfile) continue;
                    string profilePath = outputFolder + "/Volume_" + volumeIndex++ + ".asset";
                    string sourcePath = AssetDatabase.GetAssetPath(volume.sharedProfile);
                    if (!AssetDatabase.CopyAsset(sourcePath, profilePath)) throw new IOException("Volume複製に失敗しました。");
                    volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                    if (volume.sharedProfile.TryGet<MotionBlur>(out var blur)) { blur.active = false; EditorUtility.SetDirty(blur); }
                    if (volume.sharedProfile.TryGet<Bloom>(out var bloom)) { bloom.intensity.Override(0.08f); EditorUtility.SetDirty(bloom); }
                }
                var settings = new LightingSettings
                {
                    name = "StageLighting_Preview", bakedGI = true, realtimeGI = false,
                    lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
                    lightmapResolution = 12f, lightmapMaxSize = 1024,
                    directSampleCount = 32, indirectSampleCount = 128, environmentSampleCount = 32,
                    maxBounces = 3, ao = true, aoMaxDistance = 0.25f, aoExponentIndirect = 0.6f
                };
                AssetDatabase.CreateAsset(settings, outputFolder + "/StageLighting_Preview.lighting");
                Lightmapping.lightingSettings = settings;
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                SaveReviewAssets();
                Selection.activeGameObject = rig;
                File.WriteAllText(ReportFolder + "/OutputPath.txt", pendingScene);
                StartBake();
            }
            catch (Exception e) { Debug.LogError("[Stage Lighting][失敗] " + e); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void SaveReviewAssets()
        {
            // 別作業中の未保存アセットまで保存しないよう、今回の出力に限定する。
            foreach (string guid in AssetDatabase.FindAssets("", new[] { outputFolder }))
                AssetDatabase.SaveAssetIfDirty(new GUID(guid));
        }

        private static void CreateArea(Transform parent, string name, Vector3 position, Vector3 target, Vector2 size, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent);
            light.transform.position = position;
            light.transform.rotation = Quaternion.LookRotation(target - position);
            light.type = LightType.Rectangle;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.areaSize = size;
            light.color = new Color(1f, 0.97f, 0.89f);
            light.intensity = intensity;
            light.range = 25f;
            light.shadows = LightShadows.Soft;
        }

        [MenuItem("GASG/ステージ照明（レビュー）/04. ベイク/レビュー用コピーを再ベイク")]
        public static void StartBake()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith("Assets/GASGFighter/LightingReviews/") || Lightmapping.isRunning)
            { Debug.LogWarning("[Stage Lighting][スキップ] 調整シーン以外、またはベイク中です。"); return; }
            pendingScene = scene.path;
            outputFolder = Path.GetDirectoryName(scene.path).Replace('\\', '/');
            Directory.CreateDirectory(ReportFolder);
            Lightmapping.bakeCompleted -= BakeCompleted;
            Lightmapping.bakeCompleted += BakeCompleted;
            File.WriteAllText(ReportFolder + "/BakeStatus.txt", "Baking " + pendingScene);
            if (!Lightmapping.BakeAsync())
            {
                Lightmapping.bakeCompleted -= BakeCompleted;
                File.WriteAllText(ReportFolder + "/BakeStatus.txt", "Failed to start");
                Debug.LogError("[Stage Lighting][失敗] ベイクを開始できませんでした。");
            }
        }

        private static void BakeCompleted()
        {
            Lightmapping.bakeCompleted -= BakeCompleted;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (SceneManager.GetActiveScene().path != pendingScene) throw new InvalidOperationException("ベイク中にシーンが変わりました。");
                    var maps = LightmapSettings.lightmaps;
                    if (maps.Length == 0 || !maps[0].lightmapColor) throw new InvalidOperationException("ライトマップが生成されていません。");
                    var probe = Components<ReflectionProbe>().FirstOrDefault(p => p.name == "Stage_Reflection_Baked");
                    bool reflectionOk = probe && Lightmapping.BakeReflectionProbe(probe, AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/StageReflection.exr"));
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    SaveReviewAssets();
                    Capture("After.png");
                    CaptureViews("Current");
                    File.WriteAllText(ReportFolder + "/BakeStatus.txt", $"Completed: lightmaps={maps.Length}; reflection={reflectionOk}; scene={pendingScene}");
                    Debug.Log("[Stage Lighting][成功] ベイク・保存・比較画像の出力が完了しました。");
                }
                catch (Exception e)
                {
                    File.WriteAllText(ReportFolder + "/BakeStatus.txt", "Failed: " + e);
                    Debug.LogError("[Stage Lighting][失敗] " + e);
                }
            };
        }

        private static void Capture(string name)
        {
            var camera = Components<Camera>().FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (!camera) throw new InvalidOperationException("MainCamera が見つかりません。");
            Directory.CreateDirectory(ReportFolder);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(ReportFolder, name), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(texture);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
