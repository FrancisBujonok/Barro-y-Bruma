using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.Characters
{
    /// <summary>
    /// Every layer sheet of one body type. All the sheets share the same grid (columns x rows of square cells), so a
    /// character is drawn by stacking the layers it wears, in order, into one texture.
    /// Grid rows: one row per animation and direction, in the order of <see cref="clips"/>. With 4 views the
    /// directions go up, left, down, right (the Liberated Pixel Cup order). With 8 views (the sheets rendered in
    /// Blender) they go down, down-right, right, up-right, up, up-left, left, down-left.
    /// A clip can also bring its own sheet (frames in columns, one row per view) with its own cell size and feet:
    /// big creatures use that so each animation only takes the room it needs.
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Capas de personaje", fileName = "CharacterLayers")]
    public sealed class CharacterLayerSet : ScriptableObject
    {
        [Serializable]
        public sealed class Layer
        {
            public string id = "capa";
            public Texture2D sheet;
            [Tooltip("Lower numbers are drawn first (behind).")]
            public int order;
        }

        [Serializable]
        public sealed class Clip
        {
            public string name = "idle";
            public int frames = 2;
            [Tooltip("4 = one row per direction, 1 = a single row used for every direction.")]
            public int directions = 4;
            public float fps = 8f;
            public bool loop = true;
            [Tooltip("Optional: a sheet of its own (frames in columns, one row per view). Empty = the shared grid.")]
            public Texture2D sheet;
            [Tooltip("Cell size of that sheet in texels (width, height).")]
            public Vector2Int size;
            [Tooltip("Feet inside a cell of that sheet, texels from the left and from the bottom.")]
            public Vector2 feet;
            [Tooltip("Optional zone map with the same layout as the sheet (wounds, bandages, Bruma).")]
            public Texture2D zoneSheet;
            [Tooltip("Plays the frames backwards (e.g. drawing the sword reuses the sheathe sheet).")]
            public bool reverse;
            [Tooltip("Attacks: frames [hitFrom, hitTo) are the blow itself; before is the wind-up, after the recovery.")]
            public int hitFrom;
            public int hitTo;
        }

        [Header("Grid")]
        [Tooltip("4 = up, left, down, right (LPC). 8 = down, down-right, right, up-right, up, up-left, left, down-left (Blender).")]
        public int views = 4;
        public int cell = 128;
        public int columns = 9;
        public int rows = 13;
        [Tooltip("Where the feet stand inside a cell, in texels from the left and from the bottom.")]
        public Vector2 feet = new Vector2(64f, 36f);

        [Header("Layers")]
        public List<Layer> layers = new List<Layer>();
        [Tooltip("Always drawn: body, head and the clothes worn under any armour.")]
        public string[] baseLayers = new string[0];
        [Tooltip("Base layers hidden while something is worn on the head (hair under a hood).")]
        public string[] hiddenUnderHeadgear = new string[0];
        [Tooltip("Same grid; red channel = zone index * 16 (0 = no zone). Zone 1 is the first of zoneIds.")]
        public Texture2D zoneMap;
        public string[] zoneIds = new string[0];

        [Header("Animation")]
        public List<Clip> clips = new List<Clip>();

        public Layer Find(string id)
        {
            for (int i = 0; i < layers.Count; i++)
                if (layers[i] != null && layers[i].id == id) return layers[i];
            return null;
        }

        /// <summary>Adds every layer that belongs to an item: the one named like it and the ones named "id_something".</summary>
        public void CollectFor(string itemId, List<Layer> into)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            string prefix = itemId + "_";
            for (int i = 0; i < layers.Count; i++)
            {
                Layer l = layers[i];
                if (l == null || l.sheet == null) continue;
                if (l.id == itemId || l.id.StartsWith(prefix, StringComparison.Ordinal)) into.Add(l);
            }
        }

        public int ClipIndex(string clipName)
        {
            for (int i = 0; i < clips.Count; i++)
                if (clips[i].name == clipName) return i;
            return -1;
        }

        /// <summary>True when every clip brings its own sheet, so nothing has to be composed.</summary>
        public bool ClipsHaveSheets
        {
            get
            {
                if (clips.Count == 0) return false;
                for (int i = 0; i < clips.Count; i++) if (clips[i].sheet == null) return false;
                return true;
            }
        }

        /// <summary>
        /// View index for a facing. <paramref name="yawFromCamera"/> is the character's yaw minus the camera's yaw
        /// (0 = looking away from the camera, 90 = to the right of the screen).
        /// </summary>
        public static int ViewFor(float yawFromCamera, int views)
        {
            float a = Mathf.Repeat(yawFromCamera, 360f);
            if (views >= 8) return (int)Mathf.Repeat(Mathf.Round((180f - a) / 45f), 8f);
            if (a < 22.5f || a >= 337.5f) return 0;
            if (a < 157.5f) return 3;
            if (a < 202.5f) return 2;
            return 1;
        }

        /// <summary>Grid row of a clip for a direction (see <see cref="views"/> for the order).</summary>
        public int RowOf(int clipIndex, int direction)
        {
            int row = 0;
            for (int i = 0; i < clipIndex; i++) row += Mathf.Max(1, clips[i].directions);
            return row + (clips[clipIndex].directions > 1 ? Mathf.Clamp(direction, 0, clips[clipIndex].directions - 1) : 0);
        }
    }
}
