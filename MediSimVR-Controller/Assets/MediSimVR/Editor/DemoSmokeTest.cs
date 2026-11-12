using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MediSimVR.EditorTools
{
    // Demo mode smoke test (batchmode, no -quit; exit code 0 = no errors):
    //   Unity.exe -batchmode -projectPath <phone project> -executeMethod MediSimVR.EditorTools.DemoSmokeTest.Run -logFile <log>
    // Rebuilds the UI scene, plays Main.unity with demo mode ON and runs DemoTour (every main screen, host / player session,
    // text chat, scene, summary, every theme preset) by pressing the app's own buttons. Counts errors / exceptions in the log.
    // The demo mode setting of this Editor is restored afterwards.
    [InitializeOnLoad]
    public static class DemoSmokeTest
    {
        const string Tag = "[DEMO-TEST] ";
        const string RunKey = "MSVR_DEMO_SMOKE_RUN";
        const string PrevKey = "MSVR_DEMO_SMOKE_PREV";

        static int errors, started;
        static bool running;
        static float deadline;

        static DemoSmokeTest()
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) Begin(); };
            if (EditorApplication.isPlaying) EditorApplication.delayCall += Begin;
        }

        [MenuItem("MediSimVR/Run Demo Smoke Test")]
        public static void Run()
        {
            SessionState.SetInt(PrevKey, PlayerPrefs.GetInt(DemoConfig.PrefKey, 1));
            DemoConfig.Enabled = true;
            MediSimUIBuilder.Build();
            EditorSceneManager.OpenScene("Assets/MediSimVR/Scenes/Main.unity");
            SessionState.SetBool(RunKey, true);
            Debug.Log(Tag + "entering Play mode (demo ON)");
            EditorApplication.EnterPlaymode();
        }

        static void Begin()
        {
            if (running) return;
            running = true;
            errors = 0;
            started = 0;
            Application.logMessageReceived += OnLog;
            deadline = Time.realtimeSinceStartup + 240f;
            EditorApplication.update += Tick;
            Debug.Log(Tag + "started");
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (msg.StartsWith(Tag)) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                Debug.Log(Tag + "ERROR SEEN: " + msg);
            }
        }

        static void Tick()
        {
            if (!running) return;
            if (Time.realtimeSinceStartup > deadline) { Finish("timeout"); return; }
            if (started == 0)
            {
                var tour = UnityEngine.Object.FindObjectOfType<DemoTour>();
                if (tour == null || AppRoot.Service == null) return;
                if (!AppRoot.Demo) { Finish("demo mode is not ON in Play mode"); return; }
                started = 1;
                Debug.Log(Tag + "services: " + AppRoot.Service.GetType().Name + " + " + (AppRoot.Session != null ? AppRoot.Session.GetType().Name : "no session") +
                          ", Quests " + AppRoot.Service.Devices.Count + ", files " + AppRoot.Service.Files.Count);
                if (!tour.Run(true)) Finish("the demo tour did not start");
                return;
            }
            if (DemoTour.Running) return;
            Finish(null);
        }

        static void Finish(string problem)
        {
            running = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.EraseBool(RunKey);
            PlayerPrefs.SetInt(DemoConfig.PrefKey, SessionState.GetInt(PrevKey, 1));
            PlayerPrefs.Save();
            bool ok = problem == null && errors == 0 && DemoTour.Steps >= 20;
            Debug.Log(Tag + "RESULT screens " + DemoTour.Steps + ", errors " + errors + (problem != null ? ", " + problem : "") + (ok ? "  -> PASSED" : "  -> FAILED"));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
