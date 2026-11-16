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
        public long listVersion;   // grows when the station's object list (or how an object looks) changes
        public SyncObjectInfo[] objects;
    }

    // The same answer of GET /api/scene, read again for the fields SyncObjectInfo does not have
    [Serializable]
    public class ServerSceneLook
    {
        public ServerObjectLook[] objects;
    }

    [Serializable]
    public class ServerObjectLook
    {
        public string id = "";
        public string model = "";   // hash of the object's SceneModelDto on the server, "" = none (plain shape)
    }

    // How a station object really looks (GET /api/scene/models/<hash>): meshes in the object's own space,
    // their colours and textures. Written by the station (StationModelExporter), drawn by StationModels.
    [Serializable]
    public class SceneModelDto
    {
        public int v = 1;
        public SceneModelPartDto[] parts;
        public SceneModelMaterialDto[] materials;
        public SceneModelTextureDto[] textures;
    }

    [Serializable]
    public class SceneModelPartDto
    {
        public float[] vertices;   // x y z, ...
        public float[] normals;    // x y z, ... (empty = calculated here)
        public float[] uv;         // u v, ... (empty = none)
        public int[] triangles;
        public int material;
    }

    [Serializable]
    public class SceneModelMaterialDto
    {
        public float[] color = { 1f, 1f, 1f, 1f };
        public int texture = -1;
        public bool transparent;
        public bool unlit;
        public bool twoSided;
        public float smoothness = 0.2f;
        public float metallic;
    }

    [Serializable]
    public class SceneModelTextureDto
    {
        public int width;
        public int height;
        public string png = "";   // base64
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

    // Answer of POST /api/scene/recording/save (the Live Scene's Save button)
    [Serializable]
    public class SceneRecordingReply
    {
        public string saved = "";     // "SceneRecordings/<file>.json" on the server PC, "" = nothing was saved
        public int samples;
        public float seconds;
        public string machine = "";
        public string error = "";
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
