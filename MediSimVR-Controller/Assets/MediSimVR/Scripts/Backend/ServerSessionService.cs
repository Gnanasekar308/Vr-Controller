using System;
using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    // Real ISessionService through the Medisim PC server (demo mode OFF, MedisimBackend.UseServer):
    //   live voice  -> PhoneVoice (server voice relay UDP 5082): the phone talks to the selected stations (open mic,
    //                  muted with MicMuted), the students answer with push-to-talk (A / X); QuestMics turns their mics off
    //   text / Speak -> station text panel ("showtext" / "speak": shown and read aloud by the station PC)
    //   Upload      -> POST /api/sessions (saved on the PC in MedisimVR.LocalServer/Sessions)
    // Timeline and chat stay on this phone (same as the demo service). Not available yet: PC recording, another
    // host phone detection, student text replies.
    public class ServerSessionService : ISessionService, IDisposable
    {
        public event Action Changed;

        private const float TargetsEvery = 3f;

        private readonly ServerControllerService devices;
        private readonly MedisimServer server = MedisimServer.Instance;
        private readonly List<SessionEvent> timeline = new List<SessionEvent>();
        private readonly List<ChatMessage> chat = new List<ChatMessage>();
        private readonly Dictionary<string, bool> wasOnline = new Dictionary<string, bool>();
        private readonly GameObject voiceObject;
        private readonly PhoneVoice voice;
        private readonly string facultyId;
        private float elapsed, nextTargets, nextPresence;
        private string targetsKey = "", speaking = "";
        private bool questMics = true;

        public ServerSessionService(ServerControllerService controller)
        {
            devices = controller;
            facultyId = MedisimBackend.FacultyId();
            voiceObject = new GameObject("MedisimPhoneVoice");
            voice = voiceObject.AddComponent<PhoneVoice>();
            voice.Init(facultyId);
            server.ServerChanged += OnServerChanged;
        }

        public SessionRole Role { get; private set; }
        public bool Active { get { return Role != SessionRole.None; } }
        public string PlayerDeviceId { get; private set; } = "";
        public TimeSpan Elapsed { get { return TimeSpan.FromSeconds(elapsed); } }
        public bool Recording { get { return false; } }               // PC recording is not built yet
        public TimeSpan RecordingTime { get { return TimeSpan.Zero; } }
        public string RecorderName { get { return server.MachineName.Length > 0 ? server.MachineName : "PC server"; } }
        public bool LiveVoice { get; set; } = true;
        public bool MicMuted { get; set; }
        public bool PhoneSound { get; set; } = true;

        public bool QuestMics
        {
            get { return questMics; }
            set
            {
                if (questMics == value) return;
                questMics = value;
                SendMics();
            }
        }

        public string SpeakingDeviceId { get { return Active && LiveVoice ? speaking : ""; } }
        public IReadOnlyList<string> Instructions { get { return DemoConfig.Instructions; } }   // the configured instruction list
        public bool OtherHostActive { get { return false; } }
        public string OtherHostText { get { return ""; } }
        public IReadOnlyList<SessionEvent> Timeline { get { return timeline; } }
        public IReadOnlyList<ChatMessage> Chat { get { return chat; } }
        public IReadOnlyList<string> QuickMessages { get { return DemoConfig.QuickMessages; } }

        private void Fire() { if (Changed != null) Changed(); }

        // ---------------------------------------------------------------- session

        public bool StartHost(bool takeOver)
        {
            Begin(SessionRole.Host);
            Add(SessionEventKind.Info, "Host", "session started" + (server.HasServer ? " (PC server " + RecorderName + ")" : " (PC server not connected)"));
            foreach (var d in devices.Devices)
                Add(d.Online ? SessionEventKind.Join : SessionEventKind.Leave, d.Name, d.Online ? "joined" : "is offline");
            SendMics();
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
            speaking = "";
            PlayerDeviceId = "";
            timeline.Clear();
            chat.Clear();
            wasOnline.Clear();
            foreach (var d in devices.Devices) wasOnline[d.Id] = d.Online;
            targetsKey = "";
            nextTargets = 0f;
        }

        public SessionSummary End()
        {
            var s = new SessionSummary { Duration = Elapsed, Role = Role, SavedPath = SessionLog.FilePath };
            if (Role == SessionRole.Player) s.Devices = 1;
            else foreach (var d in devices.Devices) if (d.Online) s.Devices++;
            foreach (var e in timeline)
            {
                int n;
                s.Counts.TryGetValue(e.Kind, out n);
                s.Counts[e.Kind] = n + 1;
            }

            Role = SessionRole.None;
            PlayerDeviceId = "";
            speaking = "";
            SetVoiceTargets(new List<string>()); // nobody hears this phone any more
            Fire();
            return s;
        }

        // ---------------------------------------------------------------- text and read aloud

        public void SendText(string text, List<QuestDevice> to)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || to == null || to.Count == 0) return;
            chat.Add(new ChatMessage { Time = DateTime.Now, From = Role == SessionRole.Player ? "Me" : "Host", Text = text, Mine = true });
            Add(SessionEventKind.Text, "Host", "text to " + Names(to) + ": \"" + text + "\"");
            float seconds = Mathf.Clamp(8f + text.Length / 10f, 10f, 60f);
            devices.Command(to, new StationCommandRequest { cmd = "showtext", title = "Message from your teacher", text = text, seconds = seconds });
            Fire();
        }

        public void Speak(SessionEventKind kind, string text, List<QuestDevice> to)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || to == null || to.Count == 0) return;
            bool instruction = kind == SessionEventKind.Instruction;
            string who = Role == SessionRole.Player ? (to.Count > 0 ? to[0].Name : "Player") : "Host";
            Add(kind, who, (instruction ? "important instruction" : "lesson note") + " read aloud on " + Names(to) + ": \"" + text + "\"");
            devices.Command(to, new StationCommandRequest { cmd = "speak", title = instruction ? "Important instruction" : "Lesson note", text = text });
            Fire();
        }

        // ---------------------------------------------------------------- session record

        public void Upload(string fileName, string json, Action<bool, string> done)
        {
            string body = JsonUtility.ToJson(new SessionUploadRequest { fileName = fileName, json = json });
            server.Request("POST", "/api/sessions", body, r =>
            {
                SessionUploadReply reply = null;
                try { reply = JsonUtility.FromJson<SessionUploadReply>(r.Text); } catch (Exception) { }

                if (done == null) return;
                if (r.Ok && reply != null) done(true, "Sent to " + (string.IsNullOrEmpty(reply.machine) ? "the PC server" : reply.machine) + ": " + reply.saved);
                else done(false, "Not sent to the PC: " + (r.Error.Length > 0 ? r.Error : reply != null && !string.IsNullOrEmpty(reply.error) ? reply.error : "HTTP " + r.Code));
            }, 15000);
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

        // ---------------------------------------------------------------- per frame

        public void Tick(float dt)
        {
            bool live = Active && LiveVoice;
            voice.Transmit = live && !MicMuted;
            voice.Listen = live && PhoneSound;

            if (!Active) return;
            elapsed += dt;
            bool changed = false;

            string now = live ? voice.Speaking() : "";
            if (now != speaking)
            {
                speaking = now;
                var d = Find(now);
                if (now.Length > 0) Add(SessionEventKind.Voice, d != null ? d.Name : now, "is speaking");
                changed = true;
            }

            if (Time.unscaledTime >= nextTargets)
            {
                nextTargets = Time.unscaledTime + 0.5f;
                SyncVoiceTargets();
            }

            if (Time.unscaledTime >= nextPresence)
            {
                nextPresence = Time.unscaledTime + 1f;
                changed |= TrackPresence();
            }

            if (changed) Fire();
        }

        // Who hears the phone: the selected online stations (host) or the player's own station.
        private void SyncVoiceTargets()
        {
            var ids = new List<string>();

            if (Role == SessionRole.Player)
            {
                if (PlayerDeviceId.Length > 0) ids.Add(PlayerDeviceId);
            }
            else
            {
                foreach (var d in devices.SelectedOnline()) ids.Add(d.Id);
            }

            string key = string.Join(",", ids.ToArray());

            if (key != targetsKey || Time.unscaledTime >= nextTargetsForce)
            {
                targetsKey = key;
                nextTargetsForce = Time.unscaledTime + TargetsEvery * 3f; // also after a server restart
                SetVoiceTargets(ids);
            }
        }

        private float nextTargetsForce;

        private void SetVoiceTargets(List<string> ids)
        {
            if (!server.HasServer) return;
            server.Request("POST", "/api/voice/targets",
                JsonUtility.ToJson(new VoiceTargetsRequest { facultyId = facultyId, targetDeviceIds = ids.ToArray() }), null);
        }

        // Quest mics switch -> every online station (students' push-to-talk on / off).
        private void SendMics()
        {
            var online = new List<QuestDevice>();
            foreach (var d in devices.Devices) if (d.Online) online.Add(d);
            if (online.Count > 0) devices.Command(online, new StationCommandRequest { cmd = "mic", mute = !questMics });
        }

        private bool TrackPresence()
        {
            bool changed = false;

            foreach (var d in devices.Devices)
            {
                bool before;
                bool known = wasOnline.TryGetValue(d.Id, out before);
                if (known && before == d.Online) continue;
                wasOnline[d.Id] = d.Online;
                if (!known && !d.Online) continue;
                Add(d.Online ? SessionEventKind.Join : SessionEventKind.Leave, d.Name, d.Online ? (known ? "joined again" : "joined") : "left");
                if (d.Online && !questMics) devices.Command(new List<QuestDevice> { d }, new StationCommandRequest { cmd = "mic", mute = true });
                changed = true;
            }

            return changed;
        }

        private void OnServerChanged()
        {
            targetsKey = ""; // send the voice targets again to the (new) server
        }

        private QuestDevice Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var d in devices.Devices) if (d.Id == id) return d;
            return null;
        }

        private static string Names(List<QuestDevice> to)
        {
            var names = new List<string>();
            foreach (var d in to) names.Add(d.Name);
            return string.Join(", ", names.ToArray());
        }

        public void Dispose()
        {
            server.ServerChanged -= OnServerChanged;
            if (voiceObject != null) UnityEngine.Object.Destroy(voiceObject);
        }
    }
}
