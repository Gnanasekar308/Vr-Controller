using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MediSimVR
{
    // The Medisim PC server on the lab Wi-Fi (HTTP 5080). Found automatically (UDP broadcast "MEDISIM_DISCOVER" on 5081,
    // answer "MEDISIM_SERVER|5080") or at a manual IP (Settings > Manual IP Connect, or the Scene tab's link button).
    // Requests run on worker threads; their callbacks run on the main thread in Pump() (ServerControllerService.Tick).
    public class MedisimServer
    {
        public const int HttpPort = 5080;
        public const int DiscoveryPort = 5081;
        public const string ManualIpKey = "msvr_server_ip";
        private const string DiscoverMessage = "MEDISIM_DISCOVER";
        private const string ReplyPrefix = "MEDISIM_SERVER|";

        private static MedisimServer instance;
        public static MedisimServer Instance { get { return instance ?? (instance = new MedisimServer()); } }

        private readonly ConcurrentQueue<Action> mainQueue = new ConcurrentQueue<Action>();
        private volatile string baseUrl = "";
        private volatile string machine = "";
        private volatile string status = "Searching for the PC server...";
        private volatile string manualIp = "";
        private volatile bool running, findNow;
        private Thread finder;
        private string lastAnnounced = "";

        public string BaseUrl { get { return baseUrl; } }
        public bool HasServer { get { return baseUrl.Length > 0; } }
        public string Host { get { return HasServer ? new Uri(baseUrl).Host : ""; } }
        public string MachineName { get { return machine; } }
        public string Status { get { return status; } }
        public string ManualIp { get { return manualIp; } }

        // Main thread: the server was found, changed or lost (BaseUrl is "" when lost).
        public event Action ServerChanged;

        private MedisimServer() { }

        // Main thread. Safe to call more than once.
        public void Start()
        {
            if (running)
            {
                return;
            }

            manualIp = PlayerPrefs.GetString(ManualIpKey, "").Trim();
            running = true;
            finder = new Thread(FinderLoop) { IsBackground = true, Name = "MedisimServerFinder" };
            finder.Start();
        }

        public void Stop()
        {
            running = false;
        }

        // Main thread. "" = automatic discovery again.
        public void SetManualIp(string ip)
        {
            ip = (ip ?? "").Trim();
            PlayerPrefs.SetString(ManualIpKey, ip);
            PlayerPrefs.Save();
            manualIp = ip;
            baseUrl = "";
            findNow = true;
            Start();
        }

        // Look again now (Refresh button).
        public void Rediscover()
        {
            if (!HasServer)
            {
                findNow = true;
            }
        }

        // Main thread: runs queued callbacks. Called every frame.
        public void Pump()
        {
            if (lastAnnounced != baseUrl)
            {
                lastAnnounced = baseUrl;
                Debug.Log("[MediSimVR] " + (baseUrl.Length > 0 ? "PC server " + baseUrl + " (" + machine + ")" : "PC server lost"));
                if (ServerChanged != null) ServerChanged();
            }

            Action a;
            int n = 0;

            while (n++ < 200 && mainQueue.TryDequeue(out a))
            {
                try { a(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void Post(Action a)
        {
            mainQueue.Enqueue(a);
        }

        // JSON request (json null = no body). done runs on the main thread.
        public void Request(string method, string path, string json, Action<HttpReply> done, int timeoutMs = 5000)
        {
            string url = baseUrl;

            if (url.Length == 0)
            {
                var r = new HttpReply { Error = "PC server not found" };
                if (done != null) Post(() => done(r));
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                HttpReply r = MiniHttp.Send(method, url + path, json, timeoutMs);
                if (done != null) Post(() => done(r));
            });
        }

        // ---------------------------------------------------------------- finder (worker thread)

        private void FinderLoop()
        {
            int failures = 0;
            DateTime nextCheck = DateTime.MinValue;

            while (running)
            {
                try
                {
                    if (baseUrl.Length == 0)
                    {
                        string found = Find();

                        if (found != null)
                        {
                            baseUrl = found;
                            failures = 0;
                            nextCheck = DateTime.UtcNow.AddSeconds(5);
                            status = "Connected to " + (machine.Length > 0 ? machine : "PC server") + " (" + new Uri(found).Host + ")";
                        }
                        else
                        {
                            Sleep(2000);
                        }

                        continue;
                    }

                    if (DateTime.UtcNow >= nextCheck)
                    {
                        nextCheck = DateTime.UtcNow.AddSeconds(5);

                        if (Healthy(baseUrl))
                        {
                            failures = 0;
                        }
                        else if (++failures >= 2)
                        {
                            Debug.LogWarning("[MediSimVR] PC server " + baseUrl + " stopped answering");
                            baseUrl = "";
                            status = "PC server lost, searching again...";
                            continue;
                        }
                    }

                    Sleep(500);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MediSimVR] Server finder: " + e.Message);
                    Sleep(2000);
                }
            }
        }

        private void Sleep(int ms)
        {
            for (int t = 0; t < ms && running && !findNow; t += 100)
            {
                Thread.Sleep(100);
            }

            findNow = false;
        }

        private string Find()
        {
            string manual = manualIp;

            if (manual.Length > 0)
            {
                string url = "http://" + manual + ":" + HttpPort;
                status = "Connecting to the PC server at " + manual + "...";
                if (Healthy(url)) return url;
                status = "PC server not reachable at " + manual + " (is Start_Server running? firewall?)";
                return null;
            }

            status = "Searching for the PC server on this Wi-Fi...";

            using (var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
            {
                udp.EnableBroadcast = true;
                udp.Client.ReceiveTimeout = 500;
                byte[] ask = Encoding.ASCII.GetBytes(DiscoverMessage);

                foreach (IPAddress a in BroadcastAddresses())
                {
                    try { udp.Send(ask, ask.Length, new IPEndPoint(a, DiscoveryPort)); }
                    catch (Exception) { }
                }

                DateTime until = DateTime.UtcNow.AddSeconds(2);

                while (DateTime.UtcNow < until && running)
                {
                    try
                    {
                        var from = new IPEndPoint(IPAddress.Any, 0);
                        string text = Encoding.ASCII.GetString(udp.Receive(ref from)).Trim();
                        int port;

                        if (text.StartsWith(ReplyPrefix) && int.TryParse(text.Substring(ReplyPrefix.Length), out port))
                        {
                            string url = "http://" + from.Address + ":" + port;
                            if (Healthy(url)) return url;
                        }
                    }
                    catch (SocketException)
                    {
                        // timeout: keep waiting until 'until'
                    }
                }
            }

            status = "PC server not found on this Wi-Fi. Start it on the PC, or type its IP in Settings > Manual IP Connect.";
            return null;
        }

        // GET /health; also reads the PC name.
        private bool Healthy(string url)
        {
            HttpReply r = MiniHttp.Send("GET", url + "/health", null, 3000);

            if (!r.Ok || r.Text.IndexOf("Medisim VR Local Server", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            const string key = "\"machine\":\"";
            int i = r.Text.IndexOf(key, StringComparison.Ordinal);

            if (i >= 0)
            {
                int end = r.Text.IndexOf('"', i + key.Length);
                if (end > i) machine = r.Text.Substring(i + key.Length, end - i - key.Length);
            }

            return true;
        }

        // 255.255.255.255 + the directed broadcast of every IPv4 interface (Android does not always send the global one).
        private static List<IPAddress> BroadcastAddresses()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast };

            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        byte[] ip = ua.Address.GetAddressBytes();
                        if (ip[0] == 127 || (ip[0] == 169 && ip[1] == 254)) continue;
                        byte[] mask = null;
                        try { if (ua.IPv4Mask != null) mask = ua.IPv4Mask.GetAddressBytes(); } catch (Exception) { }
                        if (mask == null || mask.Length != 4 || (mask[0] | mask[1] | mask[2] | mask[3]) == 0) mask = new byte[] { 255, 255, 255, 0 };
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                        var a = new IPAddress(b);
                        if (!list.Contains(a)) list.Add(a);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MediSimVR] Could not read network interfaces: " + e.Message);
            }

            return list;
        }
    }
}
