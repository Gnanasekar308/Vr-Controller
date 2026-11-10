using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // One object of the live scene on the phone (filled from the receiver's object list).
    public class SceneObjectState
    {
        public string id;
        public string name;
        public string shape;
        public Color color;
        public Vector3 size;      // bounds size in the object's own unscaled space
        public Vector3 center;    // bounds centre in the object's own unscaled space
        public ScenePose pose;
        public bool locked;
        public ScenePose initialPose;   // as received from the scene source (Reset)

        public static SceneObjectState From(SyncObjectInfo info)
        {
            var pose = ScenePose.FromRotation(info.Pos, info.Rot, info.Scale, Vector3.zero);
            pose.euler = Tidy(pose.euler);
            var size = info.Size;
            return new SceneObjectState
            {
                id = info.id,
                name = string.IsNullOrWhiteSpace(info.name) ? info.id : info.name,
                shape = string.IsNullOrEmpty(info.shape) ? SceneSyncProtocol.ShapeBox : info.shape,
                color = info.Color,
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)),
                center = info.Center,
                pose = pose,
                locked = info.locked,
                initialPose = pose
            };
        }

        // centre of the object's bounds, relative to the SyncWorld root
        public Vector3 CenterPoint { get { return CenterOf(pose); } }

        public Vector3 CenterOf(ScenePose p)
        {
            return p.pos + p.rot * Vector3.Scale(p.scale, center);
        }

        // radius of a sphere around the scaled bounds (camera focus)
        public float Radius
        {
            get
            {
                Vector3 s = Vector3.Scale(VisualSize, pose.scale);
                return Mathf.Max(0.0005f, 0.5f * s.magnitude);
            }
        }

        // size of the drawn shape before scaling (primitives have a fixed size)
        public Vector3 VisualSize
        {
            get
            {
                switch (shape)
                {
                    case SceneSyncProtocol.ShapeCylinder:
                    case SceneSyncProtocol.ShapeCapsule: return new Vector3(1f, 2f, 1f);
                    case SceneSyncProtocol.ShapePlane: return new Vector3(10f, 0f, 10f);
                    case SceneSyncProtocol.ShapeCube:
                    case SceneSyncProtocol.ShapeSphere: return Vector3.one;
                    default: return size;
                }
            }
        }

        // -0.0001 -> 0, 359.99997 -> 360 (values received from a receiver)
        private static Vector3 Tidy(Vector3 e)
        {
            return new Vector3(T(e.x), T(e.y), T(e.z));
        }

        private static float T(float a)
        {
            float r = Mathf.Round(a * 1000f) / 1000f;
            if (Mathf.Abs(r - 360f) < 0.001f) r = 0f;
            return r == 0f ? 0f : r;
        }
    }
}
