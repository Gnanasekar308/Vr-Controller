using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace MediSimVR
{
    public class HttpReply
    {
        public int Code;
        public string Text = "";
        public string Error = "";
        public bool Ok { get { return Error.Length == 0 && Code >= 200 && Code < 300; } }

        public override string ToString()
        {
            return Error.Length > 0 ? Error : "HTTP " + Code;
        }
    }

    // Minimal HTTP/1.1 client over a plain socket, for the Medisim PC server on the lab Wi-Fi (http, port 5080).
    // Blocking: call it from a worker thread (MedisimServer does). Plain TCP on purpose: no Android cleartext-HTTP
    // policy, no code stripping problems, and large files are streamed (never loaded into memory).
    public static class MiniHttp
    {
        public const int MaxResponseBytes = 16 * 1024 * 1024;
        private const int Buffer = 256 * 1024;

        public static HttpReply Send(string method, string url, string json, int timeoutMs)
        {
            byte[] body = json == null ? null : Encoding.UTF8.GetBytes(json);
            return Send(method, url, "application/json", body == null ? null : new MemoryStream(body), body == null ? 0 : body.Length, timeoutMs, null, null);
        }

        // body may be null. progress(bytesSent) and cancelled() are called on this (worker) thread.
        public static HttpReply Send(string method, string url, string contentType, Stream body, long length, int timeoutMs,
            Action<long> progress, Func<bool> cancelled)
        {
            var reply = new HttpReply();

            try
            {
                var uri = new Uri(url);

                using (var tcp = new TcpClient())
                {
                    IAsyncResult ar = tcp.BeginConnect(uri.Host, uri.Port, null, null);

                    if (!ar.AsyncWaitHandle.WaitOne(Math.Min(timeoutMs, 5000)))
                    {
                        reply.Error = "PC server not reachable (" + uri.Host + ")";
                        return reply;
                    }

                    tcp.EndConnect(ar);
                    tcp.NoDelay = true;
                    tcp.SendTimeout = timeoutMs;
                    tcp.ReceiveTimeout = timeoutMs;
                    NetworkStream stream = tcp.GetStream();

                    var head = new StringBuilder(256);
                    head.Append(method).Append(' ').Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n");
                    head.Append("Host: ").Append(uri.Host).Append(':').Append(uri.Port).Append("\r\n");
                    head.Append("Connection: close\r\n");

                    if (body != null)
                    {
                        head.Append("Content-Type: ").Append(contentType ?? "application/octet-stream").Append("\r\n");
                        head.Append("Content-Length: ").Append(length).Append("\r\n");
                    }
                    else if (method == "POST" || method == "PUT")
                    {
                        head.Append("Content-Length: 0\r\n");
                    }

                    head.Append("\r\n");
                    byte[] headBytes = Encoding.ASCII.GetBytes(head.ToString());
                    stream.Write(headBytes, 0, headBytes.Length);

                    if (body != null)
                    {
                        byte[] buf = new byte[Buffer];
                        long sent = 0;

                        while (sent < length)
                        {
                            if (cancelled != null && cancelled())
                            {
                                reply.Error = "Cancelled";
                                return reply;
                            }

                            int n = body.Read(buf, 0, (int)Math.Min(buf.Length, length - sent));

                            if (n <= 0)
                            {
                                reply.Error = "The file ended early (" + sent + " of " + length + " bytes)";
                                return reply;
                            }

                            stream.Write(buf, 0, n);
                            sent += n;

                            if (progress != null)
                            {
                                progress(sent);
                            }
                        }
                    }

                    ReadResponse(stream, reply);
                }
            }
            catch (Exception e)
            {
                reply.Error = e is SocketException || e is IOException ? "PC server not reachable: " + e.Message : e.Message;
            }

            return reply;
        }

        private static void ReadResponse(Stream stream, HttpReply reply)
        {
            var all = new MemoryStream();
            byte[] buf = new byte[16 * 1024];
            int n;

            while ((n = stream.Read(buf, 0, buf.Length)) > 0)
            {
                all.Write(buf, 0, n);

                if (all.Length > MaxResponseBytes)
                {
                    reply.Error = "Response too large";
                    return;
                }
            }

            byte[] data = all.ToArray();
            int headerEnd = IndexOf(data, new byte[] { 13, 10, 13, 10 });

            if (headerEnd < 0)
            {
                reply.Error = "Invalid answer from the PC server";
                return;
            }

            string[] lines = Encoding.ASCII.GetString(data, 0, headerEnd).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] status = lines[0].Split(' ');
            int code;

            if (status.Length < 2 || !int.TryParse(status[1], out code))
            {
                reply.Error = "Invalid answer from the PC server";
                return;
            }

            reply.Code = code;
            bool chunked = false;

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase) &&
                    lines[i].IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    chunked = true;
                }
            }

            int start = headerEnd + 4;
            byte[] body = chunked ? Dechunk(data, start) : Slice(data, start, data.Length - start);
            reply.Text = Encoding.UTF8.GetString(body);
        }

        private static byte[] Dechunk(byte[] data, int pos)
        {
            var output = new MemoryStream();

            while (pos < data.Length)
            {
                int lineEnd = IndexOf(data, new byte[] { 13, 10 }, pos);

                if (lineEnd < 0)
                {
                    break;
                }

                string sizeText = Encoding.ASCII.GetString(data, pos, lineEnd - pos).Split(';')[0].Trim();
                int size;

                if (!int.TryParse(sizeText, System.Globalization.NumberStyles.HexNumber, null, out size) || size <= 0)
                {
                    break;
                }

                pos = lineEnd + 2;

                if (pos + size > data.Length)
                {
                    size = data.Length - pos;
                }

                output.Write(data, pos, size);
                pos += size + 2;
            }

            return output.ToArray();
        }

        private static byte[] Slice(byte[] data, int start, int count)
        {
            var b = new byte[Math.Max(0, count)];
            Array.Copy(data, start, b, 0, b.Length);
            return b;
        }

        private static int IndexOf(byte[] data, byte[] pattern, int from = 0)
        {
            for (int i = from; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;

                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
