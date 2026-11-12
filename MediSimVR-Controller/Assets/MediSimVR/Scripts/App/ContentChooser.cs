using System;
using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    // Choosing and sending the content, shared by the Send tab and the Play page (on the App object, one per app):
    //   type (Video / Audio / Image / Text) -> the files of that type already in the app (demo files, earlier picks) or
    //   browse the phone (Android picker); text can also be typed. Only this object listens to the file picker, so a
    //   picked file is added once. Send asks before replacing what a Quest already plays or shows.
    public class ContentChooser : MonoBehaviour
    {
        public Navigator nav;

        static readonly string[] Kinds = { "Video", "Audio", "Image", "Text" };
        static readonly string[] KindIcons = { "movie", "music_note", "image", "article" };

        private FilePicker hooked;
        private Action pendingDone;   // 'done' of the pick that waits for the phone's file picker

        private IControllerService Svc { get { return AppRoot.Service; } }

        // AppRoot.Awake creates the picker (all Awakes run before any Start); Picker() also hooks late, before any pick
        private void Start() { Picker(); }

        private FilePicker Picker()
        {
            if (hooked != null) return hooked;
            hooked = AppRoot.Picker != null ? AppRoot.Picker : GetComponent<FilePicker>();
            if (hooked == null) { Debug.LogWarning("[MediSimVR] ContentChooser: no file picker"); return null; }
            hooked.Picked += OnPicked;
            hooked.Failed += OnFailed;
            return hooked;
        }

        // the phone's file picker answers later (OnPicked); 'done' is called then
        private void Pick(MediaKind kind, Action done)
        {
            var p = Picker();
            if (p == null) { nav.Say("File picker not available"); return; }
            pendingDone = done;
            p.Pick(kind);
        }

        private void OnDestroy()
        {
            if (hooked == null) return;
            hooked.Picked -= OnPicked;
            hooked.Failed -= OnFailed;
        }

        private void OnPicked(List<MediaFile> list)
        {
            var done = pendingDone;
            pendingDone = null;
            if (list.Count == 0) return;
            Svc.AddFile(list[0]);              // the newest choice becomes the current content
            nav.Say(list[0].Name + " selected");
            if (done != null) done();
        }

        private void OnFailed(string msg) { pendingDone = null; nav.Say(msg); }

        // type first (Send tab Select / Change, Play page Change). done: called once the new content is the current one
        // (file from the app, typed text, or a file picked on the phone)
        public void Choose(Action done)
        {
            nav.actionSheet.Show("Select file type", Kinds, KindIcons, k => Choose((MediaKind)k, done));
        }

        public void Choose(MediaKind kind, Action done)
        {
            string[] extra = kind == MediaKind.Text ? new[] { "Write new text", "Open .txt file" } : new[] { "Browse phone" };
            string[] extraIcons = kind == MediaKind.Text ? new[] { "note_add", "folder_open" } : new[] { "folder_open" };
            int room = Mathf.Max(0, nav.actionSheet.options.Count - extra.Length);
            var lib = new List<MediaFile>();
            foreach (var f in Svc.Files) if (f.Kind == kind && lib.Count < room) lib.Add(f);
            if (lib.Count == 0 && kind != MediaKind.Text) { Pick(kind, done); return; }   // nothing in the app yet

            var names = new List<string>();
            var icons = new List<string>();
            foreach (var f in lib) { names.Add(f.Name); icons.Add(MediaTypes.Icon(kind)); }
            names.AddRange(extra);
            icons.AddRange(extraIcons);
            nav.actionSheet.Show("Choose " + MediaTypes.Label(kind).ToLowerInvariant(), names.ToArray(), icons.ToArray(), k =>
            {
                if (k < lib.Count)
                {
                    Svc.ControlFileId = lib[k].Id;
                    nav.Say(lib[k].Name + " selected");
                    if (done != null) done();
                    return;
                }
                if (kind == MediaKind.Text && k == lib.Count) WriteText(done);
                else Pick(kind, done);
            });
        }

        public void WriteText(Action done)
        {
            nav.textEditor.Open("New Text", "", "", (title, content) =>
            {
                var f = FilePicker.Make("typed:" + title + DateTime.Now.Ticks, TxtName(title), 0, MediaKind.Text);
                f.Text = content;
                f.Source = "typed";
                f.SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content);
                Svc.AddFile(f);
                nav.Say("Text ready to send");
                if (done != null) done();
            });
        }

        public void EditText()
        {
            var f = Svc.ControlFile;
            if (f == null || f.Kind != MediaKind.Text) return;
            nav.textEditor.Open("Edit Text", f.Name.Replace(".txt", ""), f.Text, (title, content) =>
            {
                f.Name = TxtName(title);
                f.Text = content;
                Svc.UpdateFile(f);
            });
        }

        static string TxtName(string title)
        {
            string t = (title ?? "").Trim();
            if (t.Length == 0) t = "Text";
            return t.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? t : t + ".txt";
        }

        // send f to the targets; a Quest shows one thing at a time, so ask before replacing what it plays / shows.
        // sent: called after the send started.
        public void Send(MediaFile f, List<QuestDevice> targets, Action sent)
        {
            if (f == null || targets == null || targets.Count == 0) return;
            string busy = "";
            foreach (var d in targets)
                if (DeviceStatus.InUse(d) && d.FileId != f.Id) busy += (busy.Length > 0 ? ", " : "") + d.Name;
            Action go = () =>
            {
                Svc.Send(f);
                nav.Say("Sending " + f.Name + " to " + targets.Count + " Quest(s)");
                if (sent != null) sent();
            };
            if (busy.Length > 0)
                nav.popup.Show("Replace content?", busy + " is already playing or showing something.\nSend " + f.Name + " instead?",
                    "Replace", go, "Cancel");
            else go();
        }
    }
}
