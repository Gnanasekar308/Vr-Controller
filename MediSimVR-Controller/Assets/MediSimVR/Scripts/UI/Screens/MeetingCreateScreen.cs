using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Create Meeting form: name, today's topic, date (day picker), class timing (start - end, 15 min steps),
    // students (multi-select), Quest devices (multi-select; Online / In Use selectable, Offline not). Save -> list.
    public class MeetingCreateScreen : AppScreen
    {
        public const int StepMinutes = 15;

        public Button backButton, saveButton, prevDay, nextDay, startMinus, startPlus, endMinus, endPlus, allStudents, allQuests;
        public TMP_InputField nameInput, topicInput;
        public TMP_Text dateText, startText, endText, durationText, studentCount, questCount, allStudentsLabel;
        public CheckRowView studentTemplate, questTemplate;

        private readonly List<CheckRowView> studentRows = new List<CheckRowView>();
        private readonly List<CheckRowView> questRows = new List<CheckRowView>();
        private readonly HashSet<string> students = new HashSet<string>();
        private readonly HashSet<string> quests = new HashSet<string>();
        private int dayOffset, start, end;   // minutes after midnight

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
            saveButton.onClick.AddListener(Save);
            prevDay.onClick.AddListener(() => { dayOffset = Mathf.Max(0, dayOffset - 1); Refresh(); });
            nextDay.onClick.AddListener(() => { dayOffset = Mathf.Min(60, dayOffset + 1); Refresh(); });
            startMinus.onClick.AddListener(() => MoveStart(-StepMinutes));
            startPlus.onClick.AddListener(() => MoveStart(StepMinutes));
            endMinus.onClick.AddListener(() => MoveEnd(-StepMinutes));
            endPlus.onClick.AddListener(() => MoveEnd(StepMinutes));
            allStudents.onClick.AddListener(() =>
            {
                bool all = students.Count == Classroom.Students.Count;
                students.Clear();
                if (!all) foreach (var s in Classroom.Students) students.Add(s.Id);
                Refresh();
            });
            allQuests.onClick.AddListener(() =>
            {
                quests.Clear();
                foreach (var d in Svc.Devices) if (d.Online) quests.Add(d.Id);
                Refresh();
            });
            studentTemplate.gameObject.SetActive(false);
            questTemplate.gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            // a fresh form every time it opens
            nameInput.text = "";
            topicInput.text = "";
            dayOffset = 0;
            int now = DateTime.Now.Hour * 60;
            start = Mathf.Clamp(now + 60, 8 * 60, 22 * 60);
            end = start + 60;
            students.Clear();
            quests.Clear();
            base.OnShow();
        }

        void MoveStart(int d)
        {
            int len = end - start;
            start = Mathf.Clamp(start + d, 0, 24 * 60 - StepMinutes);
            end = Mathf.Min(24 * 60, start + len);
            Refresh();
        }

        void MoveEnd(int d)
        {
            end = Mathf.Clamp(end + d, start + StepMinutes, 24 * 60);
            Refresh();
        }

        public static string Hm(int minutes) { return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"); }

        public string NextName() { return "Meeting " + (Classroom.Meetings.Count + 1).ToString("00"); }

        // the tour and the quick buttons use these
        public void Fill(string name, string topic) { nameInput.text = name; topicInput.text = topic; }
        public void ToggleStudent(string id) { if (!students.Remove(id)) students.Add(id); Refresh(); }

        public void ToggleQuest(string id)
        {
            var d = Classroom.Device(Svc, id);
            if (d == null) return;
            if (!d.Online) { nav.Say(d.Name + " is offline: it cannot be assigned"); return; }
            if (!quests.Remove(id)) quests.Add(id);
            Refresh();
        }

        void Save()
        {
            string topic = topicInput.text.Trim();
            if (topic.Length == 0) { nav.Say("Enter today's topic"); return; }
            if (students.Count == 0) { nav.Say("Select at least one student"); return; }
            if (quests.Count == 0) { nav.Say("Assign at least one online Quest"); return; }
            var m = new Meeting
            {
                Name = nameInput.text.Trim().Length > 0 ? nameInput.text.Trim() : NextName(),
                Topic = topic, Start = Hm(start), End = Hm(end), Date = DateTime.Today.AddDays(dayOffset)
            };
            foreach (var s in Classroom.Students) if (students.Contains(s.Id)) m.StudentIds.Add(s.Id);
            foreach (var d in Svc.Devices) if (quests.Contains(d.Id)) m.QuestIds.Add(d.Id);
            Classroom.Add(m);
            nav.Back();
            nav.Say(m.Name + " saved");
        }

        public override void Refresh()
        {
            ((TMP_Text)nameInput.placeholder).text = "e.g. " + NextName();
            dateText.text = Classroom.DateText(DateTime.Today.AddDays(dayOffset));
            prevDay.interactable = dayOffset > 0;
            startText.text = Hm(start);
            endText.text = Hm(end);
            durationText.text = (end - start) + " min";

            Keep(studentTemplate, studentRows, Classroom.Students.Count);
            for (int i = 0; i < Classroom.Students.Count; i++)
            {
                var s = Classroom.Students[i];
                var row = studentRows[i];
                row.Bind(s.Id, s.Name, s.Email, students.Contains(s.Id), null, ColorRole.Primary);
                row.button.onClick.RemoveAllListeners();
                string id = s.Id;
                row.button.onClick.AddListener(() => ToggleStudent(id));
            }
            studentCount.text = students.Count + " of " + Classroom.Students.Count + " selected";
            allStudentsLabel.text = students.Count == Classroom.Students.Count ? "Clear" : "Select all";

            var devs = Svc.Devices;
            Keep(questTemplate, questRows, devs.Count);
            for (int i = 0; i < devs.Count; i++)
            {
                var d = devs[i];
                var st = DeviceStatus.Of(d);
                var row = questRows[i];
                row.Bind(d.Id, d.Name, d.Online ? DeviceStatus.Battery(d) + "  •  " + d.Ip : "Offline: cannot be assigned",
                    d.Online ? quests.Contains(d.Id) : (bool?)null, DeviceStatus.Label(st), DeviceStatus.Role(st), !d.Online);
                row.button.onClick.RemoveAllListeners();
                string id = d.Id;
                row.button.onClick.AddListener(() => ToggleQuest(id));
            }
            questCount.text = quests.Count + " assigned";
        }
    }
}
