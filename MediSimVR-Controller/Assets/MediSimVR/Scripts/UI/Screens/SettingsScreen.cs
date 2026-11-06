using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public class SettingsScreen : AppScreen
    {
        public Button customizationRow, networkRow, connectionRow, notificationRow, aboutRow;
        public TMP_Text notificationSub, aboutSub;

        public override void Init(Navigator n)
        {
            base.Init(n);
            customizationRow.onClick.AddListener(() => nav.Push(ScreenId.EditApp));
            networkRow.onClick.AddListener(() => nav.popup.Show("Network",
                "Discovery: UDP 8766\nControl: TCP 8765\nTransfer: TCP 8767\n\nMock mode now. Real discovery starts when the Quest Receiver is connected.", "OK"));
            connectionRow.onClick.AddListener(() => nav.popup.Show("Manual IP Connect", "Enter the Quest IP address", "Add",
                null, "Cancel", "e.g. 192.168.1.30", ip => { Svc.AddManualDevice(ip); nav.Say("Device added"); }));
            notificationRow.onClick.AddListener(() =>
            {
                Svc.NotificationsEnabled = !Svc.NotificationsEnabled;
                nav.Say("Notifications " + (Svc.NotificationsEnabled ? "enabled" : "disabled"));
            });
            aboutRow.onClick.AddListener(() => nav.popup.Show("About",
                "MediSimVR v" + Application.version + "\nQuest Media Control\n\u00A9 MediSim", "OK"));
        }

        public override void Refresh()
        {
            notificationSub.text = Svc.NotificationsEnabled ? "Enabled" : "Disabled";
            aboutSub.text = "MediSimVR v" + Application.version;
        }
    }
}
