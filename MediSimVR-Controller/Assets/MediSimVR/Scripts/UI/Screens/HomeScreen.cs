using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    public class HomeScreen : AppScreen
    {
        public Button bellButton, controlModeButton, receiverModeButton;
        public GameObject badge;
        public TMP_Text badgeText;

        public override void Init(Navigator n)
        {
            base.Init(n);
            bellButton.onClick.AddListener(() => nav.Push(ScreenId.Notifications));
            controlModeButton.onClick.AddListener(() => nav.Tab(ScreenId.Devices));
            receiverModeButton.onClick.AddListener(() => nav.popup.Show("Receiver Mode",
                "Receiver Mode runs inside the Meta Quest app (MQ Receiver). Install it on each Quest; this phone app controls them.",
                "OK"));
        }

        public override void Refresh()
        {
            int n = Svc.UnreadCount;
            badge.SetActive(n > 0);
            badgeText.text = n > 9 ? "9+" : n.ToString();
        }
    }
}
