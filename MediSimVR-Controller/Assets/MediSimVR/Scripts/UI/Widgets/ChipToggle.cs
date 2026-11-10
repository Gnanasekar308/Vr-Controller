using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // On/off pill button (Snap, Lock, uniform scale). Click handling is done by the owner (Button.onClick),
    // which calls Set(); colours follow the theme: on = Primary, off = Field.
    public class ChipToggle : ThemedBase
    {
        public Image background;
        public TMP_Text label;
        public Image icon;
        public bool isOn;

        public void Set(bool on)
        {
            isOn = on;
            Apply();
        }

        public void SetLabel(string text) { if (label != null) label.text = text; }

        public override void Apply()
        {
            if (background != null) background.color = isOn ? ThemeManager.Get(ColorRole.Primary) : ThemeManager.Get(ColorRole.Field);
            if (label != null) label.color = isOn ? ThemeManager.Get(ColorRole.OnPrimary) : ThemeManager.Get(ColorRole.Text);
            if (icon != null) icon.color = isOn ? ThemeManager.Get(ColorRole.OnPrimary) : ThemeManager.Get(ColorRole.SubText);
        }
    }
}
