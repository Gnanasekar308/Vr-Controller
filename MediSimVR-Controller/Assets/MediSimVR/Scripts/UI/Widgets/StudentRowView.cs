using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // a student in the Live session: name, Quest + status, three small meters (attention, engagement, emotion),
    // mute button. Tap = student details.
    public class StudentRowView : MonoBehaviour
    {
        public Button button, muteButton;
        public Image muteIcon, focusRing;
        public Sprite soundSprite, mutedSprite;
        public TMP_Text nameText, subText;
        public MeterView attention, engagement, emotion, participation;
        public TMP_Text avatarText, statusText, batteryText;   // compact Live student row (null elsewhere)
        public Image statusDot;

        public string studentId = "";
        private Action<string> onTap, onMute;

        private void Awake()
        {
            button.onClick.AddListener(() => { if (onTap != null) onTap(studentId); });
            if (muteButton != null) muteButton.onClick.AddListener(() => { if (onMute != null) onMute(studentId); });
        }

        public void Bind(string id, string name, string sub, bool present, bool muted, bool focused, Action<string> tap, Action<string> mute)
        {
            studentId = id;
            onTap = tap;
            onMute = mute;
            nameText.text = name;
            subText.text = sub;
            float a = present ? 1f : 0.5f;
            nameText.alpha = a;
            subText.alpha = a;
            if (muteButton != null) muteButton.gameObject.SetActive(present && mute != null);
            if (muteIcon != null)
            {
                muteIcon.sprite = muted ? mutedSprite : soundSprite;
                muteIcon.color = ThemeManager.Get(muted ? ColorRole.Danger : ColorRole.Primary);
            }
            if (focusRing != null) focusRing.gameObject.SetActive(focused);
            if (attention != null) attention.gameObject.SetActive(present);
            if (engagement != null) engagement.gameObject.SetActive(present);
            if (emotion != null) emotion.gameObject.SetActive(present);
            if (participation != null) participation.gameObject.SetActive(present);
        }

        // compact row: initials, Online / Offline / Absent, battery
        public void SetDevice(string initials, string status, ColorRole statusRole, string battery)
        {
            if (avatarText != null) avatarText.text = initials;
            Color c = ThemeManager.Get(statusRole);
            if (statusText != null) { statusText.text = status; statusText.color = c; }
            if (statusDot != null) statusDot.color = c;
            if (batteryText != null) batteryText.text = battery;
        }

        public void SetValues(StudentMetrics m)
        {
            if (m == null) return;
            if (attention != null) attention.Set(m.ShownAttention);
            if (engagement != null) engagement.Set(m.ShownEngagement);
            if (emotion != null) emotion.Set(m.ShownEmotion);
            if (participation != null) participation.Set(m.ShownParticipation);
        }
    }
}
