using System;
using System.Collections.Generic;
using BrumaPx.Combat;
using BrumaPx.Data;
using BrumaPx.World;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// The hero's body against the Bruma. It keeps a level (0 to 100) and an open-wound flag for every zone of the
    /// BodyProfileDef, and one Resistance that stands in front of them: exposure drains resistance first, and the
    /// Bruma only gets into a zone when resistance is low (or the zone has an open wound). Each zone, as it fills,
    /// takes things away (BodyStat multipliers) that HeroCombat, DioramaHero and Inventory read through Mult.
    /// Ways in: thick air (BrumaArea), blows of the infected (HeroCombat.ReceiveAttack), open wounds, and objects
    /// or bodies (BrumaSource). A refuge stops everything and slowly cleans the zones that are only exposed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroBody : MonoBehaviour
    {
        public static HeroBody Instance { get; private set; }

        /// <summary>Multiplies how far the hero sees (the fog island and the loaded chunks). 1 = normal.</summary>
        public static float VisionScale = 1f;

        [Header("Links")]
        public Health health;
        public HeroCombat combat;

        [Header("Data")]
        public BodyProfileDef profile;
        public BrumaTuning tuning;

        /// <summary>Short messages about the body ("Herida abierta: Brazo der.").</summary>
        public event Action<string> Notice;

        float[] _level;
        bool[] _wound;
        BrumaStage[] _stage;
        readonly List<int> _woundOrder = new List<int>();
        readonly float[] _mult = new float[BodyStatInfo.Count];

        float _resistance, _sinceExposure, _palliateLeft, _baseMax, _refugeWoundTimer;
        bool _wasDead, _inAir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            VisionScale = 1f;
            Instance = null;
        }

        // ------------------------------------------------------------------------------------------ state

        public int Count { get { return _level != null ? _level.Length : 0; } }
        public BodyZone Zone(int i) { return profile.Zones[i]; }
        public float Level(int i) { return _level[i]; }
        public BrumaStage Stage(int i) { return _stage[i]; }
        public bool HasWound(int i) { return _wound[i]; }
        public bool HasOpenWound { get { return _woundOrder.Count > 0; } }
        public int OpenWounds { get { return _woundOrder.Count; } }
        public float Resistance01 { get { return tuning != null ? Mathf.Clamp01(_resistance / tuning.MaxResistance) : 1f; } }
        public float PalliateLeft { get { return _palliateLeft; } }

        /// <summary>Average level of all the zones, 0 to 1. Slow by nature: one zone alone barely moves it.</summary>
        public float Overall01
        {
            get
            {
                if (_level == null || _level.Length == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < _level.Length; i++) sum += _level[i];
                return sum / (_level.Length * 100f);
            }
        }

        /// <summary>The zone with the most Bruma, or -1 when the body is clean.</summary>
        public int WorstZone
        {
            get
            {
                int worst = -1;
                float best = 0f;
                for (int i = 0; _level != null && i < _level.Length; i++)
                    if (_level[i] > best) { best = _level[i]; worst = i; }
                return worst;
            }
        }

        /// <summary>True when a palliative would do something: there is Bruma in the body or resistance is down.</summary>
        public bool CanPalliate { get { return Active && (Overall01 > 0.0001f || Resistance01 < 0.999f); } }

        /// <summary>Current multiplier of a stat (1 = unchanged). Never below 0.2 or above 3.</summary>
        public float Mult(BodyStat stat) { return _mult[(int)stat]; }

        public int IndexOf(string id) { return profile != null ? profile.IndexOf(id) : -1; }

        bool Active { get { return _level != null && tuning != null && health != null && !health.IsDead; } }

        /// <summary>How much of a stat's effect a zone has right now (0 to 1).</summary>
        public static float Strength(ZoneEffect e, float level)
        {
            return Mathf.Clamp01((level - e.startsAt) / Mathf.Max(1f, 100f - e.startsAt));
        }

        // ------------------------------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < _mult.Length; i++) _mult[i] = 1f;
            if (profile != null)
            {
                int n = profile.Zones.Length;
                _level = new float[n];
                _wound = new bool[n];
                _stage = new BrumaStage[n];
            }
            _resistance = tuning != null ? tuning.MaxResistance : 100f;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            VisionScale = 1f;
        }

        void Update()
        {
            if (_level == null || tuning == null || health == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (health.IsDead)
            {
                _wasDead = true;
                return;
            }
            if (_wasDead) OnRespawn();
            if (_baseMax <= 0f) _baseMax = health.max;

            Vector3 p = transform.position;
            bool refuge = Refuge.At(p) != null;
            if (_palliateLeft > 0f) _palliateLeft = Mathf.Max(0f, _palliateLeft - dt);

            float density = refuge ? 0f : BrumaArea.DensityAt(p);
            if (density > 0.001f) ApplyAir(density, dt);
            if (density > 0.25f && !_inAir) Say("Bruma densa: te alcanza por el aire");
            _inAir = density > 0.25f;

            Recover(dt, refuge);
            RecomputeMods();
            ApplyHealthMax();
            VisionScale = _mult[(int)BodyStat.VisionRange];
        }

        // The hero came back after dying: wounds are closed and resistance is full, but the Bruma stays.
        void OnRespawn()
        {
            _wasDead = false;
            _resistance = tuning.MaxResistance;
            _sinceExposure = 0f;
            _palliateLeft = 0f;
            _woundOrder.Clear();
            for (int i = 0; i < _wound.Length; i++) _wound[i] = false;
        }

        // ------------------------------------------------------------------------------------------ exposure

        public void Say(string text)
        {
            if (Notice != null && !string.IsNullOrEmpty(text)) Notice(text);
        }

        /// <summary>Exposure that is felt as tiredness first: resistance goes down.</summary>
        public void Absorb(float cost)
        {
            if (!Active || cost <= 0f) return;
            _resistance = Mathf.Max(0f, _resistance - cost);
            _sinceExposure = 0f;
        }

        /// <summary>Puts Bruma into one zone. Resistance, an open wound and a palliative change how much gets in.</summary>
        public void Dose(int zone, float amount)
        {
            if (!Active || zone < 0 || zone >= _level.Length || amount <= 0f) return;
            float gate = Mathf.Max(0.01f, tuning.ResistanceGate);
            float leak = Mathf.Clamp01((gate - Resistance01) / gate);
            if (_wound[zone])
            {
                leak = Mathf.Max(leak, tuning.WoundLeak);
                amount *= tuning.WoundMultiplier;
            }
            if (_palliateLeft > 0f) amount *= tuning.PalliateRateFactor;
            amount *= leak;
            if (amount <= 0f) return;
            SetLevel(zone, _level[zone] + amount, true);
        }

        void ApplyAir(float density, float dt)
        {
            Absorb(tuning.AirResistDrain * density * dt);
            var zones = profile.Zones;
            for (int z = 0; z < zones.Length; z++)
            {
                float w = zones[z].airWeight * (1f - zones[z].cover);
                if (w <= 0f) continue;
                Dose(z, tuning.AirZoneRate * density * w * dt);
            }
        }

        /// <summary>
        /// A blow landed. It opens a wound where it hit; if the attacker carries Bruma it also drains resistance
        /// and doses that zone. Behind the guard the blow tends to land on the left arm, and carries half the dose.
        /// </summary>
        public void OnHitTaken(float brumaDose, bool guarded)
        {
            if (!Active) return;
            int zone = PickHitZone(guarded);
            if (zone < 0) return;
            OpenWound(zone);
            if (brumaDose > 0f)
            {
                Absorb(tuning.HitResistDrain);
                Dose(zone, brumaDose * (guarded ? 0.5f : 1f));
            }
        }

        int PickHitZone(bool guarded)
        {
            var zones = profile.Zones;
            float total = 0f;
            for (int i = 0; i < zones.Length; i++) total += HitWeight(zones[i], guarded);
            if (total <= 0f) return -1;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < zones.Length; i++)
            {
                r -= HitWeight(zones[i], guarded);
                if (r <= 0f) return i;
            }
            return zones.Length - 1;
        }

        static float HitWeight(BodyZone z, bool guarded)
        {
            return z.hitWeight * (guarded && z.guardSide ? 6f : 1f);
        }

        // ------------------------------------------------------------------------------------------ wounds and relief

        void OpenWound(int zone)
        {
            if (_wound[zone]) return;
            _wound[zone] = true;
            _woundOrder.Add(zone);
            Say("Herida abierta: " + profile.Zones[zone].displayName);
        }

        /// <summary>Closes the most recent open wound (a bandage). Returns false when there is none.</summary>
        public bool CloseWound()
        {
            if (_woundOrder.Count == 0) return false;
            int zone = _woundOrder[_woundOrder.Count - 1];
            _woundOrder.RemoveAt(_woundOrder.Count - 1);
            _wound[zone] = false;
            Say("Herida cerrada: " + profile.Zones[zone].displayName);
            return true;
        }

        /// <summary>A palliative: gives back some resistance and slows the Bruma for a while. It does not cure.</summary>
        public void Palliate(float resistance, float seconds)
        {
            if (!Active) return;
            _resistance = Mathf.Min(tuning.MaxResistance, _resistance + Mathf.Max(0f, resistance));
            _palliateLeft = Mathf.Max(_palliateLeft, seconds);
            Say("La Bruma afloja un rato");
        }

        void Recover(float dt, bool refuge)
        {
            _sinceExposure += dt;
            if (refuge)
            {
                _resistance = Mathf.Min(tuning.MaxResistance, _resistance + tuning.RefugeResistRegen * dt);
                for (int z = 0; z < _level.Length; z++)
                {
                    if (_level[z] > 0f && _level[z] < tuning.InfectedAt)
                        SetLevel(z, Mathf.Max(0f, _level[z] - tuning.RefugeCleanse * dt), false);
                }
                if (_woundOrder.Count > 0)
                {
                    _refugeWoundTimer += dt;
                    if (_refugeWoundTimer >= tuning.RefugeWoundSeconds)
                    {
                        _refugeWoundTimer = 0f;
                        CloseWound();
                    }
                }
                else
                {
                    _refugeWoundTimer = 0f;
                }
            }
            else if (_sinceExposure >= tuning.ResistRegenDelay)
            {
                _resistance = Mathf.Min(tuning.MaxResistance, _resistance + tuning.IdleResistRegen * dt);
            }
        }

        void SetLevel(int zone, float value, bool announce)
        {
            float v = Mathf.Clamp(value, 0f, 100f);
            _level[zone] = v;
            BrumaStage s = tuning.StageOf(v);
            if (announce && s > _stage[zone])
                Say("Bruma en " + profile.Zones[zone].displayName.ToLowerInvariant() + ": " + BodyStatInfo.StageName(s).ToLowerInvariant());
            _stage[zone] = s;
        }

        // ------------------------------------------------------------------------------------------ effects

        // Every effect of every zone multiplies its stat. A stat can come from several zones, so they add up.
        void RecomputeMods()
        {
            for (int i = 0; i < _mult.Length; i++) _mult[i] = 1f;
            var zones = profile.Zones;
            for (int z = 0; z < zones.Length; z++)
            {
                float lv = _level[z];
                if (lv <= 0f || zones[z].effects == null) continue;
                var fx = zones[z].effects;
                for (int e = 0; e < fx.Length; e++)
                {
                    float s = Strength(fx[e], lv);
                    if (s > 0f) _mult[(int)fx[e].stat] *= 1f + fx[e].atFull * s;
                }
            }
            for (int i = 0; i < _mult.Length; i++) _mult[i] = Mathf.Clamp(_mult[i], 0.2f, 3f);
        }

        void ApplyHealthMax()
        {
            float target = Mathf.Max(10f, _baseMax * _mult[(int)BodyStat.MaxHealth]);
            if (Mathf.Abs(health.max - target) < 0.01f) return;
            health.max = target;
            health.ClampToMax();
        }

        // ------------------------------------------------------------------------------------------ test hooks

        /// <summary>Test hook: sets a zone level without messages or resistance.</summary>
        public void DebugSetLevel(int zone, float level)
        {
            if (_level != null && zone >= 0 && zone < _level.Length) SetLevel(zone, level, false);
        }

        /// <summary>Test hook: sets resistance (0 to max).</summary>
        public void DebugSetResistance(float value)
        {
            _resistance = Mathf.Clamp(value, 0f, tuning != null ? tuning.MaxResistance : 100f);
        }
    }
}
