using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Admin: "Meeting Room" = Create Meeting + meeting list (first 3, View All Meetings shows the rest).
    // Student: "Available Meetings" = the meetings the student is in. Tap a meeting -> Meeting details.
    public class MeetingsScreen : AppScreen
    {
        public const int FirstCount = 3;

        public TMP_Text titleText, subtitleText, viewAllLabel, emptyText;
        public Button createButton, viewAllButton;
        public MeetingRowView rowTemplate;

        private readonly List<MeetingRowView> rows = new List<MeetingRowView>();
        private bool showAll;

        public override void Init(Navigator n)
        {
            base.Init(n);
            createButton.onClick.AddListener(() => nav.Push(ScreenId.MeetingCreate));
            viewAllButton.onClick.AddListener(() => { showAll = !showAll; Refresh(); });
            rowTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnable() { base.OnEnable(); Classroom.Changed += OnClass; }
        protected override void OnDisable() { base.OnDisable(); Classroom.Changed -= OnClass; }
        void OnClass() { if (isActiveAndEnabled) Refresh(); }

        public void ShowAll(bool on) { showAll = on; Refresh(); }
        public bool ShowingAll { get { return showAll; } }

        void Open(Meeting m)
        {
            Classroom.Selected = m;
            nav.Push(ScreenId.MeetingDetails);
        }

        public override void Refresh()
        {
            bool admin = Classroom.IsAdmin;
            titleText.text = admin ? "Meeting Room" : "Available Meetings";
            var all = Classroom.Visible();
            subtitleText.text = all.Count + (all.Count == 1 ? " meeting" : " meetings") + (admin ? "  •  create and start sessions" : "  •  tap one to join");
            createButton.gameObject.SetActive(admin);
            int count = showAll ? all.Count : Mathf.Min(FirstCount, all.Count);
            Keep(rowTemplate, rows, count);
            for (int i = 0; i < count; i++) rows[i].Bind(all[i], Open);
            viewAllButton.gameObject.SetActive(all.Count > FirstCount);
            viewAllLabel.text = showAll ? "Show fewer" : "View All Meetings (" + all.Count + ")";
            emptyText.gameObject.SetActive(all.Count == 0);
        }
    }
}
