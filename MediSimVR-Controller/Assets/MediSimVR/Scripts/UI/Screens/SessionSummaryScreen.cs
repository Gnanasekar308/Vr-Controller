using System.Text;
using TMPro;
using UnityEngine.UI;

namespace MediSimVR
{
    // After End session / Leave: duration, devices, event counts, where the PC saved the session.
    public class SessionSummaryScreen : AppScreen
    {
        public TMP_Text durationText, devicesText, countsText, savedText;
        public Button newButton, homeButton;
        [System.NonSerialized] public SessionSummary summary;

        private static readonly SessionEventKind[] Order =
        {
            SessionEventKind.Play, SessionEventKind.Pause, SessionEventKind.Stop, SessionEventKind.Content, SessionEventKind.Note,
            SessionEventKind.Instruction, SessionEventKind.Text, SessionEventKind.Voice, SessionEventKind.Mute, SessionEventKind.Object,
            SessionEventKind.Join, SessionEventKind.Leave
        };

        public override void Init(Navigator n)
        {
            base.Init(n);
            newButton.onClick.AddListener(() => nav.Tab(ScreenId.Session));
            homeButton.onClick.AddListener(() => nav.Tab(ScreenId.Home));
        }

        // the JSON record is sent to the PC after the end: show its progress
        protected override void OnEnable() { base.OnEnable(); SessionLog.Changed += OnLog; }
        protected override void OnDisable() { base.OnDisable(); SessionLog.Changed -= OnLog; }
        private void OnLog() { if (isActiveAndEnabled) Refresh(); }

        public override void Refresh()
        {
            if (summary == null)
            {
                durationText.text = "--:--:--";
                devicesText.text = "-";
                countsText.text = "No session yet";
                savedText.text = "";
                return;
            }
            durationText.text = Fmt.Clock(summary.Duration);
            devicesText.text = summary.Devices + (summary.Role == SessionRole.Player ? " Quest (player)" : " Quests (host)");
            // two columns (fits at every text size)
            var sb = new StringBuilder();
            for (int i = 0; i < Order.Length; i++)
            {
                int c;
                summary.Counts.TryGetValue(Order[i], out c);
                if (i % 2 == 0) { if (sb.Length > 0) sb.Append('\n'); }
                else sb.Append("<pos=52%>");
                sb.Append(Label(Order[i])).Append(":  ").Append(c);
            }
            countsText.text = sb.ToString();
            // the JSON record: on this phone + the PC recorder
            savedText.text = SessionLog.FilePath.Length == 0 ? "Session record: not saved"
                : "Saved on this phone: " + SessionLog.Folder + "/" + System.IO.Path.GetFileName(SessionLog.FilePath) +
                  "\n" + (SessionLog.UploadStatus.Length > 0 ? SessionLog.UploadStatus : "Sending to the PC...");
        }

        private static string Label(SessionEventKind k)
        {
            switch (k)
            {
                case SessionEventKind.Content: return "Content sent";
                case SessionEventKind.Note: return "Lesson notes";
                case SessionEventKind.Instruction: return "Important instructions";
                case SessionEventKind.Text: return "Text messages";
                case SessionEventKind.Voice: return "Voice";
                case SessionEventKind.Object: return "Object moves";
                case SessionEventKind.Join: return "Joined";
                case SessionEventKind.Leave: return "Left";
                default: return k.ToString();
            }
        }
    }
}
