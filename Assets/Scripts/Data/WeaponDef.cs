using UnityEngine;

namespace BrumaPx.Data
{
    /// <summary>What kind of thing goes in a hand. A shield goes in the off hand, next to a light weapon.</summary>
    public enum WeaponClass { Ligera, Pesada, Escudo }

    /// <summary>
    /// One weapon or shield, kept as a file so new ones are made in the editor. The numbers are multipliers over
    /// the HeroCombatProfile (1 = unchanged); nothing reads them yet, they are here so the balance has a home.
    /// The look comes from the character layer set: every layer whose id is this Id, or starts with this Id plus
    /// "_", is drawn while the piece is held.
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Arma", fileName = "NewWeapon")]
    public sealed class WeaponDef : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string _id = "arma";
        [SerializeField] string _displayName = "Arma";
        [SerializeField, TextArea(2, 4)] string _description = string.Empty;
        [SerializeField] WeaponClass _class = WeaponClass.Ligera;

        [Header("Combat (multipliers, not wired yet)")]
        [SerializeField, Min(0f)] float _damage = 1f;
        [SerializeField, Min(0f)] float _staminaCost = 1f;
        [Tooltip("Above 1 the swing is slower.")]
        [SerializeField, Min(0.1f)] float _swingTime = 1f;
        [SerializeField, Min(0f)] float _range = 1f;
        [Tooltip("Shield: share of the damage that gets through the guard, multiplied over the profile.")]
        [SerializeField, Range(0f, 2f)] float _guardLeak = 1f;

        public string Id { get { return string.IsNullOrEmpty(_id) ? name : _id; } }
        public string DisplayName { get { return _displayName; } }
        public string Description { get { return _description; } }
        public WeaponClass Class { get { return _class; } }
        public float Damage { get { return _damage; } }
        public float StaminaCost { get { return _staminaCost; } }
        public float SwingTime { get { return _swingTime; } }
        public float Range { get { return _range; } }
        public float GuardLeak { get { return _guardLeak; } }

        /// <summary>A heavy weapon needs both hands, so it cannot go with a shield.</summary>
        public bool TwoHanded { get { return _class == WeaponClass.Pesada; } }
    }
}
