using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // One Quest in the Devices tab and in the Session Quest list: select box, name, battery + IP, what it plays,
    // status chip Online (green) / Offline (grey) / In Use (blue).
    public class DeviceRowView : MonoBehaviour
    {
        public Button button;
        public Image checkBox, checkMark, statusChip, statusDot;
        public TMP_Text nameText, infoText, contentText, statusText;
        public Sprite boxFilled, boxOutline;

        public void Bind(QuestDevice d, string content, Action onTap)
        {
            nameText.text = d.Name;
            infoText.text = DeviceStatus.Battery(d) + "  •  " + (d.Ip.Length > 0 ? d.Ip : "—");

            bool sel = d.Selected && d.Online;
            checkBox.sprite = sel ? boxFilled : boxOutline;
            checkBox.color = sel ? ThemeManager.Get(ColorRole.Primary) : ThemeManager.Get(ColorRole.SubText);
            checkMark.gameObject.SetActive(sel);
            var cg = checkBox.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = d.Online ? 1f : 0.35f;

            var st = DeviceStatus.Of(d);
            Color c = DeviceStatus.Tint(st);
            statusText.text = DeviceStatus.Label(st);
            statusText.color = c;
            statusDot.color = c;
            statusChip.color = new Color(c.r, c.g, c.b, 0.15f);
            contentText.text = st == QuestState.InUse ? content : st == QuestState.Online ? "Ready" : "Not connected";
            contentText.color = st == QuestState.InUse ? c : ThemeManager.Get(ColorRole.SubText);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (onTap != null) onTap(); });
        }
    }
}
