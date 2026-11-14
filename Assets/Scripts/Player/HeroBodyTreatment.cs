using BrumaPx.Data;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// Second half of HeroBody: treating wounds by zone, the cure, and what the worn equipment changes.
    /// A wound goes Open (the Bruma enters fast) to Cleaned (a salve; it still lets some in) to Bandaged (sealed for
    /// a while, then healed). Bandaging an open wound without cleaning it seals the Bruma in. A blow on a bandaged
    /// zone opens it again.
    /// </summary>
    public sealed partial class HeroBody
    {
        [Header("Treatment")]
        [Tooltip("Dose multiplier of a cleaned wound (an open one uses the tuning's wound multiplier).")]
        [SerializeField, Min(1f)] float cleanedMultiplier = 1.5f;
        [Tooltip("Minimum share of the Bruma that gets through a cleaned wound even with full resistance.")]
        [SerializeField, Range(0f, 1f)] float cleanedLeak = 0.15f;
        [Tooltip("Seconds a bandage holds before the wound is healed.")]
        [SerializeField, Min(1f)] float bandageSeconds = 90f;
        [Tooltip("Bruma that gets in when a wound is bandaged without cleaning it first.")]
        [SerializeField, Min(0f)] float dirtyBandageDose = 8f;
        [Tooltip("Bruma a salve takes out of the zone it is put on.")]
        [SerializeField, Min(0f)] float salveRelief = 8f;

        float[] _bandageLeft;
        Equipment _eq;

        Equipment Eq
        {
            get
            {
                if (_eq == null) _eq = GetComponent<Equipment>();
                return _eq;
            }
        }

        bool InRange(int zone) { return _level != null && zone >= 0 && zone < _level.Length; }

        public float SalveRelief { get { return salveRelief; } }

        // ------------------------------------------------------------------------------------------ equipment

        /// <summary>What a blow still does after the armour: 1 = all of it.</summary>
        public float BlowTaken
        {
            get
            {
                Equipment e = Eq;
                return e != null ? 1f - e.BlowReduction : 1f;
            }
        }

        /// <summary>Share of the Bruma stopped in a zone (the profile's own cover plus what is worn there).</summary>
        public float Cover(int zone)
        {
            if (!InRange(zone)) return 0f;
            BodyZone z = profile.Zones[zone];
            Equipment e = Eq;
            float worn = e != null ? e.CoverFor(z.id) : 0f;
            return Mathf.Clamp01(1f - (1f - Mathf.Clamp01(z.cover)) * (1f - worn));
        }

        // ------------------------------------------------------------------------------------------ wounds

        public WoundState Wound(int i) { return InRange(i) ? _wound[i] : WoundState.None; }
        public float BandageLeft(int i) { return InRange(i) ? _bandageLeft[i] : 0f; }

        public bool CanBandage(int zone)
        {
            return Active && InRange(zone) && (_wound[zone] == WoundState.Open || _wound[zone] == WoundState.Cleaned);
        }

        public bool CanSalve(int zone)
        {
            return Active && InRange(zone) && (_wound[zone] == WoundState.Open || _level[zone] > 0.01f);
        }

        public bool CanCure(int zone)
        {
            return Active && InRange(zone) && _level[zone] > 0.01f;
        }

        /// <summary>Seals the wound of a zone with a bandage. Over a wound that was not cleaned, some Bruma is sealed in.</summary>
        public bool Bandage(int zone)
        {
            if (!CanBandage(zone)) return false;
            bool dirty = _wound[zone] == WoundState.Open;
            string name = profile.Zones[zone].displayName;
            _wound[zone] = WoundState.Bandaged;
            _bandageLeft[zone] = bandageSeconds;
            _woundOrder.Remove(zone);
            if (dirty)
            {
                SetLevel(zone, _level[zone] + dirtyBandageDose, true);
                Say("Vendada sin limpiar: algo de Bruma quedó dentro (" + name + ")");
            }
            else
            {
                Say("Herida vendada: " + name);
            }
            return true;
        }

        // The belt bandage: picks the wound itself, the cleaned ones first, then the most recent.
        bool BandageNext()
        {
            int pick = -1;
            for (int k = _woundOrder.Count - 1; k >= 0; k--)
            {
                if (_wound[_woundOrder[k]] == WoundState.Cleaned) { pick = _woundOrder[k]; break; }
            }
            if (pick < 0 && _woundOrder.Count > 0) pick = _woundOrder[_woundOrder.Count - 1];
            return pick >= 0 && Bandage(pick);
        }

        /// <summary>A salve on a zone: cleans an open wound and takes a little Bruma out of the zone.</summary>
        public bool Salve(int zone, float relief)
        {
            if (!CanSalve(zone)) return false;
            string name = profile.Zones[zone].displayName;
            if (_wound[zone] == WoundState.Open)
            {
                _wound[zone] = WoundState.Cleaned;
                Say("Herida limpia: " + name + ". Falta vendarla");
            }
            if (_level[zone] > 0.01f) SetLevel(zone, _level[zone] - Mathf.Max(0f, relief), false);
            return true;
        }

        /// <summary>A cure on a zone: takes Bruma out of it. It never takes it all, only time.</summary>
        public bool Cure(int zone, float amount)
        {
            if (!CanCure(zone)) return false;
            SetLevel(zone, _level[zone] - Mathf.Max(0f, amount), false);
            Say("La Bruma retrocede: " + profile.Zones[zone].displayName);
            return true;
        }

        // In a refuge a wound heals on its own, the most recent first.
        void HealOneWound()
        {
            if (_woundOrder.Count == 0) return;
            int zone = _woundOrder[_woundOrder.Count - 1];
            _woundOrder.RemoveAt(_woundOrder.Count - 1);
            _wound[zone] = WoundState.None;
            Say("Herida curada: " + profile.Zones[zone].displayName);
        }

        void TickBandages(float dt)
        {
            for (int i = 0; i < _wound.Length; i++)
            {
                if (_wound[i] != WoundState.Bandaged) continue;
                _bandageLeft[i] -= dt;
                if (_bandageLeft[i] > 0f) continue;
                _wound[i] = WoundState.None;
                _bandageLeft[i] = 0f;
                Say("La herida sanó: " + profile.Zones[i].displayName);
            }
        }
    }
}
