using System;
using System.Collections.Generic;
using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // Demo mode: the Scene tab works without any receiver. The online demo Quests are the "connected" targets and the
    // demo scene (DemoConfig.SceneObjects, same ids as the PC / Quest demo objects) arrives after a short delay.
    // Nothing is sent anywhere.
    public class DemoSceneLink : ISceneLink
    {
        public event Action TargetsChanged;
        public event Action<List<SyncObjectInfo>, string> SceneReceived;

        private readonly List<string> names = new List<string>();
        private float sceneAt = -1f;
        private string only = "";

        public IReadOnlyList<string> TargetNames { get { return names; } }
        public string Error { get { return null; } }
        public bool HasScene { get; private set; }
        public bool WaitingForScene { get { return !HasScene && sceneAt >= 0f; } }
        public int Updates { get; private set; }   // object states the screen handed over (demo: not sent)

        // Session "Open in Scene" for one Quest: only that Quest is shown as connected ("" = all online Quests)
        public void ShowOnly(string questName)
        {
            only = questName ?? "";
            Refresh();
        }

        public bool Start()
        {
            Refresh();
            if (!HasScene && sceneAt < 0f) sceneAt = Time.unscaledTime + DemoConfig.SceneLoadSeconds;
            return true;
        }

        public void Tick(float now)
        {
            if (!HasScene && sceneAt >= 0f && now >= sceneAt)
            {
                HasScene = true;
                Debug.Log("[MediSimVR] Demo scene loaded (" + DemoConfig.SceneObjects().Count + " objects)");
                if (SceneReceived != null) SceneReceived(DemoConfig.SceneObjects(), DemoConfig.SceneSourceName);
            }
        }

        public List<string> SetManualIps(string text) { return new List<string>(); }

        public void SetObjectState(string id, Vector3 pos, Quaternion rot, Vector3 scale, bool locked) { Updates++; }

        private void Refresh()
        {
            names.Clear();
            if (only.Length > 0) names.Add(only);
            else if (AppRoot.Service != null)
                foreach (var d in AppRoot.Service.Devices) if (d.Online) names.Add(d.Name);
            if (TargetsChanged != null) TargetsChanged();
        }

        public void Dispose() { }
    }
}
