using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A patch of thick Bruma on the ground. Standing in it is one of the ways the hero is exposed (through the air).
    /// Full density in the core, fading to nothing at the edge. Refuges are never exposed.
    /// </summary>
    public sealed class BrumaArea : MonoBehaviour
    {
        public static readonly List<BrumaArea> All = new List<BrumaArea>();

        [SerializeField] string displayName = "Foco de Bruma";
        [SerializeField, Min(0.5f)] float radius = 4f;
        [SerializeField, Range(0f, 1f)] float density = 1f;

        public string DisplayName { get { return displayName; } }
        public float Radius { get { return radius; } }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        /// <summary>The thickest Bruma at this point (0 = clean air, 1 = full density).</summary>
        public static float DensityAt(Vector3 point)
        {
            float best = 0f;
            for (int i = 0; i < All.Count; i++)
            {
                BrumaArea a = All[i];
                Vector3 d = a.transform.position - point;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist >= a.radius) continue;
                float core = a.radius * 0.6f;
                float k = dist <= core ? 1f : 1f - (dist - core) / (a.radius - core);
                best = Mathf.Max(best, k * a.density);
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.4f, 0.65f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
