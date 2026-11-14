using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.Atmosphere
{
    /// <summary>
    /// A lamp pool for the pixel look: lights the ground and props around it with stepped, dithered rings when it
    /// is dark. The DayNightWeather component picks the nearest few and hands them to the shaders.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PixelLight : MonoBehaviour
    {
        [Tooltip("Position of the light, in this object's local space.")]
        public Vector3 offset = new Vector3(0f, 2.45f, 0f);
        [Tooltip("World units the pool reaches.")]
        public float radius = 5.5f;
        public Color color = new Color(1f, 0.62f, 0.3f, 1f);
        [Range(0f, 2f)] public float intensity = 1f;
        [Tooltip("0 = steady flame. Higher values flicker harder (stepped, 8 times a second).")]
        [Range(0f, 1f)] public float flicker = 0.25f;
        [Tooltip("Off for lights that only help the player see (the hero's own light), so they do not make the hero easier to spot.")]
        public bool countsForVisibility = true;


        public static readonly List<PixelLight> All = new List<PixelLight>();

        float _seed;

        void OnEnable()
        {
            All.Add(this);
            _seed = Mathf.Abs(transform.position.x * 3.1f + transform.position.z * 7.7f);
        }

        void OnDisable() { All.Remove(this); }

        /// <summary>World position of the light.</summary>
        public Vector3 WorldPosition { get { return transform.TransformPoint(offset); } }

        /// <summary>Stepped brightness factor in 0..1, so flames flicker at the same low frame rate as the mist.</summary>
        public float Flicker()
        {
            if (flicker <= 0f) return 1f;
            float step = Mathf.Floor(Time.time * 8f) + _seed;
            float h = Mathf.Abs(Mathf.Sin(step * 12.9898f) * 43758.5453f) % 1f;
            return 1f - flicker * 0.5f * h;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(WorldPosition, radius);
        }
    }
}
