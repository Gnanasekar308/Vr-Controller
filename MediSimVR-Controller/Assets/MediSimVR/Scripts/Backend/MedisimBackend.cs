using System;
using UnityEngine;

namespace MediSimVR
{
    // Which real backend the app uses when demo mode is OFF.
    //   UseServer = true : the Medisim PC server (MedisimVR.LocalServer, HTTP 5080) and its Windows student stations
    //                      (one PC + Quest over Link each): ServerControllerService, ServerSessionService, ServerSceneLink
    //   UseServer = false: the direct Quest receiver protocol (UDP 8766 / TCP 8765 / 8767 / UDP 8771):
    //                      NetworkControllerService + SceneSyncClient, no session service
    // Demo mode (DemoConfig.Enabled) is not affected.
    public static class MedisimBackend
    {
        public static readonly bool UseServer = true;

        private const string FacultyIdKey = "msvr_faculty_id";

        public static IControllerService CreateController()
        {
            return UseServer ? (IControllerService)new ServerControllerService() : new NetworkControllerService();
        }

        public static ISessionService CreateSession(IControllerService controller)
        {
            var server = controller as ServerControllerService;
            return server != null ? new ServerSessionService(server) : null;
        }

        public static ISceneLink CreateSceneLink()
        {
            return UseServer ? (ISceneLink)new ServerSceneLink() : new SceneSyncClient();
        }

        // Voice id of this phone on the server's relay (must start with "faculty-"). Created once, kept on the phone.
        public static string FacultyId()
        {
            string id = PlayerPrefs.GetString(FacultyIdKey, "");

            if (!id.StartsWith("faculty-"))
            {
                id = "faculty-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                PlayerPrefs.SetString(FacultyIdKey, id);
                PlayerPrefs.Save();
            }

            return id;
        }

        // Settings > Network text.
        public static string NetworkInfo()
        {
            if (!UseServer)
            {
                return "Discovery: UDP 8766\nControl: TCP 8765\nTransfer: TCP 8767\nScene: UDP 8771";
            }

            MedisimServer s = MedisimServer.Instance;
            return "Medisim PC server: HTTP 5080\nDiscovery: UDP 5081\nVoice: UDP 5082\n\n" +
                   (s.HasServer ? "Connected: " + (s.MachineName.Length > 0 ? s.MachineName + " " : "") + "(" + s.Host + ")" : s.Status) +
                   (s.ManualIp.Length > 0 ? "\nManual IP: " + s.ManualIp : "");
        }
    }
}
