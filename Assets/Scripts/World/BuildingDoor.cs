using BrumaPx.Combat;
using BrumaPx.Player;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// The door of a building. Unlocked: the interact key swings it open (and shut again). Locked (a padlock): it
    /// only gives way to blows; the hero's attacks hit its Health and when that runs out the leaf is swapped for
    /// the broken pieces and the doorway is free for good. Either way the blocker in the doorway goes away.
    /// </summary>
    public sealed class BuildingDoor : MonoBehaviour
    {
        [Tooltip("The leaf, pivoting on its hinge (this object's own pivot is the hinge).")]
        public Transform leaf;
        [Tooltip("What is left after breaking it (hidden until then).")]
        public GameObject broken;
        [Tooltip("Boards nailed across the leaf, shown only while locked (houses). Without them the lock is a padlock.")]
        public GameObject bars;
        public WorldObstacle blocker;
        public Health health;
        public bool locked;
        [Tooltip("Degrees the leaf turns about the hinge to open (positive swings it inwards).")]
        public float openYaw = 100f;
        [Tooltip("Where the hero stands to use it, in local space.")]
        public Vector3 promptPoint;
        public float reach = 1.5f;
        public string lockedText = "Cerrada con candado: se puede romper a golpes";
        public string barredText = "Tapiada con tablas: se puede romper a golpes";

        public bool IsOpen { get { return _open || _broken; } }
        public bool IsBroken { get { return _broken; } }

        bool _open, _broken;
        float _t, _shake;
        Quaternion _rest;
        float _cooldown;

        void Awake()
        {
            if (leaf != null) _rest = leaf.localRotation;
            if (broken != null) broken.SetActive(false);
            if (bars != null) bars.SetActive(locked);
        }

        void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += Break;
            }
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= Break;
            }
        }

        void OnDamaged(float amount, Vector2 dir) { _shake = 0.25f; }

        /// <summary>Breaks the door at once (blows, or a story event).</summary>
        public void Break()
        {
            if (_broken) return;
            _broken = true;
            if (leaf != null) leaf.gameObject.SetActive(false);
            if (broken != null) broken.SetActive(true);
            if (blocker != null) blocker.enabled = false;
            if (health != null) health.gameObject.SetActive(false);
            MapWorld.Banner("La puerta cede.", 2.5f);
        }

        public void Unlock() { SetLocked(false); }

        /// <summary>Locks or unlocks the door (and shows or hides the boards nailed across it).</summary>
        public void SetLocked(bool on)
        {
            locked = on;
            if (bars != null) bars.SetActive(on && !_broken);
        }

        void Update()
        {
            if (!_broken && leaf != null)
            {
                _t = Mathf.MoveTowards(_t, _open ? 1f : 0f, Time.deltaTime / 0.35f);
                float yaw = Mathf.SmoothStep(0f, openYaw, _t);
                if (_shake > 0f)
                {
                    _shake -= Time.deltaTime;
                    yaw += Mathf.Sin(Time.time * 70f) * 2.5f * (_shake / 0.25f);
                }
                leaf.localRotation = _rest * Quaternion.Euler(0f, yaw, 0f);
                if (blocker != null) blocker.enabled = _t < 0.5f;
            }
            if (_broken) return;

            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;
            Vector3 d = transform.TransformPoint(promptPoint) - hero.position;
            d.y = 0f;
            if (d.sqrMagnitude > reach * reach) return;

            if (locked)
            {
                MapLoot.SetPrompt(bars != null ? barredText : lockedText);
                return;
            }
            MapLoot.SetPrompt(_open ? "[Interactuar] Cerrar la puerta" : "[Interactuar] Abrir la puerta");
            if (Time.unscaledTime < _cooldown || !DioramaInput.InteractPressed()) return;
            _cooldown = Time.unscaledTime + 0.4f;
            _open = !_open;
        }
    }
}
