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
    // step of the Control Mode flow (Session > Devices > Send > Control > Scene, player path, record), pressing its own
    // buttons and checking each step and the way back;
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


        // scroll position of the current screen's list (1 = top, 0 = bottom)
        private static void ScrollTo(AppScreen s, float y)
        {
            var sc = s.GetComponentInChildren<ScrollRect>();
            if (sc != null) { Canvas.ForceUpdateCanvases(); sc.verticalNormalizedPosition = y; }
        }

        private static bool AnyPlaying(IControllerService svc)
        {
            foreach (var d in svc.SelectedOnline()) if (d.State == DeviceState.Playing) return true;
            return false;
        }

        // pick a file through the app's own content sheets: type (0 Video, 1 Audio, 2 Image, 3 Text), then a file
        private IEnumerator PickContent(Button select, int type, int file, string shotOfList)
        {
            Click(select);
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[type]);
            if (shotOfList != null) yield return Shot(shotOfList);
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[file]);
        }

        private IEnumerator Tour()
        {
            step = 0;
            var ses = AppRoot.Session;
            var svc = AppRoot.Service;
            var home = nav.Get<HomeScreen>();
            var start = nav.Get<SessionStartScreen>();
            var devices = nav.Get<DevicesScreen>();
            var send = nav.Get<SendScreen>();
            var control = nav.Get<ControlScreen>();
            var pick = nav.Get<SessionPlayerPickScreen>();
            var host = nav.Get<SessionHostScreen>();
            var scene = nav.Get<SceneScreen>();
            Debug.Log("[MediSimVR] DEMO-TOUR start");

            // ----- Home, bottom bar -----
            nav.Tab(ScreenId.Home); yield return Shot("home");
            var tabs = "";
            foreach (var item in nav.navItems) tabs += item.target + " ";
            Check(tabs == "Home Session Devices Send Control Scene ", "bottom bar: Home, Session, Devices, Send, Control, Scene (" + tabs.Trim() + ")");

            // ----- Control Mode > Session > HOST > Devices (step 1) -----
            Click(home.controlModeButton);
            Expect(ScreenId.Session, "Control Mode opens the Session screen");
            yield return Shot("session_start");
            Click(start.hostButton);
            yield return ConfirmIfAsked("session_host_already_active");   // demo: another phone hosts, take over
            Expect(ScreenId.Devices, "HOST opens the Devices tab (step 1)");
            yield return Shot("devices");
            devices.filterSeg.Select(3, true); yield return Shot("devices_filter_in_use");
            devices.filterSeg.Select(2, true);
            QuestDevice offline = null;
            foreach (var d in svc.Devices) if (!d.Online) offline = d;
            if (offline != null && devices.list.Rows.Count > 0)
            {
                Click(devices.list.Rows[0].button);
                Check(!offline.Selected, "an offline Quest cannot be selected");
            }
            devices.filterSeg.Select(0, true);
            if (devices.list.Rows.Count > 1) { Click(devices.list.Rows[0].button); Click(devices.list.Rows[1].button); }
            Check(svc.SelectedOnline().Count == 2, "two Quests selected");
            yield return Shot("devices_selected");

            // ----- Connect > Send (step 2) -----
            Click(devices.connectButton);
            Expect(ScreenId.Send, "Connect opens the Send tab (step 2)");
            yield return PickContent(send.selectButton, 0, 1, "send_choose_video");   // CPR_Training.mp4
            yield return Shot("send_ready");
            Click(send.sendButton);
            yield return ConfirmIfAsked("send_replace_content");   // Quest-A1B2 already plays another video
            Expect(ScreenId.Control, "Send opens the Control tab (step 3)");

            // ----- Control: the video plays on the Quests (and on this phone when the file has media) -----
            yield return new WaitForSecondsRealtime(DemoConfig.FakeSendSeconds + Mathf.Max(settle, 2.5f));
            Check(AnyPlaying(svc), "the video plays on the Quests after Send");
            ScrollTo(control, 1f); yield return Shot("control_playing");
            Click(control.controls.pauseButton);
            Check(!AnyPlaying(svc), "Pause pauses the Quests");
            ScrollTo(control, 0.62f); yield return Shot("control_buttons_paused");
            Click(control.controls.resumeButton);
            Check(AnyPlaying(svc), "Resume plays again");

            // ----- Scene tab: pauses the video; Android back returns to Control, still paused until Play -----
            ScrollTo(control, 1f);
            foreach (var item in nav.navItems) if (item.target == ScreenId.Scene) Click(item.button);   // the Scene tab
            Expect(ScreenId.Scene, "the Scene tab opens the Scene");
            Check(!AnyPlaying(svc), "opening the Scene pauses the video");
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
            scene.toolSeg.Select(SceneScreen.SelectToolIndex, true);
            scene.Edit.UpdateGizmo();
            Check(!scene.gizmo.Visible, "Select tool hides the gizmo");
            scene.toolSeg.Select(SceneScreen.ToolIndex(GizmoTool.Move), true);
            Click(scene.expandButton);
            Check(scene.FullView, "the expand button opens the full view");
            yield return Shot("scene_full_view");
            nav.Back();
            Check(!scene.FullView && nav.Current == ScreenId.Scene, "back leaves the full view and stays on Scene");
            nav.Back();   // Android back
            Expect(ScreenId.Control, "back from the Scene returns to Control");
            yield return new WaitForSecondsRealtime(0.6f);
            Check(!AnyPlaying(svc), "the video stays paused until Play / Resume");
            ScrollTo(control, 1f); yield return Shot("control_after_scene");
            Click(control.controls.resumeButton);
            Check(AnyPlaying(svc), "Resume continues the video after the Scene");

            // ----- audio (sender / receiver), live voice, lesson notes, important instructions -----
            control.questMicSwitch.Set(false, true);
            Check(!SessionLog.QuestMics, "Quest mics can be muted");
            control.questMicSwitch.Set(true, true);
            control.phoneMicSwitch.Set(false, true);
            Check(!SessionLog.PhoneMic, "the phone mic can be muted");
            control.phoneMicSwitch.Set(true, true);
            ScrollTo(control, 0.38f); yield return Shot("control_audio_voice");
            control.noteInput.text = "Chest compressions: 30 per cycle, then 2 breaths";
            Click(control.readNoteButton);
            Check(SessionLog.Notes.Count == 1, "a lesson note is recorded and read aloud");
            if (control.instructionButtons.Count > 0) Click(control.instructionButtons[0]);
            Check(SessionLog.InstructionsSent.Count == 1, "an important instruction is recorded and read aloud");
            ScrollTo(control, 0f); yield return Shot("control_notes_instructions");

            // ----- the JSON session record on the phone -----
            string json = SessionLog.Save();
            Check(json != null && File.Exists(SessionLog.FilePath), "session record saved as JSON on the phone: " + SessionLog.FilePath);
            Check(json != null && json.Contains("Chest compressions") && json.Contains("Put on your gloves") && json.Contains("\"events\""),
                "the JSON has the lesson note, the instruction and the events");

            // ----- live dashboard of the session: voice, timeline, chat -----
            nav.Tab(ScreenId.Session);
            yield return Shot("session_start_live");
            Click(start.activeButton);
            Expect(ScreenId.SessionHost, "the live session card opens the host dashboard");
            yield return new WaitForSecondsRealtime(Mathf.Max(settle, 2f));
            yield return Shot("session_host");
            var scroll = host.GetComponentInChildren<ScrollRect>();
            if (scroll != null) { scroll.verticalNormalizedPosition = 0f; yield return Shot("session_host_timeline"); scroll.verticalNormalizedPosition = 1f; }
            Click(host.textButton);
            if (host.quickButtons.Count > 0) Click(host.quickButtons[0]);
            yield return new WaitForSecondsRealtime(DemoConfig.ReplyDelay * 2f + 0.5f); // replies arrive
            yield return Shot("session_text_chat");
            Click(host.textClose);

            // ----- end -> summary; the record goes to the PC -----
            Click(host.endButton);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return ConfirmIfAsked("end_session_confirm");
            yield return new WaitForSecondsRealtime(DemoConfig.UploadSeconds + 0.6f);
            Check(SessionLog.UploadStatus.StartsWith("Sent to"), "the session record is sent to the PC at the end (" + SessionLog.UploadStatus + ")");
            string endedJson = File.Exists(SessionLog.FilePath) ? File.ReadAllText(SessionLog.FilePath) : "";
            Check(endedJson.Contains("\"sentToPc\": \"Sent to") && endedJson.Contains("\"endedAt\": \"2"),
                "the saved record has the end time and the PC status");
            yield return Shot("session_summary");

            // ----- PLAYER: Send (step 1 of 2) on My Quest > Control -----
            nav.Tab(ScreenId.Session);
            Click(start.playerButton);
            Expect(ScreenId.Send, "PLAYER opens the Send tab (step 1 of 2)");
            Check(ControlFlow.IsPlayer && svc.SelectedOnline().Count == 1, "player mode sends to one Quest (My Quest)");
            yield return Shot("player_send");
            Click(send.changeTargetButton);
            Expect(ScreenId.SessionPlayerPick, "Change opens the My Quest list");
            yield return Shot("player_my_quest");
            if (pick.list.Rows.Count > 1) Click(pick.list.Rows[1].button);   // the second Quest
            Expect(ScreenId.Send, "picking a Quest goes back to Send");
            yield return PickContent(send.selectButton, 1, 0, null);   // Briefing_Audio.mp3
            Click(send.sendButton);
            yield return ConfirmIfAsked(null);
            Expect(ScreenId.Control, "Send opens Control (player)");
            yield return new WaitForSecondsRealtime(DemoConfig.FakeSendSeconds + Mathf.Max(settle, 2f));
            ScrollTo(control, 1f); yield return Shot("player_control");
            nav.Back();
            Expect(ScreenId.Send, "back from Control goes to Send");
            nav.Back();
            Expect(ScreenId.Session, "back from Send goes to the Session screen");
            nav.Back();
            Expect(ScreenId.Home, "back from the Session screen goes Home");
            yield return Shot("home_player_mode");

            // ----- settings (Home header), notifications, customization -----
            Click(home.settingsButton);
            Expect(ScreenId.Settings, "the Settings icon in the Home header opens Settings");
            yield return Shot("settings");
            nav.Push(ScreenId.EditApp); yield return Shot("edit_app");
            nav.Back();
            Expect(ScreenId.Settings, "back from Edit App goes to Settings");
            var settings = nav.Get<SettingsScreen>();
            Click(settings.backButton);
            Expect(ScreenId.Home, "the Settings back button goes Home");
            Click(home.bellButton); yield return Shot("notifications");

            // ----- every theme preset: Home, Devices, Control -----
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
                string p = "theme_" + preset.ToLowerInvariant();
                nav.Tab(ScreenId.Home); yield return Shot(p + "_home");
                nav.Tab(ScreenId.Devices); yield return Shot(p + "_devices");
                nav.Tab(ScreenId.Control); ScrollTo(control, 1f); yield return Shot(p + "_control");
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
