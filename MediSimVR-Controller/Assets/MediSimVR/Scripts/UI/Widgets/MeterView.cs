using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // one horizontal bar: label, fill (0-100 % of the track) and the value; colour by value (ok / low)
    public class MeterView : MonoBehaviour
    {
        public TMP_Text label, valueText;
        public RectTransform fill;
        public Image fillImage;
        public ColorRole role = ColorRole.Primary;

        public void Set(float value, string text = null)
        {
            float v = Mathf.Clamp(value, 0f, 100f);
            if (fill != null) fill.anchorMax = new Vector2(v / 100f, fill.anchorMax.y);
            if (valueText != null) valueText.text = text ?? Mathf.RoundToInt(v) + "%";
            if (fillImage != null)
                fillImage.color = ThemeManager.Get(v < DemoConfig.LowAttention ? ColorRole.Warning : role);
        }
    }
}
