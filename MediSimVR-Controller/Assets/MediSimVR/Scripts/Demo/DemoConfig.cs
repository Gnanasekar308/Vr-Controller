using System.Collections.Generic;
using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // ===== EVERYTHING THE DEMO SHOWS, IN ONE PLACE =====
    // Texts, colours, mock Quests, files, chat messages and timings of demo mode. Change a value here, rebuild,
    // install: nothing else needs to change. Demo mode is ON by default (Settings > Demo mode switches it).
    public static class DemoConfig
    {
        // ---------- switch ----------
        public const string PrefKey = "msvr_demo";
        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(PrefKey, 1) == 1; }
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ---------- texts ----------
        public const string DemoBadge = "DEMO";
        public const string HostActiveText = "Dr. Priya's phone is hosting this room right now.\nTake over as host?";
        public const string RecorderName = "PC-LAB1";
        public const string SavedFileFormat = "MediSimVR_Sessions/{0:yyyy-MM-dd_HHmm}_host.json";
        public const string SceneSourceName = "Quest-A1B2";

        // the "Host already active" message is shown on the first HOST tap of each app start
        public const bool SimulateOtherHost = true;

        public static readonly string[] QuickMessages =
        {
            "Look at the patient",
            "Start the procedure",
            "Pause and listen",
            "Well done!"
        };

        public static readonly string[] Replies =
        {
            "OK, starting now",
            "Can you repeat that?",
            "Done!",
            "I need a minute",
            "Understood"
        };

        // important instructions (Control tab quick buttons); the Quests read them aloud
        public static readonly string[] Instructions =
        {
            "Put on your gloves",
            "Check the airway first",
            "Stop and look at me",
            "Well done, continue"
        };

        // demo: the phone speaks what the Quests read aloud, so the audience hears it (off = silent)
        public const bool SpeakOnPhone = true;
        public const float UploadSeconds = 1.5f;   // demo "send the session record to the PC" time

        public static readonly string[] VoiceLines =   // shown as "Quest-XXXX: ..." in the timeline
        {
            "asked a question",
            "is describing the step",
            "confirmed the instruction"
        };

        // ---------- colours: theme roles of the design system (so every theme preset stays consistent) ----------
        public const ColorRole SpeakingRole = ColorRole.Success;    // who is speaking (green)
        public const ColorRole RecordingRole = ColorRole.Danger;    // "Recording on PC" dot (red)
        public const ColorRole MutedRole = ColorRole.Warning;       // mute events (orange)
        public const ColorRole MyBubbleRole = ColorRole.Primary;    // own chat messages (blue)
        public const ColorRole LiveRole = ColorRole.Success;        // "LIVE" session indicator (green)

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }

        // ---------- timings (seconds) ----------
        public const int RecordingStartSeconds = 12 * 60 + 34;   // "Recording on PC 00:12:34" when the host screen opens
        public const float SpeakMin = 2.5f, SpeakMax = 5f;       // a Quest talks this long
        public const float SilenceMin = 2f, SilenceMax = 4f;     // pause between talkers
        public const float ReplyDelay = 2.2f;                    // a Quest answers a text after this
        public const float FakeSendSeconds = 1.2f;               // "transfer" time of Send content
        public const float BatteryDrainPerMinute = 0.6f;
        public const float SceneLoadSeconds = 0.6f;
        // one Quest drops out and comes back during a host session (shows Leave / Join in the timeline); false = off
        public const bool SimulateLeaveJoin = true;
        public const string LeaveJoinQuestId = "q4";
        public const float LeaveAfter = 50f, RejoinAfter = 65f;

        // ---------- Quests (one in use, one offline) ----------
        public class Quest
        {
            public string Id, Name, Ip, FileId;
            public bool Online, Muted;
            public int Battery;
            public DeviceState State;
            public float PositionSec;   // where the playing file is at app start
        }

        public static readonly Quest[] Quests =
        {
            new Quest { Id = "q1", Name = "Quest-A1B2", Ip = "192.168.1.21", Online = true, Battery = 86, State = DeviceState.Playing, FileId = "f1", PositionSec = 95f },
            new Quest { Id = "q2", Name = "Quest-C3D4", Ip = "192.168.1.22", Online = true, Battery = 64, State = DeviceState.Idle, FileId = "" },
            new Quest { Id = "q3", Name = "Quest-E5F6", Ip = "192.168.1.23", Online = false, Battery = -1, State = DeviceState.Idle, FileId = "" },
            new Quest { Id = "q4", Name = "Quest-G7H8", Ip = "192.168.1.24", Online = true, Battery = 92, State = DeviceState.Idle, FileId = "", Muted = true },
            new Quest { Id = "q5", Name = "Quest-J9K1", Ip = "192.168.1.25", Online = true, Battery = 71, State = DeviceState.Idle, FileId = "" },
            new Quest { Id = "q6", Name = "Quest-M2N3", Ip = "192.168.1.26", Online = true, Battery = 58, State = DeviceState.Idle, FileId = "" },
        };

        // ---------- classroom: app start, login, people, meetings, live metrics ----------
        public const float SplashSeconds = 1.5f;
        public const string AppTagline = "VR Medical Training";
        public const string RoleTitle = "Who is using this phone?";
        public const string AdminCardTitle = "ADMIN";
        public const string AdminCardText = "Create meetings and run live sessions";
        public const string StudentCardTitle = "STUDENT";
        public const string StudentCardText = "Join your class on a Quest";
        // mock login: any email + password works; the quick buttons fill these
        public const string AdminName = "Dr. Anitha Rao";
        public const string AdminEmail = "anitha.rao@medisim.demo";
        public const string DemoPassword = "demo1234";
        public const int DemoStudentIndex = 0;   // "Demo student" logs in as Students[0]

        public class Student
        {
            public string Id, Name, Email;
            public float Attention, Engagement, Emotion;   // base values (0-100) the live metrics move around
        }

        public static readonly Student[] Students =
        {
            new Student { Id = "s1", Name = "Arjun Kumar", Email = "arjun@medisim.demo", Attention = 82, Engagement = 76, Emotion = 74 },
            new Student { Id = "s2", Name = "Divya Menon", Email = "divya@medisim.demo", Attention = 90, Engagement = 85, Emotion = 80 },
            new Student { Id = "s3", Name = "Karthik Raj", Email = "karthik@medisim.demo", Attention = 64, Engagement = 58, Emotion = 61 },
            new Student { Id = "s4", Name = "Sneha Iyer", Email = "sneha@medisim.demo", Attention = 77, Engagement = 81, Emotion = 70 },
            new Student { Id = "s5", Name = "Rahul Verma", Email = "rahul@medisim.demo", Attention = 55, Engagement = 49, Emotion = 52 },
            new Student { Id = "s6", Name = "Fatima Khan", Email = "fatima@medisim.demo", Attention = 86, Engagement = 79, Emotion = 83 },
            new Student { Id = "s7", Name = "Vikram Singh", Email = "vikram@medisim.demo", Attention = 71, Engagement = 66, Emotion = 68 },
            new Student { Id = "s8", Name = "Meera Pillai", Email = "meera@medisim.demo", Attention = 79, Engagement = 72, Emotion = 77 },
        };

        public class Meeting
        {
            public string Id, Name, Topic, Start, End;    // Start / End "09:00"
            public int DayOffset;                          // 0 = today, 1 = tomorrow
            public string[] StudentIds, QuestIds, AbsentIds;
        }

        // students get the meeting's Quests in order (more students than Quests: they share, round robin)
        public static readonly Meeting[] Meetings =
        {
            new Meeting { Id = "m1", Name = "Meeting 01", Topic = "CPR & Basic Life Support", Start = "09:00", End = "10:00", DayOffset = 0,
                StudentIds = new[] { "s1", "s2", "s3", "s4", "s5", "s6" }, QuestIds = new[] { "q1", "q2", "q4", "q5" }, AbsentIds = new[] { "s5" } },
            new Meeting { Id = "m2", Name = "Meeting 02", Topic = "Heart Anatomy (360 view)", Start = "11:30", End = "12:30", DayOffset = 0,
                StudentIds = new[] { "s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8" }, QuestIds = new[] { "q1", "q2", "q4", "q5", "q6" }, AbsentIds = new[] { "s7" } },
            new Meeting { Id = "m3", Name = "Meeting 03", Topic = "Airway Management", Start = "14:00", End = "15:00", DayOffset = 1,
                StudentIds = new[] { "s3", "s4", "s5", "s6", "s7", "s8" }, QuestIds = new[] { "q2", "q4", "q6" }, AbsentIds = new string[0] },
        };

        // live metrics: every MetricStep seconds each value moves up to MetricJitter towards / around its base
        public const float MetricStep = 0.5f;
        public const float MetricJitter = 6f;
        public const float MetricPull = 0.25f;           // how strongly a value returns to its base
        public const float MetricAnimSeconds = 0.35f;    // bars animate to the new value
        public const float EmotionPositive = 70f, EmotionNeutral = 50f;   // >= Positive, >= Neutral, else Stressed
        public const float LowAttention = 60f;           // below this a student row turns orange
        public const int DefaultVolume = 80;
        public const string ShareText = "Report shared (demo): PDF sent to the admin email";

        // ---------- files ----------
        public class File
        {
            public string Id, Name, Top, Bottom, Text;
            // optional real sample for the phone preview: a path in Assets/StreamingAssets, e.g. "DemoMedia/CPR_Training.mp4"
            // (empty = the demo file has no media; it still "plays" on the demo Quests)
            public string Media;
            public MediaKind Kind;
            public long Size;
            public int Seconds;
        }

        public static readonly File[] Files =
        {
            new File { Id = "f1", Name = "Anatomy_Heart_360.mp4", Kind = MediaKind.Video, Size = 412L << 20, Seconds = 420, Top = "#F8B4B4", Bottom = "#B91C1C" },
            new File { Id = "f2", Name = "CPR_Training.mp4", Kind = MediaKind.Video, Size = 268L << 20, Seconds = 300, Top = "#93C5FD", Bottom = "#1D4ED8" },
            new File { Id = "f3", Name = "Briefing_Audio.mp3", Kind = MediaKind.Audio, Size = 7L << 20, Seconds = 185, Top = "#FDE68A", Bottom = "#B45309" },
            new File { Id = "f5", Name = "Heart_Diagram.png", Kind = MediaKind.Image, Size = 2L << 20, Seconds = -1, Top = "#FBCFE8", Bottom = "#BE185D" },
            new File { Id = "f4", Name = "Safety_Checklist.txt", Kind = MediaKind.Text, Top = "#E3F2FD", Bottom = "#90CAF9",
                       Text = "1. Clear the play area.\n2. Adjust the head strap.\n3. Follow the trainer's instructions." },
        };

        // ---------- the demo scene (Scene tab), same ids as the PC / Quest demo objects ----------
        public static List<SyncObjectInfo> SceneObjects()
        {
            var l = new List<SyncObjectInfo>
            {
                Obj("cube", "Cube", "cube", "#1A80E8", new Vector3(0f, 0.3f, 0f), Vector3.one * 0.6f, Vector3.one, Vector3.zero, false),
                Obj("sphere", "Sphere", "sphere", "#F28C28", new Vector3(-1.1f, 0.35f, 0.5f), Vector3.one * 0.7f, Vector3.one, Vector3.zero, false),
                Obj("cylinder", "Cylinder", "cylinder", "#2EB872", new Vector3(1.1f, 0.4f, 0.5f), new Vector3(0.5f, 0.4f, 0.5f), new Vector3(1f, 2f, 1f), Vector3.zero, false),
                Obj("capsule", "Capsule", "capsule", "#8E5BD8", new Vector3(-0.9f, 0.5f, -0.9f), Vector3.one * 0.5f, new Vector3(1f, 2f, 1f), Vector3.zero, false),
                Obj("mat", "Mat", "plane", "#F2C230", new Vector3(0f, 0.005f, 1.3f), new Vector3(0.08f, 1f, 0.08f), new Vector3(10f, 0f, 10f), Vector3.zero, false),
                Obj("table", "Table", "box", "#8B5A2B", new Vector3(1f, 0f, -0.9f), Vector3.one, new Vector3(1f, 0.75f, 0.6f), new Vector3(0f, 0.375f, 0f), true),
            };
            return l;
        }

        private static SyncObjectInfo Obj(string id, string name, string shape, string hex, Vector3 pos, Vector3 scale, Vector3 size, Vector3 center, bool locked)
        {
            return new SyncObjectInfo
            {
                id = id, name = name, shape = shape, color = SceneSyncProtocol.Arr(Hex(hex)), size = SceneSyncProtocol.Arr(size),
                center = SceneSyncProtocol.Arr(center), pos = SceneSyncProtocol.Arr(pos), rot = SceneSyncProtocol.Arr(Quaternion.identity),
                scale = SceneSyncProtocol.Arr(scale), locked = locked
            };
        }
    }
}
