using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Control tab = last step of the Control Mode flow (Session > Devices > Send > Control):
    //   target card (Playing on / My Quest + Change), player card (this phone plays the same video / audio as the Quests,
    //   mirroring their Play / Pause / Stop; demo samples without media show the thumbnail), "View in Scene" (opening the
    //   Scene pauses the video until Play / Resume), playback controls (Play, Pause, Resume, Stop, Mute), Audio (sound and
    //   mic of this phone = sender and of the Quests = receiver), Live voice, Lesson notes and Important instructions
    //   (the Quests read them aloud), and where the JSON session record is saved / sent. Every action goes to SessionLog.
    public class ControlScreen : AppScreen
    {
        public TMP_Text subtitleText, targetText;
        public Button changeTargetButton;

        // player card
        public PhoneVideoPlayer phone;
        public RawImage videoImage;
        public AspectRatioFitter videoFitter;
        public UIGradient thumb;
        public Image thumbIcon;
        public Sprite videoIcon, audioIcon, imageIcon, textIcon;
        public TMP_Text fileName, fileInfo, previewStatus, previewBadge, elapsedText, durationText;
        public RectTransform progressFill;
        public GameObject progressRow;
        public Button sceneButton, fileButton;

        public PlaybackControlsView controls;

        // audio: phone (sender) / Quests (receiver)
        public ToggleSwitch phoneSoundSwitch, phoneMicSwitch, questSoundSwitch, questMicSwitch;
        // live voice
        public ToggleSwitch voiceSwitch;
        public TMP_Text speakingText;
        public SpeakingPulse voicePulse;
        // lesson notes + important instructions
        public TMP_InputField noteInput;
        public Button readNoteButton;
        public TMP_Text notesInfo;
        public List<Button> instructionButtons = new List<Button>();
        public List<TMP_Text> instructionLabels = new List<TMP_Text>();
        // session record
        public TMP_Text recordText;

        private float nextSync;
        private readonly StringBuilder sb = new StringBuilder();

        private ISessionService Ses { get { return AppRoot.Session; } }
        private string Me { get { return ControlFlow.IsPlayer ? ControlFlow.PlayerQuestName(Svc) : "Host"; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            controls.Init();
            controls.Acted += OnActed;
            changeTargetButton.onClick.AddListener(() => nav.Push(ControlFlow.IsPlayer ? ScreenId.SessionPlayerPick : ScreenId.Devices));
            fileButton.onClick.AddListener(() => nav.Push(ScreenId.Send));
            sceneButton.onClick.AddListener(() => nav.Push(ScreenId.Scene));   // the Scene pauses the video (SceneScreen.OnShow)

            phoneSoundSwitch.Changed += on => { SessionLog.PhoneSound = on; phone.SetMute(!on); if (!on) PhoneSpeech.Stop(); };
            // the mics are part of live voice: without the session backend they do nothing, so say so and keep the switch
            phoneMicSwitch.Changed += on =>
            {
                if (Ses == null) { nav.Say("The phone mic needs the session backend (live voice)"); Refresh(); return; }
                SessionLog.PhoneMic = on;
                nav.Say(on ? "Your mic is on" : "Your mic is muted");
            };
            questSoundSwitch.Changed += on =>
            {
                if (Targets().Count == 0) { nav.Say("No Quest selected"); Refresh(); return; }
                Svc.SetMute(!on);
                SessionLog.Add(SessionEventKind.Mute, Me, (on ? "Quest sound on: " : "Quest sound off: ") + Names(Targets()));
            };
            questMicSwitch.Changed += on =>
            {
                if (Ses == null) { nav.Say("Quest mics need the session backend (live voice)"); Refresh(); return; }
                SessionLog.QuestMics = on;
                nav.Say(on ? "Quest mics on" : "Quest mics muted");
            };
            voiceSwitch.Changed += on =>
            {
                if (Ses == null) { nav.Say("Live voice needs the session backend"); Refresh(); return; }
                Ses.LiveVoice = on;
                SessionLog.Add(SessionEventKind.Voice, Me, on ? "live voice ON" : "live voice OFF");
            };
            readNoteButton.onClick.AddListener(() =>
            {
                string text = noteInput.text;
                if (text.Trim().Length == 0) { nav.Say("Type a lesson note first"); return; }
                Speak(SessionEventKind.Note, text);
                noteInput.text = "";
            });
            for (int i = 0; i < instructionButtons.Count; i++)
            {
                int k = i;
                instructionButtons[i].onClick.AddListener(() => Speak(SessionEventKind.Instruction, instructionLabels[k].text));
            }
        }

        public override void OnShow()
        {
            if (ControlFlow.IsPlayer) ControlFlow.SelectOnly(Svc, ControlFlow.MyQuest(Svc));   // player: My Quest only
            phone.Load(Svc.ControlFile);
            nextSync = 0f;
            base.OnShow();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Ses != null) Ses.Changed += OnSession;
            SessionLog.Changed += OnSession;
            if (phone != null) phone.Changed += OnSession;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Ses != null) Ses.Changed -= OnSession;
            SessionLog.Changed -= OnSession;
            if (phone != null) { phone.Changed -= OnSession; phone.Pause(); }   // no preview on other screens; resyncs on return
        }

        private void OnSession() { if (isActiveAndEnabled) Refresh(); }

        private List<QuestDevice> Targets() { return Svc.SelectedOnline(); }

        private string Names(List<QuestDevice> l)
        {
            sb.Length = 0;
            for (int i = 0; i < l.Count && i < 2; i++) { if (sb.Length > 0) sb.Append(", "); sb.Append(l[i].Name); }
            if (l.Count > 2) sb.Append(" +").Append(l.Count - 2);
            return sb.ToString();
        }

        private void Speak(SessionEventKind kind, string text)
        {
            var t = Targets();
            if (t.Count == 0) { nav.Say("No Quest selected"); return; }
            bool aloud = SessionLog.Speak(kind, text, t);
            nav.Say(aloud ? (t.Count == 1 ? t[0].Name : t.Count + " Quests") + " read it aloud" : "Saved in the session record (Quest receiver not connected)");
        }

        private void OnActed(SessionEventKind kind, string verb)
        {
            var t = Targets();
            var f = Svc.ControlFile;
            SessionLog.Add(kind, Me, verb + (kind == SessionEventKind.Play && f != null ? " " + f.Name : "") + " on " + Names(t));
            nav.Say(char.ToUpper(verb[0]) + verb.Substring(1) + " on " + t.Count + " Quest(s)");
            nextSync = 0f;
        }

        // ---------- the phone player follows the Quests ----------

        private QuestDevice Lead(MediaFile f, List<QuestDevice> t)
        {
            if (f == null) return null;
            foreach (var d in t) if (d.FileId == f.Id) return d;
            return null;
        }

        private void Update()
        {
            if (Svc == null || phone == null || Time.unscaledTime < nextSync) return;
            nextSync = Time.unscaledTime + 0.2f;
            var f = Svc.ControlFile;
            var t = Targets();
            phone.Load(f);
            phone.SetMute(!SessionLog.PhoneSound);
            var lead = Lead(f, t);
            if (lead == null) { if (phone.IsPlaying) phone.Pause(); }
            else if (lead.State == DeviceState.Playing)
            {
                if (!phone.IsPlaying)
                {
                    if (phone.Ready && lead.PositionSec > 0f && Mathf.Abs(phone.Time - lead.PositionSec) > 1.5f) phone.Seek(lead.PositionSec);
                    phone.Play();
                }
            }
            else if (lead.State == DeviceState.Paused) { if (phone.IsPlaying) phone.Pause(); }
            else if (lead.State == DeviceState.Stopped || lead.State == DeviceState.Idle) { if (phone.IsPlaying || phone.Time > 0.1f) phone.Stop(); }
            UpdateProgress(f, lead);
            UpdateVoice();
        }

        private void UpdateProgress(MediaFile f, QuestDevice lead)
        {
            bool timed = f != null && (f.Kind == MediaKind.Video || f.Kind == MediaKind.Audio);
            progressRow.SetActive(timed);
            if (!timed) return;
            float pos, len;
            if (phone.Ready && phone.Length > 0f) { pos = phone.Time; len = phone.Length; }
            else { pos = lead != null ? lead.PositionSec : 0f; len = Mathf.Max(0f, f.DurationSec); }
            progressFill.anchorMax = new Vector2(len > 0f ? Mathf.Clamp01(pos / len) : 0f, 1f);
            elapsedText.text = Fmt.Dur(Mathf.FloorToInt(pos));
            durationText.text = len > 0f ? Fmt.Dur(Mathf.FloorToInt(len)) : "--:--";
        }

        private void UpdateVoice()
        {
            bool live = Ses != null && Ses.LiveVoice;
            string speaking = "";
            if (Ses != null) foreach (var d in Svc.Devices) if (d.Id == Ses.SpeakingDeviceId) speaking = d.Name;
            voicePulse.Set(live && speaking.Length > 0);
            speakingText.text = Ses == null ? "Live voice needs the session backend" : !live ? "Live voice is off"
                : speaking.Length > 0 ? "Speaking: " + speaking : "Nobody is speaking";
        }

        // ---------- refresh ----------

        public override void Refresh()
        {
            var t = Targets();
            string step = ControlFlow.Step(ScreenId.Control);
            subtitleText.text = step.Length > 0 ? step : "Control the selected Quests";
            if (ControlFlow.IsPlayer)
            {
                var q = ControlFlow.MyQuest(Svc);
                targetText.text = "My Quest: " + (q == null ? "no Quest online" : q.Name + (q.Online ? "" : " (offline)"));
            }
            else targetText.text = t.Count == 0 ? "No Quest selected" : "Playing on: " + Names(t);

            var f = Svc.ControlFile;
            fileName.text = f != null ? f.Name : "No content: tap to choose";
            fileInfo.text = f != null ? MediaTypes.Label(f.Kind) + "  •  " + Fmt.Size(f.SizeBytes) + (f.DurationSec > 0 ? "  •  " + Fmt.Dur(f.DurationSec) : "") : "Send tab";
            bool showVideo = f != null && phone.Ready && phone.IsVideo && phone.VideoTexture != null;
            videoImage.gameObject.SetActive(showVideo);
            if (showVideo)
            {
                videoImage.texture = phone.VideoTexture;
                videoFitter.aspectRatio = (float)phone.VideoTexture.width / Mathf.Max(1, phone.VideoTexture.height);
            }
            if (f != null) thumb.SetColors(f.ThumbTop, f.ThumbBottom);
            thumbIcon.gameObject.SetActive(!showVideo);
            thumbIcon.sprite = f == null ? videoIcon : f.Kind == MediaKind.Video ? videoIcon : f.Kind == MediaKind.Audio ? audioIcon : f.Kind == MediaKind.Image ? imageIcon : textIcon;
            previewStatus.text = phone.Status;
            previewStatus.transform.parent.gameObject.SetActive(phone.Status.Length > 0);   // text + its dark background
            previewBadge.text = phone.HasMedia ? "ON THIS PHONE" : "ON THE QUEST";

            controls.Bind(f, t, ControlFlow.IsPlayer ? "Your Quest is offline" : "Select Quests on the Devices tab", null);

            bool anyMuted = false;
            foreach (var d in t) if (d.Muted) anyMuted = true;
            phoneSoundSwitch.Set(SessionLog.PhoneSound, false);
            phoneMicSwitch.Set(Ses != null && SessionLog.PhoneMic, false);   // mics = live voice: off without the backend
            questSoundSwitch.Set(t.Count > 0 && !anyMuted, false);
            questMicSwitch.Set(Ses != null && SessionLog.QuestMics, false);
            voiceSwitch.Set(Ses != null && Ses.LiveVoice, false);
            UpdateVoice();

            var list = Ses != null ? Ses.Instructions : (IReadOnlyList<string>)DemoConfig.Instructions;
            for (int i = 0; i < instructionButtons.Count; i++)
            {
                bool on = i < list.Count;
                instructionButtons[i].gameObject.SetActive(on);
                if (on) instructionLabels[i].text = list[i];
            }
            int notes = SessionLog.Notes.Count, ins = SessionLog.InstructionsSent.Count;
            notesInfo.text = notes + " note(s) and " + ins + " instruction(s) in this session";

            recordText.text = !SessionLog.Recording ? "Starts with a session: Home > Control Mode > HOST or PLAYER"
                : SessionLog.Folder + "/" + System.IO.Path.GetFileName(SessionLog.FilePath) +
                  (SessionLog.UploadStatus.Length > 0 ? "\n" + SessionLog.UploadStatus : "\nOn this phone; sent to the PC at the end");
        }
    }
}
