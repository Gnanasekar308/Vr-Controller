using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // One Quest in the Session screens: name, online + battery, current content, speaking indicator, muted icon,
    // optional select box. Tap = select (host) or pick (player).
    public class QuestCardView : MonoBehaviour
    {
        public Button button;
        public Image checkBox, checkMark, statusDot;
        public TMP_Text nameText, statusText, contentText;
        public SpeakingPulse mic;
        public GameObject mutedIcon;
        public Sprite boxFilled, boxOutline;
        public CanvasGroup group;

        public void Bind(QuestDevice d, string content, bool speaking, bool showCheck, Action onTap)
        {
            nameText.text = d.Name;
            Color on = ThemeManager.Get(ColorRole.Accent), off = ThemeManager.Get(ColorRole.SubText);
            statusDot.color = d.Online ? on : off;
            statusText.text = d.Online ? "Online  •  " + (d.Battery >= 0 ? d.Battery + "% battery" : "battery --") : "Offline";
            contentText.text = d.Online ? content : "Not connected";
            bool sel = showCheck && d.Selected && d.Online;
            checkBox.gameObject.SetActive(showCheck);
            if (showCheck)
            {
                checkBox.sprite = sel ? boxFilled : boxOutline;
                checkBox.color = sel ? ThemeManager.Get(ColorRole.Primary) : ThemeManager.Get(ColorRole.SubText);
                checkMark.gameObject.SetActive(sel);
            }
            mic.Set(d.Online && speaking);
            mutedIcon.SetActive(d.Online && d.Muted);
            if (group != null) group.alpha = d.Online ? 1f : 0.55f;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (onTap != null) onTap(); });
        }
    }
}
