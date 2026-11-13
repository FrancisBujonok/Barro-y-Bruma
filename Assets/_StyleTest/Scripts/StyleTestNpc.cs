using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.StyleTest
{
    /// <summary>A talkable character: a name, some lines and a portrait. Registers itself while enabled.</summary>
    public sealed class StyleTestNpc : MonoBehaviour
    {
        public string displayName = "Vigia del camino";
        [TextArea(2, 4)] public string[] lines;
        public Texture portrait;
        public float interactRadius = 2.6f;
        public Transform promptAnchor;

        public static readonly List<StyleTestNpc> All = new List<StyleTestNpc>();

        public Vector3 PromptPoint
        {
            get { return promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 2.4f; }
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Closest NPC whose interact radius contains pos, or null.</summary>
        public static StyleTestNpc Nearest(Vector3 pos)
        {
            StyleTestNpc best = null;
            float bestSqr = float.MaxValue;
            foreach (var n in All)
            {
                Vector3 d = n.transform.position - pos;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr <= n.interactRadius * n.interactRadius && sqr < bestSqr)
                {
                    best = n;
                    bestSqr = sqr;
                }
            }
            return best;
        }

        /// <summary>Turns to look at a point, snapped to 8 directions like everything else in this style.</summary>
        public void FaceTowards(Vector3 target)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            float yaw = Mathf.Round(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg / 45f) * 45f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
