using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // Pose of one object, relative to the SyncWorld root. "euler" is what the Inspector shows (degrees, any value,
    // e.g. 720); "rot" is the real rotation. Scale is never below SceneSyncProtocol.MinScale per axis.
    public struct ScenePose
    {
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 euler;
        public Vector3 scale;

        // rotation changed by a gizmo / pinch: the shown euler angles stay close to the previous ones (no 0 <-> 360 jumps)
        public static ScenePose FromRotation(Vector3 pos, Quaternion rot, Vector3 scale, Vector3 eulerHint)
        {
            return new ScenePose { pos = pos, rot = rot, euler = UnwrapEuler(rot.eulerAngles, eulerHint), scale = SceneSyncProtocol.ClampScale(scale) };
        }

        // rotation typed in the Inspector (degrees)
        public static ScenePose FromEuler(Vector3 pos, Vector3 euler, Vector3 scale)
        {
            return new ScenePose { pos = pos, rot = Quaternion.Euler(euler), euler = euler, scale = SceneSyncProtocol.ClampScale(scale) };
        }

        public bool SameAs(ScenePose o)
        {
            return (pos - o.pos).sqrMagnitude < 1e-12f && Quaternion.Angle(rot, o.rot) < 1e-4f &&
                   (scale - o.scale).sqrMagnitude < 1e-14f && (euler - o.euler).sqrMagnitude < 1e-8f;
        }

        // every value finite; scale at least the minimum
        public ScenePose Sanitized()
        {
            var p = this;
            if (!Finite(p.pos)) p.pos = Vector3.zero;
            if (!Finite(p.euler)) p.euler = Vector3.zero;
            if (float.IsNaN(p.rot.x) || float.IsNaN(p.rot.y) || float.IsNaN(p.rot.z) || float.IsNaN(p.rot.w) ||
                (p.rot.x == 0f && p.rot.y == 0f && p.rot.z == 0f && p.rot.w == 0f))
                p.rot = Quaternion.Euler(p.euler);
            p.rot = Quaternion.Normalize(p.rot);
            p.scale = SceneSyncProtocol.ClampScale(p.scale);
            return p;
        }

        // each angle moved by whole turns so it is as close as possible to the hint
        public static Vector3 UnwrapEuler(Vector3 e, Vector3 hint)
        {
            return new Vector3(Unwrap(e.x, hint.x), Unwrap(e.y, hint.y), Unwrap(e.z, hint.z));
        }

        private static float Unwrap(float a, float hint)
        {
            if (float.IsNaN(hint) || float.IsInfinity(hint)) return a;
            return a + 360f * Mathf.Round((hint - a) / 360f);
        }

        private static bool Finite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
