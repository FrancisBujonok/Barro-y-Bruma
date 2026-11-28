using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>Turns its object slowly about a local axis (the mill's water wheel). Sleeps when the hero is far.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.right;
        [Tooltip("Degrees per second.")]
        public float speed = -14f;
        public float wakeDistance = 40f;

        void Update()
        {
            var hero = DioramaHeroCache.Hero;
            if (hero != null && (hero.position - transform.position).sqrMagnitude > wakeDistance * wakeDistance) return;
            transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
        }
    }
}
