using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // a meeting in a list: name, topic, date + timing, students / Quests, status chip (Today / LIVE / Upcoming)
    public class MeetingRowView : MonoBehaviour
    {
        public Button button;
        public TMP_Text nameText, topicText, infoText, chipText;
        public Image chip, chipDot;

        private Meeting meeting;
        private Action<Meeting> onTap;

        private void Awake() { button.onClick.AddListener(() => { if (onTap != null && meeting != null) onTap(meeting); }); }

        public void Bind(Meeting m, Action<Meeting> tap)
        {
            meeting = m;
            onTap = tap;
            nameText.text = m.Name;
            topicText.text = m.Topic;
            infoText.text = Classroom.ShortDate(m.Date) + "  •  " + m.Timing + "  •  " + m.StudentIds.Count + " students";
            bool live = Classroom.Live == m && Classroom.LiveActive;
            bool today = m.Date.Date == DateTime.Today;
            ColorRole r = live ? DemoConfig.LiveRole : today ? ColorRole.Primary : ColorRole.SubText;
            chipText.text = live ? "LIVE" : today ? "Today" : m.Date.Date < DateTime.Today ? "Done" : "Upcoming";
            Color c = ThemeManager.Get(r);
            chipText.color = c;
            chipDot.color = c;
            chip.color = new Color(c.r, c.g, c.b, 0.14f);
        }
    }
}
