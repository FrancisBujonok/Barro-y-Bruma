using System.Collections.Generic;
using BrumaPx.Data;
using BrumaPx.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.UI
{
    /// <summary>
    /// Two bars under the health panel: Resistance (what stands between the hero and the Bruma) and Bruma (the
    /// average of the body, so it moves slowly). Also a wound mark, the worst zone and the last two messages of the
    /// body. Built at run time. The full picture is in the body window (B).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BrumaBars : MonoBehaviour
    {
        public HeroBody body;
        [Tooltip("The UI canvas that receives the bars.")]
        public Transform canvasRoot;

        const int BarSteps = 20;
        const float NoteSeconds = 4f;
        static readonly Color ResistColor = new Color32(0x6F, 0x86, 0xA8, 255);
        static readonly Color BrumaColor = new Color32(0x8E, 0x6A, 0xA8, 255);
        static readonly Color BrumaWarn = new Color32(0xB2, 0x46, 0x3B, 255);
        static readonly Color WoundColor = new Color32(0xC9, 0x55, 0x4A, 255);

        RectTransform _resFill, _brumaFill;
        Image _brumaImage;
        Text _woundLabel, _worstLabel, _notes;
        readonly List<string> _lines = new List<string>();
        readonly List<float> _left = new List<float>();
        bool _built, _notesDirty;

        void Start()
        {
            if (canvasRoot == null || body == null) return;
            Build();
            body.Notice += OnNotice;
        }

        void OnDestroy()
        {
            if (body != null) body.Notice -= OnNotice;
        }

        void Build()
        {
            var panel = UiKit.Img(canvasRoot, "Bruma HUD", new Color32(0x1B, 0x17, 0x14, 190));
            UiKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -90f), new Vector2(268f, 84f));

            Label(panel.transform, "Resistencia", 8f, new Vector2(10f, -8f), new Vector2(76f, 18f));
            _resFill = Bar(panel.transform, "Resistencia", new Vector2(86f, -9f), new Vector2(110f, 14f), ResistColor, out _);

            Label(panel.transform, "Bruma", 8f, new Vector2(10f, -30f), new Vector2(76f, 18f));
            _brumaFill = Bar(panel.transform, "Bruma", new Vector2(86f, -33f), new Vector2(110f, 10f), BrumaColor, out _brumaImage);

            _woundLabel = UiKit.Txt(panel.transform, "Herida", 13, FontStyle.Bold, WoundColor, TextAnchor.MiddleLeft);
            UiKit.Place(_woundLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -8f), new Vector2(58f, 18f));

            _worstLabel = UiKit.Txt(panel.transform, "Peor", 13, FontStyle.Bold, UiKit.Mid, TextAnchor.MiddleLeft);
            UiKit.Place(_worstLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -30f), new Vector2(58f, 18f));

            _notes = UiKit.Txt(panel.transform, "Avisos", 12, FontStyle.Normal, UiKit.Warm, TextAnchor.UpperLeft);
            UiKit.Place(_notes.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -52f), new Vector2(250f, 30f));
            _notes.text = string.Empty;
            _built = true;
        }

        static void Label(Transform parent, string text, float x, Vector2 pos, Vector2 size)
        {
            var l = UiKit.Txt(parent, text + " Label", 13, FontStyle.Bold, UiKit.Cream, TextAnchor.MiddleLeft);
            UiKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            l.text = text;
        }

        static RectTransform Bar(Transform parent, string name, Vector2 pos, Vector2 size, Color color, out Image fillImage)
        {
            var back = UiKit.Img(parent, name + " Back", UiKit.Border);
            UiKit.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var inner = UiKit.Img(back.transform, "Inner", new Color32(0x1B, 0x17, 0x14, 255));
            UiKit.Stretch(inner.rectTransform, 2f, 2f, 2f, 2f);
            var fill = UiKit.Img(inner.transform, "Fill", color);
            UiKit.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            fillImage = fill;
            return fill.rectTransform;
        }

        static void SetFill(RectTransform fill, float value)
        {
            float v = Mathf.Ceil(Mathf.Clamp01(value) * BarSteps) / BarSteps;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(v, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        void OnNotice(string text)
        {
            _lines.Add(text);
            _left.Add(NoteSeconds);
            while (_lines.Count > 2)
            {
                _lines.RemoveAt(0);
                _left.RemoveAt(0);
            }
            _notesDirty = true;
        }

        void Update()
        {
            if (!_built || body == null) return;

            SetFill(_resFill, body.Resistance01);
            SetFill(_brumaFill, body.Overall01);

            int worst = body.WorstZone;
            BrumaStage stage = worst >= 0 ? body.Stage(worst) : BrumaStage.Clean;
            _brumaImage.color = stage >= BrumaStage.Infected ? BrumaWarn : BrumaColor;
            _worstLabel.text = worst >= 0 ? Mathf.RoundToInt(body.Level(worst)) + "%" : string.Empty;
            _worstLabel.color = stage >= BrumaStage.Infected ? BrumaWarn : UiKit.Mid;
            int wounds = body.OpenWounds;
            _woundLabel.text = wounds > 0 ? (wounds > 1 ? "Herida x" + wounds : "Herida") : string.Empty;

            float dt = Time.unscaledDeltaTime;
            for (int i = _left.Count - 1; i >= 0; i--)
            {
                _left[i] -= dt;
                if (_left[i] > 0f) continue;
                _lines.RemoveAt(i);
                _left.RemoveAt(i);
                _notesDirty = true;
            }
            if (_notesDirty)
            {
                _notes.text = string.Join("\n", _lines.ToArray());
                _notesDirty = false;
            }
        }
    }
}
