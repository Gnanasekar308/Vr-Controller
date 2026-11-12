using System;
using System.Collections.Generic;
using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // What the Scene tab needs from "the receivers": SceneSyncClient (real network) or DemoSceneLink (demo mode).
    public interface ISceneLink : IDisposable
    {
        event Action TargetsChanged;
        event Action<List<SyncObjectInfo>, string> SceneReceived;   // object list + source name

        IReadOnlyList<string> TargetNames { get; }
        string Error { get; }
        bool HasScene { get; }
        bool WaitingForScene { get; }

        bool Start();
        void Tick(float now);
        List<string> SetManualIps(string text);
        void SetObjectState(string id, Vector3 pos, Quaternion rot, Vector3 scale, bool locked);
    }
}
