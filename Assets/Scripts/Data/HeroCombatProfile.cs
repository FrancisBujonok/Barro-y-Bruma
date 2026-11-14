using System;
using UnityEngine;

namespace BrumaPx.Data
{
    /// <summary>Numbers of one melee attack.</summary>
    [Serializable]
    public sealed class HeroAttackStats
    {
        public float damage = 10f;
        public float startup = 0.07f;
        public float active = 0.09f;
        public float recovery = 0.2f;
        public float range = 1.5f;
        public float arcDegrees = 110f;
        public float stamina = 12f;
        [Tooltip("How far the hero steps forward during the swing.")]
        public float lunge = 0.3f;
        [Tooltip("How far enemies hear the swing.")]
        public float noise = 6f;
        [Tooltip("Seconds the game freezes briefly on a hit.")]
        public float hitStop = 0.035f;
    }

    /// <summary>
    /// All the hero's combat numbers in one file, so balance is tuned in the inspector instead of in code.
    /// The defaults are the values the combat shipped with.
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Hero Combat Profile", fileName = "HeroCombatProfile")]
    public sealed class HeroCombatProfile : ScriptableObject
    {
        [Header("Health and stamina")]
        [SerializeField, Min(1f)] float _maxHealth = 100f;
        [SerializeField, Min(1f)] float _maxStamina = 100f;
        [SerializeField] float _staminaRegen = 30f;
        [SerializeField] float _regenDelay = 0.7f;

        [Header("Attacks")]
        [SerializeField] HeroAttackStats _light = new HeroAttackStats();
        [SerializeField]
        HeroAttackStats _heavy = new HeroAttackStats
        {
            damage = 28f, startup = 0.32f, active = 0.12f, recovery = 0.45f,
            range = 1.7f, arcDegrees = 130f, stamina = 28f, lunge = 0.55f, noise = 8f, hitStop = 0.07f
        };
        [SerializeField] float _comboWindow = 0.45f;
        [SerializeField] float _inputBuffer = 0.35f;

        [Header("Dodge")]
        [SerializeField] float _dodgeDistance = 2.6f;
        [SerializeField] float _dodgeDuration = 0.22f;
        [SerializeField] float _dodgeCooldown = 0.45f;
        [SerializeField] float _dodgeStamina = 22f;
        [SerializeField] float _dodgeNoise = 4f;

        [Header("Guard")]
        [SerializeField] float _parryWindow = 0.2f;
        [SerializeField, Range(0f, 1f)] float _guardDamageTaken = 0.3f;
        [SerializeField] float _guardStaminaPerDamage = 1.2f;
        [SerializeField, Range(0f, 1f)] float _guardMoveSpeed = 0.45f;
        [SerializeField] float _parryStaminaBack = 15f;

        [Header("Hurt")]
        [SerializeField] float _hurtStun = 0.28f;
        [SerializeField] float _respawnDelay = 2.4f;

        public float MaxHealth { get { return _maxHealth; } }
        public float MaxStamina { get { return _maxStamina; } }
        public float StaminaRegen { get { return _staminaRegen; } }
        public float RegenDelay { get { return _regenDelay; } }
        public HeroAttackStats Light { get { return _light; } }
        public HeroAttackStats Heavy { get { return _heavy; } }
        public float ComboWindow { get { return _comboWindow; } }
        public float InputBuffer { get { return _inputBuffer; } }
        public float DodgeDistance { get { return _dodgeDistance; } }
        public float DodgeDuration { get { return _dodgeDuration; } }
        public float DodgeCooldown { get { return _dodgeCooldown; } }
        public float DodgeStamina { get { return _dodgeStamina; } }
        public float DodgeNoise { get { return _dodgeNoise; } }
        public float ParryWindow { get { return _parryWindow; } }
        public float GuardDamageTaken { get { return _guardDamageTaken; } }
        public float GuardStaminaPerDamage { get { return _guardStaminaPerDamage; } }
        public float GuardMoveSpeed { get { return _guardMoveSpeed; } }
        public float ParryStaminaBack { get { return _parryStaminaBack; } }
        public float HurtStun { get { return _hurtStun; } }
        public float RespawnDelay { get { return _respawnDelay; } }
    }
}
