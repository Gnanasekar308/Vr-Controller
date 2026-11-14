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

            // ----- Meeting Room: details, delete, create (Quests + their students) -> straight into the live class -----
            Click(dash.mainCard);
            Expect(ScreenId.Meetings, "the Meeting Room card opens the meetings");
            int before = Classroom.Meetings.Count;
            Check(before == 3, "3 demo meetings");
            yield return Shot("meeting_room");
            Classroom.Selected = Classroom.Meetings[0];
            nav.Push(ScreenId.MeetingDetails);
            Expect(ScreenId.MeetingDetails, "a meeting opens its details");
            Check(details.deleteButton.gameObject.activeSelf, "the admin sees Delete");
            ScrollTo(details, 1f); yield return Shot("meeting_details");
            ScrollTo(details, 0f); yield return Shot("meeting_details_attendance");
            nav.Back();
            var m3 = Classroom.Meetings[2];
            Classroom.Selected = m3;
            nav.Push(ScreenId.MeetingDetails);
            Click(details.deleteButton);
            yield return ConfirmIfAsked("meeting_delete_confirm");
            Expect(ScreenId.Meetings, "Delete returns to the meeting list");
            Check(Classroom.Meetings.Count == before - 1 && !Classroom.Meetings.Contains(m3), "the meeting is deleted");
            Click(meetings.createButton);
            Expect(ScreenId.MeetingCreate, "Create Meeting opens the form");
            yield return Shot("create_meeting_empty");
            Click(create.saveButton);
            Check(nav.Current == ScreenId.MeetingCreate && Classroom.Meetings.Count == before - 1, "an empty form is not saved");
            create.Fill("Meeting 04", "Sepsis Recognition");
            foreach (var d in svc.Devices) if (!d.Online) create.ToggleQuest(d.Id);   // offline: refused
            create.ToggleQuest("q2");
            create.ToggleQuest("q4");
            create.ToggleQuest("q6");
            int onlineQuests = 0;
            foreach (var d in svc.Devices) if (d.Online) onlineQuests++;
            int rowsShown = 0;
            foreach (var r in create.GetComponentsInChildren<CheckRowView>()) rowsShown++;
            Check(rowsShown == onlineQuests, "only online Quests are listed (" + rowsShown + " of " + svc.Devices.Count + ")");
            Check(create.SelectedCount == 3, "3 Quests (with their students) selected");
            ScrollTo(create, 1f); yield return Shot("create_meeting_filled");
            ScrollTo(create, 0f); yield return Shot("create_meeting_quests");
            Click(create.saveButton);
            Expect(ScreenId.Live, "Start Live opens the new meeting live");
            var lm = Classroom.Live;
            Check(lm != null && lm.Name == "Meeting 04" && Classroom.State == LiveState.Running && lm.StudentIds.Count == 3 && lm.StudentIds.Contains("s2"),
                "the new meeting is live and started, with the students of its Quests");
            float t = 0f;
            while (!live.Landscape && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            Check(live.Landscape && !nav.navBar.activeSelf, "the live class opens in the landscape (tablet) layout, no bottom bar");
            Check(ses == null || ses.Active, "the session record starts with Start Live");
            yield return new WaitForSecondsRealtime(Mathf.Max(settle, 2f));
            Check(Classroom.Elapsed > 0.5f, "the session clock runs");
            yield return Shot("live_metrics");

            // admin: metric levels (sliders), settings, mute
            live.levelSliders[1].value = 40f;
            float avgA, avgE, avgM;
            Classroom.Average(out avgA, out avgE, out avgM);
            Check(Mathf.Abs(avgA - 40f) < 4f, "the admin moves the class attention level to 40% (" + avgA.ToString("0") + "%)");
            Check(Classroom.NeedAttention() > 0, "students under the alert level are counted (" + Classroom.NeedAttention() + ")");
            yield return Shot("live_metrics_level");
            live.levelSliders[1].value = 80f;
            live.MetricsOption(2);
            Check(!Classroom.ShowParticipation && !live.levelBoxes[2].gameObject.activeSelf, "Participation can be hidden");
            live.MetricsOption(2);
            live.MetricsOption(3);
            Check(Mathf.RoundToInt(Classroom.AlertLevel) == 70, "the alert level can be changed (70%)");
            live.MetricsOption(3);
            live.MetricsOption(3);
            live.muteAllSwitch.Set(true, true);
            Check(Classroom.MuteAll, "Mute all mutes every student");
            live.muteAllSwitch.Set(false, true);
            var firstId = lm.StudentIds[0];
            var m0 = Classroom.MetricsOf(firstId);
            Classroom.ToggleMute(firstId);
            Check(m0 != null && m0.Muted, "one student can be muted");
            Classroom.ToggleMute(firstId);
            ScrollPanel(live.metricsPanel, 0.4f); yield return Shot("live_questions_comments");
            ScrollPanel(live.metricsPanel, 1f);

            // Content (control bar): the video plays on the meeting's Quests
            Click(live.contentButton);
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[0]);   // Video
            if (nav.actionSheet.root.activeSelf) Click(nav.actionSheet.options[1]);   // CPR_Training.mp4
            yield return ConfirmIfAsked("live_replace_content");
            yield return new WaitForSecondsRealtime(DemoConfig.FakeSendSeconds + Mathf.Max(settle, 2.5f));
            Check(AnyPlaying(svc), "Content plays the video on the meeting's Quests");
            yield return Shot("live_quest_playing");

            // questions, comments, Record (red REC), Instruction
            Check(Classroom.Questions.Count >= 1 && Classroom.Comments.Count >= 1,
                "questions (" + Classroom.Questions.Count + ") and live comments (" + Classroom.Comments.Count + ") are shown");
            int qn = Classroom.Questions.Count;
            Classroom.Answer(Classroom.Questions[qn - 1]);
            Check(Classroom.Questions[qn - 1].Answered, "a question can be marked answered");
            Click(live.recordButton);
            yield return new WaitForSecondsRealtime(0.4f);
            Check(Classroom.Recording && live.recChip.activeSelf, "Record shows the red REC chip");
            yield return Shot("live_recording");
            int insBefore = SessionLog.InstructionsSent.Count;
            Click(live.instructionButton);
            Check(nav.actionSheet.root.activeSelf, "Instruction opens the instruction list");
            yield return Shot("live_instruction");
            Click(nav.actionSheet.options[0]);
            Check(SessionLog.InstructionsSent.Count == insBefore + 1, "an instruction is sent to the Quests");
            Click(live.recordButton);
            yield return new WaitForSecondsRealtime(0.4f);
            Check(!Classroom.Recording && !live.recChip.activeSelf && Classroom.RecordSeconds > 0.5f, "Record stops, REC chip hidden (" + Classroom.RecordSeconds.ToString("0.0") + " s)");

            // Students: details + "Show Quest screen"
            var sid = lm.StudentIds[1];
            Classroom.Focus(lm.StudentIds[0]);
            foreach (var r in live.studentsPanel.GetComponentsInChildren<StudentRowView>())
                if (r.studentId == sid) { Click(r.button); break; }
            Check(nav.popup.root.activeSelf, "tapping a student opens the details");
            yield return Shot("live_student_details");
            Click(nav.popup.okButton);
            Check(Classroom.FocusStudentId == sid, "Show Quest screen shows that student's Quest");

            // full screen Quest view
            Click(live.fullscreenButton);
            Check(live.Fullscreen && !live.header.gameObject.activeSelf && !live.controlBar.gameObject.activeSelf, "full screen Quest view");
            yield return Shot("live_fullscreen");
            nav.Back();
            Check(!live.Fullscreen && nav.Current == ScreenId.Live && live.controlBar.gameObject.activeSelf && live.metricsPanel.gameObject.activeSelf,
                "back leaves the full screen view; panels and control bar are back");

            // Pause / Start (resume) / End Class -> report
            Click(live.pauseSessionButton);
            Check(Classroom.State == LiveState.Paused && !AnyPlaying(svc), "Pause pauses the session and the Quests");
            yield return Shot("live_paused");
            Click(live.startButton);
            Check(Classroom.State == LiveState.Running, "Start resumes the session");
            yield return new WaitForSecondsRealtime(1f);
            Click(live.endButton);
            yield return ConfirmIfAsked("live_end_confirm");
            Expect(ScreenId.Report, "End Class opens the session report");
            var rep = Classroom.LastReport;
            Check(rep != null && rep.Present + rep.Absent == lm.StudentIds.Count && rep.Engagement > 0 && rep.Content.Count > 0 && rep.Timeline.Count > 3,
                "the report has attendance, averages, content and the timeline");
            yield return new WaitForSecondsRealtime(DemoConfig.UploadSeconds + 0.6f);
            Check(ses == null || SessionLog.UploadStatus.StartsWith("Sent to"), "the session record is sent to the PC at the end (" + SessionLog.UploadStatus + ")");
            string json = File.Exists(SessionLog.FilePath) ? File.ReadAllText(SessionLog.FilePath) : "";
            Check(json.Contains("\"events\"") && json.Contains("session started"), "the JSON record has the session events: " + SessionLog.FilePath);
            ScrollTo(report, 1f); yield return Shot("report");
            ScrollTo(report, 0f); yield return Shot("report_timeline");
            Click(report.shareButton);
            Click(report.dashboardButton);
            Expect(ScreenId.Dashboard, "Back to Dashboard");
            Check(Classroom.SessionsToday == 1, "sessions today = 1");

            yield return Shot("admin_dashboard_after");

            // ----- Devices, Settings, Edit App, Notifications -----
            TapTab(ScreenId.Devices);
            Expect(ScreenId.Devices, "the Devices tab");
            Check(svc.Devices.Count == 8, "8 demo Quests (one per student)");
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
            float wait = 0f;
            while (!live.Landscape && wait < 5f) { wait += Time.unscaledDeltaTime; yield return null; }
            Check(!live.controlBar.gameObject.activeSelf && !live.metricsPanel.gameObject.activeSelf && live.studentPanel.gameObject.activeSelf,
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
