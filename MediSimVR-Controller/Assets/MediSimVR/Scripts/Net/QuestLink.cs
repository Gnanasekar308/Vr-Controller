using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MQVR
{
    // One TCP control connection to ONE Quest (from ControllerCore). Answers the MediSimVR challenge first.
    public class QuestLink
    {
        public readonly string Ip;
        public readonly int Port;
        public string Error = "";

        private TcpClient tcp;
        private StreamWriter writer;
        private volatile bool alive;
        private volatile bool connecting;
        private volatile bool closedByUser;
        private string reply;

        public bool IsConnected { get { return alive; } }
        public bool IsConnecting { get { return connecting; } }

        public QuestLink(string ip, int port)
        {
            Ip = ip;
            Port = port;
        }

        public void Connect()
        {
            if (alive || connecting) return;
            connecting = true;
            closedByUser = false;
            Error = "";
            var t = new Thread(Run);
            t.IsBackground = true;
            t.Start();
        }

        private void Run()
        {
            try
            {
                tcp = new TcpClient();
                tcp.NoDelay = true;
                var ar = tcp.BeginConnect(Ip, Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(4000)) throw new Exception("Connect timeout");
                tcp.EndConnect(ar);
                tcp.ReceiveTimeout = 8000;
                tcp.SendTimeout = 2000;

                var stream = tcp.GetStream();
                string aerr;
                if (!MediSimAuth.ClientAnswer(stream, out aerr)) throw new Exception(aerr);

                writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.AutoFlush = true;
                writer.NewLine = "\n";
                alive = true;
                connecting = false;

                var pinger = new Thread(PingLoop);
                pinger.IsBackground = true;
                pinger.Start();

                var reader = new StreamReader(stream, Encoding.UTF8);
                string line;
                while (alive && (line = reader.ReadLine()) != null)
                {
                    Interlocked.Exchange(ref reply, line);
                }
                if (!closedByUser) Error = "Connection closed by Quest";
            }
            catch (Exception e)
            {
                if (!closedByUser) Error = e.Message;
            }
            finally
            {
                alive = false;
                connecting = false;
                try { if (tcp != null) tcp.Close(); } catch (Exception) { }
            }
        }

        private void PingLoop()
        {
            while (alive)
            {
                Send("{\"cmd\":\"ping\"}");
                Thread.Sleep(2000);
            }
        }

        // true only if the line was really written to the socket
        public bool Send(string json)
        {
            try
            {
                var w = writer;
                if (w == null || !alive) return false;
                lock (w) { w.WriteLine(json); }
                return true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                alive = false;
                try { if (tcp != null) tcp.Close(); } catch (Exception) { }
                return false;
            }
        }

        public string TakeReply()
        {
            return Interlocked.Exchange(ref reply, null);
        }

        public void Disconnect()
        {
            closedByUser = true;
            alive = false;
            try { if (tcp != null) tcp.Close(); } catch (Exception) { }
        }
    }
}
