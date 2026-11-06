using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public class FileRowView : MonoBehaviour
    {
        public Button button, moreButton;
        public Image thumb, selectedRing, typeIcon, badge;
        public UIGradient thumbGradient;
        public TMP_Text duration, nameText, infoText;
        public Sprite videoIcon, audioIcon, textIcon;

        public void Bind(MediaFile f, bool selected, Action onTap, Action onMore)
        {
            nameText.text = f.Name;
            string extra = f.Kind == MediaKind.Text ? Preview(f.Text) : Fmt.Dur(f.DurationSec);
            infoText.text = MediaTypes.Label(f.Kind) + "  \u2022  " + Fmt.Size(f.SizeBytes) + (extra.Length > 0 ? "  \u2022  " + extra : "");
            thumbGradient.SetColors(f.ThumbTop, f.ThumbBottom);
            thumbGradient.vertical = true;
            typeIcon.sprite = f.Kind == MediaKind.Video ? videoIcon : f.Kind == MediaKind.Audio ? audioIcon : textIcon;
            bool timed = f.Kind != MediaKind.Text && f.DurationSec >= 0;
            badge.gameObject.SetActive(timed);
            duration.text = Fmt.Dur(f.DurationSec);
            if (selectedRing != null) selectedRing.gameObject.SetActive(selected);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (onTap != null) onTap(); });
            moreButton.onClick.RemoveAllListeners();
            moreButton.onClick.AddListener(() => { if (onMore != null) onMore(); });
        }

        static string Preview(string t)
        {
            t = (t ?? "").Replace("\n", " ").Trim();
            return t.Length > 24 ? t.Substring(0, 23) + "\u2026" : t;
        }
    }
}
