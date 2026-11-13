using UnityEngine;

namespace MediSimVR
{
    // App launch: logo + "MediSimVR" for DemoConfig.SplashSeconds, then Role selection (no bottom bar, back = quit)
    public class SplashScreen : AppScreen
    {
        public CanvasGroup content;
        private float shown;   // seconds on screen (frame steps capped: the first frames after app start can be long)
        private bool done;

        public override bool ShowsNav { get { return false; } }

        public override void OnShow()
        {
            shown = 0f;
            done = false;
            base.OnShow();
        }

        public override void Refresh() { }

        private void Update()
        {
            if (done || nav == null) return;
            shown += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float t = shown;
            if (content != null) content.alpha = Mathf.Clamp01(t / 0.4f);
            if (t < DemoConfig.SplashSeconds) return;
            done = true;
            nav.Tab(nav.HomeScreen);
        }
    }
}
