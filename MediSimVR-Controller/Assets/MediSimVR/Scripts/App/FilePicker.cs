using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MediSimVR
{
    // Picks video / audio / text files from the phone (Plugins/Android/MQGallery.java).
    // Only Quest-supported formats are accepted. Text files are read so they can be edited.
    public class FilePicker : MonoBehaviour
    {
        const string JavaClass = "com.highbrow.mq.MQGallery";
        public event Action<List<MediaFile>> Picked;
        public event Action<string> Failed;

        MediaKind pending = MediaKind.Video;

        public void Pick(MediaKind kind)
        {
            pending = kind;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var c = new AndroidJavaClass(JavaClass))
                    c.CallStatic("pickTypes", gameObject.name, "OnPickResult", kind != MediaKind.Text, MediaTypes.Mimes(kind));
            }
            catch (Exception e) { Fail("File picker could not open: " + e.Message); }
#elif UNITY_EDITOR
            string ext = string.Join(",", Array.ConvertAll(MediaTypes.Ext(kind), x => x.TrimStart('.')));
            string p = UnityEditor.EditorUtility.OpenFilePanel("Choose " + MediaTypes.Label(kind), "", ext);
            if (string.IsNullOrEmpty(p)) return;
            var fi = new FileInfo(p);
            Deliver(new List<string[]> { new[] { p, fi.Length.ToString(), fi.Name } });
#else
            Fail("File picking is available on Android only");
#endif
        }

        // called from Java: lines "uri \t size \t name", "ERR \t msg", or "" when cancelled
        public void OnPickResult(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            if (data.StartsWith("ERR\t")) { Fail(data.Substring(4)); return; }
            var rows = new List<string[]>();
            foreach (string line in data.Split('\n'))
            {
                string[] p = line.Split('\t');
                if (p.Length >= 3 && p[0].Length > 0) rows.Add(p);
            }
            Deliver(rows);
        }

        void Deliver(List<string[]> rows)
        {
            var ok = new List<MediaFile>();
            var rejected = new List<string>();
            foreach (var p in rows)
            {
                string name = p[2];
                if (!MediaTypes.Allowed(pending, name)) { rejected.Add(name); continue; }
                long size;
                if (!long.TryParse(p[1], out size)) size = -1;
                var f = Make(p[0], name, size, pending);
                if (pending == MediaKind.Text) f.Text = ReadText(p[0]);
                if (pending == MediaKind.Image) f.Preview = ReadImage(p[0]);
                ok.Add(f);
            }
            if (rejected.Count > 0) Fail("Not supported on Quest: " + string.Join(", ", rejected.ToArray()));
            if (ok.Count > 0 && Picked != null) Picked(ok);
        }

        static string ReadText(string source)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (source.StartsWith("content:"))
                    using (var c = new AndroidJavaClass(JavaClass)) return c.CallStatic<string>("readText", source, 65536) ?? "";
#endif
                return File.ReadAllText(source);
            }
            catch (Exception) { return ""; }
        }

        // image preview for the Send card (max 8 MB)
        static Texture2D ReadImage(string source)
        {
            try
            {
                byte[] bytes = null;
#if UNITY_ANDROID && !UNITY_EDITOR
                if (source.StartsWith("content:"))
                {
                    using (var c = new AndroidJavaClass(JavaClass))
                    {
                        sbyte[] sb = c.CallStatic<sbyte[]>("readBytes", source, 8 * 1024 * 1024);
                        if (sb != null) { bytes = new byte[sb.Length]; Buffer.BlockCopy(sb, 0, bytes, 0, sb.Length); }
                    }
                }
#endif
                if (bytes == null) bytes = File.ReadAllBytes(source);
                var tex = new Texture2D(2, 2);
                return tex.LoadImage(bytes) ? tex : null;
            }
            catch (Exception) { return null; }
        }

        public static MediaFile Make(string source, string name, long size, MediaKind kind)
        {
            int h = Mathf.Abs(source.GetHashCode());
            float hue = (h % 360) / 360f;
            return new MediaFile
            {
                Id = "p" + h + "_" + DateTime.Now.Ticks,
                Name = name,
                Kind = kind,
                SizeBytes = size,
                DurationSec = -1,
                Source = source,
                ThumbTop = Color.HSVToRGB(hue, 0.35f, 0.95f),
                ThumbBottom = Color.HSVToRGB(hue, 0.8f, 0.45f)
            };
        }

        void Fail(string msg) { if (Failed != null) Failed(msg); }
    }
}
