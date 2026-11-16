using System.Collections.Generic;
using System.IO;
using BrumaPx.Data;
using BrumaPx.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Menu "BrumaPx/Overworld/Build Chapter 1 Prototype": saves the props and enemies of the Overworld scene as prefabs,
    /// copies the scene to Cap1Prototype and turns the copy into the map-driven world (MapWorld + map ground shader).
    /// The Overworld scene itself is not touched.
    /// </summary>
    public static class Cap1PrototypeBuilder
    {
        const string SrcScene = "Assets/Scenes/Overworld.unity";
        const string DstScene = "Assets/Scenes/Cap1Prototype.unity";
        const string PrefabDir = "Assets/Prefabs/Map";
        const string MapAsset = "Assets/Data/cap1_map.json";
        const string MatDir = "Assets/Art/Materials/Diorama/";

        [MenuItem("BrumaPx/Overworld/Build Chapter 1 Prototype")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(MapAsset, ImportAssetOptions.ForceUpdate);
            var map = AssetDatabase.LoadAssetAtPath<TextAsset>(MapAsset);
            if (map == null) { Debug.LogError("Cap1: falta " + MapAsset); return; }

            var scene = EditorSceneManager.OpenScene(SrcScene, OpenSceneMode.Single);
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);

            var entries = new List<MapWorld.Entry>();
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            Transform chunk00 = null;
            foreach (var t in all) if (t.name == "Chunk 0,0" && t.gameObject.scene == scene) chunk00 = t;
            if (chunk00 == null) { Debug.LogError("Cap1: no esta Chunk 0,0 en Overworld"); return; }

            Transform props = chunk00.Find("Props");
            Transform graves = chunk00.Find("Graves");

            Wrap(entries, "Tree", chunk00.Find("Dead Tree"));
            Wrap(entries, "RockBig", Child(props, "Rock Big"));
            Wrap(entries, "RockMid", Child(props, "Rock Mid"));
            Wrap(entries, "RockSmall", Child(props, "Rock Small"));
            Wrap(entries, "Cross", Child(graves, "Cross Post"), Child(graves, "Cross Arm"));
            Wrap(entries, "StoneA", Child(graves, "Stone A"));
            Wrap(entries, "StoneB", Child(graves, "Stone B"));
            Wrap(entries, "Fence", chunk00.Find("Fence"));
            Wrap(entries, "Hut", chunk00.Find("Hut"));
            Wrap(entries, "Lantern", chunk00.Find("Lantern Post"));
            Wrap(entries, "Charco", chunk00.Find("Charco contaminado"));
            Wrap(entries, "Refugio", chunk00.Find("Refugio Casa"));
            Wrap(entries, "RuinedHut", FindAnywhere(all, scene, "Ruined Hut"));
            Wrap(entries, "Well", FindAnywhere(all, scene, "Well"));
            Wrap(entries, "Cart", FindAnywhere(all, scene, "Cart"));
            Wrap(entries, "Cadaver", FindAnywhere(all, scene, "Cadaver"));
            Wrap(entries, "Foco", FindAnywhere(all, scene, "Foco de Bruma"));
            Wrap(entries, "Npc", FindAnywhere(all, scene, "Vigia del Camino"));

            Wrap(entries, "Infectado", FindAnywhere(all, scene, "Infectado A"));
            Wrap(entries, "Saqueador", FindAnywhere(all, scene, "Saqueador A"));
            Wrap(entries, "Hinchado", FindAnywhere(all, scene, "Hinchado A"));
            Wrap(entries, "Sabueso", FindAnywhere(all, scene, "Sabueso A"));
            Wrap(entries, "Ballestero", FindAnywhere(all, scene, "Ballestero A"));
            Wrap(entries, "Emboscador", FindAnywhere(all, scene, "Emboscador A"));
            Wrap(entries, "Tomado", FindAnywhere(all, scene, "Tomado A"));

            AssetDatabase.SaveAssets();

            ApplyHitboxes();

            // The copy of the scene becomes the prototype.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DstScene) != null) AssetDatabase.DeleteAsset(DstScene);
            AssetDatabase.CopyAsset(SrcScene, DstScene);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(DstScene, OpenSceneMode.Single);

            var chunks = GameObject.Find("Chunks");
            Text label = null;
            var streamer = chunks.GetComponent<ChunkStreamer>();
            if (streamer != null) { label = streamer.label; Object.DestroyImmediate(streamer); }
            for (int i = chunks.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(chunks.transform.GetChild(i).gameObject);

            var world = chunks.GetComponent<MapWorld>();
            if (world == null) world = chunks.AddComponent<MapWorld>();
            world.mapJson = map;
            world.chunkRoot = chunks.transform;
            world.label = label;
            world.activateRadius = 26f;
            world.prefabs = entries.ToArray();
            world.wood = Mat("Wood");
            world.woodDark = Mat("WoodDark");
            world.glow = Mat("Glow");
            world.stone = Mat("Stone");
            world.items = LoadAll<ItemDef>("Assets/Data/Items");
            world.armors = LoadAll<ArmorDef>("Assets/Data/Prendas");

            // Ground with the map shader.
            var shader = Shader.Find("BrumaPx/Diorama/PPMapGround");
            var matPath = MatDir + "PP_MapGround.mat";
            var gm = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (gm == null) { gm = new Material(shader); AssetDatabase.CreateAsset(gm, matPath); }
            else gm.shader = shader;
            var ground = GameObject.Find("Ground");
            if (ground != null) ground.GetComponent<MeshRenderer>().sharedMaterial = gm;

            var hero = GameObject.Find("Hero");
            if (hero != null) hero.transform.position = new Vector3(-191.5f, 0f, -18f);

            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Cap1: prototipo listo con " + entries.Count + " prefabs en " + DstScene);
        }

        // Collision sizes tuned by hand (smaller than the old values, which blocked more than what is drawn).
        static void ApplyHitboxes()
        {
            var circ = new Dictionary<string, float> { { "RockBig", 0.60f }, { "RockMid", 0.33f }, { "StoneA", 0.30f }, { "StoneB", 0.28f }, { "Tree", 0.32f }, { "Cross", 0.20f }, { "Lantern", 0.18f }, { "Fence", 0.15f }, { "Cart", 0.90f }, { "Well", 0.75f }, { "Npc", 0.35f } };
            var box = new Dictionary<string, Vector2> { { "Hut", new Vector2(1.75f, 1.45f) }, { "RuinedHut", new Vector2(1.55f, 1.25f) } };
            foreach (var kv in circ) EditPrefab(kv.Key, kv.Value, Vector2.zero);
            foreach (var kv in box) EditPrefab(kv.Key, 0f, kv.Value);
        }

        static void EditPrefab(string key, float radius, Vector2 half)
        {
            string path = PrefabDir + "/" + key + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var o in root.GetComponentsInChildren<WorldObstacle>(true))
            {
                if (o.kind == WorldObstacle.Kind.Circle && radius > 0f) o.radius = radius;
                else if (o.kind == WorldObstacle.Kind.Box && half.x > 0f) o.halfSize = half;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static bool NeedsOwnRoot(Transform t)
        {
            return t.GetComponent<BrumaPx.Combat.EnemyBrain>() != null || t.GetComponent<BrumaPx.Dialogue.TalkableNpc>() != null
                || t.GetComponent<Refuge>() != null || t.GetComponent<BrumaArea>() != null || t.GetComponent<BrumaSource>() != null;
        }

        static Transform Child(Transform parent, string name) { return parent == null ? null : parent.Find(name); }

        static Transform FindAnywhere(Transform[] all, UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var t in all)
                if (t.name == name && t.gameObject.scene == scene && t.GetComponentInParent<ChunkInfo>(true) != null) return t;
            return null;
        }

        static Material Mat(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "PP_" + n + ".mat"); }

        static T[] LoadAll<T>(string dir) where T : Object
        {
            var list = new List<T>();
            foreach (var g in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { dir }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (a != null) list.Add(a);
            }
            return list.ToArray();
        }

        // Saves the source objects as one prefab whose root is at the origin (the first source is the anchor).
        static void Wrap(List<MapWorld.Entry> entries, string key, params Transform[] srcs)
        {
            if (srcs == null || srcs.Length == 0 || srcs[0] == null) { Debug.LogWarning("Cap1: falta el original de " + key); return; }
            if (srcs.Length == 1 && NeedsOwnRoot(srcs[0]))
            {
                var direct = Object.Instantiate(srcs[0].gameObject);
                direct.name = key;
                direct.SetActive(true);
                direct.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var dp = PrefabUtility.SaveAsPrefabAsset(direct, PrefabDir + "/" + key + ".prefab");
                Object.DestroyImmediate(direct);
                entries.Add(new MapWorld.Entry { key = key, prefab = dp });
                return;
            }

            var root = new GameObject(key);
            Vector3 anchor = srcs[0].position;
            foreach (var s in srcs)
            {
                if (s == null) continue;
                var c = Object.Instantiate(s.gameObject, root.transform, true);
                c.name = s.name;
                c.SetActive(true);
                c.transform.position = new Vector3(s.position.x - anchor.x, s.position.y, s.position.z - anchor.z);
                c.transform.rotation = s.rotation;
            }
            string path = PrefabDir + "/" + key + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            entries.Add(new MapWorld.Entry { key = key, prefab = prefab });
        }
    }
}
