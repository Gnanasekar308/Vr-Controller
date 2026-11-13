using System;
using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    public enum UserRole { None, Admin, Student }
    public enum LiveState { None, Ready, Running, Paused, Ended }

    public class Student
    {
        public string Id = "", Name = "", Email = "";
        public float BaseAttention, BaseEngagement, BaseEmotion;
    }

    public class Meeting
    {
        public string Id = "", Name = "", Topic = "", Start = "09:00", End = "10:00";
        public DateTime Date = DateTime.Today;
        public readonly List<string> StudentIds = new List<string>();
        public readonly List<string> QuestIds = new List<string>();
        public readonly HashSet<string> Absent = new HashSet<string>();   // attendance (mock)

        public string Timing { get { return Start + " - " + End; } }
        public bool Present(string studentId) { return StudentIds.Contains(studentId) && !Absent.Contains(studentId); }
        public int PresentCount { get { int n = 0; foreach (var s in StudentIds) if (!Absent.Contains(s)) n++; return n; } }
    }

    // live values of one student (0-100); Shown* follow the values smoothly for the animated bars
    public class StudentMetrics
    {
        public float Attention, Engagement, Emotion;
        public float ShownAttention, ShownEngagement, ShownEmotion;
        public float SumAttention, SumEngagement, SumEmotion;
        public int Samples;
        public bool Muted;
    }

    public class ReportEvent
    {
        public TimeSpan At;          // session time
        public SessionEventKind Kind;
        public string Text = "";
    }

    public class StudentResult
    {
        public string Name = "", Quest = "";
        public bool Present;
        public int Attention, Engagement, Emotion;   // session averages
    }

    public class SessionReport
    {
        public string Meeting = "", Topic = "", Date = "", Timing = "";
        public TimeSpan Duration;
        public int Present, Absent;
        public readonly List<string> AbsentNames = new List<string>();
        public int Attention, Engagement, Emotion;   // class averages
        public readonly List<StudentResult> Students = new List<StudentResult>();
        public readonly List<string> Content = new List<string>();
        public readonly List<ReportEvent> Timeline = new List<ReportEvent>();
        public string SavedPath = "", PcStatus = "";
    }

    // The classroom side of the app: who is logged in (admin / student), the meetings, and the live session with its
    // metrics and report. Today all mock data from DemoConfig (UI only, no backend); the screens only use this class,
    // so a real backend can replace the data later without touching the UI.
    public static class Classroom
    {
        public static event Action Changed;

        // ---------- login ----------
        public static UserRole Role { get; private set; }
        public static string UserName { get; private set; } = "";
        public static string StudentId { get; private set; } = "";   // the logged-in student
        public static bool IsAdmin { get { return Role == UserRole.Admin; } }
        public static bool IsStudent { get { return Role == UserRole.Student; } }

        public static readonly List<Student> Students = new List<Student>();
        public static readonly List<Meeting> Meetings = new List<Meeting>();
        public static Meeting Selected;          // the meeting the details screen shows
        public static int SessionsToday { get; private set; }

        // ---------- live session ----------
        public static Meeting Live { get; private set; }
        public static LiveState State { get; private set; }
        public static float Elapsed { get; private set; }            // seconds while running
        public static string FocusStudentId = "";                     // whose Quest screen the center shows
        public static int Volume = DemoConfig.DefaultVolume;
        public static bool MuteAll;
        public static SessionReport LastReport { get; private set; }
        public static bool LiveActive { get { return Live != null && (State == LiveState.Ready || State == LiveState.Running || State == LiveState.Paused); } }

        static readonly Dictionary<string, StudentMetrics> metrics = new Dictionary<string, StudentMetrics>();
        static readonly List<ReportEvent> events = new List<ReportEvent>();
        static readonly List<string> content = new List<string>();
        static float stepTimer;
        static System.Random rnd = new System.Random(7);

        // app start / demo switch (statics survive a scene reload)
        public static void Reset()
        {
            Role = UserRole.None;
            UserName = "";
            StudentId = "";
            Students.Clear();
            foreach (var s in DemoConfig.Students)
                Students.Add(new Student { Id = s.Id, Name = s.Name, Email = s.Email, BaseAttention = s.Attention, BaseEngagement = s.Engagement, BaseEmotion = s.Emotion });
            Meetings.Clear();
            foreach (var m in DemoConfig.Meetings)
            {
                var x = new Meeting { Id = m.Id, Name = m.Name, Topic = m.Topic, Start = m.Start, End = m.End, Date = DateTime.Today.AddDays(m.DayOffset) };
                x.StudentIds.AddRange(m.StudentIds);
                x.QuestIds.AddRange(m.QuestIds);
                foreach (var a in m.AbsentIds) x.Absent.Add(a);
                Meetings.Add(x);
            }
            Selected = null;
            SessionsToday = 0;
            Live = null;
            State = LiveState.None;
            Elapsed = 0f;
            LastReport = null;
            ClearLive();
            Fire();
        }

        static void ClearLive()
        {
            metrics.Clear();
            events.Clear();
            content.Clear();
            FocusStudentId = "";
            Volume = DemoConfig.DefaultVolume;
            MuteAll = false;
            stepTimer = 0f;
        }

        static void Fire() { if (Changed != null) Changed(); }

        // mock login: any non-empty email / password works. Student: the email picks the student (unknown = first one)
        public static void Login(UserRole role, string email)
        {
            Role = role;
            if (role == UserRole.Admin) { UserName = DemoConfig.AdminName; StudentId = ""; }
            else
            {
                Student who = Students.Count > 0 ? Students[Mathf.Clamp(DemoConfig.DemoStudentIndex, 0, Students.Count - 1)] : null;
                foreach (var s in Students) if (string.Equals(s.Email, (email ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) who = s;
                UserName = who != null ? who.Name : "Student";
                StudentId = who != null ? who.Id : "";
            }
            Debug.Log("[MediSimVR] login as " + role);
            Fire();
        }

        public static void Logout()
        {
            if (LiveActive) { Log(SessionEventKind.Leave, UserName + " logged out"); State = LiveState.Ended; Live = null; ClearLive(); }
            Role = UserRole.None;
            UserName = "";
            StudentId = "";
            Selected = null;
            Debug.Log("[MediSimVR] logged out");
            Fire();
        }

        // ---------- lookups ----------
        public static Student StudentById(string id)
        {
            foreach (var s in Students) if (s.Id == id) return s;
            return null;
        }

        public static string FirstName(string id) { var s = StudentById(id); return s == null ? "?" : s.Name.Split(' ')[0]; }

        // the meetings a list shows: admin = all, student = the ones the student is in
        public static List<Meeting> Visible()
        {
            var l = new List<Meeting>();
            foreach (var m in Meetings) if (IsAdmin || m.StudentIds.Contains(StudentId)) l.Add(m);
            return l;
        }

        public static List<Meeting> Today()
        {
            var l = new List<Meeting>();
            foreach (var m in Visible()) if (m.Date.Date == DateTime.Today) l.Add(m);
            return l;
        }

        // the Quest of a student in a meeting: the meeting's Quests in order, shared round robin
        public static string QuestIdFor(Meeting m, string studentId)
        {
            if (m == null || m.QuestIds.Count == 0) return "";
            int i = m.StudentIds.IndexOf(studentId);
            return i < 0 ? "" : m.QuestIds[i % m.QuestIds.Count];
        }

        public static QuestDevice Device(IControllerService svc, string questId)
        {
            if (svc == null) return null;
            foreach (var d in svc.Devices) if (d.Id == questId) return d;
            return null;
        }

        public static string QuestName(IControllerService svc, string questId)
        {
            var d = Device(svc, questId);
            return d != null ? d.Name : questId.Length > 0 ? questId : "No Quest";
        }

        public static string DateText(DateTime d)
        {
            if (d.Date == DateTime.Today) return "Today, " + d.ToString("dd MMM yyyy");
            if (d.Date == DateTime.Today.AddDays(1)) return "Tomorrow, " + d.ToString("dd MMM yyyy");
            return d.ToString("ddd, dd MMM yyyy");
        }

        // short form for list rows: Today / Tomorrow / Fri 20 Nov
        public static string ShortDate(DateTime d)
        {
            if (d.Date == DateTime.Today) return "Today";
            if (d.Date == DateTime.Today.AddDays(1)) return "Tomorrow";
            return d.ToString("ddd dd MMM");
        }

        public static string Mood(float emotion)
        {
            return emotion >= DemoConfig.EmotionPositive ? "Positive" : emotion >= DemoConfig.EmotionNeutral ? "Neutral" : "Stressed";
        }

        // ---------- meetings ----------
        public static void Add(Meeting m)
        {
            if (string.IsNullOrEmpty(m.Id)) m.Id = "m" + (Meetings.Count + 1) + "_" + DateTime.Now.Ticks % 100000;
            Meetings.Add(m);
            Debug.Log("[MediSimVR] meeting saved: " + m.Name + " (" + m.StudentIds.Count + " students, " + m.QuestIds.Count + " Quests)");
            Fire();
        }

        // ---------- live session ----------
        // Start Session (admin) / Join Session (student) on the details screen: the live screen opens, Ready
        public static void Open(Meeting m)
        {
            if (m == null) return;
            if (Live == m && LiveActive) return;
            Live = m;
            ClearLive();
            foreach (var id in m.StudentIds)
            {
                var s = StudentById(id);
                if (s == null) continue;
                metrics[id] = new StudentMetrics
                {
                    Attention = s.BaseAttention, Engagement = s.BaseEngagement, Emotion = s.BaseEmotion,
                    ShownAttention = s.BaseAttention, ShownEngagement = s.BaseEngagement, ShownEmotion = s.BaseEmotion
                };
            }
            FocusStudentId = IsStudent ? StudentId : FirstPresent(m);
            Elapsed = 0f;
            State = IsStudent ? LiveState.Running : LiveState.Ready;   // a student joins a running class
            if (IsStudent) Log(SessionEventKind.Join, UserName + " joined");
            else Log(SessionEventKind.Info, "Opened: " + m.PresentCount + " of " + m.StudentIds.Count + " present");
            Fire();
        }

        static string FirstPresent(Meeting m)
        {
            foreach (var id in m.StudentIds) if (!m.Absent.Contains(id)) return id;
            return m.StudentIds.Count > 0 ? m.StudentIds[0] : "";
        }

        public static void Start()
        {
            if (Live == null || State == LiveState.Running) return;
            bool first = State == LiveState.Ready;
            State = LiveState.Running;
            if (first) SessionsToday++;
            Log(SessionEventKind.Play, first ? "Session started" : "Session resumed");
            Fire();
        }

        public static void Pause()
        {
            if (State != LiveState.Running) return;
            State = LiveState.Paused;
            Log(SessionEventKind.Pause, "Session paused");
            Fire();
        }

        // END: builds the report (averages over the session) and closes the live session
        public static SessionReport End(string savedPath, string pcStatus)
        {
            if (Live == null) return null;
            Log(SessionEventKind.Stop, "Session ended");
            var m = Live;
            var r = new SessionReport
            {
                Meeting = m.Name, Topic = m.Topic, Date = DateText(m.Date), Timing = m.Timing,
                Duration = TimeSpan.FromSeconds(Mathf.Round(Elapsed)), SavedPath = savedPath ?? "", PcStatus = pcStatus ?? ""
            };
            float sa = 0, se = 0, sm = 0;
            int n = 0;
            foreach (var id in m.StudentIds)
            {
                var s = StudentById(id);
                if (s == null) continue;
                bool present = !m.Absent.Contains(id);
                var res = new StudentResult { Name = s.Name, Present = present, Quest = QuestName(AppRoot.Service, QuestIdFor(m, id)) };
                StudentMetrics x;
                if (present && metrics.TryGetValue(id, out x))
                {
                    res.Attention = Mathf.RoundToInt(x.Samples > 0 ? x.SumAttention / x.Samples : x.Attention);
                    res.Engagement = Mathf.RoundToInt(x.Samples > 0 ? x.SumEngagement / x.Samples : x.Engagement);
                    res.Emotion = Mathf.RoundToInt(x.Samples > 0 ? x.SumEmotion / x.Samples : x.Emotion);
                    sa += res.Attention; se += res.Engagement; sm += res.Emotion; n++;
                }
                if (present) r.Present++; else { r.Absent++; r.AbsentNames.Add(s.Name); }
                r.Students.Add(res);
            }
            if (n > 0) { r.Attention = Mathf.RoundToInt(sa / n); r.Engagement = Mathf.RoundToInt(se / n); r.Emotion = Mathf.RoundToInt(sm / n); }
            r.Content.AddRange(content);
            r.Timeline.AddRange(events);
            LastReport = r;
            State = LiveState.Ended;
            Live = null;
            ClearLive();
            Debug.Log("[MediSimVR] session report: " + r.Meeting + ", " + r.Duration + ", present " + r.Present + ", absent " + r.Absent +
                      ", engagement " + r.Engagement + "%, attention " + r.Attention + "%");
            Fire();
            return r;
        }

        public static void SetPcStatus(string status)
        {
            if (LastReport == null || LastReport.PcStatus == status) return;
            LastReport.PcStatus = status ?? "";
            Fire();
        }

        public static void Log(SessionEventKind kind, string text)
        {
            events.Add(new ReportEvent { At = TimeSpan.FromSeconds(Mathf.Floor(Elapsed)), Kind = kind, Text = text });
        }

        public static void Played(string what)
        {
            if (!content.Contains(what)) content.Add(what);
            Log(SessionEventKind.Content, "Played " + what);
            Fire();
        }

        public static IReadOnlyList<ReportEvent> Events { get { return events; } }

        public static StudentMetrics MetricsOf(string id)
        {
            StudentMetrics x;
            return metrics.TryGetValue(id, out x) ? x : null;
        }

        // class averages of the present students (shown values = animated)
        public static void Average(out float attention, out float engagement, out float emotion)
        {
            attention = engagement = emotion = 0f;
            if (Live == null) return;
            int n = 0;
            foreach (var id in Live.StudentIds)
            {
                if (Live.Absent.Contains(id)) continue;
                var x = MetricsOf(id);
                if (x == null) continue;
                attention += x.ShownAttention; engagement += x.ShownEngagement; emotion += x.ShownEmotion; n++;
            }
            if (n == 0) return;
            attention /= n; engagement /= n; emotion /= n;
        }

        public static void ToggleMute(string id)
        {
            var x = MetricsOf(id);
            if (x == null) return;
            x.Muted = !x.Muted;
            Log(SessionEventKind.Mute, (x.Muted ? "Muted " : "Unmuted ") + FirstName(id));
            Fire();
        }

        public static void SetMuteAll(bool on)
        {
            if (MuteAll == on) return;
            MuteAll = on;
            Log(SessionEventKind.Mute, on ? "Muted all students" : "Unmuted all students");
            Fire();
        }

        public static void SetVolume(int v)
        {
            Volume = Mathf.Clamp(v, 0, 100);
        }

        public static void Focus(string studentId)
        {
            if (FocusStudentId == studentId) return;
            FocusStudentId = studentId;
            Fire();
        }

        // demo metrics: a random walk around each student's base while running; the bars animate in between
        public static void Tick(float dt)
        {
            if (Live == null) return;
            float k = Mathf.Clamp01(dt / Mathf.Max(0.01f, DemoConfig.MetricAnimSeconds));
            foreach (var x in metrics.Values)
            {
                x.ShownAttention = Mathf.Lerp(x.ShownAttention, x.Attention, k);
                x.ShownEngagement = Mathf.Lerp(x.ShownEngagement, x.Engagement, k);
                x.ShownEmotion = Mathf.Lerp(x.ShownEmotion, x.Emotion, k);
            }
            if (State != LiveState.Running) return;
            Elapsed += dt;
            stepTimer += dt;
            if (stepTimer < DemoConfig.MetricStep) return;
            stepTimer = 0f;
            foreach (var kv in metrics)
            {
                if (Live.Absent.Contains(kv.Key)) continue;
                var s = StudentById(kv.Key);
                var x = kv.Value;
                x.Attention = Walk(x.Attention, s.BaseAttention);
                x.Engagement = Walk(x.Engagement, s.BaseEngagement);
                x.Emotion = Walk(x.Emotion, s.BaseEmotion);
                x.SumAttention += x.Attention; x.SumEngagement += x.Engagement; x.SumEmotion += x.Emotion;
                x.Samples++;
            }
        }

        static float Walk(float v, float baseValue)
        {
            float j = (float)(rnd.NextDouble() * 2.0 - 1.0) * DemoConfig.MetricJitter;
            return Mathf.Clamp(v + j + (baseValue - v) * DemoConfig.MetricPull, 5f, 99f);
        }
    }
}
