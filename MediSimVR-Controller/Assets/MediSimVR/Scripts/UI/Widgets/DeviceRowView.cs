using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public class DeviceRowView : MonoBehaviour
    {
        public Button button;
        public Image checkBox, checkMark, statusDot;
        public TMP_Text nameText, ipText, statusText;
        public RectTransform statusLine;
        public GameObject inUseBadge;
        public Sprite boxFilled, boxOutline;

        public static bool InUse(QuestDevice d)
        {
            return d.Online && (d.State == DeviceState.Playing || d.State == DeviceState.Paused || d.State == DeviceState.ShowingText);
        }

        public void Bind(QuestDevice d, Action onTap)
        {
            nameText.text = d.Name;
            ipText.text = d.Ip.Length > 0 ? d.Ip : "\u2014";
            bool sel = d.Selected && d.Online;
            checkBox.sprite = sel ? boxFilled : boxOutline;
            checkBox.color = sel ? ThemeManager.Get(ColorRole.Primary) : ThemeManager.Get(ColorRole.SubText);
            checkMark.gameObject.SetActive(sel);
            var cg = checkBox.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = d.Online ? 1f : 0.35f;

            Color sc = d.Online ? ThemeManager.Get(ColorRole.Accent) : ThemeManager.Get(ColorRole.SubText);
            statusText.text = d.Online ? "Online" : "Offline";
            statusText.color = sc;
            statusDot.color = sc;
            bool use = InUse(d);
            inUseBadge.SetActive(use);
            statusLine.anchoredPosition = new Vector2(statusLine.anchoredPosition.x, use ? 30f : 0f);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (onTap != null) onTap(); });
        }
    }
}
