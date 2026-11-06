namespace BarroYBruma.Combat
{
    /// <summary>Phases of a melee attack.</summary>
    public enum AttackPhase { Idle, Startup, Active, Recovery }

    /// <summary>
    /// Pure startup/active/recovery timer for a melee attack. Transitions:
    /// Idle -> Startup -> Active -> Recovery -> Idle (via <see cref="Start"/> and <see cref="Tick"/>).
    /// </summary>
    public sealed class AttackTimer
    {
        private float _startup, _active, _recovery, _elapsed;

        /// <summary>Current phase.</summary>
        public AttackPhase Phase { get; private set; } = AttackPhase.Idle;

        /// <summary>True while any phase other than Idle is running.</summary>
        public bool IsBusy => Phase != AttackPhase.Idle;

        /// <summary>Begins an attack. Ignored if one is already running. Returns whether it started.</summary>
        public bool Start(float startup, float active, float recovery)
        {
            if (IsBusy) return false;
            _startup = startup; _active = active; _recovery = recovery;
            _elapsed = 0f;
            Phase = Resolve();
            return true;
        }

        /// <summary>Aborts the running attack.</summary>
        public void Cancel() => Phase = AttackPhase.Idle;

        /// <summary>Advances the timer by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (!IsBusy) return;
            _elapsed += dt;
            Phase = Resolve();
        }

        private AttackPhase Resolve()
        {
            if (_elapsed < _startup) return AttackPhase.Startup;
            if (_elapsed < _startup + _active) return AttackPhase.Active;
            if (_elapsed < _startup + _active + _recovery) return AttackPhase.Recovery;
            return AttackPhase.Idle;
        }
    }
}
