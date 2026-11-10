using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // PC sync scene only (PCSync.unity, Editor Play mode): keeps running in the background and shows
    // PC name, IP(s), UDP port, object count and whether the phone is sending (OnGUI).
    public class PCSyncStatus : MonoBehaviour
    {
        public SyncReceiver receiver;

        private readonly List<string> ips = new List<string>();
        private float nextIpRefresh;
        private GUIStyle box, big;

        private void Awake()
        {
            Application.runInBackground = true; // keeps receiving while the phone is in your hand and Unity is not focused
            RefreshIps();
            Debug.Log("[MediSimVR] PC sync: IP " + string.Join(", ", ips.ToArray()) + ", UDP " + SceneSyncProtocol.Port);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextIpRefresh) return;
            nextIpRefresh = Time.unscaledTime + 5f;
            RefreshIps();
        }

        private void RefreshIps()
        {
            ips.Clear();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string s = ua.Address.ToString();
                        if (s.StartsWith("127.") || s.StartsWith("169.254")) continue;
                        if (!ips.Contains(s)) ips.Add(s);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[MediSimVR] PC sync: could not read IPs: " + e.Message); }
            if (ips.Count == 0) ips.Add("no network");
        }

        private void OnGUI()
        {
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 18, richText = true, padding = new RectOffset(14, 14, 10, 10) };
                big = new GUIStyle(box) { fontSize = 24, fontStyle = FontStyle.Bold };
            }
            string state;
            if (receiver == null) state = "<color=#ff6060>No SyncReceiver</color>";
            else if (!receiver.IsListening) state = "<color=#ff6060>Cannot listen on UDP " + SceneSyncProtocol.Port + ": " + receiver.ListenError + "</color>";
            else if (receiver.PhoneConnected) state = "<color=#5ad17a>Phone connected</color> (" + receiver.LastSender + ")";
            else state = "<color=#ffb340>Waiting for the phone...</color>";

            GUILayout.BeginArea(new Rect(16, 16, 600, 290));
            GUILayout.Label("MediSimVR PC Sync  -  " + state, big);
            GUILayout.Label(
                "Name: " + (receiver != null ? receiver.DeviceName : "-") + "\n" +
                "PC IP: " + string.Join(", ", ips.ToArray()) + "\n" +
                "UDP port: " + SceneSyncProtocol.Port + "   Objects: " + (receiver != null ? receiver.ObjectCount : 0) + "\n" +
                (receiver != null ? "Transforms: " + receiver.AcceptedTransforms + " accepted, " + receiver.IgnoredOldTransforms +
                                    " old ignored, " + receiver.UnknownIdTransforms + " unknown id, " + receiver.DroppedPackets + " dropped (bad sig)\n" +
                                    "Scene sent to the phone: " + receiver.SceneRequests + " time(s)" : ""), box);
            GUILayout.EndArea();
        }
    }
}
