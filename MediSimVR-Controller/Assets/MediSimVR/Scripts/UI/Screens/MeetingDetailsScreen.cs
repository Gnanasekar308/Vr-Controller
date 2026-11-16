using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Meeting details: students count, today's topic, date, class timing, assigned Quest devices, attendance
    // (present / absent; the admin can tap a student to change it). Admin: [Start Session], student: [Join Session]
    // -> Live session.
    public class MeetingDetailsScreen : AppScreen
    {
        public Button backButton, actionButton, deleteButton;
        public TMP_Text titleText, subtitleText, actionLabel, studentsValue, topicValue, dateValue, timingValue, questsTitle, attendanceTitle, attendanceHint;
        public CheckRowView questTemplate, attendanceTemplate;

        private readonly List<CheckRowView> questRows = new List<CheckRowView>();
        private readonly List<CheckRowView> attendanceRows = new List<CheckRowView>();

        private Meeting M { get { return Classroom.Selected; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
            actionButton.onClick.AddListener(Act);
            deleteButton.onClick.AddListener(Delete);
            questTemplate.gameObject.SetActive(false);
            attendanceTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnable() { base.OnEnable(); Classroom.Changed += OnClass; }
        protected override void OnDisable() { base.OnDisable(); Classroom.Changed -= OnClass; }
        void OnClass() { if (isActiveAndEnabled) Refresh(); }

        public override bool TryGetParent(out ScreenId parent) { parent = ScreenId.Meetings; return M == null; }

        void Act()
        {
            var m = M;
            if (m == null) return;
            if (Classroom.LiveActive && Classroom.Live == m) { nav.Tab(ScreenId.Live); return; }
            if (Classroom.IsStudent)
            {
                if (m.Date.Date > DateTime.Today) { nav.Say(m.Name + " starts " + Classroom.DateText(m.Date) + " at " + m.Start); return; }
                if (Classroom.LiveActive) { nav.Say("You are already in " + Classroom.Live.Name); return; }
                Open(m);
                return;
            }
            if (Classroom.LiveActive)
            {
                nav.popup.Show("Another session is live", Classroom.Live.Name + " is running. End it before you start " + m.Name + ".",
                    "Open live session", () => nav.Tab(ScreenId.Live), "Cancel");
                return;
            }
            if (m.Date.Date > DateTime.Today)
            {
                nav.popup.Show("Start now?", m.Name + " is planned for " + Classroom.DateText(m.Date) + ", " + m.Timing + ".",
                    "Start now", () => Open(m), "Cancel");
                return;
            }
            Open(m);
        }

        void Open(Meeting m)
        {
            Classroom.Open(m);
            if (Classroom.IsStudent)
            {
                // a student joins a running class: the session record (JSON, player) starts now, on the student's Quest
                string q = Classroom.QuestIdFor(m, Classroom.StudentId);
                foreach (var d in Svc.Devices) d.Selected = d.Id == q;
                var ses = AppRoot.Session;
                if (ses != null && !ses.Active && q.Length > 0) ses.StartPlayer(q);
                SessionLog.Add(SessionEventKind.Join, Classroom.UserName, "joined " + m.Name);
            }
            // the live controls act on this meeting's online Quests
            else foreach (var d in Svc.Devices) d.Selected = d.Online && m.QuestIds.Contains(d.Id);
            nav.Tab(ScreenId.Live);
        }

        // admin: delete the meeting (confirm); not while it is live
        void Delete()
        {
            var m = M;
            if (m == null || !Classroom.IsAdmin) return;
            if (Classroom.LiveActive && Classroom.Live == m) { nav.Say("End the live session first"); return; }
            nav.popup.Show("Delete " + m.Name + "?", "The meeting and its attendance are removed. This cannot be undone.", "Delete", () =>
            {
                string name = m.Name;
                if (!Classroom.Delete(m)) return;
                nav.Back();
                nav.Say(name + " deleted");
            }, "Cancel", destructive: true);
        }

        public void ConfirmDelete() { Delete(); }

        void ToggleAttendance(string id)
        {
            var m = M;
            if (m == null || !Classroom.IsAdmin) return;
            if (Classroom.Live == m && Classroom.LiveActive) { nav.Say("Attendance is locked while the session is live"); return; }
            if (!m.Absent.Remove(id)) m.Absent.Add(id);
            Refresh();
        }

        public override void Refresh()
        {
            var m = M;
            if (m == null) { titleText.text = "Meeting"; subtitleText.text = "No meeting selected"; return; }
            bool admin = Classroom.IsAdmin;
            deleteButton.gameObject.SetActive(admin);
            titleText.text = m.Name;
            subtitleText.text = Classroom.DateText(m.Date) + "  •  " + m.Timing;
            studentsValue.text = m.StudentIds.Count.ToString();
            topicValue.text = m.Topic;
            dateValue.text = Classroom.DateText(m.Date);
            timingValue.text = m.Timing;

            Keep(questTemplate, questRows, m.QuestIds.Count);
            for (int i = 0; i < m.QuestIds.Count; i++)
            {
                var d = Classroom.Device(Svc, m.QuestIds[i]);
                var row = questRows[i];
                if (d == null) { row.Bind(m.QuestIds[i], m.QuestIds[i], "Unknown device", null, "Offline", ColorRole.SubText, true); continue; }
                var st = DeviceStatus.Of(d);
                row.Bind(d.Id, d.Name + "  •  " + Classroom.FirstName(Classroom.StudentOfQuest(d.Id)),
                    DeviceStatus.Join(Classroom.StudentLabel(Classroom.StudentOfQuest(d.Id)), d.Online ? DeviceStatus.Battery(d) : "Offline"),
                    null, DeviceStatus.Label(st), DeviceStatus.Role(st), !d.Online);
            }
            questsTitle.text = "Quest devices and students (" + m.QuestIds.Count + ")";

            Keep(attendanceTemplate, attendanceRows, m.StudentIds.Count);
            for (int i = 0; i < m.StudentIds.Count; i++)
            {
                string id = m.StudentIds[i];
                var s = Classroom.StudentById(id);
                bool present = !m.Absent.Contains(id);
                var row = attendanceRows[i];
                row.Bind(id, s != null ? s.Name : id, Classroom.QuestName(Svc, Classroom.QuestIdFor(m, id)), null,
                    present ? "Present" : "Absent", present ? ColorRole.Success : ColorRole.Danger, !present);
                row.button.onClick.RemoveAllListeners();
                row.button.onClick.AddListener(() => ToggleAttendance(id));
                row.button.enabled = admin;   // only the admin changes attendance (no disabled tint for a student)
            }
            attendanceTitle.text = "Attendance  •  " + m.PresentCount + " present, " + (m.StudentIds.Count - m.PresentCount) + " absent";
            attendanceHint.text = admin ? "Tap a student to mark present / absent" : "";
            attendanceHint.gameObject.SetActive(admin);

            bool liveHere = Classroom.LiveActive && Classroom.Live == m;
            actionLabel.text = liveHere ? (admin ? "Open Live Session" : "Back to Live Session") : admin ? "Start Session" : "Join Session";
        }
    }
}
