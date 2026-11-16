using System;
using MediSimVR.SceneSync;

namespace MediSimVR
{
    // JSON contracts with the Medisim PC server (field names = the server's camelCase JSON).

    [Serializable]
    public class StationStatusDto
    {
        public string state = "idle";   // idle | loading | ready | playing | paused | ended
        public string media = "";
        public string mediaKind = "";
        public float pos;
        public float dur;
        public bool muted;
        public float volume = 1f;
        public string text = "";
        public bool micEnabled = true;
        public bool headset;
        public int battery = -1;
        public string[] files = new string[0];
        public int eventSeq;
        public string eventLevel = "info";
        public string eventText = "";
    }

    [Serializable]
    public class StationDto
    {
        public string deviceId = "";
        public string deviceName = "";
        public bool isOnline;
        public string ip = "";
        public StationStatusDto status;
    }

    [Serializable]
    public class StationList
    {
        public StationDto[] items;
    }

    [Serializable]
    public class StationCommandRequest
    {
        public string[] targetDeviceIds;
        public string cmd = "";
        public string media = "";
        public string text = "";
        public string title = "";
        public bool mute;
        public float pos;
        public float volume = 1f;
        public bool autoplay = true;
        public float seconds;
    }

    [Serializable]
    public class CommandResult
    {
        public string deviceId;
        public string status;
        public string error;
    }

    [Serializable]
    public class CommandResults
    {
        public CommandResult[] results;
        public string error;
    }

    [Serializable]
    public class MediaInfoDto
    {
        public string name = "";
        public long size;
        public string sha256 = "";
    }

    [Serializable]
    public class MediaList
    {
        public MediaInfoDto[] files;
    }

    [Serializable]
    public class ServerScene
    {
        public string source = "";
        public long seq;
        public SyncObjectInfo[] objects;
    }

    [Serializable]
    public class ScenePoseDto
    {
        public string id;
        public float[] pos;
        public float[] rot;
        public float[] scale;
        public bool locked;
    }

    [Serializable]
    public class SceneStateRequest
    {
        public ScenePoseDto[] objects;
    }

    [Serializable]
    public class VoiceTargetsRequest
    {
        public string facultyId;
        public string[] targetDeviceIds;
    }

    [Serializable]
    public class SessionUploadRequest
    {
        public string fileName;
        public string json;
    }

    [Serializable]
    public class SessionUploadReply
    {
        public string saved;
        public string machine;
        public string error;
    }
}
