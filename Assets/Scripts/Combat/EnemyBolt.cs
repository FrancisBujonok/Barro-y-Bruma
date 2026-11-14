using BrumaPx.Player;
using BrumaPx.World;
using UnityEngine;

namespace BrumaPx.Combat
{
    /// <summary>
    /// A crossbow bolt. It flies straight on the ground plane, stops at the first obstacle, and when it reaches the
    /// hero it goes through HeroCombat.ReceiveAttack, so rolling, guarding, parrying, armour and the body all apply.
    /// </summary>
    public sealed class EnemyBolt : MonoBehaviour
    {
        const float HitRadius = 0.5f;

        EnemyBrain _owner;
        HeroCombat _combat;
        Transform _hero;
        Vector3 _dir;
        float _speed, _damage, _left;

        public void Launch(EnemyBrain owner, Vector3 dir, float speed, float damage, HeroCombat combat, Transform hero, float range)
        {
            _owner = owner;
            _dir = dir;
            _speed = speed;
            _damage = damage;
            _combat = combat;
            _hero = hero;
            _left = Mathf.Max(1f, range);
        }

        void Update()
        {
            float step = _speed * Time.deltaTime;
            Vector3 next = transform.position + _dir * step;
            Vector2 p = new Vector2(next.x, next.z);

            var obstacles = WorldObstacle.All;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Vector2 pushed = obstacles[i].Resolve(p, 0.08f);
                if ((pushed - p).sqrMagnitude > 1e-6f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (_hero != null)
            {
                Vector3 h = _hero.position;
                if (new Vector2(h.x - next.x, h.z - next.z).magnitude < HitRadius)
                {
                    if (_combat != null) _combat.ReceiveAttack(_owner, _damage, new Vector2(_dir.x, _dir.z));
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position = next;
            _left -= step;
            if (_left <= 0f) Destroy(gameObject);
        }
    }
}
