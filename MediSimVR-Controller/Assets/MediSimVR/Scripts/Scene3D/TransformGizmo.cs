using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    public enum GizmoTool { Move, Rotate, Scale, All }
    public enum GizmoHandle { None, MoveX, MoveY, MoveZ, MoveFree, RotX, RotY, RotZ, ScaleX, ScaleY, ScaleZ, ScaleUniform }

    // Unity-style handles on the selected object, always drawn on top, constant size on screen:
    //   Move   = red / green / blue arrows (X / Y / Z) + centre ball (free move on the camera plane)
    //   Rotate = one ring per axis
    //   Scale  = axis handles with cubes + centre cube (uniform)
    //   All    = arrows (outer part of each axis) + scale cubes (inner part) + rings + centre ball
    // Pick() works in the camera's pixel space (the RenderTexture), with a finger-sized tolerance.
    public class TransformGizmo : MonoBehaviour
    {
        public Material material;     // MediSimVR/Gizmo (unlit, on top); colours via MaterialPropertyBlock
        public Mesh cylinderMesh, cubeMesh, sphereMesh;
        public Camera cam;
        [Tooltip("Arrow length as a fraction of the view height.")]
        public float screenFraction = 0.17f;

        // layout in gizmo units (1 = arrow length)
        public const float ShaftStart = 0.14f, ConeStart = 0.80f, ConeLen = 0.20f, ShaftRadius = 0.022f, ConeRadius = 0.075f;
        public const float RingRadius = 0.92f, ScaleCubeAt = 0.86f, ScaleCube = 0.13f, CenterSize = 0.17f;
        public const float AllArrowStart = 0.66f, AllScaleAt = 0.42f, AllScaleCube = 0.11f, AllRingRadius = 1.24f;

        public static readonly Color XColor = new Color(0.93f, 0.26f, 0.22f), YColor = new Color(0.42f, 0.80f, 0.22f), ZColor = new Color(0.22f, 0.48f, 0.96f);
        public static readonly Color HotColor = new Color(1f, 0.82f, 0.10f), LockedColor = new Color(0.55f, 0.56f, 0.60f, 0.55f);

        public GizmoTool Tool { get; private set; }
        public bool Visible { get; private set; }
        public float Size { get; private set; }              // world length of one arrow this frame
        public Vector3 Origin { get { return origin; } }
        public Quaternion Axes { get { return axes; } }
        public GizmoHandle Hot { get { return hot; } }

        private class Part
        {
            public GizmoHandle handle;
            public Renderer r;
            public Color color;
        }

        private readonly List<Part> parts = new List<Part>();
        private readonly List<Vector2> ringBuffer = new List<Vector2>();
        private Transform holder;
        private MaterialPropertyBlock mpb;
        private Vector3 origin;
        private Quaternion axes = Quaternion.identity;
        private GizmoHandle hot;
        private bool lockedLook, built;

        public static Vector3 LocalAxis(int i) { return i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward; }
        public static Color AxisColor(int i) { return i == 0 ? XColor : i == 1 ? YColor : ZColor; }
        public Vector3 Axis(int i) { return axes * LocalAxis(i); }

        public static int AxisOf(GizmoHandle h)
        {
            switch (h)
            {
                case GizmoHandle.MoveX: case GizmoHandle.RotX: case GizmoHandle.ScaleX: return 0;
                case GizmoHandle.MoveY: case GizmoHandle.RotY: case GizmoHandle.ScaleY: return 1;
                case GizmoHandle.MoveZ: case GizmoHandle.RotZ: case GizmoHandle.ScaleZ: return 2;
                default: return -1;
            }
        }

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            holder = new GameObject("Handles").transform;
            holder.SetParent(transform, false);
            holder.gameObject.SetActive(false);
        }

        public void SetTool(GizmoTool t)
        {
            if (built && t == Tool) return;
            Tool = t;
            Rebuild();
        }

        public void Place(Vector3 originWorld, Quaternion axesWorld, bool visible, bool locked)
        {
            if (!built) Rebuild();
            origin = originWorld;
            axes = axesWorld;
            Visible = visible;
            lockedLook = locked;
            if (holder.gameObject.activeSelf != visible) holder.gameObject.SetActive(visible);
            UpdateTransform();
        }

        public void SetHot(GizmoHandle h) { hot = h; }

        private void LateUpdate()
        {
            if (!Visible) return;
            UpdateTransform();
            foreach (var p in parts)
            {
                Color c = lockedLook ? LockedColor : (p.handle != GizmoHandle.None && p.handle == hot ? HotColor : p.color);
                mpb.SetColor("_Color", c);
                p.r.SetPropertyBlock(mpb);
            }
        }

        public void UpdateTransform()
        {
            if (cam == null) return;
            Vector3 toObj = origin - cam.transform.position;
            float depth = Mathf.Max(Vector3.Dot(toObj, cam.transform.forward), cam.nearClipPlane * 2f);
            Size = depth * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * screenFraction;
            transform.SetPositionAndRotation(origin, axes);
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(Size / parentScale.x, Size / parentScale.y, Size / parentScale.z);
        }

        // ---------- visuals ----------

        private void Rebuild()
        {
            built = true;
            foreach (var p in parts) if (p.r != null) Destroy(p.r.gameObject);
            parts.Clear();
            for (int i = 0; i < 3; i++)
            {
                Vector3 d = LocalAxis(i);
                Color c = AxisColor(i);
                switch (Tool)
                {
                    case GizmoTool.Move:
                        Shaft(d, ShaftStart, ConeStart, ShaftRadius, c, GizmoHandle.MoveX + i);
                        Cone(d, ConeStart, c, GizmoHandle.MoveX + i);
                        break;
                    case GizmoTool.Rotate:
                        Ring(d, RingRadius, c, GizmoHandle.RotX + i);
                        break;
                    case GizmoTool.Scale:
                        Shaft(d, ShaftStart, ScaleCubeAt - ScaleCube * 0.5f, ShaftRadius, c, GizmoHandle.ScaleX + i);
                        Box(d * ScaleCubeAt, ScaleCube, c, GizmoHandle.ScaleX + i);
                        break;
                    default: // All
                        Shaft(d, ShaftStart, AllScaleAt, ShaftRadius * 0.6f, c, GizmoHandle.ScaleX + i);
                        Box(d * AllScaleAt, AllScaleCube, c, GizmoHandle.ScaleX + i);
                        Shaft(d, AllArrowStart, ConeStart, ShaftRadius, c, GizmoHandle.MoveX + i);
                        Cone(d, ConeStart, c, GizmoHandle.MoveX + i);
                        Ring(d, AllRingRadius, c, GizmoHandle.RotX + i);
                        break;
                }
            }
            if (Tool == GizmoTool.Scale) Box(Vector3.zero, CenterSize, new Color(0.88f, 0.88f, 0.90f), GizmoHandle.ScaleUniform);
            else if (Tool == GizmoTool.Rotate) Ball(CenterSize * 0.45f, new Color(1f, 1f, 1f, 0.8f), GizmoHandle.None);
            else Ball(CenterSize, new Color(1f, 1f, 1f, 0.9f), GizmoHandle.MoveFree);
        }

        private Renderer Add(string name, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Color c, GizmoHandle h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            parts.Add(new Part { handle = h, r = r, color = c });
            return r;
        }

        private void Shaft(Vector3 dir, float from, float to, float radius, Color c, GizmoHandle h)
        {
            // Unity cylinder: height 2 along Y, radius 0.5
            Add("Shaft" + h, cylinderMesh, dir * ((from + to) * 0.5f), Quaternion.FromToRotation(Vector3.up, dir),
                new Vector3(radius * 2f, (to - from) * 0.5f, radius * 2f), c, h);
        }

        private void Cone(Vector3 dir, float at, Color c, GizmoHandle h)
        {
            Add("Cone" + h, GizmoMeshes.Cone, dir * at, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(ConeRadius * 2f, ConeLen, ConeRadius * 2f), c, h);
        }

        private void Ring(Vector3 normal, float radius, Color c, GizmoHandle h)
        {
            Add("Ring" + h, GizmoMeshes.Torus, Vector3.zero, Quaternion.FromToRotation(Vector3.up, normal), Vector3.one * radius, c, h);
        }

        private void Box(Vector3 pos, float size, Color c, GizmoHandle h)
        {
            Add("Box" + h, cubeMesh, pos, Quaternion.identity, Vector3.one * size, c, h);
        }

        private void Ball(float size, Color c, GizmoHandle h)
        {
            Add("Ball" + h, sphereMesh, Vector3.zero, Quaternion.identity, Vector3.one * size, c, h);
        }

        // ---------- picking (camera pixels) ----------

        public bool Project(Vector3 world, out Vector2 px)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            px = new Vector2(s.x, s.y);
            return s.z > 0f;
        }

        public GizmoHandle Pick(Vector2 px, float tol)
        {
            if (!Visible || cam == null) return GizmoHandle.None;
            UpdateTransform();
            GizmoHandle best = GizmoHandle.None;
            float bestD = float.MaxValue;
            Vector2 c;
            if (!Project(origin, out c)) return GizmoHandle.None;

            GizmoHandle centre = Tool == GizmoTool.Rotate ? GizmoHandle.None : Tool == GizmoTool.Scale ? GizmoHandle.ScaleUniform : GizmoHandle.MoveFree;
            float dc = (px - c).magnitude;
            if (centre != GizmoHandle.None && dc <= tol * 1.2f) { best = centre; bestD = dc * 0.8f; }

            for (int i = 0; i < 3; i++)
            {
                Vector3 d = Axis(i) * Size;
                switch (Tool)
                {
                    case GizmoTool.Move:
                        Consider(SegmentDistance(px, origin + d * ShaftStart, origin + d), tol, GizmoHandle.MoveX + i, ref best, ref bestD);
                        break;
                    case GizmoTool.Scale:
                        Consider(SegmentDistance(px, origin + d * ShaftStart, origin + d * (ScaleCubeAt + ScaleCube * 0.5f)), tol, GizmoHandle.ScaleX + i, ref best, ref bestD);
                        break;
                    case GizmoTool.Rotate:
                        Consider(RingDistance(px, Axis(i), RingRadius * Size), tol, GizmoHandle.RotX + i, ref best, ref bestD);
                        break;
                    default:
                        Consider(SegmentDistance(px, origin + d * AllArrowStart, origin + d), tol, GizmoHandle.MoveX + i, ref best, ref bestD);
                        Consider(PointDistance(px, origin + d * AllScaleAt), tol, GizmoHandle.ScaleX + i, ref best, ref bestD);
                        Consider(RingDistance(px, Axis(i), AllRingRadius * Size), tol, GizmoHandle.RotX + i, ref best, ref bestD);
                        break;
                }
            }
            return best;
        }

        private static void Consider(float d, float tol, GizmoHandle h, ref GizmoHandle best, ref float bestD)
        {
            if (d <= tol && d < bestD) { best = h; bestD = d; }
        }

        private float PointDistance(Vector2 px, Vector3 world)
        {
            Vector2 p;
            return Project(world, out p) ? (px - p).magnitude : float.MaxValue;
        }

        private float SegmentDistance(Vector2 px, Vector3 a, Vector3 b)
        {
            Vector2 pa, pb;
            if (!Project(a, out pa) || !Project(b, out pb)) return float.MaxValue;
            return DistToSegment(px, pa, pb);
        }

        // ring around 'normal' (world), radius in world units
        private float RingDistance(Vector2 px, Vector3 normal, float radius)
        {
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(normal, u);
            ringBuffer.Clear();
            const int n = 48;
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector2 p;
                ringBuffer.Add(Project(origin + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius, out p) ? p : new Vector2(float.NaN, float.NaN));
            }
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = ringBuffer[i], b = ringBuffer[i + 1];
                if (float.IsNaN(a.x) || float.IsNaN(b.x)) continue;
                best = Mathf.Min(best, DistToSegment(px, a, b));
            }
            return best;
        }

        // world point on the ring of 'axis' nearest to the pixel (start of a rotate drag)
        public Vector3 NearestRingPoint(Vector2 px, int axis)
        {
            float radius = (Tool == GizmoTool.All ? AllRingRadius : RingRadius) * Size;
            Vector3 normal = Axis(axis);
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(normal, u);
            Vector3 best = origin + u * radius;
            float bestD = float.MaxValue;
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2f / 72;
                Vector3 w = origin + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                Vector2 p;
                if (!Project(w, out p)) continue;
                float d = (p - px).sqrMagnitude;
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-6f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
