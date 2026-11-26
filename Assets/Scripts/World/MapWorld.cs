using System;
using System.Collections.Generic;
using System.Reflection;
using BrumaPx.Characters;
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
            LoadRocks();
            LoadPlants();

            // Boss lairs inside mountains are carved before the ground texture is made.
            CaveLair.CarveAll(MapGrid.File);
            // keep the boulders of the mountain edge (and trees) away from the openings, so they can be seen
            foreach (var lair in CaveLair.Lairs)
            {
                _clear.Add(new Vector3(lair.mouth.x, lair.mouth.y, 3.2f));
                _clear.Add(new Vector3(lair.opening.x, lair.opening.y, 3.6f));
            }
            MarkWaterDecks();
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
            var markers = new List<MapMarker>(MapGrid.File.markers);
            markers.AddRange(VillageHouses(MapGrid.File.markers));
            foreach (var m in markers)
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

        // Cenizal's houses (Blender buildings), laid out around the square of the village marker. Encoded as
        // markers of type "casa" with label "Prefab|yaw|locked" so they stream with the chunks like the rest.
        static readonly string[] CenizalHouses =
        {
            "CasaEntramado|0|",          "1.3,11.9",
            "CasaPiedra|0|",             "-11.7,0.9",
            "CasaPiedra|90|",            "14.3,6.9",
            "CasaEntramado|90|",         "14.3,-6.1",
            "CasaPiedra|0|locked",       "-22.7,-8.1",
            "CasaEntramado|0|",          "-25.7,2.9",
            "CasaPiedra|0|locked",       "-24.7,13.9",
            "CasaEntramado|0|locked",    "-12.7,11.9",
        };

        static List<MapMarker> VillageHouses(IEnumerable<MapMarker> markers)
        {
            var list = new List<MapMarker>();
            foreach (var m in markers)
            {
                if (m.type != "aldea" || m.label == null || !m.label.ToLowerInvariant().Contains("cenizal")) continue;
                for (int i = 0; i < CenizalHouses.Length; i += 2)
                {
                    var o = CenizalHouses[i + 1].Split(',');
                    list.Add(new MapMarker
                    {
                        id = m.id + "_casa" + (i / 2 + 1),
                        type = "casa",
                        x = m.x + float.Parse(o[0], System.Globalization.CultureInfo.InvariantCulture),
                        z = m.z + float.Parse(o[1], System.Globalization.CultureInfo.InvariantCulture),
                        label = CenizalHouses[i],
                        note = string.Empty
                    });
                }
            }
            return list;
        }

        void SpawnHouse(Transform parent, MapMarker m, Vector3 p)
        {
            var bits = m.label.Split('|');
            float yaw = bits.Length > 1 ? float.Parse(bits[1], System.Globalization.CultureInfo.InvariantCulture) : 0f;
            var g = SpawnBuilding(bits[0], p, yaw, parent);
            if (g == null) { Spawn("Hut", p, yaw, 1f, parent); return; }
            g.name = bits[0] + " " + m.id;
            var b = g.GetComponent<Building>();
            if (b != null && b.door != null) b.door.SetLocked(bits.Length > 2 && bits[2] == "locked");
        }

        static float ClearRadius(MapMarker m)
        {
            switch (m.type)
            {
                case "casa": return 6f;
                case "start": return 4f;
                case "carruaje": return 3f;
                case "aldea": return 7f;
                case "salida": return 4.5f;
                case "jefe": return 4f;
                case "refugio":
                {
                    string l = m.label == null ? string.Empty : m.label.ToLowerInvariant();
                    return l.Contains("curandera") ? 14f : 6f;       // the healer's hut and garden need a clearing
                }
                case "poi":
                {
                    string l = m.label == null ? string.Empty : m.label.ToLowerInvariant();
                    // the woodcutter's cabin is a big building: clear the trees in front of its door too
                    if (l.Contains("caba") && (l.Contains("leñador") || l.Contains("lenador"))) return 8f;
                    return 5.5f;
                }
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

            foreach (var lair in CaveLair.Lairs) CaveLair.Create(this, lair);
        }

        /// <summary>Instantiates one of the map props by key (for CaveLair and other builders).</summary>
        public GameObject SpawnProp(string key, Vector3 pos, float yaw, float scale, Transform parent)
        {
            return Spawn(key, pos, yaw, scale, parent);
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
            string[] n = { "llanura", "bosque", "bosque muerto", "pantano", "agua", "camino", "campo", "escarpado", "montana", "ruinas", "aldea", "cueva", "sobre el agua" };
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
            {
                // buildings first, so the people who live inside them (the woodcutter in his basement) find them
                for (int i = 0; i < ms.Count; i++) if (IsPlace(ms[i])) SpawnMarker(go.transform, ms[i], rng);
                for (int i = 0; i < ms.Count; i++) if (!IsPlace(ms[i])) SpawnMarker(go.transform, ms[i], rng);
            }

            Scatter(go.transform, cx, cz, rng);
        }

        static bool IsPlace(MapMarker m) { return m.type == "poi" || m.type == "refugio" || m.type == "aldea" || m.type == "casa"; }

        float R(System.Random rng) { return (float)rng.NextDouble(); }

        /// <summary>A building made in Blender (Assets/Resources/Buildings/&lt;id&gt;, built by BuildingImporter).</summary>
        GameObject SpawnBuilding(string id, Vector3 pos, float yaw, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Buildings/" + id);
            if (prefab == null) return null;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
            go.name = id;
            return go;
        }

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
                    if (id == 4) { WaterDecor(parent, rng, c, r, x, zTop); continue; }
                    if (id == 5 || id == MapGrid.WaterDeckId) continue;

                    switch (id)
                    {
                        case 0:
                            Roll(parent, rng, "Tree", 0.025f, x, zTop, 0.9f, 0.9f, 1.3f, true);
                            Roll(parent, rng, "RockMid", 0.035f, x, zTop, 0.7f, 0.7f, 1.2f, true);
                            Roll(parent, rng, "Arbusto", NearRoad(c, r) ? 0.09f : 0.03f, x, zTop, 0.6f, 0.8f, 1.25f, true);
                            Roll(parent, rng, "Matorral", 0.012f, x, zTop, 0.7f, 0.8f, 1.2f, true);
                            Roll(parent, rng, "Cross", 0.004f, x, zTop, 1.4f, 1f, 1f, true);
                            break;
                        case 1:
                            Roll(parent, rng, "Tree", 0.21f, x, zTop, 0.9f, 0.9f, 1.5f, true);
                            Roll(parent, rng, "Arbusto", NearRoad(c, r) ? 0.14f : 0.09f, x, zTop, 0.6f, 0.8f, 1.3f, true);
                            Roll(parent, rng, "RockSmall", 0.02f, x, zTop, 0.5f, 0.8f, 1.2f, false);
                            break;
                        case 2:
                            Roll(parent, rng, "Tree", 0.16f, x, zTop, 0.9f, 0.8f, 1.2f, true);
                            Roll(parent, rng, "Matorral", 0.05f, x, zTop, 0.7f, 0.8f, 1.3f, true);
                            Roll(parent, rng, "RockMid", 0.03f, x, zTop, 0.7f, 0.7f, 1.1f, true);
                            Roll(parent, rng, "Cross", 0.004f, x, zTop, 1.4f, 1f, 1f, true);
                            break;
                        case 3:
                            Roll(parent, rng, "Tree", 0.07f, x, zTop, 0.9f, 0.8f, 1.2f, true);
                            Roll(parent, rng, "Matorral", 0.02f, x, zTop, 0.7f, 0.7f, 1.1f, true);
                            Roll(parent, rng, "RockSmall", 0.02f, x, zTop, 0.5f, 0.8f, 1.1f, false);
                            Roll(parent, rng, "Charco", 0.012f, x, zTop, 1.4f, 1f, 1f, false);
                            Roll(parent, rng, "Foco", 0.004f, x, zTop, 3.5f, 1f, 1f, false);
                            break;
                        case 6:
                            Roll(parent, rng, "Fence", 0.04f, x, zTop, 2.0f, 1f, 1f, true);
                            Roll(parent, rng, "Tree", 0.008f, x, zTop, 0.9f, 0.8f, 1.1f, true);
                            Roll(parent, rng, "Arbusto", NearRoad(c, r) ? 0.05f : 0.015f, x, zTop, 0.6f, 0.8f, 1.2f, true);
                            break;
                        case 9:
                            Roll(parent, rng, "RuinedHut", 0.05f, x, zTop, 3.6f, 1f, 1f, false);
                            Roll(parent, rng, "StoneA", 0.12f, x, zTop, 0.8f, 0.8f, 1.1f, false);
                            Roll(parent, rng, "RockMid", 0.08f, x, zTop, 0.8f, 0.7f, 1.1f, false);
                            Roll(parent, rng, "Cross", 0.05f, x, zTop, 1.4f, 1f, 1f, false);
                            break;
                        case MapGrid.CaveId:
                            // stalagmites, puddles and what is left of those who came in before
                            Roll(parent, rng, "RockSmall", 0.10f, x, zTop, 0.5f, 0.7f, 1.3f, false);
                            Roll(parent, rng, "RockMid", 0.03f, x, zTop, 0.7f, 0.6f, 0.9f, false);
                            Roll(parent, rng, "Charco", 0.04f, x, zTop, 1.4f, 1f, 1f, false);
                            Roll(parent, rng, "Cadaver", 0.012f, x, zTop, 1.2f, 1f, 1f, false);
                            break;
                        case 10:
                            // the houses of a village are placed by hand (Cenizal); here only odds and ends
                            Roll(parent, rng, "Fence", 0.02f, x, zTop, 2.0f, 1f, 1f, true);
                            Roll(parent, rng, "Lantern", 0.01f, x, zTop, 0.8f, 1f, 1f, false);
                            Roll(parent, rng, "Arbusto", 0.02f, x, zTop, 0.6f, 0.8f, 1.1f, true);
                            Roll(parent, rng, "StoneA", 0.02f, x, zTop, 0.8f, 0.8f, 1f, false);
                            break;
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------ rocks

        // The big rocks made in Blender (Resources/Props/Roca*). The old sphere rocks RockBig and RockMid are swapped
        // for one of them wherever they are asked for; the choice depends on the position, so it is the same rock
        // every time the chunk is built.
        static readonly string[] RockNames = { "RocaPena", "RocaPartida", "RocaLosa", "RocaMonton", "RocaAguja" };
        static readonly int[] RockWeights = { 30, 20, 20, 15, 15 };
        readonly List<string> _rocks = new List<string>();
        int _rockWeightSum;

        void LoadRocks()
        {
            _rockWeightSum = 0;
            for (int i = 0; i < RockNames.Length; i++)
            {
                var g = Resources.Load<GameObject>("Props/" + RockNames[i]);
                if (g == null) continue;
                _prefabs[RockNames[i]] = g;
                _rocks.Add(RockNames[i]);
                _rockWeightSum += RockWeights[i];
            }
        }

        static float Hash01(float x, float z, float salt)
        {
            float h = Mathf.Abs(Mathf.Sin(x * 12.9898f + z * 78.233f + salt * 37.719f)) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        string PickRock(float x, float z, float salt)
        {
            int roll = (int)(Hash01(x, z, salt) * _rockWeightSum);
            for (int i = 0; i < RockNames.Length; i++)
            {
                if (!_prefabs.ContainsKey(RockNames[i])) continue;
                roll -= RockWeights[i];
                if (roll < 0) return RockNames[i];
            }
            return _rocks[0];
        }

        // ------------------------------------------------------------------------------------------ decks over water

        // The bridge over the Rio Ceniza and the pier on the Lago Hondo stand over water. Their cells become
        // MapGrid.WaterDeckId: walkable, with water drawn under them; the prefab is placed over those cells.
        sealed class Deck
        {
            public string markerId, prefab;
            public Vector3 pos;
            public float yaw;
        }
        readonly List<Deck> _decks = new List<Deck>();

        void MarkWaterDecks()
        {
            foreach (var m in MapGrid.File.markers)
            {
                if (m.type != "poi" || string.IsNullOrEmpty(m.label)) continue;
                string l = m.label.ToLowerInvariant();
                if (l.Contains("puente")) MarkBridge(m);
                else if (l.Contains("embarcadero")) MarkPier(m);
            }
        }

        static bool WaterCell(int c, int r) { byte id = MapGrid.IdAt(c, r); return id == 4 || id == MapGrid.WaterDeckId; }

        // A road crossing the river: road cells with water a few cells away on both sides of one axis.
        void MarkBridge(MapMarker m)
        {
            int mc = MapGrid.ColOf(m.x), mr = MapGrid.RowOf(m.z);
            const int reach = 7, gap = 5;
            var cells = new List<Vector2Int>();
            bool riverNS = true;
            for (int r = mr - reach; r <= mr + reach; r++)
                for (int c = mc - reach; c <= mc + reach; c++)
                {
                    if (MapGrid.IdAt(c, r) != 5) continue;
                    bool n = false, so = false, e = false, w = false;
                    for (int k = 1; k <= gap; k++)
                    {
                        n |= MapGrid.IdAt(c, r - k) == 4;
                        so |= MapGrid.IdAt(c, r + k) == 4;
                        e |= MapGrid.IdAt(c + k, r) == 4;
                        w |= MapGrid.IdAt(c - k, r) == 4;
                    }
                    if (n && so) { cells.Add(new Vector2Int(c, r)); riverNS = true; }
                    else if (e && w) { cells.Add(new Vector2Int(c, r)); riverNS = false; }
                }
            if (cells.Count == 0) return;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var v in cells)
            {
                MapGrid.SetId(v.x, v.y, MapGrid.WaterDeckId);
                x0 = Mathf.Min(x0, MapGrid.CellX(v.x) - 1f); x1 = Mathf.Max(x1, MapGrid.CellX(v.x) + 1f);
                z0 = Mathf.Min(z0, MapGrid.CellZ(v.y) - 1f); z1 = Mathf.Max(z1, MapGrid.CellZ(v.y) + 1f);
            }
            var centre = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            // the bridge runs across the river: east-west over a river that flows north-south
            _decks.Add(new Deck { markerId = m.id, prefab = "PuentePiedra", pos = centre, yaw = riverNS ? 0f : 90f });
            _clear.Add(new Vector3(centre.x - 6.5f, centre.z, 3.5f));
            _clear.Add(new Vector3(centre.x + 6.5f, centre.z, 3.5f));
        }

        // A pier from the shore into the water nearest to the marker: a walkway of five cells and a landing of two.
        void MarkPier(MapMarker m)
        {
            int mc = MapGrid.ColOf(m.x), mr = MapGrid.RowOf(m.z);
            Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1) };
            int best = -1, bestK = 99;
            for (int i = 0; i < 4; i++)
                for (int k = 0; k <= 4; k++)
                    if (MapGrid.IdAt(mc + dirs[i].x * k, mr + dirs[i].y * k) == 4) { if (k < bestK) { bestK = k; best = i; } break; }
            if (best < 0) return;
            var d = dirs[best];                                   // in cells: x = column, y = row (row - 1 is north)
            var side = new Vector2Int(d.y, -d.x);                 // to the left of the walkway, seen walking out
            int sc = mc + d.x * bestK, sr = mr + d.y * bestK;
            for (int a = 0; a < 5; a++)
                for (int b = 0; b < 2; b++)
                {
                    if (b == 1 && a < 3) continue;
                    int c = sc + d.x * a + side.x * b, r = sr + d.y * a + side.y * b;
                    if (MapGrid.IdAt(c, r) == 4) MapGrid.SetId(c, r, MapGrid.WaterDeckId);
                }
            var dirW = new Vector3(d.x, 0f, -d.y);
            var start = new Vector3(MapGrid.CellX(sc), 0f, MapGrid.CellZ(sr)) - dirW * (MapGrid.Cell * 0.5f);
            float yaw = Mathf.Atan2(-dirW.z, dirW.x) * Mathf.Rad2Deg;     // turns the model's +x onto the walkway
            _decks.Add(new Deck { markerId = m.id, prefab = "Embarcadero", pos = start, yaw = yaw });
            _clear.Add(new Vector3(start.x - dirW.x * 3.5f, start.z - dirW.z * 3.5f, 4.5f));
        }

        GameObject SpawnDeck(MapMarker m, Transform parent)
        {
            foreach (var d in _decks)
                if (d.markerId == m.id) return SpawnBuilding(d.prefab, d.pos, d.yaw, parent);
            return null;
        }

        // Reeds along the banks (standing in the water, at the foot of the bank) and lily pads on open water.
        void WaterDecor(Transform parent, System.Random rng, int c, int r, float x, float zTop)
        {
            if (!_plants) return;
            float cell = MapGrid.Cell;
            Vector3 centre = new Vector3(x + cell * 0.5f, -MapGrid.WaterDepth, zTop - cell * 0.5f);
            bool e = !WaterCell(c + 1, r), w = !WaterCell(c - 1, r), n = !WaterCell(c, r - 1), so = !WaterCell(c, r + 1);
            if (e || w || n || so)
            {
                if (R(rng) > 0.4f) return;
                Vector3 land = new Vector3((e ? 1f : 0f) - (w ? 1f : 0f), 0f, (n ? 1f : 0f) - (so ? 1f : 0f));
                if (land.sqrMagnitude > 0.01f) land.Normalize();
                Vector3 pos = centre + land * (0.45f + R(rng) * 0.35f) + new Vector3((R(rng) - 0.5f) * 0.8f, 0f, (R(rng) - 0.5f) * 0.8f);
                if (InClear(pos.x, pos.z, 0.5f)) return;
                Spawn(R(rng) < 0.35f ? "Totoras" : "Juncos", pos, R(rng) * 360f, 0.8f + R(rng) * 0.45f, parent);
                return;
            }
            // open water: all around is water for a few cells
            for (int k = 2; k <= 3; k++)
                if (!WaterCell(c + k, r) || !WaterCell(c - k, r) || !WaterCell(c, r + k) || !WaterCell(c, r - k)) return;
            if (R(rng) < 0.05f)
            {
                Vector3 pos = centre + new Vector3((R(rng) - 0.5f) * 1.2f, 0.01f, (R(rng) - 0.5f) * 1.2f);
                Spawn("Nenufares", pos, R(rng) * 360f, 0.8f + R(rng) * 0.4f, parent);
            }
        }

        // ------------------------------------------------------------------------------------------ trees and bushes

        // Leafy trees and bushes made in Blender (Resources/Props). The old bare tree (prefab Tree) stays in the
        // swamp and the dead forest; anywhere else a "Tree" becomes one of these, picked by terrain and position.
        static readonly string[] TreeNames = { "Roble", "Pino", "Abedul", "ArbolOtono", "Arbolito" };
        static readonly int[] TreesForest = { 30, 30, 10, 15, 15 };
        static readonly int[] TreesPlain = { 35, 0, 25, 15, 25 };
        static readonly int[] TreesFarm = { 30, 0, 10, 25, 35 };
        bool _plants;

        void LoadPlants()
        {
            string[] names = { "Roble", "Pino", "Abedul", "ArbolOtono", "Arbolito", "Arbusto", "ArbustoBayas", "Matorral" };
            foreach (var n in new[] { "Juncos", "Totoras", "Nenufares" })
            {
                var g = Resources.Load<GameObject>("Props/" + n);
                if (g != null) _prefabs[n] = g;
            }
            int found = 0;
            foreach (var n in names)
            {
                var g = Resources.Load<GameObject>("Props/" + n);
                if (g == null) continue;
                _prefabs[n] = g;
                found++;
            }
            _plants = found == names.Length;
        }

        string PickTree(byte terrain, float x, float z)
        {
            int[] w = terrain == 1 ? TreesForest : (terrain == 0 ? TreesPlain : TreesFarm);
            int sum = 0;
            for (int i = 0; i < w.Length; i++) sum += w[i];
            int roll = (int)(Hash01(x, z, 3f) * sum);
            for (int i = 0; i < w.Length; i++)
            {
                roll -= w[i];
                if (roll < 0) return TreeNames[i];
            }
            return TreeNames[0];
        }

        static bool NearRoad(int c, int r)
        {
            return MapGrid.IdAt(c + 1, r) == 5 || MapGrid.IdAt(c - 1, r) == 5 || MapGrid.IdAt(c, r + 1) == 5 || MapGrid.IdAt(c, r - 1) == 5;
        }

        // Cliffs and mountains block the hero by whole cells (MapGrid.Resolve: the invisible wall sits Inset inside
        // the blocked cell). Each edge cell gets one rock laid along that wall, its near face on the wall, so what
        // stops the hero is the stone he sees. Further in, an odd rock now and then.
        void Barrier(Transform parent, System.Random rng, int c, int r, float x, float zTop)
        {
            bool e = !MapMath.IsBlocked(c + 1, r), w = !MapMath.IsBlocked(c - 1, r);
            bool n = !MapMath.IsBlocked(c, r - 1), so = !MapMath.IsBlocked(c, r + 1);   // row - 1 is north
            bool edge = e || w || n || so;
            float cell = MapGrid.Cell;
            Vector3 centre = new Vector3(x + cell * 0.5f, 0f, zTop - cell * 0.5f);
            if (_rocks.Count == 0)
            {
                float px = x + R(rng) * cell, pz = zTop - R(rng) * cell;
                if (edge && R(rng) < 0.95f) PlaceBarrier(parent, "RockBig", px, pz, 1.4f + R(rng) * 0.9f, rng);
                else if (!edge && R(rng) < 0.05f) PlaceBarrier(parent, "RockMid", px, pz, 1.6f + R(rng) * 1.0f, rng);
                return;
            }
            if (!edge)
            {
                if (R(rng) < 0.04f)
                {
                    var p = new Vector3(x + R(rng) * cell, 0f, zTop - R(rng) * cell);
                    PlaceBarrier(parent, PickRock(p.x, p.z, 1f), p.x, p.z, 0.9f + R(rng) * 0.5f, rng);
                }
                return;
            }
            if (InClear(centre.x, centre.z, 1f)) return;

            Vector3 open = new Vector3((e ? 1f : 0f) - (w ? 1f : 0f), 0f, (n ? 1f : 0f) - (so ? 1f : 0f));
            string key = PickRock(centre.x, centre.z, 0f);
            GameObject prefab;
            if (!_prefabs.TryGetValue(key, out prefab)) return;
            var info = prefab.GetComponent<WorldProp>();
            float halfLength = info != null ? info.halfLength : 1.5f;

            // long enough to cover the side of the cell, with some overlap with the neighbours
            float s = Mathf.Clamp(cell * (1.15f + R(rng) * 0.4f) / (2f * halfLength), 0.6f, 1.25f);
            float yaw;
            Vector3 d = Vector3.zero;
            if (open.sqrMagnitude < 0.01f)
            {
                // a wall one cell thick, open on both sides: the rock fills the cell
                yaw = R(rng) * 360f;
                s = Mathf.Min(s, cell * 0.5f / Mathf.Max(info != null ? info.halfWidth : 1f, 0.1f));
            }
            else
            {
                d = open.normalized;
                Vector3 t = new Vector3(-d.z, 0f, d.x);                 // along the wall
                float wallYaw = Mathf.Atan2(-t.z, t.x) * Mathf.Rad2Deg;  // yaw that turns local +x onto t
                yaw = wallYaw - (info != null ? info.axisYaw : 0f) + (R(rng) - 0.5f) * 36f + (R(rng) < 0.5f ? 180f : 0f);
            }
            var rock = Spawn(key, centre, yaw, s, parent);
            if (rock == null) return;
            foreach (var o in rock.GetComponentsInChildren<WorldObstacle>(true)) Destroy(o);
            if (d != Vector3.zero)
            {
                var prop = rock.GetComponent<WorldProp>();
                float reach = prop != null ? prop.Reach(d) : halfLength * s * 0.7f;
                Vector3 t = new Vector3(-d.z, 0f, d.x);
                float wall = cell * 0.5f - WallInset;                   // the invisible wall, from the cell centre
                rock.transform.position = centre + d * (wall - reach * 0.92f) + t * ((R(rng) - 0.5f) * 0.5f);
            }
        }

        // Same as MapGrid's Inset: how far inside a blocked cell the invisible wall stands.
        const float WallInset = 0.5f;

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
            // the old sphere rocks become one of the Blender rocks, at about the same size
            if (_rocks.Count > 0 && (key == "RockBig" || key == "RockMid"))
            {
                scale *= key == "RockBig" ? 0.62f : 0.33f;
                key = PickRock(pos.x, pos.z, 2f);
            }
            // leafy trees outside the swamp and the dead forest (a few bare ones stay in the forest, for the mood)
            if (_plants && key == "Tree")
            {
                byte id = MapGrid.IdAtWorld(pos.x, pos.z);
                if (id != 2 && id != 3 && !(id == 1 && Hash01(pos.x, pos.z, 5f) < 0.08f))
                {
                    key = PickTree(id, pos.x, pos.z);
                    scale = Mathf.Lerp(0.85f, 1.12f, Mathf.InverseLerp(0.8f, 1.5f, scale));
                }
            }
            if (_plants && key == "Arbusto" && Hash01(pos.x, pos.z, 7f) < 0.25f) key = "ArbustoBayas";
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
                    Spawn("Lantern", p + new Vector3(6f, 0f, 3f), 0f, 1f, parent);
                    Spawn("Cart", p + new Vector3(-4f, 0f, -3f), 25f, 1f, parent);
                    break;
                case "casa":
                    SpawnHouse(parent, m, p);
                    break;
                case "salida":
                    Spawn("Lantern", p + new Vector3(-2.4f, 0f, 0f), 0f, 1f, parent);
                    Spawn("Lantern", p + new Vector3(2.4f, 0f, 0f), 0f, 1f, parent);
                    break;
                case "refugio":
                    SpawnRefuge(parent, m, p);
                    break;
                case "jefe":
                    SpawnBoss(parent, m, p);
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
                if (g.GetComponent<RefugeRest>() == null) g.AddComponent<RefugeRest>();
            }

            // What stands around the fire depends on the name given in the map.
            string l = m.label == null ? string.Empty : m.label.ToLowerInvariant();
            if (l.Contains("curandera"))
            {
                // the healer's hut: the rest point moves inside, by her fire pit, and the ring of the camp fire hides
                var hut = SpawnBuilding("ChozaCurandera", p + HealerHutOffset, 0f, parent);
                if (hut != null)
                {
                    var rest = hut.transform.Find("Cabin/Rest");
                    if (rest != null) g.transform.position = rest.position;
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    Spawn("Lantern", hut.transform.TransformPoint(new Vector3(-1.75f, 0f, -3.2f)), 0f, 1f, parent);
                    return;
                }
            }
            if (l.Contains("caba")) Spawn("Hut", p + new Vector3(4.6f, 0f, 3.2f), 0f, 1f, parent);
            else if (l.Contains("capilla"))
            {
                Spawn("RuinedHut", p + new Vector3(4.4f, 0f, 3.4f), 0f, 1.1f, parent);
                Spawn("Cross", p + new Vector3(-3.2f, 0f, -2.4f), 12f, 1f, parent);
                Spawn("Cross", p + new Vector3(-4.2f, 0f, -1.2f), -9f, 1f, parent);
            }
            else if (l.Contains("campamento"))
            {
                Spawn("Cart", p + new Vector3(3.8f, 0f, 2.2f), 70f, 1f, parent);
                Spawn("Fence", p + new Vector3(-4.2f, 0f, 2.6f), 0f, 1f, parent);
                Spawn("Fence", p + new Vector3(-4.2f, 0f, -0.6f), 0f, 1f, parent);
            }
            else if (l.Contains("fogata"))
            {
                for (int i = 0; i < 4; i++) Spawn("RockMid", Ring(p, 2.9f, i, 4, 0.5f), 0f, 0.9f, parent);
            }
            else Spawn("RuinedHut", p + new Vector3(4.2f, 0f, 3.2f), 0f, 1f, parent);
            Spawn("Lantern", p + new Vector3(-1.8f, 0f, 1.2f), 0f, 1f, parent);
        }

        // The healer's hut stands north-east of her refuge marker: clear of the road on the west, garden to the east.
        static readonly Vector3 HealerHutOffset = new Vector3(3f, 0f, 2.6f);

        static Building NearestBuilding(string name, Vector3 p, float range)
        {
            Building best = null;
            float bd = range * range;
            foreach (var b in FindObjectsByType<Building>(FindObjectsSortMode.None))
            {
                if (b.name != name) continue;
                float d = (b.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        void SpawnBoss(Transform parent, MapMarker m, Vector3 p)
        {
            var g = Spawn("ReySapo", p, 330f, 1f, parent);
            if (g == null) return;
            g.name = "Jefe " + m.label;
            var b = g.GetComponent<BossMarker>();
            if (b != null && !string.IsNullOrEmpty(m.label)) b.title = m.label;
            ActorSpriteDriver.Attach(g, "ReySapo", ActorSpriteDriver.Mode.Boss);
            var lair = CaveLair.For(m.id);
            if (lair != null)
            {
                // A boss with a lair walks between the cave and the outside: it must not switch off with its chunk.
                g.transform.SetParent(transform, true);
                BossLair.Attach(g, lair);
            }
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
            string id = NpcSpriteId(m.label);
            if (id != null) ActorSpriteDriver.Attach(g, id, ActorSpriteDriver.Mode.Npc);

            // The woodcutter lives chained in the basement of his cabin: he only exists down there.
            if (id == "Lenador")
            {
                var home = Building.WithBasementNear(p, 30f);
                if (home != null)
                {
                    g.transform.SetParent(home.occupantAnchor.parent, true);
                    g.transform.position = home.occupantAnchor.position;
                    g.transform.rotation = home.occupantAnchor.rotation;
                }
            }
            // The healer attends from behind the counter of her hut.
            if (id == "Curandera")
            {
                var hut = NearestBuilding("ChozaCurandera", p, 30f);
                if (hut != null && hut.occupantAnchor != null)
                {
                    g.transform.SetParent(hut.occupantAnchor.parent, true);
                    g.transform.position = hut.occupantAnchor.position;
                    g.transform.rotation = hut.occupantAnchor.rotation;
                }
            }
        }

        // NPCs with a sprite of their own, found by the label of the marker. The rest keep the hooded figure.
        static string NpcSpriteId(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            string l = label.ToLowerInvariant();
            if (l.Contains("médico") || l.Contains("medico") || l.Contains("peste")) return "MedicoPeste";
            if (l.Contains("leñador") || l.Contains("lenador")) return "Lenador";
            if (l.Contains("curandera")) return "Curandera";
            if (l.Contains("superviviente")) return "Superviviente";
            if (l.Contains("guardia")) return "Guardia";
            if (l.Contains("cochero")) return "Cochero";
            return null;
        }

        void SpawnEnemies(Transform parent, MapMarker m, Vector3 p, string key, System.Random rng)
        {
            int n = Mathf.Max(1, m.n);
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = n == 1 ? p : Ring(p, 1.8f, i, n, R(rng) * 6f);
                if (MapGrid.BlockedAt(pos.x, pos.z)) pos = p;
                var g = Spawn(key, pos, Mathf.Round(R(rng) * 8f) * 45f, 1f, parent);
                if (g == null) continue;
                g.name = key + " " + m.id + (n > 1 ? "." + (i + 1) : string.Empty);
                // Blender sprite of the enemy; the infected come in three villagers, picked at random.
                string sprite = key == "Infectado" ? "Infectado" + (1 + (int)(R(rng) * 2.999f)) : key;
                ActorSpriteDriver.Attach(g, sprite, ActorSpriteDriver.Mode.Enemy);
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
                if (SpawnDeck(m, parent) == null)
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
            else if (l.Contains("caba") && (l.Contains("leñador") || l.Contains("lenador")))
            {
                if (SpawnBuilding("CabanaLenador", p, 0f, parent) == null) Spawn("Hut", p, 0f, 1f, parent);
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
                if (SpawnDeck(m, parent) == null)
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

            var heroT = DioramaHeroCache.Hero;
            var boss = heroT != null ? BossMarker.Nearest(heroT.position) : null;
            if (boss != null)
            {
                Shadowed(new Rect(0f, 52f, w, 28f), boss.title, new Color(0.9f, 0.55f, 0.45f));
                float bw = 360f;
                var back = new Rect((w - bw) * 0.5f, 84f, bw, 12f);
                GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.9f);
                GUI.DrawTexture(new Rect(back.x - 2f, back.y - 2f, back.width + 4f, back.height + 4f), Texture2D.whiteTexture);
                GUI.color = new Color(0.62f, 0.2f, 0.17f, 1f);
                GUI.DrawTexture(new Rect(back.x, back.y, back.width * boss.Fraction, back.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

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
