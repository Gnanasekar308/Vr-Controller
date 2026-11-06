using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    public class NotificationRowView : MonoBehaviour
    {
        public Image icon;
        public TMP_Text device, message, time;
        public Sprite info, ok, warn, error;

        public void Bind(AppNotification n)
        {
            device.text = n.Device;
            message.text = n.Message;
            time.text = Fmt.Ago(n.Time);
            switch (n.Level)
            {
                case NoteLevel.Success: icon.sprite = ok; icon.color = ThemeManager.Get(ColorRole.Accent); break;
                case NoteLevel.Warning: icon.sprite = warn; icon.color = ThemeManager.Get(ColorRole.Warning); break;
                case NoteLevel.Error: icon.sprite = error; icon.color = ThemeManager.Get(ColorRole.Danger); break;
                default: icon.sprite = info; icon.color = ThemeManager.Get(ColorRole.Primary); break;
            }
        }
    }
}
