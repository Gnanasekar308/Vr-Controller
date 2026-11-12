using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Video / Audio: Play, Pause, Resume, Stop, Mute.  Image / Text: Show on Quest, Hide.  (controls shared with the Play page)
    public class ControlScreen : AppScreen
    {
        public TMP_Text selectedText, fileName, fileInfo;
        public Button fileCard;
        public UIGradient fileThumb;
        public Image fileIcon;
        public Sprite videoIcon, audioIcon, imageIcon, textIcon;
        public PlaybackControlsView controls;

        public override void Init(Navigator n)
        {
            base.Init(n);
            fileCard.onClick.AddListener(() => nav.Tab(ScreenId.Send));
            controls.Init();
        }

        public override void Refresh()
        {
            var sel = Svc.SelectedOnline();
            var sb = new StringBuilder();
            foreach (var d in sel) { if (sb.Length > 0) sb.Append(", "); sb.Append(d.Name); }
            selectedText.text = sel.Count == 0 ? "No device selected" : "Selected: " + sb;

            var f = Svc.ControlFile;
            fileName.text = f != null ? f.Name : "Choose a file";
            fileInfo.text = f != null ? MediaTypes.Label(f.Kind) + "  •  " + Fmt.Size(f.SizeBytes) : "Tap to open Send";
            if (f != null)
            {
                fileThumb.SetColors(f.ThumbTop, f.ThumbBottom);
                fileIcon.sprite = f.Kind == MediaKind.Video ? videoIcon : f.Kind == MediaKind.Audio ? audioIcon : f.Kind == MediaKind.Image ? imageIcon : textIcon;
            }
            controls.Bind(f, sel, "Select devices on the Devices tab", null);
        }
    }
}
