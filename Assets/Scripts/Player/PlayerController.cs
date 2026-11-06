using System;
using System.Collections.Generic;
using BarroYBruma.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarroYBruma.Player
{
    /// <summary>
    /// Player movement, melee attack, dodge and health (story 001, design/game-brief.md MVP feature 1).
    /// Values come from <see cref="PlayerStats"/>; logic timers live in <see cref="AttackTimer"/>,
    /// <see cref="DodgeTimer"/> and <see cref="ArcMath"/>. Input is read in Update; velocity and the
    /// dodge run in FixedUpdate so the dodge distance does not depend on frame rate. Walls/doors
    /// (story 006), fog (002) and back-attack bonus (005) are out of scope.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public sealed class PlayerController : MonoBehaviour, IDamageable
    {
        private const int SortOrderPerUnit = 100;
        private const int HitBufferSize = 16;
        // The floor sits at short.MinValue and the shadow one below the player.
        private const int MinSortOrder = short.MinValue + 2;
        private const int MaxSortOrder = short.MaxValue;

        [SerializeField] private PlayerStats _stats;
        [SerializeField] private InputActionAsset _inputActions;
        [Tooltip("Optional: sorted one step behind the player sprite.")]
        [SerializeField] private SpriteRenderer _shadowRenderer;

        private Rigidbody2D _body;
        private SpriteRenderer _spriteRenderer;
        private InputAction _move, _attack, _dodge;
        private readonly AttackTimer _attackTimer = new AttackTimer();
        private readonly DodgeTimer _dodgeTimer = new DodgeTimer();
        private readonly Collider2D[] _hitBuffer = new Collider2D[HitBufferSize];
        private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();
        private Vector2 _moveInput;
        private Vector2 _attackFacing;
        private bool _dodgeQueued;

        /// <summary>Raised once when health reaches 0 (fail state; consumed by story 007).</summary>
        public event Action OnDied;

        /// <summary>Current health.</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>Maximum health from the stats asset.</summary>
        public float MaxHealth => _stats.MaxHealth;

        /// <summary>Last non-zero movement direction (normalized). Starts facing down.</summary>
        public Vector2 Facing { get; private set; } = Vector2.down;

        private void Awake()
        {
            if (_stats == null)
            {
                Debug.LogWarning("[PlayerController] No PlayerStats assigned; using default runtime values.", this);
                _stats = ScriptableObject.CreateInstance<PlayerStats>();
            }
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            CurrentHealth = _stats.MaxHealth;

            if (_inputActions == null)
            {
                Debug.LogError("[PlayerController] No InputActionAsset assigned; disabling the controller.", this);
                enabled = false;
                return;
            }
            _move = _inputActions.FindAction("Player/Move", true);
            _attack = _inputActions.FindAction("Player/Attack", true);
            _dodge = _inputActions.FindAction("Player/Dodge", true);
        }

        private void OnEnable()
        {
            if (_inputActions != null) _inputActions.FindActionMap("Player", true).Enable();
        }

        private void OnDisable()
        {
            if (_inputActions != null) _inputActions.FindActionMap("Player", true).Disable();
            _body.linearVelocity = Vector2.zero;
            _dodgeQueued = false;
        }

        private void Update()
        {
            _moveInput = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            if (_moveInput.sqrMagnitude > 0f) Facing = _moveInput.normalized;

            // Consumed by the next FixedUpdate.
            if (_dodge.WasPressedThisFrame()) _dodgeQueued = true;

            _attackTimer.Tick(Time.deltaTime);
            if (!_dodgeTimer.IsDodging && _attack.WasPressedThisFrame() &&
                _attackTimer.Start(_stats.AttackStartup, _stats.AttackActive, _stats.AttackRecovery))
            {
                _attackFacing = Facing;
                _alreadyHit.Clear();
            }

            if (_attackTimer.Phase == AttackPhase.Active) ApplyMeleeHits();
        }

        private void FixedUpdate()
        {
            if (_dodgeQueued)
            {
                _dodgeQueued = false;
                // Dodging cancels an attack in progress.
                if (_dodgeTimer.TryStart(Facing, _stats.DodgeDistance, _stats.DodgeDuration, _stats.DodgeCooldown))
                    _attackTimer.Cancel();
            }

            bool dodging = _dodgeTimer.IsDodging;
            Vector2 dodgeVelocity = _dodgeTimer.Step(Time.fixedDeltaTime);
            _body.linearVelocity = dodging ? dodgeVelocity : _moveInput * _stats.MoveSpeed;
        }

        private void LateUpdate()
        {
            // 2.5D: lower on screen draws in front. Uses the interpolated (drawn) position, sorted by
            // the feet rather than the sprite centre.
            int order = -Mathf.RoundToInt((transform.position.y + _stats.SortPivotOffsetY) * SortOrderPerUnit);
            order = Mathf.Clamp(order, MinSortOrder, MaxSortOrder);
            _spriteRenderer.sortingOrder = order;
            if (_shadowRenderer != null) _shadowRenderer.sortingOrder = order - 1;
        }

        private void ApplyMeleeHits()
        {
            Vector2 origin = _body.position;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _stats.AttackHitMask,
                useTriggers = true, // enemy hurtboxes may be triggers
            };
            int count = Physics2D.OverlapCircle(origin, _stats.AttackRange, filter, _hitBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D col = _hitBuffer[i];
                if (!col.TryGetComponent(out IDamageable target) || ReferenceEquals(target, this)) continue;
                if (_alreadyHit.Contains(target)) continue;

                Vector2 closest = col.ClosestPoint(origin);
                if (!ArcMath.IsInArc(origin, _attackFacing, closest, _stats.AttackRange, _stats.AttackArcDegrees)) continue;

                _alreadyHit.Add(target);
                Vector2 toTarget = (Vector2)col.bounds.center - origin;
                target.TakeDamage(_stats.AttackDamage, toTarget.sqrMagnitude > 0f ? toTarget.normalized : _attackFacing);
            }
        }

        /// <summary>Reduces health; raises <see cref="OnDied"/> once when it reaches 0.</summary>
        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (amount <= 0f || CurrentHealth <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (CurrentHealth <= 0f) OnDied?.Invoke();
        }
    }
}
