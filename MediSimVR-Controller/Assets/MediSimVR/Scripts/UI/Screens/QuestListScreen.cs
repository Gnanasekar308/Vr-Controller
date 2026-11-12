using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // HOST: pick the Quests to play on. Filter chips All / Online / Offline / In Use (with counts), the Devices list
    // (status chip, battery, what plays), select one / several / all online (offline cannot be selected),
    // "Continue (n)" -> Play page. Up: Session.
    public class QuestListScreen : AppScreen
    {
        public Button backButton, selectAllButton, continueButton;
        public TMP_Text subtitleText, selectAllLabel, selectedText, continueLabel;
        public Segmented filterSeg;
        public DeviceListView list;

        static readonly string[] FilterNames = { "All", "Online", "Offline", "In Use" };
        private int filter;
        private readonly List<QuestDevice> shown = new List<QuestDevice>();

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
            filterSeg.Changed += i => { filter = i; Refresh(); };
            selectAllButton.onClick.AddListener(SelectAllOnline);
            continueButton.onClick.AddListener(Continue);
        }

        public override bool TryGetParent(out ScreenId parent) { parent = ScreenId.Session; return true; }

        // the chips match the card status: Online = online and free, In Use = plays / shows something
        private bool Pass(QuestDevice d, int f)
        {
            var s = DeviceStatus.Of(d);
            return f == 0 || (f == 1 && s == QuestState.Online) || (f == 2 && s == QuestState.Offline) || (f == 3 && s == QuestState.InUse);
        }

        public override void Refresh()
        {
            subtitleText.text = SessionStartScreen.ModeLine(true);
            for (int f = 0; f < FilterNames.Length; f++)
            {
                int n = 0;
                foreach (var d in Svc.Devices) if (Pass(d, f)) n++;
                filterSeg.SetLabel(f, FilterNames[f] + " (" + n + ")");
            }
            filterSeg.Select(filter, false);

            shown.Clear();
            foreach (var d in Svc.Devices) if (Pass(d, filter)) shown.Add(d);
            list.Show(shown, Svc, d => { if (DeviceListView.ToggleSelect(d, nav)) Refresh(); },
                filter == 0 ? "No Quests found" : "No " + FilterNames[filter].ToLowerInvariant() + " Quests");

            int sel = Svc.SelectedOnline().Count, online = 0;
            foreach (var d in Svc.Devices) if (d.Online) online++;
            selectAllLabel.text = online > 0 && sel == online ? "Clear" : "Select all online";
            selectedText.text = sel + " of " + online + " online selected";
            continueLabel.text = "Continue (" + sel + ")";
        }

        private void SelectAllOnline()
        {
            int sel = Svc.SelectedOnline().Count, online = 0;
            foreach (var d in Svc.Devices) if (d.Online) online++;
            bool all = !(online > 0 && sel == online);
            foreach (var d in Svc.Devices) d.Selected = all && d.Online;
            Refresh();
        }

        private void Continue()
        {
            if (Svc.SelectedOnline().Count == 0) { nav.Say("Select at least one online Quest"); return; }
            nav.Push(ScreenId.Play);
        }
    }
}
