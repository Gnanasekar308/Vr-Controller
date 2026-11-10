using System.Collections.Generic;
using UnityEngine;

// Put this on every object the phone may control (it must be inside the GameObject that has the SyncReceiver,
// e.g. a child of "SyncWorld"). The receiver sends the object to the phone (id, name, shape, colour, pose) and
// moves it smoothly when the phone sends a new pose. Values are relative to the receiver's root ("space").
namespace MediSimVR.SceneSync
{
    [DisallowMultipleComponent]
    public class SyncObject : MonoBehaviour
    {
        [Tooltip("Stable id used by the phone. Empty = derived from the object name. Must be unique inside one SyncWorld.")]
        [SerializeField] private string id = "";
        [Tooltip("Name shown in the phone's Hierarchy. Empty = GameObject name.")]
        [SerializeField] private string displayName = "";
        [Tooltip("Locked objects cannot be moved from the phone (the phone can unlock them).")]
        [SerializeField] private bool locked;
        [Tooltip("Higher = follows the phone faster.")]
        [SerializeField] private float smoothing = 12f;

        private static readonly string[] Primitives = { "Cube", "Sphere", "Cylinder", "Capsule", "Plane" };
        private static readonly List<Renderer> rendererBuffer = new List<Renderer>();

        private Transform space;          // the SyncReceiver's transform
        private Vector3 targetPos, targetScale;
        private Quaternion targetRot = Quaternion.identity;
        private bool hasTarget;

        public string Id { get { return string.IsNullOrWhiteSpace(id) ? AutoId(gameObject.name) : id.Trim(); } }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName.Trim(); } }
        public bool Locked { get { return locked; } set { locked = value; } }
        public bool HasTarget { get { return hasTarget; } }

        // editor builders / tests
        public void Setup(string newId, string newName, bool isLocked)
        {
            id = newId;
            displayName = newName;
            locked = isLocked;
        }

        public static string AutoId(string objectName)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (objectName ?? "").Trim().ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
            return sb.Length == 0 ? "object" : SceneSyncProtocol.Clip(sb.ToString(), SceneSyncProtocol.MaxIdLength);
        }

        // ---------- pose relative to the receiver root ----------

        public void GetPose(Transform root, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            if (hasTarget && root == space)
            {
                pos = targetPos;
                rot = targetRot;
                scale = targetScale;
                return;
            }
            ReadPose(root, out pos, out rot, out scale);
        }

        private void ReadPose(Transform root, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            Transform t = transform;
            if (root == null) { pos = t.position; rot = t.rotation; scale = t.lossyScale; return; }
            if (t.parent == root) { pos = t.localPosition; rot = t.localRotation; scale = t.localScale; return; }
            pos = root.InverseTransformPoint(t.position);
            rot = Quaternion.Inverse(root.rotation) * t.rotation;
            scale = Div(t.lossyScale, root.lossyScale);
        }

        private void WritePose(Transform root, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Transform t = transform;
            if (root == null) { t.SetPositionAndRotation(pos, rot); return; }
            if (t.parent == root)
            {
                t.localPosition = pos;
                t.localRotation = rot;
                t.localScale = scale;
                return;
            }
            t.SetPositionAndRotation(root.TransformPoint(pos), root.rotation * rot);
            Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = Div(Vector3.Scale(scale, root.lossyScale), parentScale);
        }

        // called by the SyncReceiver (main thread) when the phone sends a pose
        public void SetTarget(Transform root, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            space = root;
            targetPos = pos;
            targetRot = rot;
            targetScale = SceneSyncProtocol.ClampScale(scale);
            hasTarget = true;
        }

        private void Update()
        {
            if (!hasTarget) return;
            Vector3 p, s;
            Quaternion r;
            ReadPose(space, out p, out r, out s);
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            // "arrived" relative to the size of the values (float steps are bigger far away, e.g. 400 m)
            float posTol = 1e-10f * Mathf.Max(1f, targetPos.sqrMagnitude);
            float scaleTol = 1e-12f * Mathf.Max(1f, targetScale.sqrMagnitude);
            bool close = (p - targetPos).sqrMagnitude < posTol && Quaternion.Angle(r, targetRot) < 0.01f && (s - targetScale).sqrMagnitude < scaleTol;
            if (close || smoothing <= 0f)
            {
                WritePose(space, targetPos, targetRot, targetScale);
                hasTarget = false; // arrived: stop writing every frame
                return;
            }
            WritePose(space, Vector3.Lerp(p, targetPos, k), Quaternion.Slerp(r, targetRot, k), Vector3.Lerp(s, targetScale, k));
        }

        // ---------- description for the phone ----------

        public SyncObjectInfo Describe(Transform root, string uniqueId)
        {
            Vector3 pos, scale;
            Quaternion rot;
            GetPose(root, out pos, out rot, out scale);

            string shape = SceneSyncProtocol.ShapeBox;
            Vector3 size = Vector3.one * 0.2f, center = Vector3.zero;
            Color color = new Color(0.7f, 0.7f, 0.7f, 1f);

            rendererBuffer.Clear();
            GetComponentsInChildren(true, rendererBuffer);
            var mf = GetComponent<MeshFilter>();
            Renderer own = GetComponent<Renderer>();
            if (mf != null && mf.sharedMesh != null && own != null && rendererBuffer.Count == 1)
            {
                string meshName = mf.sharedMesh.name;
                foreach (string p in Primitives)
                    if (meshName == p) shape = p.ToLowerInvariant();
            }

            Bounds b;
            if (shape != SceneSyncProtocol.ShapeBox && mf != null) b = mf.sharedMesh.bounds;
            else if (!LocalBounds(out b)) b = new Bounds(Vector3.zero, Vector3.one * 0.2f);
            size = b.size;
            center = b.center;

            foreach (var r in rendererBuffer)
            {
                var m = r != null ? r.sharedMaterial : null;
                if (m == null) continue;
                if (m.HasProperty("_Color")) { color = m.GetColor("_Color"); break; }
                if (m.HasProperty("_BaseColor")) { color = m.GetColor("_BaseColor"); break; }
            }
            rendererBuffer.Clear();

            return new SyncObjectInfo
            {
                id = uniqueId,
                name = DisplayName,
                shape = shape,
                color = SceneSyncProtocol.Arr(color),
                size = SceneSyncProtocol.Arr(size),
                center = SceneSyncProtocol.Arr(center),
                pos = SceneSyncProtocol.Arr(pos),
                rot = SceneSyncProtocol.Arr(rot),
                scale = SceneSyncProtocol.Arr(SceneSyncProtocol.ClampScale(scale)),
                locked = locked
            };
        }

        // bounds of all child renderers in this object's own (unscaled) space
        private bool LocalBounds(out Bounds result)
        {
            result = new Bounds();
            bool any = false;
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            foreach (var r in rendererBuffer)
            {
                if (r == null) continue;
                Bounds rb;
                Matrix4x4 m;
                var f = r.GetComponent<MeshFilter>();
                if (f != null && f.sharedMesh != null) { rb = f.sharedMesh.bounds; m = toLocal * r.transform.localToWorldMatrix; }
                else { rb = r.bounds; m = toLocal; }
                Vector3 c = rb.center, e = rb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return any;
        }

        private static Vector3 Div(Vector3 a, Vector3 b)
        {
            return new Vector3(Safe(a.x, b.x), Safe(a.y, b.y), Safe(a.z, b.z));
        }

        private static float Safe(float a, float b) { return Mathf.Abs(b) < 1e-8f ? a : a / b; }
    }
}
