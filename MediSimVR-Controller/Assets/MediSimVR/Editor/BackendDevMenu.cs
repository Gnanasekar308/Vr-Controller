using UnityEditor;
using UnityEngine;

namespace MediSimVR.EditorTools
{
    // Editor-only helper for testing with the real backend (demo mode OFF).
    // The Scene screen has no button in the app UI yet, so this opens it while the app plays in the Editor.
    public static class BackendDevMenu
    {
        const string OpenSceneMenu = "MediSimVR/Backend/Open Scene Screen (Play mode)";

        [MenuItem(OpenSceneMenu)]
        static void OpenScene()
        {
            var nav = Object.FindObjectOfType<Navigator>();
            if (nav == null) { Debug.LogWarning("[MediSimVR] No Navigator: press Play in Main.unity first."); return; }
            nav.Push(ScreenId.Scene);
        }

        [MenuItem(OpenSceneMenu, true)]
        static bool CanOpenScene() { return Application.isPlaying; }
    }
}
