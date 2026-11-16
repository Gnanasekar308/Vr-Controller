using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // Send tab = step 2 of the host flow (step 1 for a player): ONE item at a time (a Quest shows one video / audio /
    // image / text). Target card ("Send to: ..." / "My Quest: ..." + Change), preview card, Select, "Send (n)" -> sends
    // to the target Quests and opens Control. Picker, preview card and send are the shared components.
    public class SendScreen : AppScreen
    {
        public MediaPreviewView preview;
        public TMP_Text sendLabel, subtitleText, targetText;
        public Button selectButton, sendButton, changeTargetButton, sceneButton;   // sceneButton: "Live Scene" row

        private ContentChooser chooser;
        private readonly StringBuilder sb = new StringBuilder();

        public override void Init(Navigator n)
        {
            base.Init(n);
            chooser = n.GetComponent<ContentChooser>();
            selectButton.onClick.AddListener(() => chooser.Choose(Refresh));
            preview.changeButton.onClick.AddListener(() => chooser.Choose(Refresh));
            preview.removeButton.onClick.AddListener(() => { Svc.ControlFileId = ""; Refresh(); });
            preview.editButton.onClick.AddListener(() => chooser.EditText());
            sendButton.onClick.AddListener(SendSelected);
            changeTargetButton.onClick.AddListener(() => nav.Push(ScreenId.Devices));
            sceneButton.onClick.AddListener(() => nav.Push(ScreenId.Scene));
        }

        public override void OnShow()
        {
            // player: everything goes to My Quest only
            if (ControlFlow.IsPlayer) ControlFlow.SelectOnly(Svc, ControlFlow.MyQuest(Svc));
            base.OnShow();
        }

        private string Names(List<QuestDevice> l)
        {
            sb.Length = 0;
            for (int i = 0; i < l.Count && i < 2; i++) { if (sb.Length > 0) sb.Append(", "); sb.Append(l[i].Name); }
            if (l.Count > 2) sb.Append(" +").Append(l.Count - 2);
            return sb.ToString();
        }

        void SendSelected()
        {
            var f = Svc.ControlFile;
            if (f == null) { nav.Say("Tap Select to choose a file"); return; }
            var devs = Svc.SelectedOnline();
            if (devs.Count == 0) { nav.Say(ControlFlow.IsPlayer ? "Your Quest is offline" : "Select Quests on the Devices tab first"); return; }
            chooser.Send(f, devs, () =>
            {
                SessionLog.Add(SessionEventKind.Content, ControlFlow.IsPlayer ? ControlFlow.PlayerQuestName(Svc) : "Host",
                    "sent " + f.Name + " to " + Names(devs));
                if (Classroom.LiveActive) Classroom.Played(f.Name);
                // opened from the Live session: back there; else step 3: control the playback
                if (nav.Previous == ScreenId.Live) nav.Back();
                else nav.Push(ScreenId.Control);
            });
        }

        public override void Refresh()
        {
            preview.Bind(Svc.ControlFile);
            var sel = Svc.SelectedOnline();
            sendLabel.text = "Send (" + sel.Count + ")";
            string step = ControlFlow.Step(ScreenId.Send);
            subtitleText.text = step.Length > 0 ? step : "Send to the selected Quests";
            if (ControlFlow.IsPlayer)
            {
                var q = ControlFlow.MyQuest(Svc);
                targetText.text = "My Quest: " + (q == null ? "no Quest online" : q.Name + (q.Online ? "" : " (offline)"));
            }
            else targetText.text = sel.Count == 0 ? "No Quest selected" : "Send to: " + (sel.Count > 2 ? sel[0].Name + " +" + (sel.Count - 1) : Names(sel));
        }
    }
}
