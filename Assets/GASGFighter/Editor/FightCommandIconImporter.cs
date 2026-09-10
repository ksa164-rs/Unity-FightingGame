using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    public sealed class FightCommandIconImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/GASGFighter/Resources/CommandIcons/", System.StringComparison.Ordinal)) return;
            // 初回取り込みのみ設定する。後からInspectorで256等へ調整できる。
            if (!assetImporter.importSettingsMissing) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 128;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            Debug.Log($"[GASG Fighter][成功] コマンド画像を128pxで設定: {assetPath}");
        }
    }
}
