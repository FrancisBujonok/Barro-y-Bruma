using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace BarroYBruma.Editor
{
    /// <summary>
    /// Builds a throwaway look-test scene for the base art: an ash-cobble floor on a
    /// Tilemap, the player sprite in the middle, a pixel-perfect camera and a dim
    /// global 2D light. Run it from the menu <c>Barro y Bruma/Build Ash Test Scene</c>;
    /// re-running overwrites the scene and the tile asset.
    /// </summary>
    public static class AshTestSceneBuilder
    {
        /// <summary>Where the generated scene is saved.</summary>
        public const string ScenePath = "Assets/Scenes/AshTestScene.unity";

        /// <summary>Tile assets are created as <c>FloorAshCobble_{n}.asset</c> in this folder.</summary>
        public const string FloorTileFolder = "Assets/Art/Tiles";

        /// <summary>Floor variant sprites (<c>FloorAshCobble_{n}.png</c>) exported from <c>tools/art/barro_y_bruma.px.js</c>.</summary>
        public const string FloorSpriteFolder = "Assets/Art/Sprites/Tiles";

        /// <summary>Player sprite exported from <c>tools/art/barro_y_bruma.px.js</c>.</summary>
        public const string PlayerSpritePath = "Assets/Art/Sprites/Characters/Player.png";

        /// <summary>Contact shadow drawn under the player.</summary>
        public const string PlayerShadowSpritePath = "Assets/Art/Sprites/Characters/PlayerShadow.png";

        // Look-test values only; gameplay tuning lives elsewhere once those systems exist.
        private const int FloorWidth = 24;
        private const int FloorHeight = 14;
        // Relative frequency of each floor variant (index = variant). Plain floor dominates.
        private static readonly int[] FloorVariantWeights = { 6, 2, 2, 1 };
        private const int FloorSeed = 1729;       // fixed so every rebuild gives the same layout
        private const int ReferenceWidth = 640;   // 20 x 11.25 tiles visible at 32 PPU
        private const int ReferenceHeight = 360;
        private static readonly Color SootBackground = new Color32(0x14, 0x12, 0x10, 0xFF);
        // Near-neutral: the palette already carries the gloom; a dark tint on top crushed it.
        private static readonly Color AshLight = new Color32(0xEC, 0xE8, 0xDF, 0xFF);
        private const float AshLightIntensity = 1f;
        private static readonly Color ShadowTint = new Color(1f, 1f, 1f, 0.45f);
        private const float ShadowOffsetY = -0.42f; // feet sit near the bottom of the 1-unit sprite

        /// <summary>Creates (or overwrites) the look-test scene and opens it.</summary>
        [MenuItem("Barro y Bruma/Build Ash Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // NewScene(Single) unloads unused assets, which destroys any asset loaded before
            // it (the tile came back null and the floor saved empty). Load everything after.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerShadowSpritePath);
            var floorTiles = new Tile[FloorVariantWeights.Length];
            for (int i = 0; i < floorTiles.Length; i++)
            {
                string spritePath = $"{FloorSpriteFolder}/FloorAshCobble_{i}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogError($"[AshTestScene] Missing floor sprite {spritePath}. Reimport Assets/Art and retry.");
                    return;
                }
                floorTiles[i] = CreateOrUpdateTile($"{FloorTileFolder}/FloorAshCobble_{i}.asset", sprite);
            }
            if (playerSprite == null || shadowSprite == null)
            {
                Debug.LogError($"[AshTestScene] Missing sprite(s). Player: {playerSprite != null} ({PlayerSpritePath}), " +
                               $"Shadow: {shadowSprite != null} ({PlayerShadowSpritePath}). Reimport Assets/Art and retry.");
                return;
            }

            CreateCamera();
            CreateGlobalLight();
            CreateFloor(floorTiles);
            CreatePlayer(playerSprite, shadowSprite);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[AshTestScene] Built {ScenePath}. Press Play or look at the Game view.");
        }

        private static Tile CreateOrUpdateTile(string path, Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
            return tile;
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SootBackground;

            var ppc = go.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = (int)PixelArtImportPostprocessor.PixelsPerUnit;
            ppc.refResolutionX = ReferenceWidth;
            ppc.refResolutionY = ReferenceHeight;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            // Always frame exactly the reference view; a small window letterboxes instead of
            // showing extra world at 1x.
            ppc.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
        }

        private static void CreateGlobalLight()
        {
            var go = new GameObject("Global Light 2D (ash haze)");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = AshLight;
            light.intensity = AshLightIntensity;
        }

        private static void CreateFloor(Tile[] variants)
        {
            int totalWeight = 0;
            foreach (int w in FloorVariantWeights) totalWeight += w;
            var rng = new System.Random(FloorSeed);

            var grid = new GameObject("Grid").AddComponent<Grid>();
            var floorGo = new GameObject("Floor");
            floorGo.transform.SetParent(grid.transform, false);
            var tilemap = floorGo.AddComponent<Tilemap>();
            var renderer = floorGo.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -100;

            for (int y = -FloorHeight / 2; y < FloorHeight / 2; y++)
            for (int x = -FloorWidth / 2; x < FloorWidth / 2; x++)
            {
                int roll = rng.Next(totalWeight), v = 0;
                while (roll >= FloorVariantWeights[v]) roll -= FloorVariantWeights[v++];
                tilemap.SetTile(new Vector3Int(x, y, 0), variants[v]);
            }

            int placed = tilemap.GetUsedTilesCount();
            if (placed == 0) Debug.LogError("[AshTestScene] Floor tilemap is empty after filling it.");
            else Debug.Log($"[AshTestScene] Floor: {placed} tile type(s), {FloorWidth}x{FloorHeight} cells.");
        }

        private static void CreatePlayer(Sprite sprite, Sprite shadow)
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0.5f, 0.5f, 0f);  // centred on a tile
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 10;

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, ShadowOffsetY, 0f);
            var shadowSr = shadowGo.AddComponent<SpriteRenderer>();
            shadowSr.sprite = shadow;
            shadowSr.color = ShadowTint;
            shadowSr.sortingOrder = 9;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
