using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

// Video transfer over Wi-Fi, separate from the control link (own TCP port, own threads per transfer).
// No UnityEngine here on purpose: the same code runs in the Quest app, the phone app and the desktop test harness.
//
// Protocol (one TCP connection = one file):
//   phone -> quest : PUT \t <size> \t <sha256> \t <name> \n
//   quest -> phone : HAVE \n                       (already stored and verified, nothing to send)
//                    OK \t <offset> \n              (send bytes [offset .. size), resume if offset > 0)
//                    ERR \t <code> \t <message> \n  (BADREQ | BUSY | INUSE | STORAGE | IO)
//   phone -> quest : raw bytes
//   quest -> phone : DONE \t <sha256> \n            (written, SHA-256 of the whole file matches)
//                    ERR \t CHECKSUM|IO \t <message> \n
namespace MQVR
{
    public static class Xfer
    {
        public const int Port = 8767;
        public const int Buf = 256 * 1024;
        public const long ReserveBytes = 300L * 1024 * 1024; // always leave this much free on the Quest
        public const string PartExt = ".mqpart";   // bytes received so far
        public const string InfoExt = ".mqinfo";   // "<sha> <size>" of the .mqpart, needed to resume safely
        public const string ShaExt = ".mqsha";     // "<sha> <size> <mtimeTicks>" next to a verified video
        public static int MaxAttempts = 6;      // phone setting "Transfer Retry"

        public static string SafeName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            n = n.Replace('\\', '/');
            int i = n.LastIndexOf('/');
            if (i >= 0) n = n.Substring(i + 1);
            var sb = new StringBuilder();
            foreach (char c in n) sb.Append(c < 32 || ":*?\"<>|".IndexOf(c) >= 0 ? '_' : c);
            n = sb.ToString().Trim().TrimStart('.');
            if (n.Length > 120)
            {
                string ext = Path.GetExtension(n);
                n = n.Substring(0, 120 - ext.Length) + ext;
            }
            string low = n.ToLowerInvariant();
            if (low.EndsWith(PartExt) || low.EndsWith(InfoExt) || low.EndsWith(ShaExt)) return "";
            return n;
        }

        public static string Hex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        public static bool IsSha(string s)
        {
            if (s == null || s.Length != 64) return false;
            foreach (char c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        // Returns lower-case hex SHA-256, or null if cancelled.
        public static string HashStream(Stream s, Func<bool> cancelled, Action<long> progress)
        {
            using (var h = SHA256.Create())
            {
                var buf = new byte[Buf];
                long total = 0;
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancelled != null && cancelled()) return null;
                    h.TransformBlock(buf, 0, n, null, 0);
                    total += n;
                    if (progress != null) progress(total);
                }
                h.TransformFinalBlock(new byte[0], 0, 0);
                return Hex(h.Hash);
            }
        }

        // Header lines are read byte by byte so no binary payload is swallowed by a reader buffer.
        public static string ReadLine(Stream s, int max)
        {
            var bytes = new List<byte>(128);
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) return bytes.Count == 0 ? null : Encoding.UTF8.GetString(bytes.ToArray());
                if (b == '\n') return Encoding.UTF8.GetString(bytes.ToArray()).TrimEnd('\r');
                bytes.Add((byte)b);
                if (bytes.Count > max) throw new IOException("Header too long");
            }
        }

        public static void WriteLine(Stream s, string line)
        {
            byte[] d = Encoding.UTF8.GetBytes(line + "\n");
            s.Write(d, 0, d.Length);
            s.Flush();
        }

        public static string Mb(long bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }

        public static void TryDelete(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch (Exception) { }
        }
    }

    // Checksum record stored next to each verified video. Checked before every playback.
    public static class Integrity
    {
        public enum Result { Verified, Unverified, Changed }

        public static void Record(string videoPath, string sha)
        {
            var fi = new FileInfo(videoPath);
            File.WriteAllText(videoPath + Xfer.ShaExt, sha + " " + fi.Length + " " + fi.LastWriteTimeUtc.Ticks);
        }

        public static void Forget(string videoPath)
        {
            Xfer.TryDelete(videoPath + Xfer.ShaExt);
        }

        // Verified: size and write time are exactly what they were when the SHA-256 was confirmed.
        // Unverified: no record (file was copied to the Quest by hand). Changed: file differs from the verified one.
        public static Result Check(string videoPath, out string sha)
        {
            sha = "";
            try
            {
                string rec = videoPath + Xfer.ShaExt;
                if (!File.Exists(rec)) return Result.Unverified;
                string[] p = File.ReadAllText(rec).Trim().Split(' ');
                if (p.Length < 3 || !Xfer.IsSha(p[0])) return Result.Changed;
                var fi = new FileInfo(videoPath);
                if (!fi.Exists || fi.Length != long.Parse(p[1])) return Result.Changed;
                long dt = Math.Abs(fi.LastWriteTimeUtc.Ticks - long.Parse(p[2]));
                if (dt > TimeSpan.TicksPerSecond * 2) return Result.Changed;
                sha = p[0];
                return Result.Verified;
            }
            catch (Exception) { return Result.Changed; }
        }

        public static bool Matches(string videoPath, string sha, long size)
        {
            string have;
            return File.Exists(videoPath) && Check(videoPath, out have) == Result.Verified &&
                have == sha && new FileInfo(videoPath).Length == size;
        }
    }

    // ===================== QUEST SIDE =====================
    public class FileReceiver
    {
        public string Folder = "";
        public Func<long> FreeBytes = () => -1L;            // must be thread-safe; -1 = unknown
        public Func<string, bool> InUse = n => false;        // must be thread-safe
        public Action<string, string> OnEvent;              // (level, msg), called from worker threads
        public string Error = "";
        public int Port { get; private set; }

        private class Active { public long Done, Total; }
        private readonly object lk = new object();
        private readonly Dictionary<string, Active> active = new Dictionary<string, Active>();
        private readonly List<TcpClient> conns = new List<TcpClient>();
        private TcpListener listener;
        private volatile bool running;

        public bool Start(int port)
        {
            try
            {
                if (running) return true;
                Directory.CreateDirectory(Folder);
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                running = true;
                var t = new Thread(AcceptLoop);
                t.IsBackground = true;
                t.Start();
                Error = "";
                return true;
            }
            catch (Exception e)
            {
                Error = "File server error: " + e.Message;
                return false;
            }
        }

        public void Stop()
        {
            running = false;
            try { if (listener != null) listener.Stop(); } catch (Exception) { }
            List<TcpClient> copy;
            lock (lk) { copy = new List<TcpClient>(conns); conns.Clear(); }
            foreach (var c in copy) { try { c.Close(); } catch (Exception) { } }
        }

        // First active transfer (for the status shown on the phone).
        public bool Progress(out string name, out long done, out long total, out int count)
        {
            lock (lk)
            {
                count = active.Count;
                foreach (var kv in active)
                {
                    name = kv.Key; done = kv.Value.Done; total = kv.Value.Total;
                    return true;
                }
            }
            name = ""; done = 0; total = 0;
            return false;
        }

        private void Emit(string level, string msg)
        {
            try { if (OnEvent != null) OnEvent(level, msg); } catch (Exception) { }
        }

        private void AcceptLoop()
        {
            while (running)
            {
                try
                {
                    TcpClient c = listener.AcceptTcpClient();
                    lock (lk) { conns.Add(c); }
                    var t = new Thread(() => Serve(c));
                    t.IsBackground = true;
                    t.Start();
                }
                catch (Exception)
                {
                    if (!running) break;
                    Thread.Sleep(100);
                }
            }
        }

        private static void Reply(Stream s, string line)
        {
            try { Xfer.WriteLine(s, line); } catch (Exception) { }
        }

        private void Serve(TcpClient c)
        {
            string name = "";
            bool registered = false;
            try
            {
                c.NoDelay = true;
                c.ReceiveTimeout = 30000;
                c.SendTimeout = 30000;
                c.ReceiveBufferSize = Xfer.Buf;
                NetworkStream ns = c.GetStream();
                if (!MediSimAuth.ServerCheck(ns))
                {
                    Emit("warning", "Rejected file connection from " + c.Client.RemoteEndPoint + " (not MediSimVR)");
                    return;
                }

                string head = Xfer.ReadLine(ns, 2048);
                string[] p = head == null ? new string[0] : head.Split('\t');
                long size;
                if (p.Length != 4 || p[0] != "PUT" || !long.TryParse(p[1], out size) || size <= 0)
                {
                    Reply(ns, "ERR\tBADREQ\tBad request");
                    return;
                }
                string sha = p[2].Trim().ToLowerInvariant();
                name = Xfer.SafeName(p[3]);
                if (name.Length == 0 || !Xfer.IsSha(sha))
                {
                    Reply(ns, "ERR\tBADREQ\tBad file name or checksum");
                    return;
                }

                string final = Path.Combine(Folder, name);
                lock (lk)
                {
                    if (active.ContainsKey(name))
                    {
                        Reply(ns, "ERR\tBUSY\tAlready receiving " + name);
                        return;
                    }
                    active[name] = new Active { Total = size };
                    registered = true;
                }

                if (Integrity.Matches(final, sha, size))
                {
                    Reply(ns, "HAVE");
                    return;
                }
                if (InUse(name))
                {
                    Reply(ns, "ERR\tINUSE\t" + name + " is loaded for playback here; stop it first");
                    return;
                }

                Receive(ns, name, final, size, sha);
            }
            catch (Exception e)
            {
                Emit("error", "Transfer " + name + " failed: " + e.Message);
            }
            finally
            {
                if (registered) lock (lk) { active.Remove(name); }
                lock (lk) { conns.Remove(c); }
                try { c.Close(); } catch (Exception) { }
            }
        }

        private void SetDone(string name, long v)
        {
            lock (lk) { Active a; if (active.TryGetValue(name, out a)) a.Done = v; }
        }

        private void Receive(NetworkStream ns, string name, string final, long size, string sha)
        {
            string part = final + Xfer.PartExt;
            string info = final + Xfer.InfoExt;
            string want = sha + " " + size;

            long offset = 0;
            try
            {
                if (File.Exists(part) && File.Exists(info) && File.ReadAllText(info).Trim() == want)
                {
                    offset = new FileInfo(part).Length;
                    if (offset > size) offset = 0;
                }
            }
            catch (Exception) { offset = 0; }
            if (offset == 0)
            {
                Xfer.TryDelete(part);
                File.WriteAllText(info, want);
            }

            long free = FreeBytes();
            long need = size - offset;
            if (free >= 0 && free - need < Xfer.ReserveBytes)
            {
                Reply(ns, "ERR\tSTORAGE\tNot enough storage: needs " + Xfer.Mb(need) + ", free " + Xfer.Mb(free));
                Emit("error", "Not enough storage for " + name + " (needs " + Xfer.Mb(need) + ", free " + Xfer.Mb(free) + ")");
                return;
            }

            using (var h = SHA256.Create())
            using (var fs = new FileStream(part, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, Xfer.Buf))
            {
                var buf = new byte[Xfer.Buf];
                // re-hash the bytes we already have so the final checksum covers the whole file
                fs.Position = 0;
                long left = offset;
                while (left > 0)
                {
                    int n = fs.Read(buf, 0, (int)Math.Min(buf.Length, left));
                    if (n <= 0) throw new IOException("Partial file shorter than expected");
                    h.TransformBlock(buf, 0, n, null, 0);
                    left -= n;
                }
                fs.SetLength(offset);
                fs.Position = offset;
                SetDone(name, offset);
                if (offset > 0) Emit("info", "Resuming " + name + " at " + (offset * 100 / size) + "%");
                Xfer.WriteLine(ns, "OK\t" + offset);

                long got = offset, sinceFlush = 0;
                while (got < size)
                {
                    int n;
                    try { n = ns.Read(buf, 0, (int)Math.Min(buf.Length, size - got)); }
                    catch (Exception e) { n = -1; Emit("warning", Interrupted(name, got, size, e.Message)); }
                    if (n == 0) Emit("warning", Interrupted(name, got, size, "connection closed"));
                    if (n <= 0) { fs.Flush(); return; } // keep .mqpart: the next attempt resumes from here

                    try { fs.Write(buf, 0, n); }
                    catch (Exception e)
                    {
                        fs.Dispose();
                        Xfer.TryDelete(part);
                        Xfer.TryDelete(info);
                        Reply(ns, "ERR\tIO\tWrite failed: " + e.Message);
                        Emit("error", "Writing " + name + " failed: " + e.Message);
                        return;
                    }
                    h.TransformBlock(buf, 0, n, null, 0);
                    got += n;
                    sinceFlush += n;
                    if (sinceFlush >= 32L * 1024 * 1024) { fs.Flush(); sinceFlush = 0; }
                    SetDone(name, got);
                }
                fs.Flush(true);
                h.TransformFinalBlock(new byte[0], 0, 0);
                string actual = Xfer.Hex(h.Hash);
                if (actual != sha)
                {
                    fs.Dispose();
                    Xfer.TryDelete(part);
                    Xfer.TryDelete(info);
                    Reply(ns, "ERR\tCHECKSUM\tChecksum mismatch");
                    Emit("error", "Checksum mismatch for " + name + ", file discarded");
                    return;
                }
            }

            if (InUse(name))
            {
                Reply(ns, "ERR\tINUSE\t" + name + " was loaded for playback during the transfer");
                return; // .mqpart kept, a retry after Stop finishes instantly
            }
            Integrity.Forget(final);
            Xfer.TryDelete(final);
            File.Move(part, final);
            Integrity.Record(final, sha);
            Xfer.TryDelete(info);
            Reply(ns, "DONE\t" + sha);
            Emit("info", "Received and verified " + name + " (" + Xfer.Mb(size) + ")");
        }

        private static string Interrupted(string name, long got, long size, string why)
        {
            return "Transfer of " + name + " interrupted at " + (got * 100 / size) + "% (" + why + ")";
        }
    }

    // ===================== PHONE SIDE =====================

    // One video chosen on the phone. Open() must return a NEW readable stream each call.
    public class MediaSource
    {
        public string Id = "";     // content:// uri or file path
        public string Name = "";
        public long Size;
        public Func<Stream> Open;
        public volatile string Sha = "";
        public volatile string HashError = "";
        public long Hashed;
        private int hashing;

        public bool HashReady { get { return Sha.Length > 0; } }

        public void StartHash()
        {
            if (HashReady || Interlocked.Exchange(ref hashing, 1) == 1) return;
            HashError = "";
            var t = new Thread(() =>
            {
                try
                {
                    using (Stream s = Open())
                    {
                        string h = Xfer.HashStream(s, null, n => Interlocked.Exchange(ref Hashed, n));
                        long len = Interlocked.Read(ref Hashed);
                        if (Size > 0 && len != Size) throw new IOException("Read " + len + " bytes, expected " + Size);
                        if (Size <= 0) Size = len;
                        Sha = h;
                    }
                }
                catch (Exception e) { HashError = "Cannot read video on phone: " + e.Message; }
                finally { Interlocked.Exchange(ref hashing, 0); }
            });
            t.IsBackground = true;
            t.Start();
        }
    }

    public enum JobState { WaitHash, Queued, Connecting, Sending, Verifying, Retrying, Done, Failed, Cancelled }

    // One file to ONE Quest, on its own thread and socket. Retries with resume; never reports success unless the Quest says DONE/HAVE.
    public class SendJob
    {
        public string DevKey;               // changed only by TransferManager.Rekey (main thread)
        public readonly MediaSource Src;
        public readonly bool PlayAfter;
        public Func<string> Ip;
        public int Port = Xfer.Port;
        public volatile JobState State = JobState.WaitHash;
        public volatile string Error = "";
        public volatile bool Skipped;     // Quest already had the verified file
        public int Attempt;
        public bool Reported;             // used by TransferManager
        public long Sent;
        public long SpeedBps;             // measured upload speed, bytes per second (0 when not sending)
        public float ConnectTimeout = 5f;
        private volatile bool cancel;

        public SendJob(string devKey, MediaSource src, bool playAfter)
        {
            DevKey = devKey;
            Src = src;
            PlayAfter = playAfter;
        }

        public bool Finished
        {
            get { return State == JobState.Done || State == JobState.Failed || State == JobState.Cancelled; }
        }

        public float Fraction
        {
            get { return Src.Size > 0 ? Math.Min(1f, (float)Interlocked.Read(ref Sent) / Src.Size) : 0f; }
        }

        public void Cancel() { cancel = true; }

        public void Start()
        {
            State = JobState.Connecting;
            var t = new Thread(Run);
            t.IsBackground = true;
            t.Start();
        }

        private void Run()
        {
            int checksumFails = 0;
            try
            {
                for (Attempt = 1; Attempt <= Xfer.MaxAttempts && !cancel; Attempt++)
                {
                    string err;
                    bool retry;
                    if (Once(ref checksumFails, out err, out retry)) { State = JobState.Done; return; }
                    Error = err;
                    if (cancel || !retry || Attempt >= Xfer.MaxAttempts) break;
                    State = JobState.Retrying;
                    int waitMs = Math.Min(30000, 1000 << Attempt);
                    for (int w = 0; w < waitMs && !cancel; w += 100) Thread.Sleep(100);
                }
            }
            catch (Exception e) { Error = e.Message; }
            State = cancel ? JobState.Cancelled : JobState.Failed;
        }

        private bool Once(ref int checksumFails, out string err, out bool retry)
        {
            err = "";
            retry = true;
            State = JobState.Connecting;
            string ip = Ip != null ? Ip() : "";
            try
            {
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect(ip, Port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne((int)(ConnectTimeout * 1000))) { err = "Connect timeout (" + ip + ")"; return false; }
                    c.EndConnect(ar);
                    c.NoDelay = true;
                    c.SendTimeout = 30000;
                    c.ReceiveTimeout = 600000; // resume re-hash / final hash on the Quest can take a while
                    c.SendBufferSize = Xfer.Buf;
                    NetworkStream ns = c.GetStream();
                    if (!MediSimAuth.ClientAnswer(ns, out err)) return false;

                    Xfer.WriteLine(ns, "PUT\t" + Src.Size + "\t" + Src.Sha + "\t" + Src.Name);
                    string r = Xfer.ReadLine(ns, 4096);
                    if (r == null) { err = "Quest closed the connection"; return false; }
                    string[] p = r.Split('\t');
                    if (p[0] == "HAVE")
                    {
                        Skipped = true;
                        Interlocked.Exchange(ref Sent, Src.Size);
                        return true;
                    }
                    if (p[0] == "ERR") return Fail(p, out err, out retry);
                    long offset;
                    if (p[0] != "OK" || p.Length < 2 || !long.TryParse(p[1], out offset) || offset < 0 || offset > Src.Size)
                    {
                        err = "Unexpected reply: " + r;
                        return false;
                    }

                    State = JobState.Sending;
                    Interlocked.Exchange(ref Sent, offset);
                    long winT = DateTime.UtcNow.Ticks, winB = offset;
                    using (Stream s = Src.Open())
                    {
                        SkipTo(s, offset);
                        var buf = new byte[Xfer.Buf];
                        long pos = offset;
                        while (pos < Src.Size)
                        {
                            if (cancel) { err = "Cancelled"; retry = false; return false; }
                            int n = s.Read(buf, 0, (int)Math.Min(buf.Length, Src.Size - pos));
                            if (n <= 0) { err = "Video on phone ended early (" + pos + " of " + Src.Size + " bytes)"; retry = false; return false; }
                            ns.Write(buf, 0, n);
                            pos += n;
                            Interlocked.Exchange(ref Sent, pos);
                            long nowT = DateTime.UtcNow.Ticks;
                            if (nowT - winT >= TimeSpan.TicksPerSecond / 2)
                            {
                                Interlocked.Exchange(ref SpeedBps, (long)((pos - winB) * (double)TimeSpan.TicksPerSecond / (nowT - winT)));
                                winT = nowT;
                                winB = pos;
                            }
                        }
                    }
                    ns.Flush();
                    Interlocked.Exchange(ref SpeedBps, 0);

                    State = JobState.Verifying;
                    r = Xfer.ReadLine(ns, 4096);
                    if (r == null) { err = "Quest closed the connection before confirming"; return false; }
                    p = r.Split('\t');
                    if (p[0] == "DONE" && p.Length >= 2 && p[1] == Src.Sha) return true;
                    if (p[0] == "ERR" && p.Length >= 2 && p[1] == "CHECKSUM")
                    {
                        checksumFails++;
                        err = "Checksum mismatch on Quest";
                        retry = checksumFails < 2; // second mismatch: source or network is bad, stop
                        return false;
                    }
                    if (p[0] == "ERR") return Fail(p, out err, out retry);
                    err = "Unexpected reply: " + r;
                    return false;
                }
            }
            catch (Exception e)
            {
                err = cancel ? "Cancelled" : e.Message;
                retry = !cancel;
                return false;
            }
            finally { Interlocked.Exchange(ref SpeedBps, 0); }
        }

        private static bool Fail(string[] p, out string err, out bool retry)
        {
            string code = p.Length > 1 ? p[1] : "";
            err = p.Length > 2 ? p[2] : code;
            retry = code == "BUSY" || code == "IO";
            return false;
        }

        private static void SkipTo(Stream s, long offset)
        {
            if (offset == 0) return;
            if (s.CanSeek) { s.Seek(offset, SeekOrigin.Begin); return; }
            var buf = new byte[Xfer.Buf];
            long left = offset;
            while (left > 0)
            {
                int n = s.Read(buf, 0, (int)Math.Min(buf.Length, left));
                if (n <= 0) throw new IOException("Cannot skip to resume position");
                left -= n;
            }
        }
    }

    // Runs SendJobs: hashes first, then at most MaxParallel uploads at once, and only ONE upload per Quest at a time
    // (several videos for the same Quest wait in a FIFO queue). Call everything from one thread only.
    public class TransferManager
    {
        public int MaxParallel = 3;
        public readonly List<SendJob> History = new List<SendJob>();   // finished jobs, newest first
        private readonly List<SendJob> jobs = new List<SendJob>();

        public List<SendJob> Jobs { get { return jobs; } }

        public static bool IsRunning(SendJob j)
        {
            return j.State == JobState.Connecting || j.State == JobState.Sending ||
                   j.State == JobState.Verifying || j.State == JobState.Retrying;
        }

        // Adds a video to this Quest's queue. The same video already waiting/running for this Quest is not added twice.
        public SendJob Add(string devKey, Func<string> ip, int port, MediaSource src, bool playAfter)
        {
            foreach (var x in jobs)
                if (x.DevKey == devKey && x.Src.Id == src.Id && !x.Finished) return x;
            var j = new SendJob(devKey, src, playAfter);
            j.Ip = ip;
            j.Port = port > 0 ? port : Xfer.Port;
            jobs.Add(j);
            src.StartHash();
            return j;
        }

        public void Cancel(SendJob j)
        {
            if (j == null || j.Finished) return;
            if (j.State == JobState.WaitHash || j.State == JobState.Queued) j.State = JobState.Cancelled;
            else j.Cancel(); // partial file stays on the Quest, a later send resumes it
        }

        public void CancelDev(string devKey)
        {
            foreach (var j in jobs) if (j.DevKey == devKey) Cancel(j);
        }

        public void CancelAll()
        {
            foreach (var j in jobs) Cancel(j);
        }

        // A Quest added by IP gets its real id once its beacon arrives.
        public void Rekey(string from, string to)
        {
            foreach (var j in jobs) if (j.DevKey == from) j.DevKey = to;
        }

        // The job to show for a Quest: the running one, else the next waiting one, else the last finished one.
        public SendJob Current(string devKey)
        {
            SendJob waiting = null, last = null;
            foreach (var j in jobs)
            {
                if (j.DevKey != devKey) continue;
                if (IsRunning(j)) return j;
                if (!j.Finished && waiting == null) waiting = j;
                last = j;
            }
            if (waiting != null) return waiting;
            if (last != null) return last;
            foreach (var h in History) if (h.DevKey == devKey) return h;
            return null;
        }

        public SendJob Latest(string devKey)
        {
            return Current(devKey);
        }

        public bool Busy(string devKey)
        {
            foreach (var j in jobs) if (j.DevKey == devKey && !j.Finished) return true;
            return false;
        }

        // Returns jobs that finished since the last call.
        public List<SendJob> Tick()
        {
            int running = 0;
            var busyDev = new HashSet<string>();
            foreach (var j in jobs)
            {
                if (IsRunning(j)) { running++; busyDev.Add(j.DevKey); }
            }
            foreach (var j in jobs)
            {
                if (j.State == JobState.WaitHash)
                {
                    if (j.Src.HashError.Length > 0) { j.Error = j.Src.HashError; j.State = JobState.Failed; }
                    else if (j.Src.HashReady) j.State = JobState.Queued;
                }
                if (j.State == JobState.Queued && running < MaxParallel && !busyDev.Contains(j.DevKey))
                {
                    j.Start();
                    running++;
                    busyDev.Add(j.DevKey);
                }
            }
            var done = new List<SendJob>();
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (!j.Finished || j.Reported) continue;
                j.Reported = true;
                done.Add(j);
            }
            // finished jobs move to History (kept short)
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                if (jobs[i].Finished && jobs[i].Reported)
                {
                    History.Insert(0, jobs[i]);
                    jobs.RemoveAt(i);
                }
            }
            while (History.Count > 40) History.RemoveAt(History.Count - 1);
            return done;
        }

        public void Shutdown()
        {
            foreach (var j in jobs) j.Cancel();
        }
    }
}
