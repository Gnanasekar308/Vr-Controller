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
            SessionEventKind.Play, SessionEventKind.Pause, SessionEventKind.Stop, SessionEventKind.Content, SessionEventKind.Text,
            SessionEventKind.Voice, SessionEventKind.Mute, SessionEventKind.Object, SessionEventKind.Join, SessionEventKind.Leave
        };

        public override void Init(Navigator n)
        {
            base.Init(n);
            newButton.onClick.AddListener(() => nav.Tab(ScreenId.Session));
            homeButton.onClick.AddListener(() => nav.Tab(ScreenId.Home));
        }

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
            var sb = new StringBuilder();
            foreach (var k in Order)
            {
                int c;
                summary.Counts.TryGetValue(k, out c);
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Label(k)).Append(":  ").Append(c);
            }
            countsText.text = sb.ToString();
            savedText.text = "Saved on PC:  " + summary.SavedPath;
        }

        private static string Label(SessionEventKind k)
        {
            switch (k)
            {
                case SessionEventKind.Content: return "Content sent";
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
