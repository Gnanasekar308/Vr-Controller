using UnityEngine.UI;

namespace MediSimVR
{
    // Role selection: two big cards ADMIN / STUDENT -> Login for that role (no bottom bar)
    public class RoleSelectScreen : AppScreen
    {
        public Button adminButton, studentButton;
        public UnityEngine.GameObject demoBadge;

        public override bool ShowsNav { get { return false; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            adminButton.onClick.AddListener(() => Open(UserRole.Admin));
            studentButton.onClick.AddListener(() => Open(UserRole.Student));
        }

        void Open(UserRole role)
        {
            var login = nav.Get<LoginScreen>();
            if (login != null) login.role = role;
            nav.Push(ScreenId.Login);
        }

        public override void Refresh()
        {
            if (demoBadge != null) demoBadge.SetActive(AppRoot.Demo);
        }
    }
}
