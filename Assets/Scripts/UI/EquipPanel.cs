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
    /// The clothing window (P). Four slots (head, torso, arms, legs): up and down pick the slot, left and right
    /// leaf through the pieces the hero owns for it, Enter or E puts one on (or takes it off). Dressing takes time and
    /// the world does not stop: the hero cannot defend himself meanwhile, and a blow cancels it and closes the window.
    /// The look is a placeholder: blocks, no art yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipPanel : MonoBehaviour
    {
        [Header("Links")]
        public Equipment equipment;
        public HeroBody body;
        public HeroCombat combat;
        public Health health;
        [Tooltip("The UI canvas that receives the window.")]
        public Transform canvasRoot;

        const float RowW = 330f;
        const float RowH = 54f;
        const float RightX = 372f;
        const float RightW = 400f;

        sealed class SlotRow
        {
            public Image frame, fill;
            public Text label, piece;
        }

        static readonly Color Blank = new Color32(0x3A, 0x33, 0x2B, 255);

        bool _open;
        int _slot, _option;
        float _lastHp, _flashUntil;
        string _flash = string.Empty;
        GameObject _panel;
        RectTransform _barFill;
        Text _optTitle, _optInfo, _action, _barLabel, _totals;
        readonly List<SlotRow> _rows = new List<SlotRow>();
        readonly List<ArmorDef> _options = new List<ArmorDef>();
        readonly StringBuilder _sb = new StringBuilder();

        public bool IsOpen { get { return _open; } }

        void Start()
        {
            if (canvasRoot == null || equipment == null) return;
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
                if (DioramaInput.EquipPressed() && !Inventory.MenuOpen && !health.IsDead && !Talking()) Open();
                return;
            }

            if (DioramaInput.EquipPressed() || DioramaInput.CancelPressed() || health.IsDead)
            {
                Close();
                return;
            }
            if (health.Current < _lastHp - 0.01f)
            {
                Close();
                return;
            }
            _lastHp = health.Current;

            Vector2Int step = DioramaInput.MenuStep();
            if (step.y != 0)
            {
                _slot = Mathf.Clamp(_slot - step.y, 0, ArmorSlotInfo.Count - 1);
                RebuildOptions(true);
            }
            else if (step.x != 0 && _options.Count > 0)
            {
                _option = (_option + step.x + _options.Count) % _options.Count;
            }
            if (DioramaInput.ConfirmPressed()) Confirm();
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
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            _lastHp = health.Current;
            _flash = string.Empty;
            RebuildOptions(true);
            Refresh();
        }

        void Close()
        {
            _open = false;
            Inventory.MenuOpen = false;
            combat.InputLocked = false;
            if (equipment != null) equipment.Cancel(null);
            if (_panel != null) _panel.SetActive(false);
        }

        void Flash(string text)
        {
            _flash = text;
            _flashUntil = Time.unscaledTime + 2.5f;
        }

        // Option 0 is "nothing"; the rest are the pieces he owns for the selected slot.
        void RebuildOptions(bool selectWorn)
        {
            _options.Clear();
            _options.Add(null);
            var wardrobe = equipment.Wardrobe;
            for (int i = 0; i < wardrobe.Count; i++)
                if (wardrobe[i].Slot == (ArmorSlot)_slot) _options.Add(wardrobe[i]);

            if (selectWorn)
            {
                ArmorDef worn = equipment.Worn((ArmorSlot)_slot);
                _option = worn != null ? Mathf.Max(0, _options.IndexOf(worn)) : 0;
            }
            else
            {
                _option = Mathf.Clamp(_option, 0, _options.Count - 1);
            }
        }

        void Confirm()
        {
            if (equipment.Busy)
            {
                Flash("Ya te estás vistiendo");
                return;
            }
            ArmorSlot slot = (ArmorSlot)_slot;
            ArmorDef pick = _options[_option];
            ArmorDef worn = equipment.Worn(slot);
            if (pick == null || pick == worn)
            {
                if (worn == null)
                {
                    Flash("No llevas nada en esa ranura");
                    return;
                }
                equipment.BeginUnequip(slot);
            }
            else
            {
                equipment.BeginEquip(pick);
            }
        }

        // ------------------------------------------------------------------------------------------ layout

        void BuildPanel()
        {
            var bg = UiKit.Img(canvasRoot, "Equip Window", UiKit.Border);
            _panel = bg.gameObject;
            UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(792f, 430f));
            var inner = UiKit.Img(bg.transform, "Paper", UiKit.PanelBg);
            UiKit.Stretch(inner.rectTransform, 3f, 3f, 3f, 3f);

            var title = UiKit.Txt(inner.transform, "Title", 20, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            TopLeft(title.rectTransform, new Vector2(18f, -10f), new Vector2(300f, 28f));
            title.text = "Prendas";

            for (int i = 0; i < ArmorSlotInfo.Count; i++) BuildRow(inner.transform, i);

            _optTitle = UiKit.Txt(inner.transform, "Option Title", 18, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            TopLeft(_optTitle.rectTransform, new Vector2(RightX, -48f), new Vector2(RightW, 26f));

            _optInfo = UiKit.Txt(inner.transform, "Option Info", 14, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperLeft);
            _optInfo.supportRichText = true;
            TopLeft(_optInfo.rectTransform, new Vector2(RightX, -78f), new Vector2(RightW, 150f));

            _action = UiKit.Txt(inner.transform, "Action", 14, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            TopLeft(_action.rectTransform, new Vector2(RightX, -236f), new Vector2(RightW, 22f));

            var back = UiKit.Img(inner.transform, "Bar Back", UiKit.Border);
            TopLeft(back.rectTransform, new Vector2(RightX, -262f), new Vector2(300f, 14f));
            var barInner = UiKit.Img(back.transform, "Inner", new Color32(0x1B, 0x17, 0x14, 255));
            UiKit.Stretch(barInner.rectTransform, 2f, 2f, 2f, 2f);
            var fill = UiKit.Img(barInner.transform, "Fill", UiKit.Warm);
            UiKit.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            _barFill = fill.rectTransform;

            _barLabel = UiKit.Txt(inner.transform, "Bar Label", 13, FontStyle.Normal, UiKit.Mid, TextAnchor.UpperLeft);
            TopLeft(_barLabel.rectTransform, new Vector2(RightX, -280f), new Vector2(RightW, 20f));

            var head = UiKit.Txt(inner.transform, "Totals Head", 16, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            TopLeft(head.rectTransform, new Vector2(RightX, -306f), new Vector2(RightW, 22f));
            head.text = "Conjunto puesto";
            _totals = UiKit.Txt(inner.transform, "Totals", 14, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperLeft);
            TopLeft(_totals.rectTransform, new Vector2(RightX, -330f), new Vector2(RightW, 80f));

            var footer = UiKit.Txt(inner.transform, "Footer", 13, FontStyle.Normal, UiKit.Mid, TextAnchor.UpperLeft);
            TopLeft(footer.rectTransform, new Vector2(18f, -372f), new Vector2(340f, 48f));
            footer.text = "Arriba/abajo: ranura   Izq/der: prenda\nEnter o E: ponerse o quitarse   P o Esc: cerrar\nVestirse lleva tiempo y un golpe lo corta.";

            _panel.SetActive(false);
        }

        void BuildRow(Transform parent, int index)
        {
            var row = new SlotRow();
            row.frame = UiKit.Img(parent, "Slot " + index, UiKit.Border);
            TopLeft(row.frame.rectTransform, new Vector2(18f, -48f - index * (RowH + 8f)), new Vector2(RowW, RowH));
            row.fill = UiKit.Img(row.frame.transform, "Fill", Blank);
            UiKit.Stretch(row.fill.rectTransform, 2f, 2f, 2f, 2f);
            row.label = UiKit.Txt(row.fill.transform, "Slot Name", 12, FontStyle.Normal, UiKit.Mid, TextAnchor.UpperLeft);
            TopLeft(row.label.rectTransform, new Vector2(10f, -4f), new Vector2(RowW - 24f, 16f));
            row.piece = UiKit.Txt(row.fill.transform, "Piece", 15, FontStyle.Bold, UiKit.Cream, TextAnchor.UpperLeft);
            TopLeft(row.piece.rectTransform, new Vector2(10f, -22f), new Vector2(RowW - 24f, 26f));
            row.label.text = ArmorSlotInfo.Label((ArmorSlot)index);
            _rows.Add(row);
        }

        static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
        }

        // ------------------------------------------------------------------------------------------ state

        static string Signed(int v)
        {
            return (v > 0 ? "+" : string.Empty) + v + "%";
        }

        static string Seconds(float s)
        {
            return s.ToString("0.#") + " s";
        }

        void Refresh()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                bool sel = i == _slot;
                ArmorDef worn = equipment.Worn((ArmorSlot)i);
                r.frame.color = sel ? UiKit.Warm : UiKit.Border;
                r.piece.text = worn == null
                    ? "—"
                    : worn.DisplayName + "   Bruma -" + Mathf.RoundToInt(worn.BrumaCover * 100f) + "%  golpes -" + Mathf.RoundToInt(worn.BlowReduction * 100f) + "%";
                r.piece.color = worn == null ? UiKit.Mid : UiKit.Cream;
            }

            ArmorSlot slot = (ArmorSlot)_slot;
            ArmorDef pick = _options.Count > 0 ? _options[Mathf.Clamp(_option, 0, _options.Count - 1)] : null;
            ArmorDef current = equipment.Worn(slot);

            _optTitle.text = (pick == null ? "Sin prenda" : pick.DisplayName) + "   (" + (_option + 1) + "/" + _options.Count + ")";
            _sb.Length = 0;
            if (pick == null)
            {
                _sb.Append("Dejar ").Append(ArmorSlotInfo.CoverText(slot)).Append(" al descubierto.\n");
                _sb.Append("<color=#9A968C>Sin cobertura contra la Bruma ni contra los golpes.</color>");
            }
            else
            {
                if (!string.IsNullOrEmpty(pick.Description)) _sb.Append(pick.Description).Append("\n\n");
                _sb.Append("Cubre ").Append(ArmorSlotInfo.CoverText(slot)).Append('\n');
                _sb.Append("Detiene la Bruma   ").Append(Mathf.RoundToInt(pick.BrumaCover * 100f)).Append("%\n");
                _sb.Append("Absorbe golpes   ").Append(Mathf.RoundToInt(pick.BlowReduction * 100f)).Append("%\n");
                var mods = pick.Mods;
                for (int m = 0; mods != null && m < mods.Length; m++)
                {
                    if (mods[m] == null || Mathf.Abs(mods[m].change) < 0.0001f) continue;
                    _sb.Append(BodyStatInfo.Label(mods[m].stat)).Append("   ").Append(Signed(Mathf.RoundToInt(mods[m].change * 100f))).Append('\n');
                }
            }
            _optInfo.text = _sb.ToString();

            if (pick == null)
                _action.text = current != null ? "Enter: quitarse " + current.DisplayName + " (" + Seconds(current.UnequipSeconds) + ")" : "Ranura vacía";
            else if (pick == current)
                _action.text = "Puesta. Enter: quitarse (" + Seconds(pick.UnequipSeconds) + ")";
            else
                _action.text = "Enter: ponerse (" + Seconds(pick.EquipSeconds + (current != null ? current.UnequipSeconds : 0f)) + ")";

            var fill = _barFill;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(equipment.Progress01, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            bool flashing = !string.IsNullOrEmpty(_flash) && Time.unscaledTime < _flashUntil;
            _barLabel.text = equipment.Busy ? equipment.BusyText : (flashing ? _flash : string.Empty);
            _barLabel.color = equipment.Busy ? UiKit.Cream : UiKit.Warm;

            _sb.Length = 0;
            _sb.Append("Absorbe golpes   ").Append(Mathf.RoundToInt(equipment.BlowReduction * 100f)).Append("%\n");
            int lines = 0;
            for (int s = 0; s < BodyStatInfo.Count; s++)
            {
                int pct = Mathf.RoundToInt((equipment.StatMult((BodyStat)s) - 1f) * 100f);
                if (pct == 0 || lines >= 4) continue;
                _sb.Append(BodyStatInfo.Label((BodyStat)s)).Append("   ").Append(Signed(pct)).Append('\n');
                lines++;
            }
            _totals.text = _sb.ToString();
        }
    }
}
