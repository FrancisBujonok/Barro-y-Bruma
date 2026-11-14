using BrumaPx.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.UI
{
    /// <summary>
    /// The four belt pockets, bottom centre. Each shows its key (A S D F), a provisional icon (a colour square with the
    /// first letter of the item) and how many are left. Built from code when the game starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeltHud : MonoBehaviour
    {
        public Inventory inventory;
        [Tooltip("The UI canvas that receives the pockets.")]
        public Transform canvasRoot;

        static readonly string[] Keys = { "A", "S", "D", "F" };

        sealed class Pocket
        {
            public Image icon;
            public Text glyph, count;
        }

        readonly Pocket[] _pockets = new Pocket[Inventory.BeltSize];

        void Start()
        {
            if (inventory == null || canvasRoot == null) return;
            Build();
            inventory.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= Refresh;
        }

        void Build()
        {
            const float size = 52f, gap = 6f;
            var holder = new GameObject("Belt HUD", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(canvasRoot, false);
            float width = Inventory.BeltSize * size + (Inventory.BeltSize - 1) * gap;
            UiKit.Place(holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(width, size));

            for (int i = 0; i < Inventory.BeltSize; i++)
            {
                var frame = UiKit.Img(holder, "Pocket " + Keys[i], UiKit.Border);
                UiKit.Place(frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (size + gap), 0f), new Vector2(size, size));
                var inner = UiKit.Img(frame.transform, "Inner", UiKit.PanelBg);
                UiKit.Stretch(inner.rectTransform, 2f, 2f, 2f, 2f);

                var p = new Pocket();
                p.icon = UiKit.Img(inner.transform, "Icon", Color.white);
                UiKit.Place(p.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
                p.glyph = UiKit.Txt(p.icon.transform, "Glyph", 20, FontStyle.Bold, new Color32(0x1B, 0x17, 0x14, 255), TextAnchor.MiddleCenter);
                UiKit.Stretch(p.glyph.rectTransform, 0f, 0f, 0f, 0f);

                var key = UiKit.Txt(inner.transform, "Key", 12, FontStyle.Bold, UiKit.Mid, TextAnchor.UpperLeft);
                UiKit.Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(3f, -1f), new Vector2(16f, 14f));
                key.text = Keys[i];

                p.count = UiKit.Txt(inner.transform, "Count", 12, FontStyle.Bold, UiKit.Cream, TextAnchor.LowerRight);
                UiKit.Place(p.count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-3f, 1f), new Vector2(30f, 14f));
                _pockets[i] = p;
            }
        }

        void Refresh()
        {
            for (int i = 0; i < Inventory.BeltSize; i++)
            {
                var p = _pockets[i];
                if (p == null) continue;
                var stack = inventory.GetBelt(i);
                bool has = stack != null && stack.item != null && stack.count > 0;
                p.icon.gameObject.SetActive(has);
                p.count.text = has && stack.count > 1 ? stack.count.ToString() : string.Empty;
                if (!has) continue;
                p.icon.color = stack.item.Tint;
                p.glyph.text = stack.item.Glyph;
            }
        }
    }
}
