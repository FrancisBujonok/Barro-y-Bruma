using BrumaPx.Atmosphere;
using BrumaPx.Combat;
using BrumaPx.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.UI
{
    /// <summary>
    /// Health and stamina bars plus the light icon. The icon is a small eye drawn in code (16x16, no art files):
    /// closed in the dark, half open in the dim, wide open when lit, so the player can tell at a glance whether
    /// enemies will notice them. Bars move in 20 steps to keep the pixel look.
    /// </summary>
    public sealed class HeroHud : MonoBehaviour
    {
        [Header("Sources")]
        public Health health;
        public HeroCombat combat;
        public HeroVisibility visibility;

        [Header("Widgets")]
        public RectTransform healthFill;
        public RectTransform staminaFill;
        public Image staminaFillImage;
        public RawImage lightIcon;
        public Text lightLabel;
        public Text message;

        const int BarSteps = 20;
        static readonly Color Dark = new Color32(0x1B, 0x17, 0x14, 255);
        static readonly Color HiddenColor = new Color32(0x6F, 0x86, 0xA8, 255);
        static readonly Color DimColor = new Color32(0x9A, 0x96, 0x8C, 255);
        static readonly Color ExposedColor = new Color32(0xE0, 0xA2, 0x5F, 255);
        static readonly Color StaminaColor = new Color32(0xCD, 0xBB, 0x94, 255);
        static readonly Color ExhaustedColor = new Color32(0xA8, 0x3A, 0x32, 255);

        Texture2D[] _eyes;
        float _messageLeft;
        float _exhaustedLeft;
        VisibilityState _shown = (VisibilityState)(-1);

        void Awake()
        {
            _eyes = new[]
            {
                BuildEye(0f, HiddenColor, false),
                BuildEye(0.55f, DimColor, true),
                BuildEye(1f, ExposedColor, true)
            };
            if (message != null) message.text = string.Empty;
        }

        void OnEnable()
        {
            if (combat == null) return;
            combat.Announce += OnAnnounce;
            combat.Exhausted += OnExhausted;
        }

        void OnDisable()
        {
            if (combat == null) return;
            combat.Announce -= OnAnnounce;
            combat.Exhausted -= OnExhausted;
        }

        void OnDestroy()
        {
            if (_eyes == null) return;
            for (int i = 0; i < _eyes.Length; i++) if (_eyes[i] != null) Destroy(_eyes[i]);
        }

        void OnAnnounce(string text)
        {
            if (message == null) return;
            message.text = text;
            _messageLeft = string.IsNullOrEmpty(text) ? 0f : 2.2f;
        }

        void OnExhausted()
        {
            _exhaustedLeft = 0.35f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (health != null && healthFill != null)
                SetFill(healthFill, health.Current / Mathf.Max(1f, health.max));
            if (combat != null && staminaFill != null)
                SetFill(staminaFill, combat.Stamina01);

            if (_exhaustedLeft > 0f) _exhaustedLeft -= dt;
            if (staminaFillImage != null)
                staminaFillImage.color = (_exhaustedLeft > 0f && ((int)(Time.unscaledTime * 12f) & 1) == 0) ? ExhaustedColor : StaminaColor;

            if (_messageLeft > 0f)
            {
                _messageLeft -= dt;
                if (_messageLeft <= 0f && message != null) message.text = string.Empty;
            }

            if (visibility != null && visibility.State != _shown)
            {
                _shown = visibility.State;
                if (lightIcon != null) lightIcon.texture = _eyes[(int)_shown];
                if (lightLabel != null)
                {
                    lightLabel.text = _shown == VisibilityState.Hidden ? "Oculto" : (_shown == VisibilityState.Dim ? "Penumbra" : "Expuesto");
                    lightLabel.color = _shown == VisibilityState.Hidden ? HiddenColor : (_shown == VisibilityState.Dim ? DimColor : ExposedColor);
                }
            }
        }

        static void SetFill(RectTransform fill, float value)
        {
            float v = Mathf.Ceil(Mathf.Clamp01(value) * BarSteps) / BarSteps;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(v, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        // A 16x16 eye. open: 0 = closed (a line), 1 = wide. The outline takes the state colour.
        static Texture2D BuildEye(float open, Color outline, bool iris)
        {
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Color white = new Color32(0xC4, 0xBF, 0xB2, 255);
            Color pupil = Dark;
            float c = (n - 1) * 0.5f;

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float nx = (x - c) / c;
                    float half = open * 5.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx));
                    float dy = Mathf.Abs(y - c);

                    if (open <= 0.001f)
                    {
                        // Closed eye: a gently curved line with two small lashes.
                        float curve = c - 1.5f + 2.2f * nx * nx;
                        if (Mathf.Abs(nx) < 0.95f && Mathf.Abs(y - curve) < 0.6f) px[y * n + x] = outline;
                        continue;
                    }

                    if (dy > half) continue;
                    bool edge = dy > half - 1f;
                    if (edge) { px[y * n + x] = outline; continue; }

                    px[y * n + x] = white;
                    if (iris)
                    {
                        float dx = x - c, dyy = y - c;
                        float d2 = dx * dx + dyy * dyy;
                        if (d2 <= 1.5f) px[y * n + x] = pupil;
                        else if (d2 <= 7f) px[y * n + x] = outline;
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
