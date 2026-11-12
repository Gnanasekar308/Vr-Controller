using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // PLAYER: one Quest. Status, Play / Pause / Stop / Mute for that Quest, voice bar, shortcut to its Scene, Leave.
    public class SessionPlayerScreen : AppScreen
    {
        public Button backButton, leaveButton, playButton, pauseButton, stopButton, muteButton, sceneButton, muteMeButton;
        public TMP_Text subtitleText, questName, statusText, contentText, speakingText, muteLabel;
        public Image statusDot;
        public ToggleSwitch voiceSwitch;
        public ChipToggle muteMeChip;
        public SpeakingPulse questMic, myMic;

        private ISessionService Ses { get { return AppRoot.Session; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
            leaveButton.onClick.AddListener(() => nav.popup.Show("Leave session?", "Your Quest leaves the live session.", "Leave", () =>
            {
                if (Ses == null) return;
                var summary = Ses.End();
                var s = nav.Get<SessionSummaryScreen>();
                if (s != null) s.summary = summary;
                nav.Tab(ScreenId.SessionSummary);
            }, "Cancel", destructive: true));
            playButton.onClick.AddListener(() => Do(SessionEventKind.Play, "play", () => Svc.Play()));
            pauseButton.onClick.AddListener(() => Do(SessionEventKind.Pause, "pause", () => Svc.Pause()));
            stopButton.onClick.AddListener(() => Do(SessionEventKind.Stop, "stop", () => Svc.Stop()));
            muteButton.onClick.AddListener(() =>
            {
                var d = Quest();
                if (d == null) return;
                Do(SessionEventKind.Mute, d.Muted ? "unmute" : "mute", () => Svc.SetMute(!d.Muted));
            });
            sceneButton.onClick.AddListener(() =>
            {
                var d = Quest();
                nav.Tab(ScreenId.Scene);
                var scene = nav.Get<SceneScreen>();
                if (scene != null && d != null) scene.ShowFor(d.Name);
            });
            voiceSwitch.Changed += on =>
            {
                if (Ses == null) return;
                Ses.LiveVoice = on;
                Ses.Log(SessionEventKind.Voice, Me(), on ? "live voice ON" : "live voice OFF");
                Refresh();
            };
            muteMeButton.onClick.AddListener(() =>
            {
                if (Ses == null) return;
                Ses.MicMuted = !Ses.MicMuted;
                Ses.Log(SessionEventKind.Mute, Me(), Ses.MicMuted ? "muted the player mic" : "unmuted the player mic");
                nav.Say(Ses.MicMuted ? "Your mic is muted" : "Your mic is live");
                Refresh();
            });
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Ses != null) Ses.Changed += OnSession;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Ses != null) Ses.Changed -= OnSession;
        }

        private void OnSession() { if (isActiveAndEnabled) Refresh(); }

        private QuestDevice Quest()
        {
            if (Ses == null) return null;
            foreach (var d in Svc.Devices) if (d.Id == Ses.PlayerDeviceId) return d;
            return null;
        }

        private string Me() { var d = Quest(); return d != null ? d.Name : "Player"; }

        private void Do(SessionEventKind kind, string verb, System.Action act)
        {
            var d = Quest();
            if (d == null || !d.Online) { nav.Say("Your Quest is offline"); return; }
            foreach (var x in Svc.Devices) x.Selected = x == d;
            act();
            Ses.Log(kind, d.Name, verb);
            nav.Say(char.ToUpper(verb[0]) + verb.Substring(1) + " on " + d.Name);
        }

        public override void Refresh()
        {
            var d = Quest();
            subtitleText.text = SessionStartScreen.ModeLine(true);
            if (d == null)
            {
                questName.text = "No Quest";
                statusText.text = "Pick a Quest on the Session tab";
                contentText.text = "";
                return;
            }
            questName.text = d.Name;
            statusDot.color = d.Online ? ThemeManager.Get(ColorRole.Accent) : ThemeManager.Get(ColorRole.SubText);
            statusText.text = d.Online ? "Online  •  " + (d.Battery >= 0 ? d.Battery + "% battery" : "battery --") + (d.Muted ? "  •  muted" : "") : "Offline";
            contentText.text = DeviceStatus.Content(d, Svc);
            muteLabel.text = d.Muted ? "Unmute" : "Mute";
            bool live = Ses != null && Ses.LiveVoice;
            voiceSwitch.Set(live, false);
            muteMeChip.Set(Ses != null && Ses.MicMuted);
            muteMeChip.SetLabel(Ses != null && Ses.MicMuted ? "Muted" : "Mute me");
            myMic.Set(live && Ses != null && !Ses.MicMuted);
            string speaking = "";
            if (Ses != null) foreach (var x in Svc.Devices) if (x.Id == Ses.SpeakingDeviceId) speaking = x.Name;
            questMic.Set(speaking.Length > 0);
            speakingText.text = !live ? "Live voice is off" : speaking.Length > 0 ? "Speaking: " + speaking : "Nobody is speaking";
        }
    }
}
