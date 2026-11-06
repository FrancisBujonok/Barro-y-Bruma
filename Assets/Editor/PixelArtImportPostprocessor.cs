using UnityEditor;
using UnityEngine;

namespace BarroYBruma.Editor
{
    /// <summary>
    /// Applies pixel-perfect import settings to every texture under <c>Assets/Art/</c>:
    /// single sprite, point filtering, no compression, no mipmaps, 32 pixels per unit.
    /// Art is authored in <c>tools/art/barro_y_bruma.px.js</c> (pxl) at 32 px per tile.
    /// </summary>
    public sealed class PixelArtImportPostprocessor : AssetPostprocessor
    {
        /// <summary>Folder whose textures receive pixel-art settings.</summary>
        public const string ArtRoot = "Assets/Art/";

        /// <summary>Source resolution of one world unit (one floor tile) in pixels.</summary>
        public const float PixelsPerUnit = 32f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
