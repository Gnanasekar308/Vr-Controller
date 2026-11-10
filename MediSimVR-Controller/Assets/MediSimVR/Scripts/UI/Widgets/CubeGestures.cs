using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MediSimVR
{
    public enum GestureMode { Move, Rotate, Scale }

    // Touch / mouse control of the synced cube, on the 3D view (RawImage). Only touches that START on the view count.
    //   Move:   1 finger = slide on the floor (camera-relative), 2 fingers up/down = height
    //   Rotate: 1 finger = turn (left/right = yaw, up/down = tilt), 2-finger twist = spin
    //   Scale:  1 finger up/down
    //   Pinch = scale in every mode.
    // Editor mouse: left-drag = 1 finger, right-drag = 2 fingers (up/down; left/right = twist), wheel = pinch.
    public class CubeGestures : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
    {
        public Transform target;       // the cube (local values are synced)
        public Camera viewCamera;      // camera that renders the view
        public RectTransform view;     // the RawImage rect (pixel -> metre conversion)
        public GestureMode mode = GestureMode.Move;

        public Vector3 minPos = new Vector3(-2f, 0f, -2f);   // floor is 4 m x 4 m
        public Vector3 maxPos = new Vector3(2f, 3f, 2f);
        public float minScale = 0.1f, maxScale = 3f;
        public float degPerPixel = 0.35f;

        public event Action Changed;   // the user changed the cube

        private readonly List<int> fingers = new List<int>();
        private readonly List<Touch> active = new List<Touch>();
        private bool mouseLeft, mouseRight, hadTwo;
        private Vector2 lastMouse, lastMid, lastOne;
        private float lastDist, lastAngle;
        private int lastOneId = -999;
        private Canvas canvas;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.pointerId >= 0) { if (!fingers.Contains(e.pointerId)) fingers.Add(e.pointerId); return; }
            if (e.button == PointerEventData.InputButton.Left) mouseLeft = true;
            else if (e.button == PointerEventData.InputButton.Right) mouseRight = true;
            lastMouse = e.position;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId >= 0) { fingers.Remove(e.pointerId); return; }
            if (e.button == PointerEventData.InputButton.Left) mouseLeft = false;
            else if (e.button == PointerEventData.InputButton.Right) mouseRight = false;
        }

        public void OnScroll(PointerEventData e)
        {
            if (ScaleBy(Mathf.Exp(e.scrollDelta.y * 0.08f))) Fire();
        }

        private void OnDisable()
        {
            fingers.Clear();
            mouseLeft = mouseRight = hadTwo = false;
            lastOneId = -999;
        }

        private void Update()
        {
            if (target == null || viewCamera == null) return;
            bool changed = Input.touchCount > 0 ? Touches() : Mouse();
            if (changed) Fire();
        }

        private void Fire() { if (Changed != null) Changed(); }

        // ---------- input ----------

        private bool Touches()
        {
            active.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (fingers.Contains(t.fingerId) && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) active.Add(t);
            }
            for (int i = fingers.Count - 1; i >= 0; i--) // a finger whose Up event was missed
            {
                bool present = false;
                for (int k = 0; k < Input.touchCount; k++) if (Input.GetTouch(k).fingerId == fingers[i]) present = true;
                if (!present) fingers.RemoveAt(i);
            }

            bool changed = false;
            if (active.Count >= 2)
            {
                Vector2 a = active[0].position, b = active[1].position;
                Vector2 mid = (a + b) * 0.5f;
                float dist = (b - a).magnitude;
                float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                if (hadTwo) changed = TwoFingers(mid - lastMid, lastDist > 1f ? dist / lastDist : 1f, Mathf.DeltaAngle(lastAngle, ang));
                hadTwo = true;
                lastMid = mid;
                lastDist = dist;
                lastAngle = ang;
                lastOneId = -999; // going back to one finger starts fresh (no jump)
            }
            else if (active.Count == 1)
            {
                hadTwo = false;
                Touch t = active[0];
                if (lastOneId == t.fingerId) changed = OneFinger(t.position - lastOne);
                lastOneId = t.fingerId;
                lastOne = t.position;
            }
            else
            {
                hadTwo = false;
                lastOneId = -999;
            }
            return changed;
        }

        private bool Mouse()
        {
            if (!mouseLeft && !mouseRight) return false;
            if (!Input.GetMouseButton(0)) mouseLeft = false;
            if (!Input.GetMouseButton(1)) mouseRight = false;
            Vector2 p = Input.mousePosition;
            Vector2 d = p - lastMouse;
            lastMouse = p;
            if (d.sqrMagnitude < 0.01f) return false;
            if (mouseLeft) return OneFinger(d);
            if (mouseRight) return TwoFingers(new Vector2(0f, d.y), 1f, -d.x * 0.5f);
            return false;
        }

        // ---------- gestures ----------

        private bool OneFinger(Vector2 d)
        {
            if (d.sqrMagnitude < 0.01f) return false;
            switch (mode)
            {
                case GestureMode.Move:
                {
                    Transform c = viewCamera.transform;
                    Vector3 right = Flat(c.right), fwd = Flat(c.forward);
                    Vector3 world = (right * d.x + fwd * d.y) * MetresPerPixel();
                    return MoveBy(world);
                }
                case GestureMode.Rotate:
                {
                    Vector3 up = target.parent != null ? target.parent.up : Vector3.up;
                    Quaternion q = Quaternion.AngleAxis(-d.x * degPerPixel, up) *
                                   Quaternion.AngleAxis(d.y * degPerPixel, viewCamera.transform.right);
                    target.rotation = q * target.rotation;
                    return true;
                }
                default:
                    return ScaleBy(Mathf.Exp(d.y * 0.005f));
            }
        }

        private bool TwoFingers(Vector2 dMid, float pinch, float twist)
        {
            bool changed = false;
            if (Mathf.Abs(pinch - 1f) > 0.0005f) changed |= ScaleBy(pinch); // pinch = scale in every mode
            if (mode == GestureMode.Move && Mathf.Abs(dMid.y) > 0.01f)
            {
                Vector3 up = target.parent != null ? target.parent.up : Vector3.up;
                changed |= MoveBy(up * dMid.y * MetresPerPixel());
            }
            else if (mode == GestureMode.Rotate && Mathf.Abs(twist) > 0.01f)
            {
                target.rotation = Quaternion.AngleAxis(twist, viewCamera.transform.forward) * target.rotation;
                changed = true;
            }
            return changed;
        }

        private bool MoveBy(Vector3 worldDelta)
        {
            Vector3 local = target.parent != null ? target.parent.InverseTransformVector(worldDelta) : worldDelta;
            Vector3 p = target.localPosition + local;
            p = new Vector3(Mathf.Clamp(p.x, minPos.x, maxPos.x), Mathf.Clamp(p.y, minPos.y, maxPos.y), Mathf.Clamp(p.z, minPos.z, maxPos.z));
            if ((p - target.localPosition).sqrMagnitude < 1e-10f) return false;
            target.localPosition = p;
            return true;
        }

        private bool ScaleBy(float f)
        {
            if (target == null || float.IsNaN(f) || f <= 0f) return false;
            Vector3 s = target.localScale * f;
            float big = Mathf.Max(s.x, Mathf.Max(s.y, s.z)), small = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            if (big > maxScale) s *= maxScale / big;
            if (small < minScale) s *= minScale / small;
            if ((s - target.localScale).sqrMagnitude < 1e-10f) return false;
            target.localScale = s;
            return true;
        }

        // metres on the cube's depth plane per screen pixel of the view, so the cube follows the finger
        private float MetresPerPixel()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            float viewPx = view != null ? view.rect.height * scaleFactor : Screen.height;
            float dist = Vector3.Distance(viewCamera.transform.position, target.position);
            return 2f * dist * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, viewPx);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }
    }
}
