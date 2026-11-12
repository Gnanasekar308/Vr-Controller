using UnityEngine;

namespace MediSimVR
{
    public enum QuestState { Online, Offline, InUse }

    // A Quest's status, the same in every list: Online (green), Offline (grey), In Use (blue: it plays or shows something).
    public static class DeviceStatus
    {
        public static readonly Color OnlineColor = new Color32(0x22, 0xC5, 0x5E, 255);
        public static readonly Color OfflineColor = new Color32(0x9C, 0xA3, 0xAF, 255);
        public static readonly Color InUseColor = new Color32(0x25, 0x63, 0xEB, 255);

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

        public static Color Tint(QuestState s)
        {
            return s == QuestState.InUse ? InUseColor : s == QuestState.Online ? OnlineColor : OfflineColor;
        }

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

        public static string Battery(QuestDevice d)
        {
            return d.Online && d.Battery >= 0 ? d.Battery + "% battery" : "battery --";
        }
    }
}
