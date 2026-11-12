using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MediSimVR
{
    // Records a Control Mode session as JSON (SessionRecord) on the phone and sends it to the PC at the end.
    //   File: Application.persistentDataPath/MediSimVR_Sessions/<yyyy-MM-dd_HHmmss>_<host|player>.json
    //         (Android: Android/data/com.medisim.vr/files/MediSimVR_Sessions, readable over USB)
    //   With a session service (demo mode): starts / ends with the session, events = its timeline, autosaved every few
    //   seconds while something changed, sent with ISessionService.Upload when the session ends.
    //   Without one (demo off, no backend yet): starts with the Control Mode role, events are kept here, autosaved; it
    //   stays on the phone ("PC recorder not connected").
    // Screens log through Add / Speak instead of ISessionService.Log, so both cases are recorded.
    public static class SessionLog
    {
        public const string Folder = "MediSimVR_Sessions";
        const float SaveEvery = 3f;
        const float UploadTimeout = 20f;   // no answer from the PC after this: the record says "not sent"

        public static event Action Changed;

        public static string FilePath { get; private set; } = "";      // current or last record
        public static string UploadStatus { get; private set; } = "";
        public static bool Recording { get { return startedAt != DateTime.MinValue; } }

        static DateTime startedAt = DateTime.MinValue, endedAt = DateTime.MinValue;
        static string role = "";
        static bool wasActive, dirty;
        static float saveTimer;
        static int uploadId, uploadCounter;   // the upload being waited for (0 = none)
        static SessionRecord pendingRecord;   // the ended session's record and file, until the PC answers
        static string pendingPath = "";
        static float uploadWait;
        static readonly List<RecordEvent> localEvents = new List<RecordEvent>();
        static readonly List<RecordText> notes = new List<RecordText>();
        static readonly List<RecordText> instructions = new List<RecordText>();
        // audio settings when there is no session service
        static bool localPhoneSound = true, localPhoneMic = true, localQuestMics = true;

        static ISessionService Ses { get { return AppRoot.Session; } }
        static IControllerService Svc { get { return AppRoot.Service; } }

        public static void Reset()
        {
            startedAt = endedAt = DateTime.MinValue;
            role = "";
            wasActive = dirty = false;
            FilePath = UploadStatus = "";
            uploadId = 0;
            pendingRecord = null;
            pendingPath = "";
            localEvents.Clear();
            notes.Clear();
            instructions.Clear();
        }

        // ---------- audio switches (session service when there is one) ----------

        public static bool PhoneSound
        {
            get { return Ses != null ? Ses.PhoneSound : localPhoneSound; }
            set { if (Ses != null) Ses.PhoneSound = value; else localPhoneSound = value; Add(SessionEventKind.Audio, Me(), "phone sound " + (value ? "on" : "off")); }
        }

        public static bool PhoneMic
        {
            get { return Ses != null ? !Ses.MicMuted : localPhoneMic; }
            set { if (Ses != null) Ses.MicMuted = !value; else localPhoneMic = value; Add(SessionEventKind.Audio, Me(), "phone mic " + (value ? "on" : "off")); }
        }

        public static bool QuestMics
        {
            get { return Ses != null ? Ses.QuestMics : localQuestMics; }
            set { if (Ses != null) Ses.QuestMics = value; else localQuestMics = value; Add(SessionEventKind.Audio, Me(), "Quest mics " + (value ? "on" : "off")); }
        }

        // ---------- recording ----------

        // an action (play, pause, send, audio switch...) for the timeline / record
        public static void Add(SessionEventKind kind, string who, string message)
        {
            if (Ses != null && Ses.Active) Ses.Log(kind, who, message);
            else
            {
                EnsureLocal();
                localEvents.Add(new RecordEvent { time = Iso(DateTime.Now), kind = kind.ToString(), who = who ?? "", message = message ?? "" });
            }
            dirty = true;
            Fire();
        }

        // a lesson note (Note) or important instruction (Instruction) for the Quests to read aloud.
        // false = recorded, but nobody can read it aloud (no session service / Quest receiver yet)
        public static bool Speak(SessionEventKind kind, string text, List<QuestDevice> to)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return false;
            bool aloud = Ses != null && Ses.Active && to != null && to.Count > 0;
            var names = new List<string>();
            if (to != null) foreach (var d in to) names.Add(d.Name);
            var entry = new RecordText { time = Iso(DateTime.Now), text = text, to = names.ToArray(), readAloud = aloud };
            (kind == SessionEventKind.Instruction ? instructions : notes).Add(entry);
            if (aloud) Ses.Speak(kind, text, to);
            else Add(kind, Me(), (kind == SessionEventKind.Instruction ? "important instruction" : "lesson note") + " (not read aloud: Quest receiver not connected): \"" + text + "\"");
            dirty = true;
            Fire();
            return aloud;
        }

        public static IReadOnlyList<RecordText> Notes { get { return notes; } }
        public static IReadOnlyList<RecordText> InstructionsSent { get { return instructions; } }

        // AppRoot.Update: start / end with the session, autosave
        public static void Tick(float dt)
        {
            bool active = Ses != null ? Ses.Active : ControlFlow.Role != SessionRole.None;
            if (active && !wasActive) Begin();
            if (!active && wasActive && Ses != null) Finish();
            wasActive = active;
            if (uploadId != 0 && (uploadWait += dt) >= UploadTimeout)
            {
                Debug.LogWarning("[MediSimVR] session record: no answer from the PC after " + UploadTimeout + " s");
                EndUpload("Not sent to the PC (no answer): saved on this phone");   // a late answer is then ignored
            }
            if (!Recording || !dirty || endedAt != DateTime.MinValue) return;
            saveTimer += dt;
            if (saveTimer < SaveEvery) return;
            saveTimer = 0f;
            Save();
        }

        static void EnsureLocal()
        {
            if (Ses == null && !Recording && ControlFlow.Role != SessionRole.None) Begin();
        }

        static void Begin()
        {
            startedAt = DateTime.Now;
            endedAt = DateTime.MinValue;
            role = ControlFlow.Role == SessionRole.Player ? "player" : "host";
            localEvents.Clear();
            notes.Clear();
            instructions.Clear();
            UploadStatus = Ses != null ? "" : "PC recorder not connected: saved on this phone";
            FilePath = Path.Combine(Application.persistentDataPath, Folder, startedAt.ToString("yyyy-MM-dd_HHmmss") + "_" + role + ".json");
            dirty = true;
            Save();
            Debug.Log("[MediSimVR] session record started: " + FilePath);
        }

        // the session ended: final save ("Sending to the PC..."), then upload. The upload keeps its own record and path,
        // so its answer (or the timeout) is written to that file even if a new session has started meanwhile.
        static void Finish()
        {
            endedAt = DateTime.Now;
            UploadStatus = "Sending to the PC...";
            if (Save() == null) return;
            pendingRecord = Build();
            pendingPath = FilePath;
            string json = JsonUtility.ToJson(pendingRecord, true);
            int id = ++uploadCounter;
            uploadId = id;
            uploadWait = 0f;
            Ses.Upload(Path.GetFileName(pendingPath), json, (ok, msg) =>
            {
                if (id != uploadId) return;   // timed out meanwhile
                Debug.Log("[MediSimVR] session record " + (ok ? "sent" : "not sent") + ": " + msg);
                EndUpload(ok ? msg : "Not sent to the PC (" + msg + "): saved on this phone");
            });
        }

        // the upload's final status goes into its own file (and on screen while that record is still the current one)
        static void EndUpload(string status)
        {
            uploadId = 0;
            if (pendingRecord != null)
            {
                pendingRecord.sentToPc = status;
                try { WriteFile(pendingPath, JsonUtility.ToJson(pendingRecord, true)); }
                catch (Exception e) { Debug.LogWarning("[MediSimVR] session record not updated: " + e.Message); }
                if (pendingPath == FilePath) UploadStatus = status;
            }
            pendingRecord = null;
            Fire();
        }

        static void WriteFile(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // writes the record; returns the JSON (null on failure)
        public static string Save()
        {
            if (!Recording) return null;
            try
            {
                string json = JsonUtility.ToJson(Build(), true);
                WriteFile(FilePath, json);
                dirty = false;
                Fire();
                return json;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MediSimVR] session record not saved: " + e.Message);
                UploadStatus = "Could not save on this phone: " + e.Message;
                Fire();
                return null;
            }
        }

        static SessionRecord Build()
        {
            var r = new SessionRecord
            {
                version = Application.version,
                demo = AppRoot.Demo,
                role = role,
                playerQuest = ControlFlow.PlayerQuestName(Svc),
                startedAt = Iso(startedAt),
                endedAt = endedAt != DateTime.MinValue ? Iso(endedAt) : "",
                durationSeconds = (int)((endedAt != DateTime.MinValue ? endedAt : DateTime.Now) - startedAt).TotalSeconds,
                recorder = Ses != null ? Ses.RecorderName : "",
                lessonNotes = notes.ToArray(),
                instructions = instructions.ToArray(),
                savedOnPhone = Folder + "/" + Path.GetFileName(FilePath),
                sentToPc = UploadStatus
            };
            bool anyMuted = false, anySelected = false;
            var quests = new List<RecordQuest>();
            if (Svc != null)
                foreach (var d in Svc.Devices)
                {
                    string file = "";
                    foreach (var f in Svc.Files) if (f.Id == d.FileId) file = f.Name;
                    quests.Add(new RecordQuest { id = d.Id, name = d.Name, ip = d.Ip, online = d.Online, selected = d.Selected && d.Online,
                                                 muted = d.Muted, battery = d.Battery, state = d.State.ToString(), file = file });
                    if (d.Selected && d.Online) { anySelected = true; if (d.Muted) anyMuted = true; }
                }
            r.quests = quests.ToArray();
            r.audio = new RecordAudio
            {
                liveVoice = Ses != null && Ses.LiveVoice,
                phoneSound = PhoneSound,
                phoneMic = PhoneMic,
                questSound = anySelected && !anyMuted,
                questMics = QuestMics
            };
            var events = new List<RecordEvent>();
            if (Ses != null)
            {
                var tl = Ses.Timeline;   // newest first
                for (int i = tl.Count - 1; i >= 0; i--)
                {
                    var e = tl[i];
                    if (e.Time < startedAt.AddSeconds(-2)) continue;   // an older session's leftovers
                    events.Add(new RecordEvent { time = Iso(e.Time), kind = e.Kind.ToString(), who = e.Device, message = e.Message });
                }
            }
            else events.AddRange(localEvents);
            r.events = events.ToArray();
            return r;
        }

        static string Me()
        {
            return ControlFlow.IsPlayer ? ControlFlow.PlayerQuestName(Svc) : "Host";
        }

        static string Iso(DateTime t) { return t.ToString("yyyy-MM-ddTHH:mm:ss"); }

        static void Fire() { if (Changed != null) Changed(); }
    }
}
