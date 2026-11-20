using System.Collections.Generic;
using BrumaPx.Atmosphere;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A boss lair dug into a mountain. When a boss marker ("jefe") sits on a mountain or cliff cell, the map gets a
    /// wide chamber carved around it and a tunnel to the nearest open ground; the carved cells become "cueva"
    /// (walkable, dark wet stone). From outside a mound of rocks covers the cave, so it reads as a hole in the
    /// mountain; once the hero walks in, the mound is hidden and the light drops (DayNightWeather.CaveDarkness).
    /// The rocks around the chamber come from the usual mountain edge, so the walls need nothing extra.
    /// </summary>
    public sealed class CaveLair : MonoBehaviour
    {
        public sealed class Data
        {
            public string markerId;
            public Vector2 center;          // the boss marker (the lair inside)
            public readonly List<Vector2Int> cells = new List<Vector2Int>();
            public readonly HashSet<Vector2Int> mouthCells = new HashSet<Vector2Int>();
            public Vector2 mouth;           // last cave cell before open ground
            public Vector2 opening;         // first open cell outside
            public Vector2 outward;         // unit direction from the cave towards the outside
            public Vector2 nightSpot;       // where the boss waits at night, out on open ground
            public int cMin, cMax, rMin, rMax;
        }

        public static readonly List<Data> Lairs = new List<Data>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Lairs.Clear(); _insideCount = 0; }

        const float ChamberX = 11f, ChamberZ = 9f;    // half sizes of the chamber in metres (about 22 x 18 m)
        static readonly Vector2 ChamberShift = new Vector2(-1f, 1f);   // the boss rests a little off the middle
        static readonly Vector2 ToCamera = new Vector2(-0.7071f, -0.7071f); // the diorama camera looks north-east

        public static Data For(string markerId)
        {
            for (int i = 0; i < Lairs.Count; i++) if (Lairs[i].markerId == markerId) return Lairs[i];
            return null;
        }

        // ------------------------------------------------------------------------------------------ carving

        /// <summary>Carves every lair. Call after MapGrid.Load and before the ground texture is built.</summary>
        public static void CarveAll(MapFile file)
        {
            Lairs.Clear();
            if (file == null || file.markers == null) return;
            foreach (var m in file.markers)
                if (m.type == "jefe" && MapGrid.BlockedAt(m.x, m.z)) Carve(m);
        }

        static bool Rock(int c, int r)
        {
            byte id = MapGrid.IdAt(c, r);
            return MapGrid.InBounds(c, r) && (id == 7 || id == 8);   // cliffs and mountains, never water
        }

        static void Dig(Data d, int c, int r)
        {
            if (!Rock(c, r)) return;
            MapGrid.SetId(c, r, MapGrid.CaveId);
            d.cells.Add(new Vector2Int(c, r));
        }

        static void Carve(MapMarker m)
        {
            var d = new Data { markerId = m.id, center = new Vector2(m.x, m.z) };
            float mx = m.x + ChamberShift.x, mz = m.z + ChamberShift.y;
            int c0 = MapGrid.ColOf(mx - ChamberX - 2f), c1 = MapGrid.ColOf(mx + ChamberX + 2f);
            int r0 = MapGrid.RowOf(mz + ChamberZ + 2f), r1 = MapGrid.RowOf(mz - ChamberZ - 2f);
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    float dx = (MapGrid.CellX(c) - mx) / ChamberX, dz = (MapGrid.CellZ(r) - mz) / ChamberZ;
                    // a lumpy outline, the same every time
                    float wobble = (Mathf.PerlinNoise(c * 0.37f + 3.1f, r * 0.37f + 7.7f) - 0.5f) * 0.45f;
                    if (dx * dx + dz * dz <= 1f + wobble) Dig(d, c, r);
                }
            }
            if (d.cells.Count == 0) return;

            // Tunnel: the shortest way through rock from the chamber to open ground.
            var start = new List<Vector2Int>(d.cells);
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var seen = new HashSet<Vector2Int>(start);
            var queue = new Queue<Vector2Int>(start);
            var depth = new Dictionary<Vector2Int, int>();
            foreach (var c in start) depth[c] = 0;
            Vector2Int goal = new Vector2Int(-1, -1);
            float bestScore = float.MaxValue;
            var steps = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            // Every way out within 14 cells is a candidate; short ones that face the camera win, so the opening
            // can be seen from the diorama's point of view.
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                int dp = depth[p];
                foreach (var s in steps)
                {
                    var n = p + s;
                    if (seen.Contains(n) || !MapGrid.InBounds(n.x, n.y)) continue;
                    seen.Add(n);
                    byte id = MapGrid.IdAt(n.x, n.y);
                    if (!MapGrid.IsBlockedId(id) && id != MapGrid.CaveId)
                    {
                        Vector2 dir = new Vector2(MapGrid.CellX(n.x) - mx, MapGrid.CellZ(n.y) - mz).normalized;
                        float score = dp + 1 - 6f * Vector2.Dot(dir, ToCamera);
                        if (score < bestScore) { bestScore = score; goal = n; parent[n] = p; }
                        continue;
                    }
                    if (!Rock(n.x, n.y) || dp >= 14) continue;
                    parent[n] = p;
                    depth[n] = dp + 1;
                    queue.Enqueue(n);
                }
            }

            Vector2Int last = d.cells[0];
            if (goal.x >= 0)
            {
                var path = new List<Vector2Int>();
                var cur = parent[goal];
                while (parent.ContainsKey(cur)) { path.Add(cur); cur = parent[cur]; }
                last = path.Count > 0 ? path[0] : cur;
                // three cells wide (about 6 m): room for the boss and for a fight at the mouth
                foreach (var p in path)
                {
                    Dig(d, p.x, p.y);
                    foreach (var s in steps) Dig(d, p.x + s.x, p.y + s.y);
                }
                // the cells right at the opening (no roof over them)
                foreach (var s in steps)
                {
                    var q = goal + s;
                    if (MapGrid.IdAt(q.x, q.y) == MapGrid.CaveId) d.mouthCells.Add(q);
                }
                d.mouthCells.Add(last);
                Vector2 open = new Vector2(MapGrid.CellX(goal.x), MapGrid.CellZ(goal.y));
                d.opening = open;
                d.mouth = new Vector2(MapGrid.CellX(last.x), MapGrid.CellZ(last.y));
                d.outward = (open - d.center).normalized;
                // night: out on open ground, up to 12 m beyond the opening
                Vector2 spot = open;
                for (float t = 2f; t <= 12f; t += 1f)
                {
                    Vector2 q = open + d.outward * t;
                    if (MapGrid.BlockedAt(q.x, q.y) || MapGrid.IdAtWorld(q.x, q.y) == MapGrid.CaveId) break;
                    spot = q;
                }
                d.nightSpot = spot;
            }
            else
            {
                d.mouth = d.center;
                d.opening = d.center;
                d.outward = Vector2.right;
                d.nightSpot = d.center;
            }

            d.cMin = d.rMin = int.MaxValue;
            d.cMax = d.rMax = int.MinValue;
            foreach (var c in d.cells)
            {
                d.cMin = Mathf.Min(d.cMin, c.x); d.cMax = Mathf.Max(d.cMax, c.x);
                d.rMin = Mathf.Min(d.rMin, c.y); d.rMax = Mathf.Max(d.rMax, c.y);
            }
            Lairs.Add(d);
        }

        // ------------------------------------------------------------------------------------------ the mound and the darkness

        public Data data;
        public MapWorld world;

        readonly List<GameObject> _roof = new List<GameObject>();
        static int _insideCount;
        Transform _hero;
        bool _inside, _built;

        public static CaveLair Create(MapWorld world, Data d)
        {
            var go = new GameObject("Cueva " + d.markerId);
            go.transform.SetParent(world.transform, false);
            go.transform.position = new Vector3(d.center.x, 0f, d.center.y);
            var lair = go.AddComponent<CaveLair>();
            lair.data = d;
            lair.world = world;
            return lair;
        }

        void Start()
        {
            BuildRoof();
            BuildGlows();
        }

        // A mound of the same rocks as the mountains, over every cave cell but the opening.
        void BuildRoof()
        {
            var rng = new System.Random(data.markerId.GetHashCode() ^ 7177);
            foreach (var c in data.cells)
            {
                if (data.mouthCells.Contains(c)) continue;
                // leave the last stretch of the tunnel uncovered: from outside it reads as a dark notch in the rock
                if (Vector2.Distance(new Vector2(MapGrid.CellX(c.x), MapGrid.CellZ(c.y)), data.mouth) < 4.5f) continue;
                float x = MapGrid.CellX(c.x) + ((float)rng.NextDouble() - 0.5f) * 0.8f;
                float z = MapGrid.CellZ(c.y) + ((float)rng.NextDouble() - 0.5f) * 0.8f;
                // higher towards the middle of the chamber
                float dist = Vector2.Distance(new Vector2(x, z), data.center);
                float scale = Mathf.Lerp(3.2f, 2.3f, Mathf.Clamp01(dist / 9f)) + (float)rng.NextDouble() * 0.5f;
                var rock = world.SpawnProp("RockBig", new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, scale, transform);
                if (rock == null) continue;
                rock.name = "Techo";
                foreach (var o in rock.GetComponentsInChildren<WorldObstacle>(true)) Destroy(o);
                rock.transform.position += Vector3.up * Mathf.Lerp(0.9f, 0.3f, Mathf.Clamp01(dist / 9f));
                _roof.Add(rock);
            }
            BuildMouth();
            _built = true;
        }

        // The opening: a long rock laid across it as a lintel, and darkness behind, facing the camera, so from
        // outside it reads as a hole into the mountain.
        void BuildMouth()
        {
            Vector2 o = data.outward;
            Vector2 side = new Vector2(-o.y, o.x);
            Vector3 m = new Vector3(data.mouth.x, 0f, data.mouth.y);
            var lintel = world.SpawnProp("RockBig", m - new Vector3(o.x, 0f, o.y) * 0.6f + Vector3.up * 1.9f,
                                         Mathf.Atan2(side.x, side.y) * Mathf.Rad2Deg + 90f, 1f, transform);
            if (lintel != null)
            {
                lintel.name = "Dintel";
                foreach (var ob in lintel.GetComponentsInChildren<WorldObstacle>(true)) Destroy(ob);
                lintel.transform.localScale = new Vector3(3.6f, 1.5f, 1.8f);
                _roof.Add(lintel);
            }

            var hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hole.name = "Oscuridad de la cueva";
            Destroy(hole.GetComponent<Collider>());
            hole.transform.SetParent(transform, false);
            var cam = Camera.main;
            Vector3 toCam = cam != null ? -cam.transform.forward : new Vector3(-0.6f, 0.5f, -0.6f);
            Vector3 flat = new Vector3(toCam.x, 0f, toCam.z).normalized;
            hole.transform.position = m - new Vector3(o.x, 0f, o.y) * 1.4f + Vector3.up * 1.25f;
            hole.transform.rotation = Quaternion.LookRotation(-flat, Vector3.up);
            hole.transform.localScale = new Vector3(4.2f, 2.6f, 1f);
            // same toon shader as the rocks (so it fades with the island and the fog like everything else), almost black
            Material mat = null;
            var toon = Shader.Find("BrumaPx/Diorama/PPToon");
            if (toon != null)
            {
                mat = new Material(toon);
                if (mat.HasProperty("_OutlineTexels")) mat.SetFloat("_OutlineTexels", 0f);
                if (mat.HasProperty("_CastShadow")) mat.SetFloat("_CastShadow", 0f);
            }
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh != null) mat = new Material(sh);
            }
            if (mat != null)
            {
                mat.SetColor("_BaseColor", new Color(0.055f, 0.045f, 0.04f, 1f));
                hole.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            hole.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _roof.Add(hole);
        }

        // A few dim violet glows (Bruma pustules on the walls), the only light inside besides the hero's own.
        void BuildGlows()
        {
            var rng = new System.Random(data.markerId.GetHashCode() ^ 3301);
            int made = 0;
            for (int i = 0; i < data.cells.Count && made < 4; i++)
            {
                var c = data.cells[(i * 7 + 3) % data.cells.Count];
                if (data.mouthCells.Contains(c)) continue;
                // only cells against the wall
                if (!MapGrid.IsBlockedId(MapGrid.IdAt(c.x + 1, c.y)) && !MapGrid.IsBlockedId(MapGrid.IdAt(c.x - 1, c.y))
                    && !MapGrid.IsBlockedId(MapGrid.IdAt(c.x, c.y + 1)) && !MapGrid.IsBlockedId(MapGrid.IdAt(c.x, c.y - 1))) continue;
                if (rng.NextDouble() < 0.35) continue;
                var g = new GameObject("Brillo de Bruma");
                g.transform.SetParent(transform, false);
                g.transform.position = new Vector3(MapGrid.CellX(c.x), 0f, MapGrid.CellZ(c.y));
                var l = g.AddComponent<PixelLight>();
                l.offset = new Vector3(0f, 0.6f, 0f);
                l.radius = 3.2f;
                l.color = new Color(0.55f, 0.42f, 0.85f, 1f);
                l.intensity = 0.8f;
                l.flicker = 0.15f;
                l.countsForVisibility = false;
                if (world.glow != null)
                {
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(bulb.GetComponent<Collider>());
                    bulb.transform.SetParent(g.transform, false);
                    bulb.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                    bulb.transform.localScale = new Vector3(0.35f, 0.28f, 0.35f);
                    bulb.GetComponent<MeshRenderer>().sharedMaterial = world.glow;
                }
                made++;
            }
        }

        public bool Contains(Vector3 p) { return Inside(data, p); }

        /// <summary>True when the point stands on a cell of this lair.</summary>
        public static bool Inside(Data d, Vector3 p)
        {
            if (d == null) return false;
            int c = MapGrid.ColOf(p.x), r = MapGrid.RowOf(p.z);
            return c >= d.cMin && c <= d.cMax && r >= d.rMin && r <= d.rMax && MapGrid.IdAt(c, r) == MapGrid.CaveId;
        }

        void Update()
        {
            if (!_built) return;
            if (_hero == null)
            {
                var h = DioramaHeroCache.Hero;
                if (h == null) return;
                _hero = h;
            }
            bool inside = Contains(_hero.position);
            if (inside != _inside)
            {
                _inside = inside;
                _insideCount += inside ? 1 : -1;
                for (int i = 0; i < _roof.Count; i++) if (_roof[i] != null) _roof[i].SetActive(!inside);
            }
            DayNightWeather.CaveDarknessTarget = _insideCount > 0 ? 1f : 0f;
        }
    }
}
