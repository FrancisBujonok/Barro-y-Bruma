using System;
using UnityEngine;

namespace BrumaPx.World
{
    [Serializable] public sealed class MapSize { public int cols; public int rows; }

    [Serializable]
    public sealed class MapMarker
    {
        public string id;
        public string type;
        public float x;
        public float z;
        public string label;
        public string note;
        public int n = 1;
        public int phase;
    }

    [Serializable] public sealed class MapPlayer { public float x; public float z; }

    [Serializable]
    public sealed class MapFile
    {
        public int cell = 2;
        public MapSize size;
        public string[] terrain;
        public MapMarker[] markers;
        public MapPlayer player;
    }

    /// <summary>
    /// The chapter map read from the JSON the map tool exports. Row 0 of the terrain is the north edge.
    /// Cell (col,row) has its centre at x = xMin + (col+0.5)*cell, z = zMax - (row+0.5)*cell.
    /// </summary>
    public static class MapGrid
    {
        public static MapFile File { get; private set; }
        public static int Cols, Rows;
        public static float Cell = 2f, XMin, ZMax;
        static byte[] _ids;      // terrain id per cell, row-major from the north

        public static bool Loaded { get { return File != null && _ids != null; } }
        public static float XMax { get { return XMin + Cols * Cell; } }
        public static float ZMin { get { return ZMax - Rows * Cell; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { File = null; _ids = null; }

        public static bool Load(string json)
        {
            File = JsonUtility.FromJson<MapFile>(json);
            if (File == null || File.terrain == null || File.size == null) { File = null; return false; }
            Cols = File.size.cols;
            Rows = File.size.rows;
            Cell = File.cell > 0 ? File.cell : 2f;
            XMin = -Cols * Cell * 0.5f;
            ZMax = Rows * Cell * 0.5f;
            _ids = new byte[Cols * Rows];
            for (int r = 0; r < Rows; r++)
            {
                string line = r < File.terrain.Length ? File.terrain[r] : string.Empty;
                for (int c = 0; c < Cols; c++)
                    _ids[r * Cols + c] = Id(c < line.Length ? line[c] : 'l');
            }
            return true;
        }

        // l Llanura b Bosque d Bosque muerto p Pantano a Agua c Camino k Campo e Escarpado m Montana r Ruinas v Aldea
        public static byte Id(char ch)
        {
            switch (ch)
            {
                case 'l': return 0;
                case 'b': return 1;
                case 'd': return 2;
                case 'p': return 3;
                case 'a': return 4;
                case 'c': return 5;
                case 'k': return 6;
                case 'e': return 7;
                case 'm': return 8;
                case 'r': return 9;
                case 'v': return 10;
                default: return 0;
            }
        }

        public static bool IsBlockedId(byte id) { return id == 4 || id == 7 || id == 8; }

        /// <summary>Cave floor: carved into the mountain by CaveLair, walkable. Not a letter of the map file.</summary>
        public const byte CaveId = 11;

        /// <summary>Deck over water (a bridge, a pier): walkable, but the ground shader draws water under it. Not a letter.</summary>
        public const byte WaterDeckId = 12;

        /// <summary>How far below the ground the water surface is drawn (the ground shader uses the same value).</summary>
        public const float WaterDepth = 0.8f;

        /// <summary>Changes the terrain of a cell (before the ground texture is built).</summary>
        public static void SetId(int c, int r, byte id)
        {
            if (!Loaded || !InBounds(c, r)) return;
            _ids[r * Cols + c] = id;
        }

        public static int ColOf(float x) { return Mathf.FloorToInt((x - XMin) / Cell); }
        public static int RowOf(float z) { return Mathf.FloorToInt((ZMax - z) / Cell); }
        public static bool InBounds(int c, int r) { return c >= 0 && r >= 0 && c < Cols && r < Rows; }
        public static float CellX(int c) { return XMin + (c + 0.5f) * Cell; }
        public static float CellZ(int r) { return ZMax - (r + 0.5f) * Cell; }

        /// <summary>Terrain id of a cell; outside the map counts as mountain.</summary>
        public static byte IdAt(int c, int r)
        {
            if (!Loaded || !InBounds(c, r)) return 8;
            return _ids[r * Cols + c];
        }

        public static byte IdAtWorld(float x, float z) { return IdAt(ColOf(x), RowOf(z)); }
        public static bool BlockedAt(float x, float z) { return IsBlockedId(IdAtWorld(x, z)); }

        /// <summary>The whole terrain as a texture for the ground shader: red = id, row 0 at the south (v grows north).</summary>
        public static Texture2D BuildTexture()
        {
            var tex = new Texture2D(Cols, Rows, TextureFormat.R8, false, true);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new byte[Cols * Rows];
            for (int r = 0; r < Rows; r++)
            {
                int y = Rows - 1 - r;
                for (int c = 0; c < Cols; c++) px[y * Cols + c] = _ids[r * Cols + c];
            }
            tex.SetPixelData(px, 0);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Pushes a circle out of the blocked cells (water, cliffs, mountains) and keeps it inside the map.
        /// Used by the hero and the enemies; does nothing while no map is loaded.
        /// </summary>
public static Vector2 Resolve(Vector2 p, float r)
        {
            if (!Loaded) return p;
            for (int pass = 0; pass < 2; pass++)
            {
                int c0 = ColOf(p.x), r0 = RowOf(p.y);
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int c = c0 + dc, rr = r0 + dr;
                        if (InBounds(c, rr) && !IsBlockedId(_ids[rr * Cols + c])) continue;

                        // Every side that faces walkable ground is pulled back by Inset, so the invisible wall sits
                        // inside the blocked cell, where the ragged drawn border (which wanders) still looks blocked.
                        float h = Cell * 0.5f;
                        float cx = CellX(c), cz = CellZ(rr);
                        float minX = cx - h, maxX = cx + h, minZ = cz - h, maxZ = cz + h;
                        if (Walkable(c - 1, rr)) minX += Inset;
                        if (Walkable(c + 1, rr)) maxX -= Inset;
                        if (Walkable(c, rr - 1)) maxZ -= Inset;   // row - 1 is the north neighbour
                        if (Walkable(c, rr + 1)) minZ += Inset;
                        p = PushOutOfRect(p, minX, maxX, minZ, maxZ, r);
                    }
                }
            }
            p.x = Mathf.Clamp(p.x, XMin + r, XMax - r);
            p.y = Mathf.Clamp(p.y, ZMin + r, ZMax - r);
            return p;
        }

static bool Walkable(int c, int r)
        {
            return InBounds(c, r) && !IsBlockedId(_ids[r * Cols + c]);
        }

        const float Inset = 0.5f;

        static Vector2 PushOutOfRect(Vector2 p, float minX, float maxX, float minZ, float maxZ, float r)
        {
            Vector2 clamped = new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, minZ, maxZ));
            Vector2 delta = p - clamped;
            float dist = delta.magnitude;
            if (dist >= r) return p;
            if (dist > 1e-4f) return clamped + delta / dist * r;

            // Centre inside the rectangle: leave through the nearest side.
            float left = p.x - minX, right = maxX - p.x, down = p.y - minZ, up = maxZ - p.y;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
            if (m == left) p.x = minX - r;
            else if (m == right) p.x = maxX + r;
            else if (m == down) p.y = minZ - r;
            else p.y = maxZ + r;
            return p;
        }
    }
}
