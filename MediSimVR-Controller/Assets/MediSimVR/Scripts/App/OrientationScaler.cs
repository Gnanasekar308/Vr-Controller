using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // The UI is designed for a 1080 x 2340 portrait canvas. When the phone is landscape (only the Live session turns),
    // the reference becomes 2340 x 1080, so the canvas is as tall as a landscape phone and the sizes stay the same.
    [RequireComponent(typeof(CanvasScaler))]
    public class OrientationScaler : MonoBehaviour
    {
        public Vector2 portrait = new Vector2(1080f, 2340f);
        private CanvasScaler scaler;
        private bool? wasLandscape;

        private void Awake() { scaler = GetComponent<CanvasScaler>(); }

        private void Update()
        {
            bool land = Screen.width > Screen.height;
            if (wasLandscape == land) return;
            wasLandscape = land;
            scaler.referenceResolution = land ? new Vector2(portrait.y, portrait.x) : portrait;
        }
    }
}
