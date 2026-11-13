using BrumaPx.Rendering;
using BrumaPx.World;
using BrumaPx.Player;
using BrumaPx.Dialogue;
using BrumaPx.Atmosphere;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Extends the Overworld scene: chunked world made from clones of the hand-built props, a hero that can walk,
    /// a talkable NPC with a portrait, and a dialogue UI. Safe to run again: generated content is rebuilt.
    /// Menu: BrumaPx / Overworld / Rebuild World.
    /// </summary>
    public static class OverworldBuilder
    {
        const string ScenePath = "Assets/Scenes/Overworld.unity";
        const string MatDir = "Assets/Art/Materials/Diorama/";
        const float Cell = 16f;
        const int Half = 2;
        const float Limit = 38f;

        static readonly float[][] Paths =
        {
            new[] { -0.7f, -40f, -0.7f, 3.7f },
            new[] { -0.7f, -10f, 12f, -14f },
            new[] { 12f, -14f, 40f, -11f },
            new[] { -0.7f, 2f, -14f, 8f },
            new[] { -14f, 8f, -40f, 20f }
        };

        static Transform _chunksRoot;
        static Dictionary<Vector2Int, Transform> _chunks;
        static readonly List<Vector3> Occupied = new List<Vector3>();   // x, z, radius
        static readonly List<Vector3> Zones = new List<Vector3>();      // x, z, radius kept free of random scatter
        static Font _font;

        [MenuItem("BrumaPx/Overworld/Rebuild World")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            _chunks = new Dictionary<Vector2Int, Transform>();
            Occupied.Clear();
            Zones.Clear();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Transform world = GameObject.Find("World").transform;

            // 1. Chunk root + home chunk holding the hand-built props.
            Transform home = PrepareHome(world);

            Transform tree = home.Find("Dead Tree");
            Transform graves = home.Find("Graves");
            Transform fence = home.Find("Fence");
            Transform lantern = home.Find("Lantern Post");
            Transform props = home.Find("Props");
            Transform hut = home.Find("Hut");

            AddHomeObstacles(tree, graves, fence, lantern, props, hut);
            AddHomeLights(lantern, hut);
            TrimFence(fence);

            // Occupied cells inside the home area so scatter never lands there.
            Zones.Add(new Vector3(0f, 0f, 9.5f));
            Zones.Add(new Vector3(0f, 0f, -9.5f)); // spawn + NPC corridor handled below as one big area
            Zones.RemoveAt(Zones.Count - 1);

            // 2. Landmarks.
            Vector3 npcPos = new Vector3(-2.8f, 0f, -9.0f);
            BuildNpc(npcPos, 45f);
            Zones.Add(new Vector3(npcPos.x, npcPos.z, 3.2f));
            PlaceLandmarks(tree, graves, fence, lantern, props, hut);

            // 3. Random scatter in every other chunk.
            Scatter(tree, graves, fence, props);

            // 4. Hero, rig, UI.
            WireHeroAndRig(world);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Overworld: world rebuilt (" + _chunks.Count + " chunks).");
        }

        // ---------------------------------------------------------------------------------------------- chunks

        static Transform PrepareHome(Transform world)
        {
            var existing = GameObject.Find("Chunks");
            Transform home;
            if (existing != null)
            {
                _chunksRoot = existing.transform;
                home = _chunksRoot.Find("Chunk 0,0");
                for (int i = _chunksRoot.childCount - 1; i >= 0; i--)
                {
                    var c = _chunksRoot.GetChild(i);
                    if (c != home) Object.DestroyImmediate(c.gameObject);
                }
            }
            else
            {
                _chunksRoot = new GameObject("Chunks").transform;
                home = null;
            }

            foreach (var n in new[] { "Vigia del Camino", "Portrait Stage", "Portrait Camera", "UI", "Atmosphere" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            if (home == null)
            {
                home = MakeChunk(0, 0);
                foreach (var n in new[] { "Hut", "Dead Tree", "Graves", "Fence", "Lantern Post", "Props" })
                    world.Find(n).SetParent(home, true);
            }
            else
            {
                _chunks[new Vector2Int(0, 0)] = home;
            }
            return home;
        }

        static Transform MakeChunk(int cx, int cz)
        {
            var go = new GameObject("Chunk " + cx + "," + cz);
            go.transform.SetParent(_chunksRoot, false);
            go.AddComponent<ChunkInfo>().center = new Vector3(cx * Cell, 0f, cz * Cell);
            _chunks[new Vector2Int(cx, cz)] = go.transform;
            return go.transform;
        }

        static Transform ChunkAt(Vector3 p)
        {
            int cx = Mathf.Clamp(Mathf.RoundToInt(p.x / Cell), -Half, Half);
            int cz = Mathf.Clamp(Mathf.RoundToInt(p.z / Cell), -Half, Half);
            Transform t;
            return _chunks.TryGetValue(new Vector2Int(cx, cz), out t) ? t : MakeChunk(cx, cz);
        }

        // ------------------------------------------------------------------------------------------ home props

        static void AddCircle(Transform t, float r)
        {
            if (t == null || t.GetComponent<WorldObstacle>() != null) return;
            var o = t.gameObject.AddComponent<WorldObstacle>();
            o.kind = WorldObstacle.Kind.Circle;
            o.radius = r;
        }

        static void AddBox(Transform t, Vector2 half)
        {
            if (t == null || t.GetComponent<WorldObstacle>() != null) return;
            var o = t.gameObject.AddComponent<WorldObstacle>();
            o.kind = WorldObstacle.Kind.Box;
            o.halfSize = half;
        }

        static void AddHomeObstacles(Transform tree, Transform graves, Transform fence, Transform lantern, Transform props, Transform hut)
        {
            AddCircle(tree, 0.55f);
            AddCircle(lantern, 0.3f);
            AddBox(hut, new Vector2(1.9f, 1.6f));
            AddCircle(graves.Find("Stone A"), 0.5f);
            AddCircle(graves.Find("Stone B"), 0.45f);
            AddCircle(graves.Find("Cross Post"), 0.3f);
            AddCircle(props.Find("Barrel A"), 0.5f);
            AddCircle(props.Find("Barrel B"), 0.42f);
            AddBox(props.Find("Crate"), new Vector2(0.5f, 0.5f));
            AddCircle(props.Find("Rock Big"), 0.85f);
            AddCircle(props.Find("Rock Mid"), 0.45f);
            AddCircle(props.Find("Rock Right"), 0.65f);
            foreach (Transform c in fence)
                if (c.name.StartsWith("Post")) AddCircle(c, 0.2f);
        }

        // The fence used to cross the path, so the first post pair goes and the rails are shortened.
        static void TrimFence(Transform fence)
        {
            var p0 = fence.Find("Post 0");
            if (p0 != null) Object.DestroyImmediate(p0.gameObject);
            var p1 = fence.Find("Post 1");
            if (p1 != null) Object.DestroyImmediate(p1.gameObject);
            foreach (var rn in new[] { "Rail Top", "Rail Low" })
            {
                var r = fence.Find(rn);
                if (r == null) continue;
                r.position = new Vector3(2.25f, r.position.y, r.position.z);
                var s = r.localScale; s.x = 3.9f; r.localScale = s;
            }
        }

        // -------------------------------------------------------------------------------------------- helpers

        static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "PP_" + n + ".mat"); }

        static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 lpos, Vector3 scale, Vector3 euler, string mat, bool shadow = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = lpos;
            t.localEulerAngles = euler;
            t.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterials = shadow ? new[] { M(mat), M("Shadow") } : new[] { M(mat) };
            return go;
        }

        static Transform Group(string name, Vector3 pos, float yaw)
        {
            var g = new GameObject(name).transform;
            g.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            g.SetParent(ChunkAt(pos), true);
            return g;
        }

        /// <summary>Clones props (kept together) and drops the copy at pos. anchor is the point of the originals that lands on pos.</summary>
        static Transform Place(IList<Transform> srcs, Vector3 anchor, Vector3 pos, float yaw, float scale)
        {
            var w = new GameObject(srcs[0].name).transform;
            w.position = new Vector3(anchor.x, 0f, anchor.z);
            foreach (var s in srcs) Object.Instantiate(s.gameObject, w, true).name = s.name;
            w.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            w.localScale = Vector3.one * scale;
            w.SetParent(ChunkAt(pos), true);

            bool quarter = Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Round(yaw / 90f) * 90f)) < 0.5f;
            foreach (var o in w.GetComponentsInChildren<WorldObstacle>(true))
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
            return w;
        }

        static Transform Place(Transform src, Vector3 pos, float yaw, float scale)
        {
            return Place(new[] { src }, src.position, pos, yaw, scale);
        }

        static float PathDist(float x, float z)
        {
            float d = float.MaxValue;
            for (int i = 0; i < Paths.Length; i++)
            {
                var s = Paths[i];
                float ax = s[0], az = s[1], bx = s[2], bz = s[3];
                float abx = bx - ax, abz = bz - az;
                float t = Mathf.Clamp01(((x - ax) * abx + (z - az) * abz) / (abx * abx + abz * abz));
                float dx = x - (ax + abx * t), dz = z - (az + abz * t);
                d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return d;
        }

        static bool Free(float x, float z, float r, bool keepOffPath = true)
        {
            if (Mathf.Abs(x) > Limit || Mathf.Abs(z) > Limit) return false;
            if (Mathf.Abs(x) < 9.5f && Mathf.Abs(z) < 9.5f) return false;
            if (keepOffPath && PathDist(x, z) < 2.4f + r * 0.5f) return false;
            foreach (var zn in Zones)
            {
                float dx = x - zn.x, dz = z - zn.y;
                if (dx * dx + dz * dz < (zn.z + r) * (zn.z + r)) return false;
            }
            foreach (var o in Occupied)
            {
                float dx = x - o.x, dz = z - o.y;
                float min = r + o.z + 0.5f;
                if (dx * dx + dz * dz < min * min) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------- landmarks

        static void PlaceLandmarks(Transform tree, Transform graves, Transform fence, Transform lantern, Transform props, Transform hut)
        {
            // Lanterns that mark the paths.
            foreach (var p in new[] { new Vector2(1.3f, -13.5f), new Vector2(1.3f, -24f), new Vector2(1.3f, -34f), new Vector2(14.5f, -11.8f), new Vector2(26f, -9.2f), new Vector2(-13f, 10.4f), new Vector2(-26f, 15f) })
            {
                Place(lantern, new Vector3(p.x, 0f, p.y), 0f, 1f);
                Zones.Add(new Vector3(p.x, p.y, 1.2f));
            }

            BuildWell(new Vector3(-12.5f, 0f, 4.6f), hut);
            Zones.Add(new Vector3(-12.5f, 4.6f, 3.4f));

            BuildRuin(new Vector3(-24f, 0f, 6f));
            Zones.Add(new Vector3(-24f, 6f, 5.5f));

            BuildCart(new Vector3(22f, 0f, -8.5f), 30f);
            var camp = new List<Transform> { props.Find("Barrel A"), props.Find("Barrel A Band"), props.Find("Barrel B"), props.Find("Crate") };
            Place(camp, new Vector3(-1.5f, 0f, 3f), new Vector3(24.8f, 0f, -7.4f), 70f, 1f);
            Zones.Add(new Vector3(23f, -8f, 4.8f));

            // Old graveyard south of the east branch.
            var rng = new System.Random(77);
            var stoneA = graves.Find("Stone A");
            var stoneB = graves.Find("Stone B");
            var cross = new List<Transform> { graves.Find("Cross Post"), graves.Find("Cross Arm") };
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = 8.8f + i * 2.9f + (float)(rng.NextDouble() - 0.5) * 0.6f;
                    float z = -19.6f - row * 2.8f + (float)(rng.NextDouble() - 0.5) * 0.6f;
                    float yaw = (float)(rng.NextDouble() - 0.5) * 30f;
                    float sc = 0.85f + (float)rng.NextDouble() * 0.25f;
                    int kind = rng.Next(3);
                    var pos = new Vector3(x, 0f, z);
                    if (kind == 0) Place(stoneA, pos, yaw, sc);
                    else if (kind == 1) Place(stoneB, pos, yaw, sc);
                    else Place(cross, cross[0].position, pos, yaw, sc);
                }
            }
            Place(fence, new Vector3(10.6f, 0f, -17.4f), 0f, 1f);
            Place(fence, new Vector3(18.4f, 0f, -17.4f), 0f, 1f);
            Place(tree, new Vector3(7.2f, 0f, -22.5f), 20f, 1.2f);
            Place(tree, new Vector3(21.8f, 0f, -23f), 200f, 1.1f);
            Zones.Add(new Vector3(14.5f, -21f, 10.5f));
        }

        static void BuildWell(Vector3 pos, Transform hut)
        {
            var g = Group("Well", pos, 0f);
            Prim(g, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.45f, 1.3f), Vector3.zero, "Stone");
            Prim(g, PrimitiveType.Cylinder, "Water", new Vector3(0f, 0.92f, 0f), new Vector3(0.96f, 0.02f, 0.96f), Vector3.zero, "Black", false);
            Prim(g, PrimitiveType.Cube, "Post L", new Vector3(-0.62f, 1.45f, 0f), new Vector3(0.14f, 1.4f, 0.14f), Vector3.zero, "WoodDark");
            Prim(g, PrimitiveType.Cube, "Post R", new Vector3(0.62f, 1.45f, 0f), new Vector3(0.14f, 1.4f, 0.14f), Vector3.zero, "WoodDark");
            Prim(g, PrimitiveType.Cube, "Beam", new Vector3(0f, 1.9f, 0f), new Vector3(1.4f, 0.1f, 0.1f), Vector3.zero, "Wood");
            var roof = Object.Instantiate(hut.Find("Roof").gameObject, g);
            roof.name = "Roof";
            roof.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            roof.transform.localRotation = Quaternion.identity;
            roof.transform.localScale = new Vector3(0.42f, 0.5f, 0.3f);
            var o = g.gameObject.AddComponent<WorldObstacle>();
            o.kind = WorldObstacle.Kind.Circle;
            o.radius = 1.05f;
        }

        static void BuildRuin(Vector3 pos)
        {
            var g = Group("Ruined Hut", pos, 0f);
            Prim(g, PrimitiveType.Cube, "Slab", new Vector3(0f, 0.12f, 0f), new Vector3(3.6f, 0.25f, 3.0f), Vector3.zero, "Stone");
            Prim(g, PrimitiveType.Cube, "Wall Back", new Vector3(0f, 1.0f, -1.3f), new Vector3(3.2f, 1.6f, 0.35f), Vector3.zero, "Wood");
            Prim(g, PrimitiveType.Cube, "Wall Left", new Vector3(-1.6f, 0.75f, 0f), new Vector3(0.35f, 1.0f, 2.6f), Vector3.zero, "Wood");
            Prim(g, PrimitiveType.Cube, "Wall Right Stub", new Vector3(1.6f, 0.55f, 0.7f), new Vector3(0.35f, 0.6f, 1.2f), Vector3.zero, "WoodDark");
            Prim(g, PrimitiveType.Cube, "Wall Front Stub", new Vector3(-1.1f, 0.6f, 1.3f), new Vector3(1.0f, 0.7f, 0.35f), Vector3.zero, "WoodDark");
            Prim(g, PrimitiveType.Cube, "Chimney", new Vector3(1.1f, 1.45f, -1.0f), new Vector3(0.5f, 2.3f, 0.5f), Vector3.zero, "Stone");
            Prim(g, PrimitiveType.Cube, "Fallen Beam", new Vector3(0.2f, 0.4f, 0.4f), new Vector3(2.6f, 0.14f, 0.2f), new Vector3(0f, 20f, 14f), "WoodDark");
            var o = g.gameObject.AddComponent<WorldObstacle>();
            o.kind = WorldObstacle.Kind.Box;
            o.halfSize = new Vector2(1.9f, 1.6f);
        }

        static void BuildCart(Vector3 pos, float yaw)
        {
            var g = Group("Cart", pos, yaw);
            Prim(g, PrimitiveType.Cube, "Bed", new Vector3(0f, 0.65f, 0f), new Vector3(1.9f, 0.18f, 1.0f), Vector3.zero, "Wood");
            Prim(g, PrimitiveType.Cube, "Side A", new Vector3(0f, 0.92f, 0.5f), new Vector3(1.9f, 0.36f, 0.08f), Vector3.zero, "WoodDark");
            Prim(g, PrimitiveType.Cube, "Side B", new Vector3(0f, 0.92f, -0.5f), new Vector3(1.9f, 0.36f, 0.08f), Vector3.zero, "WoodDark");
            Prim(g, PrimitiveType.Cylinder, "Wheel A", new Vector3(0f, 0.4f, 0.62f), new Vector3(0.8f, 0.07f, 0.8f), new Vector3(90f, 0f, 0f), "WoodDark");
            Prim(g, PrimitiveType.Cylinder, "Wheel B", new Vector3(0f, 0.4f, -0.62f), new Vector3(0.8f, 0.07f, 0.8f), new Vector3(90f, 0f, 0f), "WoodDark");
            Prim(g, PrimitiveType.Cube, "Handle A", new Vector3(1.55f, 0.5f, 0.3f), new Vector3(1.3f, 0.08f, 0.08f), new Vector3(0f, 0f, -12f), "Wood");
            Prim(g, PrimitiveType.Cube, "Handle B", new Vector3(1.55f, 0.5f, -0.3f), new Vector3(1.3f, 0.08f, 0.08f), new Vector3(0f, 0f, -12f), "Wood");
            var o = g.gameObject.AddComponent<WorldObstacle>();
            o.kind = WorldObstacle.Kind.Circle;
            o.radius = 1.1f;
        }

        // ------------------------------------------------------------------------------------------ scatter

        static void Scatter(Transform tree, Transform graves, Transform fence, Transform props)
        {
            var rng = new System.Random(20261113);
            var rocks = new[] { props.Find("Rock Big"), props.Find("Rock Mid"), props.Find("Rock Right"), props.Find("Rock Small") };
            var cross = new List<Transform> { graves.Find("Cross Post"), graves.Find("Cross Arm") };

            // Occupied entries for the landmarks that are already down (so scatter keeps its distance from them too).
            for (int cz = -Half; cz <= Half; cz++)
            {
                for (int cx = -Half; cx <= Half; cx++)
                {
                    if (cx == 0 && cz == 0) continue;
                    float ox = cx * Cell, oz = cz * Cell;

                    int trees = 3 + rng.Next(3);
                    for (int i = 0; i < trees; i++)
                        TryPlace(rng, ox, oz, 0.9f, delegate (Vector3 p)
                        {
                            Place(tree, p, (float)rng.NextDouble() * 360f, 0.85f + (float)rng.NextDouble() * 0.5f);
                        });

                    int rk = 2 + rng.Next(3);
                    for (int i = 0; i < rk; i++)
                    {
                        var r = rocks[rng.Next(rocks.Length)];
                        float sc = 0.8f + (float)rng.NextDouble() * 0.9f;
                        TryPlace(rng, ox, oz, 0.7f * sc, delegate (Vector3 p)
                        {
                            Place(r, p, (float)rng.NextDouble() * 360f, sc);
                        });
                    }

                    if (rng.NextDouble() < 0.4)
                        TryPlace(rng, ox, oz, 1.6f, delegate (Vector3 p)
                        {
                            float yaw = (float)rng.NextDouble() * 360f;
                            Place(cross, cross[0].position, p, yaw, 1f);
                            Place(graves.Find("Stone A"), p + new Vector3(1.3f, 0f, 0.5f), yaw + 8f, 0.95f);
                        });

                    if (rng.NextDouble() < 0.35)
                        TryPlace(rng, ox, oz, 2.2f, delegate (Vector3 p)
                        {
                            Place(fence, p, (float)Mathf.Round((float)rng.NextDouble() * 4f) * 45f, 1f);
                        });
                }
            }
        }

        static void TryPlace(System.Random rng, float ox, float oz, float radius, System.Action<Vector3> place)
        {
            for (int k = 0; k < 25; k++)
            {
                float x = ox + ((float)rng.NextDouble() - 0.5f) * (Cell - 1.2f);
                float z = oz + ((float)rng.NextDouble() - 0.5f) * (Cell - 1.2f);
                if (!Free(x, z, radius)) continue;
                Occupied.Add(new Vector3(x, z, radius));
                place(new Vector3(x, 0f, z));
                return;
            }
        }

        // ---------------------------------------------------------------------------------------------- NPC

        static void BuildNpc(Vector3 pos, float yaw)
        {
            var g = Group("Vigia del Camino", pos, yaw);
            Prim(g, PrimitiveType.Cylinder, "Robe", new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.55f, 0.7f), Vector3.zero, "Roof");
            Prim(g, PrimitiveType.Sphere, "Shoulders", new Vector3(0f, 1.1f, 0f), new Vector3(0.78f, 0.5f, 0.55f), Vector3.zero, "Roof");
            Prim(g, PrimitiveType.Sphere, "Hood", new Vector3(0f, 1.45f, 0f), new Vector3(0.52f, 0.56f, 0.52f), Vector3.zero, "Roof");
            Prim(g, PrimitiveType.Sphere, "Face", new Vector3(0f, 1.42f, 0.13f), new Vector3(0.34f, 0.36f, 0.3f), Vector3.zero, "HeroScarf");
            Prim(g, PrimitiveType.Cube, "Eye L", new Vector3(-0.08f, 1.45f, 0.28f), new Vector3(0.06f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(g, PrimitiveType.Cube, "Eye R", new Vector3(0.08f, 1.45f, 0.28f), new Vector3(0.06f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(g, PrimitiveType.Cube, "Staff", new Vector3(0.48f, 0.95f, 0.1f), new Vector3(0.07f, 1.9f, 0.07f), Vector3.zero, "Wood");
            var staffLight = Prim(g, PrimitiveType.Sphere, "Staff Light", new Vector3(0.48f, 1.98f, 0.1f), new Vector3(0.16f, 0.16f, 0.16f), Vector3.zero, "Glow", false);
            AddLight(staffLight.transform, Vector3.zero, 3.4f, new Color(1f, 0.62f, 0.3f), 0.8f, 0.25f);

            var anchor = new GameObject("Prompt Anchor").transform;
            anchor.SetParent(g, false);
            anchor.localPosition = new Vector3(0f, 2.55f, 0f);

            var rtTex = LoadOrCreatePortraitRt();
            BuildPortraitStage(rtTex);

            var npc = g.gameObject.AddComponent<TalkableNpc>();
            npc.displayName = "Vigía del Camino";
            npc.lines = new[]
            {
                "Alto ahí, viajero. Pocos pasan por este camino cuando la bruma baja.",
                "Dicen que el barro se traga las huellas... y a quien las deja.",
                "Si buscas refugio, la cabaña del norte sigue en pie. La del oeste, no tanto.",
                "Mantén la linterna cerca. La niebla no es lo peor que vive aquí."
            };
            npc.portrait = rtTex;
            npc.interactRadius = 2.8f;
            npc.promptAnchor = anchor;

            var o = g.gameObject.AddComponent<WorldObstacle>();
            o.kind = WorldObstacle.Kind.Circle;
            o.radius = 0.5f;
        }

        static RenderTexture LoadOrCreatePortraitRt()
        {
            const string path = "Assets/Art/Textures/Portrait_RT.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt != null) return rt;
            var desc = new RenderTextureDescriptor(72, 72, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.D24_UNorm_S8_UInt, 1);
            rt = new RenderTexture(desc) { name = "Portrait_RT", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            AssetDatabase.CreateAsset(rt, path);
            return rt;
        }

        // A bust far away from the world, filmed by its own camera into a 72x72 texture.
        static void BuildPortraitStage(RenderTexture rt)
        {
            Vector3 c = new Vector3(400f, 0f, 400f);
            var stage = new GameObject("Portrait Stage").transform;
            stage.position = c;
            Prim(stage, PrimitiveType.Sphere, "Shoulders", new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 0.5f, 0.6f), Vector3.zero, "Roof", false);
            Prim(stage, PrimitiveType.Sphere, "Hood", new Vector3(0f, 1.38f, 0f), new Vector3(0.58f, 0.62f, 0.58f), Vector3.zero, "Roof", false);
            Prim(stage, PrimitiveType.Sphere, "Face", new Vector3(0f, 1.34f, 0.15f), new Vector3(0.38f, 0.4f, 0.34f), Vector3.zero, "HeroScarf", false);
            Prim(stage, PrimitiveType.Cube, "Eye L", new Vector3(-0.09f, 1.38f, 0.31f), new Vector3(0.07f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(stage, PrimitiveType.Cube, "Eye R", new Vector3(0.09f, 1.38f, 0.31f), new Vector3(0.07f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(stage, PrimitiveType.Cube, "Mouth", new Vector3(0f, 1.25f, 0.325f), new Vector3(0.1f, 0.02f, 0.02f), Vector3.zero, "Black", false);

            var camGo = new GameObject("Portrait Camera");
            camGo.transform.SetPositionAndRotation(c + new Vector3(0f, 1.3f, 4f), Quaternion.Euler(6f, 180f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.52f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x2A, 0x24, 0x20, 255);
            cam.allowMSAA = false;
            cam.allowHDR = false;
            cam.depth = -30;
            cam.targetTexture = rt;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;

            var rig = Object.FindFirstObjectByType<PixelDioramaRig>();
            if (rig != null) rig.portraitCamera = cam;
        }

        // -------------------------------------------------------------------------------------- hero, rig, UI

        static void WireHeroAndRig(Transform world)
        {
            var heroT = world.Find("Hero");
            var body = heroT.Find("Body");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(body.gameObject);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(heroT.gameObject);
            var legL = heroT.Find("Leg L");
            var legR = heroT.Find("Leg R");
            var bootL = heroT.Find("Boot L");
            var bootR = heroT.Find("Boot R");
            if (bootL != null && bootL.parent == heroT) bootL.SetParent(legL, true);
            if (bootR != null && bootR.parent == heroT) bootR.SetParent(legR, true);

            var rig = Object.FindFirstObjectByType<PixelDioramaRig>();
            var cam = GameObject.Find("Diorama Camera").GetComponent<Camera>();

            Text chunkLabel;
            DialogueBox dlg;
            RectTransform prompt;
            BuildUi(rig, out dlg, out prompt, out chunkLabel);

            var hero = heroT.GetComponent<DioramaHero>();
            if (hero == null) hero = heroT.gameObject.AddComponent<DioramaHero>();
            hero.sceneCamera = cam;
            hero.body = body;
            hero.legL = legL;
            hero.legR = legR;
            hero.pixelsPerUnit = rig.pixelsPerUnit;
            hero.worldLimit = Limit;
            hero.dialogue = dlg;
            hero.prompt = prompt;

            rig.focus = heroT;
            rig.displayCamera = GameObject.Find("Display Camera").GetComponent<Camera>();
            rig.followers = new[] { world.Find("Ground"), world.Find("Mist Low"), world.Find("Mist High") };

            var streamer = _chunksRoot.GetComponent<ChunkStreamer>();
            if (streamer == null) streamer = _chunksRoot.gameObject.AddComponent<ChunkStreamer>();
            streamer.focus = heroT;
            streamer.chunkRoot = _chunksRoot;
            streamer.activateRadius = 22f;
            streamer.label = chunkLabel;

            EditorUtility.SetDirty(rig);

            BuildAtmosphere(rig, cam, heroT);
        }

        static Text _clockLabel;

        static void AddLight(Transform t, Vector3 offset, float radius, Color color, float intensity, float flicker)
        {
            if (t == null || t.GetComponent<PixelLight>() != null) return;
            var l = t.gameObject.AddComponent<PixelLight>();
            l.offset = offset;
            l.radius = radius;
            l.color = color;
            l.intensity = intensity;
            l.flicker = flicker;
        }

        // Lantern posts and the hut window light their surroundings after dark.
        static void AddHomeLights(Transform lantern, Transform hut)
        {
            AddLight(lantern, new Vector3(0f, 2.45f, 0f), 5.5f, new Color(1f, 0.62f, 0.3f), 1f, 0.25f);
            AddLight(hut, new Vector3(-2.4f, 0.8f, 0.2f), 4.2f, new Color(1f, 0.68f, 0.32f), 0.9f, 0.2f);
        }

        // Rain overlay, the day/night + weather controller and the hero's small light.
        static void BuildAtmosphere(PixelDioramaRig rig, Camera cam, Transform hero)
        {
            var oldRain = cam.transform.Find("Rain Overlay");
            if (oldRain != null) Object.DestroyImmediate(oldRain.gameObject);

            const string rainPath = MatDir + "PP_Rain.mat";
            var rainMat = AssetDatabase.LoadAssetAtPath<Material>(rainPath);
            if (rainMat == null)
            {
                rainMat = new Material(Shader.Find("BrumaPx/Diorama/PPRain"));
                AssetDatabase.CreateAsset(rainMat, rainPath);
            }
            rainMat.SetVector("_QuadSize", new Vector4(24.6f, 13.9f, 0f, 0f));
            EditorUtility.SetDirty(rainMat);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Rain Overlay";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(cam.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 30f);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(24.6f, 13.9f, 1f);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = rainMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 100;

            var go = new GameObject("Atmosphere");
            var dnw = go.AddComponent<DayNightWeather>();
            dnw.rig = rig;
            dnw.focus = hero;
            dnw.hud = _clockLabel;

            AddLight(hero, new Vector3(0f, 1.2f, 0f), 3.2f, new Color(0.82f, 0.8f, 0.66f), 0.5f, 0f);
        }

        static void BuildUi(PixelDioramaRig rig, out DialogueBox dlg, out RectTransform prompt, out Text chunkLabel)
        {
            var root = new GameObject("UI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Color border = new Color32(0x7A, 0x66, 0x50, 255);
            Color panelBg = new Color32(0x1B, 0x17, 0x14, 240);
            Color cream = new Color32(0xC4, 0xBF, 0xB2, 255);
            Color warm = new Color32(0xCD, 0xBB, 0x94, 255);
            Color mid = new Color32(0x9A, 0x96, 0x8C, 255);

            // Dialogue box.
            var panel = Img(root.transform, "Dialogue Panel", border);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 28f);
            prt.sizeDelta = new Vector2(820f, 176f);
            var inner = Img(panel.transform, "Inner", panelBg);
            Stretch(inner.rectTransform, 4f, 4f, 4f, 4f);

            var frame = Img(inner.transform, "Portrait Frame", new Color32(0x59, 0x4A, 0x3C, 255));
            var frt = frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(14f, 0f);
            frt.sizeDelta = new Vector2(128f, 128f);
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
            portraitGo.transform.SetParent(frame.transform, false);
            var portrait = portraitGo.GetComponent<RawImage>();
            portrait.raycastTarget = false;
            Stretch(portrait.rectTransform, 4f, 4f, 4f, 4f);

            var nameText = Txt(inner.transform, "Name", 22, FontStyle.Bold, warm, TextAnchor.UpperLeft);
            var nrt = nameText.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(0f, 1f);
            nrt.anchoredPosition = new Vector2(162f, -12f);
            nrt.sizeDelta = new Vector2(-178f, 30f);

            var body = Txt(inner.transform, "Body", 20, FontStyle.Normal, cream, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, 162f, 34f, 16f, 46f);

            var hint = Txt(inner.transform, "Hint", 15, FontStyle.Normal, mid, TextAnchor.LowerRight);
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0f); hrt.pivot = new Vector2(1f, 0f);
            hrt.anchoredPosition = new Vector2(-16f, 8f);
            hrt.sizeDelta = new Vector2(240f, 22f);

            dlg = root.AddComponent<DialogueBox>();
            dlg.panel = panel.gameObject;
            dlg.nameText = nameText;
            dlg.bodyText = body;
            dlg.hintText = hint;
            dlg.portraitImage = portrait;

            // Prompt that floats over the NPC: child of the diorama picture so it shares its pixel grid.
            Transform output = rig.output.transform;
            var old = output.Find("Talk Prompt");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var pr = Img(output, "Talk Prompt", panelBg);
            prompt = pr.rectTransform;
            prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0.5f);
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.sizeDelta = new Vector2(78f, 22f);
            var pb = Img(pr.transform, "Border", border);
            Stretch(pb.rectTransform, 0f, 0f, 0f, 0f);
            pb.transform.SetAsFirstSibling();
            var pi = Img(pb.transform, "Fill", panelBg);
            Stretch(pi.rectTransform, 2f, 2f, 2f, 2f);
            pr.color = new Color(0f, 0f, 0f, 0f);
            var pt = Txt(pi.transform, "Label", 14, FontStyle.Bold, cream, TextAnchor.MiddleCenter);
            Stretch(pt.rectTransform, 0f, 0f, 0f, 0f);
            pt.text = "[E] Hablar";

            // Debug / help labels.
            var hudPanel = Img(root.transform, "HUD Panel", new Color32(0x1B, 0x17, 0x14, 160));
            var hpr = hudPanel.rectTransform;
            hpr.anchorMin = hpr.anchorMax = new Vector2(0f, 1f); hpr.pivot = new Vector2(0f, 1f);
            hpr.anchoredPosition = new Vector2(8f, -6f);
            hpr.sizeDelta = new Vector2(600f, 88f);

            chunkLabel = Txt(root.transform, "Chunk Label", 16, FontStyle.Normal, cream, TextAnchor.UpperLeft);
            var crt = chunkLabel.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f); crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(14f, -10f);
            crt.sizeDelta = new Vector2(420f, 24f);
            chunkLabel.text = "chunks cargados: -";

            var help = Txt(root.transform, "Help Label", 15, FontStyle.Normal, mid, TextAnchor.UpperLeft);
            var hlp = help.rectTransform;
            hlp.anchorMin = hlp.anchorMax = new Vector2(0f, 1f); hlp.pivot = new Vector2(0f, 1f);
            hlp.anchoredPosition = new Vector2(14f, -34f);
            hlp.sizeDelta = new Vector2(620f, 24f);
            help.text = "WASD / flechas: mover   Shift: correr   E: hablar   F5: clima   F6: +1 hora   F7: pausar hora";

            var clock = Txt(root.transform, "Clock Label", 18, FontStyle.Bold, warm, TextAnchor.UpperLeft);
            var klr = clock.rectTransform;
            klr.anchorMin = klr.anchorMax = new Vector2(0f, 1f); klr.pivot = new Vector2(0f, 1f);
            klr.anchoredPosition = new Vector2(14f, -58f);
            klr.sizeDelta = new Vector2(300f, 26f);
            _clockLabel = clock;
        }

        static Image Img(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(Transform parent, string name, int size, FontStyle style, Color c, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = c;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        static void Stretch(RectTransform rt, float l, float b, float r, float t)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }
    }
}
