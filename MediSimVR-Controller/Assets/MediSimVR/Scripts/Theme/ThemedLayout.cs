using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public enum LayoutKind { List, Row }

    // List: gap + side padding from "Size & Spacing". Row: height from "Layout" density.
    public class ThemedLayout : ThemedBase
    {
        public LayoutKind kind = LayoutKind.List;
        public float baseHeight = 140f;
        public bool sidePadding = true;

        public override void Apply()
        {
            var t = ThemeManager.Current;
            if (kind == LayoutKind.List)
            {
                var g = GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (g == null) return;
                g.spacing = t.ListGap;
                if (sidePadding) { g.padding.left = t.ScreenPadding; g.padding.right = t.ScreenPadding; }
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            }
            else
            {
                var le = GetComponent<LayoutElement>();
                if (le != null) le.preferredHeight = baseHeight * ThemeManager.DensityScale;
            }
        }
    }
}
