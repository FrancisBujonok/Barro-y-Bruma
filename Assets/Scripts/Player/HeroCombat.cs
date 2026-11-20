using System;
using System.Collections.Generic;
using BarroYBruma.Combat;
using BarroYBruma.Player;
using BrumaPx.Combat;
using BrumaPx.Data;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// The hero's melee combat, real time and without magic. Z light attack (chains into a 3-hit combo),
    /// X heavy attack, C roll (invulnerable while it lasts), V guard (a press right before a blow is a parry).
    /// One stamina bar pays for all of it. Reuses ArcMath, AttackTimer and DodgeTimer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroCombat : MonoBehaviour
    {
        [Serializable]
        public sealed class AttackData
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

        public static HeroCombat Instance { get; private set; }

        [Header("Links")]
        public DioramaHero hero;
        public Health health;
        public Transform body;
        [Tooltip("Pivot that holds the arm and the sword; it swings during attacks.")]
        public Transform swordPivot;
        [Tooltip("The sword held in the hand (shown while fighting).")]
        public GameObject drawnSword;
        [Tooltip("The sword on the back (shown the rest of the time).")]
        public GameObject sheathedSword;


        [Header("Data")]
        [Tooltip("When set, its numbers replace the ones below at start. Edit the file to tune the combat.")]
        public HeroCombatProfile profile;

        [Header("Stamina")]
        [Min(1f)] public float maxStamina = 100f;
        public float staminaRegen = 30f;
        public float regenDelay = 0.7f;

        [Header("Attacks")]
        public AttackData lightAttack = new AttackData();
        public AttackData heavy = new AttackData
        {
            damage = 28f, startup = 0.32f, active = 0.12f, recovery = 0.45f,
            range = 1.7f, arcDegrees = 130f, stamina = 28f, lunge = 0.55f, noise = 8f, hitStop = 0.07f
        };
        [Tooltip("Seconds after a light attack in which the next press continues the combo.")]
        public float comboWindow = 0.45f;
        [Tooltip("Third blow of a combo: damage and recovery multipliers (light, heavy).")]
        public float finisherDamage = 1.5f;
        public float finisherRecovery = 1.5f;
        [Tooltip("Seconds to put the sword away or draw it (R).")]
        public float sheathTime = 0.45f;
        [Tooltip("A press during an attack is remembered this long.")]
        public float inputBuffer = 0.35f;

        [Header("Dodge")]
        public float dodgeDistance = 2.6f;
        public float dodgeDuration = 0.22f;
        public float dodgeCooldown = 0.45f;
        public float dodgeStamina = 22f;
        public float dodgeNoise = 4f;

        [Header("Guard")]
        [Tooltip("A guard pressed this recently before a blow is a parry.")]
        public float parryWindow = 0.2f;
        [Tooltip("Share of the damage that gets through a guard.")]
        [Range(0f, 1f)] public float guardDamageTaken = 0.3f;
        public float guardStaminaPerDamage = 1.2f;
        [Range(0f, 1f)] public float guardMoveSpeed = 0.45f;
        public float parryStaminaBack = 15f;

        [Header("Hurt")]
        public float hurtStun = 0.28f;
        public float respawnDelay = 2.4f;

        /// <summary>Short messages for the HUD ("Has caído").</summary>
        public event Action<string> Announce;

        /// <summary>The player tried to act without enough stamina.</summary>
        public event Action Exhausted;

        /// <summary>While true the hero ignores combat input (the bag or the notebook is open).</summary>
        public bool InputLocked { get; set; }

        /// <summary>True when the hero is free to use an item: not dead, swinging, rolling or stunned.</summary>
        public bool CanAct { get { return health != null && !health.IsDead && !_attack.IsBusy && !_dodge.IsDodging && _hurtTimer <= 0f && _sheathDir == 0; } }

        public void RestoreStamina(float amount)
        {
            _stamina = Mathf.Min(maxStamina, _stamina + amount);
        }

        /// <summary>Where the hero comes back after falling (a refuge sets it when the hero rests).</summary>
        public void SetCheckpoint(Vector3 position, Quaternion rotation)
        {
            _spawnPos = new Vector3(position.x, 0f, position.z);
            _spawnRot = rotation;
        }

        public float Stamina01 { get { return Mathf.Clamp01(_stamina / Mathf.Max(1f, maxStamina)); } }
        public bool IsGuarding { get { return _guarding; } }
        public bool IsDodging { get { return _dodge.IsDodging; } }
        public bool IsAttacking { get { return _attack.IsBusy; } }
        /// <summary>True while the attack in progress is a heavy one.</summary>
        public bool IsHeavyAttack { get { return _isHeavy; } }
        /// <summary>Place of the attack in progress inside its combo: 0, 1 or 2 (the finisher).</summary>
        public int ComboStep { get { return _combo; } }
        /// <summary>The sword is in its scabbard.</summary>
        public bool Sheathed { get; private set; }
        /// <summary>Putting the sword away (true) or drawing it (false); only meaningful while IsSheathing.</summary>
        public bool SheathingIn { get { return _sheathDir > 0; } }
        public bool IsSheathing { get { return _sheathDir != 0; } }
        public float SheathProgress { get { return Mathf.Clamp01(_sheathClock / Mathf.Max(0.01f, sheathTime)); } }
        /// <summary>0..1 through the roll (only while IsDodging).</summary>
        public float DodgeProgress { get { return Mathf.Clamp01(_dodgeClock / Mathf.Max(0.01f, dodgeDuration)); } }
        /// <summary>Hit and staggered (not a blocked blow).</summary>
        public bool IsHurt { get { return _hurtTimer > 0f && _hurtAnim; } }
        public float HurtProgress { get { return 1f - Mathf.Clamp01(_hurtTimer / Mathf.Max(0.01f, hurtStun)); } }

        /// <summary>
        /// Frame of an attack animation for the current moment of the swing. The clip has <paramref name="frames"/>
        /// frames; [hitFrom, hitTo) are the frames of the blow itself (the active window), the ones before cover
        /// the wind-up and the ones after the recovery.
        /// </summary>
        public int AttackFrame(int frames, int hitFrom, int hitTo)
        {
            if (!_attack.IsBusy || _current == null) return 0;
            hitFrom = Mathf.Clamp(hitFrom, 0, frames - 1);
            hitTo = Mathf.Clamp(hitTo, hitFrom + 1, frames);
            float s = Mathf.Max(0.01f, _current.startup * _startupMul), a = Mathf.Max(0.01f, _current.active), r = Mathf.Max(0.01f, _curRecovery);
            float t = _attackClock;
            int f;
            if (t < s) f = Mathf.FloorToInt(t / s * hitFrom);
            else if (t < s + a) f = hitFrom + Mathf.FloorToInt((t - s) / a * (hitTo - hitFrom));
            else f = hitTo + Mathf.FloorToInt((t - s - a) / r * (frames - hitTo));
            return Mathf.Clamp(f, 0, frames - 1);
        }

        /// <summary>True while a heavy blow is winding up (some enemies react to it).</summary>
        public bool IsHeavyWindup { get { return _attack.IsBusy && _isHeavy && _attack.Phase == AttackPhase.Startup; } }

        readonly AttackTimer _attack = new AttackTimer();
        readonly DodgeTimer _dodge = new DodgeTimer();
        readonly HashSet<Health> _hitSet = new HashSet<Health>();

        AttackData _current;
        HeroBody _body;
        bool _isHeavy, _guarding, _blocking;
        int _combo, _queued;
        float _stamina, _regenTimer, _attackClock, _curRecovery, _damageMul = 1f, _lungeLeft;
        float _comboTimer, _queueTimer, _hurtTimer, _guardTime, _dodgeClock, _hitStopLeft;
        float _deadClock, _graceTimer;
        float _sheathClock, _startupMul = 1f, _arcMul = 1f;
        int _sheathDir;
        bool _hurtAnim;

        Vector3 _knock, _spawnPos;
        Quaternion _spawnRot, _bodyRest = Quaternion.identity, _swordRest = Quaternion.identity;

        const float TargetRadius = 0.35f;

        void Awake()
        {
            Instance = this;
            ApplyProfile();
        }

        // Copies the numbers of the profile file over the inline ones.
        void ApplyProfile()
        {
            if (profile == null) return;
            maxStamina = profile.MaxStamina;
            staminaRegen = profile.StaminaRegen;
            regenDelay = profile.RegenDelay;
            Copy(profile.Light, lightAttack);
            Copy(profile.Heavy, heavy);
            comboWindow = profile.ComboWindow;
            inputBuffer = profile.InputBuffer;
            dodgeDistance = profile.DodgeDistance;
            dodgeDuration = profile.DodgeDuration;
            dodgeCooldown = profile.DodgeCooldown;
            dodgeStamina = profile.DodgeStamina;
            dodgeNoise = profile.DodgeNoise;
            parryWindow = profile.ParryWindow;
            guardDamageTaken = profile.GuardDamageTaken;
            guardStaminaPerDamage = profile.GuardStaminaPerDamage;
            guardMoveSpeed = profile.GuardMoveSpeed;
            parryStaminaBack = profile.ParryStaminaBack;
            hurtStun = profile.HurtStun;
            respawnDelay = profile.RespawnDelay;
        }

        void ApplyHealthFromProfile()
        {
            if (profile == null || health == null) return;
            health.max = profile.MaxHealth;
            health.ResetFull();
        }

        static void Copy(HeroAttackStats s, AttackData d)
        {
            d.damage = s.damage;
            d.startup = s.startup;
            d.active = s.active;
            d.recovery = s.recovery;
            d.range = s.range;
            d.arcDegrees = s.arcDegrees;
            d.stamina = s.stamina;
            d.lunge = s.lunge;
            d.noise = s.noise;
            d.hitStop = s.hitStop;
        }

        void OnEnable()
        {
            if (health == null) return;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
            Time.timeScale = 1f;
            if (hero != null)
            {
                hero.MovementLocked = false;
                hero.SpeedMultiplier = 1f;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _stamina = maxStamina;
            ApplyHealthFromProfile();
            _body = GetComponent<HeroBody>();
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
            if (body != null) _bodyRest = body.localRotation;
            if (swordPivot != null) _swordRest = swordPivot.localRotation;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f); // a long frame must not skip the active window of an attack
            TickHitStop();
            if (hero == null || health == null) return;

            if (health.IsDead)
            {
                TickDead();
                return;
            }

            _hurtTimer = Mathf.Max(0f, _hurtTimer - dt);
            _comboTimer = Mathf.Max(0f, _comboTimer - dt);
            _queueTimer = Mathf.Max(0f, _queueTimer - dt);
            if (_graceTimer > 0f)
            {
                _graceTimer -= dt;
                if (_graceTimer <= 0f) health.Invulnerable = false;
            }

            bool talking = hero.dialogue != null && hero.dialogue.IsOpen;
            if (talking) _guarding = false;
            else if (InputLocked) _guarding = false;
            else ReadInput();

            if (_guarding) _guardTime += dt;
            TickSheath(dt);

            TickAttack(dt);
            TickDodge(dt);
            TickKnock(dt);
            TickStamina(dt);

            hero.MovementLocked = _attack.IsBusy || _dodge.IsDodging || _hurtTimer > 0f || _sheathDir != 0 || InputLocked;
            hero.SpeedMultiplier = _guarding ? guardMoveSpeed : 1f;
            Animate();
        }

        // ------------------------------------------------------------------------------------------ input

        void ReadInput()
        {
            if (_hurtTimer > 0f) { _guarding = false; return; }

            bool lightPress = DioramaInput.LightPressed();
            bool heavyPress = DioramaInput.HeavyPressed();
            bool dodgePress = DioramaInput.DodgePressed();
            bool sheathPress = DioramaInput.SheathPressed();

            if (dodgePress) TryDodge();
            else if (lightPress) TryAttack(false);
            else if (heavyPress) TryAttack(true);
            else if (sheathPress) TrySheath();

            bool guardHeld = DioramaInput.GuardHeld();
            if (guardHeld && Sheathed && _sheathDir == 0 && CanAct) StartSheath(false);   // raising the guard draws the sword
            bool wantGuard = guardHeld && !Sheathed && _sheathDir == 0 && !_attack.IsBusy && !_dodge.IsDodging;
            if (wantGuard && !_guarding)
            {
                _guarding = true;
                _guardTime = 0f;
            }
            else if (!wantGuard)
            {
                _guarding = false;
            }
        }

        // ------------------------------------------------------------------------------------------ attacks

        void TryAttack(bool isHeavy)
        {
            if (_dodge.IsDodging) return;
            if (Sheathed || _sheathDir != 0)
            {
                // Draw first; the blow comes out as soon as the sword is in hand.
                if (Sheathed && _sheathDir == 0) StartSheath(false);
                else if (_sheathDir > 0) return;     // he is putting it away: ignore
                _queued = isHeavy ? 2 : 1;
                _queueTimer = sheathTime + inputBuffer;
                return;
            }
            if (_attack.IsBusy)
            {
                _queued = isHeavy ? 2 : 1;
                _queueTimer = inputBuffer;
                return;
            }
            StartAttack(isHeavy);
        }

        bool StartAttack(bool isHeavy)
        {
            AttackData a = isHeavy ? heavy : lightAttack;
            if (_stamina < a.stamina)
            {
                if (Exhausted != null) Exhausted();
                return false;
            }

            // Light and heavy blows each chain into a combo of three; switching kind starts over.
            int combo = _comboTimer > 0f && isHeavy == _isHeavy ? (_combo + 1) % 3 : 0;
            float recovery = a.recovery * (combo == 2 ? finisherRecovery : 1f) * M(BodyStat.AttackRecovery);
            // The heavy finisher is a leap: a longer wind-up. The second heavy blow sweeps wider.
            _startupMul = isHeavy && combo == 2 ? 1.35f : 1f;
            _arcMul = isHeavy && combo == 1 ? 1.35f : 1f;
            if (!_attack.Start(a.startup * _startupMul, a.active, recovery)) return false;

            _combo = combo;
            _current = a;
            _isHeavy = isHeavy;
            _curRecovery = recovery;
            _attackClock = 0f;
            _hitSet.Clear();
            _lungeLeft = a.lunge * (combo == 2 ? 1.8f : 1f);
            _damageMul = combo == 2 ? finisherDamage : (isHeavy && combo == 1 ? 1.15f : 1f);
            _stamina -= a.stamina;
            _regenTimer = regenDelay;
            _guarding = false;

            hero.FaceInput();
            hero.SnapFacing();
            AimAssist(a);
            NoiseBus.Emit(transform.position, a.noise);
            return true;
        }

        // The hero faces 8 directions only, so a swing leans towards the nearest enemy in front.
        void AimAssist(AttackData a)
        {
            Vector3 pos = transform.position;
            Vector3 f = transform.forward;
            f.y = 0f;
            float best = float.MaxValue;
            Health target = null;
            for (int i = 0; i < Health.All.Count; i++)
            {
                Health h = Health.All[i];
                if (h == health || h.team == health.team || h.IsDead) continue;
                Vector3 d = h.transform.position - pos;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > a.range + 1f || Vector3.Angle(f, d) > 75f) continue;
                if (dist < best) { best = dist; target = h; }
            }
            if (target == null) return;
            hero.FaceTowards(target.transform.position);
            hero.SnapFacing();
        }

        void TickAttack(float dt)
        {
            if (!_attack.IsBusy)
            {
                if (_queued != 0)
                {
                    bool h = _queued == 2;
                    _queued = 0;
                    if (_sheathDir != 0 || Sheathed)      // still drawing: keep the blow waiting while the buffer lasts
                    {
                        if (_queueTimer > 0f) _queued = h ? 2 : 1;
                        return;
                    }
                    if (_queueTimer > 0f && _hurtTimer <= 0f && !_dodge.IsDodging) StartAttack(h);
                }
                if (!_attack.IsBusy) return;
            }

            _attackClock += dt;
            _attack.Tick(dt);

            if (_attack.Phase == AttackPhase.Active) HitCheck();

            if (_lungeLeft > 0f && (_attack.Phase == AttackPhase.Startup || _attack.Phase == AttackPhase.Active))
            {
                float total = Mathf.Max(0.01f, _current.startup * _startupMul + _current.active);
                float step = Mathf.Min(_lungeLeft, _current.lunge * dt / total);
                _lungeLeft -= step;
                MoveBy(transform.forward * step);
            }

            if (!_attack.IsBusy) _comboTimer = _combo == 2 ? 0f : comboWindow;
        }

        void HitCheck()
        {
            Vector3 o3 = transform.position, f3 = transform.forward;
            Vector2 origin = new Vector2(o3.x, o3.z);
            Vector2 facing = new Vector2(f3.x, f3.z);
            for (int i = 0; i < Health.All.Count; i++)
            {
                Health h = Health.All[i];
                if (h == health || h.team == health.team || h.IsDead || _hitSet.Contains(h)) continue;
                Vector3 t3 = h.transform.position;
                Vector2 target = new Vector2(t3.x, t3.z);
                if (!ArcMath.IsInArc(origin, facing, target, _current.range + TargetRadius, Mathf.Min(359f, _current.arcDegrees * _arcMul))) continue;

                _hitSet.Add(h);
                Vector2 dir = target - origin;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : facing.normalized;
                h.TakeDamage(_current.damage * _damageMul * M(BodyStat.MeleeDamage) * (_isHeavy ? M(BodyStat.HeavyDamage) : 1f), dir);
                StartHitStop(_current.hitStop);
            }
        }

        // ------------------------------------------------------------------------------------------ dodge and guard

        void TryDodge()
        {
            float dodgeCost = dodgeStamina * M(BodyStat.DodgeCost);
            if (!_dodge.IsReady || _stamina < dodgeCost || _hurtTimer > 0f)
            {
                if (_stamina < dodgeCost && Exhausted != null) Exhausted();
                return;
            }
            if (_attack.IsBusy && _attack.Phase != AttackPhase.Recovery) return;

            Vector3 dir = hero.InputDirection();
            bool hasInput = dir.sqrMagnitude > 0.01f;
            if (!hasInput) dir = -transform.forward;
            if (!_dodge.TryStart(new Vector2(dir.x, dir.z), dodgeDistance * M(BodyStat.DodgeDistance), dodgeDuration, dodgeCooldown)) return;

            _attack.Cancel();
            _queued = 0;
            _comboTimer = 0f;
            _guarding = false;
            _sheathDir = 0;
            _stamina -= dodgeCost;
            _regenTimer = regenDelay;
            _dodgeClock = 0f;
            if (hasInput)
            {
                hero.FaceInput();
                hero.SnapFacing();
            }
            NoiseBus.Emit(transform.position, dodgeNoise);
        }

        void TickDodge(float dt)
        {
            if (_dodge.IsDodging) _dodgeClock += dt;
            Vector2 v = _dodge.Step(dt);
            if (v.sqrMagnitude > 0f) MoveBy(new Vector3(v.x, 0f, v.y) * dt);
        }

        /// <summary>Called by an enemy whose blow lands. Applies rolling, parry and guard before the damage.</summary>
        public void ReceiveAttack(EnemyBrain attacker, float damage, Vector2 dir)
        {
            if (health == null || health.IsDead || health.Invulnerable) return;
            if (_dodge.IsDodging) return;

            Vector3 f3 = transform.forward;
            Vector2 facing = new Vector2(f3.x, f3.z).normalized;
            bool facingAttacker = Vector2.Dot(facing, -dir) > 0.2f;

            if (_guarding && facingAttacker)
            {
                if (_guardTime <= parryWindow * M(BodyStat.ParryWindow))
                {
                    _stamina = Mathf.Min(maxStamina, _stamina + parryStaminaBack);
                    if (attacker != null) attacker.Parried();
                    NoiseBus.Emit(transform.position, 7f);
                    StartHitStop(0.08f);
                    return;
                }

                _stamina -= damage * guardStaminaPerDamage;
                _regenTimer = regenDelay;
                if (_stamina <= 0f)
                {
                    // Guard broken: the hero is thrown off balance and takes no damage this time.
                    _stamina = 0f;
                    _guarding = false;
                    _hurtTimer = 0.5f;
                    _attack.Cancel();
                    StartHitStop(0.06f);
                    return;
                }

                _blocking = true;
                health.TakeDamage(damage * Armor() * Mathf.Clamp01(guardDamageTaken * M(BodyStat.GuardLeak)), dir);
                _blocking = false;
                NotifyBody(attacker, true);
                return;
            }

            health.TakeDamage(damage * Armor(), dir);
            NotifyBody(attacker, false);
        }

        // ------------------------------------------------------------------------------------------ reactions

        // What the body has lost to the Bruma changes the numbers of the fight (1 = unchanged).
        float M(BodyStat stat)
        {
            return _body != null ? _body.Mult(stat) : 1f;
        }

        // What the worn armour lets through of a blow (1 = all of it).
        float Armor()
        {
            return _body != null ? _body.BlowTaken : 1f;
        }

        // A blow that landed: the body opens a wound and takes the Bruma the attacker carries.
        void NotifyBody(EnemyBrain attacker, bool guarded)
        {
            if (_body == null) return;
            float dose = attacker != null && attacker.profile != null ? attacker.profile.BrumaOnHit : 0f;
            _body.OnHitTaken(dose, guarded);
        }

        void OnDamaged(float amount, Vector2 dir)
        {
            if (_blocking)
            {
                _hurtTimer = 0.1f;
                _hurtAnim = false;
                _knock = new Vector3(dir.x, 0f, dir.y) * 1.5f;
                return;
            }
            _hurtTimer = hurtStun;
            _hurtAnim = true;
            _attack.Cancel();
            _queued = 0;
            _guarding = false;
            _comboTimer = 0f;
            _sheathDir = 0;
            _knock = new Vector3(dir.x, 0f, dir.y) * Mathf.Clamp(amount / 6f, 1.5f, 4f);
            StartHitStop(0.05f);
        }

        void OnDied()
        {
            _deadClock = 0f;
            _attack.Cancel();
            _queued = 0;
            _guarding = false;
            hero.MovementLocked = true;
            if (Announce != null) Announce("Has caído");
        }

        void TickDead()
        {
            _deadClock += Time.unscaledDeltaTime;
            if (body != null)
            {
                float steps = Mathf.Floor(Mathf.Clamp01(_deadClock / 0.4f) * 4f) / 4f;
                body.localRotation = _bodyRest * Quaternion.Euler(-90f * steps, 0f, 0f);
            }
            if (_deadClock >= respawnDelay) Respawn();
        }

        void Respawn()
        {
            transform.SetPositionAndRotation(_spawnPos, _spawnRot);
            health.ResetFull();
            health.Invulnerable = true;
            _graceTimer = 2f;
            _stamina = maxStamina;
            _knock = Vector3.zero;
            _hurtTimer = 0f;
            _sheathDir = 0;
            _deadClock = 0f;
            hero.MovementLocked = false;
            hero.SpeedMultiplier = 1f;
            if (body != null) body.localRotation = _bodyRest;
            for (int i = 0; i < EnemyBrain.All.Count; i++) EnemyBrain.All[i].ResetToHome();
            if (Announce != null) Announce(string.Empty);
        }

        void TickKnock(float dt)
        {
            if (_knock.sqrMagnitude < 0.0004f) return;
            MoveBy(_knock * dt);
            _knock = Vector3.MoveTowards(_knock, Vector3.zero, 14f * dt);
        }

        void TickStamina(float dt)
        {
            _regenTimer -= dt;
            if (_regenTimer > 0f || _guarding || _attack.IsBusy) return;
            _stamina = Mathf.Min(maxStamina, _stamina + staminaRegen * M(BodyStat.StaminaRegen) * dt);
        }

        void MoveBy(Vector3 delta)
        {
            Vector3 p = transform.position + delta;
            Vector2 g = hero.ResolveGround(new Vector2(p.x, p.z), hero.radius);
            transform.position = new Vector3(g.x, 0f, g.y);
        }

        // ------------------------------------------------------------------------------------------ hit stop

        void StartHitStop(float seconds)
        {
            if (seconds <= 0f) return;
            _hitStopLeft = Mathf.Max(_hitStopLeft, seconds);
            Time.timeScale = 0.05f;
        }

        void TickHitStop()
        {
            if (_hitStopLeft <= 0f) return;
            _hitStopLeft -= Time.unscaledDeltaTime;
            if (_hitStopLeft <= 0f) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------------------------------ animation

        static float Snap(float degrees)
        {
            return Mathf.Round(degrees / 15f) * 15f;
        }

        static float Ease(float k)
        {
            return k * k * (3f - 2f * k);
        }

        // ------------------------------------------------------------------------------------------ sheath

        void TrySheath()
        {
            if (!CanAct || _guarding) return;
            StartSheath(!Sheathed);
        }

        void StartSheath(bool putAway)
        {
            _sheathDir = putAway ? 1 : -1;
            _sheathClock = 0f;
            _comboTimer = 0f;
            _guarding = false;
        }

        void TickSheath(float dt)
        {
            if (_sheathDir == 0) return;
            _sheathClock += dt;
            if (_sheathClock < sheathTime) return;
            Sheathed = _sheathDir > 0;
            _sheathDir = 0;
        }

        void Animate()
        {
            bool drawn = !Sheathed;
            if (drawnSword != null && drawnSword.activeSelf != drawn) drawnSword.SetActive(drawn);
            if (sheathedSword != null && sheathedSword.activeSelf == drawn) sheathedSword.SetActive(!drawn);

            if (swordPivot != null)
            {
                Vector3 e = Vector3.zero;
                if (_attack.IsBusy) e = AttackPose();
                else if (_guarding) e = new Vector3(55f, -50f, 0f);
                else if (_hurtTimer > 0f) e = new Vector3(-20f, 0f, 0f);
                swordPivot.localRotation = _swordRest * Quaternion.Euler(Snap(e.x), Snap(e.y), 0f);
            }

            if (body != null)
            {
                if (_dodge.IsDodging)
                {
                    float k = Mathf.Clamp01(_dodgeClock / dodgeDuration);
                    body.localRotation = _bodyRest * Quaternion.Euler(Snap(65f * Mathf.Sin(Mathf.PI * k)), 0f, 0f);
                }
                else if (_hurtTimer > 0f)
                {
                    body.localRotation = _bodyRest * Quaternion.Euler(-15f, 0f, 0f);
                }
                else
                {
                    body.localRotation = _bodyRest;
                }
            }
        }

        // Pitch (x) and yaw (y) offsets of the sword over the swing: wind up, strike, come back.
        Vector3 AttackPose()
        {
            float s = _current.startup, a = _current.active, r = _curRecovery, t = _attackClock;
            Vector3 wind, strike;
            if (_isHeavy) { wind = new Vector3(-100f, 20f, 0f); strike = new Vector3(95f, -10f, 0f); }
            else if (_combo == 0) { wind = new Vector3(30f, 85f, 0f); strike = new Vector3(45f, -85f, 0f); }
            else if (_combo == 1) { wind = new Vector3(30f, -85f, 0f); strike = new Vector3(45f, 85f, 0f); }
            else { wind = new Vector3(-80f, 0f, 0f); strike = new Vector3(85f, 0f, 0f); }

            if (t < s) return Vector3.Lerp(Vector3.zero, wind, Ease(Mathf.Clamp01(t / Mathf.Max(0.01f, s))));
            if (t < s + a) return Vector3.Lerp(wind, strike, Mathf.Clamp01((t - s) / Mathf.Max(0.01f, a)));
            return Vector3.Lerp(strike, Vector3.zero, Ease(Mathf.Clamp01((t - s - a) / Mathf.Max(0.01f, r))));
        }
    }
}
