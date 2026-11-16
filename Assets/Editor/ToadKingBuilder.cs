using System.Collections.Generic;
using BrumaPx.Combat;
using BrumaPx.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Menu "BrumaPx/Overworld/Build Rey Sapo (prototype)": makes the toad king boss (profile, placeholder model,
    /// prefab) and registers it in the MapWorld of the open scene. The boss uses the normal enemy brain: a big, slow
    /// body with a heavy bite, a Bruma aura that walks with it and a huge cloud when it dies.
    /// </summary>
    public static class ToadKingBuilder
    {
        const string MatDir = "Assets/Art/Materials/Diorama/";
        const string PrefabPath = "Assets/Prefabs/Map/ReySapo.prefab";
        const string ProfilePath = "Assets/Data/Enemies/ReySapo.asset";

        [MenuItem("BrumaPx/Overworld/Build Rey Sapo (prototype)")]
        public static void Build()
        {
            var profile = MakeProfile();
            Material skin = Tinted("ToadSkin", new Color(0.24f, 0.33f, 0.20f));
            Material belly = Tinted("ToadBelly", new Color(0.56f, 0.55f, 0.36f));
            Material eye = Tinted("ToadEye", new Color(0.85f, 0.72f, 0.18f));
            Material gold = Mat("Glow");

            var root = new GameObject("ReySapo");
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            Prim(body, PrimitiveType.Sphere, "Torso", new Vector3(0f, 1.0f, 0f), new Vector3(2.3f, 1.6f, 2.0f), Vector3.zero, skin);
            Prim(body, PrimitiveType.Sphere, "Belly", new Vector3(0f, 0.7f, 0.45f), new Vector3(1.8f, 1.0f, 1.5f), Vector3.zero, belly);
            Prim(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.55f, 0.95f), new Vector3(1.7f, 0.95f, 1.25f), Vector3.zero, skin);
            Prim(body, PrimitiveType.Cube, "Mouth", new Vector3(0f, 1.36f, 1.55f), new Vector3(1.3f, 0.08f, 0.08f), Vector3.zero, Mat("Black"));
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(body, PrimitiveType.Sphere, "Eye " + s, new Vector3(0.55f * s, 2.05f, 1.05f), new Vector3(0.46f, 0.46f, 0.46f), Vector3.zero, eye);
                Prim(body, PrimitiveType.Cube, "Pupil " + s, new Vector3(0.55f * s, 2.07f, 1.27f), new Vector3(0.08f, 0.3f, 0.06f), Vector3.zero, Mat("Black"));
            }
            // Crown: a ring of small spikes.
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Prim(body, PrimitiveType.Cube, "Crown " + i, new Vector3(Mathf.Cos(a) * 0.36f, 2.3f, 0.95f + Mathf.Sin(a) * 0.36f), new Vector3(0.1f, 0.3f, 0.1f), Vector3.zero, gold);
            }
            for (int i = 0; i < 6; i++)
                Prim(body, PrimitiveType.Sphere, "Wart " + i, new Vector3(Mathf.Cos(i * 1.9f) * 0.85f, 1.45f + (i % 3) * 0.12f, -0.3f + Mathf.Sin(i * 1.9f) * 0.6f), new Vector3(0.22f, 0.18f, 0.22f), Vector3.zero, belly);

            var arms = new GameObject("Arms").transform;
            arms.SetParent(body, false);
            arms.localPosition = new Vector3(0f, 1.2f, 0.75f);
            Prim(arms, PrimitiveType.Cube, "Arm L", new Vector3(-1.0f, -0.55f, 0.45f), new Vector3(0.42f, 1.1f, 0.42f), new Vector3(15f, 0f, 8f), skin);
            Prim(arms, PrimitiveType.Cube, "Arm R", new Vector3(1.0f, -0.55f, 0.45f), new Vector3(0.42f, 1.1f, 0.42f), new Vector3(15f, 0f, -8f), skin);
            Prim(arms, PrimitiveType.Sphere, "Hand L", new Vector3(-1.05f, -1.1f, 0.75f), new Vector3(0.6f, 0.3f, 0.7f), Vector3.zero, belly);
            Prim(arms, PrimitiveType.Sphere, "Hand R", new Vector3(1.05f, -1.1f, 0.75f), new Vector3(0.6f, 0.3f, 0.7f), Vector3.zero, belly);

            var legL = Prim(root.transform, PrimitiveType.Cube, "Leg L", new Vector3(-0.95f, 0.45f, -0.6f), new Vector3(0.75f, 0.9f, 1.1f), Vector3.zero, skin);
            var legR = Prim(root.transform, PrimitiveType.Cube, "Leg R", new Vector3(0.95f, 0.45f, -0.6f), new Vector3(0.75f, 0.9f, 1.1f), Vector3.zero, skin);

            var health = root.AddComponent<Health>();
            health.team = 1;
            health.max = profile.MaxHealth;
            health.flashMaterial = Mat("Glow");
            var brain = root.AddComponent<EnemyBrain>();
            brain.profile = profile;
            brain.body = body;
            brain.legL = legL.transform;
            brain.legR = legR.transform;
            brain.arms = arms;
            brain.pixelsPerUnit = 20;
            var traits = root.AddComponent<EnemyTraits>();
            traits.cloudMaterial = Mat("HeroCloakDark");
            var boss = root.AddComponent<BossMarker>();
            boss.title = "Rey Sapo";

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            var world = Object.FindFirstObjectByType<MapWorld>();
            if (world == null) { Debug.LogWarning("Rey Sapo: abri la escena Cap1Prototype para registrar el prefab."); return; }
            var list = new List<MapWorld.Entry>(world.prefabs);
            list.RemoveAll(e => e != null && e.key == "ReySapo");
            list.Add(new MapWorld.Entry { key = "ReySapo", prefab = prefab });
            world.prefabs = list.ToArray();
            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Rey Sapo: prefab listo y registrado en MapWorld.");
        }

        static EnemyProfile MakeProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<EnemyProfile>(ProfilePath);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<EnemyProfile>();
                AssetDatabase.CreateAsset(p, ProfilePath);
            }
            var so = new SerializedObject(p);
            so.FindProperty("_displayName").stringValue = "Rey Sapo";
            F(so, "_maxHealth", 320f);
            F(so, "_radius", 1.0f);
            F(so, "_walkSpeed", 1.0f);
            F(so, "_chaseSpeed", 2.7f);
            F(so, "_sightRange", 11f);
            F(so, "_minLightFactor", 0.6f);
            F(so, "_fieldOfView", 200f);
            F(so, "_hearingMultiplier", 1.0f);
            F(so, "_loseTrackSeconds", 10f);
            F(so, "_attackRange", 2.8f);
            F(so, "_attackDamage", 30f);
            F(so, "_attackStartup", 1.1f);
            F(so, "_attackActive", 0.25f);
            F(so, "_attackRecovery", 1.9f);
            F(so, "_attackArcDegrees", 150f);
            F(so, "_attackNoise", 9f);
            F(so, "_brumaOnHit", 14f);
            F(so, "_staggerThreshold", 45f);
            F(so, "_staggerSeconds", 0.5f);
            F(so, "_knockbackScale", 0.2f);
            F(so, "_auraRadius", 4.0f);
            F(so, "_auraDensity", 0.7f);
            F(so, "_deathCloudRadius", 5.5f);
            F(so, "_deathCloudSeconds", 40f);
            F(so, "_deathCloudDensity", 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static void F(SerializedObject so, string prop, float v) { so.FindProperty(prop).floatValue = v; }

        static Material Mat(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "PP_" + n + ".mat"); }

        // A copy of the rock material (same toon shader) with another base colour.
        static Material Tinted(string name, Color c)
        {
            string path = MatDir + "PP_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Mat("Rock"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 lpos, Vector3 scale, Vector3 euler, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = lpos;
            t.localEulerAngles = euler;
            t.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat, Mat("Shadow") };
            return go;
        }
    }
}
