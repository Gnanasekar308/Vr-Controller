using System.IO;
using MediSimVR.SceneSync;
using MediSimVR.SceneSync.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MediSimVR.EditorTools
{
    // Menu: MediSimVR > Build PC Sync Scene
    // Creates Assets/MediSimVR/Scenes/PCSync.unity: the same SyncWorld as the phone view, the demo objects with
    // SyncObject (same ids as the Quest receiver), SyncReceiver (kind "pc") on the SyncWorld root, and PCSyncStatus
    // (OnGUI: PC name, IP, port, phone connected). Open it and press Play on the PC.
    // NOT added to the build settings: the phone APK contains only Main.unity.
    public static class PCSyncSceneBuilder
    {
        public const string ScenePath = "Assets/MediSimVR/Scenes/PCSync.unity";

        [MenuItem("MediSimVR/Build PC Sync Scene")]
        public static void Build()
        {
            bool batch = Application.isBatchMode;
            if (!batch && File.Exists(ScenePath) && !EditorUtility.DisplayDialog("MediSimVR",
                "PCSync.unity already exists. Rebuild it?", "Rebuild", "Cancel")) return;
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var w = SyncWorldBuilder.Build(Vector3.zero, "Main Camera", new Color(0.10f, 0.12f, 0.16f));
            w.camera.gameObject.tag = "MainCamera";

            SyncDemoObjects.Build(w.world);
            var receiver = w.world.gameObject.AddComponent<SyncReceiver>();
            var so = new SerializedObject(receiver);
            so.FindProperty("kind").stringValue = SceneSyncProtocol.KindPc;
            so.ApplyModifiedPropertiesWithoutUndo();

            var app = new GameObject("PCSync");
            var status = app.AddComponent<PCSyncStatus>();
            status.receiver = receiver;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            // safety: the phone build must never contain this scene
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path == ScenePath) Debug.LogError("[MediSimVR] PCSync.unity is in the build settings, remove it");
            Debug.Log("[MediSimVR] PC sync scene built: " + ScenePath + " (" + w.world.GetComponentsInChildren<SyncObject>().Length +
                      " SyncObjects, not in build settings)");
            if (!batch) EditorUtility.DisplayDialog("MediSimVR", "PC sync scene ready:\n" + ScenePath +
                "\n\nPress Play, allow Windows Firewall, then open the Scene tab on the phone.", "OK");
        }
    }
}
