using System;

namespace MediSimVR
{
    // The JSON session record (JsonUtility). One file per session: MediSimVR_Sessions/<start>_<role>.json on the phone,
    // also sent to the PC recorder. See SessionLog.
    [Serializable]
    public class SessionRecord
    {
        public string app = "MediSimVR phone";
        public string version = "";
        public bool demo;
        public string role = "";
        public string playerQuest = "";
        public string startedAt = "";
        public string endedAt = "";
        public int durationSeconds;
        public string recorder = "";
        public RecordAudio audio = new RecordAudio();
        public RecordQuest[] quests = new RecordQuest[0];
        public RecordText[] lessonNotes = new RecordText[0];
        public RecordText[] instructions = new RecordText[0];
        public RecordEvent[] events = new RecordEvent[0];
        public string savedOnPhone = "";
        public string sentToPc = "";
    }

    [Serializable]
    public class RecordAudio
    {
        public bool liveVoice, phoneSound, phoneMic, questSound, questMics;
    }

    [Serializable]
    public class RecordQuest
    {
        public string id = "", name = "", ip = "", state = "", file = "";
        public bool online, selected, muted;
        public int battery;
    }

    [Serializable]
    public class RecordText
    {
        public string time = "", text = "";
        public string[] to = new string[0];
        public bool readAloud;
    }

    [Serializable]
    public class RecordEvent
    {
        public string time = "", kind = "", who = "", message = "";
    }
}
