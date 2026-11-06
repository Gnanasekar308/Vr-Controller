using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    public class NotificationsScreen : AppScreen
    {
        public Button backButton, clearButton;
        public NotificationRowView rowTemplate;
        public TMP_Text emptyText;

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(nav.Back);
            clearButton.onClick.AddListener(() => Svc.ClearNotifications());
        }

        public override void OnShow()
        {
            base.OnShow();
            Svc.MarkAllRead();
        }

        public override void Refresh()
        {
            int count = Svc.NotificationsEnabled ? Svc.Notifications.Count : 0;
            var rows = Fill(rowTemplate, count);
            for (int i = 0; i < rows.Count; i++) rows[i].Bind(Svc.Notifications[i]);
            emptyText.gameObject.SetActive(count == 0);
            emptyText.text = Svc.NotificationsEnabled ? "No notifications" : "Notifications are disabled (Settings)";
        }
    }
}
