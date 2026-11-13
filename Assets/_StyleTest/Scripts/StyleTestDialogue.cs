using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.StyleTest
{
    /// <summary>Dialogue box with a portrait on the left and a typewriter effect. E / Space / Enter advance, Esc closes.</summary>
    public sealed class StyleTestDialogue : MonoBehaviour
    {
        public GameObject panel;
        public Text nameText;
        public Text bodyText;
        public Text hintText;
        public RawImage portraitImage;
        public float charsPerSecond = 38f;

        StyleTestNpc _npc;
        int _line;
        float _shown;
        string _full = "";
        int _openedFrame = -1;

        public bool IsOpen { get; private set; }
        /// <summary>Frame the box last closed on, so the key press that closed it does not reopen it.</summary>
        public int ClosedFrame { get; private set; } = -1;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Open(StyleTestNpc npc)
        {
            if (npc == null || npc.lines == null || npc.lines.Length == 0) return;
            _npc = npc;
            _line = 0;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            if (panel != null) panel.SetActive(true);
            if (nameText != null) nameText.text = npc.displayName;
            if (portraitImage != null) portraitImage.texture = npc.portrait;
            StartLine();
        }

        public void Close()
        {
            IsOpen = false;
            ClosedFrame = Time.frameCount;
            _npc = null;
            if (panel != null) panel.SetActive(false);
        }

        void StartLine()
        {
            _full = _npc.lines[_line];
            _shown = 0f;
            Render();
        }

        void Render()
        {
            if (bodyText == null) return;
            int n = Mathf.Clamp(Mathf.FloorToInt(_shown), 0, _full.Length);
            bodyText.text = _full.Substring(0, n);
        }

        void Update()
        {
            if (!IsOpen) return;

            bool typing = _shown < _full.Length;
            if (typing)
            {
                _shown = Mathf.Min(_full.Length, _shown + charsPerSecond * Time.unscaledDeltaTime);
                Render();
            }

            if (hintText != null)
            {
                bool last = _line >= _npc.lines.Length - 1;
                hintText.text = typing ? "E  saltar" : (last ? "E  cerrar" : "E  continuar");
                Color c = hintText.color;
                c.a = typing ? 0.55f : 0.55f + 0.45f * (Mathf.Sin(Time.unscaledTime * 5f) * 0.5f + 0.5f);
                hintText.color = c;
            }

            if (Time.frameCount == _openedFrame) return;
            if (StyleTestInput.CancelPressed()) { Close(); return; }
            if (!StyleTestInput.AdvancePressed()) return;

            if (typing)
            {
                _shown = _full.Length;
                Render();
                return;
            }

            _line++;
            if (_line >= _npc.lines.Length) Close();
            else StartLine();
        }
    }
}
