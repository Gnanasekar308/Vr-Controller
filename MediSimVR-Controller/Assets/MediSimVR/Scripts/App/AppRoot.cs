using System;
using UnityEngine;

namespace MediSimVR
{
    // Entry point (on the "App" object). The UI only talks to the service interfaces:
    //   demo mode ON (default, DemoConfig): MockControllerService + MockSessionService, no device or network needed
    //   demo mode OFF: NetworkControllerService (real Quests); Session = null until the real backend exists
    public class AppRoot : MonoBehaviour
    {
        public static IControllerService Service { get; private set; }
        public static ISessionService Session { get; private set; }
        public static bool Demo { get; private set; }
        public static FilePicker Picker { get; private set; }

        public Navigator navigator;

        void Awake()
        {
            Demo = DemoConfig.Enabled;
            Debug.Log("[MediSimVR] AppRoot.Awake, demo mode " + (Demo ? "ON" : "OFF"));
            // data first: nothing below may leave the app without a service
            if (Demo)
            {
                var mock = new MockControllerService();
                Service = mock;
                Session = new MockSessionService(mock, mock.NotifyChanged);
            }
            else
            {
                Service = new NetworkControllerService();
                Session = null;
            }
            Picker = GetComponent<FilePicker>();
            if (Picker == null) Picker = gameObject.AddComponent<FilePicker>();
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            try { ThemeManager.Load(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void Start()
        {
            if (navigator == null) { Debug.LogError("[MediSimVR] AppRoot.navigator is not set. Run MediSimVR > Build UI Scene again."); return; }
            navigator.Init();
        }

        void Update()
        {
            if (Service == null) return;
            Service.Tick(Time.unscaledDeltaTime);
            if (Session != null) Session.Tick(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.Escape) && navigator != null) navigator.Back(); // Android back button
        }

        void OnDestroy()
        {
            var d = Service as IDisposable;
            if (d != null) d.Dispose();
            var s = Session as IDisposable;
            if (s != null) s.Dispose();
        }

        // Settings > Demo mode: store the choice and rebuild the app with the other services
        public static void SetDemoAndRestart(bool on)
        {
            DemoConfig.Enabled = on;
            Debug.Log("[MediSimVR] demo mode " + (on ? "ON" : "OFF") + ", reloading the app");
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}
