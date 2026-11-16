using System;
using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    // Fake data + fake delays so the whole UI can be used without any Quest. No networking here.
    public class MockControllerService : IControllerService
    {
        public event Action Changed;

        private readonly List<QuestDevice> devices = new List<QuestDevice>();
        private readonly List<MediaFile> files = new List<MediaFile>();
        private readonly List<AppNotification> notes = new List<AppNotification>();
        private readonly List<QuestDevice> connecting = new List<QuestDevice>();
        private float scanLeft, connectLeft, sendLeft;
        private MediaFile sending;
        private bool notesEnabled = true;

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

        // Quests, files and texts come from DemoConfig (demo mode)
        public MockControllerService()
        {
            notesEnabled = PlayerPrefs.GetInt("msvr_notes", 1) == 1;
            foreach (var q in DemoConfig.Quests)
                devices.Add(new QuestDevice
                {
                    Id = q.Id, Name = q.Name, Ip = q.Ip, Online = q.Online, Connected = q.Online, Battery = q.Battery,
                    State = q.State, FileId = q.FileId ?? "", Muted = q.Muted, Selected = false,
                    PositionSec = q.PositionSec
                });
            foreach (var f in DemoConfig.Files) Add(f.Id, f.Name, f.Kind, f.Size, f.Seconds, f.Top, f.Bottom, f.Text ?? "", f.Media);
            ControlFileId = DemoConfig.Files.Length > 0 ? DemoConfig.Files[0].Id : "";
            Note("System", "Demo mode: sample data, no network", NoteLevel.Info);
        }

        // demo session changes devices (battery, leave / join): tell the screens
        public void NotifyChanged() { Fire(); }

        private void Add(string id, string name, MediaKind k, long size, int sec, string top, string bottom, string text, string media)
        {
            var f = new MediaFile { Id = id, Name = name, Kind = k, SizeBytes = size, DurationSec = sec, Text = text,
                                    Source = string.IsNullOrEmpty(media) ? "mock" : "streaming:" + media };   // sample in StreamingAssets
            if (k == MediaKind.Text) f.SizeBytes = System.Text.Encoding.UTF8.GetByteCount(text);
            ColorUtility.TryParseHtmlString(top, out f.ThumbTop);
            ColorUtility.TryParseHtmlString(bottom, out f.ThumbBottom);
            files.Add(f);
        }

        private void Fire() { if (Changed != null) Changed(); }

        private void Note(string dev, string msg, NoteLevel lvl)
        {
            notes.Insert(0, new AppNotification { Time = DateTime.Now, Device = dev, Message = msg, Level = lvl });
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
            scanLeft = 1.5f;
            Fire();
        }

        public void Connect(List<QuestDevice> list)
        {
            connecting.Clear();
            foreach (var d in list) if (d.Online && !d.Connected) connecting.Add(d);
            if (connecting.Count > 0) connectLeft = 1.0f;
            Fire();
        }

        public void AddManualDevice(string ip)
        {
            ip = (ip ?? "").Trim();
            if (ip.Length == 0) return;
            foreach (var d in devices) if (d.Ip == ip) return;
            devices.Add(new QuestDevice { Id = "m" + devices.Count, Name = "Quest (" + ip + ")", Ip = ip, Online = false });
            Note("System", "Added " + ip + " (mock: offline)", NoteLevel.Info);
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
            if (f.Kind == MediaKind.Text) f.SizeBytes = System.Text.Encoding.UTF8.GetByteCount(f.Text ?? "");
            Fire();
        }

        public void RemoveFile(MediaFile f)
        {
            files.Remove(f);
            if (ControlFileId == f.Id) ControlFileId = files.Count > 0 ? files[0].Id : "";
            Fire();
        }

        public void Send(MediaFile f)
        {
            var list = SelectedOnline();
            if (list.Count == 0) { Note("System", "No online device selected", NoteLevel.Warning); Fire(); return; }
            ControlFileId = f.Id;
            sending = f;
            sendLeft = 1.2f; // fake transfer time
            foreach (var d in list) Note(d.Name, "Sending " + f.Name + "...", NoteLevel.Info);
            Fire();
        }

        private void FinishSend()
        {
            foreach (var d in SelectedOnline())
            {
                d.FileId = sending.Id;
                d.PositionSec = 0f;
                bool visual = sending.Kind == MediaKind.Text || sending.Kind == MediaKind.Image;
                d.State = visual ? DeviceState.ShowingText : DeviceState.Playing;   // one item per Quest: replaces the old one
                Note(d.Name, (visual ? "Showing " : "Playing ") + sending.Name, NoteLevel.Success);
            }
            sending = null;
            Fire();
        }

        private void ForSelected(string verb, Action<QuestDevice> act)
        {
            var list = SelectedOnline();
            if (list.Count == 0) { Note("System", "No online device selected", NoteLevel.Warning); Fire(); return; }
            foreach (var d in list)
            {
                act(d);
                Note(d.Name, verb, NoteLevel.Success);
            }
            Fire();
        }

        public void Play()
        {
            var f = ControlFile;
            if (f == null) { Note("System", "Choose a file first", NoteLevel.Warning); Fire(); return; }
            ForSelected("Playing " + f.Name, d =>
            {
                if (d.FileId != f.Id || d.State == DeviceState.Stopped) d.PositionSec = 0f;   // new file / after Stop: from the start
                d.FileId = f.Id;
                d.State = DeviceState.Playing;
            });
        }

        public void Pause() { ForSelected("Paused", d => d.State = DeviceState.Paused); }
        public void Resume() { ForSelected("Resumed", d => d.State = DeviceState.Playing); }
        public void Stop() { ForSelected("Stopped", d => { d.State = DeviceState.Stopped; d.PositionSec = 0f; }); }
        public void SetMute(bool mute) { ForSelected(mute ? "Muted" : "Unmuted", d => d.Muted = mute); }

        public void ShowText()
        {
            var f = ControlFile;
            if (f == null || (f.Kind != MediaKind.Text && f.Kind != MediaKind.Image)) { Note("System", "Choose an image or text first", NoteLevel.Warning); Fire(); return; }
            ForSelected("Showing " + f.Name, d => { d.FileId = f.Id; d.State = DeviceState.ShowingText; });
        }

        public void HideText() { ForSelected("Hidden", d => d.State = DeviceState.Idle); }

        public void MarkAllRead()
        {
            foreach (var n in notes) n.Read = true;
            Fire();
        }

        public void ClearNotifications()
        {
            notes.Clear();
            Fire();
        }

        public void Tick(float dt)
        {
            if (scanLeft > 0f && (scanLeft -= dt) <= 0f)
            {
                int on = 0;
                foreach (var d in devices) if (d.Online) on++;
                Note("System", "Scan finished: " + on + " online", NoteLevel.Info);
                Fire();
            }
            if (connectLeft > 0f && (connectLeft -= dt) <= 0f)
            {
                foreach (var d in connecting)
                {
                    d.Connected = true;
                    Note(d.Name, "Connected (mock)", NoteLevel.Success);
                }
                connecting.Clear();
                Fire();
            }
            if (sending != null && (sendLeft -= dt) <= 0f) FinishSend();
            AdvancePlayback(dt);
        }

        // playing video / audio moves on; at the end the Quest stops (the screens read PositionSec themselves)
        private void AdvancePlayback(float dt)
        {
            bool ended = false;
            foreach (var d in devices)
            {
                if (!d.Online || d.State != DeviceState.Playing) continue;
                MediaFile f = null;
                foreach (var x in files) if (x.Id == d.FileId) f = x;
                if (f == null || f.DurationSec <= 0 || f.Kind == MediaKind.Text || f.Kind == MediaKind.Image) continue;
                d.PositionSec += dt;
                if (d.PositionSec < f.DurationSec) continue;
                d.PositionSec = 0f;
                d.State = DeviceState.Stopped;
                Note(d.Name, "Finished " + f.Name, NoteLevel.Info);
                ended = true;
            }
            if (ended) Fire();
        }
    }
}
