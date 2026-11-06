using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MQVR
{
    public static class Net
    {
        public const int TcpPort = 8765;
        public const int UdpPort = 8766;
        public const float OfflineAfterSeconds = 4f;

        // "VR content" = 360 videos. Decided by file name so no extra metadata is needed.
        public static bool IsVr(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n.Contains("360") || n.StartsWith("vr_") || n.Contains("_vr");
        }

        public static string LocalIp()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string s = ua.Address.ToString();
                        if (s.StartsWith("127.") || s.StartsWith("169.254")) continue;
                        return s;
                    }
                }
            }
            catch (Exception) { }
            return "0.0.0.0";
        }
    }

    // One error/info message produced on a Quest. seq grows by 1 per event.
    [Serializable]
    public class QuestEvent
    {
        public int seq;
        public string level = "info"; // info | warning | error
        public string msg = "";
    }

    [Serializable]
    public class QuestStatus
    {
        public string app = MediSimAuth.AppId; // phone ignores beacons from other apps
        public bool muted;
        public string id = "";
        public string name = "";
        public string ip = "";
        public int port = Net.TcpPort;
        public string boot = "";            // changes every time the Quest app starts
        public string state = "idle";       // idle | loading | ready | playing | paused | ended
        public string video = "";
        public float pos;
        public float dur;
        public int battery = -1;
        public int controllers;
        public float vol = 0.7f;
        public string err = "";             // last error, cleared by the next successful action
        public string[] videos = new string[0];
        public float[] vdur = new float[0]; // seconds per video, -1 = unknown yet (same order as videos)
        public QuestEvent[] events = new QuestEvent[0];
        // v2 transfer fields
        public int fport = Xfer.Port;       // file-transfer port (separate from the control port)
        public long free = -1;              // free storage on the Quest in bytes, -1 = unknown
        public long total = -1;             // total storage in bytes, -1 = unknown
        public string model = "";           // headset model (SystemInfo.deviceModel)
        public string rx = "";              // file being received now ("" = none)
        public long rxDone;
        public long rxTotal;
        public int rxCount;                 // number of files being received
        public int[] vok = new int[0];      // per video: 1 = checksum verified, 0 = copied by hand, -1 = changed/corrupt
    }

    [Serializable]
    public class QuestCommand
    {
        public string cmd = "";   // ping load play pause resume stop seek volume mute recenter rename
        public bool mute;
        public string video = "";
        public string name = "";
        public float pos;
        public float vol;
        public bool autoplay;
    }
}
