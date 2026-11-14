using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // a student's question or comment in the Live session: initials avatar, name, time, text, "New" chip (questions)
    public class LiveLineView : MonoBehaviour
    {
        public Button button;
        public TMP_Text avatarText, nameText, timeText, messageText, chipText;
        public GameObject newChip;

        private LiveLine line;
        private Action<LiveLine> onTap;

        private void Awake() { if (button != null) button.onClick.AddListener(() => { if (onTap != null && line != null) onTap(line); }); }

        public void Bind(LiveLine l, bool isNew, Action<LiveLine> tap)
        {
            line = l;
            onTap = tap;
            var s = Classroom.StudentById(l.StudentId);
            avatarText.text = s != null ? s.Initials : "?";
            nameText.text = s != null ? s.Name : "Student";
            timeText.text = l.Time.ToString("HH:mm");
            messageText.text = l.Text;
            if (newChip != null) newChip.SetActive(isNew);
            if (chipText != null) chipText.text = l.Answered ? "Answered" : "New";
            if (newChip != null && l.Answered) newChip.SetActive(true);
            float a = l.Answered ? 0.55f : 1f;
            messageText.alpha = a;
        }
    }
}
