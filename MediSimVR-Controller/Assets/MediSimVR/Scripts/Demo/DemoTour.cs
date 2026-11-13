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
    // step of the app flow (Splash > Role > Admin login > Dashboard > Meeting Room > Create Meeting > Details > Live
    // session > Report, Devices, Settings, Logout, Student login > Meetings > Join > Live), pressing its own buttons and
    // checking each step and the way back;
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

        private static void ScrollPanel(RectTransform panel, float y)
        {
            var sc = panel != null ? panel.GetComponent<ScrollRect>() : null;
            if (sc != null) { Canvas.ForceUpdateCanvases(); sc.verticalNormalizedPosition = y; }
        }

        private static bool AnyPlaying(IControllerService svc)
        {
            foreach (var d in svc.SelectedOnline()) if (d.State == DeviceState.Playing) return true;
            return false;
        }

        private void TapTab(ScreenId id) { foreach (var item in nav.navItems) if (item.target == id) Click(item.button); }

        private string VisibleTabs()
        {
            var tabs = "";
            foreach (var item in nav.navItems) if (item.button.gameObject.activeSelf) tabs += item.target + " ";
            return tabs;
        }

        private IEnumerator Tour()
        {
            step = 0;
            var ses = AppRoot.Session;
            var svc = AppRoot.Service;
            var role = nav.Get<RoleSelectScreen>();
            var login = nav.Get<LoginScreen>();
            var dash = nav.Get<DashboardScreen>();
            var meetings = nav.Get<MeetingsScreen>();
            var create = nav.Get<MeetingCreateScreen>();
            var details = nav.Get<MeetingDetailsScreen>();
            var live = nav.Get<LiveSessionScreen>();
            var report = nav.Get<SessionReportScreen>();
            var scene = nav.Get<SceneScreen>();
            var settings = nav.Get<SettingsScreen>();
            Debug.Log("[MediSimVR] DEMO-TOUR start");

            // ----- launch: splash -> role selection (no bottom bar before login) -----
            if (nav.Current == ScreenId.Splash)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                step++;
                Steps = step;
                Debug.Log("[MediSimVR] DEMO-TOUR " + step.ToString("00") + " splash");
                float waited = 0f;
                while (nav.Current == ScreenId.Splash && waited < 6f) { waited += Time.unscaledDeltaTime; yield return null; }
                yield return new WaitForSecondsRealtime(0.2f);
            }
            else { Classroom.Logout(); nav.Tab(ScreenId.RoleSelect); }
            Expect(ScreenId.RoleSelect, "the splash opens the role selection");
            Check(!nav.navBar.activeSelf, "no bottom bar before login");
            yield return Shot("role_select");

            // ----- ADMIN login -----
            Click(role.adminButton);
            Expect(ScreenId.Login, "ADMIN opens the admin login");
            Click(login.loginButton);
            Check(nav.Current == ScreenId.Login && Classroom.Role == UserRole.None, "an empty login is refused");
            yield return Shot("login_admin");
            Click(login.quickButton);
            Expect(ScreenId.Dashboard, "Demo admin logs in to the dashboard");
            Check(Classroom.IsAdmin && nav.navBar.activeSelf, "admin logged in, bottom bar shown");
            string tabs = VisibleTabs();
            Check(tabs == "Dashboard Meetings Live Devices Settings ", "admin tabs: Dashboard, Meetings, Live, Devices, Settings (" + tabs.Trim() + ")");
            yield return Shot("admin_dashboard");

            // ----- Meeting Room: create a meeting -----
            Click(dash.mainCard);
            Expect(ScreenId.Meetings, "the Meeting Room card opens the meetings");
            int before = Classroom.Meetings.Count;
            Check(before == 3, "3 demo meetings");
            yield return Shot("meeting_room");
            Click(meetings.createButton);
            Expect(ScreenId.MeetingCreate, "Create Meeting opens the form");
            yield return Shot("create_meeting_empty");
            Click(create.saveButton);
            Check(nav.Current == ScreenId.MeetingCreate && Classroom.Meetings.Count == before, "an empty form is not saved");
            create.Fill("Meeting 04", "Sepsis Recognition");
            for (int i = 0; i < 4 && i < Classroom.Students.Count; i++) create.ToggleStudent(Classroom.Students[i].Id);
            foreach (var d in svc.Devices) if (!d.Online) create.ToggleQuest(d.Id);   // offline: refused
            create.ToggleQuest("q2");
            create.ToggleQuest("q6");
            Check(create.questCount.text == "2 assigned", "an offline Quest cannot be assigned (" + create.questCount.text + ")");
            ScrollTo(create, 1f); yield return Shot("create_meeting_filled");
            ScrollTo(create, 0.45f); yield return Shot("create_meeting_students");
            ScrollTo(create, 0f); yield return Shot("create_meeting_quests");
            Click(create.saveButton);
            Expect(ScreenId.Meetings, "Save returns to the meeting list");
            Check(Classroom.Meetings.Count == before + 1 && Classroom.Meetings[before].Name == "Meeting 04", "the new meeting is in the list");
            Check(meetings.viewAllButton.gameObject.activeSelf, "View All Meetings shows with more than 3 meetings");
            yield return Shot("meeting_room_saved");
            Click(meetings.viewAllButton);
            Check(meetings.ShowingAll, "View All Meetings lists all " + Classroom.Meetings.Count);
            ScrollTo(meetings, 0f); yield return Shot("meetings_view_all");
            meetings.ShowAll(false);

            // ----- Meeting details -> START SESSION -----
            Classroom.Selected = Classroom.Meetings[0];
            nav.Push(ScreenId.MeetingDetails);
            Expect(ScreenId.MeetingDetails, "a meeting opens its details");
            ScrollTo(details, 1f); yield return Shot("meeting_details");
            ScrollTo(details, 0f); yield return Shot("meeting_details_attendance");
            Click(details.actionButton);
            Expect(ScreenId.Live, "Start Session opens the live session");
            Check(Classroom.State == LiveState.Ready, "the session is ready (START not pressed yet)");
            yield return Shot("live_ready");
            Click(live.startButton);
            Check(Classroom.State == LiveState.Running, "START starts the session");
            Check(ses == null || ses.Active, "the session record starts with START");
            yield return new WaitForSecondsRealtime(Mathf.Max(settle, 2f));
            Check(Classroom.Elapsed > 0.5f, "the session clock runs");
            yield return Shot("live_metrics");
            ScrollPanel(live.metricsPanel, 0.55f); yield return Shot("live_metrics_students");
            live.muteAllSwitch.Set(true, true);
            Check(Classroom.MuteAll, "Mute all mutes every student");
            live.muteAllSwitch.Set(false, true);
            var firstId = Classroom.Live.StudentIds[0];
            var m0 = Classroom.MetricsOf(firstId);
            Classroom.ToggleMute(firstId);
            Check(m0 != null && m0.Muted, "one student can be muted");
            Classroom.ToggleMute(firstId);
            ScrollPanel(live.metricsPanel, 1f);

            // Quest tab: play content on the meeting's Quests
            live.SelectTab(1);
            Check(live.CurrentTab == 1 && live.questPanel.gameObject.activeSelf, "the Quest tab shows the Quest panel");
            yield return Shot("live_quest");
            Click(live.videoButton);
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[1]);   // CPR_Training.mp4
            yield return ConfirmIfAsked("live_replace_content");
            yield return new WaitForSecondsRealtime(DemoConfig.FakeSendSeconds + Mathf.Max(settle, 2.5f));
            Check(AnyPlaying(svc), "the video plays on the meeting's Quests");
            yield return Shot("live_quest_playing");
            Click(live.pauseButton);
            Check(!AnyPlaying(svc), "Pause pauses the Quests");
            Click(live.playButton);
            Check(AnyPlaying(svc), "Play plays again");

            // the Live Scene pauses the video; back returns to the live session, still paused until Play
            Click(live.sceneButton);
            Expect(ScreenId.Scene, "Live Scene opens the Scene");
            Check(!AnyPlaying(svc), "opening the Scene pauses the video");
            yield return new WaitForSecondsRealtime(DemoConfig.SceneLoadSeconds + settle);
            yield return Shot("live_scene");
            Click(scene.expandButton);
            Check(scene.FullView, "the expand button opens the full view");
            nav.Back();
            nav.Back();   // Android back
            Expect(ScreenId.Live, "back from the Scene returns to the live session");
            Check(!AnyPlaying(svc), "the video stays paused until Play");
            Click(live.playButton);
            Check(AnyPlaying(svc), "Play continues the video after the Scene");

            // Students tab: details + "Show Quest screen"
            live.SelectTab(2);
            yield return Shot("live_students");
            var sid = Classroom.Live.StudentIds[1];
            Classroom.Focus(Classroom.Live.StudentIds[0]);
            foreach (var r in live.GetComponentsInChildren<StudentRowView>())
                if (r.studentId == sid && r.muteButton == null) { Click(r.button); break; }
            Check(nav.popup.root.activeSelf, "tapping a student opens the details");
            yield return Shot("live_student_details");
            Click(nav.popup.okButton);
            Check(Classroom.FocusStudentId == sid && (live.CurrentTab == 1 || live.Landscape), "Show Quest screen shows that student's Quest" + (live.Landscape ? " (landscape: all columns)" : ""));
            yield return Shot("live_focus_student");

            // full screen Quest view
            Click(live.fullscreenButton);
            Check(live.Fullscreen && !nav.navBar.activeSelf, "full screen Quest view (no bottom bar)");
            yield return Shot("live_fullscreen");
            nav.Back();
            Check(!live.Fullscreen && nav.Current == ScreenId.Live, "back leaves the full screen view");
            Check(live.questPanel.gameObject.activeSelf && live.controlBar.gameObject.activeSelf && nav.navBar.activeSelf == !live.Landscape,
                "after full screen the panels, the control bar and the bottom bar are back");

            // landscape: three columns (only on a phone: the Editor cannot turn the screen; the layout audit renders it)
            if (Application.isMobilePlatform)
            {
                live.Rotate();
                float t = 0f;
                while (!live.Landscape && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
                Check(live.Landscape && live.metricsPanel.gameObject.activeSelf && live.questPanel.gameObject.activeSelf && live.studentsPanel.gameObject.activeSelf,
                    "landscape shows Metrics | Quest | Students side by side");
                Check(!nav.navBar.activeSelf, "no bottom bar in landscape");
                yield return Shot("live_landscape");
                live.Rotate();
                t = 0f;
                while (live.Landscape && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
                Check(!live.Landscape, "back to portrait");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            else Debug.Log("[MediSimVR] DEMO-TOUR landscape: phone only (the layout audit renders it in the Editor)");

            // PAUSE / START (resume) / END -> report
            Click(live.pauseSessionButton);
            Check(Classroom.State == LiveState.Paused && !AnyPlaying(svc), "PAUSE pauses the session and the Quests");
            live.SelectTab(0);
            yield return Shot("live_paused");
            Click(live.startButton);
            Check(Classroom.State == LiveState.Running, "START resumes the session");
            yield return new WaitForSecondsRealtime(1f);
            Click(live.endButton);
            yield return ConfirmIfAsked("live_end_confirm");
            Expect(ScreenId.Report, "END opens the session report");
            var rep = Classroom.LastReport;
            Check(rep != null && rep.Present + rep.Absent == Classroom.Meetings[0].StudentIds.Count && rep.Engagement > 0 && rep.Content.Count > 0 && rep.Timeline.Count > 3,
                "the report has attendance, averages, content and the timeline");
            yield return new WaitForSecondsRealtime(DemoConfig.UploadSeconds + 0.6f);
            Check(ses == null || SessionLog.UploadStatus.StartsWith("Sent to"), "the session record is sent to the PC at the end (" + SessionLog.UploadStatus + ")");
            string json = File.Exists(SessionLog.FilePath) ? File.ReadAllText(SessionLog.FilePath) : "";
            Check(json.Contains("\"events\"") && json.Contains("session started"), "the JSON record has the session events: " + SessionLog.FilePath);
            ScrollTo(report, 1f); yield return Shot("report");
            ScrollTo(report, 0.55f); yield return Shot("report_engagement");
            ScrollTo(report, 0f); yield return Shot("report_timeline");
            Click(report.shareButton);
            Click(report.dashboardButton);
            Expect(ScreenId.Dashboard, "Back to Dashboard");
            Check(Classroom.SessionsToday == 1, "sessions today = 1");
            yield return Shot("admin_dashboard_after");

            // ----- Devices, Settings, Edit App, Notifications -----
            TapTab(ScreenId.Devices);
            Expect(ScreenId.Devices, "the Devices tab");
            Check(svc.Devices.Count == 6, "6 demo Quests");
            yield return Shot("devices");
            TapTab(ScreenId.Settings);
            Expect(ScreenId.Settings, "the Settings tab");
            yield return Shot("settings");
            nav.Push(ScreenId.EditApp); yield return Shot("edit_app");
            nav.Back();
            Expect(ScreenId.Settings, "back from Edit App goes to Settings");
            TapTab(ScreenId.Dashboard);
            Click(dash.bellButton);
            Expect(ScreenId.Notifications, "the bell opens the notifications");
            yield return Shot("notifications");
            nav.Back();
            Expect(ScreenId.Dashboard, "back from the notifications");

            // ----- Logout -> role selection -----
            TapTab(ScreenId.Settings);
            Click(settings.logoutRow);
            yield return ConfirmIfAsked("logout_confirm");
            Expect(ScreenId.RoleSelect, "Log out returns to the role selection");
            Check(Classroom.Role == UserRole.None && !nav.navBar.activeSelf, "logged out, no bottom bar");

            // ----- STUDENT -----
            Click(role.studentButton);
            Expect(ScreenId.Login, "STUDENT opens the student login");
            yield return Shot("login_student");
            Click(login.quickButton);
            Expect(ScreenId.Dashboard, "Demo student logs in to the dashboard");
            tabs = VisibleTabs();
            Check(tabs == "Dashboard Meetings Settings ", "student tabs: Dashboard, Meetings, Settings (" + tabs.Trim() + ")");
            yield return Shot("student_dashboard");
            TapTab(ScreenId.Meetings);
            var mine = Classroom.Visible();
            bool onlyMine = mine.Count > 0;
            foreach (var m in mine) if (!m.StudentIds.Contains(Classroom.StudentId)) onlyMine = false;
            Check(onlyMine && mine.Count < Classroom.Meetings.Count && !meetings.createButton.gameObject.activeSelf,
                "a student sees only their meetings (" + mine.Count + "), no Create");
            yield return Shot("student_meetings");
            Classroom.Selected = mine[0];
            nav.Push(ScreenId.MeetingDetails);
            Check(details.actionLabel.text == "Join Session", "the student button is Join Session");
            yield return Shot("student_meeting_details");
            Click(details.actionButton);
            Expect(ScreenId.Live, "Join Session opens the live session (student view)");
            Check(!live.controlBar.gameObject.activeSelf && !live.tabs.gameObject.activeSelf && live.studentPanel.gameObject.activeSelf,
                "the student view has no admin controls");
            yield return new WaitForSecondsRealtime(Mathf.Max(settle, 1.5f));
            yield return Shot("student_live");
            live.SetFullscreen(true);
            Check(!live.studentPanel.gameObject.activeSelf, "student full screen hides the student panel");
            nav.Back();
            Check(!live.Fullscreen && live.studentPanel.gameObject.activeSelf, "after full screen the student panel is back");
            ScrollPanel(live.studentPanel, 0f); yield return Shot("student_live_sound");
            Click(live.leaveButton);
            yield return ConfirmIfAsked(null);
            Expect(ScreenId.Dashboard, "Leave returns to the dashboard");
            Check(!Classroom.LiveActive && (ses == null || !ses.Active), "the student left the session");

            // ----- every theme preset: dashboard + meeting details -----
            foreach (string preset in ThemeData.PresetNames)
            {
                if (preset == "Custom") continue;
                ThemeManager.Preview(ThemeData.FromPreset(preset)); // not saved
                string p = "theme_" + preset.ToLowerInvariant();
                nav.Tab(ScreenId.Dashboard); yield return Shot(p + "_dashboard");
                nav.Tab(ScreenId.Meetings);
                Classroom.Selected = mine[0];
                nav.Push(ScreenId.MeetingDetails);
                ScrollTo(details, 1f); yield return Shot(p + "_details");
            }
            ThemeManager.Load(); // the saved theme again
            Classroom.Logout();
            nav.Tab(ScreenId.RoleSelect);
            foreach (var d in svc.Devices) d.Selected = false;
            Debug.Log("[MediSimVR] DEMO-TOUR done " + step + " screens");
            Running = false;
        }
    }
}
