using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // row with a title, a secondary line, an optional checkbox (multi-select) and an optional status chip
    // (Create Meeting: students and Quests; Meeting details: attendance, assigned Quests)
    public class CheckRowView : MonoBehaviour
    {
        public Button button;
        public TMP_Text titleText, subText, chipText;
        public Image check, chip, chipDot;
        public Sprite checkedSprite, uncheckedSprite;
        public string key = "";

        public void Bind(string k, string title, string sub, bool? isChecked, string chipLabel, ColorRole chipRole, bool dim = false)
        {
            key = k;
            titleText.text = title;
            subText.text = sub;
            if (check != null)
            {
                check.gameObject.SetActive(isChecked.HasValue);
                if (isChecked.HasValue)
                {
                    check.sprite = isChecked.Value ? checkedSprite : uncheckedSprite;
                    check.color = ThemeManager.Get(isChecked.Value ? ColorRole.Primary : ColorRole.SubText);
                }
            }
            if (chip != null)
            {
                chip.gameObject.SetActive(!string.IsNullOrEmpty(chipLabel));
                Color c = ThemeManager.Get(chipRole);
                chipText.text = chipLabel ?? "";
                chipText.color = c;
                chipDot.color = c;
                chip.color = new Color(c.r, c.g, c.b, 0.14f);
            }
            var a = dim ? 0.5f : 1f;
            titleText.alpha = a;
            subText.alpha = a;
        }
    }
}
