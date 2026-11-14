using BrumaPx.Atmosphere;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// Measures how lit the hero is: the ambient light of the time of day and weather, plus the lamp pools around.
    /// Enemies use the level to decide how far they can see; the HUD shows it as an icon.
    /// The hero's own light does not count (PixelLight.countsForVisibility is off on it).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroVisibility : MonoBehaviour
    {
        public DayNightWeather atmosphere;
        [Tooltip("How fast the level follows the real light (per second).")]
        public float smoothing = 8f;

        /// <summary>0 = hidden in the dark, 1 = fully lit.</summary>
        public float Level { get; private set; } = 1f;

        public VisibilityState State { get; private set; } = VisibilityState.Exposed;

        void Update()
        {
            float ambient = atmosphere != null ? atmosphere.AmbientLight : 1f;
            float lampStrength = atmosphere != null ? atmosphere.LampStrength : 0f;

            float lamps = 0f;
            Vector3 p = transform.position;
            var all = PixelLight.All;
            for (int i = 0; i < all.Count; i++)
            {
                PixelLight l = all[i];
                if (l == null || !l.isActiveAndEnabled || !l.countsForVisibility) continue;
                Vector3 d = l.WorldPosition - p;
                d.y = 0f;
                lamps += VisibilityMath.PoolContribution(d.magnitude, l.radius, l.intensity);
            }

            float target = VisibilityMath.Combine(ambient, lamps * lampStrength);
            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            Level = Mathf.Lerp(Level, target, k);
            State = VisibilityMath.Classify(Level, State);
        }
    }
}
