using System;
using System.Collections.Generic;
using System.Text;
using BrumaPx.Combat;
using BrumaPx.Data;
using BrumaPx.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.UI
{
    /// <summary>
    /// The body window (B). A silhouette made of the zones of the body profile, each coloured by how far the Bruma
    /// has gone in it, with a red mark where a wound is open. Arrows pick a zone; the right side says what that zone
    /// takes away (every effect, how much now and the most it can take) and below it everything the whole body has
    /// lost so far. Opening it pauses the game. The look is a placeholder: blocks, no art yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BodyPanel : MonoBehaviour
    {
        [Header("Links")]
        public HeroBody body;
        public HeroCombat combat;
        public Health health;
        [Tooltip("The UI canvas that receives the window.")]
        public Transform canvasRoot;

        const float Area = 330f;
        const float RightX = 372f;
        const float RightW = 400f;

        sealed class ZoneView
        {
            public Image frame, fill, wound;
            public RectTransform rt;
            public Vector2 spot;
        }

        static readonly Color PageColor = new Color32(0x24, 0x1E, 0x19, 255);
        static readonly Color Blank = new Color32(0x3A, 0x33, 0x2B, 255);
        static readonly Color InfectedColor = new Color32(0xD0, 0x8A, 0x3E, 255);
        static readonly Color TakenColor = new Color32(0xB2, 0x46, 0x3B, 255);
        static readonly Color WoundColor = new Color32(0xC9, 0x55, 0x4A, 255);

        bool _open;
        int _selected;
        GameObject _panel;
        RectTransform _zoneBarFill;
        Text _zoneTitle, _zoneStage, _zoneNote, _effects, _totalsHead, _totals, _general, _actions;
        Inventory _inventory;
        Equipment _equipment;
        string _flash = string.Empty;
        float _flashUntil;
        readonly List<ZoneView> _zones = new List<ZoneView>();
        readonly StringBuilder _sb = new StringBuilder();

        public bool IsOpen { get { return _open; } }

        void Start()
        {
            if (canvasRoot == null || body == null || body.Count == 0) return;
            _inventory = body.GetComponent<Inventory>();
            _equipment = body.GetComponent<Equipment>();
            BuildPanel();
        }

        void OnDisable()
        {
            if (_open) Close();
        }

        void Update()
        {
            if (_panel == null || combat == null || health == null) return;

            if (!_open)
            {
                bool pressed = DioramaInput.BodyPressed();
                if (pressed && !Inventory.MenuOpen && !health.IsDead && !Talking()) Open();
                return;
            }

            if (DioramaInput.BodyPressed() || DioramaInput.CancelPressed() || health.IsDead)
            {
                Close();
                return;
            }

            Time.timeScale = 0f;
            int treat = DioramaInput.TreatPressed();
            if (treat >= 0) Treat(treat);
            Vector2Int step = DioramaInput.MenuStep();
            if (step != Vector2Int.zero) MoveSelection(step);
            Refresh();
        }

        bool Talking()
        {
            return combat.hero != null && combat.hero.dialogue != null && combat.hero.dialogue.IsOpen;
        }

        void Open()
        {
            _open = true;
            Inventory.MenuOpen = true;
            combat.InputLocked = true;
            Time.timeScale = 0f;
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            int worst = body.WorstZone;
            _selected = worst >= 0 ? worst : Mathf.Max(0, body.IndexOf("torso"));
            Refresh();
        }

        void Close()
        {
            _open = false;
            Inventory.MenuOpen = false;
            combat.InputLocked = false;
            Time.timeScale = 1f;
            if (_panel != null) _panel.SetActive(false);
        }

        // ------------------------------------------------------------------------------------------ layout

        void BuildPanel()
        {
            var bg = UiKit.Img(canvasRoot, "Body Window", UiKit.Border);
            _panel = bg.gameObject;
            UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(792f, 476f));
            var inner = UiKit.Img(bg.transform, "Paper", UiKit.PanelBg);
            UiKit.Stretch(inner.rectTransform, 3f, 3f, 3f, 3f);

            var title = UiKit.Txt(inner.transform, "Title", 20, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            TopLeft(title.rectTransform, new Vector2(18f, -10f), new Vector2(300f, 28f));
            title.text = "Cuerpo";

            var figure = UiKit.Img(inner.transform, "Figure", PageColor);
            TopLeft(figure.rectTransform, new Vector2(18f, -46f), new Vector2(Area, Area));
            for (int i = 0; i < body.Count; i++) BuildZone(figure.transform, body.Zone(i));

            _zoneTitle = UiKit.Txt(inner.transform, "Zone Title", 20, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            TopLeft(_zoneTitle.rectTransform, new Vector2(RightX, -12f), new Vector2(RightW, 28f));
            _zoneStage = UiKit.Txt(inner.transform, "Zone Stage", 15, FontStyle.Bold, UiKit.Cream, TextAnchor.UpperLeft);
            TopLeft(_zoneStage.rectTransform, new Vector2(RightX, -42f), new Vector2(RightW, 22f));

            var back = UiKit.Img(inner.transform, "Zone Bar Back", UiKit.Border);
            TopLeft(back.rectTransform, new Vector2(RightX, -68f), new Vector2(300f, 14f));
            var barInner = UiKit.Img(back.transform, "Inner", new Color32(0x1B, 0x17, 0x14, 255));
            UiKit.Stretch(barInner.rectTransform, 2f, 2f, 2f, 2f);
            var fill = UiKit.Img(barInner.transform, "Fill", UiKit.Warm);
            UiKit.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            _zoneBarFill = fill.rectTransform;
            if (body.tuning != null)
            {
                AddMark(barInner.transform, body.tuning.InfectedAt / 100f);
                AddMark(barInner.transform, body.tuning.TakenAt / 100f);
            }

            _zoneNote = UiKit.Txt(inner.transform, "Zone Note", 14, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperLeft);
            TopLeft(_zoneNote.rectTransform, new Vector2(RightX, -90f), new Vector2(RightW, 20f));

            _effects = UiKit.Txt(inner.transform, "Effects", 14, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperLeft);
            _effects.supportRichText = true;
            TopLeft(_effects.rectTransform, new Vector2(RightX, -116f), new Vector2(RightW, 110f));

            _totalsHead = UiKit.Txt(inner.transform, "Totals Head", 16, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            TopLeft(_totalsHead.rectTransform, new Vector2(RightX, -236f), new Vector2(RightW, 22f));
            _totalsHead.text = "Todo el cuerpo";
            _totals = UiKit.Txt(inner.transform, "Totals", 14, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperLeft);
            TopLeft(_totals.rectTransform, new Vector2(RightX, -260f), new Vector2(RightW, 100f));

            _general = UiKit.Txt(inner.transform, "General", 14, FontStyle.Bold, UiKit.Cream, TextAnchor.UpperLeft);
            TopLeft(_general.rectTransform, new Vector2(RightX, -366f), new Vector2(RightW, 22f));

            _actions = UiKit.Txt(inner.transform, "Actions", 14, FontStyle.Bold, UiKit.Cream, TextAnchor.UpperLeft);
            _actions.supportRichText = true;
            TopLeft(_actions.rectTransform, new Vector2(18f, -388f), new Vector2(756f, 40f));

            var footer = UiKit.Txt(inner.transform, "Footer", 13, FontStyle.Normal, UiKit.Mid, TextAnchor.UpperLeft);
            TopLeft(footer.rectTransform, new Vector2(18f, -430f), new Vector2(756f, 30f));
            footer.text = "Flechas: elegir zona   1: vendar   2: ungüento   3: cura   B o Esc: cerrar\nLa Bruma entra por el aire, los golpes de infectados, las heridas abiertas y el contacto con cuerpos o aguas turbias.";

            _panel.SetActive(false);
        }

        static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
        }

        static void AddMark(Transform bar, float at)
        {
            var mark = UiKit.Img(bar, "Mark", UiKit.Cream);
            var rt = mark.rectTransform;
            rt.anchorMin = new Vector2(at, 0f);
            rt.anchorMax = new Vector2(at, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(2f, 0f);
        }

        void BuildZone(Transform figure, BodyZone zone)
        {
            var v = new ZoneView();
            v.spot = zone.center;
            v.frame = UiKit.Img(figure, "Zone " + zone.id, UiKit.Border);
            v.rt = v.frame.rectTransform;
            v.rt.anchorMin = v.rt.anchorMax = new Vector2(0f, 1f);
            v.rt.pivot = new Vector2(0.5f, 0.5f);
            v.rt.anchoredPosition = new Vector2(zone.center.x * Area, -(1f - zone.center.y) * Area);
            v.rt.sizeDelta = new Vector2(zone.extent.x * Area, zone.extent.y * Area);
            v.fill = UiKit.Img(v.frame.transform, "Fill", Blank);
            UiKit.Stretch(v.fill.rectTransform, 2f, 2f, 2f, 2f);
            v.wound = UiKit.Img(v.frame.transform, "Wound", WoundColor);
            var wrt = v.wound.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(1f, 1f);
            wrt.pivot = new Vector2(1f, 1f);
            wrt.anchoredPosition = new Vector2(-3f, -3f);
            wrt.sizeDelta = new Vector2(9f, 9f);
            v.wound.gameObject.SetActive(false);
            _zones.Add(v);
        }

        // ------------------------------------------------------------------------------------------ state

        static Color StageColor(BrumaStage s)
        {
            switch (s)
            {
                case BrumaStage.Exposed: return UiKit.Warm;
                case BrumaStage.Infected: return InfectedColor;
                case BrumaStage.Taken: return TakenColor;
                default: return UiKit.Mid;
            }
        }

        static string Signed(int v)
        {
            return (v > 0 ? "+" : string.Empty) + v + "%";
        }

        void Refresh()
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                var v = _zones[i];
                float lv = body.Level(i);
                bool sel = i == _selected;
                v.frame.color = sel ? UiKit.Warm : UiKit.Border;
                v.fill.color = lv <= 0f ? Blank : Color.Lerp(Blank, StageColor(body.Stage(i)), Mathf.Clamp01(0.3f + lv / 100f * 0.7f));
                v.rt.localScale = sel ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
                WoundState ws = body.Wound(i);
                v.wound.gameObject.SetActive(ws != WoundState.None);
                v.wound.color = WoundMarker(ws);
            }

            if (_selected < 0 || _selected >= body.Count) return;
            BodyZone zone = body.Zone(_selected);
            float level = body.Level(_selected);
            BrumaStage stage = body.Stage(_selected);

            _zoneTitle.text = zone.displayName;
            _zoneStage.text = BodyStatInfo.StageName(stage) + "   " + Mathf.RoundToInt(level) + "%";
            _zoneStage.color = StageColor(stage);
            _zoneBarFill.anchorMin = Vector2.zero;
            _zoneBarFill.anchorMax = new Vector2(Mathf.Clamp01(level / 100f), 1f);
            _zoneBarFill.offsetMin = Vector2.zero;
            _zoneBarFill.offsetMax = Vector2.zero;
            _zoneBarFill.GetComponent<Image>().color = level <= 0f ? UiKit.Mid : StageColor(stage);

            UpdateWoundNote();
            

            _sb.Length = 0;
            var fx = zone.effects;
            if (fx == null || fx.Length == 0) _sb.Append("Esta zona no quita nada.");
            for (int e = 0; fx != null && e < fx.Length; e++)
            {
                string label = BodyStatInfo.Label(fx[e].stat);
                int most = Mathf.RoundToInt(fx[e].atFull * 100f);
                if (level < fx[e].startsAt)
                {
                    _sb.Append("<color=#9A968C>").Append(label).Append("   desde el ")
                        .Append(Mathf.RoundToInt(fx[e].startsAt)).Append("% (máx ").Append(Signed(most)).Append(")</color>");
                }
                else
                {
                    int now = Mathf.RoundToInt(fx[e].atFull * 100f * HeroBody.Strength(fx[e], level));
                    _sb.Append(label).Append("   ").Append(Signed(now)).Append("  (máx ").Append(Signed(most)).Append(")");
                }
                _sb.Append('\n');
            }
            AppendCover(_sb, _selected);
            _effects.text = _sb.ToString();

            _sb.Length = 0;
            int lines = 0, hidden = 0;
            for (int s = 0; s < BodyStatInfo.Count; s++)
            {
                int pct = Mathf.RoundToInt((body.Mult((BodyStat)s) - 1f) * 100f);
                if (pct == 0) continue;
                if (lines >= 6) { hidden++; continue; }
                _sb.Append(BodyStatInfo.Label((BodyStat)s)).Append("   ").Append(Signed(pct)).Append('\n');
                lines++;
            }
            if (hidden > 0) _sb.Append("y ").Append(hidden).Append(" más...");
            if (lines == 0) _sb.Append("Todavía no has perdido nada.");
            _totals.text = _sb.ToString();

            _sb.Length = 0;
            _sb.Append("Resistencia ").Append(Mathf.RoundToInt(body.Resistance01 * 100f)).Append("%");
            if (body.PalliateLeft > 0f) _sb.Append("    Paliativo ").Append(Mathf.CeilToInt(body.PalliateLeft)).Append(" s");
            if (body.OpenWounds > 0) _sb.Append("    Heridas abiertas ").Append(body.OpenWounds);
            _general.text = _sb.ToString();
            UpdateActions();
        }

        // ------------------------------------------------------------------------------------------ treatment

        void Flash(string text)
        {
            _flash = text;
            _flashUntil = Time.unscaledTime + 2.5f;
        }

        // 1 bandage, 2 salve, 3 cure: used on the selected zone with what is in the belt or the bag.
        void Treat(int kind)
        {
            if (_inventory == null || _selected < 0 || _selected >= body.Count) return;
            Func<ItemDef, bool> match;
            bool can;
            string lacks, cannot;
            if (kind == 0)
            {
                match = delegate (ItemDef i) { return i.ClosesWound; };
                can = body.CanBandage(_selected);
                lacks = "No te quedan vendas";
                cannot = "Esa zona no tiene una herida que vendar";
            }
            else if (kind == 1)
            {
                match = delegate (ItemDef i) { return i.Disinfects; };
                can = body.CanSalve(_selected);
                lacks = "No te queda ungüento";
                cannot = "Esa zona no tiene herida abierta ni Bruma";
            }
            else
            {
                match = delegate (ItemDef i) { return i.Effect == ItemEffect.CureZone; };
                can = body.CanCure(_selected);
                lacks = "No te queda nada para curar";
                cannot = "Esa zona no tiene Bruma";
            }
            if (!can)
            {
                Flash(cannot);
                return;
            }
            ItemDef used;
            if (!_inventory.TakeOne(match, out used))
            {
                Flash(lacks);
                return;
            }
            if (kind == 0) body.Bandage(_selected);
            else if (kind == 1) body.Salve(_selected, body.SalveRelief);
            else body.Cure(_selected, used.Amount);
            Flash(body.Zone(_selected).displayName + ": hecho");
        }

        void UpdateActions()
        {
            int bandages = _inventory != null ? _inventory.CountWhere(delegate (ItemDef i) { return i.ClosesWound; }) : 0;
            int salves = _inventory != null ? _inventory.CountWhere(delegate (ItemDef i) { return i.Disinfects; }) : 0;
            int cures = _inventory != null ? _inventory.CountWhere(delegate (ItemDef i) { return i.Effect == ItemEffect.CureZone; }) : 0;
            _sb.Length = 0;
            AppendAction(_sb, "1 Vendar", bandages, body.CanBandage(_selected));
            _sb.Append("    ");
            AppendAction(_sb, "2 Ungüento", salves, body.CanSalve(_selected));
            _sb.Append("    ");
            AppendAction(_sb, "3 Curar", cures, body.CanCure(_selected));
            if (!string.IsNullOrEmpty(_flash) && Time.unscaledTime < _flashUntil)
                _sb.Append("\n<color=#D9A441>").Append(_flash).Append("</color>");
            _actions.text = _sb.ToString();
        }

        static void AppendAction(StringBuilder sb, string label, int count, bool can)
        {
            bool on = can && count > 0;
            if (!on) sb.Append("<color=#9A968C>");
            sb.Append(label).Append(" (x").Append(count).Append(')');
            if (!on) sb.Append("</color>");
        }

        void UpdateWoundNote()
        {
            switch (body.Wound(_selected))
            {
                case WoundState.Open:
                    _zoneNote.text = "Herida abierta: la Bruma entra mucho más rápido";
                    _zoneNote.color = WoundColor;
                    break;
                case WoundState.Cleaned:
                    _zoneNote.text = "Herida limpia: entra algo de Bruma. Falta vendarla";
                    _zoneNote.color = InfectedColor;
                    break;
                case WoundState.Bandaged:
                    _zoneNote.text = "Vendada: sellada " + Mathf.CeilToInt(body.BandageLeft(_selected)) + " s";
                    _zoneNote.color = UiKit.Cream;
                    break;
                default:
                    _zoneNote.text = "Sin herida";
                    _zoneNote.color = UiKit.Mid;
                    break;
            }
        }

        static Color WoundMarker(WoundState s)
        {
            switch (s)
            {
                case WoundState.Cleaned: return InfectedColor;
                case WoundState.Bandaged: return UiKit.Cream;
                default: return WoundColor;
            }
        }

        void AppendCover(StringBuilder sb, int zone)
        {
            if (_equipment == null) return;
            ArmorDef piece = _equipment.PieceCovering(body.Zone(zone).id);
            sb.Append('\n');
            if (piece == null) sb.Append("<color=#9A968C>Sin prenda sobre esta zona</color>");
            else sb.Append(piece.DisplayName).Append("   detiene la Bruma ").Append(Mathf.RoundToInt(body.Cover(zone) * 100f)).Append("%");
        }

        void MoveSelection(Vector2Int step)
        {
            if (_zones.Count == 0) return;
            Vector2 dir = new Vector2(step.x, step.y).normalized;
            Vector2 here = _zones[_selected].spot;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _zones.Count; i++)
            {
                if (i == _selected) continue;
                Vector2 d = _zones[i].spot - here;
                float dist = d.magnitude;
                if (dist < 0.0001f) continue;
                float dot = Vector2.Dot(d / dist, dir);
                if (dot < 0.3f) continue;
                float score = dist / dot;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0) _selected = best;
        }
    }
}
