using UnityEngine;

namespace BarroYBruma.Player
{
    /// <summary>
    /// Pure dodge state: a short impulse that covers an exact distance, followed by a cooldown that
    /// starts when the impulse ends. Transitions: Ready -> Dodging -> Cooldown -> Ready.
    /// Drive it from fixed steps (<see cref="Step"/>) so the distance does not depend on frame rate.
    /// </summary>
    public sealed class DodgeTimer
    {
        private float _distanceRemaining;
        private float _cooldownRemaining;
        private float _cooldown;
        private float _speed;
        private Vector2 _direction;

        /// <summary>True while the impulse is running.</summary>
        public bool IsDodging => _distanceRemaining > 0f;

        /// <summary>True when a new dodge may start.</summary>
        public bool IsReady => !IsDodging && _cooldownRemaining <= 0f;

        /// <summary>Starts a dodge covering <paramref name="distance"/> in <paramref name="duration"/> seconds. Returns false if not ready.</summary>
        public bool TryStart(Vector2 direction, float distance, float duration, float cooldown)
        {
            if (!IsReady || duration <= 0f || distance <= 0f) return false;
            _direction = direction.normalized;
            _speed = distance / duration;
            _distanceRemaining = distance;
            _cooldown = cooldown;
            return true;
        }

        /// <summary>
        /// Advances by one step of <paramref name="dt"/> seconds and returns the velocity to apply for
        /// that step. The last step is shortened so the total travel equals the requested distance.
        /// Returns zero when not dodging (and counts the cooldown down).
        /// </summary>
        public Vector2 Step(float dt)
        {
            if (!IsDodging)
            {
                if (_cooldownRemaining > 0f) _cooldownRemaining -= dt;
                return Vector2.zero;
            }

            float stepDistance = Mathf.Min(_speed * dt, _distanceRemaining);
            _distanceRemaining -= stepDistance;
            if (_distanceRemaining <= 1e-5f)
            {
                _distanceRemaining = 0f;
                _cooldownRemaining = _cooldown;
            }
            return _direction * (stepDistance / dt);
        }
    }
}
