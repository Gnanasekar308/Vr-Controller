using UnityEngine;

namespace MediSimVR
{
    // State of the Control Mode flow: Home > Control Mode > Session (HOST / PLAYER) > Quest list (host) > Play page.
    // The role comes from the session service when the app has one (demo mode); without it (demo off: no session backend
    // yet) it is kept here, so the flow itself also works with the real controller service.
    // "My Quest" (player) = the Quest last used as player if it is online, else the first online Quest.
    public static class ControlFlow
    {
        public const string MyQuestKey = "msvr_my_quest";
        private static SessionRole localRole;

        public static SessionRole Role
        {
            get
            {
                var s = AppRoot.Session;
                if (s != null) return s.Active ? s.Role : SessionRole.None;
                return localRole;
            }
        }

        public static bool IsPlayer { get { return Role == SessionRole.Player; } }

        // app start / demo switch (statics survive a scene reload)
        public static void Reset() { localRole = SessionRole.None; }

        // host without a session service (the session service path is handled by the Session screen: other-host message)
        public static void StartLocalHost() { localRole = SessionRole.Host; }

        public static QuestDevice MyQuest(IControllerService svc)
        {
            if (svc == null) return null;
            // in a player session: the session's Quest, even while it is offline (the Play page then says so)
            var s = AppRoot.Session;
            if (s != null && s.Active && s.Role == SessionRole.Player)
                foreach (var d in svc.Devices) if (d.Id == s.PlayerDeviceId) return d;
            string id = PlayerPrefs.GetString(MyQuestKey, "");
            QuestDevice first = null;
            foreach (var d in svc.Devices)
            {
                if (d.Id == id && d.Online) return d;
                if (first == null && d.Online) first = d;
            }
            return first;
        }

        // play as player on this Quest (online): remember it, the controls act on it only, join the session as player
        public static void UsePlayerQuest(IControllerService svc, QuestDevice d)
        {
            if (svc == null || d == null) return;
            PlayerPrefs.SetString(MyQuestKey, d.Id);
            PlayerPrefs.Save();
            SelectOnly(svc, d);
            var s = AppRoot.Session;
            if (s == null) { localRole = SessionRole.Player; return; }
            if (!(s.Active && s.Role == SessionRole.Player && s.PlayerDeviceId == d.Id)) s.StartPlayer(d.Id);
        }

        // only d is selected (d == null: nothing; an offline d is never a target: SelectedOnline skips it)
        public static void SelectOnly(IControllerService svc, QuestDevice d)
        {
            foreach (var x in svc.Devices) x.Selected = d != null && x == d;
        }

        // step line of the Control Mode flow: Host = Devices (1) > Send (2) > Control (3); Player = Send (1) > Control (2).
        // "" outside the flow (a tab opened from the bottom bar without a role).
        public static string Step(ScreenId id)
        {
            var r = Role;
            if (r == SessionRole.None) return "";
            bool player = r == SessionRole.Player;
            int total = player ? 2 : 3;
            switch (id)
            {
                case ScreenId.Devices: return player ? "" : "Step 1 of 3  •  Select Quests and connect";
                case ScreenId.Send: return "Step " + (player ? 1 : 2) + " of " + total + "  •  Choose what to play";
                case ScreenId.Control: return "Step " + total + " of " + total + "  •  Play and guide the session";
                default: return "";
            }
        }

        // "Quest-A1B2" of the player, "" if not a player
        public static string PlayerQuestName(IControllerService svc)
        {
            if (!IsPlayer) return "";
            var d = MyQuest(svc);
            return d != null ? d.Name : "-";
        }
    }
}
