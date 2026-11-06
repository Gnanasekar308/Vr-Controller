using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Send tab: ONE item at a time (a Quest shows one video / audio / image / text).
    // Select -> preview card -> Send to the selected Quests. UI + mock only.
    public class SendScreen : AppScreen
    {
        public GameObject filledGroup, emptyGroup, badge, textPaper;
        public UIGradient previewBackground;
        public Image previewImage, previewIcon;
        public TMP_Text previewText, badgeText, nameText, infoText, sendLabel;
        public Button changeButton, editButton, removeButton, selectButton, sendButton;
        public Sprite videoIcon, audioIcon, imageIcon, textIcon;

        static readonly string[] Kinds = { "Video", "Audio", "Image", "Text" };
        static readonly string[] KindIcons = { "movie", "music_note", "image", "article" };
        Sprite imageSprite;

        public override void Init(Navigator n)
        {
            base.Init(n);
            selectButton.onClick.AddListener(ChooseType);
            changeButton.onClick.AddListener(ChooseType);
            removeButton.onClick.AddListener(() => { Svc.ControlFileId = ""; Refresh(); });
            editButton.onClick.AddListener(EditText);
            sendButton.onClick.AddListener(SendSelected);
            AppRoot.Picker.Picked += list =>
            {
                if (list.Count == 0) return;
                Svc.AddFile(list[0]);              // newest choice replaces the card
                nav.Say(list[0].Name + " selected");
            };
            AppRoot.Picker.Failed += msg => nav.Say(msg);
        }

        void ChooseType()
        {
            nav.actionSheet.Show("Select file type", Kinds, KindIcons, k =>
            {
                if (k == 0) AppRoot.Picker.Pick(MediaKind.Video);
                else if (k == 1) AppRoot.Picker.Pick(MediaKind.Audio);
                else if (k == 2) AppRoot.Picker.Pick(MediaKind.Image);
                else ChooseTextSource();
            });
        }

        void ChooseTextSource()
        {
            nav.actionSheet.Show("Text", new[] { "Write new text", "Open .txt file" }, new[] { "note_add", "folder_open" }, k =>
            {
                if (k == 1) { AppRoot.Picker.Pick(MediaKind.Text); return; }
                nav.textEditor.Open("New Text", "", "", (title, content) =>
                {
                    var f = FilePicker.Make("typed:" + title + System.DateTime.Now.Ticks, TxtName(title), 0, MediaKind.Text);
                    f.Text = content;
                    f.Source = "typed";
                    f.SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content);
                    Svc.AddFile(f);
                    nav.Say("Text ready to send");
                });
            });
        }

        void EditText()
        {
            var f = Svc.ControlFile;
            if (f == null || f.Kind != MediaKind.Text) return;
            nav.textEditor.Open("Edit Text", f.Name.Replace(".txt", ""), f.Text, (title, content) =>
            {
                f.Name = TxtName(title);
                f.Text = content;
                Svc.UpdateFile(f);
            });
        }

        static string TxtName(string title) { return title.EndsWith(".txt") ? title : title + ".txt"; }

        void SendSelected()
        {
            var f = Svc.ControlFile;
            if (f == null) { nav.Say("Tap Select to choose a file"); return; }
            var devs = Svc.SelectedOnline();
            if (devs.Count == 0) { nav.Say("Select devices on the Devices tab first"); return; }
            // a Quest shows one thing at a time: ask before replacing what it is playing / showing
            string busy = "";
            foreach (var d in devs)
            {
                if (DeviceRowView.InUse(d) && d.FileId != f.Id) busy += (busy.Length > 0 ? ", " : "") + d.Name;
            }
            if (busy.Length > 0)
                nav.popup.Show("Replace content?", busy + " is already playing or showing something.\nSend " + f.Name + " instead?",
                    "Replace", () => DoSend(f, devs.Count), "Cancel");
            else DoSend(f, devs.Count);
        }

        void DoSend(MediaFile f, int n)
        {
            Svc.Send(f);
            nav.Say("Sending " + f.Name + " to " + n + " device(s)");
            nav.Tab(ScreenId.Control);
        }

        public override void Refresh()
        {
            var f = Svc.ControlFile;
            filledGroup.SetActive(f != null);
            emptyGroup.SetActive(f == null);
            sendLabel.text = "Send (" + Svc.SelectedOnline().Count + ")";
            if (f == null) return;

            nameText.text = f.Name;
            string extra = f.Kind == MediaKind.Video || f.Kind == MediaKind.Audio ? "  \u2022  " + Fmt.Dur(f.DurationSec) : "";
            infoText.text = MediaTypes.Label(f.Kind) + "  \u2022  " + Fmt.Size(f.SizeBytes) + extra;
            previewBackground.SetColors(f.ThumbTop, f.ThumbBottom);

            bool isImage = f.Kind == MediaKind.Image && f.Preview != null;
            if (isImage)
            {
                if (imageSprite == null || imageSprite.texture != f.Preview)
                    imageSprite = Sprite.Create(f.Preview, new Rect(0, 0, f.Preview.width, f.Preview.height), new Vector2(0.5f, 0.5f));
                previewImage.sprite = imageSprite;
            }
            previewImage.gameObject.SetActive(isImage);

            bool isText = f.Kind == MediaKind.Text;
            textPaper.SetActive(isText);
            if (isText) previewText.text = string.IsNullOrEmpty(f.Text) ? "(empty text)" : f.Text;

            previewIcon.gameObject.SetActive(!isImage && !isText);
            previewIcon.sprite = f.Kind == MediaKind.Video ? videoIcon : f.Kind == MediaKind.Audio ? audioIcon : f.Kind == MediaKind.Image ? imageIcon : textIcon;
            bool timed = (f.Kind == MediaKind.Video || f.Kind == MediaKind.Audio) && f.DurationSec >= 0;
            badge.SetActive(timed);
            badgeText.text = Fmt.Dur(f.DurationSec);
            editButton.gameObject.SetActive(isText);
        }
    }
}
