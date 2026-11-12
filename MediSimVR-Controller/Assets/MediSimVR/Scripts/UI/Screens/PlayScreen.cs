using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Play page of the Control Mode flow, the same for both roles:
    //   Host:   the Quests picked in the Quest list ("Playing on: ..."; Change = back to the list)
    //   Player: "My Quest" only ("My Quest: Quest-XXXX"; Change = pick another Quest)
    // Pick content (Video / Audio / Image / Text, shared picker + preview card), "Send & Play" / "Send & Show", then
    // the controls of that type on the same page (shared with the Control tab). Actions go to the session timeline.
    // Up: Quest list (host) or Session (player).
    public class PlayScreen : AppScreen
    {
        public Button backButton, changeTargetButton, sendButton;
        public TMP_Text subtitleText, targetText, changeLabel, sendLabel, hintText;
        public Segmented kindSeg;
        public MediaPreviewView preview;
        public PlaybackControlsView controls;
        public GameObject controlsRoot;

        private ContentChooser chooser;
        private string sendingFileId = "";
        private float sendingUntil, nextProgress;
        private readonly StringBuilder sb = new StringBuilder();

        private ISessionService Ses { get { return AppRoot.Session; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            chooser = n.GetComponent<ContentChooser>();
            backButton.onClick.AddListener(() => nav.Back());
            changeTargetButton.onClick.AddListener(ChangeTarget);
            sendButton.onClick.AddListener(Send);
            kindSeg.Changed += k => chooser.Choose((MediaKind)k, Refresh);
            preview.changeButton.onClick.AddListener(() => chooser.Choose(Refresh));
            preview.removeButton.onClick.AddListener(() => { Svc.ControlFileId = ""; Refresh(); });
            preview.editButton.onClick.AddListener(() => chooser.EditText());
            controls.Init();
            controls.Acted += OnActed;
        }

        public override bool TryGetParent(out ScreenId parent)
        {
            parent = ControlFlow.IsPlayer ? ScreenId.Session : ScreenId.QuestList;
            return true;
        }

        public override void OnShow()
        {
            // player: the controls act on My Quest only; no My Quest (none online) = no target at all
            if (ControlFlow.IsPlayer) ControlFlow.SelectOnly(Svc, ControlFlow.MyQuest(Svc));
            base.OnShow();
        }

        private List<QuestDevice> Targets() { return Svc.SelectedOnline(); }

        private string Names(List<QuestDevice> l)
        {
            sb.Length = 0;
            foreach (var d in l) { if (sb.Length > 0) sb.Append(", "); sb.Append(d.Name); }
            return sb.ToString();
        }

        // short form for the target chip: "Quest-A1B2, Quest-C3D4 +1"
        private static string ShortNames(List<QuestDevice> l)
        {
            if (l.Count <= 2) return l.Count == 2 ? l[0].Name + ", " + l[1].Name : l.Count == 1 ? l[0].Name : "";
            return l[0].Name + ", " + l[1].Name + " +" + (l.Count - 2);
        }

        public override void Refresh()
        {
            subtitleText.text = SessionStartScreen.ModeLine(true);
            var t = Targets();
            if (ControlFlow.IsPlayer)
            {
                var q = ControlFlow.MyQuest(Svc);
                targetText.text = "My Quest: " + (q == null ? "no Quest online" : q.Name + (q.Online ? "" : " (offline)"));
            }
            else targetText.text = t.Count == 0 ? "No Quest selected" : "Playing on: " + ShortNames(t);
            changeLabel.text = "Change";

            var f = Svc.ControlFile;
            preview.Bind(f);
            if (f != null) kindSeg.Select((int)f.Kind, false); // shows the type of the current content
            bool visual = PlaybackControlsView.IsVisual(f);
            string verb = visual ? "Send & Show" : "Send & Play";
            sendLabel.text = f == null ? "Choose content" : verb + " (" + t.Count + ")";

            bool sent = false;
            if (f != null) foreach (var d in t) if (d.FileId == f.Id) sent = true;
            bool sending = f != null && sendingFileId == f.Id && Time.unscaledTime < sendingUntil && !sent;
            controlsRoot.SetActive(f != null && (sent || sending));
            hintText.gameObject.SetActive(!controlsRoot.activeSelf);
            hintText.text = f == null ? "Pick a video, audio, image or text above"
                : t.Count == 0 ? (ControlFlow.IsPlayer ? "Your Quest is offline" : "Select Quests first: tap Change")
                : "Tap " + verb + " to start it on " + (t.Count == 1 ? t[0].Name : t.Count + " Quests");
            if (f != null) controls.Bind(f, t, "", sending ? "Sending " + f.Name + "..." : null);
        }

        private void Update()
        {
            if (Svc == null || !controlsRoot.activeSelf || Time.unscaledTime < nextProgress) return;
            nextProgress = Time.unscaledTime + 0.25f;
            controls.UpdateProgress(Svc.ControlFile, Targets());
        }

        private void ChangeTarget()
        {
            if (ControlFlow.IsPlayer) nav.Push(ScreenId.SessionPlayerPick);
            else nav.Tab(ScreenId.QuestList);   // host: pick other Quests (the list's way up is Session)
        }

        private void Send()
        {
            var f = Svc.ControlFile;
            if (f == null) { chooser.Choose(Refresh); return; }
            var t = Targets();
            if (t.Count == 0) { nav.Say(ControlFlow.IsPlayer ? "Your Quest is offline" : "Select at least one online Quest"); return; }
            chooser.Send(f, t, () =>
            {
                sendingFileId = f.Id;
                sendingUntil = Time.unscaledTime + DemoConfig.FakeSendSeconds + 3f;
                Log(SessionEventKind.Content, "sent " + f.Name + " to " + Names(t));
                Refresh();
            });
        }

        private void OnActed(SessionEventKind kind, string verb)
        {
            var t = Targets();
            var f = Svc.ControlFile;
            Log(kind, verb + (kind == SessionEventKind.Play && f != null ? " " + f.Name : "") + " on " + Names(t));
            nav.Say(char.ToUpper(verb[0]) + verb.Substring(1) + " on " + t.Count + " Quest(s)");
        }

        private void Log(SessionEventKind kind, string message)
        {
            if (Ses == null || !Ses.Active) return;
            string who = ControlFlow.IsPlayer ? ControlFlow.PlayerQuestName(Svc) : "Host";
            Ses.Log(kind, who, message);
        }
    }
}
