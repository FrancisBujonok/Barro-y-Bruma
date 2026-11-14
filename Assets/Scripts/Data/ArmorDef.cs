using System;
using UnityEngine;

namespace BrumaPx.Data
{
    /// <summary>The four places a piece of clothing or armour goes. Each one covers several body zones.</summary>
    public enum ArmorSlot { Cabeza, Torso, Brazos, Piernas }

    /// <summary>Names and zones of the armour slots. The zone ids are the ones of the BodyProfileDef.</summary>
    public static class ArmorSlotInfo
    {
        public const int Count = 4;

        static readonly string[] Names = { "Cabeza", "Torso", "Brazos", "Piernas" };
        static readonly string[] CoverNames = { "la cabeza", "el torso y los hombros", "brazos y antebrazos", "muslos, rodillas y pantorrillas" };
        static readonly string[][] Zones =
        {
            new[] { "cabeza" },
            new[] { "torso", "hombro.d", "hombro.i" },
            new[] { "brazo.d", "brazo.i", "antebrazo.d", "antebrazo.i" },
            new[] { "muslo.d", "muslo.i", "rodilla.d", "rodilla.i", "pantorrilla.d", "pantorrilla.i" }
        };

        public static string Label(ArmorSlot slot) { return Names[(int)slot]; }
        public static string CoverText(ArmorSlot slot) { return CoverNames[(int)slot]; }

        public static bool Covers(ArmorSlot slot, string zoneId)
        {
            var ids = Zones[(int)slot];
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == zoneId) return true;
            return false;
        }
    }

    /// <summary>A change a piece makes to a stat while worn: -0.05 is 5% less, +0.1 is 10% more.</summary>
    [Serializable]
    public sealed class ArmorMod
    {
        public BodyStat stat;
        public float change;
    }

    /// <summary>
    /// One piece of clothing or armour, kept as a file so new ones are made in the editor. It lives in the
    /// wardrobe (the Equipment component), not in the bag. Worn, it covers the zones of its slot: it cuts the Bruma
    /// that reaches them, absorbs part of every blow, and may slow the hero down.
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Armadura", fileName = "NewArmor")]
    public sealed class ArmorDef : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string _id = "armor";
        [SerializeField] string _displayName = "Prenda";
        [SerializeField, TextArea(2, 4)] string _description = string.Empty;
        [SerializeField] ArmorSlot _slot = ArmorSlot.Torso;

        [Header("Protection")]
        [Tooltip("Share of the Bruma stopped in every zone of the slot (0 to 1). Closed leather stops a lot; mail lets it through.")]
        [SerializeField, Range(0f, 0.9f)] float _brumaCover = 0.2f;
        [Tooltip("Share of every blow this piece absorbs. The pieces worn add up (capped at 75%).")]
        [SerializeField, Range(0f, 0.5f)] float _blowReduction = 0.05f;

        [Header("Cost")]
        [SerializeField] ArmorMod[] _mods = new ArmorMod[0];
        [Tooltip("Seconds to put it on. Taking it off takes half. A blow in the meantime stops it.")]
        [SerializeField, Min(0f)] float _equipSeconds = 2f;

        [Header("Look (placeholder until there is art)")]
        [SerializeField] Color _tint = Color.white;

        public string Id { get { return string.IsNullOrEmpty(_id) ? name : _id; } }
        public string DisplayName { get { return _displayName; } }
        public string Description { get { return _description; } }
        public ArmorSlot Slot { get { return _slot; } }
        public float BrumaCover { get { return _brumaCover; } }
        public float BlowReduction { get { return _blowReduction; } }
        public ArmorMod[] Mods { get { return _mods; } }
        public float EquipSeconds { get { return _equipSeconds; } }
        public float UnequipSeconds { get { return _equipSeconds * 0.5f; } }
        public Color Tint { get { return _tint; } }
    }
}
