using UnityEngine;

namespace BrumaPx.Data
{
    /// <summary>What an item does when it is used.</summary>
    public enum ItemEffect { None, Heal, RestoreStamina, PalliateBruma, CureZone }

    /// <summary>
    /// One kind of item, kept as a file so new ones are made in the editor without touching code.
    /// Create from the menu "Barro y Bruma/Item".
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Item", fileName = "NewItem")]
    public sealed class ItemDef : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string _id = "item";
        [SerializeField] string _displayName = "Objeto";
        [SerializeField, TextArea(2, 4)] string _description = string.Empty;

        [Header("Stacking")]
        [SerializeField, Min(1)] int _maxStack = 5;

        [Header("Use")]
        [SerializeField] ItemEffect _effect = ItemEffect.None;
        [SerializeField, Min(0f)] float _amount = 0f;
        [Tooltip("Heal: also closes the most recent open wound.")]
        [SerializeField] bool _closesWound;
        [Tooltip("Palliative: seconds the Bruma stays slowed. Amount is the resistance it gives back.")]
        [SerializeField, Min(0f)] float _duration = 0f;
        [Tooltip("Palliative: used on a zone from the body window it also cleans an open wound.")]
        [SerializeField] bool _disinfects;

        [Header("Look (placeholder until there is art)")]
        [SerializeField] Color _tint = Color.white;
        [Tooltip("Shape of the object when it lies on the bag cloth.")]
        [SerializeField] PrimitiveType _worldShape = PrimitiveType.Cube;
        [SerializeField] Vector3 _worldSize = new Vector3(0.22f, 0.12f, 0.22f);
        [Tooltip("Existing diorama material used for the object on the cloth.")]
        [SerializeField] Material _worldMaterial;

        public string Id { get { return string.IsNullOrEmpty(_id) ? name : _id; } }
        public string DisplayName { get { return _displayName; } }
        public string Description { get { return _description; } }
        public int MaxStack { get { return Mathf.Max(1, _maxStack); } }
        public ItemEffect Effect { get { return _effect; } }
        public float Amount { get { return _amount; } }
        public bool ClosesWound { get { return _closesWound; } }
        public float Duration { get { return _duration; } }
        public bool Disinfects { get { return _disinfects; } }
        public bool IsUsable { get { return _effect != ItemEffect.None; } }
        public Color Tint { get { return _tint; } }
        public PrimitiveType WorldShape { get { return _worldShape; } }
        public Vector3 WorldSize { get { return _worldSize; } }
        public Material WorldMaterial { get { return _worldMaterial; } }

        /// <summary>First letter, used as the provisional icon on the belt.</summary>
        public string Glyph { get { return string.IsNullOrEmpty(_displayName) ? "?" : _displayName.Substring(0, 1).ToUpperInvariant(); } }

        /// <summary>One line for the bag label, e.g. "Cura 25 de vida".</summary>
        public string EffectLine
        {
            get
            {
                switch (_effect)
                {
                    case ItemEffect.Heal: return "Cura " + Mathf.RoundToInt(_amount) + " de vida" + (_closesWound ? " y cierra una herida" : string.Empty);
                    case ItemEffect.RestoreStamina: return "Devuelve " + Mathf.RoundToInt(_amount) + " de aguante";
                    case ItemEffect.PalliateBruma: return "Frena la Bruma " + Mathf.RoundToInt(_duration) + " s y da " + Mathf.RoundToInt(_amount) + " de resistencia" + (_disinfects ? "; limpia heridas" : string.Empty);
                    case ItemEffect.CureZone: return "Baja " + Mathf.RoundToInt(_amount) + " de Bruma en una zona";
                    default: return "Sin uso directo";
                }
            }
        }
    }
}
