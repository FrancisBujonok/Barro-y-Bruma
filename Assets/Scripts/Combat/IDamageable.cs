namespace BarroYBruma.Combat
{
    /// <summary>Anything that can receive melee damage (player, enemies, breakables).</summary>
    public interface IDamageable
    {
        /// <summary>Applies damage.</summary>
        /// <param name="amount">Damage points; non-positive values are ignored.</param>
        /// <param name="hitDirection">Normalized direction from the attacker towards this target.</param>
        void TakeDamage(float amount, UnityEngine.Vector2 hitDirection);
    }
}
