using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Live session dashboard.
    //   Admin, portrait: Quest screen on top (always visible), tabs Metrics | Quest | Students below, control bar
    //   START / PAUSE / END. Landscape (Rotate button, or the phone turned while rotation is allowed): three columns
    //   Metrics | Quest | Students side by side, no bottom bar.
    //   Metrics: class averages (emotion, engagement, attention), sound (master volume, mute all), per student meters +
    //   mute. Quest: device status, Play / Pause, pick Video / Audio / Image / Text (the Send picker), all controls,
    //   Live Scene. Students: count, attendance, devices, list; tap = details + "Show this Quest screen".
    //   Student: the same Quest screen (own Quest) + own status and sound, no admin controls.
    //   END -> confirm -> Session report.
    public class LiveSessionScreen : AppScreen
    {
        // header
        public RectTransform header;
        public TMP_Text titleText, subtitleText, liveChipText;
        public Image liveChip, liveDot;
        public Button rotateButton, leaveButton;
        public TMP_Text rotateLabel;
        // Quest screen
        public RectTransform questCard;
        public UIGradient questGradient;
        public Image questIcon, progressFill;
        public TMP_Text questName, questContent, questState, questTime;
        public Button fullscreenButton;
        public Image fullscreenIcon;
        public Sprite videoSprite, audioSprite, imageSprite, textSprite, idleSprite, expandSprite, collapseSprite;
        // tabs + panels
        public Segmented tabs;
        public RectTransform metricsPanel, questPanel, studentsPanel, studentPanel, controlBar, emptyState;
        public Button emptyButton;
        // metrics
        public MeterView classEmotion, classEngagement, classAttention;
        public TMP_Text moodText, volumeLabel;
        public Slider volumeSlider;
        public ToggleSwitch muteAllSwitch;
        public StudentRowView metricRowTemplate;
        // Quest
        public TMP_Text deviceName, deviceStatus, devicePlaying;
        public Button playButton, pauseButton, videoButton, audioButton, imageButton, textButton, controlsButton, sceneButton;
        // students
        public TMP_Text studentCount, attendanceText, devicesText;
        public StudentRowView studentRowTemplate;
        // student view
        public TMP_Text myQuestText, myStatusText;
        public MeterView myAttention, myEngagement;
        public Slider myVolume;
        public TMP_Text myVolumeLabel;
        public ToggleSwitch myMuteSwitch;
        // control bar
        public Button startButton, pauseSessionButton, endButton;
        public TMP_Text startLabel;

        private readonly List<StudentRowView> metricRows = new List<StudentRowView>();
        private readonly List<StudentRowView> studentRows = new List<StudentRowView>();
        private ContentChooser chooser;
        private bool landscape, fullscreen, laidOut;
        private Vector2 laidSize;
        private float textTimer;
        private int tab;

        public const float QuestH = 500f, TabsH = 96f, BarH = 168f, HeadH = 250f, LandHead = 190f, Gap = 20f, Side = 40f;

        public bool Landscape { get { return landscape; } }
        public bool Fullscreen { get { return fullscreen; } }
        public int CurrentTab { get { return tab; } }
        public override bool ShowsNav { get { return !landscape && !fullscreen; } }

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
            tabs.Changed += i => { tab = i; ApplyLayout(); };
            rotateButton.onClick.AddListener(Rotate);
            leaveButton.onClick.AddListener(Leave);
            fullscreenButton.onClick.AddListener(() => SetFullscreen(!fullscreen));
            emptyButton.onClick.AddListener(() => nav.Tab(ScreenId.Meetings));

            volumeSlider.onValueChanged.AddListener(v => { Classroom.SetVolume((int)v); volumeLabel.text = (int)v + "%"; });
            muteAllSwitch.Changed += on => { Classroom.SetMuteAll(on); Svc.SetMute(on); nav.Say(on ? "All students muted" : "All students unmuted"); };
            myVolume.onValueChanged.AddListener(v => { Classroom.SetVolume((int)v); myVolumeLabel.text = (int)v + "%"; });
            myMuteSwitch.Changed += on => { var q = MyQuest(); if (q != null) { Only(q); Svc.SetMute(on); } nav.Say(on ? "Your Quest is muted" : "Your Quest sound is on"); };

            playButton.onClick.AddListener(() => Playback(true));
            pauseButton.onClick.AddListener(() => Playback(false));
            videoButton.onClick.AddListener(() => Pick(MediaKind.Video));
            audioButton.onClick.AddListener(() => Pick(MediaKind.Audio));
            imageButton.onClick.AddListener(() => Pick(MediaKind.Image));
            textButton.onClick.AddListener(() => Pick(MediaKind.Text));
            controlsButton.onClick.AddListener(() => { SelectMeetingQuests(); nav.Push(ScreenId.Control); });
            sceneButton.onClick.AddListener(() => nav.Push(ScreenId.Scene));

            startButton.onClick.AddListener(StartSession);
            pauseSessionButton.onClick.AddListener(PauseSession);
            endButton.onClick.AddListener(() => nav.popup.Show("End session?",
                "The session ends for all students. The report opens.", "End session", EndSession, "Cancel", destructive: true));

            metricRowTemplate.gameObject.SetActive(false);
            studentRowTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnable() { base.OnEnable(); Classroom.Changed += OnClass; }

        protected override void OnDisable()
        {
            base.OnDisable();
            Classroom.Changed -= OnClass;
            fullscreen = false;
            // only the Live session turns: every other screen is portrait
            if (Screen.orientation != ScreenOrientation.Portrait) Screen.orientation = ScreenOrientation.Portrait;
        }

        void OnClass() { if (isActiveAndEnabled) Refresh(); }

        public override void OnShow()
        {
            if (Classroom.LiveActive && Classroom.IsAdmin) SelectMeetingQuests();
            laidOut = false;
            base.OnShow();
        }

        public override bool HandleBack()
        {
            if (fullscreen) { SetFullscreen(false); return true; }
            return false;
        }

        // ---------- actions ----------
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

        public void Rotate()
        {
            bool toLand = !landscape;
            Screen.orientation = toLand ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            Debug.Log("[MediSimVR] live session: " + (toLand ? "landscape" : "portrait"));
        }

        public void SetFullscreen(bool on)
        {
            fullscreen = on;
            ApplyLayout();
            nav.Chrome();
        }

        public void SelectTab(int i) { tabs.Select(i, true); }

        void Playback(bool play)
        {
            if (Classroom.State != LiveState.Running) { nav.Say(Classroom.State == LiveState.Ready ? "Tap START to begin the session" : "The session is paused: tap START"); return; }
            SelectMeetingQuests();
            if (Svc.SelectedOnline().Count == 0) { nav.Say("No online Quest in this meeting"); return; }
            bool anyPaused = false;
            foreach (var d in Svc.SelectedOnline()) if (d.State == DeviceState.Paused) anyPaused = true;
            if (play) { if (anyPaused) Svc.Resume(); else Svc.Play(); } else Svc.Pause();
            Classroom.Log(play ? SessionEventKind.Play : SessionEventKind.Pause, play ? "Play on the Quests" : "Pause on the Quests");
            SessionLog.Add(play ? SessionEventKind.Play : SessionEventKind.Pause, "Host", play ? "play" : "pause");
            nav.Say(play ? "Playing on the Quests" : "Paused on the Quests");
        }

        void Pick(MediaKind kind)
        {
            if (Classroom.State == LiveState.Ready) { nav.Say("Tap START to begin the session"); return; }
            chooser.Choose(kind, () =>
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

        void StartSession()
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
            // the JSON record exists once the session was started (START); ended while Ready = nothing recorded
            bool recorded = Ses != null ? Ses.Active : SessionLog.Recording;
            if (recorded) SessionLog.Add(SessionEventKind.Stop, "Host", "session ended");
            if (Ses != null && Ses.Active) Ses.End();
            Classroom.End(recorded ? SessionLog.FilePath : "", recorded ? SessionLog.UploadStatus : "Not started: nothing recorded");
            nav.Tab(ScreenId.Report);
        }

        void Leave()
        {
            nav.popup.Show("Leave session?", "You leave " + (Classroom.Live != null ? Classroom.Live.Name : "the session") + ".", "Leave", () =>
            {
                if (Ses != null && Ses.Active) Ses.End();
                string name = Classroom.Live != null ? Classroom.Live.Name : "";
                Classroom.Log(SessionEventKind.Leave, Classroom.UserName + " left");
                Classroom.End(SessionLog.FilePath, SessionLog.UploadStatus);
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
            string body = "Quest: " + (q != null ? q.Name + (q.Online ? "  (" + DeviceStatus.Label(DeviceStatus.Of(q)) + ", " + DeviceStatus.Battery(q) + ")" : "  (Offline)") : "none") +
                "\nStatus: " + (present ? "Present" + (x != null && x.Muted ? ", muted" : "") : "Absent");
            if (present && x != null)
                body += "\nAttention: " + Mathf.RoundToInt(x.Attention) + "%   Engagement: " + Mathf.RoundToInt(x.Engagement) + "%" +
                        "\nEmotion: " + Classroom.Mood(x.Emotion) + " (" + Mathf.RoundToInt(x.Emotion) + "%)";
            nav.popup.Show(s.Name, body, "Show Quest screen", () =>
            {
                Classroom.Focus(id);
                if (!landscape) SelectTab(1);
                nav.Say("Showing " + (q != null ? q.Name : "the Quest") + " of " + s.Name.Split(' ')[0]);
            }, "Close");
        }

        void ToggleMute(string id) { Classroom.ToggleMute(id); var x = Classroom.MetricsOf(id); nav.Say((x != null && x.Muted ? "Muted " : "Unmuted ") + Classroom.FirstName(id)); }

        // ---------- layout ----------
        private void Update()
        {
            var rt = (RectTransform)transform;
            bool land = rt.rect.width > rt.rect.height;
            // the canvas rescales a frame after the phone turns: lay out again whenever the size changes
            if (land != landscape || !laidOut || rt.rect.size != laidSize)
            {
                landscape = land;
                laidOut = true;
                laidSize = rt.rect.size;
                if (landscape) fullscreen = false;
                ApplyLayout();
                nav.Chrome();
            }
            if (Classroom.Live == null) return;
            UpdateMeters();
            if ((textTimer += Time.unscaledDeltaTime) < 0.25f) return;
            textTimer = 0f;
            UpdateTexts();
        }

        static void Box(RectTransform r, float x0, float x1, float top, float bottom, float l, float right)
        {
            r.anchorMin = new Vector2(x0, 0f);
            r.anchorMax = new Vector2(x1, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(l, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        void ApplyLayout()
        {
            bool live = Classroom.Live != null;
            bool admin = Classroom.IsAdmin;
            float head = landscape ? LandHead : HeadH;
            header.anchoredPosition = new Vector2(0f, landscape ? HeadH - LandHead : 0f);   // landscape: header 60 higher
            header.gameObject.SetActive(!fullscreen);
            emptyState.gameObject.SetActive(!live);
            questCard.gameObject.SetActive(live);
            rotateButton.gameObject.SetActive(live);
            leaveButton.gameObject.SetActive(live && !admin);
            controlBar.gameObject.SetActive(live && admin && !fullscreen);
            tabs.gameObject.SetActive(live && admin && !landscape && !fullscreen);
            studentPanel.gameObject.SetActive(live && !admin && !fullscreen);
            if (rotateLabel != null) rotateLabel.text = landscape ? "Portrait" : "Landscape";
            // the short Quest screen of the landscape columns has no room for the big icon
            questIcon.gameObject.SetActive(!(landscape && admin && !fullscreen));
            if (fullscreenIcon != null) fullscreenIcon.sprite = fullscreen ? collapseSprite : expandSprite;
            if (!live) { metricsPanel.gameObject.SetActive(false); questPanel.gameObject.SetActive(false); studentsPanel.gameObject.SetActive(false); return; }

            if (fullscreen)
            {
                Box(questCard, 0f, 1f, 0f, 0f, 0f, 0f);
                metricsPanel.gameObject.SetActive(false);
                questPanel.gameObject.SetActive(false);
                studentsPanel.gameObject.SetActive(false);
                return;
            }
            float bottom = admin ? BarH : 0f;
            if (!admin)
            {
                metricsPanel.gameObject.SetActive(false);
                questPanel.gameObject.SetActive(false);
                studentsPanel.gameObject.SetActive(false);
                if (landscape)
                {
                    Box(questCard, 0f, 0.62f, head, Gap, Side, Gap * 0.5f);
                    Box(studentPanel, 0.62f, 1f, head, 0f, Gap * 0.5f, 0f);
                }
                else
                {
                    Box(questCard, 0f, 1f, head, 0f, Side, Side);
                    SetHeight(questCard, head, QuestH);
                    Box(studentPanel, 0f, 1f, head + QuestH + Gap, 0f, 0f, 0f);
                }
                return;
            }
            if (landscape)
            {
                // three columns: Metrics | Quest screen + Quest panel | Students
                Box(metricsPanel, 0f, 0.3f, head, bottom, 0f, 0f);
                float h = ((RectTransform)transform).rect.height;
                float card = Mathf.Clamp((h - head - bottom) * 0.58f, 260f, QuestH);
                Box(questCard, 0.3f, 0.7f, head, 0f, Gap * 0.5f, Gap * 0.5f);
                SetHeight(questCard, head, card);
                Box(questPanel, 0.3f, 0.7f, head + card + Gap * 0.5f, bottom, -Side + Gap * 0.5f, -Side + Gap * 0.5f);
                Box(studentsPanel, 0.7f, 1f, head, bottom, 0f, 0f);
                metricsPanel.gameObject.SetActive(true);
                questPanel.gameObject.SetActive(true);
                studentsPanel.gameObject.SetActive(true);
            }
            else
            {
                Box(questCard, 0f, 1f, head, 0f, Side, Side);
                SetHeight(questCard, head, QuestH);
                var tr = (RectTransform)tabs.transform;
                Box(tr, 0f, 1f, head + QuestH + Gap, 0f, Side, Side);
                SetHeight(tr, head + QuestH + Gap, TabsH);
                float top = head + QuestH + Gap + TabsH + 8f;
                foreach (var p in new[] { metricsPanel, questPanel, studentsPanel }) Box(p, 0f, 1f, top, bottom, 0f, 0f);
                metricsPanel.gameObject.SetActive(tab == 0);
                questPanel.gameObject.SetActive(tab == 1);
                studentsPanel.gameObject.SetActive(tab == 2);
            }
        }

        // fixed height from the top (Box makes it full height)
        static void SetHeight(RectTransform r, float top, float h)
        {
            r.anchorMin = new Vector2(r.anchorMin.x, 1f);
            r.anchorMax = new Vector2(r.anchorMax.x, 1f);
            r.offsetMin = new Vector2(r.offsetMin.x, -top - h);
            r.offsetMax = new Vector2(r.offsetMax.x, -top);
        }

        // ---------- data ----------
        void UpdateMeters()
        {
            float a, e, m;
            Classroom.Average(out a, out e, out m);
            classAttention.Set(a);
            classEngagement.Set(e);
            classEmotion.Set(m);
            foreach (var r in metricRows) if (r.gameObject.activeSelf) r.SetValues(Classroom.MetricsOf(r.studentId));
            foreach (var r in studentRows) if (r.gameObject.activeSelf) r.SetValues(Classroom.MetricsOf(r.studentId));
            var mine = Classroom.MetricsOf(Classroom.StudentId);
            if (mine != null) { myAttention.Set(mine.ShownAttention); myEngagement.Set(mine.ShownEngagement); }
        }

        void UpdateTexts()
        {
            var m = Classroom.Live;
            bool running = Classroom.State == LiveState.Running;
            liveChipText.text = StateText() + "  " + Clock(Classroom.Elapsed);
            Color c = ThemeManager.Get(running ? DemoConfig.LiveRole : Classroom.State == LiveState.Paused ? ColorRole.Warning : ColorRole.Primary);
            liveChipText.color = c;
            liveDot.color = c;
            liveChip.color = new Color(c.r, c.g, c.b, 0.14f);
            float a, e, mo;
            Classroom.Average(out a, out e, out mo);
            moodText.text = "Class mood: " + Classroom.Mood(mo);

            var q = Classroom.IsAdmin ? FocusQuest() : MyQuest();
            MediaFile f = null;
            if (q != null) foreach (var x in Svc.Files) if (x.Id == q.FileId) f = x;
            string who = Classroom.IsAdmin ? Classroom.FirstName(Classroom.FocusStudentId) : "You";
            questName.text = (q != null ? q.Name : "No Quest") + "  •  " + who;
            questContent.text = q == null ? "No Quest assigned" : !q.Online ? "Offline" : DeviceStatus.Content(q, Svc);
            questState.text = q == null || !q.Online ? "NO SIGNAL" : Classroom.State == LiveState.Paused ? "SESSION PAUSED" : q.State == DeviceState.Playing ? "LIVE VIEW" : q.State == DeviceState.Paused ? "PAUSED" : "IDLE";
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

            // Quest panel
            if (q != null)
            {
                deviceName.text = q.Name + "  •  " + Classroom.FirstName(Classroom.FocusStudentId);
                deviceStatus.text = q.Online ? DeviceStatus.Label(DeviceStatus.Of(q)) + "  •  Connected  •  " + DeviceStatus.Battery(q) + (q.Muted ? "  •  muted" : "") : "Offline  •  not connected";
                devicePlaying.text = q.Online ? DeviceStatus.Content(q, Svc) : "-";
            }
            if (!Classroom.IsAdmin && q != null)
            {
                myQuestText.text = q.Name + "  •  " + (q.Online ? DeviceStatus.Battery(q) : "Offline");
                myStatusText.text = (running ? "In class" : Classroom.State == LiveState.Paused ? "Class paused" : "Waiting") + "  •  " + DeviceStatus.Content(q, Svc);
            }
            else if (!Classroom.IsAdmin) { myQuestText.text = "No Quest assigned"; myStatusText.text = running ? "In class" : "Waiting"; }

            startLabel.text = Classroom.State == LiveState.Paused ? "RESUME" : "START";
            startButton.interactable = Classroom.State != LiveState.Running;
            pauseSessionButton.interactable = running;
            subtitleText.text = m.Topic + "  •  " + m.PresentCount + "/" + m.StudentIds.Count + " present";
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
            // metrics + students lists (present first)
            var ids = new List<string>();
            foreach (var id in m.StudentIds) if (!m.Absent.Contains(id)) ids.Add(id);
            foreach (var id in m.StudentIds) if (m.Absent.Contains(id)) ids.Add(id);
            if (admin)
            {
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
                    string st = !present ? "Absent" : q == null ? "No Quest" : !q.Online ? qn + "  •  Offline" : qn + "  •  " + DeviceStatus.Label(DeviceStatus.Of(q)) + (q.Battery >= 0 ? "  •  " + q.Battery + "%" : "");
                    studentRows[i].Bind(id, s.Name, st, present, muted, id == Classroom.FocusStudentId, ShowStudent, null);
                }
                studentCount.text = m.StudentIds.Count + " students";
                attendanceText.text = m.PresentCount + " present  •  " + (m.StudentIds.Count - m.PresentCount) + " absent";
                devicesText.text = online + " of " + m.QuestIds.Count + " Quests online";
                muteAllSwitch.Set(Classroom.MuteAll, false);
                volumeSlider.SetValueWithoutNotify(Classroom.Volume);
                volumeLabel.text = Classroom.Volume + "%";
            }
            else
            {
                myVolume.SetValueWithoutNotify(Classroom.Volume);
                myVolumeLabel.text = Classroom.Volume + "%";
                var q = MyQuest();
                myMuteSwitch.Set(q != null && q.Muted, false);
            }
            ApplyLayout();
            UpdateTexts();
        }
    }
}
