using System;
using System.Collections.Generic;
using BrumaPx.Combat;
using BrumaPx.Data;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// What the hero wears. The wardrobe holds every piece he owns (apart from the bag, on purpose) and each of the
    /// four slots can carry one piece. Putting one on or taking it off takes time, during which the hero is busy: a
    /// blow stops it. What is worn is read by HeroBody: cover against the Bruma, share of each blow absorbed, and
    /// stat changes (the heavy pieces slow him down).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Equipment : MonoBehaviour
    {
        const float MaxBlowReduction = 0.75f;

        [Header("Links")]
        public Health health;
        public HeroBody body;

        [Tooltip("Pieces the hero owns when the game starts (test wardrobe for now).")]
        [SerializeField] ArmorDef[] startingWardrobe = new ArmorDef[0];

        public event Action Changed;

        readonly List<ArmorDef> _wardrobe = new List<ArmorDef>();
        readonly ArmorDef[] _worn = new ArmorDef[ArmorSlotInfo.Count];
        readonly float[] _mult = new float[BodyStatInfo.Count];
        float _blow;

        bool _busy, _putOn;
        ArmorDef _target;
        ArmorSlot _slot;
        float _total, _left, _lastHp;

        public IReadOnlyList<ArmorDef> Wardrobe { get { return _wardrobe; } }
        public ArmorDef Worn(ArmorSlot slot) { return _worn[(int)slot]; }
        public bool Busy { get { return _busy; } }
        public float Progress01 { get { return _busy && _total > 0f ? Mathf.Clamp01(1f - _left / _total) : 0f; } }

        /// <summary>"Te pones: Cota de malla" while a change is under way, empty otherwise.</summary>
        public string BusyText
        {
            get
            {
                if (!_busy || _target == null) return string.Empty;
                return (_putOn ? "Te pones: " : "Te quitas: ") + _target.DisplayName;
            }
        }

        /// <summary>Share of every blow the worn pieces absorb, 0 to 0.75.</summary>
        public float BlowReduction { get { return _blow; } }

        /// <summary>Multiplier the worn pieces put on a stat (1 = unchanged).</summary>
        public float StatMult(BodyStat stat) { return _mult[(int)stat]; }

        /// <summary>Share of the Bruma the worn piece stops in this zone (0 when nothing covers it).</summary>
        public float CoverFor(string zoneId)
        {
            ArmorDef p = PieceCovering(zoneId);
            return p != null ? p.BrumaCover : 0f;
        }

        /// <summary>The piece worn over a zone, or null.</summary>
        public ArmorDef PieceCovering(string zoneId)
        {
            for (int s = 0; s < _worn.Length; s++)
                if (_worn[s] != null && ArmorSlotInfo.Covers((ArmorSlot)s, zoneId)) return _worn[s];
            return null;
        }

        void Awake()
        {
            for (int i = 0; i < _mult.Length; i++) _mult[i] = 1f;
        }

        void Start()
        {
            for (int i = 0; i < startingWardrobe.Length; i++) Add(startingWardrobe[i]);
        }

        /// <summary>Adds a piece to the wardrobe. Returns false when he already has it.</summary>
        public bool Add(ArmorDef piece)
        {
            if (piece == null || _wardrobe.Contains(piece)) return false;
            _wardrobe.Add(piece);
            RaiseChanged();
            return true;
        }

        public bool BeginEquip(ArmorDef piece)
        {
            if (_busy || piece == null || !_wardrobe.Contains(piece)) return false;
            ArmorDef old = _worn[(int)piece.Slot];
            if (old == piece) return false;
            _busy = true;
            _putOn = true;
            _target = piece;
            _slot = piece.Slot;
            _total = piece.EquipSeconds + (old != null ? old.UnequipSeconds : 0f);
            Begin();
            return true;
        }

        public bool BeginUnequip(ArmorSlot slot)
        {
            ArmorDef piece = _worn[(int)slot];
            if (_busy || piece == null) return false;
            _busy = true;
            _putOn = false;
            _target = piece;
            _slot = slot;
            _total = piece.UnequipSeconds;
            Begin();
            return true;
        }

        void Begin()
        {
            _total = Mathf.Max(0.05f, _total);
            _left = _total;
            _lastHp = health != null ? health.Current : 0f;
        }

        /// <summary>Stops the change under way. The reason, when given, is said to the hero.</summary>
        public void Cancel(string reason)
        {
            if (!_busy) return;
            _busy = false;
            _target = null;
            if (!string.IsNullOrEmpty(reason) && body != null) body.Say(reason);
        }

        void Update()
        {
            if (health == null) return;
            if (health.IsDead)
            {
                Cancel(null);
                return;
            }
            if (!_busy)
            {
                _lastHp = health.Current;
                return;
            }
            if (health.Current < _lastHp - 0.01f)
            {
                _lastHp = health.Current;
                Cancel("Te golpean: dejas de vestirte");
                return;
            }
            _lastHp = health.Current;
            _left -= Time.deltaTime;
            if (_left <= 0f) Finish();
        }

        void Finish()
        {
            _busy = false;
            ArmorDef piece = _target;
            _target = null;
            if (piece == null) return;
            _worn[(int)_slot] = _putOn ? piece : null;
            if (body != null) body.Say((_putOn ? "Te pones: " : "Te quitas: ") + piece.DisplayName);
            RaiseChanged();
        }

        void RaiseChanged()
        {
            float blow = 0f;
            for (int i = 0; i < _mult.Length; i++) _mult[i] = 1f;
            for (int s = 0; s < _worn.Length; s++)
            {
                ArmorDef p = _worn[s];
                if (p == null) continue;
                blow += p.BlowReduction;
                var mods = p.Mods;
                for (int m = 0; mods != null && m < mods.Length; m++)
                    if (mods[m] != null) _mult[(int)mods[m].stat] *= 1f + mods[m].change;
            }
            _blow = Mathf.Clamp(blow, 0f, MaxBlowReduction);
            if (Changed != null) Changed();
        }
    }
}
