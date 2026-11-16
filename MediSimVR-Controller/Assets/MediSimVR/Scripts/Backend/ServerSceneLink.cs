using System;
using System.Collections.Generic;
using System.Net;
using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // Scene tab through the Medisim PC server (demo mode OFF, MedisimBackend.UseServer):
    //   the stations publish their room objects to the server; the phone loads them from GET /api/scene
    //   and sends object poses to POST /api/scene/state (changed objects, at most ~15 times per second).
    // There is ONE shared scene: every station applies the same poses (same as SceneSync).
    public class ServerSceneLink : ISceneLink
    {
        private const float SendGap = 1f / 15f;
        private const float SceneRetry = 1f;
        private const float TargetsEvery = 1f;

        public event Action TargetsChanged;
        public event Action<List<SyncObjectInfo>, string> SceneReceived;

        private readonly MedisimServer server = MedisimServer.Instance;
        private readonly List<string> names = new List<string>();
        private readonly Dictionary<string, ScenePoseDto> dirty = new Dictionary<string, ScenePoseDto>();
        private bool started, asking, sending;
        private float nextAsk, nextSend, nextTargets;

        public IReadOnlyList<string> TargetNames { get { return names; } }
        public string Error { get; private set; }
        public bool HasScene { get; private set; }
        public bool WaitingForScene { get { return !HasScene && server.HasServer && names.Count > 0; } }

        public bool Start()
        {
            if (!started)
            {
                started = true;
                server.Start();
                Debug.Log("[MediSimVR] Scene through the PC server: " + server.Status);
            }

            return true;
        }

        public void Tick(float now)
        {
            if (!started) return;

            if (now >= nextTargets)
            {
                nextTargets = now + TargetsEvery;
                UpdateTargets();
            }

            if (!HasScene)
            {
                if (!asking && server.HasServer && now >= nextAsk)
                {
                    nextAsk = now + SceneRetry;
                    AskScene();
                }

                return;
            }

            if (dirty.Count > 0 && !sending && now >= nextSend)
            {
                nextSend = now + SendGap;
                SendPoses();
            }
        }

        // The Scene tab's link button: the IP of the PC server. Returns the tokens that are not IPv4 addresses.
        public List<string> SetManualIps(string text)
        {
            var bad = new List<string>();
            string first = "";

            foreach (string raw in (text ?? "").Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tok = raw.Trim();
                IPAddress a;
                if (tok.Split('.').Length == 4 && IPAddress.TryParse(tok, out a)) { if (first.Length == 0) first = tok; }
                else bad.Add(tok);
            }

            if (first != server.ManualIp && (first.Length > 0 || server.ManualIp.Length > 0))
            {
                server.SetManualIp(first);
                HasScene = false; // load the scene from the new server
            }

            return bad;
        }

        public void SetObjectState(string id, Vector3 pos, Quaternion rot, Vector3 scale, bool locked)
        {
            if (string.IsNullOrEmpty(id)) return;
            scale = SceneSyncProtocol.ClampScale(scale);
            dirty[id] = new ScenePoseDto
            {
                id = id,
                pos = SceneSyncProtocol.Arr(pos),
                rot = SceneSyncProtocol.Arr(rot),
                scale = SceneSyncProtocol.Arr(scale),
                locked = locked
            };
        }

        private void UpdateTargets()
        {
            var now = new List<string>();

            if (server.HasServer && AppRoot.Service != null)
            {
                foreach (var d in AppRoot.Service.Devices) if (d.Online) now.Add(d.Name);
            }

            Error = server.HasServer ? null : server.Status;
            bool same = now.Count == names.Count;
            for (int i = 0; same && i < now.Count; i++) same = now[i] == names[i];
            if (same) return;

            names.Clear();
            names.AddRange(now);
            if (TargetsChanged != null) TargetsChanged();
        }

        private void AskScene()
        {
            asking = true;
            server.Request("GET", "/api/scene", null, r =>
            {
                asking = false;
                if (HasScene || !r.Ok || r.Code == 204 || string.IsNullOrEmpty(r.Text)) return;

                ServerScene scene = null;
                try { scene = JsonUtility.FromJson<ServerScene>(r.Text); }
                catch (Exception e) { Debug.LogWarning("[MediSimVR] Bad scene from the server: " + e.Message); }
                if (scene == null || scene.objects == null) return;

                var list = new List<SyncObjectInfo>();
                foreach (var o in scene.objects) if (o != null && o.IsValid()) list.Add(o);

                HasScene = true;
                Debug.Log("[MediSimVR] Scene from " + scene.source + " through the PC server: " + list.Count + " object(s)");
                if (SceneReceived != null) SceneReceived(list, scene.source);
            });
        }

        private void SendPoses()
        {
            var list = new List<ScenePoseDto>(dirty.Values);
            dirty.Clear();
            sending = true;

            server.Request("POST", "/api/scene/state", JsonUtility.ToJson(new SceneStateRequest { objects = list.ToArray() }), r =>
            {
                sending = false;

                if (!r.Ok)
                {
                    // keep the newest pose of each object for the next try
                    foreach (var p in list) if (!dirty.ContainsKey(p.id)) dirty[p.id] = p;
                    Debug.LogWarning("[MediSimVR] Scene state not sent: " + r);
                }
            }, 3000);
        }

        public void Dispose()
        {
            dirty.Clear();
        }
    }
}
