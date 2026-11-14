using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Create Meeting form: name, today's topic, date (day picker), class timing (start - end, 15 min steps), Quest
    // devices (multi-select). Only online Quests are listed (Online / In Use); every Quest shows the student signed in
    // on it (name + ID entered on the headset), so picking a Quest adds its student. Start Live -> the meeting is saved
    // and its live class starts at once.
    public class MeetingCreateScreen : AppScreen
    {
        public const int StepMinutes = 15;

        public Button backButton, saveButton, prevDay, nextDay, startMinus, startPlus, endMinus, endPlus, allQuests;
        public TMP_InputField nameInput, topicInput;
        public TMP_Text dateText, startText, endText, durationText, questCount;
        public CheckRowView questTemplate;

        private readonly List<CheckRowView> questRows = new List<CheckRowView>();
        private readonly HashSet<string> quests = new HashSet<string>();
        private readonly List<QuestDevice> shown = new List<QuestDevice>();   // the online Quests with a student
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
            allQuests.onClick.AddListener(() =>
            {
                quests.Clear();
                foreach (var d in Svc.Devices) if (d.Online && Classroom.StudentOfQuest(d.Id).Length > 0) quests.Add(d.Id);
                Refresh();
            });
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

        // the tour uses these
        public void Fill(string name, string topic) { nameInput.text = name; topicInput.text = topic; }
        public int SelectedCount { get { return quests.Count; } }

        public void ToggleQuest(string id)
        {
            var d = Classroom.Device(Svc, id);
            if (d == null) return;
            if (!d.Online) { nav.Say(d.Name + " is offline: it cannot be added"); return; }
            if (Classroom.StudentOfQuest(id).Length == 0) { nav.Say(d.Name + " has no student signed in"); return; }
            if (!quests.Remove(id)) quests.Add(id);
            Refresh();
        }

        void Save()
        {
            string topic = topicInput.text.Trim();
            if (topic.Length == 0) { nav.Say("Enter today's topic"); return; }
            if (quests.Count == 0) { nav.Say("Select at least one online Quest"); return; }
            var m = new Meeting
            {
                Name = nameInput.text.Trim().Length > 0 ? nameInput.text.Trim() : NextName(),
                Topic = topic, Start = Hm(start), End = Hm(end), Date = DateTime.Today.AddDays(dayOffset)
            };
            foreach (var d in Svc.Devices)
                if (quests.Contains(d.Id)) { m.QuestIds.Add(d.Id); m.StudentIds.Add(Classroom.StudentOfQuest(d.Id)); }
            Classroom.Add(m);
            Classroom.Selected = m;
            if (Classroom.LiveActive)
            {
                // one live session at a time: the new meeting waits in the list
                nav.Back();
                nav.Say(m.Name + " saved. End " + Classroom.Live.Name + " to start it");
                return;
            }
            // straight into the live class, already started
            Classroom.Open(m);
            foreach (var d in Svc.Devices) d.Selected = d.Online && m.QuestIds.Contains(d.Id);
            nav.Tab(ScreenId.Live);
            var live = nav.Get<LiveSessionScreen>();
            if (live != null) live.StartSession();
            nav.Say(m.Name + " is live");
        }

        public override void Refresh()
        {
            ((TMP_Text)nameInput.placeholder).text = "e.g. " + NextName();
            dateText.text = Classroom.DateText(DateTime.Today.AddDays(dayOffset));
            prevDay.interactable = dayOffset > 0;
            startText.text = Hm(start);
            endText.text = Hm(end);
            durationText.text = (end - start) + " min";

            // only online Quests (an offline one cannot join); a Quest that went offline is dropped from the selection
            shown.Clear();
            foreach (var d in Svc.Devices)
                if (d.Online && Classroom.StudentOfQuest(d.Id).Length > 0) shown.Add(d);
                else quests.Remove(d.Id);
            var devs = shown;
            Keep(questTemplate, questRows, devs.Count);
            for (int i = 0; i < devs.Count; i++)
            {
                var d = devs[i];
                var st = DeviceStatus.Of(d);
                string sid = Classroom.StudentOfQuest(d.Id);
                var row = questRows[i];
                row.Bind(d.Id, d.Name + "  •  " + (sid.Length > 0 ? Classroom.FirstName(sid) : "no student"),
                    (sid.Length > 0 ? "ID " + Classroom.StudentById(sid).Code + "  •  " : "") + (d.Online ? DeviceStatus.Battery(d) : "Offline"),
                    d.Online ? quests.Contains(d.Id) : (bool?)null, DeviceStatus.Label(st), DeviceStatus.Role(st), !d.Online);
                row.button.onClick.RemoveAllListeners();
                string id = d.Id;
                row.button.onClick.AddListener(() => ToggleQuest(id));
            }
            questCount.text = quests.Count + (quests.Count == 1 ? " Quest" : " Quests") + "  •  " + quests.Count + (quests.Count == 1 ? " student" : " students");
        }
    }
}
