using System;
using UnityEngine;

namespace MediSimVR
{
    public enum DeviceState { Idle, Playing, Paused, Stopped, ShowingText }
    public enum MediaKind { Video, Audio, Image, Text }

    [Serializable]
    public class QuestDevice
    {
        public string Id = "";
        public string Name = "";
        public string Ip = "";
        public bool Online;
        public bool Connected;
        public int Battery = -1;
        public DeviceState State = DeviceState.Idle;
        public string FileId = "";
        public bool Selected;
        public bool Muted;
    }

    // A file the phone can send to the Quest: video, audio or text.
    [Serializable]
    public class MediaFile
    {
        public string Id = "";
        public string Name = "";
        public MediaKind Kind = MediaKind.Video;
        public long SizeBytes;
        public int DurationSec = -1;
        public string Text = "";               // content for text files
        public Color ThumbTop = Color.gray;
        public Color ThumbBottom = Color.black;
        public string Source = "";             // "mock", "typed", or a content:// uri / file path
        [NonSerialized] public Texture2D Preview;   // image files: loaded for the preview card (UI only)
    }

    // Only formats the Quest receiver can play / show.
    public static class MediaTypes
    {
        public static readonly string[] VideoExt = { ".mp4", ".m4v", ".webm", ".mkv" };
        public static readonly string[] AudioExt = { ".mp3", ".wav", ".ogg", ".m4a", ".aac" };
        public static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png" };
        public static readonly string[] TextExt = { ".txt" };

        public static string[] Ext(MediaKind k)
        {
            return k == MediaKind.Video ? VideoExt : k == MediaKind.Audio ? AudioExt : k == MediaKind.Image ? ImageExt : TextExt;
        }

        // Android picker MIME filter
        public static string Mimes(MediaKind k)
        {
            if (k == MediaKind.Video) return "video/mp4,video/x-m4v,video/webm,video/x-matroska";
            if (k == MediaKind.Audio) return "audio/mpeg,audio/wav,audio/x-wav,audio/ogg,audio/mp4,audio/x-m4a,audio/aac";
            if (k == MediaKind.Image) return "image/jpeg,image/png";
            return "text/plain";
        }

        public static bool Allowed(MediaKind k, string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            foreach (var e in Ext(k)) if (n.EndsWith(e)) return true;
            return false;
        }

        public static string Icon(MediaKind k)
        {
            return k == MediaKind.Video ? "movie" : k == MediaKind.Audio ? "music_note" : k == MediaKind.Image ? "image" : "article";
        }

        public static string Label(MediaKind k)
        {
            return k == MediaKind.Video ? "Video" : k == MediaKind.Audio ? "Audio" : k == MediaKind.Image ? "Image" : "Text";
        }
    }

    public enum NoteLevel { Info, Success, Warning, Error }

    public class AppNotification
    {
        public DateTime Time;
        public string Device = "";
        public string Message = "";
        public NoteLevel Level;
        public bool Read;
    }

    public static class Fmt
    {
        public static string Size(long b)
        {
            if (b <= 0) return "--";
            if (b >= (1L << 30)) return (b / (double)(1L << 30)).ToString("0.0") + " GB";
            if (b >= (1L << 20)) return Mathf.RoundToInt(b / (float)(1L << 20)) + " MB";
            return Mathf.Max(1, Mathf.RoundToInt(b / 1024f)) + " KB";
        }

        public static string Dur(int s)
        {
            if (s < 0) return "--:--";
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // 00:12:34
        public static string Clock(TimeSpan t)
        {
            int s = Mathf.Max(0, (int)t.TotalSeconds);
            return (s / 3600).ToString("00") + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        public static string Ago(DateTime t)
        {
            TimeSpan d = DateTime.Now - t;
            if (d.TotalSeconds < 60) return "now";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + " min";
            if (d.TotalHours < 24) return (int)d.TotalHours + " h";
            return t.ToString("dd MMM");
        }
    }
}
