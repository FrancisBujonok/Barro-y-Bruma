using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.UI
{
    /// <summary>
    /// Small helpers to build uGUI parts from code, with the colors the rest of the HUD already uses.
    /// The belt, the bag label and the notebook are built at run time because they depend on data.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Border = new Color32(0x7A, 0x66, 0x50, 255);
        public static readonly Color PanelBg = new Color32(0x1B, 0x17, 0x14, 235);
        public static readonly Color Cream = new Color32(0xC4, 0xBF, 0xB2, 255);
        public static readonly Color Warm = new Color32(0xCD, 0xBB, 0x94, 255);
        public static readonly Color Mid = new Color32(0x9A, 0x96, 0x8C, 255);

        static Font _font;

        /// <summary>The font the HUD already uses, or the built-in one.</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                var any = Object.FindFirstObjectByType<Text>(FindObjectsInactive.Include);
                _font = any != null && any.font != null ? any.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Image Img(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Txt(Transform parent, string name, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Anchors a rect to one point of its parent and gives it a size.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
