using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Dashboard after login. Admin: greeting, Meeting Room card, stats (students, Quests online, sessions today), live
    // banner, today's meetings. Student: greeting, Available Meetings card, stats (my meetings, today, my Quest), my
    // meetings today.
    public class DashboardScreen : AppScreen
    {
        public TMP_Text greetingText, nameText, mainCardTitle, mainCardSub, listTitle, emptyText, liveText;
        public Button bellButton, mainCard, liveBanner;
        public GameObject badge, liveBannerRoot, demoBadge;
        public TMP_Text badgeText;
        public TMP_Text[] statValues = new TMP_Text[3];
        public TMP_Text[] statLabels = new TMP_Text[3];
        public MeetingRowView rowTemplate;

        private readonly List<MeetingRowView> rows = new List<MeetingRowView>();
        private float tick;

        public override void Init(Navigator n)
        {
            base.Init(n);
            bellButton.onClick.AddListener(() => nav.Push(ScreenId.Notifications));
            mainCard.onClick.AddListener(() => nav.Tab(ScreenId.Meetings));
            liveBanner.onClick.AddListener(() => nav.Tab(ScreenId.Live));
            rowTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnable() { base.OnEnable(); Classroom.Changed += OnClass; }
        protected override void OnDisable() { base.OnDisable(); Classroom.Changed -= OnClass; }
        void OnClass() { if (isActiveAndEnabled) Refresh(); }

        void Open(Meeting m)
        {
            Classroom.Selected = m;
            nav.Push(ScreenId.MeetingDetails);
        }

        private void Update()
        {
            if (!Classroom.LiveActive) return;
            if ((tick += Time.unscaledDeltaTime) < 1f) return;
            tick = 0f;
            RefreshLive();
        }

        void RefreshLive()
        {
            bool live = Classroom.LiveActive;
            if (liveBannerRoot.activeSelf != live) liveBannerRoot.SetActive(live);
            if (live)
                liveText.text = Classroom.Live.Name + "  •  " + LiveSessionScreen.StateText() + "  •  " + LiveSessionScreen.Clock(Classroom.Elapsed);
        }

        public override void Refresh()
        {
            bool admin = Classroom.IsAdmin;
            int h = DateTime.Now.Hour;
            greetingText.text = (h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening") + (admin ? ", Admin" : ", Student");
            nameText.text = Classroom.UserName;
            int unread = Svc.UnreadCount;
            badge.SetActive(admin && unread > 0);
            badgeText.text = unread > 9 ? "9+" : unread.ToString();
            bellButton.gameObject.SetActive(admin);
            if (demoBadge != null) demoBadge.SetActive(AppRoot.Demo);
            mainCardTitle.text = admin ? "Meeting Room" : "Available Meetings";
            mainCardSub.text = admin ? "Create meetings, start live sessions" : "Join a class on your Quest";

            var visible = Classroom.Visible();
            var today = Classroom.Today();
            if (admin)
            {
                int online = 0;
                foreach (var d in Svc.Devices) if (d.Online) online++;
                Stat(0, Classroom.Students.Count.ToString(), "Students");
                Stat(1, online + "/" + Svc.Devices.Count, "Quests online");
                Stat(2, Classroom.SessionsToday.ToString(), "Sessions today");
            }
            else
            {
                string quest = "-";
                if (today.Count > 0) quest = Classroom.QuestName(Svc, Classroom.QuestIdFor(today[0], Classroom.StudentId)).Replace("Quest-", "");
                Stat(0, visible.Count.ToString(), "My meetings");
                Stat(1, today.Count.ToString(), "Today");
                Stat(2, quest, "My Quest");
            }
            listTitle.text = admin ? "Today's meetings" : "My meetings today";
            Keep(rowTemplate, rows, today.Count);
            for (int i = 0; i < today.Count; i++) rows[i].Bind(today[i], Open);
            emptyText.gameObject.SetActive(today.Count == 0);
            RefreshLive();
        }

        void Stat(int i, string value, string label)
        {
            if (i < statValues.Length && statValues[i] != null) statValues[i].text = value;
            if (i < statLabels.Length && statLabels[i] != null) statLabels[i].text = label;
        }
    }
}
