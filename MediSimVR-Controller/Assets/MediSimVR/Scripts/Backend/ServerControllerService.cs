using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MQVR;
using UnityEngine;

namespace MediSimVR
{
    // Real IControllerService through the Medisim PC server (demo mode OFF, MedisimBackend.UseServer):
    //   Devices  = the student stations registered on the server (one PC + Quest over Link each), polled every second
    //   Send     = upload the file to the server (SHA-256 checked, skipped if already there), then "load" on the stations
    //   Play ... = station commands queued on the server (/api/station/commands)
    // The UI does not change: same interface as NetworkControllerService.
    public class ServerControllerService : IControllerService, IDisposable
    {
        private class Station
        {
            public QuestDevice D;
            public StationStatusDto S = new StationStatusDto();
            public int LastEvent = -1;
            public bool WasOnline;
        }

        private class Upload
        {
            public MediaFile File;
            public List<string> Targets;
            public volatile int Quarter = -1;  // 0..3 = 0 / 25 / 50 / 75 % sent
            public volatile bool Cancel;
        }

        public event Action Changed;

        private const float PollSeconds = 1f;

        private readonly MedisimServer server = MedisimServer.Instance;
        private readonly List<QuestDevice> devices = new List<QuestDevice>();
        private readonly List<MediaFile> files = new List<MediaFile>();
        private readonly List<AppNotification> notes = new List<AppNotification>();
        private readonly Dictionary<string, Station> stations = new Dictionary<string, Station>();
        private readonly HashSet<string> onServer = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // media names
        private readonly Dictionary<string, Upload> uploads = new Dictionary<string, Upload>();            // by file id
        private readonly Dictionary<string, string> hashes = new Dictionary<string, string>();             // file id -> sha256
        private readonly Dictionary<string, int> progressShown = new Dictionary<string, int>();             // file id -> quarter
        private readonly Queue<StationCommandRequest> commandQueue = new Queue<StationCommandRequest>();
        private bool commandBusy;
        private float nextPoll, scanLeft;
        private bool polling, notesEnabled = true, hadServer;
        private string lastStatus = "";
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

        public ServerControllerService()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            notesEnabled = PlayerPrefs.GetInt("msvr_notes", 1) == 1;
            ControlFileId = "";
            server.ServerChanged += OnServerChanged;
            server.Start();
            Note("System", "Searching for the Medisim PC server on this Wi-Fi...", NoteLevel.Info);
        }

        // ---------------------------------------------------------------- per frame

        public void Tick(float dt)
        {
            server.Pump();
            bool changed = false;

            if (server.Status != lastStatus)
            {
                lastStatus = server.Status;
                Debug.Log("[MediSimVR] " + lastStatus);
            }

            if (!polling && server.HasServer && Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + PollSeconds;
                Poll();
            }

            // upload progress as notifications (the UI has no progress bar)
            foreach (var u in uploads.Values)
            {
                int shown;
                progressShown.TryGetValue(u.File.Id, out shown);
                int q = u.Quarter;

                if (q > shown)
                {
                    progressShown[u.File.Id] = q;
                    Note("Phone", "Uploading " + u.File.Name + " " + (q * 25) + "%", NoteLevel.Info);
                    changed = true;
                }
            }

            if (scanLeft > 0f && (scanLeft -= dt) <= 0f)
            {
                int on = 0;
                foreach (var d in devices) if (d.Online) on++;
                Note("System", server.HasServer ? "Scan finished: " + on + " station(s) online" : server.Status, NoteLevel.Info);
                changed = true;
            }

            if (changed) Fire();
        }

        private void OnServerChanged()
        {
            if (server.HasServer)
            {
                hadServer = true;
                Note("System", "Connected to the PC server " + (server.MachineName.Length > 0 ? server.MachineName : "") + " (" + server.Host + ")", NoteLevel.Success);
                nextPoll = 0f;
                RefreshServerFiles();
            }
            else
            {
                if (hadServer) Note("System", "PC server lost: searching again...", NoteLevel.Warning);
                foreach (var s in stations.Values) { s.D.Online = false; s.D.Connected = false; }
            }

            Fire();
        }

        private void Poll()
        {
            polling = true;
            server.Request("GET", "/api/devices", null, r =>
            {
                polling = false;

                if (!r.Ok)
                {
                    return; // the finder notices a lost server
                }

                StationList list = null;

                try { list = JsonUtility.FromJson<StationList>("{\"items\":" + r.Text + "}"); }
                catch (Exception e) { Debug.LogWarning("[MediSimVR] Bad device list: " + e.Message); }

                if (list != null && list.items != null && Apply(list.items)) Fire();
            });
        }

        private bool Apply(StationDto[] list)
        {
            bool changed = false;
            var seen = new HashSet<string>();

            foreach (var dto in list)
            {
                if (dto == null || string.IsNullOrEmpty(dto.deviceId)) continue;
                seen.Add(dto.deviceId);
                Station st;

                if (!stations.TryGetValue(dto.deviceId, out st))
                {
                    st = new Station { D = new QuestDevice { Id = dto.deviceId } };
                    stations[dto.deviceId] = st;
                    devices.Add(st.D);
                    if (devices.Count == 1) st.D.Selected = true; // one station: ready to use
                    Note(dto.deviceName, "Found station" + (string.IsNullOrEmpty(dto.ip) ? "" : " on " + dto.ip), NoteLevel.Info);
                    ClassroomStations.Bind(dto.deviceId, dto.deviceName);   // the station takes a seat in the class (meetings, students)
                    changed = true;
                }

                var s = dto.status ?? new StationStatusDto();
                var d = st.D;
                DeviceState state = MapState(s);
                string fileId = FileIdFor(s.media);

                if (d.Name != dto.deviceName || d.Online != dto.isOnline || d.State != state || d.FileId != fileId ||
                    d.Muted != s.muted || d.Battery != s.battery || Math.Abs(d.PositionSec - s.pos) > 0.01f || d.Ip != dto.ip)
                {
                    changed = true;
                }

                d.Name = string.IsNullOrEmpty(dto.deviceName) ? d.Id : dto.deviceName;
                d.Ip = dto.ip ?? "";
                d.Online = dto.isOnline;
                d.Connected = dto.isOnline;
                d.Battery = s.battery;
                d.Muted = s.muted;
                d.State = state;
                d.FileId = fileId;
                d.PositionSec = s.pos;
                st.S = s;

                if (d.Online != st.WasOnline)
                {
                    Note(d.Name, d.Online ? "Online" + (s.headset ? "" : " (headset not on Link yet)") : "Offline", d.Online ? NoteLevel.Success : NoteLevel.Warning);
                    st.WasOnline = d.Online;
                }

                if (s.eventSeq != st.LastEvent)
                {
                    if (st.LastEvent >= 0 && !string.IsNullOrEmpty(s.eventText))
                    {
                        Note(d.Name, s.eventText, s.eventLevel == "error" ? NoteLevel.Error : s.eventLevel == "warning" ? NoteLevel.Warning :
                            s.eventLevel == "success" ? NoteLevel.Success : NoteLevel.Info);
                    }

                    st.LastEvent = s.eventSeq;
                    changed = true;
                }

                if (s.files != null) foreach (var name in s.files) onServer.Add(name); // a station has it, so the server had it
            }

            foreach (var st in stations.Values)
            {
                if (!seen.Contains(st.D.Id) && st.D.Online)
                {
                    st.D.Online = st.D.Connected = false; // the server restarted and forgot it
                    changed = true;
                }
            }

            return changed;
        }

        private static DeviceState MapState(StationStatusDto s)
        {
            switch (s.state)
            {
                case "playing":
                case "loading": return DeviceState.Playing;
                case "paused":
                case "ready": return DeviceState.Paused;
                case "ended": return DeviceState.Stopped;
                default: return string.IsNullOrEmpty(s.text) ? DeviceState.Idle : DeviceState.ShowingText;
            }
        }

        private string FileIdFor(string media)
        {
            if (string.IsNullOrEmpty(media)) return "";
            foreach (var f in files) if (Xfer.SafeName(f.Name) == media) return f.Id;
            return "";
        }

        private void RefreshServerFiles()
        {
            server.Request("GET", "/api/media", null, r =>
            {
                if (!r.Ok) return;
                MediaList list = null;
                try { list = JsonUtility.FromJson<MediaList>(r.Text); } catch (Exception) { }
                if (list != null && list.files != null) foreach (var f in list.files) onServer.Add(f.name);
            });
        }

        // ---------------------------------------------------------------- IControllerService

        private void Fire() { if (Changed != null) Changed(); }

        private void Note(string dev, string msg, NoteLevel lvl)
        {
            notes.Insert(0, new AppNotification { Time = DateTime.Now, Device = dev ?? "", Message = msg, Level = lvl });
            while (notes.Count > 100) notes.RemoveAt(notes.Count - 1);
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
            server.Rediscover();
            nextPoll = 0f;
            scanLeft = 2f;
            Fire();
        }

        // Stations are always connected through the server; this only confirms the choice.
        public void Connect(List<QuestDevice> list)
        {
            foreach (var q in list) Note(q.Name, "Selected", NoteLevel.Success);
            Fire();
        }

        // Settings > Manual IP Connect: the IP of the PC that runs the Medisim server.
        public void AddManualDevice(string ip)
        {
            ip = (ip ?? "").Trim();
            IPAddressCheck(ip);
        }

        private void IPAddressCheck(string ip)
        {
            System.Net.IPAddress a;

            if (ip.Length > 0 && (!System.Net.IPAddress.TryParse(ip, out a) || ip.Split('.').Length != 4))
            {
                Note("System", "Not a valid IP: " + ip, NoteLevel.Warning);
                Fire();
                return;
            }

            server.SetManualIp(ip);
            Note("System", ip.Length > 0 ? "Using the PC server at " + ip : "Searching for the PC server automatically", NoteLevel.Info);
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
            Upload u;
            if (uploads.TryGetValue(f.Id, out u)) u.Cancel = true;
            files.Remove(f);
            hashes.Remove(f.Id);
            if (ControlFileId == f.Id) ControlFileId = files.Count > 0 ? files[0].Id : "";
            Fire();
        }

        public void Send(MediaFile f)
        {
            var list = SelectedOnline();
            if (list.Count == 0) { Note("System", "No online station selected", NoteLevel.Warning); Fire(); return; }
            ControlFileId = f.Id;

            if (f.Kind == MediaKind.Text)
            {
                Command(list, new StationCommandRequest { cmd = "showtext", text = f.Text ?? "", title = Path.GetFileNameWithoutExtension(f.Name) });
                return;
            }

            if (uploads.ContainsKey(f.Id))
            {
                Note("Phone", f.Name + " is still uploading; it starts on the stations when done", NoteLevel.Info);
                Fire();
                return;
            }

            var targets = new List<string>();
            foreach (var d in list) targets.Add(d.Id);
            StartUpload(f, targets);
        }

        private void StartUpload(MediaFile f, List<string> targets)
        {
            if (!server.HasServer) { Note("System", "Cannot send: " + server.Status, NoteLevel.Error); Fire(); return; }

            var job = new Upload { File = f, Targets = targets };
            uploads[f.Id] = job;
            progressShown[f.Id] = 0;
            string name = Xfer.SafeName(f.Name);
            string url = server.BaseUrl;
            string source = f.Source;
            string knownSha;
            hashes.TryGetValue(f.Id, out knownSha);
            Note("Phone", "Preparing " + f.Name + " (" + Fmt.Size(f.SizeBytes) + "), checking SHA-256...", NoteLevel.Info);
            Fire();

            var worker = new Thread(() =>
            {
                string error = null, sha = knownSha;
                bool skipped = false;

                try
                {
                    long size;

                    if (string.IsNullOrEmpty(sha))
                    {
                        using (Stream s = OpenSource(source)) sha = Hash(s, out size, () => job.Cancel);
                    }

                    HttpReply info = MiniHttp.Send("GET", url + "/api/media/" + Uri.EscapeDataString(name) + "/info", null, 5000);
                    skipped = info.Ok && info.Text.IndexOf(sha, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!skipped && !job.Cancel)
                    {
                        using (Stream s = OpenSource(source))
                        {
                            long length = s.CanSeek ? s.Length : job.File.SizeBytes;
                            HttpReply put = MiniHttp.Send("PUT", url + "/api/media/" + Uri.EscapeDataString(name) + "?sha256=" + sha,
                                "application/octet-stream", s, length, 20000,
                                sent => job.Quarter = length > 0 ? (int)Math.Min(3, sent * 4 / length) : 0,
                                () => job.Cancel);
                            if (!put.Ok) error = put.Error.Length > 0 ? put.Error : ErrorText(put);
                        }
                    }
                }
                catch (Exception e)
                {
                    error = e.Message;
                }

                server.Post(() => OnUploaded(job, name, sha, skipped, error));
            }) { IsBackground = true, Name = "MedisimUpload" };
            worker.Start();
        }

        private void OnUploaded(Upload job, string name, string sha, bool skipped, string error)
        {
            uploads.Remove(job.File.Id);
            progressShown.Remove(job.File.Id);

            if (job.Cancel) { Note("Phone", "Upload of " + job.File.Name + " cancelled", NoteLevel.Warning); Fire(); return; }

            if (error != null)
            {
                Debug.LogError("[MediSimVR] Upload of " + job.File.Name + " failed: " + error);
                Note("Phone", "Upload of " + job.File.Name + " failed: " + error, NoteLevel.Error);
                Fire();
                return;
            }

            hashes[job.File.Id] = sha;
            onServer.Add(name);
            Note("Phone", skipped ? job.File.Name + " already on the PC server (verified)" : "Uploaded " + job.File.Name + ", SHA-256 verified", NoteLevel.Success);
            Command(job.Targets, new StationCommandRequest { cmd = "load", media = name, autoplay = true });
        }

        public void Play()
        {
            var f = ControlFile;
            if (f == null) { Note("System", "Choose a file first", NoteLevel.Warning); Fire(); return; }

            if (f.Kind == MediaKind.Text) { ShowText(); return; }

            string name = Xfer.SafeName(f.Name);

            if (!onServer.Contains(name))
            {
                Note("System", f.Name + " is not on the PC server yet, tap Send first", NoteLevel.Warning);
                Fire();
                return;
            }

            // "play" with the file: a station that has it loaded resumes, the others load it and start
            Command(SelectedOnline(), new StationCommandRequest { cmd = "play", media = name });
        }

        public void Pause() { Command(SelectedOnline(), new StationCommandRequest { cmd = "pause" }); }
        public void Resume() { Command(SelectedOnline(), new StationCommandRequest { cmd = "resume" }); }

        public void Stop()
        {
            var f = ControlFile;
            Command(SelectedOnline(), new StationCommandRequest { cmd = f != null && f.Kind == MediaKind.Text ? "hidetext" : "stop" });
        }

        public void SetMute(bool mute) { Command(SelectedOnline(), new StationCommandRequest { cmd = "mute", mute = mute }); }

        public void ShowText()
        {
            var f = ControlFile;
            if (f == null || f.Kind != MediaKind.Text) { Note("System", "Choose a text file first", NoteLevel.Warning); Fire(); return; }
            Command(SelectedOnline(), new StationCommandRequest { cmd = "showtext", text = f.Text ?? "", title = Path.GetFileNameWithoutExtension(f.Name) });
        }

        public void HideText() { Command(SelectedOnline(), new StationCommandRequest { cmd = "hidetext" }); }

        // Sends one station command to the given stations (QuestDevice list or ids).
        public void Command(List<QuestDevice> list, StationCommandRequest c)
        {
            var ids = new List<string>();
            foreach (var d in list) ids.Add(d.Id);
            Command(ids, c);
        }

        // Commands go to the server one at a time, in the order they were given (pause, then mute, then play ...).
        public void Command(List<string> ids, StationCommandRequest c)
        {
            if (ids.Count == 0) { Note("System", "No online station selected", NoteLevel.Warning); Fire(); return; }
            c.targetDeviceIds = ids.ToArray();
            commandQueue.Enqueue(c);
            SendNextCommand();
        }

        private void SendNextCommand()
        {
            if (commandBusy || commandQueue.Count == 0) return;
            StationCommandRequest c = commandQueue.Dequeue();
            string cmd = c.cmd;
            commandBusy = true;

            server.Request("POST", "/api/station/commands", JsonUtility.ToJson(c), r =>
            {
                commandBusy = false;
                SendNextCommand();

                if (!r.Ok)
                {
                    Note("System", "Command '" + cmd + "' failed: " + ErrorText(r), NoteLevel.Error);
                    Fire();
                    return;
                }

                CommandResults res = null;
                try { res = JsonUtility.FromJson<CommandResults>(r.Text); } catch (Exception) { }

                if (res != null && res.results != null)
                {
                    foreach (var x in res.results)
                    {
                        if (x.status == "queued") continue;
                        Station st;
                        string who = stations.TryGetValue(x.deviceId, out st) ? st.D.Name : x.deviceId;
                        Note(who, "Command '" + cmd + "' not sent: " + x.error, NoteLevel.Error);
                    }
                }

                nextPoll = 0f; // show the new state soon
                Fire();
            });
        }

        // The status the station last reported (null if unknown).
        public StationStatusDto StatusOf(string deviceId)
        {
            Station st;
            return stations.TryGetValue(deviceId, out st) ? st.S : null;
        }

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
            server.ServerChanged -= OnServerChanged;
            foreach (var u in uploads.Values) u.Cancel = true;
        }

        // ---------------------------------------------------------------- helpers

        private static string ErrorText(HttpReply r)
        {
            if (r.Error.Length > 0) return r.Error;
            const string key = "\"error\":\"";
            int i = r.Text.IndexOf(key, StringComparison.Ordinal);
            if (i >= 0)
            {
                int end = r.Text.IndexOf('"', i + key.Length);
                if (end > i) return r.Text.Substring(i + key.Length, end - i - key.Length);
            }
            return "HTTP " + r.Code;
        }

        private static string Hash(Stream s, out long size, Func<bool> cancelled)
        {
            using (var sha = SHA256.Create())
            {
                byte[] buf = new byte[Xfer.Buf];
                int n;
                size = 0;

                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancelled()) throw new OperationCanceledException("cancelled");
                    sha.TransformBlock(buf, 0, n, null, 0);
                    size += n;
                }

                sha.TransformFinalBlock(buf, 0, 0);
                return Xfer.Hex(sha.Hash);
            }
        }

        // Same as NetworkControllerService: a file path, or an Android content:// uri opened via MQGallery.openFd.
        private static Stream OpenSource(string uri)
        {
            if (string.IsNullOrEmpty(uri)) throw new IOException("the file has no source");
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
            if (fd < 0) throw new IOException("the file is no longer accessible (deleted or permission removed)");
            var h = new Microsoft.Win32.SafeHandles.SafeFileHandle(new IntPtr(fd), true);
            return new FileStream(h, FileAccess.Read, Xfer.Buf);
#else
            throw new IOException("content:// files can only be read on Android");
#endif
        }
    }
}
