using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Devices tab = step 1 of the host flow (Session > HOST opens it): filter chips All / Online / Offline / In Use
    // (with counts), the Quest cards (status chip, battery, IP, what plays), select one / several / all online (offline
    // cannot be selected), Refresh (scan), "Connect (n)" -> connects the selected Quests and opens Send.
    public class DevicesScreen : AppScreen
    {
        public Button refreshButton, connectButton, selectAllButton;
        public RectTransform refreshIcon;
        public DeviceListView list;
        public Segmented filterSeg;
        public TMP_Text subtitleText, connectLabel, selectAllLabel, selectedText;

        static readonly string[] FilterNames = { "All", "Online", "Offline", "In Use" };
        private int filter;
        private readonly List<QuestDevice> shown = new List<QuestDevice>();

        public override void Init(Navigator n)
        {
            base.Init(n);
            refreshButton.onClick.AddListener(() => { Svc.Refresh(); nav.Say("Scanning for Quest devices..."); });
            filterSeg.Changed += i => { filter = i; Refresh(); };
            selectAllButton.onClick.AddListener(SelectAllOnline);
            connectButton.onClick.AddListener(Connect);
        }

        void Update()
        {
            if (Svc != null && Svc.Scanning) refreshIcon.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            else if (refreshIcon.localEulerAngles.z != 0f) refreshIcon.localEulerAngles = Vector3.zero;
        }

        // the chips match the card status: Online = online and free, In Use = plays / shows something
        private static bool Pass(QuestDevice d, int f)
        {
            var s = DeviceStatus.Of(d);
            return f == 0 || (f == 1 && s == QuestState.Online) || (f == 2 && s == QuestState.Offline) || (f == 3 && s == QuestState.InUse);
        }

        public override void Refresh()
        {
            string step = ControlFlow.Step(ScreenId.Devices);
            subtitleText.text = step.Length > 0 ? step : "Manage and connect Quest devices";
            for (int f = 0; f < FilterNames.Length; f++)
            {
                int c = 0;
                foreach (var d in Svc.Devices) if (Pass(d, f)) c++;
                filterSeg.SetLabel(f, FilterNames[f] + " (" + c + ")");
            }
            filterSeg.Select(filter, false);

            shown.Clear();
            foreach (var d in Svc.Devices) if (Pass(d, filter)) shown.Add(d);
            list.Show(shown, Svc, d => { if (DeviceListView.ToggleSelect(d, nav)) Refresh(); },
                filter == 0 ? "No Quests found: tap Refresh" : "No " + FilterNames[filter].ToLowerInvariant() + " Quests");

            int sel = Svc.SelectedOnline().Count, online = 0;
            foreach (var d in Svc.Devices) if (d.Online) online++;
            selectAllLabel.text = online > 0 && sel == online ? "Clear" : "Select all online";
            selectedText.text = sel + " of " + online + " online selected";
            connectLabel.text = "Connect (" + sel + ")";
        }

        private void SelectAllOnline()
        {
            int sel = Svc.SelectedOnline().Count, online = 0;
            foreach (var d in Svc.Devices) if (d.Online) online++;
            bool all = !(online > 0 && sel == online);
            foreach (var d in Svc.Devices) d.Selected = all && d.Online;
            Refresh();
        }

        // connect the selected Quests, then step 2: Send
        private void Connect()
        {
            var sel = Svc.SelectedOnline();
            if (sel.Count == 0) { nav.Say("Select at least one online Quest"); return; }
            Svc.Connect(sel);
            var names = new System.Text.StringBuilder();
            foreach (var d in sel) { if (names.Length > 0) names.Append(", "); names.Append(d.Name); }
            SessionLog.Add(SessionEventKind.Join, "Host", "connected " + names);
            nav.Say("Connected " + sel.Count + " Quest(s)");
            nav.Push(ScreenId.Send);
        }
    }
}
