using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // HOST dashboard: Quest cards (select one / several / all), Play / Pause / Resume / Stop / Send / Text / Mute / Scene,
    // voice bar (live voice, mute me, who is speaking), "Recording on PC" chip, Live Session timeline, text sheet with
    // quick messages + chat, End session -> summary. Device actions go to IControllerService, the timeline to ISessionService.
    public class SessionHostScreen : AppScreen
    {
        public Button backButton, endButton;
        public TMP_Text subtitleText, recordingText, timerText, speakingText, selectedText, selectAllLabel, timelineEmpty;
        public Image recordingDot;
        public ToggleSwitch voiceSwitch;
        public ChipToggle muteMeChip;
        public Button muteMeButton, selectAllButton;
        public SpeakingPulse hostMic;
        public QuestCardView cardTemplate;
        public TimelineRowView timelineTemplate;
        public Button playButton, pauseButton, resumeButton, stopButton, sendButton, textButton, muteButton, sceneButton;

        // text sheet
        public GameObject textSheet;
        public Button textClose, sendTextButton;
        public TMP_Text textToLine, chatEmpty;
        public List<Button> quickButtons = new List<Button>();
        public List<TMP_Text> quickLabels = new List<TMP_Text>();
        public ChatBubbleView chatTemplate;
        public ScrollRect chatScroll;
        public TMP_InputField textInput;

        private ISessionService Ses { get { return AppRoot.Session; } }
        private float nextClock;
        private int shownEvents = -1, shownChat = -1;
        private const int MaxTimelineRows = 40;
        private readonly List<QuestCardView> cardCache = new List<QuestCardView>();
        private readonly List<TimelineRowView> timelineCache = new List<TimelineRowView>();
        private readonly List<ChatBubbleView> chatCache = new List<ChatBubbleView>();

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
            endButton.onClick.AddListener(AskEnd);
            voiceSwitch.Changed += on =>
            {
                if (Ses == null) return;
                Ses.LiveVoice = on;
                Ses.Log(SessionEventKind.Voice, "Host", on ? "live voice ON" : "live voice OFF");
            };
            muteMeButton.onClick.AddListener(() =>
            {
                if (Ses == null) return;
                Ses.MicMuted = !Ses.MicMuted;
                Ses.Log(SessionEventKind.Mute, "Host", Ses.MicMuted ? "muted the host mic" : "unmuted the host mic");
                nav.Say(Ses.MicMuted ? "Your mic is muted" : "Your mic is live");
            });
            selectAllButton.onClick.AddListener(SelectAll);

            playButton.onClick.AddListener(() => Act(SessionEventKind.Play, "play", () => Svc.Play()));
            pauseButton.onClick.AddListener(() => Act(SessionEventKind.Pause, "pause", () => Svc.Pause()));
            resumeButton.onClick.AddListener(() => Act(SessionEventKind.Play, "resume", () => Svc.Resume()));
            stopButton.onClick.AddListener(() => Act(SessionEventKind.Stop, "stop", () => Svc.Stop()));
            sendButton.onClick.AddListener(SendContent);
            textButton.onClick.AddListener(OpenText);
            muteButton.onClick.AddListener(ToggleMute);
            sceneButton.onClick.AddListener(OpenScene);

            textClose.onClick.AddListener(() => textSheet.SetActive(false));
            sendTextButton.onClick.AddListener(() => SendText(textInput.text));
            for (int i = 0; i < quickButtons.Count; i++)
            {
                int k = i;
                quickButtons[i].onClick.AddListener(() => SendText(Ses != null && k < Ses.QuickMessages.Count ? Ses.QuickMessages[k] : ""));
            }
            textSheet.SetActive(false);
            cardTemplate.gameObject.SetActive(false);
            timelineTemplate.gameObject.SetActive(false);
            chatTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Ses != null) Ses.Changed += OnSession;
            shownEvents = shownChat = -1;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Ses != null) Ses.Changed -= OnSession;
        }

        public override bool HandleBack()
        {
            if (!textSheet.activeSelf) return false;
            textSheet.SetActive(false);
            return true;
        }

        private void OnSession() { if (isActiveAndEnabled) Refresh(); }

        private void Update()
        {
            if (Ses == null || Time.unscaledTime < nextClock) return;
            nextClock = Time.unscaledTime + 0.5f;
            recordingText.text = "Recording on " + Ses.RecorderName + "  " + Fmt.Clock(Ses.RecordingTime);
            timerText.text = "Session " + Fmt.Clock(Ses.Elapsed);
            recordingDot.color = new Color(recordingDot.color.r, recordingDot.color.g, recordingDot.color.b, Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? 1f : 0.25f);
        }

        public override void Refresh()
        {
            if (Ses == null) return;
            subtitleText.text = SessionStartScreen.ModeLine(true);
            recordingDot.color = ThemeManager.Get(DemoConfig.RecordingRole);
            voiceSwitch.Set(Ses.LiveVoice, false);
            muteMeChip.Set(Ses.MicMuted);
            muteMeChip.SetLabel(Ses.MicMuted ? "Muted" : "Mute me");
            hostMic.Set(Ses.LiveVoice && !Ses.MicMuted);

            string speaking = "";
            foreach (var d in Svc.Devices) if (d.Id == Ses.SpeakingDeviceId) speaking = d.Name;
            speakingText.text = !Ses.LiveVoice ? "Live voice is off" : speaking.Length > 0 ? "Speaking: " + speaking : "Nobody is speaking";

            // Quest cards
            var cards = Keep(cardTemplate, cardCache, Svc.Devices.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var d = Svc.Devices[i];
                cards[i].Bind(d, DeviceStatus.Content(d, Svc), d.Id == Ses.SpeakingDeviceId, true, () =>
                {
                    if (!d.Online) { nav.Say(d.Name + " is offline"); return; }
                    d.Selected = !d.Selected;
                    Refresh();
                });
            }
            int online = 0, sel = Svc.SelectedOnline().Count;
            foreach (var d in Svc.Devices) if (d.Online) online++;
            selectedText.text = sel + " of " + online + " online selected";
            selectAllLabel.text = sel == online && online > 0 ? "Select none" : "Select all";

            // timeline (rebuilt only when it changed)
            if (shownEvents != Ses.Timeline.Count || shownEvents < 0)
            {
                shownEvents = Ses.Timeline.Count;
                int n = Mathf.Min(MaxTimelineRows, Ses.Timeline.Count);
                var rows = Keep(timelineTemplate, timelineCache, n);
                for (int i = 0; i < n; i++) rows[i].Bind(Ses.Timeline[i]);
                timelineEmpty.gameObject.SetActive(n == 0);
            }
            if (textSheet.activeSelf) RefreshChat();
        }

        // ---------- actions ----------

        private List<QuestDevice> Targets()
        {
            var l = Svc.SelectedOnline();
            if (l.Count == 0) nav.Say("Select at least one online Quest");
            return l;
        }

        private static string Names(List<QuestDevice> l)
        {
            var sb = new StringBuilder();
            foreach (var d in l) { if (sb.Length > 0) sb.Append(", "); sb.Append(d.Name); }
            return sb.ToString();
        }

        private void Act(SessionEventKind kind, string verb, System.Action act)
        {
            var l = Targets();
            if (l.Count == 0) return;
            act();
            var f = Svc.ControlFile;
            Ses.Log(kind, "Host", verb + (kind == SessionEventKind.Play && f != null ? " " + f.Name : "") + " on " + Names(l));
            nav.Say(char.ToUpper(verb[0]) + verb.Substring(1) + " on " + l.Count + " Quest(s)");
        }

        private void SelectAll()
        {
            int online = 0, sel = Svc.SelectedOnline().Count;
            foreach (var d in Svc.Devices) if (d.Online) online++;
            bool all = !(sel == online && online > 0);
            foreach (var d in Svc.Devices) d.Selected = all && d.Online;
            Refresh();
        }

        private void SendContent()
        {
            var l = Targets();
            if (l.Count == 0) return;
            var files = new List<MediaFile>();
            foreach (var f in Svc.Files) { files.Add(f); if (files.Count == 4) break; }
            var names = new string[files.Count];
            var icons = new string[files.Count];
            for (int i = 0; i < files.Count; i++) { names[i] = files[i].Name; icons[i] = MediaTypes.Icon(files[i].Kind); }
            nav.actionSheet.Show("Send content to " + l.Count + " Quest(s)", names, icons, k =>
            {
                Svc.Send(files[k]);
                Ses.Log(SessionEventKind.Content, "Host", "sent " + files[k].Name + " to " + Names(l));
                nav.Say("Sending " + files[k].Name + "...");
            });
        }

        private void ToggleMute()
        {
            var l = Targets();
            if (l.Count == 0) return;
            bool allMuted = true;
            foreach (var d in l) if (!d.Muted) allMuted = false;
            Svc.SetMute(!allMuted);
            Ses.Log(SessionEventKind.Mute, "Host", (allMuted ? "unmuted " : "muted ") + Names(l));
            nav.Say((allMuted ? "Unmuted " : "Muted ") + l.Count + " Quest(s)");
        }

        private void OpenScene()
        {
            var l = Svc.SelectedOnline();
            string name = l.Count == 1 ? l[0].Name : "";
            Ses.Log(SessionEventKind.Info, "Host", "opened the Scene" + (name.Length > 0 ? " of " + name : ""));
            nav.Push(ScreenId.Scene);   // back returns here
            var scene = nav.Get<SceneScreen>();
            if (scene != null) scene.ShowFor(name);
        }

        private void AskEnd()
        {
            nav.popup.Show("End session?", "All Quests leave the session and the PC stops recording.", "End session", () =>
            {
                var summary = Ses.End();
                var s = nav.Get<SessionSummaryScreen>();
                if (s != null) s.summary = summary;
                nav.Tab(ScreenId.SessionSummary);
            }, "Cancel", destructive: true);
        }

        // ---------- text sheet ----------

        private void OpenText()
        {
            if (Targets().Count == 0) return;
            textSheet.SetActive(true);
            for (int i = 0; i < quickLabels.Count; i++)
                quickLabels[i].text = Ses != null && i < Ses.QuickMessages.Count ? Ses.QuickMessages[i] : "";
            shownChat = -1;
            RefreshChat();
        }

        private void SendText(string text)
        {
            if (Ses == null || string.IsNullOrWhiteSpace(text)) return;
            var l = Targets();
            if (l.Count == 0) return;
            Ses.SendText(text, l);
            textInput.text = "";
            nav.Say("Text sent to " + l.Count + " Quest(s)");
        }

        private void RefreshChat()
        {
            var l = Svc.SelectedOnline();
            textToLine.text = l.Count == 0 ? "No Quest selected" : "To: " + Names(l);
            if (shownChat == Ses.Chat.Count) return;
            shownChat = Ses.Chat.Count;
            var rows = Keep(chatTemplate, chatCache, Ses.Chat.Count);
            float w = ((RectTransform)chatTemplate.transform.parent).rect.width - 80f;
            for (int i = 0; i < rows.Count; i++) rows[i].Bind(Ses.Chat[i], w);
            chatEmpty.gameObject.SetActive(Ses.Chat.Count == 0);
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f; // newest at the bottom
        }
    }
}
