using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MediSimVR
{
    // Live voice of the phone through the Medisim PC server's voice relay (UDP 5082):
    //   Transmit = this phone's microphone goes to the stations chosen with /api/voice/targets
    //   Listen   = the students' voices (push-to-talk A / X on the Quest) play on this phone
    // 16 kHz mono, 20 ms frames, mu-law. Packet: 'M', type (1 hello / 2 audio), id length, id, [seq u16, samples].
    public class PhoneVoice : MonoBehaviour
    {
        public const int Port = 5082;
        public const int SampleRate = 16000;
        public const int FrameSamples = 320;

        private const byte Magic = (byte)'M';
        private const byte TypeHello = 1;
        private const byte TypeAudio = 2;
        private const float HelloEvery = 2f;
        private const float SpeakingHold = 0.5f;
        private const float MicIdleSeconds = 5f;   // microphone open but not sent this long -> close it

        public bool Transmit { get; set; }
        public bool Listen { get; set; }
        public string MicStatus { get; private set; } = "";

        private string myId = "";
        private byte[] idBytes = new byte[0];
        private UdpClient udp;
        private IPEndPoint relay;
        private string relayHost = "";
        private volatile bool running;
        private readonly ConcurrentQueue<KeyValuePair<string, float[]>> incoming = new ConcurrentQueue<KeyValuePair<string, float[]>>();
        private readonly Dictionary<string, VoiceBuffer> streams = new Dictionary<string, VoiceBuffer>();
        private float nextHello;

        private AudioClip micClip;
        private string micDevice;
        private bool micStarting;
        private int lastMicPos;
        private float micIdle;
        private VoiceResampler resampler;
        private readonly float[] frame = new float[FrameSamples];
        private ushort seq;

        public void Init(string clientId)
        {
            myId = clientId;
            idBytes = Encoding.ASCII.GetBytes(clientId);
        }

        // The station currently heard (its device id), "" = nobody.
        public string Speaking()
        {
            string best = "";
            float last = -1f;

            foreach (var pair in streams)
            {
                float t = pair.Value.LastPacket;
                if (Time.unscaledTime - t < SpeakingHold && t > last) { best = pair.Key; last = t; }
            }

            return best;
        }

        private void Update()
        {
            ReleaseIdleMic();
            MedisimServer server = MedisimServer.Instance;
            bool wanted = (Transmit || Listen) && server.HasServer;

            if (!wanted)
            {
                if (udp != null) Close();
                DrainIncoming(false);
                return;
            }

            if (udp == null || relayHost != server.Host) Open(server.Host);
            if (udp == null) return;

            if (Time.unscaledTime >= nextHello)
            {
                nextHello = Time.unscaledTime + HelloEvery;
                Send(Header(TypeHello, 0));
            }

            if (Transmit && micClip == null && !micStarting) StartCoroutine(StartMic());
            Capture();
            DrainIncoming(Listen);
        }

        // ---------------------------------------------------------------- network

        private void Open(string host)
        {
            Close();

            try
            {
                IPAddress address;
                if (!IPAddress.TryParse(host, out address)) return;
                udp = new UdpClient(0);
                relay = new IPEndPoint(address, Port);
                relayHost = host;
                running = true;
                UdpClient mine = udp;
                new Thread(() => ReceiveLoop(mine)) { IsBackground = true, Name = "PhoneVoice" }.Start();
                nextHello = 0f;
                Debug.Log("[MediSimVR] Live voice through " + relay + " as " + myId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MediSimVR] Voice socket failed: " + e.Message);
                udp = null;
            }
        }

        private void Close()
        {
            running = false;
            try { if (udp != null) udp.Close(); } catch (Exception) { }
            udp = null;
            relayHost = "";
        }

        private void ReceiveLoop(UdpClient client)
        {
            var from = new IPEndPoint(IPAddress.Any, 0);

            while (running && client == udp)
            {
                byte[] data;

                try { data = client.Receive(ref from); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (!running) break; Thread.Sleep(10); continue; }

                if (data.Length < 6 || data[0] != Magic || data[1] != TypeAudio) continue;
                int idLen = data[2];
                int start = 3 + idLen + 2;
                if (idLen == 0 || data.Length <= start) continue;
                string sender = Encoding.ASCII.GetString(data, 3, idLen);
                if (sender == myId) continue;

                var samples = new float[data.Length - start];
                for (int i = 0; i < samples.Length; i++) samples[i] = VoiceCodec.Decode(data[start + i]);
                if (incoming.Count < 500) incoming.Enqueue(new KeyValuePair<string, float[]>(sender, samples));
            }
        }

        private byte[] Header(byte type, int extra)
        {
            var p = new byte[3 + idBytes.Length + extra];
            p[0] = Magic;
            p[1] = type;
            p[2] = (byte)idBytes.Length;
            Buffer.BlockCopy(idBytes, 0, p, 3, idBytes.Length);
            return p;
        }

        private void Send(byte[] packet)
        {
            try { udp.Send(packet, packet.Length, relay); }
            catch (Exception e) { Debug.LogWarning("[MediSimVR] Voice send failed: " + e.Message); }
        }

        // ---------------------------------------------------------------- microphone

        private IEnumerator StartMic()
        {
            micStarting = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                MicStatus = "Waiting for microphone permission...";
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                float waited = 0f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone) && waited < 20f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                {
                    MicStatus = "Microphone permission denied";
                    yield return new WaitForSeconds(5f);
                    micStarting = false;
                    yield break;
                }
            }
#endif
            if (Microphone.devices.Length == 0)
            {
                MicStatus = "No microphone found";
                yield return new WaitForSeconds(5f);
                micStarting = false;
                yield break;
            }

            micDevice = null; // default microphone
            int minRate, maxRate;
            Microphone.GetDeviceCaps(micDevice, out minRate, out maxRate);
            int rate = minRate == 0 && maxRate == 0 ? SampleRate : Mathf.Clamp(SampleRate, minRate, maxRate);
            AudioClip clip = Microphone.Start(micDevice, true, 1, rate);
            float timeout = 2f;

            while (Microphone.GetPosition(micDevice) <= 0 && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (clip == null || timeout <= 0f)
            {
                MicStatus = "Microphone did not start";
                Microphone.End(micDevice);
                micStarting = false;
                yield break;
            }

            micClip = clip;
            lastMicPos = Microphone.GetPosition(micDevice);
            resampler = new VoiceResampler(clip.frequency, SampleRate);
            MicStatus = "";
            micStarting = false;
            Debug.Log("[MediSimVR] Phone microphone started at " + clip.frequency + " Hz");
        }

        // The microphone must not stay open after the class ends (or during a long mute).
        private void ReleaseIdleMic()
        {
            if (micClip == null || Transmit)
            {
                micIdle = 0f;
                return;
            }

            micIdle += Time.unscaledDeltaTime;
            if (micIdle < MicIdleSeconds) return;

            Microphone.End(micDevice);
            micClip = null;
            micIdle = 0f;
            Debug.Log("[MediSimVR] Phone microphone stopped");
        }

        private void Capture()
        {
            if (micClip == null) return;
            int pos = Microphone.GetPosition(micDevice);
            if (pos == lastMicPos) return;
            int count = pos - lastMicPos;
            if (count < 0) count += micClip.samples;
            var samples = new float[count];
            micClip.GetData(samples, lastMicPos);
            lastMicPos = pos;

            if (!Transmit)
            {
                resampler.Clear();
                return;
            }

            resampler.Push(samples);

            while (resampler.TryPull(frame))
            {
                byte[] p = Header(TypeAudio, 2 + FrameSamples);
                int o = 3 + idBytes.Length;
                p[o] = (byte)(seq & 0xFF);
                p[o + 1] = (byte)(seq >> 8);
                seq++;
                for (int i = 0; i < FrameSamples; i++) p[o + 2 + i] = VoiceCodec.Encode(frame[i]);
                Send(p);
            }
        }

        // ---------------------------------------------------------------- playback

        private void DrainIncoming(bool play)
        {
            KeyValuePair<string, float[]> item;

            while (incoming.TryDequeue(out item))
            {
                VoiceBuffer s;
                if (!streams.TryGetValue(item.Key, out s))
                {
                    s = CreateStream(item.Key);
                    streams.Add(item.Key, s);
                }

                s.LastPacket = Time.unscaledTime;
                if (play) s.Write(item.Value);
            }
        }

        private VoiceBuffer CreateStream(string sender)
        {
            var s = new VoiceBuffer();
            var go = new GameObject("Voice " + sender);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = AudioClip.Create("voice-" + sender, SampleRate * 2, 1, SampleRate, true, s.Read);
            source.loop = true;
            source.spatialBlend = 0f;
            source.Play();
            return s;
        }

        private void OnDestroy()
        {
            Close();
            if (micClip != null) Microphone.End(micDevice);
        }

        // Jitter buffer for one remote speaker, read by the audio thread.
        private class VoiceBuffer
        {
            private const int Prime = SampleRate * 60 / 1000;
            private const int MaxLatency = SampleRate * 400 / 1000;
            private const int Keep = SampleRate * 120 / 1000;
            private const float Gain = 1.4f;

            public float LastPacket = -100f;
            private readonly object gate = new object();
            private readonly float[] ring = new float[SampleRate * 2];
            private int read, count;
            private bool primed;

            public void Write(float[] samples)
            {
                lock (gate)
                {
                    foreach (float v in samples)
                    {
                        if (count == ring.Length) { read = (read + 1) % ring.Length; count--; }
                        ring[(read + count) % ring.Length] = v;
                        count++;
                    }

                    if (count > MaxLatency)
                    {
                        read = (read + count - Keep) % ring.Length;
                        count = Keep;
                    }
                }
            }

            public void Read(float[] data)
            {
                lock (gate)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (!primed)
                        {
                            if (count >= Prime) primed = true;
                            else { data[i] = 0f; continue; }
                        }

                        if (count > 0)
                        {
                            data[i] = Mathf.Clamp(ring[read] * Gain, -1f, 1f);
                            read = (read + 1) % ring.Length;
                            count--;
                        }
                        else
                        {
                            data[i] = 0f;
                            primed = false;
                        }
                    }
                }
            }
        }
    }
}
