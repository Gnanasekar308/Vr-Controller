using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    // Touch / mouse on the 3D view (the RawImage that shows the RenderTexture). Only pointers that START on the view count.
    //   1 finger on a gizmo handle  = drag that handle (one Undo step when the finger lifts)
    //   1 finger elsewhere          = orbit the camera;  tap = select (empty = deselect);  double tap on an object = focus
    //   2 fingers                   = pan + pinch zoom;  pinch that starts on the selected object = scale it
    // Editor mouse: left = 1 finger, right / middle drag = pan, wheel = zoom, double click = focus.
    // Positions are converted to the camera's pixel space (RenderTexture pixels), the same space the gizmo uses.
    // Sim*() feed the same logic directly with camera pixels (automated tests).
    public class SceneViewInput : MonoBehaviour, IPointerDownHandler, IScrollHandler
    {
        public RawImage view;
        public OrbitCameraRig rig;

        public SceneEditController Controller { get; set; }

        private enum Mode { None, Pending, Orbit, Gizmo, Two, Pinch, Pan, Done }

        private const int MouseLeft = -1, MouseRight = -2, MouseMiddle = -3;
        private const float DoubleTapTime = 0.35f;

        private readonly Dictionary<int, Vector2> points = new Dictionary<int, Vector2>();   // id -> camera pixels
        private readonly List<int> touchIds = new List<int>();     // fingers that went down on the view
        private readonly List<int> buffer = new List<int>();
        private Mode mode;
        private int primary;
        private Vector2 startP, lastP, lastMid, lastTapPos;
        private float lastDist, startDist, lastTapTime = -10f;
        private int mouseButton = int.MinValue;
        private bool gizmoMoved;

        public bool Busy { get { return mode != Mode.None; } }

        // finger movement below this = a tap (pixels)
        private float TapMove { get { return Mathf.Max(8f, ViewHeight * 0.012f); } }

        private float ViewHeight
        {
            get
            {
                var cam = rig != null ? rig.cam : null;
                return cam != null ? cam.pixelHeight : 1000f;
            }
        }

        // ---------- real input ----------

        public void OnPointerDown(PointerEventData e)
        {
            if (e.pointerId >= 0)
            {
                if (!touchIds.Contains(e.pointerId)) touchIds.Add(e.pointerId);
                Vector2 p;
                if (ToCameraPixels(e.position, out p)) Down(e.pointerId, p);
                return;
            }
            if (mouseButton != int.MinValue) return;
            mouseButton = e.button == PointerEventData.InputButton.Left ? MouseLeft : e.button == PointerEventData.InputButton.Right ? MouseRight : MouseMiddle;
            Vector2 mp;
            if (!ToCameraPixels(e.position, out mp)) { mouseButton = int.MinValue; return; }
            if (mouseButton == MouseLeft) Down(MouseLeft, mp);
            else if (mode == Mode.None) { mode = Mode.Pan; primary = mouseButton; lastP = mp; points[mouseButton] = mp; }
        }

        public void OnScroll(PointerEventData e)
        {
            if (rig != null && Mathf.Abs(e.scrollDelta.y) > 0.01f) rig.Zoom(Mathf.Exp(e.scrollDelta.y * 0.1f));
        }

        private void OnDisable()
        {
            if (mode == Mode.Gizmo && Controller != null) Controller.GizmoEnd();
            if (mode == Mode.Pinch && Controller != null) Controller.PinchEnd();
            points.Clear();
            touchIds.Clear();
            mode = Mode.None;
            mouseButton = int.MinValue;
        }

        private void Update()
        {
            if (Input.touchCount > 0 || touchIds.Count > 0) PollTouches();
            PollMouse();
        }

        private void PollTouches()
        {
            buffer.Clear();
            buffer.AddRange(touchIds);
            foreach (int id in buffer)
            {
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.fingerId != id) continue;
                    found = true;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { touchIds.Remove(id); Up(id); }
                    else if (t.phase == TouchPhase.Moved)
                    {
                        Vector2 p;
                        if (ToCameraPixels(t.position, out p)) Move(id, p);
                    }
                }
                if (!found) { touchIds.Remove(id); Up(id); } // the Up event was missed
            }
        }

        private void PollMouse()
        {
            if (mouseButton == int.MinValue) return;
            int b = mouseButton == MouseLeft ? 0 : mouseButton == MouseRight ? 1 : 2;
            Vector2 p;
            bool inside = ToCameraPixels(Input.mousePosition, out p, true);
            if (!Input.GetMouseButton(b))
            {
                int id = mouseButton;
                mouseButton = int.MinValue;
                if (id == MouseLeft) Up(id);
                else if (mode == Mode.Pan) { mode = Mode.None; points.Remove(id); }
                return;
            }
            if (!inside) return;
            if (mouseButton == MouseLeft) Move(MouseLeft, p);
            else if (mode == Mode.Pan)
            {
                if (rig != null) rig.Pan(p - lastP, ViewHeight);
                lastP = p;
            }
        }

        // screen position -> camera pixels; false when outside the view (unless 'clamp')
        public bool ToCameraPixels(Vector2 screen, out Vector2 px, bool clamp = false)
        {
            px = Vector2.zero;
            var cam = rig != null ? rig.cam : null;
            if (view == null || cam == null) return false;
            Vector2 local;
            RectTransform r = view.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, screen, null, out local)) return false;
            Rect rect = r.rect;
            Vector2 n = new Vector2((local.x - rect.xMin) / Mathf.Max(1f, rect.width), (local.y - rect.yMin) / Mathf.Max(1f, rect.height));
            bool inside = n.x >= 0f && n.x <= 1f && n.y >= 0f && n.y <= 1f;
            if (clamp) { n.x = Mathf.Clamp01(n.x); n.y = Mathf.Clamp01(n.y); }
            px = new Vector2(n.x * cam.pixelWidth, n.y * cam.pixelHeight);
            return inside || clamp;
        }

        // camera pixels -> screen position (tests)
        public Vector2 ToScreen(Vector2 px)
        {
            var cam = rig.cam;
            RectTransform r = view.rectTransform;
            Rect rect = r.rect;
            Vector2 local = new Vector2(rect.xMin + px.x / cam.pixelWidth * rect.width, rect.yMin + px.y / cam.pixelHeight * rect.height);
            return RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(local));
        }

        // ---------- automated tests (camera pixels) ----------

        public void SimDown(int id, Vector2 px) { Down(id, px); }
        public void SimMove(int id, Vector2 px) { Move(id, px); }
        public void SimUp(int id) { Up(id); }

        // ---------- gesture logic ----------

        private void Down(int id, Vector2 p)
        {
            points[id] = p;
            if (mode == Mode.None)
            {
                primary = id;
                startP = lastP = p;
                mode = Mode.Pending;
                gizmoMoved = false;
                if (Controller != null && Controller.TryBeginGizmo(p, ViewHeight)) mode = Mode.Gizmo;
                return;
            }
            // second finger: pinch / pan. Also when the first finger landed on a handle but has not moved yet
            // (two fingers never touch at exactly the same time).
            bool handleNotMoved = mode == Mode.Gizmo && !gizmoMoved;
            if ((mode == Mode.Pending || mode == Mode.Orbit || handleNotMoved) && points.Count == 2)
            {
                if (handleNotMoved && Controller != null) Controller.GizmoCancel();
                Vector2 a, b;
                TwoPoints(out a, out b);
                lastMid = (a + b) * 0.5f;
                lastDist = startDist = Mathf.Max(1f, (b - a).magnitude);
                mode = Controller != null && Controller.TryBeginPinch(lastMid) ? Mode.Pinch : Mode.Two;
            }
            // more fingers / a second finger during a gizmo drag: ignored
        }

        private void Move(int id, Vector2 p)
        {
            if (!points.ContainsKey(id)) return;
            points[id] = p;
            switch (mode)
            {
                case Mode.Pending:
                    if (id == primary && (p - startP).magnitude > TapMove)
                    {
                        mode = Mode.Orbit;
                        if (rig != null) rig.Orbit(p - lastP, ViewHeight);
                        lastP = p;
                    }
                    break;
                case Mode.Orbit:
                    if (id != primary) break;
                    if (rig != null) rig.Orbit(p - lastP, ViewHeight);
                    lastP = p;
                    break;
                case Mode.Gizmo:
                    if (id != primary || Controller == null) break;
                    if (!gizmoMoved && (p - startP).magnitude <= TapMove) break; // dead zone: a shaking finger does not move the object
                    gizmoMoved = true;
                    Controller.GizmoMove(p);
                    break;
                case Mode.Two:
                case Mode.Pinch:
                {
                    if (points.Count < 2) break;
                    Vector2 a, b;
                    TwoPoints(out a, out b);
                    Vector2 mid = (a + b) * 0.5f;
                    float dist = Mathf.Max(1f, (b - a).magnitude);
                    if (mode == Mode.Two)
                    {
                        if (rig != null)
                        {
                            rig.Pan(mid - lastMid, ViewHeight);
                            rig.Zoom(dist / lastDist);
                        }
                    }
                    else if (Controller != null) Controller.Pinch(dist / startDist);
                    lastMid = mid;
                    lastDist = dist;
                    break;
                }
            }
        }

        private void Up(int id)
        {
            Vector2 p;
            bool had = points.TryGetValue(id, out p);
            points.Remove(id);
            if (!had) return;
            switch (mode)
            {
                case Mode.Pending:
                    if (id != primary) break;
                    float now = Time.unscaledTime;
                    if (now - lastTapTime < DoubleTapTime && (p - lastTapPos).magnitude < ViewHeight * 0.06f)
                    {
                        lastTapTime = -10f;
                        if (Controller != null) Controller.DoubleTap(p);
                    }
                    else
                    {
                        lastTapTime = now;
                        lastTapPos = p;
                        if (Controller != null) Controller.Tap(p);
                    }
                    mode = Mode.Done;
                    break;
                case Mode.Gizmo:
                {
                    if (id != primary) break;
                    mode = Mode.Done;
                    if (Controller == null) break;
                    if (gizmoMoved) { Controller.GizmoEnd(); break; }
                    // touched a handle without moving: no change. Right after a tap on the same spot = double tap (focus),
                    // because the first tap selected the object and its gizmo now sits under the finger.
                    Controller.GizmoCancel();
                    float t = Time.unscaledTime;
                    if (t - lastTapTime < DoubleTapTime && (p - lastTapPos).magnitude < ViewHeight * 0.06f)
                    {
                        lastTapTime = -10f;
                        Controller.DoubleTap(p);
                    }
                    break;
                }
                case Mode.Pinch:
                    if (Controller != null) Controller.PinchEnd();
                    mode = Mode.Done;
                    break;
                case Mode.Orbit:
                case Mode.Two:
                    mode = Mode.Done;
                    break;
            }
            if (mode == Mode.Done && points.Count == 0) mode = Mode.None;
        }

        private void TwoPoints(out Vector2 a, out Vector2 b)
        {
            a = b = Vector2.zero;
            int k = 0;
            foreach (var kv in points)
            {
                if (k == 0) a = kv.Value; else if (k == 1) b = kv.Value;
                if (++k == 2) break;
            }
        }
    }
}
