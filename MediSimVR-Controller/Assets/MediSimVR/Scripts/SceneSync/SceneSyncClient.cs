using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // Phone side of SceneSync v2 (UDP 8771, signed messages, see SceneSyncProtocol).
    //  - discover every second (broadcast + manual IPs); every receiver that answers "here" (kind pc / quest) is a target.
    //  - the FIRST receiver that answers is the scene source: the phone asks it for the object list ("getscene"),
    //    then pushes its state to every other receiver so all of them match.
    //  - each object's state goes to ALL targets: changed objects at most ~30 / s, a keep-alive round robin every 0.5 s,
    //    and the full state to every receiver that appears later.
    // Receiving + signature check run on a background thread (pure .NET, no Unity API); Tick() (main thread) parses,
    // logs and sends. Nothing here blocks the main thread.
    public class SceneSyncClient : IDisposable
    {
        public class Target
        {
            public string Name = "";
            public string Kind = "";
            public IPEndPoint End;
            public float LastSeen;
            public int Count;
            public bool NeedsFull;
        }

        private class NetObj
        {
            public string Id;
            public Vector3 Pos, Scale;
            public Quaternion Rot;
            public bool Locked, Dirty;
        }

        public const float DiscoverEvery = 1f;      // broadcast + manual IPs
        public const float KeepAliveEvery = 0.5f;   // round robin of all objects, even when nothing changed
        public const int KeepAlivePerTick = 8;      // objects per keep-alive tick
        public const float MinSendGap = 1f / 30f;   // changed objects: max ~30 sends / s
        public const float LostAfter = 3f;          // no "here" for this long = receiver gone
        public const float SceneRetryEvery = 1f;    // "getscene" again if the list is not complete
        public const int MaxTargets = 16;

        public event Action TargetsChanged;
        public event Action<List<SyncObjectInfo>, string> SceneReceived;   // object list + source name

        public IReadOnlyList<Target> Targets { get { return targets; } }
        public string Error { get; private set; }
        public int ManualCount { get { return manual.Count; } }
        public int LocalPort { get; private set; }
        public int Sid { get; private set; }
        public bool HasScene { get; private set; }
        public string SourceName { get; private set; } = "";
        public bool WaitingForScene { get { return !HasScene && source != null; } }
        public int SentTransforms { get; private set; }

        private readonly List<Target> targets = new List<Target>();
        private readonly List<IPAddress> manual = new List<IPAddress>();
        private readonly ConcurrentQueue<KeyValuePair<IPEndPoint, string>> inbox = new ConcurrentQueue<KeyValuePair<IPEndPoint, string>>(); // verified bodies
        private readonly ConcurrentQueue<string> warnings = new ConcurrentQueue<string>(); // from the receive thread, logged in Tick()
        private readonly List<NetObj> net = new List<NetObj>();
        private readonly Dictionary<string, NetObj> netById = new Dictionary<string, NetObj>();
        private List<IPAddress> broadcast = new List<IPAddress>();
        private UdpClient udp;
        private Thread rx;
        private volatile bool running;
        private int dropped, rxErrors, lastRxErrorTick;
        private int seq, roundRobin, sceneTries;
        private float nextDiscover, nextBroadcastRefresh, nextStartTry, nextKeepAlive, nextSceneRequest;
        private float lastDirtySend = -100f, lastSendErrorLog = -100f;
        private Target source;
        private List<SyncObjectInfo>[] parts;

        public SceneSyncClient()
        {
            Sid = new System.Random().Next(1, int.MaxValue);
        }

        // ---------- setup ----------

        public bool Start()
        {
            if (running) return true;
            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)); // any free port; receivers answer to it
                udp.EnableBroadcast = true;
                LocalPort = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                running = true;
                Error = null;
                rx = new Thread(RxLoop) { IsBackground = true, Name = "SceneSync-Phone" };
                rx.Start();
                broadcast = BroadcastAddresses();
                Debug.Log("[MediSimVR] SceneSync client on UDP " + LocalPort + ", discover to " + Join(broadcast) +
                          (manual.Count > 0 ? " + manual " + Join(manual) : ""));
                return true;
            }
            catch (Exception e)
            {
                running = false;
                Error = e.Message;
                Debug.LogError("[MediSimVR] SceneSync client could not open a UDP socket: " + e);
                try { if (udp != null) udp.Close(); } catch (Exception) { }
                udp = null;
                return false;
            }
        }

        // "192.168.1.20, 192.168.1.31" -> manual targets. Returns the tokens that are not IPv4 addresses.
        public List<string> SetManualIps(string text)
        {
            List<string> bad;
            var list = ParseIps(text, out bad);
            manual.Clear();
            manual.AddRange(list);
            nextDiscover = 0f; // ask the new IPs at once
            return bad;
        }

        public static List<IPAddress> ParseIps(string text, out List<string> bad)
        {
            var ok = new List<IPAddress>();
            bad = new List<string>();
            foreach (string raw in (text ?? "").Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tok = raw.Trim();
                IPAddress a;
                if (tok.Split('.').Length == 4 && IPAddress.TryParse(tok, out a) && a.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (!ok.Contains(a)) ok.Add(a);
                }
                else bad.Add(tok);
            }
            return ok;
        }

        // The state the receivers must have for one object (pose relative to SyncWorld + lock). Sent with the next Tick.
        public void SetObjectState(string id, Vector3 pos, Quaternion rot, Vector3 scale, bool locked)
        {
            NetObj o;
            if (id == null || !netById.TryGetValue(id, out o)) return;
            o.Pos = pos;
            o.Rot = rot;
            o.Scale = SceneSyncProtocol.ClampScale(scale);
            o.Locked = locked;
            o.Dirty = true;
        }

        // ---------- main thread ----------

        public void Tick(float now)
        {
            if (!running)
            {
                if (now < nextStartTry) return;
                nextStartTry = now + 3f;
                if (!Start()) return;
            }

            string w;
            while (warnings.TryDequeue(out w)) Debug.LogWarning(w);

            bool changed = false;
            KeyValuePair<IPEndPoint, string> kv;
            int n = 0;
            while (n++ < 64 && inbox.TryDequeue(out kv))
            {
                SceneSyncMessage m;
                string err;
                if (!SceneSyncProtocol.TryParseBody(kv.Value, out m, out err)) { Debug.LogWarning("[MediSimVR] SceneSync ignored message from " + kv.Key + ": " + err); continue; }
                if (m.type == SceneSyncProtocol.TypeHere) changed |= OnHere(kv.Key, m, now);
                else if (m.type == SceneSyncProtocol.TypeObjects) OnObjects(kv.Key, m);
            }

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (now - targets[i].LastSeen <= LostAfter) continue;
                Debug.Log("[MediSimVR] SceneSync lost " + targets[i].Name + " " + targets[i].End);
                if (targets[i] == source) { source = null; parts = null; }
                targets.RemoveAt(i);
                changed = true;
            }

            if (now >= nextBroadcastRefresh)
            {
                nextBroadcastRefresh = now + 10f; // Wi-Fi may change
                broadcast = BroadcastAddresses();
            }

            if (now >= nextDiscover)
            {
                nextDiscover = now + DiscoverEvery;
                byte[] d = Encoding.UTF8.GetBytes(SceneSyncProtocol.BuildDiscover());
                foreach (var a in broadcast) SendTo(d, new IPEndPoint(a, SceneSyncProtocol.Port), now);
                foreach (var a in manual) SendTo(d, new IPEndPoint(a, SceneSyncProtocol.Port), now);
            }

            if (!HasScene) RequestScene(now);
            else if (targets.Count > 0) SendStates(now);

            if (changed && TargetsChanged != null) TargetsChanged();
        }

        private bool OnHere(IPEndPoint from, SceneSyncMessage m, float now)
        {
            if (m.kind != SceneSyncProtocol.KindPc && m.kind != SceneSyncProtocol.KindQuest) return false;
            string name = string.IsNullOrEmpty(m.name) ? from.Address.ToString() : m.name;
            bool changed = false;

            Target t = null;
            foreach (var x in targets) if (x.End.Equals(from)) { t = x; break; }
            if (t == null)
            {
                // the same receiver seen on a second address (PC with two networks, or its IP changed)
                foreach (var x in targets)
                {
                    if (x.Name != name || x.Kind != m.kind) continue;
                    if (now - x.LastSeen < 1.5f) return false; // still answering on its first address: keep that one
                    Debug.Log("[MediSimVR] SceneSync " + name + " moved " + x.End + " -> " + from);
                    x.End = from;
                    x.LastSeen = now;
                    x.NeedsFull = HasScene;
                    return true;
                }
            }
            if (t == null)
            {
                if (targets.Count >= MaxTargets) return false;
                t = new Target { End = from, NeedsFull = HasScene }; // a late receiver gets the whole scene state
                targets.Add(t);
                changed = true;
                Debug.Log("[MediSimVR] SceneSync found " + name + " (" + m.kind + ", " + m.count + " objects) at " + from + ", receivers now " + targets.Count);
            }
            if (t.Name != name || t.Kind != m.kind) changed = true;
            t.Name = name;
            t.Kind = m.kind;
            t.Count = m.count;
            t.LastSeen = now;

            if (!HasScene && source == null)
            {
                source = t; // the first receiver that answers is the source of the scene
                parts = null;
                sceneTries = 0;
                nextSceneRequest = 0f;
                Debug.Log("[MediSimVR] SceneSync scene source: " + name);
                changed = true;
            }
            return changed;
        }

        private void RequestScene(float now)
        {
            if (source == null && targets.Count > 0)
            {
                source = targets[0];
                parts = null;
                sceneTries = 0;
            }
            if (source == null || now < nextSceneRequest) return;
            if (sceneTries >= 5 && targets.Count > 1)
            {
                // no complete list from this receiver: try the next one
                int i = targets.IndexOf(source);
                source = targets[(i + 1) % targets.Count];
                parts = null;
                sceneTries = 0;
                Debug.LogWarning("[MediSimVR] SceneSync no object list yet, asking " + source.Name + " instead");
            }
            nextSceneRequest = now + SceneRetryEvery;
            sceneTries++;
            SendTo(Encoding.UTF8.GetBytes(SceneSyncProtocol.BuildGetScene()), source.End, now);
            Debug.Log("[MediSimVR] SceneSync getscene -> " + source.Name + " " + source.End + " (try " + sceneTries + ")");
        }

        private void OnObjects(IPEndPoint from, SceneSyncMessage m)
        {
            if (HasScene || source == null || !from.Equals(source.End)) return;
            if (m.parts <= 0 || m.parts > 64 || m.part < 0 || m.part >= m.parts) return;
            if (parts == null || parts.Length != m.parts) parts = new List<SyncObjectInfo>[m.parts];
            var list = new List<SyncObjectInfo>();
            if (m.objs != null) foreach (var o in m.objs) if (o != null && o.IsValid()) list.Add(o);
            parts[m.part] = list;
            foreach (var p in parts) if (p == null) return; // wait for the other parts

            var all = new List<SyncObjectInfo>();
            var seen = new HashSet<string>();
            foreach (var p in parts) foreach (var o in p) if (seen.Add(o.id)) all.Add(o);
            parts = null;
            HasScene = true;
            SourceName = source.Name;
            net.Clear();
            netById.Clear();
            foreach (var o in all)
            {
                var no = new NetObj { Id = o.id, Pos = o.Pos, Rot = o.Rot, Scale = o.Scale, Locked = o.locked };
                net.Add(no);
                netById[o.id] = no;
            }
            foreach (var t in targets) t.NeedsFull = t != source; // everybody else gets the source's state
            Debug.Log("[MediSimVR] SceneSync scene from " + source.Name + ": " + all.Count + " objects (sent total " + m.total + ")");
            if (SceneReceived != null) SceneReceived(all, source.Name);
        }

        private void SendStates(float now)
        {
            // 1) changed objects -> all receivers, at most ~30 / s
            if (now - lastDirtySend >= MinSendGap)
            {
                bool any = false;
                foreach (var o in net)
                {
                    if (!o.Dirty) continue;
                    o.Dirty = false;
                    any = true;
                    SendObject(o, null, now);
                }
                if (any) lastDirtySend = now;
            }

            // 2) a receiver that appeared after the scene arrived gets every object now
            foreach (var t in targets)
            {
                if (!t.NeedsFull) continue;
                t.NeedsFull = false;
                foreach (var o in net) SendObject(o, t.End, now);
                Debug.Log("[MediSimVR] SceneSync full state (" + net.Count + " objects) -> " + t.Name);
            }

            // 3) keep-alive: a few objects per tick, round robin, so lost packets are repaired
            if (now >= nextKeepAlive && net.Count > 0)
            {
                nextKeepAlive = now + KeepAliveEvery;
                int count = Math.Min(KeepAlivePerTick, net.Count);
                for (int i = 0; i < count; i++)
                {
                    roundRobin = (roundRobin + 1) % net.Count;
                    SendObject(net[roundRobin], null, now);
                }
            }
        }

        // to == null: every target
        private void SendObject(NetObj o, IPEndPoint to, float now)
        {
            seq++;
            byte[] b = Encoding.UTF8.GetBytes(SceneSyncProtocol.BuildTransform(Sid, seq, o.Id, o.Pos, o.Rot, o.Scale, o.Locked));
            if (to != null) SendTo(b, to, now);
            else foreach (var t in targets) SendTo(b, t.End, now);
            SentTransforms++;
        }

        private void SendTo(byte[] data, IPEndPoint to, float now)
        {
            UdpClient c = udp;
            if (c == null) return;
            try { c.Send(data, data.Length, to); }
            catch (Exception e)
            {
                if (now - lastSendErrorLog > 5f) // a missing route to one address must not spam the log
                {
                    lastSendErrorLog = now;
                    Debug.LogWarning("[MediSimVR] SceneSync send to " + to + " failed: " + e.Message);
                }
            }
        }

        // ---------- background thread ----------

        private void RxLoop()
        {
            UdpClient client = udp;
            while (running)
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                byte[] data;
                try { data = client.Receive(ref from); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException e)
                {
                    if (!running) break;
                    if (e.SocketErrorCode == SocketError.ConnectionReset) continue; // ICMP port unreachable (Windows / Editor)
                    RxError("socket error " + e.SocketErrorCode + ": " + e.Message);
                    Thread.Sleep(50);
                    continue;
                }
                catch (Exception e)
                {
                    if (!running) break;
                    RxError(e.GetType().Name + ": " + e.Message);
                    Thread.Sleep(50);
                    continue;
                }

                if (data == null || data.Length == 0 || data.Length > SceneSyncProtocol.MaxPacketBytes) { Drop(from, "bad size"); continue; }
                string text;
                try { text = Encoding.UTF8.GetString(data); }
                catch (Exception) { Drop(from, "not UTF-8"); continue; }
                string body; // signature check here (pure .NET); JSON parsing happens in Tick() on the main thread
                if (!SceneSyncProtocol.TryVerify(text, out body)) { Drop(from, "bad or missing sig"); continue; }
                if (inbox.Count < 256) inbox.Enqueue(new KeyValuePair<IPEndPoint, string>(from, body));
            }
        }

        // background thread: at most one log line per 5 s, so a broken network is visible without flooding logcat
        private void RxError(string what)
        {
            int n = Interlocked.Increment(ref rxErrors);
            int now = Environment.TickCount;
            if (n == 1 || unchecked(now - lastRxErrorTick) > 5000)
            {
                lastRxErrorTick = now;
                Warn("[MediSimVR] SceneSync receive failed (" + n + " total): " + what);
            }
        }

        private void Drop(IPEndPoint from, string why)
        {
            int k = Interlocked.Increment(ref dropped);
            if (k == 1 || k % 50 == 0) Warn("[MediSimVR] SceneSync dropped packet from " + from + ": " + why + " (total " + k + ")");
        }

        // background thread: no Unity API here, Tick() logs the line on the main thread
        private void Warn(string line)
        {
            if (warnings.Count < 100) warnings.Enqueue(line);
        }

        // ---------- helpers ----------

        // 255.255.255.255 + the directed broadcast of every IPv4 interface (Android does not always send the global one).
        private static List<IPAddress> BroadcastAddresses()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        byte[] ip = ua.Address.GetAddressBytes();
                        if (ip[0] == 127 || (ip[0] == 169 && ip[1] == 254)) continue;
                        byte[] mask = null;
                        try { if (ua.IPv4Mask != null) mask = ua.IPv4Mask.GetAddressBytes(); } catch (Exception) { }
                        if (mask == null || mask.Length != 4 || (mask[0] | mask[1] | mask[2] | mask[3]) == 0) mask = new byte[] { 255, 255, 255, 0 };
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                        var a = new IPAddress(b);
                        if (!list.Contains(a)) list.Add(a);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MediSimVR] SceneSync could not read network interfaces: " + e.Message);
            }
            return list;
        }

        private static string Join(List<IPAddress> l)
        {
            var sb = new StringBuilder();
            foreach (var a in l) { if (sb.Length > 0) sb.Append(", "); sb.Append(a); }
            return sb.ToString();
        }

        public void Dispose()
        {
            running = false;
            try { if (udp != null) udp.Close(); } catch (Exception) { }
            udp = null;
            if (rx != null && rx.IsAlive) rx.Join(300);
            rx = null;
            targets.Clear();
        }
    }
}
