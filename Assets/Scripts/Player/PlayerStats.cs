using UnityEngine;

namespace BarroYBruma.Player
{
    /// <summary>
    /// All tunable player gameplay values (story 001, design/game-brief.md). Distances are world
    /// units (32 px each), times are seconds. Defaults are starting points for tuning.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Barro y Bruma/Player Stats")]
    public sealed class PlayerStats : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;

        [Header("Health")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        [Header("Melee attack")]
        [SerializeField, Min(0f)] private float _attackDamage = 10f;
        [SerializeField, Min(0f)] private float _attackRange = 1.2f;
        [SerializeField, Range(1f, 360f)] private float _attackArcDegrees = 100f;
        [SerializeField, Min(0f)] private float _attackStartup = 0.08f;
        [SerializeField, Min(0f)] private float _attackActive = 0.10f;
        [SerializeField, Min(0f)] private float _attackRecovery = 0.25f;
        [SerializeField] private LayerMask _attackHitMask = ~0;

        [Header("Dodge")]
        [SerializeField, Min(0f)] private float _dodgeDistance = 2.5f;
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.18f;
        [SerializeField, Min(0f)] private float _dodgeCooldown = 0.8f;

        [Header("Sorting")]
        [Tooltip("Y offset from the transform to the feet; sorting uses the feet so the player passes behind/in front correctly.")]
        [SerializeField] private float _sortPivotOffsetY = -0.42f;

        /// <summary>Walk speed, world units per second.</summary>
        public float MoveSpeed => _moveSpeed;
        /// <summary>Maximum health.</summary>
        public float MaxHealth => _maxHealth;
        /// <summary>Damage dealt per melee hit.</summary>
        public float AttackDamage => _attackDamage;
        /// <summary>Melee reach in world units.</summary>
        public float AttackRange => _attackRange;
        /// <summary>Total width of the frontal arc, degrees.</summary>
        public float AttackArcDegrees => _attackArcDegrees;
        /// <summary>Seconds before the hit window opens.</summary>
        public float AttackStartup => _attackStartup;
        /// <summary>Seconds the hit window stays open.</summary>
        public float AttackActive => _attackActive;
        /// <summary>Seconds after the hit window before another attack may start.</summary>
        public float AttackRecovery => _attackRecovery;
        /// <summary>Layers the melee attack can hit.</summary>
        public LayerMask AttackHitMask => _attackHitMask;
        /// <summary>Total dodge travel, world units.</summary>
        public float DodgeDistance => _dodgeDistance;
        /// <summary>Dodge impulse length, seconds.</summary>
        public float DodgeDuration => _dodgeDuration;
        /// <summary>Seconds after a dodge ends before the next may start.</summary>
        public float DodgeCooldown => _dodgeCooldown;
        /// <summary>Local Y offset of the feet used for Y-sorting.</summary>
        public float SortPivotOffsetY => _sortPivotOffsetY;
    }
}
