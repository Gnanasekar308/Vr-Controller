using System;
using System.Collections.Generic;

namespace MediSimVR
{
    // Demo mode only: a believable live session without any device or network.
    // Quests "speak" in turns (live voice), answer text messages, one drops out and rejoins; the PC "records".
    // All values come from DemoConfig.
    public class MockSessionService : ISessionService
    {
        public event Action Changed;

        private readonly IControllerService devices;
        private readonly List<SessionEvent> timeline = new List<SessionEvent>();
        private readonly List<ChatMessage> chat = new List<ChatMessage>();
        private readonly System.Random rnd = new System.Random(7);
        private readonly List<KeyValuePair<float, ChatMessage>> pendingReplies = new List<KeyValuePair<float, ChatMessage>>();
        private float elapsed, recording, voiceTimer, batteryTimer;
        private bool leftDone, rejoinDone, otherHost = DemoConfig.SimulateOtherHost;
        private string speaking = "";

        private readonly Action devicesChanged;

        // devicesChanged: tells the controller's listeners that a Quest changed (battery, left / joined)
        public MockSessionService(IControllerService controller, Action devicesChanged)
        {
            devices = controller;
            this.devicesChanged = devicesChanged;
        }

        public SessionRole Role { get; private set; }
        public bool Active { get { return Role != SessionRole.None; } }
        public string PlayerDeviceId { get; private set; } = "";
        public TimeSpan Elapsed { get { return TimeSpan.FromSeconds(elapsed); } }
        public bool Recording { get { return Role == SessionRole.Host; } }
        public TimeSpan RecordingTime { get { return TimeSpan.FromSeconds(recording); } }
        public string RecorderName { get { return DemoConfig.RecorderName; } }
        public bool LiveVoice { get; set; } = true;
        public bool MicMuted { get; set; }
        public string SpeakingDeviceId { get { return Active && LiveVoice ? speaking : ""; } }
        public bool OtherHostActive { get { return otherHost; } }
        public string OtherHostText { get { return DemoConfig.HostActiveText; } }
        public IReadOnlyList<SessionEvent> Timeline { get { return timeline; } }
        public IReadOnlyList<ChatMessage> Chat { get { return chat; } }
        public IReadOnlyList<string> QuickMessages { get { return DemoConfig.QuickMessages; } }

        private void Fire() { if (Changed != null) Changed(); }

        public bool StartHost(bool takeOver)
        {
            if (otherHost && !takeOver) return false;
            if (otherHost) { otherHost = false; Add(SessionEventKind.Info, "Host", "took over from the other host phone"); }
            Begin(SessionRole.Host);
            recording = DemoConfig.RecordingStartSeconds;
            Add(SessionEventKind.Info, "Host", "session started, recording on " + DemoConfig.RecorderName);
            foreach (var d in devices.Devices)
                Add(d.Online ? SessionEventKind.Join : SessionEventKind.Leave, d.Name, d.Online ? "joined" : "is offline");
            Fire();
            return true;
        }

        public void StartPlayer(string deviceId)
        {
            Begin(SessionRole.Player);
            PlayerDeviceId = deviceId ?? "";
            var d = Find(PlayerDeviceId);
            Add(SessionEventKind.Join, d != null ? d.Name : PlayerDeviceId, "joined as player");
            Fire();
        }

        private void Begin(SessionRole role)
        {
            Role = role;
            elapsed = 0f;
            voiceTimer = 1.5f;
            batteryTimer = 0f;
            speaking = "";
            leftDone = rejoinDone = false;
            timeline.Clear();
            chat.Clear();
            pendingReplies.Clear();
            PlayerDeviceId = "";
        }

        public SessionSummary End()
        {
            var s = new SessionSummary { Duration = Elapsed, Role = Role };
            if (Role == SessionRole.Player) s.Devices = 1;
            else foreach (var d in devices.Devices) if (d.Online) s.Devices++;
            foreach (var e in timeline)
            {
                int n;
                s.Counts.TryGetValue(e.Kind, out n);
                s.Counts[e.Kind] = n + 1;
            }
            string path = string.Format(DemoConfig.SavedFileFormat, DateTime.Now);
            s.SavedPath = Role == SessionRole.Player ? path.Replace("_host", "_player") : path;
            Role = SessionRole.None;
            PlayerDeviceId = "";
            speaking = "";
            pendingReplies.Clear();
            Fire();
            return s;
        }

        public void SendText(string text, List<QuestDevice> to)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || to == null || to.Count == 0) return;
            chat.Add(new ChatMessage { Time = DateTime.Now, From = Role == SessionRole.Player ? "Me" : "Host", Text = text, Mine = true });
            var names = new List<string>();
            foreach (var d in to) names.Add(d.Name);
            Add(SessionEventKind.Text, "Host", "text to " + string.Join(", ", names.ToArray()) + ": \"" + text + "\"");
            for (int i = 0; i < to.Count && i < 2; i++) // the first two answer
            {
                var reply = new ChatMessage { From = to[i].Name, Text = DemoConfig.Replies[rnd.Next(DemoConfig.Replies.Length)] };
                pendingReplies.Add(new KeyValuePair<float, ChatMessage>(elapsed + DemoConfig.ReplyDelay * (i + 1), reply));
            }
            Fire();
        }

        public void Log(SessionEventKind kind, string device, string message)
        {
            if (!Active) return;
            Add(kind, device, message);
            Fire();
        }

        private void Add(SessionEventKind kind, string device, string message)
        {
            timeline.Insert(0, new SessionEvent { Time = DateTime.Now, Kind = kind, Device = device ?? "", Message = message ?? "" });
            while (timeline.Count > 200) timeline.RemoveAt(timeline.Count - 1);
        }

        private QuestDevice Find(string id)
        {
            foreach (var d in devices.Devices) if (d.Id == id) return d;
            return null;
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            elapsed += dt;
            if (Recording) recording += dt;
            bool changed = false;

            // replies to text messages
            for (int i = pendingReplies.Count - 1; i >= 0; i--)
            {
                if (elapsed < pendingReplies[i].Key) continue;
                var m = pendingReplies[i].Value;
                m.Time = DateTime.Now;
                chat.Add(m);
                Add(SessionEventKind.Text, m.From, "replied: \"" + m.Text + "\"");
                pendingReplies.RemoveAt(i);
                changed = true;
            }

            // live voice: one online, unmuted Quest talks at a time
            if (LiveVoice && (voiceTimer -= dt) <= 0f)
            {
                if (speaking.Length > 0)
                {
                    speaking = "";
                    voiceTimer = Range(DemoConfig.SilenceMin, DemoConfig.SilenceMax);
                }
                else
                {
                    var talkers = new List<QuestDevice>();
                    foreach (var d in devices.Devices)
                        if (d.Online && !d.Muted && (Role == SessionRole.Host || d.Id != PlayerDeviceId)) talkers.Add(d);
                    if (talkers.Count > 0)
                    {
                        var t = talkers[rnd.Next(talkers.Count)];
                        speaking = t.Id;
                        Add(SessionEventKind.Voice, t.Name, DemoConfig.VoiceLines[rnd.Next(DemoConfig.VoiceLines.Length)]);
                    }
                    voiceTimer = Range(DemoConfig.SpeakMin, DemoConfig.SpeakMax);
                }
                changed = true;
            }
            if (!LiveVoice && speaking.Length > 0) { speaking = ""; changed = true; }

            // one Quest drops out and comes back (host sessions)
            bool devChanged = false;
            if (Role == SessionRole.Host && DemoConfig.SimulateLeaveJoin)
            {
                var q = Find(DemoConfig.LeaveJoinQuestId);
                if (q != null && !leftDone && elapsed >= DemoConfig.LeaveAfter && q.Online)
                {
                    leftDone = true;
                    q.Online = false;
                    if (speaking == q.Id) speaking = "";
                    Add(SessionEventKind.Leave, q.Name, "left (Wi-Fi lost)");
                    devChanged = true;
                }
                if (q != null && leftDone && !rejoinDone && elapsed >= DemoConfig.RejoinAfter)
                {
                    rejoinDone = true;
                    q.Online = true;
                    Add(SessionEventKind.Join, q.Name, "joined again");
                    devChanged = true;
                }
            }

            // batteries go down slowly
            batteryTimer += dt;
            if (batteryTimer >= 60f / Math.Max(0.01f, DemoConfig.BatteryDrainPerMinute))
            {
                batteryTimer = 0f;
                foreach (var d in devices.Devices) if (d.Online && d.Battery > 5) d.Battery--;
                devChanged = true;
            }

            if (changed || devChanged) Fire();
            if (devChanged && devicesChanged != null) devicesChanged();
        }

        private float Range(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
    }
}
