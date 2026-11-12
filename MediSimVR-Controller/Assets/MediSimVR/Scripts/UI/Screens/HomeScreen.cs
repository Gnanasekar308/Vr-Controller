using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public class HomeScreen : AppScreen
    {
        public Button bellButton, controlModeButton, receiverModeButton, modeButton;
        public GameObject badge, demoBadge;
        public TMP_Text badgeText, modeText;

        public override void Init(Navigator n)
        {
            base.Init(n);
            bellButton.onClick.AddListener(() => nav.Push(ScreenId.Notifications));
            controlModeButton.onClick.AddListener(() => nav.Tab(ScreenId.Devices));
            receiverModeButton.onClick.AddListener(() => nav.popup.Show("Receiver Mode",
                "Receiver Mode runs inside the Meta Quest app (MQ Receiver). Install it on each Quest; this phone app controls them.",
                "OK"));
            if (modeButton != null) modeButton.onClick.AddListener(() => nav.Tab(ScreenId.Session));
        }

        public override void Refresh()
        {
            int n = Svc.UnreadCount;
            badge.SetActive(n > 0);
            badgeText.text = n > 9 ? "9+" : n.ToString();
            if (modeText != null) modeText.text = SessionStartScreen.ModeLine();   // current session mode
            if (demoBadge != null) demoBadge.SetActive(AppRoot.Demo);
        }
    }
}
