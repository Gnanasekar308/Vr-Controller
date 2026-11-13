using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // shared small builders (part of MediSimUIBuilder): section label, top-left placement, timeline row
    public static partial class MediSimUIBuilder
    {
        static void Label(Transform card, string text, float y)
        {
            var t = SmallLabel(card, text + "Label", text.ToUpperInvariant());
            TopBand(t.rectTransform, y, 48f, 48f);
        }

        // left / top distance + right inset + height, anchored to the top-left of the parent
        static void TopLeft(RectTransform rt, float left, float top, float right, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(left, -top - h);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static TimelineRowView TimelineRow(Transform box)
        {
            var r = New("TimelineRow", box);
            LE(r, 84);
            var v = r.gameObject.AddComponent<TimelineRowView>();
            var bg = Shape(r, "Bg", ColorRole.Card, ShapeKind.Field);
            bg.raycastTarget = false;
            Stretch(bg.rectTransform);
            v.icon = Icon(r, "Icon", "info", ColorRole.Primary, 44f);
            v.icon.GetComponent<ThemedColor>().enabled = false;
            Place(v.icon.rectTransform, 0f, 0.5f, 44f, 44f, 24f, 0f);
            v.timeText = Txt(r, "Time", "00:00:00", TextRole.Small, ColorRole.SubText);
            Place(v.timeText.rectTransform, 0f, 0.5f, 140f, 60f, 84f, 0f);
            v.messageText = Txt(r, "Message", "", TextRole.Caption, ColorRole.Text);
            v.messageText.richText = true;
            Stretch(v.messageText.rectTransform, 230, 0, 20, 0);
            // icon per SessionEventKind: Play, Pause, Stop, Content, Text, Voice, Join, Leave, Object, Mute, Info
            foreach (var n in new[] { "play_arrow", "pause", "stop", "send", "article", "volume_up", "check", "close", "view_in_ar", "volume_off", "info", "text_snippet", "warning", "volume_up" })   // + Note, Instruction, Audio
                v.kindIcons.Add(IconS(n));
            r.gameObject.SetActive(false);
            return v;
        }
    }
}
