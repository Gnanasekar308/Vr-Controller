using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Session tab start: HOST and PLAYER cards; a running session card; "Host already active" message (demo).
    public class SessionStartScreen : AppScreen
    {
        public TMP_Text modeText, activeText;
        public GameObject demoBadge, activeCard, unavailable;
        public Button hostButton, playerButton, activeButton;

        private ISessionService Ses { get { return AppRoot.Session; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            hostButton.onClick.AddListener(StartHost);
            playerButton.onClick.AddListener(() =>
            {
                if (Ses == null) { nav.Say("Session backend not connected yet"); return; }
                if (Ses.Active) { OpenActive(); return; }
                nav.Push(ScreenId.SessionPlayerPick);
            });
            activeButton.onClick.AddListener(OpenActive);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Ses != null) Ses.Changed += OnSession;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Ses != null) Ses.Changed -= OnSession;
        }

        private void OnSession() { if (isActiveAndEnabled) Refresh(); }

        private void StartHost()
        {
            if (Ses == null) { nav.Say("Session backend not connected yet"); return; }
            if (Ses.Active) { OpenActive(); return; }
            if (Ses.StartHost(false)) { nav.Push(ScreenId.SessionHost); return; }
            // another phone is hosting (demo shows this once)
            nav.popup.Show(DemoConfig.HostActiveTitle, Ses.OtherHostText, "Take over", () =>
            {
                Ses.StartHost(true);
                nav.Push(ScreenId.SessionHost);
            }, "Cancel");
        }

        private void OpenActive()
        {
            if (Ses == null || !Ses.Active) return;
            nav.Push(Ses.Role == SessionRole.Host ? ScreenId.SessionHost : ScreenId.SessionPlayer);
        }

        public static string ModeLine(bool withDemo = false)
        {
            var s = AppRoot.Session;
            string mode;
            if (s == null || !s.Active) mode = DemoConfig.ModeNone;
            else if (s.Role == SessionRole.Host) mode = DemoConfig.ModeHost;
            else
            {
                string q = s.PlayerDeviceId;
                if (AppRoot.Service != null) foreach (var d in AppRoot.Service.Devices) if (d.Id == s.PlayerDeviceId) q = d.Name;
                mode = DemoConfig.ModePlayer + "  •  " + q;
            }
            return "Mode: " + mode + (withDemo && AppRoot.Demo ? "  •  " + DemoConfig.DemoBadge : "");
        }

        public override void Refresh()
        {
            modeText.text = ModeLine();
            demoBadge.SetActive(AppRoot.Demo);
            unavailable.SetActive(Ses == null);
            bool active = Ses != null && Ses.Active;
            activeCard.SetActive(active);
            if (active)
                activeText.text = (Ses.Role == SessionRole.Host ? "Host session running" : "Player session running") +
                                  "  •  " + Fmt.Clock(Ses.Elapsed) + "\nTap to open";
        }

        private float nextTick;

        private void Update()
        {
            if (Ses == null || !Ses.Active || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            Refresh();
        }
    }
}
