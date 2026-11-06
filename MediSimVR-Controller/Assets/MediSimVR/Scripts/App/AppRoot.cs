using System;
using UnityEngine;

namespace MediSimVR
{
    // Entry point (on the "App" object). Swap MockControllerService for the real one later.
    public class AppRoot : MonoBehaviour
    {
        public static IControllerService Service { get; private set; }
        public static FilePicker Picker { get; private set; }

        public Navigator navigator;

        void Awake()
        {
            Debug.Log("[MediSimVR] AppRoot.Awake");
            // data first: nothing below may leave the app without a service
            Service = new NetworkControllerService();
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
            if (Input.GetKeyDown(KeyCode.Escape) && navigator != null) navigator.Back(); // Android back button
        }

        void OnDestroy()
        {
            var d = Service as IDisposable;
            if (d != null) d.Dispose();
        }
    }
}
