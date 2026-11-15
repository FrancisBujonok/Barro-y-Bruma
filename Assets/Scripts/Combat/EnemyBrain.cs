using System.Collections.Generic;
using BarroYBruma.Combat;
using BrumaPx.Player;
using BrumaPx.World;
using UnityEngine;

namespace BrumaPx.Combat
{
    /// <summary>
    /// One enemy. It notices the hero by sight (shorter when the hero is in the dark, narrower outside its field of
    /// view) or by sound (steps, rolls and swings carry), chases, attacks, loses track and goes home.
    /// States: Idle, Investigate (heard something), Chase, Attack, Stagger (heavy hit or parry), Search, Dead.
    /// All numbers come from the EnemyProfile.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed partial class EnemyBrain : MonoBehaviour
    {
        public static readonly List<EnemyBrain> All = new List<EnemyBrain>();

        public enum State { Idle, Investigate, Chase, Attack, Stagger, Search, Dead }

        [Header("Data")]
        public EnemyProfile profile;

        [Header("Rig")]
        public Transform body;
        public Transform legL;
        public Transform legR;
        [Tooltip("Pivot that holds the arms or the weapon; swings during an attack.")]
        public Transform arms;
        public int pixelsPerUnit = 20;
        public float stepsPerSecond = 5f;

        public State Current { get { return _state; } }

        State _state = State.Idle;
        Health _health;
        DioramaHero _hero;
        Health _heroHealth;
        HeroVisibility _heroVis;
        HeroCombat _heroCombat;

        Vector3 _home, _lastKnown, _wanderPoint, _knock, _sidestepDir;
        float _homeYaw, _targetYaw, _stateTime, _lostTime, _clock, _wanderTimer;
        float _staggerLeft, _sidestepLeft, _deadClock, _deadYaw, _attackClock, _searchYaw;
        bool _hasWander, _attackHit, _reacted, _moving;

        readonly AttackTimer _attack = new AttackTimer();

        Vector3 _bodyPos, _legLPos, _legRPos;
        Quaternion _bodyRot = Quaternion.identity;
        Quaternion _armsRot = Quaternion.identity;

        // ------------------------------------------------------------------------------------------ lifecycle

        void Awake()
        {
            _health = GetComponent<Health>();
            _home = transform.position;
            _homeYaw = transform.eulerAngles.y;
            _targetYaw = _homeYaw;
            if (body != null) { _bodyPos = body.localPosition; _bodyRot = body.localRotation; }
            if (legL != null) _legLPos = legL.localPosition;
            if (legR != null) _legRPos = legR.localPosition;
            if (arms != null) _armsRot = arms.localRotation;
            _wanderTimer = Random.Range(1f, 4f);
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        void OnEnable()
        {
            if (_state != State.Dead && !All.Contains(this)) All.Add(this);
            NoiseBus.Noise += OnNoise;
        }

        void OnDisable()
        {
            All.Remove(this);
            NoiseBus.Noise -= OnNoise;
        }

        void OnDestroy()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        void Start()
        {
            if (profile != null)
            {
                _health.max = profile.MaxHealth;
                _health.ResetFull();
            }
            _hero = FindFirstObjectByType<DioramaHero>();
            if (_hero != null)
            {
                _heroHealth = _hero.GetComponent<Health>();
                _heroVis = _hero.GetComponent<HeroVisibility>();
                _heroCombat = _hero.GetComponent<HeroCombat>();
            }
        }

        void Update()
        {
            if (profile == null || Dormant) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f); // a long frame must not skip the active window of an attack

            if (_state == State.Dead) { TickDead(dt); return; }

            _moving = false;
            if (_hero != null)
            {
                _stateTime += dt;
                bool heroDead = _heroHealth != null && _heroHealth.IsDead;
                if (heroDead && _state != State.Idle && _state != State.Attack && _state != State.Stagger) EnterState(State.Idle);

                switch (_state)
                {
                    case State.Idle: TickIdle(dt); break;
                    case State.Investigate: TickInvestigate(dt); break;
                    case State.Chase: TickChase(dt); break;
                    case State.Attack: TickAttack(dt); break;
                    case State.Stagger: TickStagger(dt); break;
                    case State.Search: TickSearch(dt); break;
                }
            }

            ApplyKnock(dt);
            TickTurn(dt);
            Animate(dt);
        }

        // ------------------------------------------------------------------------------------------ perception

        bool CanSeeHero(bool aware)
        {
            if (_hero == null || (_heroHealth != null && _heroHealth.IsDead)) return false;
            Vector3 to = Flat(_hero.transform.position - transform.position);
            float dist = to.magnitude;
            float light = _heroVis != null ? _heroVis.Level : 1f;
            float range = profile.SightRange * Mathf.Lerp(profile.MinLightFactor, 1f, light);
            if (aware) range *= 1.25f;
            if (dist > range) return false;
            if (aware || dist < 1.4f) return true;
            return Vector3.Angle(transform.forward, to) <= profile.FieldOfView * 0.5f;
        }

        void OnNoise(Vector3 pos, float radius)
        {
            if (Dormant || profile == null || _state == State.Dead || _state == State.Attack || _state == State.Stagger || _state == State.Chase) return;
            if (_heroHealth != null && _heroHealth.IsDead) return;
            Vector3 d = Flat(pos - transform.position);
            if (d.sqrMagnitude < 0.04f) return;
            if (d.magnitude > radius * profile.HearingMultiplier) return;
            _lastKnown = pos;
            if (_state != State.Investigate) EnterState(State.Investigate);
            else _stateTime = 0f;
        }

        // ------------------------------------------------------------------------------------------ states

        void EnterState(State s)
        {
            _state = s;
            _stateTime = 0f;
            if (s == State.Search) _searchYaw = _targetYaw;
            if (s == State.Idle)
            {
                _wanderPoint = _home;
                _hasWander = true;
                _wanderTimer = 1.5f;
            }
        }

        void Alert()
        {
            _lastKnown = _hero.transform.position;
            _lostTime = 0f;
            EnterState(State.Chase);
            RallyPack();
        }

        void TickIdle(float dt)
        {
            if (CanSeeHero(false)) { Alert(); return; }

            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = Random.Range(2.5f, 6f);
                Vector2 r = Random.insideUnitCircle * 2.5f;
                _wanderPoint = _home + new Vector3(r.x, 0f, r.y);
                _hasWander = true;
            }
            if (_hasWander)
            {
                MoveTowards(_wanderPoint, profile.WalkSpeed, dt);
                if (Flat(_wanderPoint - transform.position).magnitude < 0.25f) _hasWander = false;
            }
        }

        void TickInvestigate(float dt)
        {
            if (CanSeeHero(false)) { Alert(); return; }
            MoveTowards(_lastKnown, profile.WalkSpeed * 1.6f, dt);
            if (Flat(_lastKnown - transform.position).magnitude < 0.6f || _stateTime > 8f) EnterState(State.Search);
        }

        void TickSearch(float dt)
        {
            if (CanSeeHero(false)) { Alert(); return; }
            int look = (int)(_stateTime / 0.7f);
            _targetYaw = _searchYaw + ((look & 1) == 0 ? 45f : -45f);
            if (_stateTime > 3f) EnterState(State.Idle);
        }

        void TickChase(float dt)
        {
            bool sees = CanSeeHero(true);
            if (sees)
            {
                _lastKnown = _hero.transform.position;
                _lostTime = 0f;
            }
            else
            {
                _lostTime += dt;
                if (_lostTime > profile.LoseTrackSeconds) { EnterState(State.Search); return; }
            }

            Vector3 toHero = Flat(_hero.transform.position - transform.position);
            float dist = toHero.magnitude;

            ReactToHeavy(dist);
            if (_sidestepLeft > 0f)
            {
                float step = Mathf.Min(_sidestepLeft, profile.SidestepDistance * dt / 0.18f);
                _sidestepLeft -= step;
                Step(_sidestepDir * step);
                _moving = true;
                return;
            }

            if (sees && TryKeepDistance(toHero, dist, dt)) return;

            if (sees && dist <= profile.AttackRange * 0.9f) { StartAttack(toHero); return; }

            MoveTowards(_lastKnown, profile.ChaseSpeed * LungeFactor(dt), dt);
            if (!sees && Flat(_lastKnown - transform.position).magnitude < 0.5f) EnterState(State.Search);
        }

        // Some enemies (the raider) try to step out of the way when the hero winds up a heavy blow.
        void ReactToHeavy(float dist)
        {
            if (!profile.CanSidestep || _heroCombat == null) return;
            bool windup = _heroCombat.IsHeavyWindup && dist < 2.8f;
            if (!windup) { _reacted = false; return; }
            if (_reacted || _sidestepLeft > 0f) return;
            _reacted = true;
            if (Random.value > profile.SidestepChance) return;
            Vector3 to = Flat(_hero.transform.position - transform.position).normalized;
            _sidestepDir = Vector3.Cross(Vector3.up, to) * (Random.value < 0.5f ? 1f : -1f);
            _sidestepLeft = profile.SidestepDistance;
        }

        void StartAttack(Vector3 toHero)
        {
            FaceDir(toHero);
            transform.rotation = Quaternion.Euler(0f, _targetYaw, 0f);
            _attackHit = false;
            _attackClock = 0f;
            _attack.Cancel();
            _attack.Start(profile.AttackStartup, profile.AttackActive, profile.AttackRecovery);
            EnterState(State.Attack);
        }

        void TickAttack(float dt)
        {
            _attackClock += dt;
            _attack.Tick(dt);
            if (_attack.Phase == AttackPhase.Active && !_attackHit)
            {
                _attackHit = true;
                DoHit();
            }
            if (!_attack.IsBusy)
            {
                _lostTime = 0f;
                _lastKnown = _hero.transform.position;
                EnterState(State.Chase);
            }
        }

        void DoHit()
        {
            NoiseBus.Emit(transform.position, profile.AttackNoise);
            if (profile.ProjectileSpeed > 0f)
            {
                FireBolt();
                return;
            }
            Vector3 o3 = transform.position, t3 = _hero.transform.position, f3 = transform.forward;
            Vector2 origin = new Vector2(o3.x, o3.z);
            Vector2 target = new Vector2(t3.x, t3.z);
            if (!ArcMath.IsInArc(origin, new Vector2(f3.x, f3.z), target, profile.AttackRange + 0.35f, profile.AttackArcDegrees)) return;

            Vector2 dir = target - origin;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : new Vector2(f3.x, f3.z).normalized;
            if (_heroCombat != null) _heroCombat.ReceiveAttack(this, profile.AttackDamage, dir);
            else if (_heroHealth != null) _heroHealth.TakeDamage(profile.AttackDamage, dir);
        }

        void EnterStagger(float seconds)
        {
            _attack.Cancel();
            _staggerLeft = seconds;
            EnterState(State.Stagger);
        }

        void TickStagger(float dt)
        {
            _staggerLeft -= dt;
            if (_staggerLeft > 0f) return;
            _lostTime = 0f;
            _lastKnown = _hero.transform.position;
            EnterState(State.Chase);
        }

        /// <summary>The hero parried this enemy's blow: it reels for longer than from a heavy hit.</summary>
        public void Parried()
        {
            if (_state == State.Dead || profile == null) return;
            EnterStagger(profile.StaggerSeconds * 1.8f);
        }

        /// <summary>Back to the starting place and full health (the hero respawned).</summary>
        public void ResetToHome()
        {
            if (_state == State.Dead || _health == null) return;
            _attack.Cancel();
            _knock = Vector3.zero;
            _sidestepLeft = 0f;
            transform.position = _home;
            _health.ResetFull();
            EnterState(State.Idle);
            _hasWander = false;
            RaiseRespawned();
        }

        void OnDamaged(float amount, Vector2 dir)
        {
            if (profile == null || _state == State.Dead) return;
            _knock = new Vector3(dir.x, 0f, dir.y) * Mathf.Clamp(amount / 5f, 1.2f, 6f) * profile.KnockbackScale;
            if (amount >= profile.StaggerThreshold)
            {
                EnterStagger(profile.StaggerSeconds);
            }
            else if (_state == State.Idle || _state == State.Investigate || _state == State.Search)
            {
                if (_hero != null) Alert();
            }
        }

        void OnDied()
        {
            _state = State.Dead;
            _deadClock = 0f;
            _deadYaw = transform.eulerAngles.y;
            _attack.Cancel();
            All.Remove(this);
            Destroy(gameObject, 60f);
        }

        void TickDead(float dt)
        {
            _deadClock += dt;
            float steps = Mathf.Floor(Mathf.Clamp01(_deadClock / 0.4f) * 4f) / 4f;
            transform.rotation = Quaternion.Euler(0f, _deadYaw, 0f) * Quaternion.Euler(-90f * steps, 0f, 0f);
        }

        // ------------------------------------------------------------------------------------------ movement

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        void MoveTowards(Vector3 target, float speed, float dt)
        {
            Vector3 to = Flat(target - transform.position);
            float d = to.magnitude;
            if (d < 0.05f) return;
            Vector3 dir = to / d;
            Step(dir * Mathf.Min(speed * dt, d));
            FaceDir(dir);
            _moving = true;
        }

        void Step(Vector3 delta)
        {
            Vector3 pos = transform.position + delta;
            Vector2 p = new Vector2(pos.x, pos.z);
            for (int pass = 0; pass < 2; pass++)
            {
                var obstacles = WorldObstacle.All;
                for (int i = 0; i < obstacles.Count; i++) p = obstacles[i].Resolve(p, profile.Radius);
            }
            p = MapGrid.Resolve(p, profile.Radius);
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] == this) continue;
                p = PushApart(p, All[i].transform.position, 0.7f);
            }
            if (_hero != null) p = PushApart(p, _hero.transform.position, 0.65f);
            transform.position = new Vector3(p.x, 0f, p.y);
        }

        static Vector2 PushApart(Vector2 p, Vector3 other, float minDist)
        {
            Vector2 d = p - new Vector2(other.x, other.z);
            float m = d.magnitude;
            if (m >= minDist || m < 0.0001f) return p;
            return p + d / m * (minDist - m);
        }

        void ApplyKnock(float dt)
        {
            if (_knock.sqrMagnitude < 0.0004f) return;
            Step(_knock * dt);
            _knock = Vector3.MoveTowards(_knock, Vector3.zero, 14f * dt);
        }

        void FaceDir(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            _targetYaw = Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / 45f) * 45f;
        }

        void TickTurn(float dt)
        {
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, _targetYaw, 720f * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ------------------------------------------------------------------------------------------ animation

        static float Snap(float degrees)
        {
            return Mathf.Round(degrees / 15f) * 15f;
        }

        // Everything moves in whole render pixels, like the hero.
        void Animate(float dt)
        {
            float texel = 1f / (pixelsPerUnit * 0.8660254f);
            Vector3 bodyPos = _bodyPos;

            if (_moving)
            {
                _clock += dt * stepsPerSecond;
                int step = (int)_clock;
                bool a = (step & 1) == 0;
                if (legL != null) legL.localPosition = _legLPos + new Vector3(0f, a ? 0f : 0.07f, a ? 0.12f : -0.12f);
                if (legR != null) legR.localPosition = _legRPos + new Vector3(0f, a ? 0.07f : 0f, a ? -0.12f : 0.12f);
                bodyPos += Vector3.up * ((step & 1) * texel);
            }
            else
            {
                _clock = 0f;
                if (legL != null) legL.localPosition = _legLPos;
                if (legR != null) legR.localPosition = _legRPos;
                bodyPos += Vector3.up * (((int)(Time.time * 1.6f) & 1) * texel);
            }

            float lean = 0f, armAngle = 0f;
            if (_state == State.Attack && _attack.IsBusy)
            {
                float s = profile.AttackStartup, a = profile.AttackActive, r = profile.AttackRecovery;
                float t = _attackClock;
                if (t < s)
                {
                    float k = Mathf.Clamp01(t / Mathf.Max(0.01f, s));
                    lean = Mathf.Lerp(0f, -15f, k);
                    armAngle = Mathf.Lerp(0f, 140f, k);
                }
                else if (t < s + a)
                {
                    float k = Mathf.Clamp01((t - s) / Mathf.Max(0.01f, a));
                    lean = Mathf.Lerp(-15f, 30f, k);
                    armAngle = Mathf.Lerp(140f, -50f, k);
                }
                else
                {
                    float k = Mathf.Clamp01((t - s - a) / Mathf.Max(0.01f, r));
                    lean = Mathf.Lerp(30f, 0f, k);
                    armAngle = Mathf.Lerp(-50f, 0f, k);
                }
            }
            else if (_state == State.Stagger)
            {
                lean = -20f;
                armAngle = 40f;
            }

            if (profile.ProjectileSpeed > 0f && _state == State.Attack && _attack.IsBusy) AimPose(ref lean, ref armAngle);

            if (body != null)
            {
                body.localPosition = bodyPos;
                body.localRotation = _bodyRot * Quaternion.Euler(Snap(lean), 0f, 0f);
            }
            if (arms != null) arms.localRotation = _armsRot * Quaternion.Euler(Snap(armAngle), 0f, 0f);
        }
    }
}
