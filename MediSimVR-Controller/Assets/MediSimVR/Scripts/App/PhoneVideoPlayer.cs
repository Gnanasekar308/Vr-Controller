using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace MediSimVR
{
    // The phone's own player in the Control tab: plays the video / audio that was sent to the Quests, in step with them
    // (the Control tab mirrors the Quests' Play / Pause / Stop on it). Sources:
    //   - a file picked on the phone (content:// uri): copied to the app cache first (MQGallery.copyToFile, background
    //     thread), because Unity's VideoPlayer cannot read content:// uris
    //   - "streaming:<path>": a sample file in StreamingAssets (demo files can point to one, see DemoConfig.Files)
    //   - a plain file path (Editor)
    // Demo files without a sample have no media: HasMedia is false and the Control tab shows the thumbnail.
    // VideoPlayer and AudioSource are added by the UI builder (a runtime AddComponent could be stripped by IL2CPP).
    public class PhoneVideoPlayer : MonoBehaviour
    {
        public VideoPlayer video;
        public AudioSource audioSource;

        public event Action Changed;

        public string FileId { get; private set; } = "";
        public bool HasMedia { get; private set; }
        public bool Ready { get; private set; }
        public bool IsVideo { get; private set; }
        public string Status { get; private set; } = "";
        public Texture VideoTexture { get { return rt; } }

        private RenderTexture rt;
        private bool wantPlay, muted;
        private int loadToken;
        private string pendingCopy = "";   // cache path of the copy this player waits for

        public bool IsPlaying { get { return Ready && (IsVideo ? video.isPlaying : audioSource.isPlaying); } }

        public float Time
        {
            get
            {
                if (!Ready) return 0f;
                return IsVideo ? (float)video.time : audioSource.time;
            }
        }

        public float Length
        {
            get
            {
                if (!Ready) return 0f;
                if (IsVideo) return video.length > 0 ? (float)video.length : 0f;
                return audioSource.clip != null ? audioSource.clip.length : 0f;
            }
        }

        private void Awake()
        {
            if (video != null)
            {
                video.playOnAwake = false;
                video.waitForFirstFrame = true;
                video.audioOutputMode = VideoAudioOutputMode.Direct;
                video.renderMode = VideoRenderMode.RenderTexture;
                video.prepareCompleted += OnVideoPrepared;
                video.errorReceived += (p, msg) => Fail("This phone cannot play the video: " + msg);
            }
            if (audioSource != null) audioSource.playOnAwake = false;
        }

        private void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
        }

        // ---------- source ----------

        public void Load(MediaFile f)
        {
            if (f == null) { Clear(); return; }
            if (f.Id == FileId) return;
            Clear();
            FileId = f.Id;
            if (f.Kind != MediaKind.Video && f.Kind != MediaKind.Audio) { Fire(); return; }
            IsVideo = f.Kind == MediaKind.Video;
            string src = f.Source ?? "";
            if (src.StartsWith("streaming:"))
            {
                Open(Path.Combine(Application.streamingAssetsPath, src.Substring("streaming:".Length)));
            }
            else if (src.StartsWith("content:"))
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                string dest = Path.Combine(Application.temporaryCachePath, "preview",
                    Mathf.Abs(src.GetHashCode()).ToString("x") + "_" + Safe(f.Name));
                SetStatus("Preparing the preview on this phone...");
                pendingCopy = dest;   // OnCopied ignores answers for an older request
                try
                {
                    using (var c = new AndroidJavaClass("com.highbrow.mq.MQGallery"))
                        c.CallStatic("copyToFile", src, dest, gameObject.name, "OnCopied");
                }
                catch (Exception e) { SetStatus("Cannot open the file on this phone: " + e.Message); }
#else
                SetStatus("content:// files play on Android only");
#endif
            }
            else if (src.Length > 0 && src != "mock" && src != "typed" && File.Exists(src))
            {
                Open(src);
            }
            else
            {
                SetStatus("Sample file: it plays on the Quests. Pick a video from this phone (Browse phone) to see it here too.");
            }
        }

        // called by MQGallery.copyToFile (UnitySendMessage): "OK\t<path>" or "ERR\t<path>\t<message>"; an answer for an
        // older request (another file was chosen meanwhile) is ignored
        public void OnCopied(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            string[] p = data.Split('\t');
            if (p.Length < 2 || p[1] != pendingCopy) return;
            pendingCopy = "";
            if (p[0] == "ERR") { Fail("Cannot open the file on this phone: " + (p.Length > 2 ? p[2] : "")); return; }
            if (p[0] == "OK") Open(p[1]);
        }

        private void Open(string path)
        {
            HasMedia = true;
            Ready = false;
            int token = ++loadToken;
            if (IsVideo)
            {
                if (video == null) { SetStatus("No video player"); return; }
                video.source = VideoSource.Url;
                video.url = path;
                video.Prepare();
                SetStatus("Loading...");
            }
            else
            {
                if (audioSource == null) { SetStatus("No audio player"); return; }
                StartCoroutine(LoadAudio(path, token));
            }
        }

        private void OnVideoPrepared(VideoPlayer p)
        {
            int w = (int)Mathf.Max(16, p.width), h = (int)Mathf.Max(16, p.height);
            float k = Mathf.Min(1f, 1280f / Mathf.Max(w, h));
            w = Mathf.Max(16, Mathf.RoundToInt(w * k));
            h = Mathf.Max(16, Mathf.RoundToInt(h * k));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { p.targetTexture = null; rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "PhonePreview" };
                rt.Create();
            }
            p.targetTexture = rt;
            ApplyMute();
            Ready = true;
            SetStatus("");
            if (wantPlay) p.Play();
            Debug.Log("[MediSimVR] phone preview ready: " + w + "x" + h + ", " + p.length.ToString("F1") + " s");
        }

        private IEnumerator LoadAudio(string path, int token)
        {
            SetStatus("Loading...");
            string url = path.Contains("://") ? path : "file://" + path;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            AudioType type = ext == ".wav" ? AudioType.WAV : ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".mp3" ? AudioType.MPEG
                : ext == ".m4a" || ext == ".aac" ? AudioType.ACC : AudioType.UNKNOWN;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;
                yield return req.SendWebRequest();
                if (token != loadToken) yield break;   // another file was loaded meanwhile
                if (req.result != UnityWebRequest.Result.Success) { Fail("This phone cannot play the audio: " + req.error); yield break; }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null) { Fail("This phone cannot play the audio"); yield break; }
                audioSource.clip = clip;
                ApplyMute();
                Ready = true;
                SetStatus("");
                if (wantPlay) audioSource.Play();
            }
        }

        public void Clear()
        {
            loadToken++;
            pendingCopy = "";
            wantPlay = false;
            if (video != null) { video.Stop(); video.url = ""; }
            if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }
            FileId = "";
            HasMedia = false;
            Ready = false;
            Status = "";
            Fire();
        }

        // ---------- control (mirrors the Quests) ----------

        public void Play()
        {
            wantPlay = true;
            if (!Ready) return;
            if (IsVideo) video.Play(); else if (!audioSource.isPlaying) { if (audioSource.time > 0f) audioSource.UnPause(); else audioSource.Play(); }
        }

        public void Pause()
        {
            wantPlay = false;
            if (!Ready) return;
            if (IsVideo) video.Pause(); else audioSource.Pause();
        }

        public void Stop()
        {
            wantPlay = false;
            if (!Ready) return;
            if (IsVideo) { video.Stop(); video.Prepare(); } else { audioSource.Stop(); audioSource.time = 0f; }
        }

        public void Seek(float seconds)
        {
            if (!Ready || seconds < 0f) return;
            if (IsVideo) { if (video.canSetTime) video.time = seconds; }
            else if (audioSource.clip != null) audioSource.time = Mathf.Min(seconds, audioSource.clip.length - 0.05f);
        }

        // phone (sender) sound on / off
        public void SetMute(bool m)
        {
            muted = m;
            ApplyMute();
        }

        private void ApplyMute()
        {
            if (video != null) for (ushort i = 0; i < Mathf.Max(1, (int)video.audioTrackCount); i++) video.SetDirectAudioMute(i, muted);
            if (audioSource != null) audioSource.mute = muted;
        }

        // the phone cannot play it: no media here any more (the card shows the thumbnail and "ON THE QUEST" again)
        private void Fail(string s)
        {
            HasMedia = false;
            Ready = false;
            wantPlay = false;
            SetStatus(s);
        }

        private void SetStatus(string s)
        {
            Status = s;
            Fire();
        }

        private void Fire() { if (Changed != null) Changed(); }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
