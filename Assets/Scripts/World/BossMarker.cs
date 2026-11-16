using System.Collections.Generic;
using BrumaPx.Combat;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>Marks an enemy as a boss: gives it a name and a health bar on screen while the hero is near.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class BossMarker : MonoBehaviour
    {
        public string title = "Jefe";
        public float showRange = 28f;

        public static readonly List<BossMarker> All = new List<BossMarker>();

        Health _health;

        public float Fraction { get { return _health == null ? 0f : Mathf.Clamp01(_health.Current / Mathf.Max(1f, _health.max)); } }
        public bool Alive { get { return _health != null && !_health.IsDead; } }

        void Awake() { _health = GetComponent<Health>(); }
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void Start() { if (_health != null) _health.Died += OnDied; }
        void OnDestroy() { if (_health != null) _health.Died -= OnDied; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void OnDied() { MapWorld.Banner(title + " ha caido", 6f); }

        /// <summary>The living boss closest to the point, if one is within its show range.</summary>
        public static BossMarker Nearest(Vector3 point)
        {
            BossMarker best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < All.Count; i++)
            {
                var b = All[i];
                if (!b.Alive) continue;
                Vector3 d = b.transform.position - point;
                d.y = 0f;
                float m = d.magnitude;
                if (m <= b.showRange && m < bestD) { bestD = m; best = b; }
            }
            return best;
        }
    }
}
