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
    // main screen of the Control Mode flow (and the theme presets), pressing its own buttons and checking the way back;
    // a failed check is logged as an error. Each screen logs "[MediSimVR] DEMO-TOUR <nn> <name>"
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

        // flow checks: a failure is logged as an error, so the Editor smoke test fails on it
        private void Expect(ScreenId id, string what) { Check(nav.Current == id, what + " (now on " + nav.Current + ")"); }

        private static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[MediSimVR] DEMO-TOUR check OK: " + what);
            else Debug.LogError("[MediSimVR] DEMO-TOUR check FAILED: " + what);
        }

        // confirm a popup if one is open (screenshot of it first when 'shot' is given)
        private IEnumerator ConfirmIfAsked(string shot)
        {
            if (!nav.popup.root.activeSelf) yield break;
            if (shot != null) yield return Shot(shot);
            Click(nav.popup.okButton);
        }

        private IEnumerator Tour()
        {
            step = 0;
            var ses = AppRoot.Session;
            var svc = AppRoot.Service;
            var home = nav.Get<HomeScreen>();
            var start = nav.Get<SessionStartScreen>();
            var list = nav.Get<QuestListScreen>();
            var play = nav.Get<PlayScreen>();
            var pick = nav.Get<SessionPlayerPickScreen>();
            var host = nav.Get<SessionHostScreen>();
            Debug.Log("[MediSimVR] DEMO-TOUR start");

            // ----- Home > Control Mode > Session -----
            nav.Tab(ScreenId.Home); yield return Shot("home");
            Click(home.controlModeButton);
            Expect(ScreenId.Session, "Control Mode opens the Session screen");
            yield return Shot("session_start");

            // ----- HOST > Quest list -----
            Click(start.hostButton);
            yield return ConfirmIfAsked("session_host_already_active");   // demo: another phone hosts, take over
            Expect(ScreenId.QuestList, "HOST opens the Quest list");
            yield return Shot("quest_list");
            list.filterSeg.Select(3, true); yield return Shot("quest_list_filter_in_use");
            list.filterSeg.Select(2, true); yield return Shot("quest_list_filter_offline");
            QuestDevice offline = null;
            foreach (var d in svc.Devices) if (!d.Online) offline = d;
            if (offline != null && list.list.Rows.Count > 0)
            {
                Click(list.list.Rows[0].button);
                Check(!offline.Selected, "an offline Quest cannot be selected");
            }
            list.filterSeg.Select(0, true);
            if (list.list.Rows.Count > 1) { Click(list.list.Rows[0].button); Click(list.list.Rows[1].button); }
            Check(svc.SelectedOnline().Count == 2, "two Quests selected");
            yield return Shot("quest_list_selected");
            Click(list.continueButton);
            Expect(ScreenId.Play, "Continue opens the Play page");

            // ----- Play page (host): video -----
            Click(play.kindSeg.buttons[(int)MediaKind.Video]);
            yield return Shot("play_choose_video");
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[1]);   // the second demo video
            yield return Shot("play_host_ready");
            Click(play.sendButton);
            yield return ConfirmIfAsked("play_replace_content");   // a Quest already plays another video
            yield return new WaitForSecondsRealtime(DemoConfig.FakeSendSeconds + Mathf.Max(settle, 2.5f));
            yield return Shot("play_host_playing");
            Click(play.controls.pauseButton); yield return Shot("play_host_paused");
            Click(play.controls.resumeButton);

            // ----- Play page (host): image -> Show / Hide -----
            Click(play.kindSeg.buttons[(int)MediaKind.Image]);
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[0]);
            Click(play.sendButton);
            yield return ConfirmIfAsked(null);
            yield return new WaitForSecondsRealtime(DemoConfig.FakeSendSeconds + 1f);
            yield return Shot("play_host_image");

            // ----- Change (host) = the Quest list; Continue comes back -----
            Click(play.changeTargetButton);
            Expect(ScreenId.QuestList, "Change on the Play page goes to the Quest list (host)");
            Click(list.continueButton);
            Expect(ScreenId.Play, "Continue goes back to the Play page");

            // ----- back: Play -> Quest list (header back) -> Session (Android back) -----
            Click(play.backButton);
            Expect(ScreenId.QuestList, "back from the Play page goes to the Quest list (host)");
            yield return Shot("quest_list_in_use");
            nav.Back();
            Expect(ScreenId.Session, "back from the Quest list goes to the Session screen");
            yield return Shot("session_start_live");

            // ----- live dashboard of the session: voice, timeline, chat -----
            Click(start.activeButton);
            Expect(ScreenId.SessionHost, "the live session card opens the host dashboard");
            yield return new WaitForSecondsRealtime(Mathf.Max(settle, 3f)); // a Quest starts speaking
            yield return Shot("session_host");
            var scroll = host.GetComponentInChildren<ScrollRect>();
            if (scroll != null) { scroll.verticalNormalizedPosition = 0f; yield return Shot("session_host_timeline"); scroll.verticalNormalizedPosition = 1f; }
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
            Click(host.endButton);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return ConfirmIfAsked(null);
            yield return Shot("session_summary");

            // ----- PLAYER: straight to the Play page on My Quest -----
            nav.Tab(ScreenId.Session);
            Click(start.playerButton);
            Expect(ScreenId.Play, "PLAYER opens the Play page");
            Check(ControlFlow.IsPlayer && svc.SelectedOnline().Count == 1, "player mode acts on one Quest (My Quest)");
            yield return Shot("play_player");
            Click(play.changeTargetButton);
            Expect(ScreenId.SessionPlayerPick, "Change opens the My Quest list");
            yield return Shot("player_change_quest");
            if (pick.list.Rows.Count > 1) Click(pick.list.Rows[1].button);   // the second Quest
            Expect(ScreenId.Play, "picking a Quest goes back to the Play page");
            Click(play.kindSeg.buttons[(int)MediaKind.Audio]);
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[0]);
            Click(play.sendButton);
            yield return ConfirmIfAsked(null);
            yield return new WaitForSecondsRealtime(DemoConfig.FakeSendSeconds + Mathf.Max(settle, 2.5f));
            yield return Shot("play_player_audio");
            nav.Back();
            Expect(ScreenId.Session, "back from the Play page goes to the Session screen (player)");
            nav.Back();
            Expect(ScreenId.Home, "back from the Session screen goes Home");
            yield return Shot("home_player_mode");

            // ----- tabs built from the same components -----
            nav.Tab(ScreenId.Devices); yield return Shot("devices");
            nav.Tab(ScreenId.Send); yield return Shot("send");
            nav.Tab(ScreenId.Control); yield return Shot("control");

            // ----- settings, notifications, customization -----
            nav.Tab(ScreenId.Settings); yield return Shot("settings");
            nav.Push(ScreenId.EditApp); yield return Shot("edit_app");
            nav.Tab(ScreenId.Home); nav.Push(ScreenId.Notifications); yield return Shot("notifications");

            // ----- every theme preset on the new screens: Quest list + Play page (host) -----
            if (ses != null)
            {
                if (ses.Active) ses.End();
                ses.StartHost(true);
            }
            foreach (var d in svc.Devices) d.Selected = d.Online;
            foreach (string preset in ThemeData.PresetNames)
            {
                if (preset == "Custom") continue;
                ThemeManager.Preview(ThemeData.FromPreset(preset)); // not saved
                nav.Tab(ScreenId.Session); nav.Push(ScreenId.QuestList); yield return Shot("theme_" + preset.ToLowerInvariant() + "_quest_list");
                nav.Push(ScreenId.Play); yield return Shot("theme_" + preset.ToLowerInvariant() + "_play");
            }
            if (ses != null && ses.Active) ses.End();
            foreach (var d in svc.Devices) d.Selected = false;
            ThemeManager.Load(); // the saved theme again
            nav.Tab(ScreenId.Home);
            Debug.Log("[MediSimVR] DEMO-TOUR done " + step + " screens");
            Running = false;
        }
    }
}
