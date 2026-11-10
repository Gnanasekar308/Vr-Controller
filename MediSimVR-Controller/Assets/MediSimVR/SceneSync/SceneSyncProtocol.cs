using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// MediSimVR scene sync protocol: UDP, one JSON object per datagram, HMAC signature.
// SAME FILES in the Quest receiver and the phone project (Assets/MediSimVR/SceneSync): copy the whole folder, never edit one side only.
//
// Messages ("sig" is ALWAYS the last field):
//   {"app":"MediSimVR","type":"discover","sig":"..."}
//   {"app":"MediSimVR","type":"here","name":"Quest-AB12","kind":"quest"|"pc","pos":[x,y,z],"rot":[x,y,z,w],"scale":[x,y,z],"sig":"..."}
//   {"app":"MediSimVR","type":"transform","seq":12,"pos":[x,y,z],"rot":[x,y,z,w],"scale":[x,y,z],"sig":"..."}
// pos / rot / scale are LOCAL values of the synced object inside its "SyncWorld" root.
//
// Signing:
//   body = the JSON object WITHOUT the sig field (so it ends with '}')
//   sig  = first 8 bytes of HMAC-SHA256(SharedKey, UTF-8 bytes of body), as 16 lowercase hex chars
//   sent = body without its last '}'  +  ,"sig":"<16 hex>"}
// The receiver checks the exact bytes it got (no re-serialising), so number formatting can never break the signature.
// Wrong / missing sig, wrong app id or bad JSON = the packet is dropped.
// Note: the key is inside the APKs. This keeps other apps out; it is not protection against someone decompiling the APK.
namespace MediSimVR.SceneSync
{
    public static class SceneSyncProtocol
    {
        public const int Port = 8771;
        public const string AppId = "MediSimVR";

        // THE shared key (32 bytes as hex). Same value as MediSimAuth.Key in the phone project.
        public const string SharedKeyHex = "5bd19cc072c6207c6c5da6e7b93f63eca8d3a91d94603fbd92750f5a4e1cbd58";

        public const string TypeDiscover = "discover";
        public const string TypeHere = "here";
        public const string TypeTransform = "transform";
        public const string KindQuest = "quest";
        public const string KindPc = "pc";

        public const int SigBytes = 8;
        public const int MaxPacketBytes = 2048;

        private const string SigPrefix = ",\"sig\":\"";
        private static readonly byte[] Key = HexToBytes(SharedKeyHex);

        // ---------- build (returns the signed message) ----------

        public static string BuildDiscover()
        {
            return Sign("{\"app\":\"" + AppId + "\",\"type\":\"" + TypeDiscover + "\"}");
        }

        public static string BuildHere(string name, string kind, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"app\":\"").Append(AppId)
              .Append("\",\"type\":\"").Append(TypeHere)
              .Append("\",\"name\":\"").Append(Escape(name))
              .Append("\",\"kind\":\"").Append(Escape(kind)).Append('"');
            AppendPose(sb, pos, rot, scale);
            sb.Append('}');
            return Sign(sb.ToString());
        }

        public static string BuildTransform(int seq, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"app\":\"").Append(AppId)
              .Append("\",\"type\":\"").Append(TypeTransform)
              .Append("\",\"seq\":").Append(seq.ToString(CultureInfo.InvariantCulture));
            AppendPose(sb, pos, rot, scale);
            sb.Append('}');
            return Sign(sb.ToString());
        }

        // ---------- sign / verify ----------

        // body = JSON object without sig. Returns the same JSON with "sig" appended as the last field.
        public static string Sign(string body)
        {
            if (string.IsNullOrEmpty(body) || body[0] != '{' || body[body.Length - 1] != '}')
                throw new ArgumentException("body must be a JSON object");
            return body.Substring(0, body.Length - 1) + SigPrefix + ComputeSig(body) + "\"}";
        }

        public static string ComputeSig(string body)
        {
            byte[] mac;
            using (var h = new HMACSHA256(Key))
                mac = h.ComputeHash(Encoding.UTF8.GetBytes(body));
            var sb = new StringBuilder(SigBytes * 2);
            for (int i = 0; i < SigBytes; i++) sb.Append(mac[i].ToString("x2"));
            return sb.ToString();
        }

        // true = signature OK. body = the signed part (the message without its sig field).
        public static bool TryVerify(string message, out string body)
        {
            body = null;
            if (string.IsNullOrEmpty(message)) return false;
            message = message.Trim();
            int i = message.LastIndexOf(SigPrefix, StringComparison.Ordinal);
            if (i <= 0 || message[0] != '{') return false;
            int sigStart = i + SigPrefix.Length;
            // the message must end with exactly  <16 hex>"}
            if (message.Length != sigStart + SigBytes * 2 + 2) return false;
            if (message[message.Length - 2] != '"' || message[message.Length - 1] != '}') return false;
            string got = message.Substring(sigStart, SigBytes * 2).ToLowerInvariant();
            string signedPart = message.Substring(0, i) + "}";
            if (!SameText(got, ComputeSig(signedPart))) return false;
            body = signedPart;
            return true;
        }

        // Verify + parse + app id check, in one call (main thread). Network threads call only TryVerify (pure .NET)
        // and hand the verified body to the main thread, which calls TryParseBody.
        public static bool TryParse(string message, out SceneSyncMessage msg, out string error)
        {
            msg = null;
            if (!TryVerify(message, out string body)) { error = "bad or missing sig"; return false; }
            return TryParseBody(body, out msg, out error);
        }

        // Main thread (JsonUtility): JSON + app id + type check of a body that passed TryVerify.
        public static bool TryParseBody(string body, out SceneSyncMessage msg, out string error)
        {
            msg = null;
            SceneSyncMessage m;
            try { m = JsonUtility.FromJson<SceneSyncMessage>(body); }
            catch (Exception e) { error = "bad JSON: " + e.Message; return false; }
            if (m == null || m.app != AppId) { error = "wrong app id"; return false; }
            if (string.IsNullOrEmpty(m.type)) { error = "missing type"; return false; }
            msg = m;
            error = null;
            return true;
        }

        // ---------- helpers ----------

        private static void AppendPose(StringBuilder sb, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            sb.Append(",\"pos\":[").Append(F(pos.x)).Append(',').Append(F(pos.y)).Append(',').Append(F(pos.z)).Append(']');
            sb.Append(",\"rot\":[").Append(F(rot.x)).Append(',').Append(F(rot.y)).Append(',').Append(F(rot.z)).Append(',').Append(F(rot.w)).Append(']');
            sb.Append(",\"scale\":[").Append(F(scale.x)).Append(',').Append(F(scale.y)).Append(',').Append(F(scale.z)).Append(']');
        }

        // Invariant culture, never exponent notation, max 6 decimals.
        public static string F(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        // constant-time compare
        private static bool SameText(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int d = 0;
            for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
            return d == 0;
        }

        private static byte[] HexToBytes(string h)
        {
            var b = new byte[h.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            return b;
        }
    }
}
