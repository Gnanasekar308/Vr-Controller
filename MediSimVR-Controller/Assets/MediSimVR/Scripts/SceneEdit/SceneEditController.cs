using UnityEngine;

namespace MediSimVR
{
    // Editing logic of the Scene tab (no UI): tool settings, gizmo drags, pinch scaling, tap to select, double tap to
    // focus. SceneViewInput calls it; it changes the SceneDocument (which records Undo steps and tells the screen).
    public class SceneEditController
    {
        public readonly SceneDocument Doc;
        public readonly SceneWorldView World;
        public readonly TransformGizmo Gizmo;
        public readonly OrbitCameraRig Rig;
        public readonly Camera Cam;

        public GizmoTool Tool = GizmoTool.Move;
        public bool Global;          // false = Local
        public bool CenterPivot;     // false = Pivot
        public SnapSettings Snap = new SnapSettings { on = false, move = 0.1f, rotate = 15f, scale = 0.1f };

        private GizmoDrag drag;
        private Vector3 pinchStartScale;
        private bool pinching;

        public bool Dragging { get { return drag != null; } }
        public bool Pinching { get { return pinching; } }

        public SceneEditController(SceneDocument doc, SceneWorldView world, TransformGizmo gizmo, OrbitCameraRig rig, Camera cam)
        {
            Doc = doc;
            World = world;
            Gizmo = gizmo;
            Rig = rig;
            Cam = cam;
        }

        public static string ToolName(GizmoTool t) { return t == GizmoTool.All ? "All-in-one" : t.ToString(); }

        // gizmo position / orientation for the selected object (every frame)
        public void UpdateGizmo()
        {
            Gizmo.SetTool(Tool);
            var o = Doc.Selected;
            if (o == null) { Gizmo.Place(Vector3.zero, Quaternion.identity, false, false); return; }
            Transform root = World.transform;
            Vector3 origin = root.TransformPoint(CenterPivot ? o.CenterPoint : o.pose.pos);
            bool local = !Global || Tool == GizmoTool.Scale; // scale handles always follow the object (like Unity)
            Quaternion axes = local ? root.rotation * o.pose.rot : root.rotation;
            Gizmo.Place(origin, axes, true, o.locked);
        }

        // ---------- called by SceneViewInput (pixels in the camera / RenderTexture space) ----------

        public bool TryBeginGizmo(Vector2 px, float viewHeight)
        {
            var o = Doc.Selected;
            if (o == null || o.locked || drag != null) return false;
            UpdateGizmo();
            var h = Gizmo.Pick(px, Tolerance(viewHeight));
            if (h == GizmoHandle.None) return false;
            Debug.Log("[MediSimVR] Scene gizmo " + h + " on " + o.id);
            if (!Doc.BeginEdit(o, Label(h))) return false;
            drag = new GizmoDrag();
            drag.Begin(h, Gizmo, Cam, World.transform, o, px, viewHeight, CenterPivot, Snap);
            Gizmo.SetHot(h);
            return true;
        }

        public void GizmoMove(Vector2 px)
        {
            if (drag == null || !Doc.Editing) return;
            Doc.Preview(drag.Update(px));
        }

        public void GizmoEnd()
        {
            if (drag == null) return;
            drag = null;
            Gizmo.SetHot(GizmoHandle.None);
            Doc.CommitEdit();
        }

        // the finger touched a handle but never really moved: nothing changes, no undo step
        public void GizmoCancel()
        {
            if (drag == null) return;
            drag = null;
            Gizmo.SetHot(GizmoHandle.None);
            Doc.CancelEdit();
        }

        // two fingers starting on the selected (unlocked) object = uniform scale
        public bool TryBeginPinch(Vector2 px)
        {
            var o = Doc.Selected;
            if (o == null || o.locked || drag != null) return false;
            string id;
            if (!World.Pick(Cam.ScreenPointToRay(new Vector3(px.x, px.y, 0f)), out id) || id != o.id) return false;
            if (!Doc.BeginEdit(o, "Scale")) return false;
            pinchStartScale = o.pose.scale;
            pinching = true;
            return true;
        }

        public void Pinch(float totalFactor)
        {
            if (!pinching || !Doc.Editing || float.IsNaN(totalFactor) || totalFactor <= 0f) return;
            var o = Doc.EditObject;
            var start = Doc.EditStart;
            Vector3 s = pinchStartScale * totalFactor;
            if (Snap.on) s = new Vector3(SnapSettings.Round(s.x, Snap.scale), SnapSettings.Round(s.y, Snap.scale), SnapSettings.Round(s.z, Snap.scale));
            var p = start;
            p.scale = MediSimVR.SceneSync.SceneSyncProtocol.ClampScale(s);
            if (CenterPivot) p.pos = o.CenterOf(start) - start.rot * Vector3.Scale(p.scale, o.center);
            Doc.Preview(p);
        }

        public void PinchEnd()
        {
            if (!pinching) return;
            pinching = false;
            Doc.CommitEdit();
        }

        public void Tap(Vector2 px)
        {
            string id;
            Doc.Select(World.Pick(Cam.ScreenPointToRay(new Vector3(px.x, px.y, 0f)), out id) ? id : null);
            Debug.Log("[MediSimVR] Scene tap -> " + (id ?? "nothing"));
        }

        public void DoubleTap(Vector2 px)
        {
            string id;
            bool hit = World.Pick(Cam.ScreenPointToRay(new Vector3(px.x, px.y, 0f)), out id);
            Debug.Log("[MediSimVR] Scene double tap -> " + (hit ? id + " (focus)" : "nothing"));
            if (!hit) return;
            Doc.Select(id);
            FocusSelected();
        }

        public bool FocusSelected()
        {
            var o = Doc.Selected;
            if (o == null) return false;
            Rig.Focus(o.CenterPoint, o.Radius);
            return true;
        }

        // finger-sized: about 4 % of the view height, at least 24 px
        public static float Tolerance(float viewHeight) { return Mathf.Max(24f, viewHeight * 0.04f); }

        private static string Label(GizmoHandle h)
        {
            if (h >= GizmoHandle.ScaleX) return "Scale";
            if (h >= GizmoHandle.RotX) return "Rotate";
            return "Move";
        }
    }
}
