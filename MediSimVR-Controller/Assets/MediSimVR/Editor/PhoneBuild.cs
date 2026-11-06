using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MediSimVR.EditorTools
{
    // MediSimVR > Build Phone APK  (batchmode: -executeMethod MediSimVR.EditorTools.PhoneBuild.Build)
    // Phone controller only. NEVER add XR to this project (OpenXR pre-init makes the phone screen black).
    public static class PhoneBuild
    {
        const string Output = "Builds/MediSimVR-Controller.apk";

        [MenuItem("MediSimVR/Build Phone APK")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.medisim.vr");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory("Builds");
            var opt = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MediSimVR/Scenes/Main.unity" },
                locationPathName = Output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport r = BuildPipeline.BuildPlayer(opt);
            Debug.Log("[MediSimVR] Build result: " + r.summary.result + ", " + r.summary.totalSize / (1024 * 1024) + " MB -> " + Output);
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
