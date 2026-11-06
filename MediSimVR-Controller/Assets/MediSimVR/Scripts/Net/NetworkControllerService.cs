using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MQVR;
using UnityEngine;

namespace MediSimVR
{
    // Real service for the phone UI: UDP beacon discovery (only app "MediSimVR"), authenticated TCP control link
    // per Quest (QuestLink) and SHA-256 verified video transfer (TransferManager). Phase 1: video only.
    public class NetworkControllerService : IControllerService, IDisposable
    {
        private class Dev
        {
            public QuestDevice D;
            public QuestStatus S = new QuestStatus();
            public string[] Videos = new string[0];   // files on the Quest (from control replies)
            public float LastSeen = -100f;
            public QuestLink Link;
            public bool Manual, WasConn, ErrNoted;
            public int LastEv = -1;
            public string Boot = "";
            public float NextTry;
            public int Quarter = -1;                  // last progress note (0..3 = 0/25/50/75 %)
        }

        public event Action Changed;

        private readonly List<QuestDevice> devices = new List<QuestDevice>();
        private readonly List<MediaFile> files = new List<MediaFile>();
        private readonly List<AppNotification> notes = new List<AppNotification>();
        private readonly Dictionary<string, Dev> devs = new Dictionary<string, Dev>();
        private readonly Dictionary<string, MediaSource> sources = new Dictionary<string, MediaSource>(); // by MediaFile.Id
        private readonly TransferManager tm = new TransferManager();
        private readonly ConcurrentQueue<KeyValuePair<string, string>> inbox = new ConcurrentQueue<KeyValuePair<string, string>>();
        private UdpClient udp;
        private Thread rx;
        private volatile bool running;
        private float scanLeft, now;
        private bool notesEnabled = true;
        private static int mainThread;

        public IReadOnlyList<QuestDevice> Devices { get { return devices; } }
        public IReadOnlyList<MediaFile> Files { get { return files; } }
        public IReadOnlyList<AppNotification> Notifications { get { return notes; } }
        public bool Scanning { get { return scanLeft > 0f; } }
        public string ControlFileId { get; set; }

        public MediaFile ControlFile
        {
            get
            {
                foreach (var f in files) if (f.Id == ControlFileId) return f;
                return null;
            }
        }

        public bool NotificationsEnabled
        {
            get { return notesEnabled; }
            set { notesEnabled = value; PlayerPrefs.SetInt("msvr_notes", value ? 1 : 0); Fire(); }
        }

        public int UnreadCount
        {
            get
            {
                if (!notesEnabled) return 0;
                int n = 0;
                foreach (var x in notes) if (!x.Read) n++;
                return n;
            }
        }

        public NetworkControllerService()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            notesEnabled = PlayerPrefs.GetInt("msvr_notes", 1) == 1;
            ControlFileId = "";
            StartDiscovery();
            Note("System", "Searching for Quest on this Wi-Fi...", NoteLevel.Info);
        }

        // ---------- discovery ----------
        private void StartDiscovery()
        {
            if (running) return;
            try
            {
                udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, Net.UdpPort));
                running = true;
                rx = new Thread(RxLoop);
                rx.IsBackground = true;
                rx.Start();
                Debug.Log("[MediSimVR] Discovery listening on UDP " + Net.UdpPort + ", phone IP " + Net.LocalIp());
            }
            catch (Exception e)
            {
                Debug.LogError("[MediSimVR] Discovery failed: " + e.Message);
                Note("System", "Discovery failed: " + e.Message + " (add the Quest by IP)", NoteLevel.Error);
            }
        }

        private void RxLoop()
        {
            while (running)
            {
                try
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] d = udp.Receive(ref ep);
                    inbox.Enqueue(new KeyValuePair<string, string>(ep.Address.ToString(), Encoding.UTF8.GetString(d)));
                }
                catch (Exception)
                {
                    if (!running) break;
                    Thread.Sleep(100);
                }
            }
        }

        private bool OnBeacon(string ip, string json)
        {
            QuestStatus s = null;
            try { s = JsonUtility.FromJson<QuestStatus>(json); } catch (Exception) { }
            if (s == null || s.app != MediSimAuth.AppId || string.IsNullOrEmpty(s.id)) return false; // not our app
            if (string.IsNullOrEmpty(s.ip)) s.ip = ip;

            Dev d;
            if (!devs.TryGetValue(s.id, out d))
            {
                d = TakeManual(s.ip, s.id);
                if (d == null)
                {
                    d = new Dev { D = new QuestDevice { Id = s.id } };
                    devs[s.id] = d;
                    devices.Add(d.D);
                    if (devices.Count == 1) d.D.Selected = true; // phase 1: one Quest, ready to use
                    Debug.Log("[MediSimVR] Found Quest " + s.name + " " + s.ip + " battery " + s.battery);
                    Note(s.name, "Found on " + s.ip, NoteLevel.Info);
                }
            }
            d.LastSeen = now;
            Apply(d, s, false);
            return true;
        }

        // a Quest typed by IP gets its real id once its beacon / reply arrives
        private Dev TakeManual(string ip, string id)
        {
            Dev m;
            if (!devs.TryGetValue("ip:" + ip, out m)) return null;
            devs.Remove("ip:" + ip);
            tm.Rekey(m.D.Id, id);
            m.D.Id = id;
            m.Manual = false;
            devs[id] = m;
            return m;
        }

        private void Apply(Dev d, QuestStatus s, bool full)
        {
            int fport = s.fport > 0 ? s.fport : Xfer.Port;
            d.S = s;
            d.S.fport = fport;
            d.D.Name = string.IsNullOrEmpty(s.name) ? d.D.Name : s.name;
            d.D.Ip = s.ip;
            d.D.Battery = s.battery;
            d.D.Muted = s.muted;
            d.D.State = MapState(s.state);
            d.D.FileId = FileIdFor(s.video);
            if (!full) return;

            d.Videos = s.videos ?? new string[0];
            if (s.boot != d.Boot) { d.Boot = s.boot; d.LastEv = -1; }
            int max = d.LastEv;
            foreach (var e in s.events ?? new QuestEvent[0])
            {
                if (e.seq <= d.LastEv) continue;
                if (d.LastEv >= 0 || e.level != "info") // first contact: old info events are not repeated
                    Note(d.D.Name, e.msg, e.level == "error" ? NoteLevel.Error : e.level == "warning" ? NoteLevel.Warning : NoteLevel.Info);
                if (e.seq > max) max = e.seq;
            }
            d.LastEv = Math.Max(max, 0);
        }

        private static DeviceState MapState(string st)
        {
            switch (st)
            {
                case "playing":
                case "loading": return DeviceState.Playing;
                case "paused":
                case "ready": return DeviceState.Paused;
                case "ended": return DeviceState.Stopped;
                default: return DeviceState.Idle;
            }
        }

        private string FileIdFor(string video)
        {
            if (string.IsNullOrEmpty(video)) return "";
            foreach (var f in files) if (Xfer.SafeName(f.Name) == video) return f.Id;
            return "";
        }

        // ---------- per-frame ----------
        public void Tick(float dt)
        {
            now = Time.realtimeSinceStartup;
            bool changed = false;

            KeyValuePair<string, string> kv;
            int n = 0;
            while (n++ < 200 && inbox.TryDequeue(out kv)) changed |= OnBeacon(kv.Key, kv.Value);

            foreach (var d in new List<Dev>(devs.Values))
            {
                try { changed |= UpdateDev(d); }
                catch (Exception e) { Debug.LogError("[MediSimVR] UpdateDev " + d.D.Id + ": " + e); }
            }

            List<SendJob> fin = null;
            try { fin = tm.Tick(); }
            catch (Exception e) { Debug.LogError("[MediSimVR] Transfer tick: " + e); }
            if (fin != null) foreach (var j in fin) { OnFinished(j); changed = true; }

            if (scanLeft > 0f && (scanLeft -= dt) <= 0f)
            {
                int on = 0;
                foreach (var d in devices) if (d.Online) on++;
                Note("System", "Scan finished: " + on + " online", NoteLevel.Info);
                changed = true;
            }
            if (changed) Fire();
        }

        private bool UpdateDev(Dev d)
        {
            bool changed = false;
            if (d.Link != null)
            {
                string rep = d.Link.TakeReply();
                if (rep != null)
                {
                    QuestStatus s = null;
                    try { s = JsonUtility.FromJson<QuestStatus>(rep); } catch (Exception) { }
                    if (s != null && !string.IsNullOrEmpty(s.id))
                    {
                        if (d.Manual)
                        {
                            if (devs.ContainsKey(s.id)) // same Quest already found by beacon: drop the typed duplicate
                            {
                                d.Link.Disconnect();
                                devs.Remove(d.D.Id);
                                devices.Remove(d.D);
                                return true;
                            }
                            TakeManual(d.D.Ip, s.id);
                        }
                        if (string.IsNullOrEmpty(s.ip)) s.ip = d.D.Ip;
                        d.LastSeen = now;
                        Apply(d, s, true);
                        changed = true;
                    }
                }
            }

            bool conn = d.Link != null && d.Link.IsConnected;
            bool online = now - d.LastSeen < Net.OfflineAfterSeconds || conn;
            if (online != d.D.Online || conn != d.D.Connected) changed = true;
            d.D.Online = online;
            d.D.Connected = conn;

            if (conn && !d.WasConn)
            {
                d.ErrNoted = false;
                Debug.Log("[MediSimVR] Connected to " + d.D.Name + " " + d.D.Ip);
                Note(d.D.Name, "Connected", NoteLevel.Success);
            }
            else if (!conn && d.WasConn)
            {
                Note(d.D.Name, "Disconnected" + (d.Link.Error.Length > 0 ? ": " + d.Link.Error : ""), NoteLevel.Warning);
            }
            d.WasConn = conn;

            // connect automatically to every MediSimVR Quest we can see
            bool busy = d.Link != null && d.Link.IsConnecting;
            if (!conn && !busy && (online || d.Manual) && d.D.Ip.Length > 0 && now >= d.NextTry)
            {
                if (d.Link != null && d.Link.Error.Length > 0 && !d.ErrNoted)
                {
                    d.ErrNoted = true;
                    Debug.LogWarning("[MediSimVR] Link " + d.D.Ip + ": " + d.Link.Error);
                    Note(d.D.Name, "Cannot connect: " + d.Link.Error, NoteLevel.Error);
                    changed = true;
                }
                d.Link = new QuestLink(d.D.Ip, d.S.port > 0 ? d.S.port : Net.TcpPort);
                d.Link.Connect();
                d.NextTry = now + 3f;
            }

            // transfer progress as notifications (the UI has no progress bar)
            SendJob j = tm.Current(d.D.Id);
            if (j != null && j.State == JobState.Sending)
            {
                int q = Mathf.Clamp((int)(j.Fraction * 4f), 0, 3);
                if (q > d.Quarter)
                {
                    d.Quarter = q;
                    Debug.Log("[MediSimVR] Sending " + j.Src.Name + " " + (q * 25) + "% (" + Fmt.Size(j.SpeedBps) + "/s)");
                    if (q > 0) { Note(d.D.Name, "Sending " + j.Src.Name + " " + (q * 25) + "%", NoteLevel.Info); changed = true; }
                }
            }
            return changed;
        }

        private void OnFinished(SendJob j)
        {
            Dev d;
            devs.TryGetValue(j.DevKey, out d);
            string who = d != null ? d.D.Name : j.DevKey;
            if (d != null) d.Quarter = -1;
            if (j.State == JobState.Done)
            {
                string msg = j.Skipped ? j.Src.Name + " already on Quest (verified)" : "Received " + j.Src.Name + ", SHA-256 verified";
                Debug.Log("[MediSimVR] Transfer done: " + msg);
                Note(who, msg, NoteLevel.Success);
                if (j.PlayAfter && d != null)
                    SendCmd(d, new QuestCommand { cmd = "load", video = j.Src.Name, autoplay = true });
            }
            else if (j.State == JobState.Failed)
            {
                Debug.LogError("[MediSimVR] Transfer failed: " + j.Src.Name + ": " + j.Error);
                Note(who, "Transfer of " + j.Src.Name + " failed: " + j.Error, NoteLevel.Error);
            }
            else Note(who, "Transfer of " + j.Src.Name + " cancelled", NoteLevel.Warning);
        }

        // ---------- IControllerService ----------
        private void Fire() { if (Changed != null) Changed(); }

        private void Note(string dev, string msg, NoteLevel lvl)
        {
            notes.Insert(0, new AppNotification { Time = DateTime.Now, Device = dev, Message = msg, Level = lvl });
            while (notes.Count > 100) notes.RemoveAt(notes.Count - 1);
        }

        private Dev Find(QuestDevice q)
        {
            Dev d;
            return q != null && devs.TryGetValue(q.Id, out d) ? d : null;
        }

        public List<QuestDevice> SelectedOnline()
        {
            var l = new List<QuestDevice>();
            foreach (var d in devices) if (d.Selected && d.Online) l.Add(d);
            return l;
        }

        public void Refresh()
        {
            if (Scanning) return;
            if (!running) StartDiscovery();
            foreach (var d in devs.Values) d.NextTry = 0f;
            scanLeft = 2f;
            Fire();
        }

        public void Connect(List<QuestDevice> list)
        {
            foreach (var q in list)
            {
                var d = Find(q);
                if (d != null) { d.NextTry = 0f; d.ErrNoted = false; }
            }
            Fire();
        }

        public void AddManualDevice(string ip)
        {
            ip = (ip ?? "").Trim();
            IPAddress a;
            if (!IPAddress.TryParse(ip, out a)) { Note("System", "Not a valid IP: " + ip, NoteLevel.Warning); Fire(); return; }
            foreach (var x in devices) if (x.Ip == ip) return;
            var d = new Dev { Manual = true, D = new QuestDevice { Id = "ip:" + ip, Name = "Quest (" + ip + ")", Ip = ip } };
            d.S.ip = ip;
            devs[d.D.Id] = d;
            devices.Add(d.D);
            Note("System", "Added " + ip + ", connecting...", NoteLevel.Info);
            Fire();
        }

        public void AddFile(MediaFile f)
        {
            files.Insert(0, f);
            ControlFileId = f.Id;
            Note("Phone", "Added " + MediaTypes.Label(f.Kind).ToLower() + " " + f.Name, NoteLevel.Info);
            Fire();
        }

        public void UpdateFile(MediaFile f)
        {
            if (f.Kind == MediaKind.Text) f.SizeBytes = Encoding.UTF8.GetByteCount(f.Text ?? "");
            Fire();
        }

        public void RemoveFile(MediaFile f)
        {
            files.Remove(f);
            sources.Remove(f.Id);
            if (ControlFileId == f.Id) ControlFileId = files.Count > 0 ? files[0].Id : "";
            Fire();
        }

        public void Send(MediaFile f)
        {
            if (f.Kind != MediaKind.Video) { Note("System", MediaTypes.Label(f.Kind) + " sending comes in a later version (video only now)", NoteLevel.Warning); Fire(); return; }
            var list = SelectedOnline();
            if (list.Count == 0) { Note("System", "No online device selected", NoteLevel.Warning); Fire(); return; }
            ControlFileId = f.Id;
            MediaSource src = SourceFor(f);
            foreach (var q in list)
            {
                var d = Find(q);
                if (d == null) continue;
                if (d.Link == null || !d.Link.IsConnected) { Note(q.Name, "Cannot send " + f.Name + ": not connected yet", NoteLevel.Error); continue; }
                Dev dev = d;
                tm.Add(d.D.Id, () => dev.D.Ip, d.S.fport, src, true);
                d.Quarter = -1;
                Debug.Log("[MediSimVR] Send " + src.Name + " (" + src.Size + " bytes) to " + d.D.Name + " " + d.D.Ip + ":" + d.S.fport);
                Note(q.Name, "Preparing " + f.Name + " (" + Fmt.Size(f.SizeBytes) + "), checking SHA-256...", NoteLevel.Info);
            }
            Fire();
        }

        private MediaSource SourceFor(MediaFile f)
        {
            MediaSource m;
            if (sources.TryGetValue(f.Id, out m)) return m;
            string path = f.Source;
            m = new MediaSource { Id = path, Name = Xfer.SafeName(f.Name), Size = f.SizeBytes, Open = () => OpenSource(path) };
            sources[f.Id] = m;
            return m;
        }

        // Called from hash / transfer threads: each call opens a new file descriptor (MQGallery.openFd).
        private static Stream OpenSource(string uri)
        {
            if (!uri.StartsWith("content:")) return new FileStream(uri, FileMode.Open, FileAccess.Read, FileShare.Read, Xfer.Buf);
#if UNITY_ANDROID && !UNITY_EDITOR
            bool worker = Thread.CurrentThread.ManagedThreadId != mainThread;
            if (worker) AndroidJNI.AttachCurrentThread();
            int fd;
            try
            {
                using (var c = new AndroidJavaClass("com.highbrow.mq.MQGallery")) fd = c.CallStatic<int>("openFd", uri);
            }
            finally
            {
                if (worker) AndroidJNI.DetachCurrentThread();
            }
            if (fd < 0) throw new IOException("video is no longer accessible (deleted or permission removed)");
            var h = new Microsoft.Win32.SafeHandles.SafeFileHandle(new IntPtr(fd), true);
            return new FileStream(h, FileAccess.Read, Xfer.Buf);
#else
            throw new IOException("content:// videos can only be read on Android");
#endif
        }

        private bool SendCmd(Dev d, QuestCommand c)
        {
            if (d.Link != null && d.Link.Send(JsonUtility.ToJson(c))) return true;
            Note(d.D.Name, "Command '" + c.cmd + "' not sent: not connected", NoteLevel.Error);
            return false;
        }

        private void ForSelected(Func<Dev, QuestCommand> make)
        {
            var list = SelectedOnline();
            if (list.Count == 0) { Note("System", "No online device selected", NoteLevel.Warning); Fire(); return; }
            foreach (var q in list)
            {
                var d = Find(q);
                if (d == null) continue;
                var c = make(d);
                if (c != null) SendCmd(d, c);
            }
            Fire(); // the real state comes back in the Quest's reply
        }

        public void Play()
        {
            var f = ControlFile;
            if (f == null) { Note("System", "Choose a file first", NoteLevel.Warning); Fire(); return; }
            string name = Xfer.SafeName(f.Name);
            ForSelected(d =>
            {
                if (d.S.video == name && d.S.state != "idle") return new QuestCommand { cmd = "play" };
                if (Array.IndexOf(d.Videos, name) < 0) { Note(d.D.Name, name + " is not on this Quest yet, tap Send first", NoteLevel.Warning); return null; }
                return new QuestCommand { cmd = "load", video = name, autoplay = true };
            });
        }

        public void Pause() { ForSelected(d => new QuestCommand { cmd = "pause" }); }
        public void Resume() { ForSelected(d => new QuestCommand { cmd = "resume" }); }
        public void Stop() { ForSelected(d => new QuestCommand { cmd = "stop" }); }
        public void SetMute(bool mute) { ForSelected(d => new QuestCommand { cmd = "mute", mute = mute }); }

        public void ShowText() { Note("System", "Image / text on Quest comes in a later version", NoteLevel.Info); Fire(); }
        public void HideText() { Note("System", "Image / text on Quest comes in a later version", NoteLevel.Info); Fire(); }

        public void MarkAllRead()
        {
            foreach (var x in notes) x.Read = true;
            Fire();
        }

        public void ClearNotifications()
        {
            notes.Clear();
            Fire();
        }

        public void Dispose()
        {
            running = false;
            try { if (udp != null) udp.Close(); } catch (Exception) { }
            tm.Shutdown();
            foreach (var d in devs.Values) if (d.Link != null) d.Link.Disconnect();
        }
    }
}
