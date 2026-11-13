using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public class SettingsScreen : AppScreen
    {
        public Button demoRow, customizationRow, networkRow, connectionRow, notificationRow, aboutRow, logoutRow;
        public TMP_Text demoSub, notificationSub, aboutSub, accountName, accountSub, logoutSub;

        public override void Init(Navigator n)
        {
            base.Init(n);
            // Logout: back to Role selection (a live session ends first)
            logoutRow.onClick.AddListener(() => nav.popup.Show("Log out?",
                Classroom.LiveActive ? "The live session " + Classroom.Live.Name + " ends for you." : "You return to the role selection.",
                "Log out", () =>
                {
                    var ses = AppRoot.Session;
                    if (ses != null && ses.Active) ses.End();
                    Classroom.Logout();
                    nav.Tab(ScreenId.RoleSelect);
                    nav.Say("Logged out");
                }, "Cancel", destructive: true));
            demoRow.onClick.AddListener(() =>
            {
                bool on = AppRoot.Demo;
                nav.popup.Show(on ? "Turn demo mode OFF?" : "Turn demo mode ON?",
                    on ? "The app restarts and uses the real network (Quests on this Wi-Fi). Sessions need the backend."
                       : "The app restarts with sample Quests and sessions. No device or network needed.",
                    on ? "Turn off" : "Turn on", () => AppRoot.SetDemoAndRestart(!on), "Cancel");
            });
            customizationRow.onClick.AddListener(() => nav.Push(ScreenId.EditApp));
            networkRow.onClick.AddListener(() => nav.popup.Show("Network",
                "Discovery: UDP 8766\nControl: TCP 8765\nTransfer: TCP 8767\nScene: UDP 8771\n\n" +
                (AppRoot.Demo ? "Demo mode: nothing is sent, all data is sample data." : "Real network: Quests on this Wi-Fi are found automatically."), "OK"));
            connectionRow.onClick.AddListener(() => nav.popup.Show("Manual IP Connect", "Enter the Quest IP address", "Add",
                null, "Cancel", "e.g. 192.168.1.30", ip => { Svc.AddManualDevice(ip); nav.Say("Device added"); }));
            notificationRow.onClick.AddListener(() =>
            {
                Svc.NotificationsEnabled = !Svc.NotificationsEnabled;
                nav.Say("Notifications " + (Svc.NotificationsEnabled ? "enabled" : "disabled"));
            });
            aboutRow.onClick.AddListener(() => nav.popup.Show("About",
                "MediSimVR v" + Application.version + "\nQuest Media Control\n© MediSim", "OK"));
        }

        public override void Refresh()
        {
            demoSub.text = AppRoot.Demo ? "ON: sample Quests and sessions, no network" : "OFF: real Quests on this Wi-Fi";
            notificationSub.text = Svc.NotificationsEnabled ? "Enabled" : "Disabled";
            aboutSub.text = "MediSimVR v" + Application.version;
            accountName.text = Classroom.UserName.Length > 0 ? Classroom.UserName : "Not logged in";
            accountSub.text = (Classroom.IsAdmin ? "Admin" : Classroom.IsStudent ? "Student" : "-") + (AppRoot.Demo ? "  •  demo account" : "");
            logoutSub.text = "Back to the role selection";
        }
    }
}
