using UnityEngine;

namespace BrumaPx.Data
{
    /// <summary>
    /// The numbers of how the Bruma gets into the body. Everything is per second unless said otherwise, and the
    /// zone levels go from 0 to 100. Slow on purpose: from a clean body, thick air takes minutes to infect a zone.
    /// Create from the menu "Barro y Bruma/Bruma Tuning".
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Bruma Tuning", fileName = "BrumaTuning")]
    public sealed class BrumaTuning : ScriptableObject
    {
        [Header("Stages of a zone (0 to 100)")]
        [SerializeField, Range(1f, 99f)] float _infectedAt = 34f;
        [SerializeField, Range(1f, 99f)] float _takenAt = 67f;

        [Header("Resistance")]
        [SerializeField, Min(1f)] float _maxResistance = 100f;
        [Tooltip("While resistance is above this share the Bruma cannot get into a clean zone; below it, it gets in more and more.")]
        [SerializeField, Range(0.05f, 1f)] float _resistanceGate = 0.35f;
        [SerializeField, Min(0f)] float _idleResistRegen = 0.8f;
        [Tooltip("Seconds without exposure before resistance starts to recover.")]
        [SerializeField, Min(0f)] float _resistRegenDelay = 3f;
        [SerializeField, Min(0f)] float _refugeResistRegen = 10f;

        [Header("Air and thick Bruma (at density 1)")]
        [SerializeField, Min(0f)] float _airResistDrain = 0.45f;
        [SerializeField, Min(0f)] float _airZoneRate = 0.12f;

        [Header("Blows of the infected")]
        [SerializeField, Min(0f)] float _hitResistDrain = 8f;

        [Header("Open wounds")]
        [Tooltip("A wounded zone takes this many times more Bruma.")]
        [SerializeField, Min(1f)] float _woundMultiplier = 3f;
        [Tooltip("A wound lets this share of the Bruma in even when resistance is high.")]
        [SerializeField, Range(0f, 1f)] float _woundLeak = 0.3f;

        [Header("Refuge")]
        [Tooltip("Zones below the infected stage lose this much per second while resting in a refuge.")]
        [SerializeField, Min(0f)] float _refugeCleanse = 0.8f;
        [SerializeField, Min(1f)] float _refugeWoundSeconds = 6f;

        [Header("Palliative")]
        [Tooltip("While a palliative lasts, Bruma gets into the body at this share of its normal speed.")]
        [SerializeField, Range(0f, 1f)] float _palliateRateFactor = 0.3f;

        public float InfectedAt { get { return _infectedAt; } }
        public float TakenAt { get { return Mathf.Max(_infectedAt + 1f, _takenAt); } }
        public float MaxResistance { get { return _maxResistance; } }
        public float ResistanceGate { get { return _resistanceGate; } }
        public float IdleResistRegen { get { return _idleResistRegen; } }
        public float ResistRegenDelay { get { return _resistRegenDelay; } }
        public float RefugeResistRegen { get { return _refugeResistRegen; } }
        public float AirResistDrain { get { return _airResistDrain; } }
        public float AirZoneRate { get { return _airZoneRate; } }
        public float HitResistDrain { get { return _hitResistDrain; } }
        public float WoundMultiplier { get { return _woundMultiplier; } }
        public float WoundLeak { get { return _woundLeak; } }
        public float RefugeCleanse { get { return _refugeCleanse; } }
        public float RefugeWoundSeconds { get { return _refugeWoundSeconds; } }
        public float PalliateRateFactor { get { return _palliateRateFactor; } }

        public BrumaStage StageOf(float level)
        {
            if (level <= 0f) return BrumaStage.Clean;
            if (level < InfectedAt) return BrumaStage.Exposed;
            if (level < TakenAt) return BrumaStage.Infected;
            return BrumaStage.Taken;
        }
    }
}
