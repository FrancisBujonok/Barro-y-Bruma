using System.Collections.Generic;
using BrumaPx.World;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.UI
{
    /// <summary>
    /// The small window that opens when something is searched: its name, four slots with what was found (icon,
    /// count) and one line below ("No hay nada", what you took, what did not fit). Closes by itself after a few
    /// seconds or when the hero walks away. Built from code on the HUD canvas the first time it is needed.
    /// </summary>
    public sealed class LootWindow : MonoBehaviour
    {
        public struct Entry
        {
            public string name;
            public Color tint;
            public int count;
            public bool notFit;

            public Entry(string name, Color tint, int count, bool notFit)
            {
                this.name = name;
                this.tint = tint;
                this.count = count;
                this.notFit = notFit;
            }
        }

        const int Slots = 4;
        const float SlotSize = 52f, Gap = 8f, Seconds = 4.5f, WalkAway = 2.6f;

        static LootWindow _instance;

        sealed class Slot
        {
            public Image frame, icon;
            public Text glyph, count, cross;
        }

        Text _title, _line;
        readonly Slot[] _slots = new Slot[Slots];
        float _until;
        Vector3 _origin;

        public static bool IsOpen { get { return _instance != null && _instance.gameObject.activeSelf; } }

        public static void Show(string title, List<Entry> entries, Vector3 origin)
        {
            if (_instance == null && !Build()) return;
            _instance.Fill(title, entries);
            _instance._origin = origin;
            _instance._until = Time.unscaledTime + Seconds;
            _instance.gameObject.SetActive(true);
        }

        static bool Build()
        {
            Transform canvas = null;
            var belt = FindFirstObjectByType<BeltHud>();
            if (belt != null) canvas = belt.canvasRoot;
            if (canvas == null)
            {
                var c = FindFirstObjectByType<Canvas>();
                if (c != null) canvas = c.transform;
            }
            if (canvas == null) return false;

            float width = Slots * SlotSize + (Slots - 1) * Gap + 28f;
            var border = UiKit.Img(canvas, "Loot Window", UiKit.Border);
            UiKit.Place(border.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(width, 116f));
            var bg = UiKit.Img(border.transform, "Inner", UiKit.PanelBg);
            UiKit.Stretch(bg.rectTransform, 2f, 2f, 2f, 2f);
            var w = border.gameObject.AddComponent<LootWindow>();

            w._title = UiKit.Txt(bg.transform, "Title", 16, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperCenter);
            UiKit.Place(w._title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(width - 20f, 20f));

            float x0 = -((Slots * SlotSize + (Slots - 1) * Gap) / 2f);
            for (int i = 0; i < Slots; i++)
            {
                var s = new Slot();
                s.frame = UiKit.Img(bg.transform, "Slot " + i, UiKit.Border);
                UiKit.Place(s.frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(x0 + i * (SlotSize + Gap), -30f), new Vector2(SlotSize, SlotSize));
                var inner = UiKit.Img(s.frame.transform, "Inner", new Color32(0x24, 0x1F, 0x1B, 255));
                UiKit.Stretch(inner.rectTransform, 2f, 2f, 2f, 2f);
                s.icon = UiKit.Img(inner.transform, "Icon", Color.white);
                UiKit.Place(s.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
                s.glyph = UiKit.Txt(s.icon.transform, "Glyph", 20, FontStyle.Bold, new Color32(0x1B, 0x17, 0x14, 255), TextAnchor.MiddleCenter);
                UiKit.Stretch(s.glyph.rectTransform, 0f, 0f, 0f, 0f);
                s.count = UiKit.Txt(inner.transform, "Count", 12, FontStyle.Bold, UiKit.Cream, TextAnchor.LowerRight);
                UiKit.Place(s.count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-3f, 1f), new Vector2(30f, 14f));
                s.cross = UiKit.Txt(inner.transform, "Cross", 26, FontStyle.Bold, new Color32(0xB2, 0x4A, 0x36, 255), TextAnchor.MiddleCenter);
                UiKit.Stretch(s.cross.rectTransform, 0f, 0f, 0f, 0f);
                s.cross.text = "x";
                w._slots[i] = s;
            }

            w._line = UiKit.Txt(bg.transform, "Line", 13, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperCenter);
            UiKit.Place(w._line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(width - 16f, 30f));
            _instance = w;
            border.gameObject.SetActive(false);
            return true;
        }

        void Fill(string title, List<Entry> entries)
        {
            _title.text = title;
            var got = new List<string>();
            var lost = new List<string>();
            for (int i = 0; i < Slots; i++)
            {
                var s = _slots[i];
                bool has = i < entries.Count;
                s.icon.gameObject.SetActive(has);
                s.count.text = string.Empty;
                s.cross.gameObject.SetActive(false);
                s.frame.color = has ? UiKit.Border : new Color32(0x3D, 0x33, 0x2B, 255);
                if (!has) continue;
                var e = entries[i];
                s.icon.color = e.notFit ? Color.Lerp(e.tint, Color.black, 0.5f) : e.tint;
                s.glyph.text = string.IsNullOrEmpty(e.name) ? "?" : e.name.Substring(0, 1).ToUpperInvariant();
                s.count.text = e.count > 1 ? e.count.ToString() : string.Empty;
                s.cross.gameObject.SetActive(e.notFit);
            }
            foreach (var e in entries) (e.notFit ? lost : got).Add(e.name + (e.count > 1 ? " x" + e.count : string.Empty));
            if (entries.Count == 0) _line.text = "No hay nada.";
            else
            {
                string t = got.Count > 0 ? string.Join(", ", got.ToArray()) : string.Empty;
                if (lost.Count > 0) t += (t.Length > 0 ? "\n" : string.Empty) + "No te cabe: " + string.Join(", ", lost.ToArray());
                _line.text = t;
            }
        }

        void Update()
        {
            var hero = DioramaHeroCache.Hero;
            bool away = false;
            if (hero != null)
            {
                Vector3 d = hero.position - _origin;
                d.y = 0f;
                away = d.sqrMagnitude > WalkAway * WalkAway;
            }
            if (Time.unscaledTime > _until || away) gameObject.SetActive(false);
        }
    }
}
