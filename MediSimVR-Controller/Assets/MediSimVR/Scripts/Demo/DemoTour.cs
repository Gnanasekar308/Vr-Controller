using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Demo screenshots without touching the phone: start the app with
    //   adb shell am start -n com.medisim.vr/com.unity3d.player.UnityPlayerActivity -e unity "-demotour"
    // (or put an empty file "demo_tour.txt" into the app's files folder). In demo mode the app then walks through every
    // main screen and every theme preset, pressing its own buttons; each screen logs "[MediSimVR] DEMO-TOUR <nn> <name>"
    // and stays still for a moment so a PC script can take an adb screenshot. The theme presets are only previewed (not
    // saved), so the user's theme stays even if the tour is interrupted.
    // Also run by the Editor smoke test (fast mode, no waiting).
    public class DemoTour : MonoBehaviour
    {
        public Navigator nav;
        public float settle = 1.6f, hold = 2.2f;

        public static bool Running { get; private set; }
        public static int Steps { get; private set; }

        private int step;

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            bool arg = Array.IndexOf(Environment.GetCommandLineArgs(), "-demotour") >= 0;
            string file = Path.Combine(Application.persistentDataPath, "demo_tour.txt");
            bool flag = false;
            try { flag = File.Exists(file); if (flag) File.Delete(file); }
            catch (Exception e) { Debug.LogWarning("[MediSimVR] DEMO-TOUR flag file: " + e.Message); }
            if (arg || flag) Run(false);
        }

        // false = not started (already running, or demo mode is off)
        public bool Run(bool fast)
        {
            if (Running) { Debug.LogWarning("[MediSimVR] DEMO-TOUR is already running"); return false; }
            if (!AppRoot.Demo) { Debug.LogWarning("[MediSimVR] DEMO-TOUR needs demo mode (Settings > Demo mode)"); return false; }
            if (fast) { settle = 0.25f; hold = 0.05f; }
            Running = true;
            Steps = 0;
            StartCoroutine(Tour());
            return true;
        }

        // the tour stops with this object (scene reload, app / Play mode exit): clear its state. The theme presets of the
        // tour were never saved (ThemeManager.Preview), so the next start shows the user's saved theme.
        private void OnDisable()
        {
            if (!Running) return;
            StopAllCoroutines();
            Running = false;
            Debug.LogWarning("[MediSimVR] DEMO-TOUR stopped after " + step + " screens");
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(settle);
            step++;
            Steps = step;
            Debug.Log("[MediSimVR] DEMO-TOUR " + step.ToString("00") + " " + name);
            yield return new WaitForSecondsRealtime(hold);
        }

        private static void Click(Button b) { if (b != null) b.onClick.Invoke(); }

        private IEnumerator Tour()
        {
            step = 0;
            var ses = AppRoot.Session;
            Debug.Log("[MediSimVR] DEMO-TOUR start");

            nav.Tab(ScreenId.Home); yield return Shot("home");
            nav.Tab(ScreenId.Devices); yield return Shot("devices");
            nav.Tab(ScreenId.Send); yield return Shot("send");
            nav.Tab(ScreenId.Control); yield return Shot("control");

            // ----- session: host -----
            var start = nav.Get<SessionStartScreen>();
            var host = nav.Get<SessionHostScreen>();
            nav.Tab(ScreenId.Session); yield return Shot("session_start");
            Click(start.hostButton);
            if (nav.popup.root.activeSelf)
            {
                yield return Shot("session_host_already_active");
                Click(nav.popup.okButton); // Take over
            }
            yield return new WaitForSecondsRealtime(settle);
            if (host.selectAllLabel.text == "Select all") Click(host.selectAllButton);
            Click(host.playButton);
            yield return new WaitForSecondsRealtime(Mathf.Max(settle, 3f)); // a Quest starts speaking
            yield return Shot("session_host");
            var scroll = host.GetComponentInChildren<ScrollRect>();
            if (scroll != null) { scroll.verticalNormalizedPosition = 0f; yield return Shot("session_host_timeline"); scroll.verticalNormalizedPosition = 1f; }
            Click(host.sendButton); yield return Shot("session_send_content");
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[1]);
            Click(host.textButton);
            if (host.quickButtons.Count > 0) Click(host.quickButtons[0]);
            yield return new WaitForSecondsRealtime(DemoConfig.ReplyDelay * 2f + 0.5f); // replies arrive
            yield return Shot("session_text_chat");
            Click(host.textClose);

            // ----- scene of the session -----
            Click(host.sceneButton);
            var scene = nav.Get<SceneScreen>();
            yield return new WaitForSecondsRealtime(DemoConfig.SceneLoadSeconds + settle);
            if (scene.Doc != null && scene.Doc.Get("cube") != null)
            {
                scene.Doc.Select("cube");
                var c = scene.Doc.Get("cube");
                var p = c.pose;
                p.pos += new Vector3(0.5f, 0f, 0f);
                scene.Doc.Apply(c, p, "Move");   // shows up in the session timeline
            }
            yield return Shot("scene");
            Click(scene.inspectorButton); yield return Shot("scene_inspector");
            Click(scene.objectsButton); yield return Shot("scene_objects");
            Click(scene.hierarchyClose);

            // ----- end -> summary -----
            nav.Tab(ScreenId.Session);
            Click(start.activeButton);
            yield return new WaitForSecondsRealtime(0.3f);
            if (scroll != null) { scroll.verticalNormalizedPosition = 0f; yield return Shot("session_host_after_scene"); scroll.verticalNormalizedPosition = 1f; }
            Click(host.endButton);
            yield return new WaitForSecondsRealtime(0.3f);
            if (nav.popup.root.activeSelf) Click(nav.popup.okButton);
            yield return Shot("session_summary");

            // ----- player -----
            nav.Tab(ScreenId.Session);
            Click(start.playerButton); yield return Shot("session_player_pick");
            var pick = nav.Get<SessionPlayerPickScreen>();
            var cards = pick.GetComponentsInChildren<QuestCardView>(false);
            if (cards.Length > 1) Click(cards[1].button);
            yield return new WaitForSecondsRealtime(Mathf.Max(settle, 2.5f));
            yield return Shot("session_player");
            var player = nav.Get<SessionPlayerScreen>();
            Click(player.leaveButton);
            yield return new WaitForSecondsRealtime(0.3f);
            if (nav.popup.root.activeSelf) Click(nav.popup.okButton);

            // ----- settings, notifications, customization -----
            nav.Tab(ScreenId.Settings); yield return Shot("settings");
            nav.Push(ScreenId.EditApp); yield return Shot("edit_app");
            nav.Tab(ScreenId.Home); nav.Push(ScreenId.Notifications); yield return Shot("notifications");

            // ----- every theme preset: Home + host dashboard + scene -----
            if (ses != null && !ses.Active) ses.StartHost(true);
            foreach (string preset in ThemeData.PresetNames)
            {
                if (preset == "Custom") continue;
                ThemeManager.Preview(ThemeData.FromPreset(preset)); // not saved
                nav.Tab(ScreenId.Home); yield return Shot("theme_" + preset.ToLowerInvariant() + "_home");
                nav.Tab(ScreenId.Session); Click(start.activeButton); yield return Shot("theme_" + preset.ToLowerInvariant() + "_host");
                nav.Tab(ScreenId.Scene); yield return Shot("theme_" + preset.ToLowerInvariant() + "_scene");
            }
            if (ses != null && ses.Active) ses.End();
            ThemeManager.Load(); // the user's saved theme again
            nav.Tab(ScreenId.Home);
            Debug.Log("[MediSimVR] DEMO-TOUR done " + step + " screens");
            Running = false;
        }
    }
}
