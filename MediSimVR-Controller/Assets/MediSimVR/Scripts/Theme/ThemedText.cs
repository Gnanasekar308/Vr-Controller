using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public enum TextRole { Title, Header, Body, Button, Caption, Small }

    public enum TextWeight { Regular, SemiBold, Heading }

    [RequireComponent(typeof(TMP_Text))]
    public class ThemedText : ThemedBase
    {
        public ColorRole role = ColorRole.Text;
        public TextRole size = TextRole.Body;
        public TextWeight weight = TextWeight.Regular;
        public bool applyColor = true;   // false when another script owns the colour (tabs, status)
        public bool shrinkToFit;         // long names (PC station names): the text gets smaller to fit ...
        public float shrinkMin = 0.7f;   // ... down to this part of its size

        public static float BaseSize(TextRole r)
        {
            switch (r)
            {
                case TextRole.Title: return 62f;
                case TextRole.Header: return 48f;
                case TextRole.Button: return 38f;
                case TextRole.Caption: return 31f;
                case TextRole.Small: return 27f;
                default: return 37f;
            }
        }

        public override void Apply()
        {
            var t = GetComponent<TMP_Text>();
            if (t == null) return;
            if (applyColor) t.color = ThemeManager.Get(role);
            float fs = BaseSize(size) * ThemeManager.TextScale;
            t.fontSize = fs;
            t.enableAutoSizing = shrinkToFit;
            if (shrinkToFit) { t.fontSizeMax = fs; t.fontSizeMin = Mathf.Floor(fs * shrinkMin); }
            bool bold = weight == TextWeight.SemiBold || (weight == TextWeight.Heading && ThemeManager.Current.BoldHeadings);
            var f = ThemeManager.Font(bold);
            if (f != null && t.font != f) t.font = f;
        }
    }
}
