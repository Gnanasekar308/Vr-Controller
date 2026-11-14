using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Live classroom: always the landscape (tablet) layout, on phones too; no bottom navigation (back = header arrow /
    // Android back). Every other screen is portrait.
    //   Left  : Live metrics (engagement / attention / participation as sliders the admin drags up / down; settings: which
    //           metrics, alert level; students
    //           under the alert level), students' questions (New, tap = answered), live comments, sound (mute all),
    //           per student meters + mute (tap = details, Show Quest screen)
    //   Centre: the Quest screen (full column) + control bar: volume, Play (starts the session and the content chosen at
    //           Create Meeting; nothing plays before), Pause, End Class, Record (red REC chip in
    //           the header while recording), Instruction (read aloud on the Quests), Content (the Send picker)
    //   Right : class overview (subject, topic, attendance, Quests) + students (avatar, online dot, mute, battery)
    // Student: the same Quest screen (own Quest) + own status, focus, sound, Leave; no admin controls.
    public class LiveSessionScreen : AppScreen
    {
        // header
        public RectTransform header;
        public TMP_Text titleText, subtitleText, liveChipText, recText;
        public Image liveChip, liveDot, recDot;
        public GameObject recChip;
        public Button backButton, leaveButton;
        // Quest screen
        public RectTransform questCard;
        public UIGradient questGradient;
        public Image questIcon, progressFill;
        public TMP_Text questName, questContent, questState, questTime;
        public Button fullscreenButton;
        public Image fullscreenIcon;
        public Sprite videoSprite, audioSprite, imageSprite, textSprite, idleSprite, expandSprite, collapseSprite;
        // panels
        public RectTransform metricsPanel, studentsPanel, studentPanel, controlBar, emptyState, volumeBox;
        public Button emptyButton;
        // metrics
        // class levels: sliders the admin drags up / down (they follow the live average while not dragged)
        public Slider[] levelSliders = new Slider[3];      // 0 engagement, 1 attention, 2 participation
        public TMP_Text[] levelValues = new TMP_Text[3];
        public RectTransform[] levelBoxes = new RectTransform[3];
        public TMP_Text moodText, alertText, volumeLabel, questionsTitle, commentsTitle;
        public GameObject questionsNew;
        public Button metricsSettingsButton;
        public Slider barVolume;
        public ToggleSwitch muteAllSwitch;
        public StudentRowView metricRowTemplate;
        public LiveLineView questionTemplate, commentTemplate;
        public TMP_Text noQuestions;
        // students
        public TMP_Text overviewText;
        public StudentRowView studentRowTemplate;
        // student view
        public TMP_Text myQuestText, myStatusText;
        public MeterView myAttention, myEngagement;
        public Slider myVolume;
        public TMP_Text myVolumeLabel;
        public ToggleSwitch myMuteSwitch;
        // control bar
        public Button startButton, pauseSessionButton, endButton, recordButton, instructionButton, contentButton;
        public TMP_Text startLabel, recordLabel;
        public Image recordCard;

        private readonly List<StudentRowView> metricRows = new List<StudentRowView>();
        private readonly List<StudentRowView> studentRows = new List<StudentRowView>();
        private readonly List<LiveLineView> questionRows = new List<LiveLineView>();
        private readonly List<LiveLineView> commentRows = new List<LiveLineView>();
        private ContentChooser chooser;
        private bool fullscreen, laidOut;
        private Vector2 laidSize;
        private float textTimer;
        private Slider dragging;   // the level slider under the admin's finger (it does not follow the average then)

        public void SetDragging(Slider s) { dragging = s; }

        public const float BarH = 168f, HeadH = 190f, Gap = 20f, Side = 40f, LeftW = 0.27f, RightW = 0.73f;

        public bool Landscape { get { var r = ((RectTransform)transform).rect; return r.width > r.height; } }
        public bool Fullscreen { get { return fullscreen; } }
        public override bool ShowsNav { get { return false; } }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 3600).ToString("00") + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        public static string StateText()
        {
            switch (Classroom.State)
            {
                case LiveState.Ready: return "Ready";
                case LiveState.Running: return "LIVE";
                case LiveState.Paused: return "Paused";
                default: return "Ended";
            }
        }

        private ISessionService Ses { get { return AppRoot.Session; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            chooser = n.GetComponent<ContentChooser>();
            backButton.onClick.AddListener(() => nav.Back());
            leaveButton.onClick.AddListener(Leave);
            fullscreenButton.onClick.AddListener(() => SetFullscreen(!fullscreen));
            emptyButton.onClick.AddListener(() => nav.Tab(ScreenId.Meetings));
            metricsSettingsButton.onClick.AddListener(MetricsSettings);

            barVolume.onValueChanged.AddListener(v => SetVolume((int)v));
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                levelSliders[i].onValueChanged.AddListener(v => { Classroom.SetLevel(k, v); levelValues[k].text = Mathf.RoundToInt(v) + "%"; });
                var trig = levelSliders[i].gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                up.callback.AddListener(_ => { dragging = null; Classroom.LogLevel(k, levelSliders[k].value); });
                var down = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
                down.callback.AddListener(_ => dragging = levelSliders[k]);
                trig.triggers.Add(up);
                trig.triggers.Add(down);
            }
            muteAllSwitch.Changed += on => { Classroom.SetMuteAll(on); SelectMeetingQuests(); Svc.SetMute(on); nav.Say(on ? "All students muted" : "All students unmuted"); };
            myVolume.onValueChanged.AddListener(v => SetVolume((int)v));
            myMuteSwitch.Changed += on => { var q = MyQuest(); if (q != null) { Only(q); Svc.SetMute(on); } nav.Say(on ? "Your Quest is muted" : "Your Quest sound is on"); };

            startButton.onClick.AddListener(StartSession);
            pauseSessionButton.onClick.AddListener(PauseSession);
            endButton.onClick.AddListener(() => nav.popup.Show("End class?",
                "The session ends for all students. The report opens.", "End class", EndSession, "Cancel", destructive: true));
            recordButton.onClick.AddListener(ToggleRecord);
            instructionButton.onClick.AddListener(Instruction);
            contentButton.onClick.AddListener(PickContent);

            metricRowTemplate.gameObject.SetActive(false);
            studentRowTemplate.gameObject.SetActive(false);
            questionTemplate.gameObject.SetActive(false);
            commentTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnable() { base.OnEnable(); Classroom.Changed += OnClass; }

        protected override void OnDisable()
        {
            base.OnDisable();
            Classroom.Changed -= OnClass;
            fullscreen = false;
            // only the Live class is landscape: every other screen is portrait
            if (Screen.orientation != ScreenOrientation.Portrait) Screen.orientation = ScreenOrientation.Portrait;
        }

        void OnClass() { if (isActiveAndEnabled) Refresh(); }

        public override void OnShow()
        {
            if (Classroom.LiveActive && Classroom.IsAdmin) SelectMeetingQuests();
            // the tablet layout on every device: landscape (fixed, so the phone's rotation lock cannot keep it portrait)
            if (Screen.orientation != ScreenOrientation.LandscapeLeft) Screen.orientation = ScreenOrientation.LandscapeLeft;
            laidOut = false;
            base.OnShow();
        }

        public override bool HandleBack()
        {
            if (fullscreen) { SetFullscreen(false); return true; }
            return false;
        }

        // ---------- actions ----------
        void SetVolume(int v)
        {
            Classroom.SetVolume(v);
            volumeLabel.text = v + "%";
            myVolumeLabel.text = v + "%";
            barVolume.SetValueWithoutNotify(v);
            myVolume.SetValueWithoutNotify(v);
        }

        void SelectMeetingQuests()
        {
            var m = Classroom.Live;
            if (m == null) return;
            foreach (var d in Svc.Devices) d.Selected = d.Online && m.QuestIds.Contains(d.Id);
        }

        void Only(QuestDevice q) { foreach (var d in Svc.Devices) d.Selected = d == q; }

        QuestDevice FocusQuest()
        {
            return Classroom.Live == null ? null : Classroom.Device(Svc, Classroom.QuestIdFor(Classroom.Live, Classroom.FocusStudentId));
        }

        QuestDevice MyQuest()
        {
            return Classroom.Live == null ? null : Classroom.Device(Svc, Classroom.QuestIdFor(Classroom.Live, Classroom.StudentId));
        }

        QuestDevice ShownQuest() { return Classroom.IsAdmin ? FocusQuest() : MyQuest(); }

        MediaFile FileOf(QuestDevice q)
        {
            if (q == null) return null;
            foreach (var f in Svc.Files) if (f.Id == q.FileId) return f;
            return null;
        }

        public void SetFullscreen(bool on)
        {
            fullscreen = on;
            ApplyLayout();
        }

        // Content: the Send picker (type, then file), sent to the meeting's online Quests
        void PickContent()
        {
            if (Classroom.State == LiveState.Ready) { nav.Say("Tap Play to begin the session"); return; }
            chooser.Choose(() =>
            {
                var f = Svc.ControlFile;
                SelectMeetingQuests();
                var targets = Svc.SelectedOnline();
                if (f == null || targets.Count == 0) { nav.Say("No online Quest in this meeting"); return; }
                chooser.Send(f, targets, () =>
                {
                    Classroom.Played(f.Name);
                    SessionLog.Add(SessionEventKind.Content, "Host", "sent " + f.Name + " to " + targets.Count + " Quest(s)");
                });
            });
        }

        public void StartSession()
        {
            if (Classroom.Live == null) return;
            if (Classroom.State == LiveState.Running) { nav.Say("The session is already running"); return; }
            bool first = Classroom.State == LiveState.Ready;
            if (first && Ses != null && !Ses.Active) Ses.StartHost(true);   // session record (JSON) starts with the session
            Classroom.Start();
            SelectMeetingQuests();
            if (!first && Svc.SelectedOnline().Count > 0)
                foreach (var d in Svc.SelectedOnline()) if (d.State == DeviceState.Paused) { Svc.Resume(); break; }
            SessionLog.Add(SessionEventKind.Info, "Host", first ? "session started: " + Classroom.Live.Name : "session resumed");
            // the content chosen at Create Meeting plays now (not when the live page opened)
            MediaFile pending = null;
            foreach (var f in Svc.Files) if (f.Id == Classroom.PendingFileId) pending = f;
            Classroom.PendingFileId = "";
            var targets = Svc.SelectedOnline();
            if (pending != null && targets.Count > 0)
            {
                chooser.Send(pending, targets, () =>
                {
                    Classroom.Played(pending.Name);
                    SessionLog.Add(SessionEventKind.Content, "Host", "sent " + pending.Name + " to " + targets.Count + " Quest(s)");
                });
                return;
            }
            nav.Say(first ? "Session started" : "Session resumed");
        }

        void PauseSession()
        {
            if (Classroom.State != LiveState.Running) { nav.Say("The session is not running"); return; }
            Classroom.Pause();
            SelectMeetingQuests();
            bool playing = false;
            foreach (var d in Svc.SelectedOnline()) if (d.State == DeviceState.Playing) playing = true;
            if (playing) Svc.Pause();
            SessionLog.Add(SessionEventKind.Pause, "Host", "session paused");
            nav.Say("Session paused");
        }

        void EndSession()
        {
            if (Classroom.Live == null) return;
            // the JSON record exists once the session was started (Start); ended while Ready = nothing recorded
            bool recorded = Ses != null ? Ses.Active : SessionLog.Recording;
            if (Classroom.Recording) Classroom.SetRecording(false);
            if (recorded) SessionLog.Add(SessionEventKind.Stop, "Host", "session ended");
            if (Ses != null && Ses.Active) Ses.End();
            Classroom.End(recorded ? SessionLog.FilePath : "", recorded ? SessionLog.UploadStatus : "Not started: nothing recorded");
            nav.Tab(ScreenId.Report);
        }

        public void ToggleRecord()
        {
            if (Classroom.State == LiveState.Ready) { nav.Say("Tap Play to begin the session"); return; }
            bool on = !Classroom.Recording;
            Classroom.SetRecording(on);
            SessionLog.Add(SessionEventKind.Info, "Host", on ? "class recording started" : "class recording stopped");
            nav.Say(on ? "Recording the class" : "Recording stopped");
            UpdateTexts();
        }

        // important instruction: the meeting's Quests read it aloud (demo: the phone speaks it)
        void Instruction()
        {
            if (Classroom.State == LiveState.Ready) { nav.Say("Tap Play to begin the session"); return; }
            var list = Ses != null ? Ses.Instructions : (IReadOnlyList<string>)DemoConfig.Instructions;
            int count = Mathf.Min(list.Count, nav.actionSheet.options.Count);
            var names = new string[count];
            var icons = new string[count];
            for (int i = 0; i < count; i++) { names[i] = list[i]; icons[i] = "warning"; }
            nav.actionSheet.Show("Send an instruction", names, icons, k => SendInstruction(names[k]));
        }

        public void SendInstruction(string text)
        {
            SelectMeetingQuests();
            var targets = Svc.SelectedOnline();
            if (targets.Count == 0) { nav.Say("No online Quest in this meeting"); return; }
            SessionLog.Speak(SessionEventKind.Instruction, text, targets);
            Classroom.Log(SessionEventKind.Instruction, "Instruction: " + text);
            nav.Say("Instruction sent: " + text);
        }

        // admin settings of the Live metrics: which metrics show, and the attention alert level
        void MetricsSettings()
        {
            string[] names =
            {
                "Engagement: " + (Classroom.ShowEngagement ? "shown" : "hidden"),
                "Attention: " + (Classroom.ShowAttention ? "shown" : "hidden"),
                "Participation: " + (Classroom.ShowParticipation ? "shown" : "hidden"),
                "Attention alert below " + Mathf.RoundToInt(Classroom.AlertLevel) + "%",
            };
            string[] icons = { "visibility", "visibility", "visibility", "warning" };
            for (int i = 0; i < 3; i++) if (!(i == 0 ? Classroom.ShowEngagement : i == 1 ? Classroom.ShowAttention : Classroom.ShowParticipation)) icons[i] = "visibility_off";
            nav.actionSheet.Show("Live metrics", names, icons, MetricsOption);
        }

        public void MetricsOption(int k)
        {
            if (k == 0) Classroom.ShowEngagement = !Classroom.ShowEngagement;
            else if (k == 1) Classroom.ShowAttention = !Classroom.ShowAttention;
            else if (k == 2) Classroom.ShowParticipation = !Classroom.ShowParticipation;
            else
            {
                var levels = DemoConfig.AlertLevels;
                int i = System.Array.IndexOf(levels, Mathf.RoundToInt(Classroom.AlertLevel));
                Classroom.AlertLevel = levels[(i + 1) % levels.Length];
                nav.Say("Attention alert below " + Mathf.RoundToInt(Classroom.AlertLevel) + "%");
            }
            Classroom.Log(SessionEventKind.Info, "Metrics settings changed");
            Classroom.MetricsChanged();
        }

        void Leave()
        {
            nav.popup.Show("Leave session?", "You leave " + (Classroom.Live != null ? Classroom.Live.Name : "the session") + ".", "Leave", () =>
            {
                bool recorded = Ses != null ? Ses.Active : SessionLog.Recording;
                if (Ses != null && Ses.Active) Ses.End();
                string name = Classroom.Live != null ? Classroom.Live.Name : "";
                Classroom.Log(SessionEventKind.Leave, Classroom.UserName + " left");
                Classroom.End(recorded ? SessionLog.FilePath : "", recorded ? SessionLog.UploadStatus : "");
                nav.Tab(ScreenId.Dashboard);
                nav.Say("You left " + name);
            }, "Cancel", destructive: true);
        }

        void ShowStudent(string id)
        {
            var m = Classroom.Live;
            var s = Classroom.StudentById(id);
            if (m == null || s == null) return;
            bool present = !m.Absent.Contains(id);
            var x = Classroom.MetricsOf(id);
            var q = Classroom.Device(Svc, Classroom.QuestIdFor(m, id));
            string body = "ID: " + s.Code + "\nQuest: " + (q != null ? q.Name + (q.Online ? "  (" + DeviceStatus.Label(DeviceStatus.Of(q)) + ", " + DeviceStatus.Battery(q) + ")" : "  (Offline)") : "none") +
                "\nStatus: " + (present ? "Present" + (x != null && x.Muted ? ", muted" : "") : "Absent");
            if (present && x != null)
                body += "\nAttention " + Mathf.RoundToInt(x.Attention) + "%   Engagement " + Mathf.RoundToInt(x.Engagement) + "%" +
                        "\nParticipation " + Mathf.RoundToInt(x.Participation) + "%   Mood " + Classroom.Mood(x.Emotion);
            nav.popup.Show(s.Name, body, "Show Quest screen", () =>
            {
                Classroom.Focus(id);
                nav.Say("Showing " + (q != null ? q.Name : "the Quest") + " of " + s.Name.Split(' ')[0]);
            }, "Close");
        }

        void ShowQuestion(LiveLine q)
        {
            var s = Classroom.StudentById(q.StudentId);
            nav.popup.Show("Question from " + (s != null ? s.Name : "a student"), q.Text,
                q.Answered ? "OK" : "Mark answered", () => Classroom.Answer(q), q.Answered ? null : "Close");
        }

        void ToggleMute(string id) { Classroom.ToggleMute(id); var x = Classroom.MetricsOf(id); nav.Say((x != null && x.Muted ? "Muted " : "Unmuted ") + Classroom.FirstName(id)); }

        // ---------- layout ----------
        private void Update()
        {
            var rt = (RectTransform)transform;
            // the canvas rescales a frame after the phone turns: lay out again whenever the size changes
            if (!laidOut || rt.rect.size != laidSize)
            {
                laidOut = true;
                laidSize = rt.rect.size;
                ApplyLayout();
            }
            if (Classroom.Live == null) return;
            UpdateMeters();
            if (Classroom.Recording && recDot != null)
                recDot.color = new Color(recDot.color.r, recDot.color.g, recDot.color.b, Mathf.PingPong(Time.unscaledTime * 2f, 1f) > 0.5f ? 1f : 0.25f);
            if ((textTimer += Time.unscaledDeltaTime) < 0.25f) return;
            textTimer = 0f;
            UpdateTexts();
        }

        // a column from x0 to x1 (fractions), from 'top' (from the top) to 'bottom' (from the bottom), insets l / r
        static void Box(RectTransform r, float x0, float x1, float top, float bottom, float l, float right)
        {
            r.anchorMin = new Vector2(x0, 0f);
            r.anchorMax = new Vector2(x1, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(l, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        static void Show(Component c, bool on) { if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }

        void ApplyLayout()
        {
            bool live = Classroom.Live != null;
            bool admin = Classroom.IsAdmin;
            Show(header, !fullscreen);
            Show(emptyState, !live);
            Show(questCard, live);
            Show(leaveButton, live && !admin);
            Show(controlBar, live && admin && !fullscreen);
            Show(studentPanel, live && !admin && !fullscreen);
            Show(metricsPanel, live && admin && !fullscreen);
            Show(studentsPanel, live && admin && !fullscreen);
            if (fullscreenIcon != null) fullscreenIcon.sprite = fullscreen ? collapseSprite : expandSprite;
            if (!live) return;
            if (fullscreen) { Box(questCard, 0f, 1f, 0f, 0f, 0f, 0f); return; }
            if (!admin)
            {
                // own Quest screen | own status
                Box(questCard, 0f, 0.62f, HeadH, Gap, Side, Gap * 0.5f);
                Box(studentPanel, 0.62f, 1f, HeadH, 0f, Gap * 0.5f - Side, 0f);
                return;
            }
            // Metrics | Quest screen (full centre) | Students; the control bar under the centre and right
            Box(metricsPanel, 0f, LeftW, HeadH, 0f, 0f, Gap * 0.5f - Side);
            Box(questCard, LeftW, RightW, HeadH, BarH, Gap * 0.5f, Gap * 0.5f);
            Box(studentsPanel, RightW, 1f, HeadH, BarH, Gap * 0.5f - Side, 0f);
            controlBar.anchorMin = new Vector2(LeftW, 0f);
            controlBar.anchorMax = new Vector2(1f, 0f);
            controlBar.offsetMin = new Vector2(Gap * 0.5f, 24f);
            controlBar.offsetMax = new Vector2(-Side, 24f + 120f);
        }

        // ---------- data ----------
        void UpdateMeters()
        {
            float a, e, m;
            Classroom.Average(out a, out e, out m);
            float[] now = { e, a, Classroom.AverageParticipation() };
            for (int i = 0; i < 3; i++)
            {
                if (levelSliders[i] == null || dragging == levelSliders[i]) continue;
                levelSliders[i].SetValueWithoutNotify(now[i]);
                levelValues[i].text = Mathf.RoundToInt(now[i]) + "%";
            }
            foreach (var r in metricRows) if (r.gameObject.activeSelf) r.SetValues(Classroom.MetricsOf(r.studentId));
            var mine = Classroom.MetricsOf(Classroom.StudentId);
            if (mine != null) { myAttention.Set(mine.ShownAttention); myEngagement.Set(mine.ShownEngagement); }
        }

        void UpdateTexts()
        {
            var m = Classroom.Live;
            if (m == null) return;
            bool running = Classroom.State == LiveState.Running;
            liveChipText.text = StateText() + "  " + Clock(Classroom.Elapsed);
            Color c = ThemeManager.Get(running ? DemoConfig.LiveRole : Classroom.State == LiveState.Paused ? ColorRole.Warning : ColorRole.Primary);
            liveChipText.color = c;
            liveDot.color = c;
            liveChip.color = new Color(c.r, c.g, c.b, 0.14f);
            // red REC chip in the header + red Record button while recording
            recChip.SetActive(Classroom.Recording);
            recText.text = "REC  " + Clock(Classroom.RecordSeconds).Substring(3);
            recordLabel.text = Classroom.Recording ? "Stop REC" : "Record";
            Color red = ThemeManager.Get(ColorRole.Danger);
            recordCard.color = Classroom.Recording ? new Color(red.r, red.g, red.b, 0.16f) : ThemeManager.Get(ColorRole.Card);

            float a, e, mo;
            Classroom.Average(out a, out e, out mo);
            moodText.text = "Class mood: " + Classroom.Mood(mo);
            int low = Classroom.NeedAttention();
            alertText.text = low == 0 ? "Everyone above " + Mathf.RoundToInt(Classroom.AlertLevel) + "% attention"
                : low + (low == 1 ? " student needs" : " students need") + " attention (< " + Mathf.RoundToInt(Classroom.AlertLevel) + "%)";
            alertText.color = ThemeManager.Get(low == 0 ? ColorRole.SubText : ColorRole.Warning);
            subtitleText.text = m.Topic + "  •  " + m.PresentCount + "/" + m.StudentIds.Count + " present";

            var q = ShownQuest();
            var f = FileOf(q);
            string who = Classroom.IsAdmin ? Classroom.FirstName(Classroom.FocusStudentId) : "You";
            questName.text = (q != null ? q.Name : "No Quest") + "  •  " + who;
            MediaFile ready = null;
            foreach (var x in Svc.Files) if (x.Id == Classroom.PendingFileId) ready = x;
            questContent.text = q == null ? "No Quest assigned" : !q.Online ? "Offline"
                : ready != null ? "Ready: " + ready.Name + "  •  tap Play" : DeviceStatus.Content(q, Svc);
            questState.text = q == null || !q.Online ? "NO SIGNAL" : Classroom.State == LiveState.Paused ? "SESSION PAUSED"
                : q.State == DeviceState.Playing ? "LIVE VIEW" : q.State == DeviceState.Paused ? "PAUSED" : "CONNECTED";
            bool timed = f != null && f.DurationSec > 0 && q != null && (q.State == DeviceState.Playing || q.State == DeviceState.Paused);
            questTime.text = timed ? Mmss(q.PositionSec) + " / " + Mmss(f.DurationSec) : "";
            progressFill.rectTransform.anchorMax = new Vector2(timed ? Mathf.Clamp01(q.PositionSec / f.DurationSec) : 0f, 1f);
            if (f != null && q != null && q.Online)
            {
                questGradient.SetColors(f.ThumbTop, f.ThumbBottom);
                questIcon.sprite = f.Kind == MediaKind.Video ? videoSprite : f.Kind == MediaKind.Audio ? audioSprite : f.Kind == MediaKind.Image ? imageSprite : textSprite;
            }
            else
            {
                questGradient.SetColors(new Color(0.18f, 0.24f, 0.33f), new Color(0.06f, 0.09f, 0.14f));
                questIcon.sprite = idleSprite;
            }

            if (!Classroom.IsAdmin)
            {
                myQuestText.text = q != null ? q.Name + "  •  " + (q.Online ? DeviceStatus.Battery(q) : "Offline") : "No Quest assigned";
                myStatusText.text = (running ? "In class" : Classroom.State == LiveState.Paused ? "Class paused" : "Waiting") + (q != null ? "  •  " + DeviceStatus.Content(q, Svc) : "");
            }

            startLabel.text = Classroom.State == LiveState.Paused ? "Resume" : "Play";
            startButton.interactable = Classroom.State != LiveState.Running;
            pauseSessionButton.interactable = running;

            questionsTitle.text = "Questions (" + Classroom.Questions.Count + ")";
            questionsNew.SetActive(Classroom.NewQuestions > 0);
            var ql = Classroom.Questions;
            for (int i = 0; i < questionRows.Count; i++)
            {
                int idx = ql.Count - 1 - i;
                if (idx < 0) break;
                var l = ql[idx];
                questionRows[i].newChip.SetActive(l.Answered || Classroom.Elapsed - l.At < DemoConfig.NewSeconds);
            }
        }

        static string Mmss(float s) { int t = Mathf.Max(0, (int)s); return (t / 60).ToString("00") + ":" + (t % 60).ToString("00"); }

        public override void Refresh()
        {
            var m = Classroom.Live;
            titleText.text = m != null ? m.Name : "Live Session";
            if (m == null)
            {
                subtitleText.text = "No live session";
                laidOut = false;
                return;
            }
            bool admin = Classroom.IsAdmin;
            var ids = new List<string>();
            foreach (var id in m.StudentIds) if (!m.Absent.Contains(id)) ids.Add(id);
            foreach (var id in m.StudentIds) if (m.Absent.Contains(id)) ids.Add(id);
            if (admin)
            {
                Show(levelBoxes[0], Classroom.ShowEngagement);
                Show(levelBoxes[1], Classroom.ShowAttention);
                Show(levelBoxes[2], Classroom.ShowParticipation);
                Keep(metricRowTemplate, metricRows, ids.Count);
                Keep(studentRowTemplate, studentRows, ids.Count);
                int online = 0;
                foreach (var qid in m.QuestIds) { var d = Classroom.Device(Svc, qid); if (d != null && d.Online) online++; }
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    var s = Classroom.StudentById(id);
                    bool present = !m.Absent.Contains(id);
                    var x = Classroom.MetricsOf(id);
                    bool muted = Classroom.MuteAll || (x != null && x.Muted);
                    var q = Classroom.Device(Svc, Classroom.QuestIdFor(m, id));
                    string qn = q != null ? q.Name : "No Quest";
                    metricRows[i].Bind(id, s.Name, present ? qn + (muted ? "  •  muted" : "") : "Absent", present, muted, id == Classroom.FocusStudentId, ShowStudent, ToggleMute);
                    if (present)
                    {
                        Show(metricRows[i].engagement, Classroom.ShowEngagement);
                        Show(metricRows[i].attention, Classroom.ShowAttention);
                        Show(metricRows[i].participation, Classroom.ShowParticipation);
                    }
                    string status = !present ? "Absent" : q == null ? "No Quest" : q.Online ? "Online" : "Offline";
                    studentRows[i].Bind(id, s.Name, present && q != null ? qn : status, true, muted, id == Classroom.FocusStudentId, ShowStudent, present ? ToggleMute : null);
                    ColorRole role = !present ? ColorRole.Danger : q != null && q.Online ? ColorRole.Success : ColorRole.SubText;
                    studentRows[i].SetDevice(s.Initials, status, role, present && q != null && q.Online && q.Battery >= 0 ? q.Battery + "%" : "");
                }
                overviewText.text = "<b>Subject:</b>  " + (m.Subject.Length > 0 ? m.Subject : "-") + "\n<b>Topic:</b>  " + m.Topic +
                                    "\n<b>Attendance:</b>  " + m.PresentCount + " / " + m.StudentIds.Count + " present\n<b>Quests:</b>  " + online + " / " + m.QuestIds.Count + " online";

                var ql = Classroom.Questions;
                int nq = Mathf.Min(ql.Count, DemoConfig.ShowQuestions);
                Keep(questionTemplate, questionRows, nq);
                for (int i = 0; i < nq; i++) { var l = ql[ql.Count - 1 - i]; questionRows[i].Bind(l, l.Answered || Classroom.Elapsed - l.At < DemoConfig.NewSeconds, ShowQuestion); }
                noQuestions.gameObject.SetActive(nq == 0);
                var cl = Classroom.Comments;
                int nc = Mathf.Min(cl.Count, DemoConfig.ShowComments);
                Keep(commentTemplate, commentRows, nc);
                for (int i = 0; i < nc; i++) commentRows[i].Bind(cl[cl.Count - 1 - i], false, null);
                commentsTitle.text = "Live comments (" + cl.Count + ")";

                muteAllSwitch.Set(Classroom.MuteAll, false);
            }
            else
            {
                var q = MyQuest();
                myMuteSwitch.Set(q != null && q.Muted, false);
            }
            SetVolume(Classroom.Volume);
            ApplyLayout();
            UpdateTexts();
        }
    }
}
