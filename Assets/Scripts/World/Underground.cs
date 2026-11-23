using System;
using System.Collections;
using System.Collections.Generic;
using BrumaPx.Atmosphere;
using BrumaPx.Player;
using BrumaPx.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.World
{
    /// <summary>
    /// Going down to a basement. The basement is modelled in the same spot as the building, so the screen fades to
    /// black, everything above ground (the rest of the chunks, the building itself, the ground and the mist) is
    /// hidden, the basement is shown, it gets dark like a cave, and the screen fades back in. Coming up undoes it.
    /// Hidden objects keep their state: enemies outside simply wait.
    /// </summary>
    public sealed class Underground : MonoBehaviour
    {
        public static Building Current { get; private set; }
        public static bool Busy { get; private set; }

        static Underground _inst;
        Image _overlay;
        readonly List<GameObject> _hidden = new List<GameObject>();
        readonly List<Renderer> _hiddenRenderers = new List<Renderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Busy = false; _inst = null; }

        static Underground Inst
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("Underground");
                    _inst = go.AddComponent<Underground>();
                    var canvas = go.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 500;
                    var img = new GameObject("Fade").AddComponent<Image>();
                    img.transform.SetParent(go.transform, false);
                    img.color = new Color(0f, 0f, 0f, 0f);
                    img.raycastTarget = false;
                    var rt = img.rectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    _inst._overlay = img;
                }
                return _inst;
            }
        }

        public static void Enter(Building b, Vector3 arrive, Action done = null)
        {
            if (Busy || b == null) return;
            Inst.StartCoroutine(Inst.Go(b, true, arrive, done));
        }

        public static void Exit(Building b, Vector3 arrive, Action done = null)
        {
            if (Busy || b == null) return;
            Inst.StartCoroutine(Inst.Go(b, false, arrive, done));
        }

        IEnumerator Fade(float to, float seconds)
        {
            float from = _overlay.color.a;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            _overlay.color = new Color(0f, 0f, 0f, to);
        }

        IEnumerator Go(Building b, bool down, Vector3 arrive, Action done)
        {
            Busy = true;
            var hero = DioramaHeroCache.Hero;
            var walker = hero != null ? hero.GetComponent<DioramaHero>() : null;
            if (walker != null) walker.MovementLocked = true;

            yield return new WaitForSecondsRealtime(down ? 0.25f : 0.05f);
            yield return Fade(1f, 0.35f);

            if (down)
            {
                HideWorld(b);
                b.ShowBasement(true);
                Current = b;
                DayNightWeather.ExtraDarknessTarget = 1f;
                PixelDioramaRig.FogOverride = new Color(0.035f, 0.03f, 0.035f, 1f);
            }
            else
            {
                b.ShowBasement(false);
                ShowWorld();
                Current = null;
                DayNightWeather.ExtraDarknessTarget = 0f;
                PixelDioramaRig.FogOverride = null;
            }
            if (hero != null)
            {
                hero.position = new Vector3(arrive.x, 0f, arrive.z);
                if (walker != null)
                {
                    walker.FaceTowards(hero.position + (down ? Vector3.forward : Vector3.right));
                    walker.SnapFacing();
                }
            }
            if (done != null) done();

            yield return new WaitForSecondsRealtime(0.15f);
            yield return Fade(0f, 0.4f);
            if (walker != null) walker.MovementLocked = false;
            Busy = false;
        }

        void HideWorld(Building keep)
        {
            _hidden.Clear();
            _hiddenRenderers.Clear();
            var world = FindFirstObjectByType<MapWorld>();
            if (world != null && world.chunkRoot != null)
            {
                foreach (Transform chunk in world.chunkRoot)
                {
                    foreach (Transform child in chunk)
                    {
                        if (child == keep.transform || keep.transform.IsChildOf(child)) continue;
                        if (!child.gameObject.activeSelf) continue;
                        child.gameObject.SetActive(false);
                        _hidden.Add(child.gameObject);
                    }
                }
            }
            // the ground and the mist planes live outside the chunks
            foreach (var name in new[] { "Ground", "Mist Low", "Mist High", "MistLow", "MistHigh" })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var r = go.GetComponent<Renderer>();
                if (r != null && r.enabled) { r.enabled = false; _hiddenRenderers.Add(r); }
            }
        }

        void ShowWorld()
        {
            foreach (var go in _hidden) if (go != null) go.SetActive(true);
            foreach (var r in _hiddenRenderers) if (r != null) r.enabled = true;
            _hidden.Clear();
            _hiddenRenderers.Clear();
        }
    }
}
