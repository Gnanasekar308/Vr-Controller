using System;
using System.Collections.Generic;

namespace MediSimVR
{
    public enum SessionRole { None, Host, Player }
    // new kinds go at the end (TimelineRowView has one icon per kind, in this order)
    public enum SessionEventKind { Play, Pause, Stop, Content, Text, Voice, Join, Leave, Object, Mute, Info, Note, Instruction, Audio }

    public class SessionEvent
    {
        public DateTime Time;
        public SessionEventKind Kind;
        public string Device = "";    // Quest name, "Host" or "PC"
        public string Message = "";
    }

    public class ChatMessage
    {
        public DateTime Time;
        public string From = "";
        public string Text = "";
        public bool Mine;              // sent by this phone
    }

    public class SessionSummary
    {
        public TimeSpan Duration;
        public int Devices;
        public SessionRole Role;
        public string SavedPath = "";
        public readonly Dictionary<SessionEventKind, int> Counts = new Dictionary<SessionEventKind, int>();
    }

    // Everything the Session tab needs (host / player, voice, text, recording, timeline). Today: MockSessionService
    // (demo mode). Later: the real backend implements this; the UI does not change. Device control itself (play,
    // pause, send ...) stays in IControllerService; the UI logs those actions here so they show in the timeline.
    public interface ISessionService
    {
        event Action Changed;

        SessionRole Role { get; }
        bool Active { get; }
        string PlayerDeviceId { get; }          // Player mode: the one Quest of this phone
        TimeSpan Elapsed { get; }

        bool Recording { get; }
        TimeSpan RecordingTime { get; }
        string RecorderName { get; }

        bool LiveVoice { get; set; }
        bool MicMuted { get; set; }             // this phone's (sender) microphone
        bool PhoneSound { get; set; }           // this phone's (sender) sound: voices, read-aloud, preview
        bool QuestMics { get; set; }            // the Quests' (receiver) microphones
        string SpeakingDeviceId { get; }        // "" = nobody

        IReadOnlyList<string> Instructions { get; }      // important instructions (quick buttons)

        bool OtherHostActive { get; }           // "Host already active" (another phone hosts the room)
        string OtherHostText { get; }

        IReadOnlyList<SessionEvent> Timeline { get; }   // newest first
        IReadOnlyList<ChatMessage> Chat { get; }        // oldest first
        IReadOnlyList<string> QuickMessages { get; }

        bool StartHost(bool takeOver);          // false = another host is active and takeOver was false
        void StartPlayer(string deviceId);
        SessionSummary End();
        void SendText(string text, List<QuestDevice> to);
        // a lesson note (kind Note) or an important instruction (kind Instruction) the Quests read aloud
        void Speak(SessionEventKind kind, string text, List<QuestDevice> to);
        // send the session record (JSON) to the PC; done(ok, message) on the main thread
        void Upload(string fileName, string json, Action<bool, string> done);
        void Log(SessionEventKind kind, string device, string message);
        void Tick(float dt);
    }
}
