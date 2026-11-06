using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public class DevicesScreen : AppScreen
    {
        public Button refreshButton, connectButton;
        public RectTransform refreshIcon;
        public DeviceRowView rowTemplate;
        public TMP_Text questsText, inUseText, connectLabel, emptyText;

        public override void Init(Navigator n)
        {
            base.Init(n);
            refreshButton.onClick.AddListener(() => { Svc.Refresh(); nav.Say("Scanning for Quest devices..."); });
            connectButton.onClick.AddListener(() =>
            {
                var sel = Svc.SelectedOnline();
                if (sel.Count == 0) { nav.Say("Select at least one online device"); return; }
                Svc.Connect(sel);
                nav.Say("Connecting " + sel.Count + " device(s)...");
            });
        }

        void Update()
        {
            if (Svc != null && Svc.Scanning) refreshIcon.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            else if (refreshIcon.localEulerAngles.z != 0f) refreshIcon.localEulerAngles = Vector3.zero;
        }

        public override void Refresh()
        {
            int inUse = 0;
            foreach (var d in Svc.Devices) if (DeviceRowView.InUse(d)) inUse++;
            questsText.text = "Quests (" + Svc.Devices.Count + ")";
            inUseText.text = "In Use (" + inUse + ")";

            var rows = Fill(rowTemplate, Svc.Devices.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var d = Svc.Devices[i];
                rows[i].Bind(d, () =>
                {
                    if (!d.Online) { nav.Say(d.Name + " is offline"); return; }
                    d.Selected = !d.Selected;
                    Refresh();
                });
            }
            emptyText.gameObject.SetActive(Svc.Devices.Count == 0);
            connectLabel.text = "Connect (" + Svc.SelectedOnline().Count + ")";
        }
    }
}
