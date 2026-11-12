using UnityEngine;

namespace MediSimVR
{
    public enum CameraView { Persp, Top, Front, Side }

    // Camera of the phone's 3D view: orbit around a pivot, pan, zoom, focus on an object, Top / Front / Side / 3D views.
    // Distances are unlimited (near / far clip follow the distance), so an object far away can always be focused again.
    public class OrbitCameraRig : MonoBehaviour
    {
        public Transform worldRoot;        // SyncWorld root (pivot is relative to it)
        public Camera cam;
        public Vector3 pivot = new Vector3(0f, 0.4f, 0f);
        public float yaw = -30f, pitch = 28f, distance = 4.6f;
        public float degreesPerViewHeight = 220f;   // orbit speed: a drag over the full view height turns this many degrees

        public static readonly Vector3 HomePivot = new Vector3(0f, 0.4f, 0f);
        public const float HomeYaw = -30f, HomePitch = 28f, HomeDistance = 4.6f;
        public const float MinDistance = 0.02f, MaxDistance = 100000f;

        private bool animating;
        private float animT;
        private Vector3 fromPivot, toPivot;
        private float fromYaw, toYaw, fromPitch, toPitch, fromDist, toDist;

        public bool Animating { get { return animating; } }

        private void LateUpdate()
        {
            if (animating)
            {
                animT += Time.unscaledDeltaTime / 0.25f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animT));
                pivot = Vector3.Lerp(fromPivot, toPivot, k);
                yaw = Mathf.LerpAngle(fromYaw, toYaw, k);
                pitch = Mathf.Lerp(fromPitch, toPitch, k);
                distance = Mathf.Exp(Mathf.Lerp(Mathf.Log(fromDist), Mathf.Log(toDist), k));
                if (animT >= 1f) animating = false;
            }
            Apply();
        }

        public void Apply()
        {
            if (cam == null) return;
            Quaternion r = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 p = worldRoot != null ? worldRoot.TransformPoint(pivot) : pivot;
            cam.transform.SetPositionAndRotation(p - r * Vector3.forward * distance, (worldRoot != null ? worldRoot.rotation : Quaternion.identity) * r);
            cam.nearClipPlane = Mathf.Clamp(distance * 0.004f, 0.002f, 50f);
            cam.farClipPlane = Mathf.Max(200f, distance * 250f);
        }

        // ---------- gestures (deltas in view pixels) ----------

        public void Orbit(Vector2 deltaPx, float viewHeightPx)
        {
            StopAnim();
            float k = degreesPerViewHeight / Mathf.Max(1f, viewHeightPx);
            yaw += deltaPx.x * k;
            pitch = Mathf.Clamp(pitch - deltaPx.y * k, -90f, 90f);
            Apply();
        }

        public void Pan(Vector2 deltaPx, float viewHeightPx)
        {
            StopAnim();
            float mpp = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, viewHeightPx);
            Quaternion r = Quaternion.Euler(pitch, yaw, 0f);
            pivot -= (r * Vector3.right * deltaPx.x + r * Vector3.up * deltaPx.y) * mpp;
            Apply();
        }

        // factor > 1 = closer
        public void Zoom(float factor)
        {
            if (float.IsNaN(factor) || factor <= 0f) return;
            StopAnim();
            distance = Mathf.Clamp(distance / factor, MinDistance, MaxDistance);
            Apply();
        }

        // centre + radius relative to the SyncWorld root
        public void Focus(Vector3 center, float radius)
        {
            // the narrower of the vertical and horizontal field of view (a portrait view is narrow sideways)
            float half = cam != null ? cam.fieldOfView * 0.5f * Mathf.Deg2Rad : 0.44f;
            if (cam != null && cam.aspect > 0f && cam.aspect < 1f) half = Mathf.Atan(Mathf.Tan(half) * cam.aspect);
            float d = Mathf.Clamp(radius / Mathf.Sin(half) * 1.15f, MinDistance * 5f, MaxDistance);
            AnimateTo(center, yaw, pitch, d);
        }

        public void SetView(CameraView v)
        {
            switch (v)
            {
                case CameraView.Top: AnimateTo(pivot, 0f, 90f, distance); break;
                case CameraView.Front: AnimateTo(pivot, 0f, 0f, distance); break;
                case CameraView.Side: AnimateTo(pivot, -90f, 0f, distance); break;
                default: AnimateTo(pivot, HomeYaw, HomePitch, distance); break;
            }
        }

        public void Home()
        {
            StopAnim();
            pivot = HomePivot;
            yaw = HomeYaw;
            pitch = HomePitch;
            distance = HomeDistance;
            Apply();
        }

        public void AnimateTo(Vector3 p, float y, float pi, float d)
        {
            fromPivot = pivot; toPivot = p;
            fromYaw = yaw; toYaw = y;
            fromPitch = pitch; toPitch = Mathf.Clamp(pi, -90f, 90f);
            fromDist = Mathf.Max(distance, MinDistance); toDist = Mathf.Clamp(d, MinDistance, MaxDistance);
            animT = 0f;
            animating = true;
        }

        // finish an animation at once (tests, or before a gesture)
        public void Finish()
        {
            if (!animating) return;
            animT = 1f;
            pivot = toPivot; yaw = toYaw; pitch = toPitch; distance = toDist;
            animating = false;
            Apply();
        }

        private void StopAnim()
        {
            animating = false;
        }
    }
}
