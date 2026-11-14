using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // Create Meeting, step 2 of 2: what to play on the Quests. Video / Audio / Image (the app's content picker) with a
    // preview, Scene (opens the Live Scene) beside them, and Start Live: the meeting is saved and its live class opens
    // Ready. Nothing plays until the admin taps Play in the live class.
    public class MeetingContentScreen : AppScreen
    {
        public Button backButton, videoButton, audioButton, imageButton, sceneButton, startLiveButton;
        public TMP_Text subtitleText, targetText;
        public MediaPreviewView preview;

        private ContentChooser chooser;
        private string fileId = "";

        public string FileId { get { return fileId; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            chooser = n.GetComponent<ContentChooser>();
            backButton.onClick.AddListener(() => nav.Back());
            videoButton.onClick.AddListener(() => Pick(MediaKind.Video));
            audioButton.onClick.AddListener(() => Pick(MediaKind.Audio));
            imageButton.onClick.AddListener(() => Pick(MediaKind.Image));
            sceneButton.onClick.AddListener(() => nav.Push(ScreenId.Scene));
            preview.changeButton.onClick.AddListener(() => chooser.Choose(Picked));
            preview.removeButton.onClick.AddListener(() => { fileId = ""; Refresh(); });
            preview.editButton.onClick.AddListener(() => chooser.EditText());
            startLiveButton.onClick.AddListener(StartLive);
        }

        // a new draft from the form: no content chosen yet
        public void Begin() { fileId = ""; }

        void Pick(MediaKind kind) { chooser.Choose(kind, Picked); }

        void Picked()
        {
            var f = Svc.ControlFile;
            fileId = f != null ? f.Id : "";
            Refresh();
        }

        MediaFile File()
        {
            foreach (var f in Svc.Files) if (f.Id == fileId) return f;
            return null;
        }

        void StartLive()
        {
            var create = nav.Get<MeetingCreateScreen>();
            if (create == null) return;
            create.StartLive(fileId);
        }

        public override void Refresh()
        {
            var create = nav.Get<MeetingCreateScreen>();
            int n = create != null ? create.SelectedCount : 0;
            targetText.text = "Plays on " + n + (n == 1 ? " Quest" : " Quests") + " when you tap Play in the live class";
            var f = File();
            preview.Bind(f);
        }
    }
}
