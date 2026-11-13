using BrumaPx.Rendering;
using BrumaPx.World;
using BrumaPx.Player;
using BrumaPx.Dialogue;
using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// Flat (X/Z) blocker the hero walks around. A circle, or an axis-aligned box. Registers itself while
    /// enabled, so props in unloaded chunks cost nothing.
    /// </summary>
    public sealed class WorldObstacle : MonoBehaviour
    {
        public enum Kind { Circle, Box }

        public Kind kind = Kind.Circle;
        public float radius = 0.5f;
        public Vector2 halfSize = new Vector2(1f, 1f);

        public static readonly List<WorldObstacle> All = new List<WorldObstacle>();

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Pushes a circle (centre p, radius r) out of this obstacle and returns the corrected centre.</summary>
        public Vector2 Resolve(Vector2 p, float r)
        {
            Vector2 c = new Vector2(transform.position.x, transform.position.z);

            if (kind == Kind.Circle)
            {
                Vector2 d = p - c;
                float min = radius + r;
                float m = d.magnitude;
                if (m >= min) return p;
                d = m < 1e-4f ? Vector2.right : d / m;
                return c + d * min;
            }

            Vector2 local = p - c;
            Vector2 clamped = new Vector2(Mathf.Clamp(local.x, -halfSize.x, halfSize.x), Mathf.Clamp(local.y, -halfSize.y, halfSize.y));
            Vector2 delta = local - clamped;
            float dist = delta.magnitude;
            if (dist >= r) return p;
            if (dist > 1e-4f) return c + clamped + delta / dist * r;

            // Centre is inside the box: leave through the nearest side.
            float px = halfSize.x - Mathf.Abs(local.x);
            float py = halfSize.y - Mathf.Abs(local.y);
            if (px < py) local.x = (local.x < 0f ? -1f : 1f) * (halfSize.x + r);
            else local.y = (local.y < 0f ? -1f : 1f) * (halfSize.y + r);
            return c + local;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 c = transform.position;
            if (kind == Kind.Circle) Gizmos.DrawWireSphere(c, radius);
            else Gizmos.DrawWireCube(c, new Vector3(halfSize.x * 2f, 0.2f, halfSize.y * 2f));
        }
    }
}
