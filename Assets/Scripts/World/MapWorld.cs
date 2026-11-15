using System;
using System.Collections.Generic;
using System.Reflection;
using BrumaPx.Data;
using BrumaPx.Dialogue;
using BrumaPx.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.World
{
    /// <summary>
    /// Builds the chapter world from the map JSON while the game runs: the ground shader reads the terrain, and each
    /// 16 m chunk is generated the first time the hero gets near (trees, rocks and ruins by terrain type, then the
    /// things the markers ask for: enemies, loot, refuges, NPCs, points of interest). Chunks far from the hero are
    /// switched off, like the old ChunkStreamer did. Prototype: everything is placeholder art.
    /// </summary>
    public sealed class MapWorld : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public GameObject prefab;
        }

        public TextAsset mapJson;
        public Transform chunkRoot;
        public Text label;
        public float activateRadius = 26f;
        public Entry[] prefabs;
        public Material wood, woodDark, glow, stone;
        public ItemDef[] items;
        public ArmorDef[] armors;

        const float ChunkSize = 16f;

        readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        readonly Dictionary<Vector2Int, Transform> _chunks = new Dictionary<Vector2Int, Transform>();
        readonly List<ChunkInfo> _chunkList = new List<ChunkInfo>();
        readonly Dictionary<Vector2Int, List<MapMarker>> _byChunk = new Dictionary<Vector2Int, List<MapMarker>>();
        readonly List<Vector3> _clear = new List<Vector3>();   // x, z, radius where scatter must stay away
        readonly List<Vector3> _big = new List<Vector3>();     // big props already placed
        readonly List<MapMarker> _zones = new List<MapMarker>();
        readonly List<MapMarker> _exits = new List<MapMarker>();
        readonly Dictionary<string, int> _prendaIndex = new Dictionary<string, int>();
        readonly HashSet<string> _exitShown = new HashSet<string>();

        Transform _hero;
        Texture2D _tex;
        string _zoneName = string.Empty;
        bool _first = true;
        int _chunkMinX, _chunkMaxX, _chunkMinZ, _chunkMaxZ;

        // Temporary test switch (F8): no fog island, so the whole screen can be looked at. Not for the final game.
        public static bool FreeView;
        const float FreeViewRadius = 34f;
        Transform _mistLow, _mistHigh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFreeView() { FreeView = false; }

        void HandleFreeViewKey()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.f8Key.wasPressedThisFrame) return;
            FreeView = !FreeView;
            BrumaPx.Rendering.PixelDioramaRig.NoIsland = FreeView;
            if (_mistLow == null) { var g = GameObject.Find("Mist Low"); if (g != null) _mistLow = g.transform; }
            if (_mistHigh == null) { var g = GameObject.Find("Mist High"); if (g != null) _mistHigh = g.transform; }
            if (_mistLow != null) _mistLow.gameObject.SetActive(!FreeView);
            if (_mistHigh != null) _mistHigh.gameObject.SetActive(!FreeView);
            Banner(FreeView ? "Vista libre: niebla apagada (F8)" : "Niebla activada (F8)", 2.5f);
        }

        static string _banner = string.Empty;
        static float _bannerUntil;

        public static void Banner(string text, float seconds)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + seconds;
        }

        // ------------------------------------------------------------------------------------------ setup

        void Awake()
        {
            if (mapJson == null || !MapGrid.Load(mapJson.text))
            {
                Debug.LogError("MapWorld: no se pudo leer el mapa.");
                enabled = false;
                return;
            }

            foreach (var e in prefabs) if (e != null && e.prefab != null) _prefabs[e.key] = e.prefab;

            _tex = MapGrid.BuildTexture();
            Shader.SetGlobalTexture("_PP_MapTex", _tex);
            Shader.SetGlobalVector("_PP_MapInfo", new Vector4(MapGrid.XMin, MapGrid.ZMin, MapGrid.Cols, MapGrid.Rows));

            for (int i = chunkRoot.childCount - 1; i >= 0; i--) Destroy(chunkRoot.GetChild(i).gameObject);

            _chunkMinX = Mathf.RoundToInt(MapGrid.XMin / ChunkSize) + 1 - 1;
            _chunkMaxX = Mathf.RoundToInt(MapGrid.XMax / ChunkSize) - 1 + 1;
            _chunkMinZ = Mathf.RoundToInt(MapGrid.ZMin / ChunkSize);
            _chunkMaxZ = Mathf.RoundToInt(MapGrid.ZMax / ChunkSize);
            _chunkMinX = Mathf.FloorToInt((MapGrid.XMin + 8f) / ChunkSize);
            _chunkMaxX = Mathf.FloorToInt((MapGrid.XMax - 8f) / ChunkSize);
            _chunkMinZ = Mathf.FloorToInt((MapGrid.ZMin + 8f) / ChunkSize);
            _chunkMaxZ = Mathf.FloorToInt((MapGrid.ZMax - 8f) / ChunkSize);

            int prenda = 0;
            foreach (var m in MapGrid.File.markers)
            {
                var key = ChunkOf(m.x, m.z);
                List<MapMarker> list;
                if (!_byChunk.TryGetValue(key, out list)) { list = new List<MapMarker>(); _byChunk[key] = list; }
                list.Add(m);

                float r = ClearRadius(m);
                if (r > 0f) _clear.Add(new Vector3(m.x, m.z, r));
                if (m.type == "zona") _zones.Add(m);
                if (m.type == "salida") _exits.Add(m);
                if (m.type == "prenda") _prendaIndex[m.id] = prenda++;
            }
        }

        static float ClearRadius(MapMarker m)
        {
            switch (m.type)
            {
                case "start": return 4f;
                case "carruaje": return 3f;
                case "aldea": return 7f;
                case "salida": return 4.5f;
                case "refugio": return 6f;
                case "poi": return 5.5f;
                case "npc": return 2.5f;
                case "cofre": case "hierbas": case "venda": case "prenda": return 1.6f;
                case "zona": return 0f;
                default: return 2.6f;   // enemies
            }
        }

        static Vector2Int ChunkOf(float x, float z)
        {
            return new Vector2Int(Mathf.RoundToInt(x / ChunkSize), Mathf.RoundToInt(z / ChunkSize));
        }

        void Start()
        {
            var h = FindFirstObjectByType<DioramaHero>();
            if (h == null) return;
            _hero = h.transform;
            h.worldLimit = 1000f;

            var pin = MapGrid.File.player;
            float px = pin != null ? pin.x : -184f;
            float pz = pin != null ? pin.z : -18f;
            _hero.position = new Vector3(px, 0f, pz);
            _hero.rotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // ------------------------------------------------------------------------------------------ streaming

        void Update()
        {
            if (_hero == null) return;
            HandleFreeViewKey();

            Vector3 f = _hero.position;
            float radius = FreeView ? FreeViewRadius : activateRadius * Mathf.Max(1f, HeroBody.VisionScale);
            int span = Mathf.CeilToInt(radius / ChunkSize) + 1;
            Vector2Int cc = ChunkOf(f.x, f.z);
            int budget = _first ? 200 : 3;
            _first = false;

            for (int cz = cc.y - span; cz <= cc.y + span; cz++)
            {
                for (int cx = cc.x - span; cx <= cc.x + span; cx++)
                {
                    if (cx < _chunkMinX || cx > _chunkMaxX || cz < _chunkMinZ || cz > _chunkMaxZ) continue;
                    var key = new Vector2Int(cx, cz);
                    if (_chunks.ContainsKey(key)) continue;
                    float dx = cx * ChunkSize - f.x, dz = cz * ChunkSize - f.z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) > radius) continue;
                    if (budget-- <= 0) break;
                    Generate(cx, cz);
                }
            }

            int active = 0;
            for (int i = 0; i < _chunkList.Count; i++)
            {
                Vector3 d = _chunkList[i].center - f;
                d.y = 0f;
                bool on = d.magnitude <= radius;
                if (_chunkList[i].gameObject.activeSelf != on) _chunkList[i].gameObject.SetActive(on);
                if (on) active++;
            }

            TrackPlaces(f);

            if (label != null)
            {
                label.text = "x " + Mathf.RoundToInt(f.x) + "  z " + Mathf.RoundToInt(f.z) + "  " + TerrainName(MapGrid.IdAtWorld(f.x, f.z))
                    + "\nchunks: " + active + " / " + _chunkList.Count;
            }
        }

        static string TerrainName(byte id)
        {
            string[] n = { "llanura", "bosque", "bosque muerto", "pantano", "agua", "camino", "campo", "escarpado", "montana", "ruinas", "aldea" };
            return id < n.Length ? n[id] : "?";
        }

        void TrackPlaces(Vector3 f)
        {
            MapMarker best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _zones.Count; i++)
            {
                float dx = _zones[i].x - f.x, dz = _zones[i].z - f.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bestD) { bestD = d; best = _zones[i]; }
            }
            if (best != null && bestD < 85f && best.label != _zoneName)
            {
                _zoneName = best.label;
                Banner(best.label, 3.5f);
            }

            for (int i = 0; i < _exits.Count; i++)
            {
                var m = _exits[i];
                float dx = m.x - f.x, dz = m.z - f.z;
                if (dx * dx + dz * dz > 3.5f * 3.5f || _exitShown.Contains(m.id)) continue;
                _exitShown.Add(m.id);
                Banner(m.label + " - fin del prototipo del capitulo 1", 6f);
            }
        }

        // ------------------------------------------------------------------------------------------ chunk generation

        void Generate(int cx, int cz)
        {
            var key = new Vector2Int(cx, cz);
            var go = new GameObject("Chunk " + cx + "," + cz);
            go.transform.SetParent(chunkRoot, false);
            var info = go.AddComponent<ChunkInfo>();
            info.center = new Vector3(cx * ChunkSize, 0f, cz * ChunkSize);
            _chunks[key] = go.transform;
            _chunkList.Add(info);

            var rng = new System.Random(cx * 73856093 ^ cz * 19349663 ^ 20261115);

            List<MapMarker> ms;
            if (_byChunk.TryGetValue(key, out ms))
                for (int i = 0; i < ms.Count; i++) SpawnMarker(go.transform, ms[i], rng);

            Scatter(go.transform, cx, cz, rng);
        }

        float R(System.Random rng) { return (float)rng.NextDouble(); }

        void Scatter(Transform parent, int cx, int cz, System.Random rng)
        {
            int c0 = Mathf.RoundToInt((cx * ChunkSize - 8f - MapGrid.XMin) / MapGrid.Cell);
            int r0 = Mathf.RoundToInt((MapGrid.ZMax - (cz * ChunkSize + 8f)) / MapGrid.Cell);
            int cells = Mathf.RoundToInt(ChunkSize / MapGrid.Cell);

            for (int r = r0; r < r0 + cells; r++)
            {
                for (int c = c0; c < c0 + cells; c++)
                {
                    if (!MapGrid.InBounds(c, r)) continue;
                    byte id = MapGrid.IdAt(c, r);
                    float x = MapGrid.XMin + c * MapGrid.Cell;
                    float zTop = MapGrid.ZMax - r * MapGrid.Cell;

                    if (id == 7 || id == 8) { Barrier(parent, rng, c, r, x, zTop); continue; }
                    if (id == 4 || id == 5) continue;

                    switch (id)
                    {
                        case 0:
                            Roll(parent, rng, "Tree", 0.025f, x, zTop, 0.9f, 0.9f, 1.3f, true);
                            Roll(parent, rng, "RockMid", 0.035f, x, zTop, 0.7f, 0.7f, 1.2f, true);
                            Roll(parent, rng, "Cross", 0.004f, x, zTop, 1.4f, 1f, 1f, true);
                            break;
                        case 1:
                            Roll(parent, rng, "Tree", 0.30f, x, zTop, 0.9f, 0.9f, 1.5f, true);
                            Roll(parent, rng, "RockSmall", 0.02f, x, zTop, 0.5f, 0.8f, 1.2f, false);
                            break;
                        case 2:
                            Roll(parent, rng, "Tree", 0.16f, x, zTop, 0.9f, 0.8f, 1.2f, true);
                            Roll(parent, rng, "RockMid", 0.03f, x, zTop, 0.7f, 0.7f, 1.1f, true);
                            Roll(parent, rng, "Cross", 0.004f, x, zTop, 1.4f, 1f, 1f, true);
                            break;
                        case 3:
                            Roll(parent, rng, "Tree", 0.07f, x, zTop, 0.9f, 0.8f, 1.2f, true);
                            Roll(parent, rng, "RockSmall", 0.02f, x, zTop, 0.5f, 0.8f, 1.1f, false);
                            Roll(parent, rng, "Charco", 0.012f, x, zTop, 1.4f, 1f, 1f, false);
                            Roll(parent, rng, "Foco", 0.004f, x, zTop, 3.5f, 1f, 1f, false);
                            break;
                        case 6:
                            Roll(parent, rng, "Fence", 0.04f, x, zTop, 2.0f, 1f, 1f, true);
                            Roll(parent, rng, "Tree", 0.008f, x, zTop, 0.9f, 0.8f, 1.1f, true);
                            break;
                        case 9:
                            Roll(parent, rng, "RuinedHut", 0.05f, x, zTop, 3.6f, 1f, 1f, false);
                            Roll(parent, rng, "StoneA", 0.12f, x, zTop, 0.8f, 0.8f, 1.1f, false);
                            Roll(parent, rng, "RockMid", 0.08f, x, zTop, 0.8f, 0.7f, 1.1f, false);
                            Roll(parent, rng, "Cross", 0.05f, x, zTop, 1.4f, 1f, 1f, false);
                            break;
                        case 10:
                            Roll(parent, rng, "RuinedHut", 0.06f, x, zTop, 3.6f, 1f, 1f, false);
                            Roll(parent, rng, "Hut", 0.02f, x, zTop, 3.8f, 1f, 1f, false);
                            Roll(parent, rng, "Lantern", 0.02f, x, zTop, 0.8f, 1f, 1f, false);
                            Roll(parent, rng, "StoneA", 0.02f, x, zTop, 0.8f, 0.8f, 1f, false);
                            break;
                    }
                }
            }
        }

        // Cliffs and mountains block the hero; big rocks along the edge make the wall readable.
        void Barrier(Transform parent, System.Random rng, int c, int r, float x, float zTop)
        {
            bool edge = !MapMath.IsBlocked(c + 1, r) || !MapMath.IsBlocked(c - 1, r) || !MapMath.IsBlocked(c, r + 1) || !MapMath.IsBlocked(c, r - 1);
            float px = x + R(rng) * MapGrid.Cell, pz = zTop - R(rng) * MapGrid.Cell;
            if (edge && R(rng) < 0.95f) PlaceBarrier(parent, "RockBig", px, pz, 1.4f + R(rng) * 0.9f, rng);
            else if (!edge && R(rng) < 0.05f) PlaceBarrier(parent, "RockMid", px, pz, 1.6f + R(rng) * 1.0f, rng);
        }

        void PlaceBarrier(Transform parent, string key, float x, float z, float scale, System.Random rng)
        {
            if (InClear(x, z, 1f)) return;
            var rock = Spawn(key, new Vector3(x, 0f, z), R(rng) * 360f, scale, parent);
            // The cell grid already blocks the walls; a rock-sized collider would stick out into the walkable side.
            if (rock != null)
                foreach (var o in rock.GetComponentsInChildren<WorldObstacle>(true)) Destroy(o);
        }

        void Roll(Transform parent, System.Random rng, string key, float chance, float x, float zTop, float radius, float sMin, float sMax, bool avoidRoad)
        {
            if (R(rng) >= chance) return;
            float px = x + R(rng) * MapGrid.Cell, pz = zTop - R(rng) * MapGrid.Cell;
            float s = sMin + R(rng) * (sMax - sMin);
            float rr = radius * s;
            if (!Walkable(px, pz, Mathf.Min(rr, 1.2f), avoidRoad) || InClear(px, pz, rr)) return;
            if (radius >= 1.4f)
            {
                for (int i = 0; i < _big.Count; i++)
                {
                    float dx = px - _big[i].x, dz = pz - _big[i].y;
                    if (dx * dx + dz * dz < (_big[i].z + rr) * (_big[i].z + rr)) return;
                }
                _big.Add(new Vector3(px, pz, rr));
            }
            float yaw = key == "Fence" ? Mathf.Round(R(rng) * 4f) * 45f : R(rng) * 360f;
            Spawn(key, new Vector3(px, 0f, pz), yaw, s, parent);
        }

        bool Walkable(float x, float z, float r, bool avoidRoad)
        {
            byte id = MapGrid.IdAtWorld(x, z);
            if (MapGrid.IsBlockedId(id) || (avoidRoad && id == 5)) return false;
            if (MapGrid.BlockedAt(x + r, z) || MapGrid.BlockedAt(x - r, z) || MapGrid.BlockedAt(x, z + r) || MapGrid.BlockedAt(x, z - r)) return false;
            return true;
        }

        bool InClear(float x, float z, float r)
        {
            for (int i = 0; i < _clear.Count; i++)
            {
                float dx = x - _clear[i].x, dz = z - _clear[i].y;
                float m = _clear[i].z + r;
                if (dx * dx + dz * dz < m * m) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------ spawning

        GameObject Spawn(string key, Vector3 pos, float yaw, float scale, Transform parent)
        {
            GameObject prefab;
            if (!_prefabs.TryGetValue(key, out prefab)) return null;

            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
            go.name = key;
            go.transform.localScale = Vector3.one * scale;
            if (Mathf.Approximately(scale, 1f)) return go;

            bool quarter = Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Round(yaw / 90f) * 90f)) < 0.5f;
            foreach (var o in go.GetComponentsInChildren<WorldObstacle>(true))
            {
                if (o.kind == WorldObstacle.Kind.Circle) o.radius *= scale;
                else if (quarter)
                {
                    bool swap = Mathf.Abs(Mathf.Round(yaw / 90f)) % 2 == 1;
                    Vector2 h = o.halfSize * scale;
                    o.halfSize = swap ? new Vector2(h.y, h.x) : h;
                }
                else
                {
                    o.kind = WorldObstacle.Kind.Circle;
                    o.radius = Mathf.Max(o.halfSize.x, o.halfSize.y) * scale;
                }
            }
            return go;
        }

        static Vector3 Ring(Vector3 c, float radius, int i, int n, float phase)
        {
            float a = phase + i * Mathf.PI * 2f / Mathf.Max(1, n);
            return c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        }

        ItemDef Item(string name)
        {
            for (int i = 0; i < items.Length; i++) if (items[i] != null && items[i].name == name) return items[i];
            return null;
        }

        void SpawnMarker(Transform parent, MapMarker m, System.Random rng)
        {
            Vector3 p = new Vector3(m.x, 0f, m.z);
            switch (m.type)
            {
                case "start":
                    Spawn("Lantern", p + new Vector3(1.6f, 0f, 0.6f), 0f, 1f, parent);
                    break;
                case "carruaje":
                    Spawn("Cart", p, 40f, 1.2f, parent);
                    break;
                case "aldea":
                    Spawn("Well", p + new Vector3(3f, 0f, -3f), 0f, 1f, parent);
                    Spawn("Lantern", p + new Vector3(-2f, 0f, 2f), 0f, 1f, parent);
                    Spawn("Hut", p + new Vector3(-4f, 0f, -4f), 0f, 1f, parent);
                    break;
                case "salida":
                    Spawn("Lantern", p + new Vector3(-2.4f, 0f, 0f), 0f, 1f, parent);
                    Spawn("Lantern", p + new Vector3(2.4f, 0f, 0f), 0f, 1f, parent);
                    break;
                case "refugio":
                    SpawnRefuge(parent, m, p);
                    break;
                case "poi":
                    Decor(parent, m, p, rng);
                    break;
                case "npc":
                    SpawnNpc(parent, m, p);
                    break;
                case "infectado": SpawnEnemies(parent, m, p, "Infectado", rng); break;
                case "hinchado": SpawnEnemies(parent, m, p, "Hinchado", rng); break;
                case "sabueso": SpawnEnemies(parent, m, p, "Sabueso", rng); break;
                case "saqueador": SpawnEnemies(parent, m, p, "Saqueador", rng); break;
                case "ballestero": SpawnEnemies(parent, m, p, "Ballestero", rng); break;
                case "emboscador": SpawnEnemies(parent, m, p, "Emboscador", rng); break;
                case "tomado": SpawnEnemies(parent, m, p, "Tomado", rng); break;
                case "cofre":
                case "hierbas":
                case "venda":
                case "prenda":
                    SpawnLoot(parent, m, p, rng);
                    break;
            }
        }

        void SpawnRefuge(Transform parent, MapMarker m, Vector3 p)
        {
            var g = Spawn("Refugio", p, 0f, 1f, parent);
            if (g == null) return;
            var refuge = g.GetComponent<Refuge>();
            if (refuge != null)
            {
                var f = typeof(Refuge).GetField("displayName", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) f.SetValue(refuge, m.label);
            }
            string l = m.label == null ? string.Empty : m.label.ToLowerInvariant();
            if (l.Contains("caba")) Spawn("Hut", p + new Vector3(4.6f, 0f, 3.2f), 0f, 1f, parent);
            else Spawn("RuinedHut", p + new Vector3(4.2f, 0f, 3.2f), 0f, 1f, parent);
            Spawn("Lantern", p + new Vector3(-1.8f, 0f, 1.2f), 0f, 1f, parent);
        }

        void SpawnNpc(Transform parent, MapMarker m, Vector3 p)
        {
            var g = Spawn("Npc", p, 200f, 1f, parent);
            if (g == null) return;
            g.name = "NPC " + m.label;
            var t = g.GetComponent<TalkableNpc>();
            if (t != null)
            {
                t.displayName = m.label;
                t.lines = new[] { string.IsNullOrEmpty(m.note) ? "..." : m.note };
            }
        }

        void SpawnEnemies(Transform parent, MapMarker m, Vector3 p, string key, System.Random rng)
        {
            int n = Mathf.Max(1, m.n);
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = n == 1 ? p : Ring(p, 1.8f, i, n, R(rng) * 6f);
                if (MapGrid.BlockedAt(pos.x, pos.z)) pos = p;
                var g = Spawn(key, pos, Mathf.Round(R(rng) * 8f) * 45f, 1f, parent);
                if (g != null) g.name = key + " " + m.id + (n > 1 ? "." + (i + 1) : string.Empty);
            }
        }

        // ------------------------------------------------------------------------------------------ loot

        void SpawnLoot(Transform parent, MapMarker m, Vector3 p, System.Random rng)
        {
            var go = new GameObject("Loot " + m.type + " " + m.id);
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            var loot = go.AddComponent<MapLoot>();
            var vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);
            loot.visual = vis.gameObject;

            switch (m.type)
            {
                case "cofre":
                    loot.title = "Cofre";
                    Box(vis, new Vector3(0f, 0.2f, 0f), new Vector3(0.74f, 0.4f, 0.48f), wood);
                    Box(vis, new Vector3(0f, 0.46f, 0f), new Vector3(0.78f, 0.16f, 0.52f), woodDark);
                    Box(vis, new Vector3(0f, 0.34f, 0.26f), new Vector3(0.1f, 0.12f, 0.04f), glow);
                    int roll = Mathf.Abs(m.id.GetHashCode()) % 5;
                    switch (roll)
                    {
                        case 0: loot.items = new[] { Item("Venda"), Item("Unguento") }; loot.counts = new[] { 1, 1 }; break;
                        case 1: loot.items = new[] { Item("Tonico") }; loot.counts = new[] { 1 }; break;
                        case 2: loot.items = new[] { Item("Tintura"), Item("HierbaSeca") }; loot.counts = new[] { 1, 2 }; break;
                        case 3: loot.items = new[] { Item("Unguento") }; loot.counts = new[] { 1 }; break;
                        default: loot.items = new[] { Item("Venda") }; loot.counts = new[] { 2 }; break;
                    }
                    break;
                case "hierbas":
                    loot.title = "Hierbas";
                    for (int i = 0; i < 4; i++)
                        Box(vis, new Vector3(Mathf.Cos(i * 1.6f) * 0.18f, 0.16f, Mathf.Sin(i * 1.6f) * 0.18f), new Vector3(0.05f, 0.32f, 0.05f), stone);
                    loot.items = new[] { Item("HierbaSeca") };
                    loot.counts = new[] { 2 + rng.Next(2) };
                    break;
                case "venda":
                    loot.title = "Vendas";
                    Box(vis, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.14f, 0.2f), stone);
                    loot.items = new[] { Item("Venda") };
                    loot.counts = new[] { 1 + rng.Next(2) };
                    break;
                default:
                    loot.title = "Prenda";
                    Box(vis, new Vector3(0f, 0.06f, 0f), new Vector3(0.5f, 0.1f, 0.4f), woodDark);
                    int idx;
                    if (_prendaIndex.TryGetValue(m.id, out idx) && armors != null && armors.Length > 0)
                        loot.armor = armors[idx % armors.Length];
                    break;
            }
            Box(vis, new Vector3(0f, 0.95f, 0f), new Vector3(0.1f, 0.1f, 0.1f), glow);
        }

        static void Box(Transform parent, Vector3 lpos, Vector3 size, Material mat)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(parent, false);
            c.transform.localPosition = lpos;
            c.transform.localScale = size;
            if (mat != null) c.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------------------------------ points of interest

        void Decor(Transform parent, MapMarker m, Vector3 p, System.Random rng)
        {
            string l = (m.label ?? string.Empty).ToLowerInvariant();
            if (l.Contains("puente"))
            {
                for (int i = 0; i < 4; i++) Spawn("RockSmall", Ring(p, 2.6f, i, 4, 0.8f), 0f, 1f, parent);
            }
            else if (l.Contains("torre"))
            {
                Spawn("RuinedHut", p, 0f, 1.2f, parent);
                for (int i = 0; i < 3; i++) Spawn("RockBig", Ring(p, 4.2f, i, 3, 0.3f), 0f, 1f, parent);
            }
            else if (l.Contains("capilla"))
            {
                Spawn("RuinedHut", p, 0f, 1.1f, parent);
                Spawn("Cross", p + new Vector3(4f, 0f, 0f), 15f, 1f, parent);
                Spawn("Cross", p + new Vector3(-4f, 0f, 1f), -10f, 1f, parent);
                Spawn("StoneA", p + new Vector3(0f, 0f, -4f), 0f, 1f, parent);
            }
            else if (l.Contains("colgados"))
            {
                for (int i = 0; i < 3; i++) Spawn("Tree", Ring(p, 2.6f, i, 3, 0.5f), R(rng) * 360f, 1.4f, parent);
                Spawn("Cadaver", p + new Vector3(0.6f, 0f, 0.3f), 0f, 1f, parent);
            }
            else if (l.Contains("granja"))
            {
                Spawn("RuinedHut", p + new Vector3(0f, 0f, 3.5f), 0f, 1f, parent);
                Spawn("Cart", p + new Vector3(-3.5f, 0f, -2f), 70f, 1f, parent);
                for (int i = 0; i < 4; i++) Spawn("Fence", Ring(p, 5.6f, i, 4, 0.8f), i % 2 == 0 ? 0f : 90f, 1f, parent);
            }
            else if (l.Contains("cueva"))
            {
                for (int i = 0; i < 7; i++)
                    if (i != 2) Spawn("RockBig", Ring(p, 4.2f, i, 7, 0.2f), R(rng) * 360f, 1.5f + R(rng) * 0.6f, parent);
                Spawn("Foco", p, 0f, 1f, parent);
            }
            else if (l.Contains("derrumbe"))
            {
                for (int i = 0; i < 6; i++)
                    Spawn("RockBig", p + new Vector3((R(rng) - 0.5f) * 3f, 0f, (i - 2.5f) * 1.9f), R(rng) * 360f, 1.9f, parent);
            }
            else if (l.Contains("fosa"))
            {
                for (int i = 0; i < 5; i++) Spawn("Cross", Ring(p, 3.2f, i, 5, 0.4f), R(rng) * 40f - 20f, 1f, parent);
                Spawn("Cadaver", p + new Vector3(0.5f, 0f, 0f), 0f, 1f, parent);
                Spawn("Cadaver", p + new Vector3(-1.2f, 0f, 1f), 0f, 1f, parent);
            }
            else if (l.Contains("cantera"))
            {
                for (int i = 0; i < 4; i++) Spawn("RockBig", Ring(p, 3.8f, i, 4, 0.6f), R(rng) * 360f, 1.4f, parent);
                Spawn("Cart", p + new Vector3(1f, 0f, -1f), 20f, 1f, parent);
            }
            else if (l.Contains("fuego"))
            {
                Spawn("Foco", p, 0f, 1f, parent);
            }
            else if (l.Contains("caba"))
            {
                Spawn("Hut", p, 0f, 1f, parent);
            }
            else if (l.Contains("carreta"))
            {
                Spawn("Cart", p, 120f, 1f, parent);
            }
            else if (l.Contains("pozo"))
            {
                Spawn("Well", p, 0f, 1f, parent);
            }
            else if (l.Contains("embarcadero"))
            {
                for (int i = 0; i < 3; i++) Spawn("Fence", p + new Vector3(0f, 0f, (i - 1) * 2.2f), 90f, 1f, parent);
            }
            else if (l.Contains("altar"))
            {
                Spawn("StoneB", p, 0f, 1.5f, parent);
                Spawn("Cross", p + new Vector3(2.4f, 0f, 0f), 25f, 1f, parent);
            }
            else if (l.Contains("establo"))
            {
                Spawn("RuinedHut", p, 0f, 1.1f, parent);
                Spawn("Fence", p + new Vector3(4.5f, 0f, 1f), 0f, 1f, parent);
                Spawn("Fence", p + new Vector3(4.5f, 0f, -2f), 0f, 1f, parent);
            }
            else
            {
                Spawn("StoneA", p, 0f, 1f, parent);
            }
        }

        // ------------------------------------------------------------------------------------------ on-screen text

        GUIStyle _style;

        void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            }

            float scale = Mathf.Max(1f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            if (Time.unscaledTime < _bannerUntil && _banner.Length > 0)
                Shadowed(new Rect(0f, 28f, w, 40f), _banner, new Color(0.86f, 0.82f, 0.7f));

            string prompt = MapLoot.Prompt();
            if (prompt.Length > 0)
                Shadowed(new Rect(0f, h - 70f, w, 30f), prompt, new Color(0.95f, 0.9f, 0.7f));
        }

        void Shadowed(Rect r, string text, Color c)
        {
            _style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, _style);
            _style.normal.textColor = c;
            GUI.Label(r, text, _style);
        }
    }

    static class MapMath
    {
        public static bool IsBlocked(int c, int r) { return MapGrid.IsBlockedId(MapGrid.IdAt(c, r)); }
    }
}
