using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Put this on the scene root that holds the controllable objects ("SyncWorld"). Every SyncObject below it is
// controllable from the phone; poses are relative to this transform.
// A background thread listens on UDP SceneSyncProtocol.Port and checks the signature (pure .NET, no Unity API);
// Update() parses the verified messages on the main thread. Only correctly signed MediSimVR messages are used.
//   discover  -> reply "here" (device name, kind, object count) to the sender's ip:port
//   getscene  -> reply the object list ("objects" parts) to the sender
//   transform -> move that object smoothly to the given pos/rot/scale (+ locked flag).
//                Per object: an older (or equal) seq from the same phone run (sid) is ignored; a new sid (phone restarted) is accepted.
namespace MediSimVR.SceneSync
{
    public class SyncReceiver : MonoBehaviour
    {
        [Tooltip("Empty = automatic: \"Quest-\" (kind quest) or \"PC-\" (kind pc) + last 4 characters of SystemInfo.deviceUniqueIdentifier, uppercase.")]
        [SerializeField] private string deviceNameOverride = "";
        [SerializeField] private string kind = SceneSyncProtocol.KindQuest;
        [Tooltip("'Phone connected' while a valid signed message arrived within this many seconds.")]
        [SerializeField] private float connectedTimeout = 5f;

        public string DeviceName { get; private set; } = "";
        public string Kind { get { return kind; } }
        public bool IsListening { get { return running; } }
        public string ListenError { get; private set; }
        public bool PhoneConnected { get { return lastValidTime >= 0f && Time.unscaledTime - lastValidTime < connectedTimeout; } }
        public string LastSender { get; private set; } = "";
        public int LastSeq { get; private set; }
        public int AcceptedTransforms { get; private set; }
        public int IgnoredOldTransforms { get; private set; }
        public int UnknownIdTransforms { get; private set; }
        public int SceneRequests { get; private set; }
        public int ObjectCount { get { return objects.Count; } }
        public int DroppedPackets { get { return Interlocked.CompareExchange(ref dropped, 0, 0); } }

        private struct Incoming
        {
            public string body;   // signed part of a message whose signature was verified
            public IPEndPoint from;
        }

        private struct SeqState
        {
            public int sid, seq;
        }

        private readonly ConcurrentQueue<Incoming> inbox = new ConcurrentQueue<Incoming>();
        private readonly ConcurrentQueue<string> warnings = new ConcurrentQueue<string>(); // from the UDP thread, logged in Update()
        private readonly Dictionary<string, SyncObject> objects = new Dictionary<string, SyncObject>();
        private readonly List<string> order = new List<string>();  // ids in hierarchy order
        private readonly Dictionary<string, SeqState> seqs = new Dictionary<string, SeqState>();
        private readonly List<SyncObject> scanBuffer = new List<SyncObject>();
        private UdpClient udp;
        private Thread thread;
        private volatile bool running;
        private int dropped;
        private int lastDropLogTick;
        private float lastValidTime = -1f;
        private float lastRescan = -100f, lastUnknownLog = -100f;

        private void Awake()
        {
            DeviceName = string.IsNullOrWhiteSpace(deviceNameOverride) ? AutoName(kind) : deviceNameOverride.Trim();
            Rescan();
        }

        private void OnEnable() { StartListening(); }
        private void OnDisable() { StopListening(); }
        private void OnApplicationQuit() { StopListening(); }

        // "Quest-" / "PC-" (by kind) + last 4 chars of the unique id (uppercase). Fallback if the id is not available.
        public static string AutoName(string kind)
        {
            string prefix = kind == SceneSyncProtocol.KindPc ? "PC-" : "Quest-";
            string id = "";
            try { id = SystemInfo.deviceUniqueIdentifier ?? ""; } catch (Exception) { }
            var clean = new StringBuilder();
            if (id != SystemInfo.unsupportedIdentifier)
                foreach (char c in id) if (char.IsLetterOrDigit(c)) clean.Append(c);
            if (clean.Length < 4)
            {
                clean.Length = 0;
                clean.Append(((uint)(SystemInfo.deviceName + SystemInfo.deviceModel).GetHashCode()).ToString("X8"));
            }
            return prefix + clean.ToString(clean.Length - 4, 4).ToUpperInvariant();
        }

        // ---------- objects ----------

        // (re)collects the SyncObjects below this transform; duplicate ids get "-2", "-3", ...
        public void Rescan()
        {
            lastRescan = Time.unscaledTime;
            objects.Clear();
            order.Clear();
            scanBuffer.Clear();
            GetComponentsInChildren(true, scanBuffer);
            foreach (var so in scanBuffer)
            {
                if (so == null) continue;
                string baseId = so.Id;
                string uid = baseId;
                for (int n = 2; objects.ContainsKey(uid); n++) uid = baseId + "-" + n;
                if (uid != baseId) Debug.LogWarning("[MSVR] SyncObject id \"" + baseId + "\" is used twice; \"" + so.name + "\" is sent as \"" + uid + "\"");
                objects[uid] = so;
                order.Add(uid);
                if (order.Count >= SceneSyncProtocol.MaxObjectsPerScene) break;
            }
            scanBuffer.Clear();
        }

        public bool TryGetObject(string id, out SyncObject so) { return objects.TryGetValue(id ?? "", out so); }

        public List<SyncObjectInfo> DescribeAll()
        {
            var list = new List<SyncObjectInfo>(order.Count);
            foreach (string uid in order)
            {
                SyncObject so;
                if (objects.TryGetValue(uid, out so) && so != null) list.Add(so.Describe(transform, uid));
            }
            return list;
        }

        // ---------- network ----------

        private void StartListening()
        {
            if (running) return;
            try
            {
                udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, SceneSyncProtocol.Port));
                udp.EnableBroadcast = true;
                running = true;
                ListenError = null;
                thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "SceneSync-UDP" };
                thread.Start();
                Debug.Log("[MSVR] SyncReceiver listening on UDP " + SceneSyncProtocol.Port + " as " + DeviceName + " (kind " + kind + "), " + objects.Count + " objects");
            }
            catch (Exception e)
            {
                running = false;
                ListenError = e.Message;
                Debug.LogError("[MSVR] SyncReceiver could not open UDP " + SceneSyncProtocol.Port + ": " + e);
                try { if (udp != null) udp.Close(); } catch (Exception) { }
                udp = null;
            }
        }

        private void StopListening()
        {
            if (!running && udp == null) return;
            running = false;
            try { if (udp != null) udp.Close(); } catch (Exception) { }
            udp = null;
            if (thread != null && thread.IsAlive) thread.Join(500);
            thread = null;
            Debug.Log("[MSVR] SyncReceiver stopped");
        }

        // ---------- background thread ----------

        private void ReceiveLoop()
        {
            UdpClient client = udp;
            while (running)
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                byte[] data;
                try
                {
                    data = client.Receive(ref from);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException e)
                {
                    if (!running) break;
                    if (e.SocketErrorCode == SocketError.ConnectionReset) continue; // ICMP port unreachable (Windows)
                    Warn("[MSVR] SyncReceiver socket error: " + e.SocketErrorCode);
                    Thread.Sleep(50);
                    continue;
                }
                catch (Exception e)
                {
                    if (!running) break;
                    Warn("[MSVR] SyncReceiver receive error: " + e.Message);
                    Thread.Sleep(50);
                    continue;
                }

                if (data == null || data.Length == 0 || data.Length > SceneSyncProtocol.MaxPacketBytes)
                {
                    Drop(from, "bad size " + (data == null ? 0 : data.Length));
                    continue;
                }

                string text;
                try { text = Encoding.UTF8.GetString(data); }
                catch (Exception) { Drop(from, "not UTF-8"); continue; }

                string body; // signature check here (pure .NET); JSON parsing happens on the main thread
                if (!SceneSyncProtocol.TryVerify(text, out body))
                {
                    Drop(from, "bad or missing sig");
                    continue;
                }
                if (inbox.Count < 256) inbox.Enqueue(new Incoming { body = body, from = from });
            }
        }

        private void Drop(IPEndPoint from, string why)
        {
            int n = Interlocked.Increment(ref dropped);
            int now = Environment.TickCount;
            if (n == 1 || unchecked(now - lastDropLogTick) > 1000) // at most one log line per second
            {
                lastDropLogTick = now;
                Warn("[MSVR] dropped packet from " + from + ": " + why + " (dropped total " + n + ")");
            }
        }

        // background thread: no Unity API here, the line is logged by Update() on the main thread
        private void Warn(string line)
        {
            if (warnings.Count < 100) warnings.Enqueue(line);
        }

        // ---------- main thread ----------

        private void Update()
        {
            string w;
            while (warnings.TryDequeue(out w)) Debug.LogWarning(w);

            Incoming inc;
            int n = 0;
            while (n++ < 128 && inbox.TryDequeue(out inc))
            {
                SceneSyncMessage msg;
                string err;
                if (SceneSyncProtocol.TryParseBody(inc.body, out msg, out err)) Handle(msg, inc.from);
                else Drop(inc.from, err);
            }
            while (warnings.TryDequeue(out w)) Debug.LogWarning(w); // drops found while parsing
        }

        private void Handle(SceneSyncMessage msg, IPEndPoint from)
        {
            switch (msg.type)
            {
                case SceneSyncProtocol.TypeDiscover:
                    MarkValid(from);
                    SendHere(from);
                    break;
                case SceneSyncProtocol.TypeGetScene:
                    MarkValid(from);
                    SendScene(from);
                    break;
                case SceneSyncProtocol.TypeTransform:
                    MarkValid(from);
                    ApplyTransform(msg, from);
                    break;
                default:
                    break; // e.g. "here" / "objects" from another receiver: not for us
            }
        }

        private void MarkValid(IPEndPoint from)
        {
            lastValidTime = Time.unscaledTime;
            LastSender = from.Address.ToString();
        }

        private void Send(string msg, IPEndPoint to, string what)
        {
            UdpClient client = udp;
            if (client == null) return;
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(msg);
                client.Send(b, b.Length, to);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MSVR] could not send '" + what + "' to " + to + ": " + e.Message);
            }
        }

        private void SendHere(IPEndPoint to)
        {
            string reply = SceneSyncProtocol.BuildHere(DeviceName, kind, objects.Count);
            Send(reply, to, "here");
            Debug.Log("[MSVR] discover from " + to + " -> sent: " + reply);
        }

        private void SendScene(IPEndPoint to)
        {
            SceneRequests++;
            Rescan();
            var infos = DescribeAll();
            var parts = SceneSyncProtocol.BuildObjectParts(infos);
            foreach (string p in parts) Send(p, to, "objects");
            Debug.Log("[MSVR] getscene from " + to + " -> sent " + infos.Count + " objects in " + parts.Count + " part(s)");
        }

        private void ApplyTransform(SceneSyncMessage m, IPEndPoint from)
        {
            SyncObject so;
            if (string.IsNullOrEmpty(m.id) || !objects.TryGetValue(m.id, out so) || so == null)
            {
                if (Time.unscaledTime - lastRescan > 1f) Rescan(); // an object may have been added
                if (string.IsNullOrEmpty(m.id) || !objects.TryGetValue(m.id, out so) || so == null)
                {
                    UnknownIdTransforms++;
                    if (Time.unscaledTime - lastUnknownLog > 2f)
                    {
                        lastUnknownLog = Time.unscaledTime;
                        Debug.LogWarning("[MSVR] transform for unknown object id \"" + m.id + "\" ignored (unknown total " + UnknownIdTransforms + ")");
                    }
                    return;
                }
            }

            SeqState st;
            if (seqs.TryGetValue(m.id, out st) && st.sid == m.sid && m.seq <= st.seq)
            {
                IgnoredOldTransforms++;
                if (IgnoredOldTransforms == 1 || IgnoredOldTransforms % 30 == 0)
                    Debug.Log("[MSVR] transform " + m.id + " seq " + m.seq + " ignored (last " + st.seq + "), ignored total " + IgnoredOldTransforms);
                return;
            }

            Vector3 p, s;
            Quaternion r;
            if (!m.TryGetPos(out p) || !m.TryGetRot(out r) || !m.TryGetScale(out s))
            {
                Drop(from, "transform with bad pos/rot/scale");
                return;
            }

            bool newSession = !seqs.ContainsKey(m.id) || st.sid != m.sid;
            seqs[m.id] = new SeqState { sid = m.sid, seq = m.seq };
            LastSeq = m.seq;
            bool lockChanged = so.Locked != m.locked;
            so.Locked = m.locked;
            so.SetTarget(transform, p, r, s);
            AcceptedTransforms++;
            if (AcceptedTransforms == 1 || newSession || lockChanged || AcceptedTransforms % 30 == 0)
                Debug.Log("[MSVR] transform " + m.id + " seq " + m.seq + " from " + from + " accepted -> pos " + p.ToString("F3") +
                          " rot " + r.eulerAngles.ToString("F1") + " scale " + s.ToString("F3") + (m.locked ? " locked" : "") +
                          " (accepted total " + AcceptedTransforms + ")");
        }
    }
}
