using BrumaPx.Data;
using BrumaPx.Player;
using BrumaPx.UI;
using BrumaPx.World;
using BrumaPx.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Second half of the overworld builder: the hero's body against the Bruma (data files, components, the
    /// window), the example items that go with it and the example sources in the world. Safe to run again.
    /// </summary>
    public static partial class OverworldBuilder
    {
        sealed class Fx
        {
            public BodyStat stat;
            public float startsAt, atFull;
        }

        sealed class ZoneSpec
        {
            public string id, name;
            public float cx, cy, w, h, air, hit;
            public bool guard;
            public Fx[] fx;
        }

        static Fx F(BodyStat stat, float startsAt, float atFull)
        {
            return new Fx { stat = stat, startsAt = startsAt, atFull = atFull };
        }

        static ZoneSpec Z(string id, string name, float cx, float cy, float w, float h, float air, float hit, bool guard, params Fx[] fx)
        {
            return new ZoneSpec { id = id, name = name, cx = cx, cy = cy, w = w, h = h, air = air, hit = hit, guard = guard, fx = fx };
        }

        // ------------------------------------------------------------------------------------------ the body

        // The 14 zones. Right side of the hero is on the left of the window, like looking at a person face to face.
        // Numbers are a first pass to be tuned in the BodyProfile file: startsAt is the level where an effect begins,
        // atFull is the change at 100 (-0.25 = 25% less).
        static void FillBodyZones(SerializedObject so)
        {
            var specs = new[]
            {
                Z("cabeza", "Cabeza", 0.50f, 0.90f, 0.12f, 0.12f, 1.0f, 0.06f, false,
                    F(BodyStat.VisionRange, 0f, -0.30f), F(BodyStat.StaminaRegen, 50f, -0.15f)),
                Z("torso", "Torso", 0.50f, 0.68f, 0.24f, 0.26f, 1.0f, 0.30f, false,
                    F(BodyStat.MaxHealth, 0f, -0.30f), F(BodyStat.StaminaRegen, 40f, -0.25f)),
                Z("hombro.d", "Hombro der.", 0.325f, 0.78f, 0.11f, 0.09f, 0.5f, 0.06f, false,
                    F(BodyStat.HeavyDamage, 0f, -0.30f), F(BodyStat.MeleeDamage, 50f, -0.10f)),
                Z("hombro.i", "Hombro izq.", 0.675f, 0.78f, 0.11f, 0.09f, 0.5f, 0.06f, true,
                    F(BodyStat.ParryWindow, 0f, -0.40f), F(BodyStat.GuardLeak, 50f, 0.15f)),
                Z("brazo.d", "Brazo der.", 0.30f, 0.65f, 0.09f, 0.15f, 0.5f, 0.08f, false,
                    F(BodyStat.MeleeDamage, 0f, -0.25f), F(BodyStat.HeavyDamage, 60f, -0.10f)),
                Z("brazo.i", "Brazo izq.", 0.70f, 0.65f, 0.09f, 0.15f, 0.5f, 0.08f, true,
                    F(BodyStat.GuardLeak, 0f, 0.35f), F(BodyStat.ItemSpeed, 60f, 0.10f)),
                Z("antebrazo.d", "Antebrazo der.", 0.28f, 0.49f, 0.085f, 0.17f, 0.9f, 0.07f, false,
                    F(BodyStat.AttackRecovery, 0f, 0.30f)),
                Z("antebrazo.i", "Antebrazo izq.", 0.72f, 0.49f, 0.085f, 0.17f, 0.9f, 0.07f, true,
                    F(BodyStat.ItemSpeed, 0f, 0.50f), F(BodyStat.ParryWindow, 60f, -0.10f)),
                Z("muslo.d", "Muslo der.", 0.435f, 0.46f, 0.12f, 0.13f, 0.3f, 0.07f, false,
                    F(BodyStat.WalkSpeed, 0f, -0.20f), F(BodyStat.RunSpeed, 40f, -0.10f)),
                Z("muslo.i", "Muslo izq.", 0.565f, 0.46f, 0.12f, 0.13f, 0.3f, 0.07f, false,
                    F(BodyStat.WalkSpeed, 0f, -0.20f), F(BodyStat.RunSpeed, 40f, -0.10f)),
                Z("rodilla.d", "Rodilla der.", 0.435f, 0.345f, 0.11f, 0.07f, 0.3f, 0.03f, false,
                    F(BodyStat.DodgeDistance, 0f, -0.20f), F(BodyStat.DodgeCost, 40f, 0.15f)),
                Z("rodilla.i", "Rodilla izq.", 0.565f, 0.345f, 0.11f, 0.07f, 0.3f, 0.03f, false,
                    F(BodyStat.DodgeDistance, 0f, -0.20f), F(BodyStat.DodgeCost, 40f, 0.15f)),
                Z("pantorrilla.d", "Pantorrilla der.", 0.435f, 0.22f, 0.10f, 0.19f, 0.4f, 0.06f, false,
                    F(BodyStat.RunSpeed, 0f, -0.15f), F(BodyStat.WalkSpeed, 50f, -0.05f)),
                Z("pantorrilla.i", "Pantorrilla izq.", 0.565f, 0.22f, 0.10f, 0.19f, 0.4f, 0.06f, false,
                    F(BodyStat.RunSpeed, 0f, -0.15f), F(BodyStat.WalkSpeed, 50f, -0.05f))
            };

            var arr = so.FindProperty("_zones");
            arr.arraySize = specs.Length;
            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").stringValue = s.id;
                el.FindPropertyRelative("displayName").stringValue = s.name;
                el.FindPropertyRelative("center").vector2Value = new Vector2(s.cx, s.cy);
                el.FindPropertyRelative("extent").vector2Value = new Vector2(s.w, s.h);
                el.FindPropertyRelative("airWeight").floatValue = s.air;
                el.FindPropertyRelative("hitWeight").floatValue = s.hit;
                el.FindPropertyRelative("cover").floatValue = 0f;
                el.FindPropertyRelative("guardSide").boolValue = s.guard;
                var fx = el.FindPropertyRelative("effects");
                fx.arraySize = s.fx.Length;
                for (int k = 0; k < s.fx.Length; k++)
                {
                    var f = fx.GetArrayElementAtIndex(k);
                    f.FindPropertyRelative("stat").enumValueIndex = (int)s.fx[k].stat;
                    f.FindPropertyRelative("startsAt").floatValue = s.fx[k].startsAt;
                    f.FindPropertyRelative("atFull").floatValue = s.fx[k].atFull;
                }
            }
        }

        // The body, its two bars and its window. Data files are only created when missing (hand edits survive).
        static void BuildBody(Transform hero, Health health, HeroCombat combat)
        {
            var profile = GetAsset<BodyProfileDef>("Heroe", "BodyProfile", FillBodyZones);
            var tuning = GetAsset<BrumaTuning>("Heroe", "BrumaTuning", null);

            var body = GetOrAdd<HeroBody>(hero.gameObject);
            body.health = health;
            body.combat = combat;
            body.profile = profile;
            body.tuning = tuning;
            EditorUtility.SetDirty(body);

            var ui = GameObject.Find("UI");
            Transform canvas = ui != null ? ui.transform : null;

            var bars = GetOrAdd<BrumaBars>(hero.gameObject);
            bars.body = body;
            bars.canvasRoot = canvas;

            var panel = GetOrAdd<BodyPanel>(hero.gameObject);
            panel.body = body;
            panel.combat = combat;
            panel.health = health;
            panel.canvasRoot = canvas;
        }

        // The palliative, and the bandage that now closes wounds. Returns the new item.
        static ItemDef MakeBrumaItems(ItemDef venda)
        {
            SetItemExtra(venda, true, 0f);
            var unguento = MakeItem("Unguento", "unguento", "Ungüento de brea",
                "Frena la Bruma un rato y devuelve algo de resistencia. No cura.", 3,
                ItemEffect.PalliateBruma, 30f, new Color32(0x5A, 0x48, 0x38, 255), PrimitiveType.Cylinder,
                new Vector3(0.16f, 0.1f, 0.16f), "Bark");
            SetItemExtra(unguento, false, 150f);
            return unguento;
        }

        static void SetItemExtra(ItemDef item, bool closesWound, float duration)
        {
            var so = new SerializedObject(item);
            if (closesWound) so.FindProperty("_closesWound").boolValue = true;
            var d = so.FindProperty("_duration");
            if (duration > 0f && d.floatValue <= 0f) d.floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

        // The infected carry the Bruma in their blows; the raider does not.
        static void SetBrumaOnHit(EnemyProfile profile, float dose)
        {
            var so = new SerializedObject(profile);
            var p = so.FindProperty("_brumaOnHit");
            if (p.floatValue > 0f) return;
            p.floatValue = dose;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------------------------------ world examples

        static void SetStrings(SerializedProperty p, params string[] values)
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        // A patch of thick Bruma on the east side, one patch of tainted water and a body to search, all close to
        // the start so the ways of catching it can be tried at once. They are kept clear of the random scatter.
        static void BuildBrumaWorld()
        {
            foreach (var a in Object.FindObjectsByType<BrumaArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(a.gameObject);
            foreach (var s in Object.FindObjectsByType<BrumaSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);

            BuildBrumaPatch(new Vector3(10.5f, 0f, -2.5f), 4f);
            Zones.Add(new Vector3(10.5f, -2.5f, 4.6f));

            BuildPuddle(new Vector3(3.0f, 0f, -7.2f), 1.5f);
            Zones.Add(new Vector3(3.0f, -7.2f, 2.3f));

            BuildCorpse(new Vector3(5.4f, 0f, -9.8f), 25f);
            Zones.Add(new Vector3(5.4f, -9.8f, 1.9f));
        }

        static void BuildBrumaPatch(Vector3 pos, float radius)
        {
            var g = Group("Foco de Bruma", pos, 0f);
            Prim(g, PrimitiveType.Cylinder, "Stain", new Vector3(0f, 0.02f, 0f), new Vector3(radius * 2f, 0.01f, radius * 2f), Vector3.zero, "Black", false);
            Prim(g, PrimitiveType.Cylinder, "Core", new Vector3(0f, 0.03f, 0f), new Vector3(radius * 1.2f, 0.01f, radius * 1.2f), Vector3.zero, "HeroCloakDark", false);
            var rng = new System.Random(4242);
            for (int i = 0; i < 7; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float d = (float)rng.NextDouble() * radius * 0.7f;
                float s = 0.5f + (float)rng.NextDouble() * 0.9f;
                float h = 0.1f + (float)rng.NextDouble() * 0.15f;
                Prim(g, PrimitiveType.Sphere, "Wisp " + i, new Vector3(Mathf.Cos(a) * d, h, Mathf.Sin(a) * d),
                    new Vector3(s, 0.18f, s * 0.8f), Vector3.zero, "HeroCloakDark", false);
            }
            var area = g.gameObject.AddComponent<BrumaArea>();
            var so = new SerializedObject(area);
            so.FindProperty("displayName").stringValue = "Foco de Bruma";
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("density").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildPuddle(Vector3 pos, float radius)
        {
            var g = Group("Charco contaminado", pos, 0f);
            Prim(g, PrimitiveType.Cylinder, "Mud", new Vector3(0f, 0.015f, 0f), new Vector3(radius * 2f, 0.01f, radius * 2f), Vector3.zero, "WoodDark", false);
            Prim(g, PrimitiveType.Cylinder, "Water", new Vector3(0f, 0.03f, 0f), new Vector3(radius * 1.6f, 0.01f, radius * 1.6f), Vector3.zero, "Black", false);

            var src = g.gameObject.AddComponent<BrumaSource>();
            var so = new SerializedObject(src);
            so.FindProperty("displayName").stringValue = "Charco contaminado";
            so.FindProperty("kind").enumValueIndex = (int)BrumaSource.Kind.Water;
            so.FindProperty("radius").floatValue = radius;
            SetStrings(so.FindProperty("zoneIds"), "rodilla.d", "rodilla.i", "pantorrilla.d", "pantorrilla.i");
            so.FindProperty("dose").floatValue = 0.5f;
            so.FindProperty("resistCost").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildCorpse(Vector3 pos, float yaw)
        {
            var g = Group("Cadaver", pos, yaw);
            Prim(g, PrimitiveType.Cube, "Torso", new Vector3(0f, 0.14f, 0f), new Vector3(0.5f, 0.2f, 0.85f), Vector3.zero, "HeroCloakDark");
            Prim(g, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.2f, 0.62f), new Vector3(0.3f, 0.28f, 0.3f), Vector3.zero, "HeroScarf");
            Prim(g, PrimitiveType.Cube, "Leg L", new Vector3(-0.13f, 0.1f, -0.7f), new Vector3(0.17f, 0.16f, 0.6f), new Vector3(0f, 6f, 0f), "HeroPants");
            Prim(g, PrimitiveType.Cube, "Leg R", new Vector3(0.14f, 0.1f, -0.72f), new Vector3(0.17f, 0.16f, 0.6f), new Vector3(0f, -9f, 0f), "HeroPants");
            Prim(g, PrimitiveType.Cube, "Arm L", new Vector3(-0.36f, 0.1f, 0.12f), new Vector3(0.13f, 0.13f, 0.6f), new Vector3(0f, -14f, 0f), "HeroCloakDark");
            Prim(g, PrimitiveType.Cube, "Arm R", new Vector3(0.36f, 0.1f, 0.05f), new Vector3(0.13f, 0.13f, 0.6f), new Vector3(0f, 12f, 0f), "HeroCloakDark");

            var src = g.gameObject.AddComponent<BrumaSource>();
            var so = new SerializedObject(src);
            so.FindProperty("displayName").stringValue = "Cadáver";
            so.FindProperty("kind").enumValueIndex = (int)BrumaSource.Kind.Corpse;
            so.FindProperty("radius").floatValue = 1.4f;
            SetStrings(so.FindProperty("zoneIds"), "antebrazo.d", "antebrazo.i");
            so.FindProperty("dose").floatValue = 12f;
            so.FindProperty("resistCost").floatValue = 10f;
            var loot = AssetDatabase.LoadAssetAtPath<ItemDef>("Assets/Data/Items/Venda.asset");
            if (loot != null) so.FindProperty("loot").objectReferenceValue = loot;
            so.FindProperty("lootCount").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
