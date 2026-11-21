using UnityEditor;
using UnityEngine;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Import settings for the layered character sheets under Assets/Characters/: plain textures (not sprites),
    /// point filtering, no compression, no mipmaps, original size. Files whose name starts with "_zonas" hold the
    /// body-zone index in the red channel, so they are imported as data (no sRGB).
    /// </summary>
    public sealed class CharacterSheetImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Characters/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.isReadable = false;
            importer.sRGBTexture = !System.IO.Path.GetFileName(assetPath).StartsWith("_zonas");
        }
    }
}
