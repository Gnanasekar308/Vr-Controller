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
    // Phone side of SceneSync (UDP 8771, signed messages, see SceneSyncProtocol).
    // Multi-target: every receiver that answers "here" (kind "pc" or "quest") is kept and gets every transform.
    // First "here" (before the user touched the cube) = the phone adopts that receiver's cube state and sends it to all.
    // Receiving + signature check run on a background thread (pure .NET, no Unity API); Tick() (main thread) parses replies,
    // logs and sends. Nothing here blocks the main thread.
    public class SceneSyncClient : IDisposable
    {
        public class Target
        {
            public string Name = "";
            public string Kind = "";
            public IPEndPoint End;
            public float LastSeen;
        }

        public const float DiscoverEvery = 1f;      // broadcast + manual IPs
        public const float KeepAliveEvery = 0.5f;   // transform even when nothing changed
        public const float MinSendGap = 1f / 30f;   // max ~30 transforms / s while changing
        public const float LostAfter = 3f;          // no "here" for this long = receiver gone
        public const int MaxTargets = 16;

        public event Action<Vector3, Quaternion, Vector3, string> StateAdopted; // pose + receiver name
        public event Action TargetsChanged;

        public IReadOnlyList<Target> Targets { get { return targets; } }
        public string Error { get; private set; }
        public int ManualCount { get { return manual.Count; } }

        private readonly List<Target> targets = new List<Target>();
        private readonly List<IPAddress> manual = new List<IPAddress>();
        private readonly ConcurrentQueue<KeyValuePair<IPEndPoint, string>> inbox = new ConcurrentQueue<KeyValuePair<IPEndPoint, string>>(); // verified bodies
        private readonly ConcurrentQueue<string> warnings = new ConcurrentQueue<string>(); // from the receive thread, logged in Tick()
        private List<IPAddress> broadcast = new List<IPAddress>();
        private UdpClient udp;
        private Thread rx;
        private volatile bool running;
        private int dropped, rxErrors, lastRxErrorTick;
        private int seq;
        private bool dirty, adoptAllowed = true;
        private float nextDiscover, nextBroadcastRefresh, nextStartTry, lastSend = -100f, lastSendErrorLog = -100f;
        private Vector3 pos = new Vector3(0f, 0.5f, 0f);
        private Quaternion rot = Quaternion.identity;
        private Vector3 scale = Vector3.one;

        // ---------- setup ----------

        public bool Start()
        {
            if (running) return true;
            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)); // any free port; receivers answer to it
                udp.EnableBroadcast = true;
                running = true;
                Error = null;
                rx = new Thread(RxLoop) { IsBackground = true, Name = "SceneSync-Phone" };
                rx.Start();
                broadcast = BroadcastAddresses();
                Debug.Log("[MediSimVR] SceneSync client on UDP " + ((IPEndPoint)udp.Client.LocalEndPoint).Port +
                          ", discover to " + Join(broadcast) + (manual.Count > 0 ? " + manual " + Join(manual) : ""));
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

        // The phone's current cube pose. userChange = the user moved / loaded / reset it, so no "here" state is adopted any more.
        public void SetPose(Vector3 p, Quaternion r, Vector3 s, bool userChange)
        {
            pos = p;
            rot = r;
            scale = s;
            dirty = true;
            if (userChange) adoptAllowed = false;
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
            }

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (now - targets[i].LastSeen <= LostAfter) continue;
                Debug.Log("[MediSimVR] SceneSync lost " + targets[i].Name + " " + targets[i].End);
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

            if (targets.Count > 0 && ((dirty && now - lastSend >= MinSendGap) || now - lastSend >= KeepAliveEvery))
            {
                seq++;
                byte[] t = Encoding.UTF8.GetBytes(SceneSyncProtocol.BuildTransform(seq, pos, rot, scale));
                foreach (var tg in targets) SendTo(t, tg.End, now);
                lastSend = now;
                dirty = false;
            }

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
                if (targets.Count >= MaxTargets) return false;
                t = new Target { End = from };
                targets.Add(t);
                changed = true;
                Debug.Log("[MediSimVR] SceneSync found " + name + " (" + m.kind + ") at " + from + ", receivers now " + targets.Count);
            }
            if (t.Name != name || t.Kind != m.kind) changed = true;
            t.Name = name;
            t.Kind = m.kind;
            t.LastSeen = now;

            Vector3 p, s;
            Quaternion r;
            if (adoptAllowed && m.TryGetPos(out p) && m.TryGetRot(out r) && m.TryGetScale(out s))
            {
                adoptAllowed = false;
                pos = p;
                rot = r;
                scale = s;
                dirty = true;
                lastSend = -100f; // send it to everybody now
                Debug.Log("[MediSimVR] SceneSync adopted cube state from " + name + ": pos " + p.ToString("F3") +
                          " rot " + r.eulerAngles.ToString("F1") + " scale " + s.ToString("F3"));
                if (StateAdopted != null) StateAdopted(p, r, s, name);
            }
            return changed;
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
