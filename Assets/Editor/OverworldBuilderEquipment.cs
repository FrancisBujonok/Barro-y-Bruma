using BrumaPx.Combat;
using BrumaPx.Data;
using BrumaPx.Player;
using BrumaPx.UI;
using UnityEditor;
using UnityEngine;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Third part of the overworld builder: the wardrobe (leather and mail, four pieces each), the cure item and the
    /// equipment component and window. Data files are only created when missing, so hand edits survive a rebuild.
    /// </summary>
    public static partial class OverworldBuilder
    {
        // Leather is closed and keeps the Bruma out but takes few blows; mail takes blows but the Bruma gets between
        // the links, and it is heavy. Mods use Fx.atFull as the change (-0.05 is 5% less).
        static ArmorDef MakeArmor(string file, string id, string displayName, string description, ArmorSlot slot,
            float brumaCover, float blow, float equipSeconds, Color tint, params Fx[] mods)
        {
            return GetAsset<ArmorDef>("Prendas", file, delegate (SerializedObject so)
            {
                so.FindProperty("_id").stringValue = id;
                so.FindProperty("_displayName").stringValue = displayName;
                so.FindProperty("_description").stringValue = description;
                so.FindProperty("_slot").enumValueIndex = (int)slot;
                so.FindProperty("_brumaCover").floatValue = brumaCover;
                so.FindProperty("_blowReduction").floatValue = blow;
                so.FindProperty("_equipSeconds").floatValue = equipSeconds;
                so.FindProperty("_tint").colorValue = tint;
                var arr = so.FindProperty("_mods");
                arr.arraySize = mods.Length;
                for (int i = 0; i < mods.Length; i++)
                {
                    var el = arr.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("stat").enumValueIndex = (int)mods[i].stat;
                    el.FindPropertyRelative("change").floatValue = mods[i].atFull;
                }
            });
        }

        static ArmorDef[] MakeArmors()
        {
            Color leather = new Color32(0x6B, 0x4A, 0x2E, 255);
            Color mail = new Color32(0x8A, 0x8F, 0x94, 255);
            return new[]
            {
                MakeArmor("CapuchaCuero", "capucha_cuero", "Capucha de cuero",
                    "Cuero curtido y engrasado, cerrado hasta el cuello. Casi no deja pasar la Bruma; aguanta poco los golpes.",
                    ArmorSlot.Cabeza, 0.30f, 0.03f, 1.5f, leather),
                MakeArmor("JubonCuero", "jubon_cuero", "Jubón de cuero",
                    "Cuero doble sobre el pecho y los hombros. Liviano y cerrado.",
                    ArmorSlot.Torso, 0.35f, 0.08f, 2.5f, leather, F(BodyStat.StaminaRegen, 0f, -0.02f)),
                MakeArmor("MangasCuero", "mangas_cuero", "Mangas de cuero",
                    "Mangas atadas hasta la muñeca. No estorban el brazo.",
                    ArmorSlot.Brazos, 0.30f, 0.03f, 2.0f, leather),
                MakeArmor("PolainasCuero", "polainas_cuero", "Polainas de cuero",
                    "Cubren del muslo al tobillo. Las aguas turbias se resbalan.",
                    ArmorSlot.Piernas, 0.30f, 0.04f, 2.0f, leather),

                MakeArmor("CofiaMalla", "cofia_malla", "Cofia de malla",
                    "Eslabones sobre la cabeza. Frena filos, pero estrecha la vista.",
                    ArmorSlot.Cabeza, 0.10f, 0.07f, 3.0f, mail, F(BodyStat.VisionRange, 0f, -0.04f)),
                MakeArmor("CotaMalla", "cota_malla", "Cota de malla",
                    "Aguanta bien los golpes. La Bruma pasa entre los eslabones y pesa lo suyo.",
                    ArmorSlot.Torso, 0.12f, 0.18f, 5.0f, mail,
                    F(BodyStat.WalkSpeed, 0f, -0.05f), F(BodyStat.RunSpeed, 0f, -0.07f),
                    F(BodyStat.StaminaRegen, 0f, -0.10f), F(BodyStat.DodgeCost, 0f, 0.10f)),
                MakeArmor("MangasMalla", "mangas_malla", "Mangas de malla",
                    "Protegen el brazo de los tajos. Tardan más en recuperar el golpe.",
                    ArmorSlot.Brazos, 0.10f, 0.07f, 3.5f, mail,
                    F(BodyStat.AttackRecovery, 0f, 0.05f), F(BodyStat.ItemSpeed, 0f, 0.05f)),
                MakeArmor("GrebasMalla", "grebas_malla", "Grebas de malla",
                    "Malla en las piernas. Cuesta más correr y rodar.",
                    ArmorSlot.Piernas, 0.10f, 0.07f, 4.0f, mail,
                    F(BodyStat.WalkSpeed, 0f, -0.04f), F(BodyStat.RunSpeed, 0f, -0.05f), F(BodyStat.DodgeDistance, 0f, -0.06f))
            };
        }

        // The cure: takes a good part of the Bruma out of one zone. It buys time, it does not leave the blood clean.
        static ItemDef MakeTintura()
        {
            return MakeItem("Tintura", "tintura", "Tintura amarga",
                "Baja bastante la Bruma de una zona. Escasa; gana tiempo, no deja la sangre limpia.", 2,
                ItemEffect.CureZone, 40f, new Color32(0x4E, 0x6B, 0x3A, 255), PrimitiveType.Capsule,
                new Vector3(0.1f, 0.14f, 0.1f), "Bark");
        }

        // The salve also cleans wounds (that is what makes it useful before the bandage).
        static void SetDisinfects(ItemDef item)
        {
            var so = new SerializedObject(item);
            so.FindProperty("_disinfects").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

        static void BuildEquipment(Transform hero, Health health, HeroBody body, HeroCombat combat)
        {
            var pieces = MakeArmors();

            var eq = GetOrAdd<Equipment>(hero.gameObject);
            eq.health = health;
            eq.body = body;
            var so = new SerializedObject(eq);
            var list = so.FindProperty("startingWardrobe");
            list.arraySize = pieces.Length;
            for (int i = 0; i < pieces.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(eq);

            var ui = GameObject.Find("UI");
            Transform canvas = ui != null ? ui.transform : null;

            var panel = GetOrAdd<EquipPanel>(hero.gameObject);
            panel.equipment = eq;
            panel.body = body;
            panel.combat = combat;
            panel.health = health;
            panel.canvasRoot = canvas;
            EditorUtility.SetDirty(panel);
        }
    }
}
