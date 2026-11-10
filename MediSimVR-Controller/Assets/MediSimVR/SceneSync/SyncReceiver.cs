using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Put this on the synced object (the cube, a child of "SyncWorld").
// A background thread listens on UDP SceneSyncProtocol.Port and checks the signature (pure .NET, no Unity API);
// Update() parses the verified messages on the main thread. Only correctly signed MediSimVR messages are used.
//   discover  -> reply "here" (device name, kind, CURRENT local pos/rot/scale) to the sender's ip:port
//   transform -> move this object smoothly to the given local pos/rot/scale.
//                Older (or equal) seq is ignored; after seqResetSeconds without an accepted transform any seq is accepted again.
namespace MediSimVR.SceneSync
{
    public class SyncReceiver : MonoBehaviour
    {
        [Tooltip("Empty = automatic: \"Quest-\" (kind quest) or \"PC-\" (kind pc) + last 4 characters of SystemInfo.deviceUniqueIdentifier, uppercase.")]
        [SerializeField] private string deviceNameOverride = "";
        [SerializeField] private string kind = SceneSyncProtocol.KindQuest;
        [Tooltip("Higher = follows the target faster.")]
        [SerializeField] private float smoothing = 12f;
        [Tooltip("After this many seconds without an accepted transform, a lower seq is accepted again (phone restarted).")]
        [SerializeField] private float seqResetSeconds = 2f;
        [Tooltip("'Phone connected' while a valid signed message arrived within this many seconds.")]
        [SerializeField] private float connectedTimeout = 5f;

        public string DeviceName { get; private set; } = "";
        public bool IsListening { get { return running; } }
        public string ListenError { get; private set; }
        public bool PhoneConnected { get { return lastValidTime >= 0f && Time.unscaledTime - lastValidTime < connectedTimeout; } }
        public string LastSender { get; private set; } = "";
        public int LastSeq { get; private set; }
        public int AcceptedTransforms { get; private set; }
        public int IgnoredOldTransforms { get; private set; }
        public int DroppedPackets { get { return Interlocked.CompareExchange(ref dropped, 0, 0); } }

        private struct Incoming
        {
            public string body;   // signed part of a message whose signature was verified
            public IPEndPoint from;
        }

        private readonly ConcurrentQueue<Incoming> inbox = new ConcurrentQueue<Incoming>();
        private readonly ConcurrentQueue<string> warnings = new ConcurrentQueue<string>(); // from the UDP thread, logged in Update()
        private UdpClient udp;
        private Thread thread;
        private volatile bool running;
        private int dropped;
        private int lastDropLogTick;

        private Vector3 targetPos;
        private Quaternion targetRot;
        private Vector3 targetScale;
        private bool hasTarget;
        private float lastAcceptTime = -1000f;
        private float lastValidTime = -1f;

        private void Awake()
        {
            DeviceName = string.IsNullOrWhiteSpace(deviceNameOverride) ? AutoName(kind) : deviceNameOverride.Trim();
            targetPos = transform.localPosition;
            targetRot = transform.localRotation;
            targetScale = transform.localScale;
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
                Debug.Log("[MSVR] SyncReceiver listening on UDP " + SceneSyncProtocol.Port + " as " + DeviceName + " (kind " + kind + ")");
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
            while (inbox.TryDequeue(out inc))
            {
                SceneSyncMessage msg;
                string err;
                if (SceneSyncProtocol.TryParseBody(inc.body, out msg, out err)) Handle(msg, inc.from);
                else Drop(inc.from, err);
            }
            while (warnings.TryDequeue(out w)) Debug.LogWarning(w); // drops found while parsing

            if (hasTarget)
            {
                float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, k);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, k);
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, k);
            }
        }

        private void Handle(SceneSyncMessage msg, IPEndPoint from)
        {
            lastValidTime = Time.unscaledTime;
            LastSender = from.Address.ToString();
            switch (msg.type)
            {
                case SceneSyncProtocol.TypeDiscover:
                    SendHere(from);
                    break;
                case SceneSyncProtocol.TypeTransform:
                    ApplyTransform(msg, from);
                    break;
                default:
                    break; // e.g. "here" from another headset: not for us
            }
        }

        private void SendHere(IPEndPoint to)
        {
            UdpClient client = udp;
            if (client == null) return;
            Transform t = transform;
            string reply = SceneSyncProtocol.BuildHere(DeviceName, kind, t.localPosition, t.localRotation, t.localScale);
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(reply);
                client.Send(b, b.Length, to);
                Debug.Log("[MSVR] discover from " + to + " -> sent: " + reply);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MSVR] could not send 'here' to " + to + ": " + e.Message);
            }
        }

        private void ApplyTransform(SceneSyncMessage m, IPEndPoint from)
        {
            float now = Time.unscaledTime;
            bool afterSilence = now - lastAcceptTime > seqResetSeconds;
            if (!afterSilence && m.seq <= LastSeq)
            {
                IgnoredOldTransforms++;
                if (IgnoredOldTransforms == 1 || IgnoredOldTransforms % 30 == 0)
                    Debug.Log("[MSVR] transform seq " + m.seq + " ignored (last " + LastSeq + "), ignored total " + IgnoredOldTransforms);
                return;
            }

            LastSeq = m.seq;
            lastAcceptTime = now;
            Vector3 p; Quaternion r; Vector3 s;
            if (m.TryGetPos(out p)) targetPos = p;
            if (m.TryGetRot(out r)) targetRot = r;
            if (m.TryGetScale(out s)) targetScale = s;
            hasTarget = true;
            AcceptedTransforms++;
            if (AcceptedTransforms == 1 || afterSilence || AcceptedTransforms % 30 == 0)
                Debug.Log("[MSVR] transform seq " + m.seq + " from " + from + " accepted -> pos " + targetPos.ToString("F3") +
                          " rot " + targetRot.eulerAngles.ToString("F1") + " scale " + targetScale.ToString("F3") +
                          " (accepted total " + AcceptedTransforms + ")");
        }
    }
}
