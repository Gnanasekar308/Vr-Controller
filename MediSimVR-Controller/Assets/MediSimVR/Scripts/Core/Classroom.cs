using System;
using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    public enum UserRole { None, Admin, Student }
    public enum LiveState { None, Ready, Running, Paused, Ended }

    public class Student
    {
        public string Id = "", Code = "", Name = "", Email = "";
        public float BaseAttention, BaseEngagement, BaseEmotion, BaseParticipation;
        public string Initials
        {
            get
            {
                var p = Name.Split(' ');
                return (p[0].Substring(0, 1) + (p.Length > 1 ? p[p.Length - 1].Substring(0, 1) : "")).ToUpperInvariant();
            }
        }
    }

    // a student's question or comment in the live session
    public class LiveLine
    {
        public string StudentId = "", Text = "";
        public float At;          // session seconds
        public DateTime Time;
        public bool Answered;
    }

    public class Meeting
    {
        public string Id = "", Name = "", Subject = "", Topic = "", Start = "09:00", End = "10:00";
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
        public float Attention, Engagement, Emotion, Participation;
        public float ShownAttention, ShownEngagement, ShownEmotion, ShownParticipation;
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
        static float stepTimer, questionTimer, commentTimer;
        static int nextQuestion, nextComment;
        static readonly List<LiveLine> questions = new List<LiveLine>();
        static readonly List<LiveLine> comments = new List<LiveLine>();

        public static IReadOnlyList<LiveLine> Questions { get { return questions; } }   // oldest first
        public static IReadOnlyList<LiveLine> Comments { get { return comments; } }
        public static bool Recording { get; private set; }
        public static float RecordSeconds { get; private set; }

        public static int NewQuestions
        {
            get { int n = 0; foreach (var q in questions) if (!q.Answered && Elapsed - q.At < DemoConfig.NewSeconds) n++; return n; }
        }

        // next demo line from the pool, from a student of the meeting who is present
        static void AddLine(List<LiveLine> list, DemoConfig.Line[] pool, ref int next, Meeting m)
        {
            if (pool.Length == 0 || m == null) return;
            for (int tries = 0; tries < pool.Length; tries++)
            {
                var l = pool[next % pool.Length];
                next++;
                if (l.StudentIndex < 0 || l.StudentIndex >= Students.Count) continue;
                string id = Students[l.StudentIndex].Id;
                if (!m.Present(id)) continue;
                list.Add(new LiveLine { StudentId = id, Text = l.Text, At = Elapsed, Time = DateTime.Now });
                return;
            }
        }

        public static void Answer(LiveLine q)
        {
            if (q == null || q.Answered) return;
            q.Answered = true;
            Log(SessionEventKind.Text, "Answered a question of " + FirstName(q.StudentId));
            Fire();
        }

        public static void SetRecording(bool on)
        {
            if (Live == null || Recording == on) return;
            Recording = on;
            Log(SessionEventKind.Info, on ? "Recording started" : "Recording stopped (" + Mathf.FloorToInt(RecordSeconds) + " s)");
            Fire();
        }
        static System.Random rnd = new System.Random(7);

        // app start / demo switch (statics survive a scene reload)
        public static void Reset()
        {
            Role = UserRole.None;
            UserName = "";
            StudentId = "";
            Students.Clear();
            foreach (var s in DemoConfig.Students)
                Students.Add(new Student { Id = s.Id, Code = s.Code ?? "", Name = s.Name, Email = s.Email, BaseAttention = s.Attention, BaseEngagement = s.Engagement, BaseEmotion = s.Emotion,
                    BaseParticipation = Mathf.Clamp(s.Engagement + DemoConfig.ParticipationOffset, 5f, 99f) });
            Meetings.Clear();
            foreach (var m in DemoConfig.Meetings)
            {
                var x = new Meeting { Id = m.Id, Name = m.Name, Subject = m.Subject ?? "", Topic = m.Topic, Start = m.Start, End = m.End, Date = DateTime.Today.AddDays(m.DayOffset) };
                x.QuestIds.AddRange(m.QuestIds);
                foreach (var q in m.QuestIds) { var sid = StudentOfQuest(q); if (sid.Length > 0) x.StudentIds.Add(sid); }
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
            questions.Clear();
            comments.Clear();
            nextQuestion = nextComment = 0;
            questionTimer = commentTimer = 0f;
            Recording = false;
            RecordSeconds = 0f;
            Volume = DemoConfig.DefaultVolume;
            MuteAll = false;
            ShowEngagement = ShowAttention = ShowParticipation = true;
            levelShift[0] = levelShift[1] = levelShift[2] = 0f;
            AlertLevel = DemoConfig.LowAttention;
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

        // every Quest has its student (name + ID entered on the headset; demo: DemoConfig.Quests)
        public static string StudentOfQuest(string questId)
        {
            foreach (var q in DemoConfig.Quests) if (q.Id == questId) return q.StudentId ?? "";
            return "";
        }

        public static string QuestOfStudent(string studentId)
        {
            foreach (var q in DemoConfig.Quests) if (q.StudentId == studentId) return q.Id;
            return "";
        }

        // the Quest of a student in a meeting: the student's own Quest when it is in the meeting
        public static string QuestIdFor(Meeting m, string studentId)
        {
            if (m == null) return "";
            string q = QuestOfStudent(studentId);
            return m.QuestIds.Contains(q) ? q : "";
        }

        // "Arjun Kumar (S001)"
        public static string StudentLabel(string studentId)
        {
            var s = StudentById(studentId);
            return s == null ? "No student" : s.Name + (s.Code.Length > 0 ? " (" + s.Code + ")" : "");
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

        public static bool Delete(Meeting m)
        {
            if (m == null || (Live == m && LiveActive)) return false;
            Meetings.Remove(m);
            if (Selected == m) Selected = null;
            Debug.Log("[MediSimVR] meeting deleted: " + m.Name);
            Fire();
            return true;
        }

        // ---------- live metrics settings (admin) ----------
        public static bool ShowEngagement = true, ShowAttention = true, ShowParticipation = true;
        public static float AlertLevel = DemoConfig.LowAttention;

        // present students whose attention is under the alert level
        public static int NeedAttention()
        {
            if (Live == null) return 0;
            int n = 0;
            foreach (var id in Live.StudentIds)
            {
                if (Live.Absent.Contains(id)) continue;
                var x = MetricsOf(id);
                if (x != null && x.ShownAttention < AlertLevel) n++;
            }
            return n;
        }

        public static void MetricsChanged() { Fire(); }

        // admin slider: move the class level of a metric (0 engagement, 1 attention, 2 participation) to v. Demo: every
        // present student's values move by the same amount and keep moving around the new level.
        static readonly float[] levelShift = new float[3];

        public static void SetLevel(int kind, float v)
        {
            if (Live == null || kind < 0 || kind > 2) return;
            float a, e, m;
            Average(out a, out e, out m);
            float now = kind == 0 ? e : kind == 1 ? a : AverageParticipation();
            float d = v - now;
            levelShift[kind] = Mathf.Clamp(levelShift[kind] + d, -90f, 90f);
            foreach (var x in metrics.Values)
            {
                if (kind == 0) { x.Engagement = Mathf.Clamp(x.Engagement + d, 5f, 99f); x.ShownEngagement = Mathf.Clamp(x.ShownEngagement + d, 5f, 99f); }
                else if (kind == 1) { x.Attention = Mathf.Clamp(x.Attention + d, 5f, 99f); x.ShownAttention = Mathf.Clamp(x.ShownAttention + d, 5f, 99f); }
                else { x.Participation = Mathf.Clamp(x.Participation + d, 5f, 99f); x.ShownParticipation = Mathf.Clamp(x.ShownParticipation + d, 5f, 99f); }
            }
        }

        public static void LogLevel(int kind, float v)
        {
            Log(SessionEventKind.Info, (kind == 0 ? "Engagement" : kind == 1 ? "Attention" : "Participation") + " level set to " + Mathf.RoundToInt(v) + "%");
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
                    Attention = s.BaseAttention, Engagement = s.BaseEngagement, Emotion = s.BaseEmotion, Participation = s.BaseParticipation,
                    ShownAttention = s.BaseAttention, ShownEngagement = s.BaseEngagement, ShownEmotion = s.BaseEmotion, ShownParticipation = s.BaseParticipation
                };
            }
            FocusStudentId = IsStudent ? StudentId : FirstPresent(m);
            for (int i = 0; i < DemoConfig.StartQuestions; i++) AddLine(questions, DemoConfig.Questions, ref nextQuestion, m);
            for (int i = 0; i < DemoConfig.StartComments; i++) AddLine(comments, DemoConfig.Comments, ref nextComment, m);
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

        public static float AverageParticipation()
        {
            if (Live == null) return 0f;
            float sum = 0f;
            int n = 0;
            foreach (var id in Live.StudentIds)
            {
                if (Live.Absent.Contains(id)) continue;
                var x = MetricsOf(id);
                if (x == null) continue;
                sum += x.ShownParticipation;
                n++;
            }
            return n == 0 ? 0f : sum / n;
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
                x.ShownParticipation = Mathf.Lerp(x.ShownParticipation, x.Participation, k);
            }
            if (State != LiveState.Running) return;
            Elapsed += dt;
            if (Recording) RecordSeconds += dt;
            if ((questionTimer += dt) >= DemoConfig.QuestionEvery) { questionTimer = 0f; AddLine(questions, DemoConfig.Questions, ref nextQuestion, Live); Fire(); }
            if ((commentTimer += dt) >= DemoConfig.CommentEvery) { commentTimer = 0f; AddLine(comments, DemoConfig.Comments, ref nextComment, Live); Fire(); }
            stepTimer += dt;
            if (stepTimer < DemoConfig.MetricStep) return;
            stepTimer = 0f;
            foreach (var kv in metrics)
            {
                if (Live.Absent.Contains(kv.Key)) continue;
                var s = StudentById(kv.Key);
                var x = kv.Value;
                x.Attention = Walk(x.Attention, s.BaseAttention + levelShift[1]);
                x.Engagement = Walk(x.Engagement, s.BaseEngagement + levelShift[0]);
                x.Emotion = Walk(x.Emotion, s.BaseEmotion);
                x.Participation = Walk(x.Participation, s.BaseParticipation + levelShift[2]);
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
