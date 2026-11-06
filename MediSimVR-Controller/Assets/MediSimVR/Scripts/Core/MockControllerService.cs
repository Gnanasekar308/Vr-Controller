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

        public MockControllerService()
        {
            notesEnabled = PlayerPrefs.GetInt("msvr_notes", 1) == 1;
            Dev("q1", "Quest-01", "192.168.1.25", true, 86);
            Dev("q2", "Quest-02", "192.168.1.26", true, 64);
            Dev("q3", "Quest-03", "192.168.1.27", false, -1);
            Dev("q4", "Quest-04", "192.168.1.28", true, 92);
            devices[0].Selected = true;
            devices[1].Selected = true;
            devices[0].State = DeviceState.Playing;   // "In Use" in the mock
            devices[0].FileId = "f1";

            Add("f1", "Nature_01.mp4", MediaKind.Video, 245L << 20, 330, "#7FC8F8", "#2E7D32", "");
            Add("f2", "Ocean_03.mp4", MediaKind.Video, 312L << 20, 380, "#81D4FA", "#01579B", "");
            Add("f3", "Lecture_Intro.mp3", MediaKind.Audio, 6L << 20, 200, "#FFB74D", "#6A1B9A", "");
            Add("f4", "Safety_Instructions.txt", MediaKind.Text, 0, -1, "#E3F2FD", "#90CAF9",
                "1. Clear the play area.\n2. Adjust the head strap.\n3. Follow the trainer's instructions.");
            ControlFileId = "f1";
            Note("System", "Mock mode: no real Quest connected yet", NoteLevel.Info);
        }

        private void Dev(string id, string name, string ip, bool online, int bat)
        {
            devices.Add(new QuestDevice { Id = id, Name = name, Ip = ip, Online = online, Battery = bat });
        }

        private void Add(string id, string name, MediaKind k, long size, int sec, string top, string bottom, string text)
        {
            var f = new MediaFile { Id = id, Name = name, Kind = k, SizeBytes = size, DurationSec = sec, Text = text, Source = "mock" };
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
            ForSelected("Playing " + f.Name, d => { d.FileId = f.Id; d.State = DeviceState.Playing; });
        }

        public void Pause() { ForSelected("Paused", d => d.State = DeviceState.Paused); }
        public void Resume() { ForSelected("Resumed", d => d.State = DeviceState.Playing); }
        public void Stop() { ForSelected("Stopped", d => d.State = DeviceState.Stopped); }
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
        }
    }
}
