using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    public struct SnapSettings
    {
        public bool on;
        public float move;     // metres
        public float rotate;   // degrees
        public float scale;    // scale value step

        public static float Round(float v, float step) { return step > 0f ? Mathf.Round(v / step) * step : v; }
    }

    // One drag on a gizmo handle. All values are computed from the state at the START of the drag (no drift):
    //   axis move   = closest point between the finger ray and the axis line (screen fallback when the axis points at the camera)
    //   free move   = finger ray on the camera plane through the pivot
    //   rotate      = finger angle around the pivot on the ring plane (accumulated: several turns are fine; screen fallback edge-on)
    //   axis scale  = move along the object's own axis / handle length
    //   uniform     = finger right / up = bigger
    // Pivot / Center: the gizmo sits on the object pivot or on its bounds centre; rotation and scale keep that point fixed.
    public class GizmoDrag
    {
        public GizmoHandle Handle { get; private set; }

        private Camera cam;
        private Transform root;            // SyncWorld root (poses are relative to it)
        private SceneObjectState obj;
        private ScenePose start;
        private SnapSettings snap;
        private Vector2 startPx;
        private float viewHeight, handleLength;
        private Vector3 pivotW, axisW;
        private Vector3 startPosW;
        private Quaternion startRotW;
        private int axis;
        private bool centerMode, screenFallback;
        private float s0;                  // axis parameter at the start
        private Vector3 plane0;            // free move: start hit
        private Vector3 prevRingDir;       // rotate: last direction from the pivot on the ring plane
        private float ringAngle;           // rotate: accumulated degrees
        private Vector2 tangentPerDeg;     // rotate fallback: pixels per degree at the grabbed ring point

        public void Begin(GizmoHandle h, TransformGizmo g, Camera camera, Transform worldRoot, SceneObjectState o,
                          Vector2 px, float viewHeightPx, bool pivotAtCenter, SnapSettings s)
        {
            Handle = h;
            cam = camera;
            root = worldRoot;
            obj = o;
            start = o.pose;
            snap = s;
            startPx = px;
            viewHeight = Mathf.Max(1f, viewHeightPx);
            centerMode = pivotAtCenter;
            pivotW = g.Origin;
            startPosW = root.TransformPoint(start.pos);
            startRotW = root.rotation * start.rot;
            axis = TransformGizmo.AxisOf(h);
            axisW = axis >= 0 ? g.Axis(axis).normalized : Vector3.zero;
            float at = g.Tool == GizmoTool.All ? TransformGizmo.AllScaleAt : TransformGizmo.ScaleCubeAt;
            handleLength = Mathf.Max(1e-5f, g.Size * at);

            Vector3 view = (pivotW - cam.transform.position).normalized;
            switch (h)
            {
                case GizmoHandle.MoveX: case GizmoHandle.MoveY: case GizmoHandle.MoveZ:
                case GizmoHandle.ScaleX: case GizmoHandle.ScaleY: case GizmoHandle.ScaleZ:
                    screenFallback = Mathf.Abs(Vector3.Dot(axisW, view)) > 0.97f;
                    if (!screenFallback) s0 = AxisParam(Ray(px));
                    break;
                case GizmoHandle.MoveFree:
                    if (!PlaneHit(Ray(px), pivotW, cam.transform.forward, out plane0)) plane0 = pivotW;
                    break;
                case GizmoHandle.RotX: case GizmoHandle.RotY: case GizmoHandle.RotZ:
                {
                    ringAngle = 0f;
                    screenFallback = Mathf.Abs(Vector3.Dot(axisW, view)) < 0.15f;
                    Vector3 hit;
                    if (!screenFallback && PlaneHit(Ray(px), pivotW, axisW, out hit)) prevRingDir = Vector3.ProjectOnPlane(hit - pivotW, axisW);
                    else screenFallback = true;
                    // pixels per degree at the grabbed point (fallback and sign reference)
                    Vector3 grab = g.NearestRingPoint(px, axis);
                    Vector2 a, b;
                    g.Project(grab, out a);
                    g.Project(pivotW + Quaternion.AngleAxis(1f, axisW) * (grab - pivotW), out b);
                    tangentPerDeg = b - a;
                    break;
                }
            }
        }

        public ScenePose Update(Vector2 px)
        {
            Vector3 posW = startPosW;
            Quaternion rotW = startRotW;
            Vector3 scale = start.scale;

            switch (Handle)
            {
                case GizmoHandle.MoveX: case GizmoHandle.MoveY: case GizmoHandle.MoveZ:
                {
                    float d = AxisDelta(px);
                    if (snap.on) d = SnapSettings.Round(d, snap.move);
                    posW = startPosW + axisW * d;
                    break;
                }
                case GizmoHandle.MoveFree:
                {
                    Vector3 hit;
                    if (!PlaneHit(Ray(px), pivotW, cam.transform.forward, out hit)) return start;
                    Vector3 delta = root.InverseTransformVector(hit - plane0);
                    if (snap.on) delta = new Vector3(SnapSettings.Round(delta.x, snap.move), SnapSettings.Round(delta.y, snap.move), SnapSettings.Round(delta.z, snap.move));
                    posW = startPosW + root.TransformVector(delta);
                    break;
                }
                case GizmoHandle.RotX: case GizmoHandle.RotY: case GizmoHandle.RotZ:
                {
                    float angle = RingAngle(px);
                    if (snap.on) angle = SnapSettings.Round(angle, snap.rotate);
                    Quaternion q = Quaternion.AngleAxis(angle, axisW);
                    rotW = q * startRotW;
                    posW = pivotW + q * (startPosW - pivotW);
                    break;
                }
                case GizmoHandle.ScaleX: case GizmoHandle.ScaleY: case GizmoHandle.ScaleZ:
                {
                    float f = 1f + AxisDelta(px) / handleLength;
                    float v = start.scale[axis] * f;
                    if (snap.on) v = SnapSettings.Round(v, snap.scale);
                    scale[axis] = v;
                    break;
                }
                case GizmoHandle.ScaleUniform:
                {
                    Vector2 d = px - startPx;
                    float f = Mathf.Exp((d.x + d.y) / viewHeight * 2.5f);
                    scale = start.scale * f;
                    if (snap.on) scale = new Vector3(SnapSettings.Round(scale.x, snap.scale), SnapSettings.Round(scale.y, snap.scale), SnapSettings.Round(scale.z, snap.scale));
                    break;
                }
                default:
                    return start;
            }

            scale = SceneSyncProtocol.ClampScale(scale);
            Vector3 pos = root.InverseTransformPoint(posW);
            Quaternion rot = Quaternion.Inverse(root.rotation) * rotW;
            if (Handle >= GizmoHandle.ScaleX && centerMode)
            {
                // keep the bounds centre where it was
                Vector3 c = obj.CenterOf(start);
                pos = c - start.rot * Vector3.Scale(scale, obj.center);
            }
            return ScenePose.FromRotation(pos, rot, scale, start.euler);
        }

        // ---------- math ----------

        private Ray Ray(Vector2 px) { return cam.ScreenPointToRay(new Vector3(px.x, px.y, 0f)); }

        // metres moved along the axis since the start
        private float AxisDelta(Vector2 px)
        {
            if (screenFallback)
            {
                Vector2 a, b;
                Vector3 sa = cam.WorldToScreenPoint(pivotW), sb = cam.WorldToScreenPoint(pivotW + axisW);
                a = sa; b = sb;
                Vector2 dir = b - a;   // pixels per metre along the axis
                float l2 = dir.sqrMagnitude;
                if (l2 < 1e-4f) return 0f;
                return Vector2.Dot(px - startPx, dir) / l2;
            }
            return AxisParam(Ray(px)) - s0;
        }

        // parameter of the point on the axis line (pivotW + axisW * s) closest to the ray
        private float AxisParam(Ray r)
        {
            Vector3 w0 = pivotW - r.origin;
            float b = Vector3.Dot(axisW, r.direction);
            float d = Vector3.Dot(axisW, w0);
            float e = Vector3.Dot(r.direction, w0);
            float denom = 1f - b * b;
            if (denom < 1e-6f) return 0f;
            return (b * e - d) / denom;
        }

        private float RingAngle(Vector2 px)
        {
            if (screenFallback)
            {
                float l2 = tangentPerDeg.sqrMagnitude;
                return l2 < 1e-6f ? 0f : Vector2.Dot(px - startPx, tangentPerDeg) / l2;
            }
            Vector3 hit;
            if (!PlaneHit(Ray(px), pivotW, axisW, out hit)) return ringAngle;
            Vector3 dir = Vector3.ProjectOnPlane(hit - pivotW, axisW);
            if (dir.sqrMagnitude < 1e-12f || prevRingDir.sqrMagnitude < 1e-12f) return ringAngle;
            ringAngle += Vector3.SignedAngle(prevRingDir, dir, axisW);   // accumulated, so 2 full turns = 720
            prevRingDir = dir;
            return ringAngle;
        }

        private static bool PlaneHit(Ray r, Vector3 p, Vector3 n, out Vector3 hit)
        {
            hit = p;
            float den = Vector3.Dot(n, r.direction);
            if (Mathf.Abs(den) < 1e-5f) return false;
            float t = Vector3.Dot(p - r.origin, n) / den;
            if (t < 0f) return false;
            hit = r.origin + r.direction * t;
            return true;
        }
    }
}
