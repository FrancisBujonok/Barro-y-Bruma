using BrumaPx.Player;
using UnityEngine;

namespace BrumaPx.Combat
{
    /// <summary>
    /// Second half of EnemyBrain: the behaviours that only some enemies have, all switched on by numbers in the
    /// EnemyProfile (pack alert, shooting from a distance and keeping away, the lunge of an ambusher). An enemy whose
    /// profile leaves them at zero behaves exactly as before.
    /// </summary>
    public sealed partial class EnemyBrain
    {
        [Header("Ranged")]
        [Tooltip("Look of the bolt it shoots (only for profiles with a projectile speed).")]
        public Material projectileMaterial;

        /// <summary>True while an ambusher is waiting in hiding: it does not think, hear or move.</summary>
        public bool Dormant;

        /// <summary>Raised when the enemy is put back at its starting place (the hero respawned).</summary>
        public event System.Action Respawned;

        float _lungeLeft;

        /// <summary>The ambusher springs: it is awake and chasing, and runs faster for a moment.</summary>
        public void Ambush()
        {
            if (_hero == null || _state == State.Dead || profile == null) return;
            Dormant = false;
            _lungeLeft = profile.LungeSeconds;
            Alert();
        }

        float LungeFactor(float dt)
        {
            if (_lungeLeft <= 0f) return 1f;
            _lungeLeft -= dt;
            return profile.LungeSpeedFactor;
        }

        // The first of a pack to spot the hero calls the others of its kind that are close and not yet on him.
        void RallyPack()
        {
            if (profile.PackRadius <= 0f || _hero == null) return;
            for (int i = 0; i < All.Count; i++)
            {
                EnemyBrain o = All[i];
                if (o == this || o.Dormant || o.profile != profile) continue;
                if (o._state != State.Idle && o._state != State.Investigate && o._state != State.Search) continue;
                if (Flat(o.transform.position - transform.position).magnitude > profile.PackRadius) continue;
                o.JoinChase(_hero.transform.position);
            }
        }

        void JoinChase(Vector3 where)
        {
            if (_hero == null || _state == State.Dead) return;
            _lastKnown = where;
            _lostTime = 0f;
            EnterState(State.Chase);
        }

        void RaiseRespawned()
        {
            if (Respawned != null) Respawned();
        }

        // A ranged enemy backs away when the hero gets close. If it cannot (it is cornered) it shoots anyway.
        bool TryKeepDistance(Vector3 toHero, float dist, float dt)
        {
            if (profile.KeepDistance <= 0f || dist >= profile.KeepDistance || toHero.sqrMagnitude < 0.0001f) return false;
            Vector3 before = transform.position;
            float intended = profile.ChaseSpeed * 0.9f * dt;
            Step(-toHero.normalized * intended);
            float moved = Flat(transform.position - before).magnitude;
            FaceDir(toHero);
            _moving = true;
            return moved > intended * 0.3f;
        }

        // The shot: a bolt that flies straight at where the hero is now. Rolling through it, guarding or parrying work.
        void FireBolt()
        {
            if (_hero == null) return;
            Vector3 dir = Flat(_hero.transform.position - transform.position);
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Bolt";
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = transform.position + Vector3.up * 1.05f + dir * 0.7f;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            go.transform.localScale = new Vector3(0.12f, 0.12f, 0.75f);
            if (projectileMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = projectileMaterial;
            go.AddComponent<EnemyBolt>().Launch(this, dir, profile.ProjectileSpeed, profile.AttackDamage, _heroCombat, _hero.transform, profile.AttackRange + 3f);
        }

        // While it aims, the shooter raises the weapon arm straight ahead; the recoil is a small kick.
        void AimPose(ref float lean, ref float armAngle)
        {
            float s = profile.AttackStartup, a = profile.AttackActive, r = profile.AttackRecovery, t = _attackClock;
            float raise = Mathf.Clamp01(t / Mathf.Max(0.01f, s * 0.6f));
            float lower = t > s + a ? Mathf.Clamp01((t - s - a) / Mathf.Max(0.01f, r)) : 0f;
            armAngle = Mathf.Lerp(0f, -85f, raise) * (1f - lower);
            lean = t >= s && t < s + 0.2f ? -8f : 0f;
        }
    }
}
