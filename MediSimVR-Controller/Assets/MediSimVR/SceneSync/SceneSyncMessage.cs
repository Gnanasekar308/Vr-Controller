using System;
using UnityEngine;

// One parsed SceneSync message (see SceneSyncProtocol). Fields not present in the JSON stay null / 0 / false.
namespace MediSimVR.SceneSync
{
    [Serializable]
    public class SceneSyncMessage
    {
        public string app;
        public string type;
        // here
        public string name;
        public string kind;
        public int count;
        // objects
        public int part;
        public int parts;
        public int total;
        public SyncObjectInfo[] objs;
        // transform
        public int sid;
        public int seq;
        public string id;
        public float[] pos;
        public float[] rot;
        public float[] scale;
        public bool locked;

        public bool TryGetPos(out Vector3 v) { return SceneSyncProtocol.TryVec3(pos, out v); }
        public bool TryGetRot(out Quaternion q) { return SceneSyncProtocol.TryQuat(rot, out q); }
        public bool TryGetScale(out Vector3 v) { return SceneSyncProtocol.TryScale(scale, out v); }
    }
}
