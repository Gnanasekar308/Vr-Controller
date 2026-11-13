using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // Admin / Student login (mock: any email + password works). The quick button fills the demo account and logs in.
    public class LoginScreen : AppScreen
    {
        public UserRole role = UserRole.Admin;
        public TMP_Text titleText, subtitleText, quickLabel;
        public Image roleIcon;
        public UnityEngine.Sprite adminSprite, studentSprite;
        public TMP_InputField emailInput, passwordInput;
        public Button backButton, loginButton, quickButton;

        public override bool ShowsNav { get { return false; } }

        public override bool TryGetParent(out ScreenId parent) { parent = ScreenId.RoleSelect; return true; }

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(() => nav.Back());
            loginButton.onClick.AddListener(Login);
            quickButton.onClick.AddListener(() =>
            {
                emailInput.text = role == UserRole.Admin ? DemoConfig.AdminEmail
                    : DemoConfig.Students[DemoConfig.DemoStudentIndex].Email;
                passwordInput.text = DemoConfig.DemoPassword;
                Login();
            });
        }

        public override void OnShow()
        {
            emailInput.text = "";
            passwordInput.text = "";
            base.OnShow();
        }

        void Login()
        {
            if (string.IsNullOrEmpty(emailInput.text.Trim()) || string.IsNullOrEmpty(passwordInput.text))
            {
                nav.Say("Enter your email and password");
                return;
            }
            Classroom.Login(role, emailInput.text);
            nav.Tab(ScreenId.Dashboard);
            nav.Say("Welcome, " + Classroom.UserName);
        }

        public override void Refresh()
        {
            bool admin = role == UserRole.Admin;
            titleText.text = admin ? "Admin login" : "Student login";
            subtitleText.text = admin ? "Meetings, live sessions and reports" : "Join your class on a Quest";
            quickLabel.text = admin ? "Demo admin" : "Demo student";
            if (roleIcon != null) roleIcon.sprite = admin ? adminSprite : studentSprite;
        }
    }
}
