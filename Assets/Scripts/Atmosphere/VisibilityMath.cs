using UnityEngine;

namespace BrumaPx.Atmosphere
{
    /// <summary>How easy it is to see the hero right now.</summary>
    public enum VisibilityState { Hidden, Dim, Exposed }

    /// <summary>
    /// Pure maths for the hero's light level (0 = pitch dark, 1 = fully lit). No scene access, so it can be tested alone.
    /// The lamp falloff mirrors the lamp pools the shaders draw (PP_Atmos), without the dithered steps.
    /// </summary>
    public static class VisibilityMath
    {
        public const float HiddenBelow = 0.30f;
        public const float HiddenUntil = 0.36f;
        public const float ExposedAbove = 0.62f;
        public const float ExposedUntil = 0.56f;

        /// <summary>Ambient light from the average tone of the sky: 0.30 (deep night) maps to 0 and 1.00 (day) maps to 1.</summary>
        public static float AmbientFromTone(float averageTone)
        {
            return Mathf.Clamp01(Mathf.InverseLerp(0.30f, 1f, averageTone));
        }

        /// <summary>Light a single lamp pool adds at a given ground distance.</summary>
        public static float PoolContribution(float distance, float radius, float intensity)
        {
            float a = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, radius));
            return Mathf.Clamp01(a * 1.3f) * Mathf.Max(0f, intensity);
        }

        /// <summary>Ambient plus lamp light, clamped to 0..1.</summary>
        public static float Combine(float ambient, float lampLight)
        {
            return Mathf.Clamp01(ambient + lampLight);
        }

        /// <summary>Maps a level to a state. Each border has a gap so the icon does not flicker on the edge.</summary>
        public static VisibilityState Classify(float level, VisibilityState previous)
        {
            switch (previous)
            {
                case VisibilityState.Hidden:
                    if (level >= ExposedAbove) return VisibilityState.Exposed;
                    return level >= HiddenUntil ? VisibilityState.Dim : VisibilityState.Hidden;
                case VisibilityState.Exposed:
                    if (level <= HiddenBelow) return VisibilityState.Hidden;
                    return level <= ExposedUntil ? VisibilityState.Dim : VisibilityState.Exposed;
                default:
                    if (level < HiddenBelow) return VisibilityState.Hidden;
                    return level > ExposedAbove ? VisibilityState.Exposed : VisibilityState.Dim;
            }
        }
    }
}
