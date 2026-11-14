using UnityEngine;

namespace BrumaPx.Combat
{
    /// <summary>
    /// All tunable values of one kind of enemy. Distances are world units, times are seconds.
    /// Defaults describe a slow infected; see Assets/Data/Enemies for the real ones.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "Barro y Bruma/Enemy Profile")]
    public sealed class EnemyProfile : ScriptableObject
    {
        [Header("Body")]
        [SerializeField] private string _displayName = "Enemigo";
        [SerializeField, Min(1f)] private float _maxHealth = 30f;
        [SerializeField, Min(0.1f)] private float _radius = 0.35f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _walkSpeed = 1.1f;
        [SerializeField, Min(0f)] private float _chaseSpeed = 2.3f;

        [Header("Senses")]
        [Tooltip("How far it sees a fully lit hero.")]
        [SerializeField, Min(0f)] private float _sightRange = 7f;
        [Tooltip("Share of the sight range left when the hero is in complete darkness (0 = blind, 1 = no effect).")]
        [SerializeField, Range(0f, 1f)] private float _minLightFactor = 0.35f;
        [SerializeField, Range(20f, 360f)] private float _fieldOfView = 120f;
        [Tooltip("Multiplies how far a sound carries to this enemy.")]
        [SerializeField, Min(0f)] private float _hearingMultiplier = 1.2f;
        [Tooltip("Seconds without seeing the hero before it gives up the chase.")]
        [SerializeField, Min(0f)] private float _loseTrackSeconds = 5f;

        [Header("Attack")]
        [SerializeField, Min(0.1f)] private float _attackRange = 1.2f;
        [SerializeField, Min(0f)] private float _attackDamage = 14f;
        [SerializeField, Min(0f)] private float _attackStartup = 0.5f;
        [SerializeField, Min(0f)] private float _attackActive = 0.15f;
        [SerializeField, Min(0f)] private float _attackRecovery = 0.8f;
        [SerializeField, Range(10f, 360f)] private float _attackArcDegrees = 100f;
        [SerializeField, Min(0f)] private float _attackNoise = 4f;
        [Tooltip("Bruma that a blow of this enemy puts into the zone it hits (0 = none).")]
        [SerializeField, Min(0f)] private float _brumaOnHit;

        [Header("Reactions")]
        [Tooltip("A single hit at or above this damage interrupts it (a heavy hit).")]
        [SerializeField, Min(0f)] private float _staggerThreshold = 18f;
        [SerializeField, Min(0f)] private float _staggerSeconds = 0.5f;
        [SerializeField, Min(0f)] private float _knockbackScale = 1f;
        [SerializeField] private bool _canSidestep;
        [SerializeField, Range(0f, 1f)] private float _sidestepChance = 0.35f;
        [SerializeField, Min(0f)] private float _sidestepDistance = 1.3f;

        [Header("Traits (0 = the enemy does not have it)")]
        [Tooltip("Allies of the same kind within this distance join the chase when one of them spots the hero.")]
        [SerializeField, Min(0f)] private float _packRadius;
        [Tooltip("More than 0 makes it a ranged attacker: the speed of its bolt. Attack Range is then how far it shoots.")]
        [SerializeField, Min(0f)] private float _projectileSpeed;
        [Tooltip("Ranged: it backs away when the hero comes closer than this.")]
        [SerializeField, Min(0f)] private float _keepDistance;
        [Tooltip("More than 0 makes it an ambusher: hidden until the hero comes this close, then it lunges.")]
        [SerializeField, Min(0f)] private float _ambushRange;
        [SerializeField, Min(1f)] private float _lungeSpeedFactor = 1.9f;
        [SerializeField, Min(0f)] private float _lungeSeconds = 0.7f;
        [Tooltip("More than 0: it carries Bruma around it, this wide. The taken are patches that walk.")]
        [SerializeField, Min(0f)] private float _auraRadius;
        [SerializeField, Range(0f, 1f)] private float _auraDensity = 0.6f;
        [Tooltip("More than 0: when it dies it leaves a cloud of Bruma this wide for a while.")]
        [SerializeField, Min(0f)] private float _deathCloudRadius;
        [SerializeField, Min(1f)] private float _deathCloudSeconds = 25f;
        [SerializeField, Range(0f, 1f)] private float _deathCloudDensity = 1f;

        public string DisplayName => _displayName;
        public float MaxHealth => _maxHealth;
        public float Radius => _radius;
        public float WalkSpeed => _walkSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float SightRange => _sightRange;
        public float MinLightFactor => _minLightFactor;
        public float FieldOfView => _fieldOfView;
        public float HearingMultiplier => _hearingMultiplier;
        public float LoseTrackSeconds => _loseTrackSeconds;
        public float AttackRange => _attackRange;
        public float AttackDamage => _attackDamage;
        public float AttackStartup => _attackStartup;
        public float AttackActive => _attackActive;
        public float AttackRecovery => _attackRecovery;
        public float AttackArcDegrees => _attackArcDegrees;
        public float AttackNoise => _attackNoise;
        public float BrumaOnHit => _brumaOnHit;
        public float StaggerThreshold => _staggerThreshold;
        public float StaggerSeconds => _staggerSeconds;
        public float KnockbackScale => _knockbackScale;
        public bool CanSidestep => _canSidestep;
        public float SidestepChance => _sidestepChance;
        public float SidestepDistance => _sidestepDistance;
        public float PackRadius => _packRadius;
        public float ProjectileSpeed => _projectileSpeed;
        public float KeepDistance => _keepDistance;
        public float AmbushRange => _ambushRange;
        public float LungeSpeedFactor => _lungeSpeedFactor;
        public float LungeSeconds => _lungeSeconds;
        public float AuraRadius => _auraRadius;
        public float AuraDensity => _auraDensity;
        public float DeathCloudRadius => _deathCloudRadius;
        public float DeathCloudSeconds => _deathCloudSeconds;
        public float DeathCloudDensity => _deathCloudDensity;
    }
}
