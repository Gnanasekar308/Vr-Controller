using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // Send tab: ONE item at a time (a Quest shows one video / audio / image / text).
    // Select -> preview card -> Send to the selected Quests. Picker, preview card and send are shared with the Play page.
    public class SendScreen : AppScreen
    {
        public MediaPreviewView preview;
        public TMP_Text sendLabel;
        public Button selectButton, sendButton;

        private ContentChooser chooser;

        public override void Init(Navigator n)
        {
            base.Init(n);
            chooser = n.GetComponent<ContentChooser>();
            selectButton.onClick.AddListener(() => chooser.Choose(Refresh));
            preview.changeButton.onClick.AddListener(() => chooser.Choose(Refresh));
            preview.removeButton.onClick.AddListener(() => { Svc.ControlFileId = ""; Refresh(); });
            preview.editButton.onClick.AddListener(() => chooser.EditText());
            sendButton.onClick.AddListener(SendSelected);
        }

        void SendSelected()
        {
            var f = Svc.ControlFile;
            if (f == null) { nav.Say("Tap Select to choose a file"); return; }
            var devs = Svc.SelectedOnline();
            if (devs.Count == 0) { nav.Say("Select devices on the Devices tab first"); return; }
            chooser.Send(f, devs, () => nav.Tab(ScreenId.Control));
        }

        public override void Refresh()
        {
            preview.Bind(Svc.ControlFile);
            sendLabel.text = "Send (" + Svc.SelectedOnline().Count + ")";
        }
    }
}
