using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// MediSimVR scene sync protocol v2: UDP, one JSON object per datagram, HMAC signature.
// SAME FILES in the Quest receiver and the phone project (Assets/MediSimVR/SceneSync): copy the whole folder, never edit one side only.
//
// Messages ("sig" is ALWAYS the last field):
//   phone -> receivers (broadcast + manual IPs, every second):
//     {"app":"MediSimVR","type":"discover","sig":"..."}
//   receiver -> phone (answer to discover):
//     {"app":"MediSimVR","type":"here","name":"Quest-AB12","kind":"quest"|"pc","count":6,"sig":"..."}
//   phone -> receiver (the first receiver that answers is the source of the scene):
//     {"app":"MediSimVR","type":"getscene","sig":"..."}
//   receiver -> phone: the object list, split into parts (each datagram stays below ~1.2 KB):
//     {"app":"MediSimVR","type":"objects","part":0,"parts":2,"total":6,"objs":[
//        {"id":"cube","name":"Cube","shape":"cube","color":[r,g,b,a],"size":[x,y,z],"center":[x,y,z],
//         "pos":[x,y,z],"rot":[x,y,z,w],"scale":[x,y,z],"locked":false}, ...],"sig":"..."}
//   phone -> receivers: the state of ONE object (sid = random number per phone app run, seq grows per message):
//     {"app":"MediSimVR","type":"transform","sid":123,"seq":12,"id":"cube","pos":[x,y,z],"rot":[x,y,z,w],"scale":[x,y,z],"locked":false,"sig":"..."}
// pos / rot / scale are relative to the receiver's "SyncWorld" root (the object with the SyncReceiver).
// shape: cube | sphere | cylinder | capsule | plane = the Unity primitive; box = anything else, drawn as a box of
// "size" around "center" (both in the object's own unscaled space). Scale is never 0 or negative (min MinScale per axis).
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
        public const string TypeGetScene = "getscene";
        public const string TypeObjects = "objects";
        public const string TypeTransform = "transform";
        public const string KindQuest = "quest";
        public const string KindPc = "pc";

        public const string ShapeCube = "cube";
        public const string ShapeSphere = "sphere";
        public const string ShapeCylinder = "cylinder";
        public const string ShapeCapsule = "capsule";
        public const string ShapePlane = "plane";
        public const string ShapeBox = "box";

        public const float MinScale = 0.001f;      // per axis; scale is otherwise unlimited
        public const int MaxIdLength = 48;
        public const int MaxNameLength = 48;
        public const int MaxObjectsPerScene = 256;
        public const int SigBytes = 8;
        public const int MaxPacketBytes = 4096;    // receive limit
        public const int ObjectsBodyTarget = 1100; // an "objects" part is filled up to about this many characters

        private const string SigPrefix = ",\"sig\":\"";
        private static readonly byte[] Key = HexToBytes(SharedKeyHex);

        // ---------- build (returns the signed message) ----------

        public static string BuildDiscover()
        {
            return Sign("{\"app\":\"" + AppId + "\",\"type\":\"" + TypeDiscover + "\"}");
        }

        public static string BuildHere(string name, string kind, int count)
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"app\":\"").Append(AppId)
              .Append("\",\"type\":\"").Append(TypeHere)
              .Append("\",\"name\":\"").Append(Escape(Clip(name, MaxNameLength)))
              .Append("\",\"kind\":\"").Append(Escape(kind))
              .Append("\",\"count\":").Append(count.ToString(CultureInfo.InvariantCulture)).Append('}');
            return Sign(sb.ToString());
        }

        public static string BuildGetScene()
        {
            return Sign("{\"app\":\"" + AppId + "\",\"type\":\"" + TypeGetScene + "\"}");
        }

        // Splits the object list into signed "objects" messages (part 0..parts-1). An empty list = one part with no objects.
        public static List<string> BuildObjectParts(IList<SyncObjectInfo> objs)
        {
            var fragments = new List<string>();
            int n = objs == null ? 0 : Math.Min(objs.Count, MaxObjectsPerScene);
            var one = new StringBuilder(320);
            for (int i = 0; i < n; i++)
            {
                one.Length = 0;
                AppendObject(one, objs[i]);
                fragments.Add(one.ToString());
            }

            var groups = new List<List<string>>();
            var cur = new List<string>();
            int len = 0;
            foreach (string f in fragments)
            {
                if (cur.Count > 0 && len + f.Length + 1 > ObjectsBodyTarget)
                {
                    groups.Add(cur);
                    cur = new List<string>();
                    len = 0;
                }
                cur.Add(f);
                len += f.Length + 1;
            }
            groups.Add(cur);

            var parts = new List<string>(groups.Count);
            var sb = new StringBuilder(1400);
            for (int g = 0; g < groups.Count; g++)
            {
                sb.Length = 0;
                sb.Append("{\"app\":\"").Append(AppId)
                  .Append("\",\"type\":\"").Append(TypeObjects)
                  .Append("\",\"part\":").Append(g.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"parts\":").Append(groups.Count.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"total\":").Append(n.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"objs\":[");
                for (int i = 0; i < groups[g].Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(groups[g][i]);
                }
                sb.Append("]}");
                parts.Add(Sign(sb.ToString()));
            }
            return parts;
        }

        public static string BuildTransform(int sid, int seq, string id, Vector3 pos, Quaternion rot, Vector3 scale, bool locked)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"app\":\"").Append(AppId)
              .Append("\",\"type\":\"").Append(TypeTransform)
              .Append("\",\"sid\":").Append(sid.ToString(CultureInfo.InvariantCulture))
              .Append(",\"seq\":").Append(seq.ToString(CultureInfo.InvariantCulture))
              .Append(",\"id\":\"").Append(Escape(Clip(id, MaxIdLength))).Append('"');
            AppendVec(sb, "pos", pos);
            AppendQuat(sb, "rot", rot);
            AppendVec(sb, "scale", ClampScale(scale));
            sb.Append(",\"locked\":").Append(locked ? "true" : "false").Append('}');
            return Sign(sb.ToString());
        }

        private static void AppendObject(StringBuilder sb, SyncObjectInfo o)
        {
            sb.Append("{\"id\":\"").Append(Escape(Clip(o.id, MaxIdLength)))
              .Append("\",\"name\":\"").Append(Escape(Clip(o.name, MaxNameLength)))
              .Append("\",\"shape\":\"").Append(Escape(string.IsNullOrEmpty(o.shape) ? ShapeBox : o.shape)).Append('"');
            AppendArray(sb, "color", o.color, 4, 1f);
            AppendArray(sb, "size", o.size, 3, 1f);
            AppendArray(sb, "center", o.center, 3, 0f);
            AppendArray(sb, "pos", o.pos, 3, 0f);
            AppendArray(sb, "rot", o.rot, 4, 0f, true);
            AppendArray(sb, "scale", o.scale, 3, 1f);
            sb.Append(",\"locked\":").Append(o.locked ? "true" : "false").Append('}');
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

        // true = signature OK. body = the signed part (the message without its sig field). Pure .NET (any thread).
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

        // ---------- values ----------

        public static bool TryVec3(float[] a, out Vector3 v)
        {
            v = Vector3.zero;
            if (!Finite(a, 3)) return false;
            v = new Vector3(a[0], a[1], a[2]);
            return true;
        }

        // [x, y, z, w]; normalised. A zero quaternion is rejected.
        public static bool TryQuat(float[] a, out Quaternion q)
        {
            q = Quaternion.identity;
            if (!Finite(a, 4)) return false;
            float len = Mathf.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2] + a[3] * a[3]);
            if (len < 1e-6f) return false;
            q = new Quaternion(a[0] / len, a[1] / len, a[2] / len, a[3] / len);
            return true;
        }

        // scale: finite, then every axis at least MinScale (0, negative or tiny values become MinScale)
        public static bool TryScale(float[] a, out Vector3 v)
        {
            v = Vector3.one;
            if (!Finite(a, 3)) return false;
            v = ClampScale(new Vector3(a[0], a[1], a[2]));
            return true;
        }

        public static Vector3 ClampScale(Vector3 s)
        {
            return new Vector3(ClampScale(s.x), ClampScale(s.y), ClampScale(s.z));
        }

        public static float ClampScale(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 1f;
            return v < MinScale ? MinScale : v;
        }

        public static float[] Arr(Vector3 v) { return new[] { v.x, v.y, v.z }; }
        public static float[] Arr(Quaternion q) { return new[] { q.x, q.y, q.z, q.w }; }
        public static float[] Arr(Color c) { return new[] { c.r, c.g, c.b, c.a }; }

        public static bool Finite(float[] a, int n)
        {
            if (a == null || a.Length != n) return false;
            for (int i = 0; i < n; i++)
                if (float.IsNaN(a[i]) || float.IsInfinity(a[i])) return false;
            return true;
        }

        // ---------- helpers ----------

        private static void AppendVec(StringBuilder sb, string name, Vector3 v)
        {
            sb.Append(",\"").Append(name).Append("\":[").Append(F(v.x)).Append(',').Append(F(v.y)).Append(',').Append(F(v.z)).Append(']');
        }

        private static void AppendQuat(StringBuilder sb, string name, Quaternion q)
        {
            sb.Append(",\"").Append(name).Append("\":[").Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w)).Append(']');
        }

        // missing / wrong-length arrays are written with the default value (a rotation defaults to identity)
        private static void AppendArray(StringBuilder sb, string name, float[] a, int n, float def, bool quat = false)
        {
            sb.Append(",\"").Append(name).Append("\":[");
            bool ok = a != null && a.Length == n;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(',');
                float v = ok ? a[i] : (quat && i == 3 ? 1f : def);
                sb.Append(F(v));
            }
            sb.Append(']');
        }

        // Invariant culture, never exponent notation, max 6 decimals.
        public static string F(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        public static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
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
