using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // One line of the Live Session timeline: time, icon by event kind, "Quest-C3D4 joined".
    public class TimelineRowView : MonoBehaviour
    {
        public TMP_Text timeText, messageText;
        public Image icon;
        public List<Sprite> kindIcons = new List<Sprite>();   // index = (int)SessionEventKind

        public void Bind(SessionEvent e)
        {
            timeText.text = e.Time.ToString("HH:mm:ss");
            messageText.text = "<b>" + e.Device + "</b>  " + e.Message;
            int k = (int)e.Kind;
            icon.sprite = k < kindIcons.Count ? kindIcons[k] : null;
            icon.color = Tint(e.Kind);
        }

        private static Color Tint(SessionEventKind k)
        {
            switch (k)
            {
                case SessionEventKind.Voice: return ThemeManager.Get(DemoConfig.SpeakingRole);
                case SessionEventKind.Leave: return ThemeManager.Get(ColorRole.Danger);
                case SessionEventKind.Join: return ThemeManager.Get(ColorRole.Success);
                case SessionEventKind.Mute: return ThemeManager.Get(DemoConfig.MutedRole);
                default: return ThemeManager.Get(ColorRole.Primary);
            }
        }
    }
}
