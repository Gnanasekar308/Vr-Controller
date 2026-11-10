using System;
using UnityEngine;

// One parsed SceneSync message (see SceneSyncProtocol). Fields not present in the JSON stay null / 0.
namespace MediSimVR.SceneSync
{
    [Serializable]
    public class SceneSyncMessage
    {
        public string app;
        public string type;
        public string name;
        public string kind;
        public int seq;
        public float[] pos;
        public float[] rot;
        public float[] scale;

        public bool TryGetPos(out Vector3 v)
        {
            v = Vector3.zero;
            if (!Valid(pos, 3)) return false;
            v = new Vector3(pos[0], pos[1], pos[2]);
            return true;
        }

        public bool TryGetScale(out Vector3 v)
        {
            v = Vector3.one;
            if (!Valid(scale, 3)) return false;
            v = new Vector3(scale[0], scale[1], scale[2]);
            return true;
        }

        // rot = [x, y, z, w]; normalised. A zero quaternion is rejected.
        public bool TryGetRot(out Quaternion q)
        {
            q = Quaternion.identity;
            if (!Valid(rot, 4)) return false;
            float len = Mathf.Sqrt(rot[0] * rot[0] + rot[1] * rot[1] + rot[2] * rot[2] + rot[3] * rot[3]);
            if (len < 1e-6f) return false;
            q = new Quaternion(rot[0] / len, rot[1] / len, rot[2] / len, rot[3] / len);
            return true;
        }

        private static bool Valid(float[] a, int n)
        {
            if (a == null || a.Length != n) return false;
            for (int i = 0; i < n; i++)
                if (float.IsNaN(a[i]) || float.IsInfinity(a[i])) return false;
            return true;
        }
    }
}
