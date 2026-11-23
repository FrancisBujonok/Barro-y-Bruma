using BrumaPx.Player;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A trapdoor in the floor of a building that leads to its basement. Inside the building the interact key
    /// lifts the lid and takes the hero down; in the basement, at the foot of the stairs, it brings them back up.
    /// </summary>
    public sealed class BuildingHatch : MonoBehaviour
    {
        public Building building;
        [Tooltip("The lid, pivoting on its hinge.")]
        public Transform lid;
        [Tooltip("Degrees the lid turns about its hinge (local x) to open.")]
        public float openPitch = 100f;
        [Tooltip("Local points: the trapdoor, where the hero comes back up, where they land below, the foot of the stairs.")]
        public Vector3 center, returnPoint, arrivePoint, upPoint;
        public float reach = 1.2f;
        public string downText = "[Interactuar] Bajar al sotano";
        public string upText = "[Interactuar] Subir";

        Quaternion _rest;
        float _t;
        bool _opening;
        float _cooldown;

        void Awake()
        {
            if (lid != null) _rest = lid.localRotation;
        }

        Vector3 W(Vector3 local) { return building.transform.TransformPoint(local); }

        void Update()
        {
            if (lid != null)
            {
                _t = Mathf.MoveTowards(_t, _opening ? 1f : 0f, Time.unscaledDeltaTime / 0.3f);
                lid.localRotation = _rest * Quaternion.Euler(Mathf.SmoothStep(0f, openPitch, _t), 0f, 0f);
            }
            if (building == null || Underground.Busy) return;
            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;

            bool below = Underground.Current == building;
            Vector3 p = W(below ? upPoint : center);
            Vector3 d = p - hero.position;
            d.y = 0f;
            if (d.sqrMagnitude > reach * reach) return;
            if (!below && !building.HeroInside) return;

            MapLoot.SetPrompt(below ? upText : downText);
            if (Time.unscaledTime < _cooldown || !DioramaInput.InteractPressed()) return;
            _cooldown = Time.unscaledTime + 1f;
            if (below)
            {
                _opening = true;
                Underground.Exit(building, W(returnPoint), () => _opening = false);
            }
            else
            {
                _opening = true;
                Underground.Enter(building, W(arrivePoint), () => _opening = false);
            }
        }
    }
}
