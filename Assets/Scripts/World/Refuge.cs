using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A place to rest (a campfire, a shelter). Skill points can only be spent while the hero is inside one.
    /// </summary>
    public sealed class Refuge : MonoBehaviour
    {
        public static readonly List<Refuge> All = new List<Refuge>();

        [SerializeField] string displayName = "Refugio";
        [SerializeField, Min(0.5f)] float radius = 3.5f;

        public string DisplayName { get { return displayName; } }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        /// <summary>The refuge that contains this point (flat distance), or null.</summary>
        public static Refuge At(Vector3 point)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Vector3 d = All[i].transform.position - point;
                d.y = 0f;
                if (d.sqrMagnitude <= All[i].radius * All[i].radius) return All[i];
            }
            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.7f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
