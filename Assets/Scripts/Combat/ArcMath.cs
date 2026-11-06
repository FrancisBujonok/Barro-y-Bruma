using UnityEngine;

namespace BarroYBruma.Combat
{
    /// <summary>Pure geometry for frontal melee arcs. Story 001 (design/game-brief.md, MVP feature 1).</summary>
    public static class ArcMath
    {
        /// <summary>
        /// True when <paramref name="target"/> is within <paramref name="range"/> of
        /// <paramref name="origin"/> and within half of <paramref name="angleDeg"/> either side of
        /// <paramref name="facing"/>. A target on the origin counts as inside.
        /// </summary>
        /// <param name="origin">Attack origin.</param>
        /// <param name="facing">Facing direction (need not be normalized, must be non-zero).</param>
        /// <param name="target">Point being tested.</param>
        /// <param name="range">Maximum distance, inclusive.</param>
        /// <param name="angleDeg">Total arc width in degrees.</param>
        public static bool IsInArc(Vector2 origin, Vector2 facing, Vector2 target, float range, float angleDeg)
        {
            Vector2 delta = target - origin;
            float sqrDist = delta.sqrMagnitude;
            if (sqrDist > range * range) return false;
            if (sqrDist < 1e-8f) return true;
            return Vector2.Angle(facing, delta) <= angleDeg * 0.5f;
        }
    }
}
