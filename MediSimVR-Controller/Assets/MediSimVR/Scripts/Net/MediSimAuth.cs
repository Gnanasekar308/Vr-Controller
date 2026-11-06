using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// "Only our app" check. SAME FILE in the phone project and the Quest project (keep them identical).
// Quest (server) -> phone : MSVR1 \t MediSimVR \t <nonce hex> \n
// phone -> Quest          : AUTH \t <HMAC-SHA256(key, "MediSimVR:" + nonce) hex> \n
// Wrong / missing answer = the Quest closes the connection. Used on the control port and the file port.
// Note: the key is inside both APKs, so this keeps other apps out; it is not protection against someone decompiling the APK.
namespace MQVR
{
    public static class MediSimAuth
    {
        public const string AppId = "MediSimVR";
        private const string Hello = "MSVR1";
        private const int TimeoutMs = 5000;
        private static readonly byte[] Key = Hex("5bd19cc072c6207c6c5da6e7b93f63eca8d3a91d94603fbd92750f5a4e1cbd58");

        public static string Answer(string nonce)
        {
            using (var h = new HMACSHA256(Key))
                return Xfer.Hex(h.ComputeHash(Encoding.UTF8.GetBytes(AppId + ":" + nonce)));
        }

        // Quest side. true = the other end proved it has the key.
        public static bool ServerCheck(Stream s)
        {
            try
            {
                int old = s.ReadTimeout;
                s.ReadTimeout = TimeoutMs;
                var nb = new byte[16];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(nb);
                string nonce = Xfer.Hex(nb);
                Xfer.WriteLine(s, Hello + "\t" + AppId + "\t" + nonce);
                string r = Xfer.ReadLine(s, 256);
                s.ReadTimeout = old;
                if (r == null) return false;
                string[] p = r.Split('\t');
                return p.Length == 2 && p[0] == "AUTH" && SameText(p[1].Trim().ToLowerInvariant(), Answer(nonce));
            }
            catch (Exception) { return false; }
        }

        // Phone side. Answers the Quest's challenge; false = not a MediSimVR receiver.
        public static bool ClientAnswer(Stream s, out string err)
        {
            err = "";
            try
            {
                int old = s.ReadTimeout;
                s.ReadTimeout = TimeoutMs;
                string r = Xfer.ReadLine(s, 256);
                s.ReadTimeout = old;
                string[] p = r == null ? new string[0] : r.Split('\t');
                if (p.Length != 3 || p[0] != Hello || p[1] != AppId || p[2].Length != 32)
                {
                    err = "Not a MediSimVR receiver";
                    return false;
                }
                Xfer.WriteLine(s, "AUTH\t" + Answer(p[2]));
                return true;
            }
            catch (Exception e)
            {
                err = "Handshake failed: " + e.Message;
                return false;
            }
        }

        // constant-time compare
        private static bool SameText(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int d = 0;
            for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
            return d == 0;
        }

        private static byte[] Hex(string h)
        {
            var b = new byte[h.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            return b;
        }
    }
}
