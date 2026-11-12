using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // One chat message: my messages on the right (blue), Quest replies on the left (card colour) with the sender.
    public class ChatBubbleView : MonoBehaviour
    {
        public RectTransform bubble;
        public Image background;
        public TMP_Text fromText, messageText;
        public LayoutElement layout;
        public float widthFraction = 0.78f;

        public void Bind(ChatMessage m, float rowWidth)
        {
            fromText.text = (m.Mine ? "You" : m.From) + "  •  " + m.Time.ToString("HH:mm");
            messageText.text = m.Text;
            bool mine = m.Mine;
            float w = Mathf.Max(200f, rowWidth * widthFraction);
            bubble.anchorMin = bubble.anchorMax = new Vector2(mine ? 1f : 0f, 1f);
            bubble.pivot = new Vector2(mine ? 1f : 0f, 1f);
            bubble.anchoredPosition = Vector2.zero;
            float textH = messageText.GetPreferredValues(m.Text, w - 64f, 0f).y;
            float h = Mathf.Max(120f, textH + 86f);
            bubble.sizeDelta = new Vector2(w, h);
            layout.preferredHeight = h;
            background.color = mine ? DemoConfig.Hex(DemoConfig.MyBubbleColor) : ThemeManager.Get(ColorRole.Field);
            Color fg = mine ? Color.white : ThemeManager.Get(ColorRole.Text);
            messageText.color = fg;
            fromText.color = mine ? new Color(1f, 1f, 1f, 0.8f) : ThemeManager.Get(ColorRole.SubText);
            fromText.alignment = mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            messageText.alignment = mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        }
    }
}
