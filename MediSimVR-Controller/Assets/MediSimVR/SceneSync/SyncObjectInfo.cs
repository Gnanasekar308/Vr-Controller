using System;
using UnityEngine;

// One controllable object as sent in the "objects" message (see SceneSyncProtocol). Plain data, no MonoBehaviour.
namespace MediSimVR.SceneSync
{
    [Serializable]
    public class SyncObjectInfo
    {
        public string id;
        public string name;
        public string shape;      // cube | sphere | cylinder | capsule | plane | box
        public float[] color;     // r, g, b, a (0..1)
        public float[] size;      // bounds size in the object's own unscaled space
        public float[] center;    // bounds centre in the object's own unscaled space
        public float[] pos;       // relative to the SyncWorld root
        public float[] rot;       // x, y, z, w
        public float[] scale;
        public bool locked;

        // false = unusable entry (missing id or bad numbers); the phone skips it
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(id)) return false;
            return SceneSyncProtocol.Finite(pos, 3) && SceneSyncProtocol.Finite(rot, 4) && SceneSyncProtocol.Finite(scale, 3);
        }

        public Vector3 Pos { get { Vector3 v; return SceneSyncProtocol.TryVec3(pos, out v) ? v : Vector3.zero; } }
        public Quaternion Rot { get { Quaternion q; return SceneSyncProtocol.TryQuat(rot, out q) ? q : Quaternion.identity; } }
        public Vector3 Scale { get { Vector3 v; return SceneSyncProtocol.TryScale(scale, out v) ? v : Vector3.one; } }
        public Vector3 Size { get { Vector3 v; return SceneSyncProtocol.TryVec3(size, out v) ? v : Vector3.one; } }
        public Vector3 Center { get { Vector3 v; return SceneSyncProtocol.TryVec3(center, out v) ? v : Vector3.zero; } }

        public Color Color
        {
            get
            {
                if (!SceneSyncProtocol.Finite(color, 4)) return new Color(0.7f, 0.7f, 0.7f, 1f);
                return new Color(Mathf.Clamp01(color[0]), Mathf.Clamp01(color[1]), Mathf.Clamp01(color[2]), Mathf.Clamp01(color[3]));
            }
        }
    }
}
