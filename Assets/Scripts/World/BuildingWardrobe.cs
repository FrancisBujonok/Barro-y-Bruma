using BrumaPx.Player;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A wardrobe in a basement that hides a passage. Looked at closely (interact) it gives way and slides aside,
    /// and the hidden room behind it appears (it stays hidden, and silent, until then).
    /// </summary>
    public sealed class BuildingWardrobe : MonoBehaviour
    {
        public Building building;
        [Tooltip("Local offset the wardrobe slides by when pushed aside (building space, scaled).")]
        public Vector3 slide = new Vector3(-1.9f, 0f, 0f);
        [Tooltip("Where the hero stands to use it, in building space.")]
        public Vector3 promptPoint;
        public float reach = 1.4f;
        public float seconds = 1.4f;
        public string promptText = "[Interactuar] Revisar el armario";
        public string revealText = "El armario cede y se corre: detras hay un pasadizo.";

        public bool Opened { get; private set; }

        Vector3 _rest;
        float _t;

        void Awake() { _rest = transform.localPosition; }

        void Update()
        {
            if (Opened)
            {
                if (_t < 1f)
                {
                    _t = Mathf.MoveTowards(_t, 1f, Time.deltaTime / seconds);
                    // heavy: starts slow, a small jolt at the end
                    float k = Mathf.SmoothStep(0f, 1f, _t);
                    transform.localPosition = _rest + slide * k;
                }
                return;
            }
            if (building == null || Underground.Current != building || Underground.Busy) return;
            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;
            Vector3 d = building.transform.TransformPoint(promptPoint) - hero.position;
            d.y = 0f;
            if (d.sqrMagnitude > reach * reach) return;
            MapLoot.SetPrompt(promptText);
            if (!DioramaInput.InteractPressed()) return;
            Open();
        }

        /// <summary>Pushes the wardrobe aside and shows the hidden room.</summary>
        public void Open()
        {
            if (Opened) return;
            Opened = true;
            if (building != null && building.secretRoot != null) building.secretRoot.SetActive(true);
            MapWorld.Banner(revealText, 3.5f);
        }
    }
}
