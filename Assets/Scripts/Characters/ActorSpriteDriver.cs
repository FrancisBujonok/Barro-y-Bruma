using BrumaPx.Atmosphere;
using BrumaPx.Combat;
using BrumaPx.Player;
using BrumaPx.World;
using UnityEngine;

namespace BrumaPx.Characters
{
    /// <summary>
    /// Shows an NPC or a boss as a sprite made in Blender. It hides the old primitives (they keep moving, nobody
    /// draws them) and picks view and clip from what already exists: the yaw of the object, EnemyBrain and Health
    /// for a boss, the hero's position for an NPC. MapWorld attaches it when it spawns the marker.
    /// Boss clips: idle, crawl, bite, death and, below <see cref="ActorSpriteConfig.phase2Below"/>, p2idle, crawl2,
    /// bite2. The bite follows the attack timer of the EnemyProfile frame by frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorSpriteDriver : MonoBehaviour
    {
        public enum Mode { Npc, Boss, Enemy }

        public ActorSpriteConfig config;
        public Mode mode;

        DioramaSprite _sprite;
        MeshRenderer _spriteRenderer;
        EnemyBrain _brain;
        EnemyTraits _traits;
        Health _health;
        float _speedNow;
        bool _wasHidden;
        Transform _hero;
        Camera _camera;
        Vector3 _lastPos;
        float _movingFor, _attackStart;
        bool _wasAttacking, _moving;
        bool _greeting, _ambientPlaying;
        float _greetTime, _ambientIn;

        public bool PhaseTwo
        {
            get { return _health != null && !_health.IsDead && _health.Current < _health.max * config.phase2Below; }
        }

        /// <summary>Gives the object the sprite called <paramref name="id"/> (Resources/Actors/id). Null if there is none.</summary>
        public static ActorSpriteDriver Attach(GameObject go, string id, Mode mode)
        {
            if (go == null) return null;
            var cfg = ActorSpriteConfig.Load(id);
            if (cfg == null || cfg.set == null || cfg.material == null) return null;
            var d = go.GetComponent<ActorSpriteDriver>();
            if (d == null) d = go.AddComponent<ActorSpriteDriver>();
            d.config = cfg;
            d.mode = mode;
            return d;
        }

        void Start()
        {
            if (config == null || config.set == null) { enabled = false; return; }
            _brain = GetComponent<EnemyBrain>();
            _traits = GetComponent<EnemyTraits>();
            _health = GetComponent<Health>();
            HideOldLook();

            var go = new GameObject("Sprite");
            go.transform.SetParent(transform, false);
            _sprite = go.AddComponent<DioramaSprite>();
            _sprite.set = config.set;
            _sprite.material = config.material;
            _sprite.shadowMaterial = config.shadowMaterial;
            if (config.set.ClipsHaveSheets) _sprite.UseClipSheets();
            else _sprite.SetLayers(config.set.layers);
            _lastPos = transform.position;
            _spriteRenderer = go.GetComponent<MeshRenderer>();
            SetUpExtras();
        }

        void SetUpExtras()
        {
            ScheduleAmbient();
            if (config.lamp)
            {
                var go = new GameObject("Farol");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<PixelLight>();
                l.offset = config.lampOffset;
                l.radius = config.lampRadius;
                l.color = config.lampColor;
                l.intensity = config.lampIntensity;
                l.flicker = 0.3f;
            }
            if (config.obstacleHalf.x > 0f && config.obstacleHalf.y > 0f)
            {
                var go = new GameObject("Obstaculo");
                go.transform.SetParent(transform, false);
                go.transform.position = transform.position + new Vector3(config.obstacleOffset.x, 0f, config.obstacleOffset.y);
                go.transform.rotation = Quaternion.identity;
                var o = go.AddComponent<WorldObstacle>();
                o.kind = WorldObstacle.Kind.Box;
                o.halfSize = config.obstacleHalf;
            }
        }

        void ScheduleAmbient()
        {
            _ambientIn = Random.Range(config.ambientEvery.x, Mathf.Max(config.ambientEvery.x, config.ambientEvery.y));
        }

        // Only the primitives of the body go: auras, lights and particles stay.
        void HideOldLook()
        {
            if (_brain != null && _brain.body != null)
            {
                Hide(_brain.body);
                Hide(_brain.legL);
                Hide(_brain.legR);
                Hide(_brain.arms);
                return;
            }
            Hide(transform);
        }

        static void Hide(Transform t)
        {
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var r in t.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false;
        }

        void FindHero()
        {
            if (_hero != null) return;
            var h = Object.FindFirstObjectByType<DioramaHero>();
            if (h == null) return;
            _hero = h.transform;
            _camera = h.sceneCamera;
        }

        void LateUpdate()
        {
            if (_sprite == null) return;
            FindHero();
            // A falling enemy tips its root over; the picture stays upright and animates the fall itself.
            _sprite.transform.rotation = Quaternion.identity;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = transform.position - _lastPos;
            v.y = 0f;
            _lastPos = transform.position;
            _speedNow = v.magnitude / dt;
            bool movingNow = _speedNow > config.moveThreshold;
            _movingFor = movingNow ? 0.15f : Mathf.Max(0f, _movingFor - dt);
            _moving = _movingFor > 0f;

            if (mode == Mode.Boss) Boss();
            else if (mode == Mode.Enemy) Enemy();
            else Npc();
        }

        float CameraYaw()
        {
            var cam = _camera != null ? _camera : Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : 45f;
        }

        void Face(float yaw)
        {
            _sprite.direction = CharacterLayerSet.ViewFor(yaw - CameraYaw(), _sprite.set.views);
        }

        void Npc()
        {
            float yaw = config.faceHero ? transform.eulerAngles.y : config.fixedYaw;
            float dist = float.MaxValue;
            if (_hero != null)
            {
                Vector3 d = _hero.position - transform.position;
                d.y = 0f;
                dist = d.magnitude;
                if (config.faceHero && dist < config.lookAtHeroRange && d.sqrMagnitude > 0.01f)
                    yaw = Mathf.Round(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg / 45f) * 45f;
            }
            Face(yaw);

            // A clip that plays on its own (the woodcutter's Bruma fit) wins until it ends.
            if (_ambientPlaying)
            {
                if (!_sprite.Finished) return;
                _ambientPlaying = false;
                _greeting = false;
                ScheduleAmbient();
            }
            if (!_moving && !string.IsNullOrEmpty(config.ambientClip) && _sprite.HasClip(config.ambientClip)
                && dist < config.ambientRange)
            {
                _ambientIn -= Time.deltaTime;
                if (_ambientIn <= 0f)
                {
                    _sprite.Play(config.ambientClip, 1f, true);
                    _ambientPlaying = true;
                    return;
                }
            }

            bool canGreet = !string.IsNullOrEmpty(config.greetClip) && _sprite.HasClip(config.greetClip);
            if (_greeting && (!canGreet || dist > config.greetRange + 1.5f || dist < config.greetMin - 0.5f))
                _greeting = false;
            else if (!_greeting && canGreet && dist < config.greetRange && dist >= config.greetMin)
            {
                _greeting = true;
                _greetTime = 0f;
                _sprite.Play(config.greetClip, 1f, true);
            }

            if (_greeting && !_moving)
            {
                _greetTime += Time.deltaTime;
                if (config.greetLoopFrom >= 0 && _sprite.Finished) LoopGreet();
                else _sprite.Play(config.greetClip, 1f, false);
                return;
            }
            _sprite.Play(_moving && _sprite.HasClip("walk") ? "walk" : "idle", 1f, false);
        }

        // After the greet clip has played once, keep cycling its last frames (waving).
        void LoopGreet()
        {
            var set = _sprite.set;
            var c = set.clips[set.ClipIndex(config.greetClip)];
            int from = Mathf.Clamp(config.greetLoopFrom, 0, c.frames - 1);
            int span = Mathf.Max(1, c.frames - from);
            float t = Mathf.Max(0f, _greetTime - (c.frames - 1) / Mathf.Max(0.1f, c.fps));
            _sprite.Hold(config.greetClip, from + (int)(t * c.fps) % span);
        }

        void Boss()
        {
            bool dead = _health != null && _health.IsDead;
            if (dead)
            {
                _sprite.Play("death", 1f, false);
                return;
            }
            Face(transform.eulerAngles.y);
            bool p2 = PhaseTwo;
            bool attacking = _brain != null && _brain.Current == EnemyBrain.State.Attack && _brain.profile != null;
            if (attacking)
            {
                if (!_wasAttacking) _attackStart = Time.time;
                string clip = p2 && _sprite.HasClip("bite2") ? "bite2" : "bite";
                _sprite.Hold(clip, BiteFrame(Time.time - _attackStart, _brain.profile));
            }
            else if (_moving) _sprite.Play(p2 && _sprite.HasClip("crawl2") ? "crawl2" : "crawl", 1f, false);
            else _sprite.Play(p2 && _sprite.HasClip("p2idle") ? "p2idle" : "idle", 1f, false);
            _wasAttacking = attacking;
        }

        // Ordinary enemies: idle, walk (sped up with the actual speed), attack following the EnemyProfile timer, death.
        // An ambusher in hiding is not drawn; when it springs, the old primitives that EnemyTraits switches back on
        // are hidden again.
        void Enemy()
        {
            if (_traits != null)
            {
                bool hidden = _traits.Hidden;
                if (_spriteRenderer != null) _spriteRenderer.enabled = !hidden;
                if (_wasHidden && !hidden) HideOldLook();
                _wasHidden = hidden;
                if (hidden) return;
            }
            bool dead = _health != null && _health.IsDead;
            if (dead)
            {
                _sprite.Play("death", 1f, false);
                return;
            }
            Face(transform.eulerAngles.y);
            bool attacking = _brain != null && _brain.Current == EnemyBrain.State.Attack && _brain.profile != null;
            if (attacking)
            {
                if (!_wasAttacking) _attackStart = Time.time;
                _sprite.Hold("attack", BiteFrame(Time.time - _attackStart, _brain.profile));
            }
            else if (_moving)
                _sprite.Play("walk", Mathf.Clamp(_speedNow / Mathf.Max(0.1f, config.walkSpeedRef), 0.6f, 2.2f), false);
            else _sprite.Play("idle", 1f, false);
            _wasAttacking = attacking;
        }

        // 8 frames: 0 rest, 1-3 rears up during the startup (3 held), 4-5 the lunge and the snap while the hit is
        // active, 6-7 back during the recovery.
        static int BiteFrame(float t, EnemyProfile p)
        {
            float s = Mathf.Max(0.01f, p.AttackStartup), a = Mathf.Max(0.01f, p.AttackActive), r = Mathf.Max(0.01f, p.AttackRecovery);
            if (t < s)
            {
                float k = t / s;
                return k < 0.2f ? 1 : (k < 0.45f ? 2 : 3);
            }
            if (t < s + a) return (t - s) / a < 0.5f ? 4 : 5;
            return (t - s - a) / r < 0.5f ? 6 : 7;
        }
    }
}
