using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Session screen (Home > Control Mode, or the Session tab): HOST and PLAYER cards.
    //   HOST   -> Quest list -> Play page ("Host already active" message first in demo mode)
    //   PLAYER -> Play page on "My Quest" (no list)
    // A running session shows a card that opens its live dashboard (voice, chat, timeline). Up: Home.
    public class SessionStartScreen : AppScreen
    {
        public TMP_Text modeText, activeText, activeSub, activeTimeText;
        public GameObject demoBadge, activeCard, activeShadow, unavailable;
        public Button hostButton, playerButton, activeButton;
        public SpeakingPulse livePulse;   // green LIVE dot of the running-session card

        private ISessionService Ses { get { return AppRoot.Session; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            hostButton.onClick.AddListener(Host);
            playerButton.onClick.AddListener(Player);
            activeButton.onClick.AddListener(OpenDashboard);
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

        private void Host()
        {
            if (Ses == null) { ControlFlow.StartLocalHost(); nav.Push(ScreenId.QuestList); return; }
            if (Ses.Active && Ses.Role == SessionRole.Host) { nav.Push(ScreenId.QuestList); return; }
            if (Ses.Active) { Switch("Switch to Host?", "This ends your player session.", Host); return; }
            if (Ses.StartHost(false)) { nav.Push(ScreenId.QuestList); return; }
            // another phone is hosting (demo shows this once)
            nav.popup.Show(DemoConfig.HostActiveTitle, Ses.OtherHostText, "Take over", () =>
            {
                Ses.StartHost(true);
                nav.Push(ScreenId.QuestList);
            }, "Cancel");
        }

        private void Player()
        {
            if (Ses != null && Ses.Active && Ses.Role == SessionRole.Host) { Switch("Switch to Player?", "This ends the host session.", Player); return; }
            var q = ControlFlow.MyQuest(Svc);
            if (q == null || !q.Online) { nav.Say("No Quest is online"); return; }
            ControlFlow.UsePlayerQuest(Svc, q);
            nav.Push(ScreenId.Play);
        }

        private void Switch(string title, string text, System.Action again)
        {
            nav.popup.Show(title, text, "Switch", () => { Ses.End(); again(); }, "Cancel", destructive: true);
        }

        private void OpenDashboard()
        {
            if (Ses == null || !Ses.Active) return;
            nav.Push(Ses.Role == SessionRole.Host ? ScreenId.SessionHost : ScreenId.SessionPlayer);
        }

        public static string ModeLine(bool withDemo = false)
        {
            var role = ControlFlow.Role;
            string mode = role == SessionRole.Host ? DemoConfig.ModeHost
                : role == SessionRole.Player ? DemoConfig.ModePlayer + "  •  " + ControlFlow.PlayerQuestName(AppRoot.Service)
                : DemoConfig.ModeNone;
            return "Mode: " + mode + (withDemo && AppRoot.Demo ? "  •  " + DemoConfig.DemoBadge : "");
        }

        public override void Refresh()
        {
            modeText.text = ModeLine();
            demoBadge.SetActive(AppRoot.Demo);
            unavailable.SetActive(Ses == null);
            bool active = Ses != null && Ses.Active;
            if (activeCard.activeSelf != active) activeCard.SetActive(active);
            if (activeShadow != null && activeShadow.activeSelf != active) activeShadow.SetActive(active);
            if (livePulse != null) livePulse.Set(active);
            if (!active) return;
            activeText.text = Ses.Role == SessionRole.Host ? "Live host session" : "Live player session";
            if (activeTimeText != null) activeTimeText.text = Fmt.Clock(Ses.Elapsed);
            if (activeSub != null) activeSub.text = DemoConfig.DashboardHint;
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
