using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // Session report after END: meeting info + duration, attendance, class averages (engagement, attention, emotion)
    // and per student, content played, timeline of key events, where the JSON record went. Share (mock), Back to
    // Dashboard.
    public class SessionReportScreen : AppScreen
    {
        public TMP_Text subtitleText, infoText, presentValue, absentValue, absentNames, contentText, savedText, emptyText;
        public MeterView classEngagement, classAttention, classEmotion;
        public StudentRowView studentTemplate;
        public TimelineRowView timelineTemplate;
        public Button shareButton, dashboardButton;
        public UnityEngine.GameObject engagementSection;   // simulated averages: demo mode only

        private readonly List<StudentRowView> studentRows = new List<StudentRowView>();
        private readonly List<TimelineRowView> timelineRows = new List<TimelineRowView>();
        private readonly StringBuilder sb = new StringBuilder();

        public override void Init(Navigator n)
        {
            base.Init(n);
            shareButton.onClick.AddListener(() =>
            {
                var r = Classroom.LastReport;
                if (r == null) return;
                Classroom.Log(SessionEventKind.Info, "report shared");
                UnityEngine.Debug.Log("[MediSimVR] report shared (demo): " + r.Meeting);
                nav.Say(DemoConfig.ShareText);
            });
            dashboardButton.onClick.AddListener(() => nav.Tab(ScreenId.Dashboard));
            studentTemplate.gameObject.SetActive(false);
            timelineTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnable() { base.OnEnable(); Classroom.Changed += OnClass; SessionLog.Changed += OnRecord; }
        protected override void OnDisable() { base.OnDisable(); Classroom.Changed -= OnClass; SessionLog.Changed -= OnRecord; }
        void OnClass() { if (isActiveAndEnabled) Refresh(); }
        // the PC upload answers a moment after END
        void OnRecord() { Classroom.SetPcStatus(SessionLog.UploadStatus); }

        public override void Refresh()
        {
            var r = Classroom.LastReport;
            emptyText.gameObject.SetActive(r == null);
            if (engagementSection != null) engagementSection.SetActive(Classroom.Simulated);
            if (r == null) { subtitleText.text = "No session ended yet"; return; }
            subtitleText.text = r.Meeting + "  •  " + r.Date;
            infoText.text = "Meeting<pos=34%>" + r.Meeting + "\nTopic<pos=34%>" + r.Topic + "\nDate<pos=34%>" + r.Date +
                            "\nClass timing<pos=34%>" + r.Timing + "\nDuration<pos=34%>" + LiveSessionScreen.Clock((float)r.Duration.TotalSeconds);
            presentValue.text = r.Present.ToString();
            absentValue.text = r.Absent.ToString();
            absentNames.text = r.Absent == 0 ? "Everyone was present" : "Absent: " + string.Join(", ", r.AbsentNames.ToArray());
            classEngagement.Set(r.Engagement);
            classAttention.Set(r.Attention);
            classEmotion.Set(r.Emotion, r.Emotion + "%  " + Classroom.Mood(r.Emotion));

            Keep(studentTemplate, studentRows, r.Students.Count);
            for (int i = 0; i < r.Students.Count; i++)
            {
                var s = r.Students[i];
                var row = studentRows[i];
                row.Bind("", s.Name, s.Present ? s.Quest : "Absent", s.Present, false, false, null, null);
                row.button.enabled = false;   // a report row is not tappable (no disabled tint)
                if (s.Present)
                {
                    row.attention.Set(s.Attention);
                    row.engagement.Set(s.Engagement);
                    row.emotion.Set(s.Emotion);
                }
            }

            sb.Length = 0;
            foreach (var c in r.Content) { if (sb.Length > 0) sb.Append('\n'); sb.Append("•  ").Append(c); }
            contentText.text = sb.Length > 0 ? sb.ToString() : "Nothing was played";

            int count = Math.Min(r.Timeline.Count, 30);
            Keep(timelineTemplate, timelineRows, count);
            for (int i = 0; i < count; i++)
            {
                var e = r.Timeline[r.Timeline.Count - 1 - i];   // newest first
                timelineRows[i].Bind(new SessionEvent { Time = DateTime.Today + e.At, Kind = e.Kind, Device = "", Message = e.Text });
            }

            string file = string.IsNullOrEmpty(r.SavedPath) ? "" : Path.GetFileName(r.SavedPath);
            savedText.text = (file.Length > 0 ? "Saved on this phone: " + SessionLog.Folder + "/" + file : "Not saved") +
                             (string.IsNullOrEmpty(r.PcStatus) ? "" : "\n" + r.PcStatus);
        }
    }
}
