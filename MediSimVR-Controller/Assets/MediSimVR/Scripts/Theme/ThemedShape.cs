using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public enum ShapeKind { Button, Card, Pill, Circle, Field }

    // Corner radius / button style / button size from the theme. Uses one 9-sliced sprite (radius 64)
    // and Image.pixelsPerUnitMultiplier to get any radius without new textures.
    [RequireComponent(typeof(Image))]
    public class ThemedShape : ThemedBase
    {
        public ShapeKind kind = ShapeKind.Card;
        public bool themeHeight;          // buttons: height follows "Button Size"
        public float pillHeight = 120f;   // used for Pill radius when height is not known yet

        const float SpriteRadius = 64f;

        public override void Apply()
        {
            var img = GetComponent<Image>();
            if (img == null || kind == ShapeKind.Circle) return;
            var t = ThemeManager.Current;
            float h = themeHeight ? ThemeManager.ButtonHeight : ((RectTransform)transform).rect.height;
            if (h <= 1f) h = pillHeight;
            float r;
            switch (kind)
            {
                case ShapeKind.Button:
                    r = t.ButtonStyle == ButtonStyle.Pill ? h * 0.5f : t.ButtonStyle == ButtonStyle.Square ? 6f : t.CornerRadius;
                    break;
                case ShapeKind.Pill: r = h * 0.5f; break;
                case ShapeKind.Field: r = Mathf.Min(t.CornerRadius, h * 0.5f); break;
                default: r = t.CornerRadius + 6f; break;
            }
            r = Mathf.Clamp(r, 1f, h * 0.5f);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SpriteRadius / r;
            if (themeHeight)
            {
                var le = GetComponent<LayoutElement>();
                if (le != null) le.preferredHeight = ThemeManager.ButtonHeight;
                else
                {
                    var rt = (RectTransform)transform;
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, ThemeManager.ButtonHeight);
                }
            }
        }

        void OnRectTransformDimensionsChange() { if (isActiveAndEnabled && kind != ShapeKind.Card) Apply(); }
    }
}
