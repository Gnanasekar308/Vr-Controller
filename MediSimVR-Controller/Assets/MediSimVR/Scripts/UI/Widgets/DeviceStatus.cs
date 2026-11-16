using UnityEngine;

namespace MediSimVR
{
    public enum QuestState { Online, Offline, InUse }

    // A Quest's status, the same in every list: Online (success green), Offline (secondary grey), In Use (primary blue:
    // it plays or shows something). Colours are theme roles of the design system.
    public static class DeviceStatus
    {
        public static bool InUse(QuestDevice d)
        {
            return d.Online && (d.State == DeviceState.Playing || d.State == DeviceState.Paused || d.State == DeviceState.ShowingText);
        }

        public static QuestState Of(QuestDevice d)
        {
            return !d.Online ? QuestState.Offline : InUse(d) ? QuestState.InUse : QuestState.Online;
        }

        public static string Label(QuestState s)
        {
            return s == QuestState.InUse ? "In Use" : s == QuestState.Online ? "Online" : "Offline";
        }

        public static ColorRole Role(QuestState s)
        {
            return s == QuestState.InUse ? ColorRole.Primary : s == QuestState.Online ? ColorRole.Success : ColorRole.SubText;
        }

        public static Color Tint(QuestState s) { return ThemeManager.Get(Role(s)); }

        // "Playing Anatomy_Heart_360.mp4", "Paused ...", "Idle"
        public static string Content(QuestDevice d, IControllerService svc)
        {
            string file = "";
            if (svc != null) foreach (var f in svc.Files) if (f.Id == d.FileId) file = f.Name;
            switch (d.State)
            {
                case DeviceState.Playing: return "Playing " + file;
                case DeviceState.Paused: return "Paused  " + file;
                case DeviceState.Stopped: return "Stopped  " + file;
                case DeviceState.ShowingText: return "Showing " + file;
                default: return file.Length > 0 ? "Ready: " + file : "Idle";
            }
        }

        // "86% battery"; "" when the device does not report it (Battery = -1) or is offline: callers hide it
        public static string Battery(QuestDevice d)
        {
            return d.Online && d.Battery >= 0 ? d.Battery + "% battery" : "";
        }

        // "a  •  b" without empty parts (e.g. an unknown battery)
        public static string Join(params string[] parts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (sb.Length > 0) sb.Append("  •  ");
                sb.Append(p);
            }
            return sb.ToString();
        }
    }
}
