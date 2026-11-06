using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Video / Audio: Play, Pause, Resume, Stop, Mute.  Text: Show on Quest, Hide.  (mock)
    public class ControlScreen : AppScreen
    {
        public TMP_Text selectedText, fileName, fileInfo, stateText;
        public Button fileCard, playButton, pauseButton, resumeButton, stopButton, showButton, hideButton;
        public UIGradient fileThumb;
        public Image fileIcon;
        public Sprite videoIcon, audioIcon, imageIcon, textIcon;
        public GameObject mediaControls, textControls;
        public ToggleSwitch muteSwitch;

        public override void Init(Navigator n)
        {
            base.Init(n);
            fileCard.onClick.AddListener(() => nav.Tab(ScreenId.Send));
            playButton.onClick.AddListener(() => Svc.Play());
            pauseButton.onClick.AddListener(() => Svc.Pause());
            resumeButton.onClick.AddListener(() => Svc.Resume());
            stopButton.onClick.AddListener(() => Svc.Stop());
            showButton.onClick.AddListener(() => Svc.ShowText());
            hideButton.onClick.AddListener(() => Svc.HideText());
            muteSwitch.Changed += m => Svc.SetMute(m);
        }

        public override void Refresh()
        {
            var sel = Svc.SelectedOnline();
            var sb = new StringBuilder();
            foreach (var d in sel) { if (sb.Length > 0) sb.Append(", "); sb.Append(d.Name); }
            selectedText.text = sel.Count == 0 ? "No device selected" : "Selected: " + sb;

            var f = Svc.ControlFile;
            bool text = f != null && (f.Kind == MediaKind.Text || f.Kind == MediaKind.Image);   // visual: Show / Hide
            fileName.text = f != null ? f.Name : "Choose a file";
            fileInfo.text = f != null ? MediaTypes.Label(f.Kind) + "  \u2022  " + Fmt.Size(f.SizeBytes) : "Tap to open Send";
            if (f != null)
            {
                fileThumb.SetColors(f.ThumbTop, f.ThumbBottom);
                fileIcon.sprite = f.Kind == MediaKind.Video ? videoIcon : f.Kind == MediaKind.Audio ? audioIcon : f.Kind == MediaKind.Image ? imageIcon : textIcon;
            }
            mediaControls.SetActive(!text);
            textControls.SetActive(text);

            int playing = 0, paused = 0, showing = 0, muted = 0;
            foreach (var d in sel)
            {
                if (d.State == DeviceState.Playing) playing++;
                if (d.State == DeviceState.Paused) paused++;
                if (d.State == DeviceState.ShowingText) showing++;
                if (d.Muted) muted++;
            }
            stateText.text = sel.Count == 0 ? "Select devices on the Devices tab"
                : text ? "Showing " + showing + " of " + sel.Count
                : "Playing " + playing + "  \u2022  Paused " + paused + "  \u2022  " + sel.Count + " selected";
            muteSwitch.Set(sel.Count > 0 && muted == sel.Count, false);
        }
    }
}
