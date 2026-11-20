using BrumaPx.Atmosphere;
using BrumaPx.Combat;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// Where a boss spends the day and the night. By day it rests deep in its cave; at night it leaves and waits
    /// out on open ground near the opening (the Rey Sapo in the swamp). It only changes place while it is calm and
    /// the hero is not close enough to watch it happen, so it never pops in front of him: if he stays nearby at dusk,
    /// the boss is still in the cave when he walks in, and it is out in the swamp the next time he comes back.
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class BossLair : MonoBehaviour
    {
        public Vector3 dayHome;
        public Vector3 nightHome;
        [Tooltip("Hours of the night (from, to) on a 24 h clock.")]
        public Vector2 night = new Vector2(19.5f, 6f);
        [Tooltip("The boss only moves when the hero is farther than this from both places.")]
        public float unseenDistance = 24f;

        CaveLair.Data _lair;
        EnemyBrain _brain;
        Health _health;
        bool _woken;
        DayNightWeather _sky;
        bool _atNight, _placed;
        float _check;

        public static void Attach(GameObject boss, CaveLair.Data lair)
        {
            if (boss == null || lair == null) return;
            var l = boss.GetComponent<BossLair>();
            if (l == null) l = boss.AddComponent<BossLair>();
            l._lair = lair;
            l.dayHome = new Vector3(lair.center.x, 0f, lair.center.y);
            l.nightHome = new Vector3(lair.nightSpot.x, 0f, lair.nightSpot.y);
        }

        public bool IsNight
        {
            get
            {
                if (_sky == null) return false;
                float h = _sky.timeOfDay;
                return night.x > night.y ? (h >= night.x || h < night.y) : (h >= night.x && h < night.y);
            }
        }

        void Start()
        {
            _brain = GetComponent<EnemyBrain>();
            _health = GetComponent<Health>();
            _sky = Object.FindFirstObjectByType<DayNightWeather>();
            // First placement does not wait: nobody has seen the boss yet.
            _atNight = IsNight;
            _brain.SetHome(_atNight ? nightHome : dayHome, true, _atNight ? FaceAway() : FaceOut());
            _placed = true;
            if (_health != null) _health.Damaged += OnDamaged;
        }

        void OnDestroy() { if (_health != null) _health.Damaged -= OnDamaged; }

        void OnDamaged(float amount, Vector2 dir) { _woken = true; _brain.Dormant = false; }

        // By day it sleeps in the dark: it does not react to anything outside the cave. It wakes when the hero
        // steps into the cave or hits it, and stays awake until it is calm again.
        void Sleep()
        {
            if (_atNight || _lair == null) { _brain.Dormant = false; return; }
            var hero = DioramaHeroCache.Hero;
            bool heroInside = hero != null && CaveLair.Inside(_lair, hero.position);
            if (heroInside) _woken = true;
            else if (_woken && _brain.IsCalm && Flat(transform.position - dayHome) < 3f) _woken = false;
            _brain.Dormant = !_woken;
        }

        void Update()
        {
            if (!_placed || _brain == null || (_health != null && _health.IsDead)) return;
            Sleep();
            _check -= Time.deltaTime;
            if (_check > 0f) return;
            _check = 1f;

            bool wantNight = IsNight;
            if (wantNight == _atNight || !_brain.IsCalm) return;
            var hero = DioramaHeroCache.Hero;
            if (hero != null)
            {
                Vector3 h = hero.position;
                if (Flat(h - dayHome) < unseenDistance || Flat(h - nightHome) < unseenDistance || Flat(h - transform.position) < unseenDistance) return;
            }
            _atNight = wantNight;
            _brain.SetHome(_atNight ? nightHome : dayHome, true, _atNight ? FaceAway() : FaceOut());
        }

        // In the cave it faces the opening; out in the swamp it turns its back to the cave.
        float FaceOut() { Vector3 d = nightHome - dayHome; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        float FaceAway() { return FaceOut(); }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
