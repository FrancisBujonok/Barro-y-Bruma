using System;
using System.Collections.Generic;
using BarroYBruma.Combat;
using UnityEngine;

namespace BrumaPx.Combat
{
    /// <summary>
    /// Hit points for anything in the overworld that can be hurt (the hero and the enemies). The direction passed to
    /// TakeDamage is on the ground: x is world X, y is world Z. A short flash swaps the first material of every
    /// mesh for flashMaterial, so a hit reads clearly in the toon look.
    /// </summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        public static readonly List<Health> All = new List<Health>();

        [Tooltip("0 = hero, 1 = enemies. Attacks only hurt the other team.")]
        public int team;
        [Min(1f)] public float max = 100f;
        [Tooltip("Shown for a moment when hit (any bright material).")]
        public Material flashMaterial;

        /// <summary>While true all damage is ignored (respawn grace).</summary>
        public bool Invulnerable;

        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>Damage taken and the direction the hit came from.</summary>
        public event Action<float, Vector2> Damaged;
        public event Action Died;

        MeshRenderer[] _renderers;
        Material[][] _original;
        float _flashLeft;

        void Awake()
        {
            Current = max;
            _renderers = GetComponentsInChildren<MeshRenderer>(true);
            _original = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++) _original[i] = _renderers[i].sharedMaterials;
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>Back to full health, alive and vulnerable.</summary>
        public void ResetFull()
        {
            Current = max;
            IsDead = false;
            Invulnerable = false;
            SetFlash(false);
        }

        /// <summary>Brings the current health down to the maximum (used when the maximum was lowered).</summary>
        public void ClampToMax()
        {
            if (Current > max) Current = max;
        }

        public void Heal(float amount)
        {
            if (!IsDead && amount > 0f) Current = Mathf.Min(max, Current + amount);
        }

        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (IsDead || Invulnerable || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            _flashLeft = 0.09f;
            SetFlash(true);
            if (Damaged != null) Damaged(amount, hitDirection);
            if (Current <= 0f)
            {
                IsDead = true;
                if (Died != null) Died();
            }
        }

        void Update()
        {
            if (_flashLeft <= 0f) return;
            _flashLeft -= Time.unscaledDeltaTime;
            if (_flashLeft <= 0f) SetFlash(false);
        }

        void SetFlash(bool on)
        {
            if (_renderers == null || flashMaterial == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (!on)
                {
                    _renderers[i].sharedMaterials = _original[i];
                    continue;
                }
                Material[] flash = (Material[])_original[i].Clone();
                if (flash.Length > 0) flash[0] = flashMaterial;
                _renderers[i].sharedMaterials = flash;
            }
        }
    }
}
