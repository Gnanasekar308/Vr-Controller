using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Live session dashboard + Session report (part of MediSimUIBuilder). The Live screen positions its parts itself
    // (portrait: Quest screen on top + tabs; landscape: three columns), see LiveSessionScreen.ApplyLayout.
    public static partial class MediSimUIBuilder
    {
        // ---------- small parts ----------
        // label | track with fill | value; the fill colour is set by MeterView (low values orange)
        static MeterView Meter(Transform parent, string label, ColorRole role, float h = 64f, float labelW = 250f)
        {
            var row = New(label.Replace(" ", "") + "Meter", parent);
            LE(row, h);
            var v = row.gameObject.AddComponent<MeterView>();
            v.role = role;
            if (labelW > 0f)
            {
                v.label = Txt(row, "Label", label, TextRole.Caption, ColorRole.Text, TextWeight.Regular);
                Place(v.label.rectTransform, 0f, 0.5f, labelW, TextBoxH(TextRole.Caption));
            }
            v.valueText = Txt(row, "Value", "0%", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.MidlineRight);
            Place(v.valueText.rectTransform, 1f, 0.5f, 120f, TextBoxH(TextRole.Caption));
            var track = Shape(row, "Track", ColorRole.Divider, ShapeKind.Pill);
            track.GetComponent<ThemedShape>().pillHeight = 18f;
            track.raycastTarget = false;
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.offsetMin = new Vector2(labelW > 0f ? labelW + 16f : 0f, -9f);
            track.rectTransform.offsetMax = new Vector2(-136f, 9f);
            var fill = Shape(track.transform, "Fill", role, ShapeKind.Pill);
            fill.GetComponent<ThemedShape>().pillHeight = 18f;
            fill.GetComponent<ThemedColor>().enabled = false;
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            v.fill = fill.rectTransform;
            v.fillImage = fill;
            return v;
        }

        // student row of the Live session / report: name, secondary line, focus ring, mute button, meters
        static StudentRowView StudentRow(Transform box, string name, bool threeMeters, bool withMute)
        {
            float meterH = 52f;
            float top = 24f + TextBoxH(TextRole.Body) + TextBoxH(TextRole.Caption) + 8f;
            float h = top + (threeMeters ? 3f : 1f) * meterH + 20f;
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(box, name, h, out card, out btn, out container);
            var v = container.AddComponent<StudentRowView>();
            v.button = btn;
            v.nameText = RowTitle(rt, "Name", "Student");
            TopLeft(v.nameText.rectTransform, CardPad, 20f, withMute ? 150f : CardPad, TextBoxH(TextRole.Body));
            v.subText = Secondary(rt, "Sub", "Quest");
            TopLeft(v.subText.rectTransform, CardPad, 20f + TextBoxH(TextRole.Body), withMute ? 150f : CardPad, TextBoxH(TextRole.Caption));
            if (withMute)
            {
                var hit = Img(rt, "Mute", circle, ColorRole.PrimarySoft);
                Place(hit.rectTransform, 1f, 1f, 96f, 96f, -24f, -20f);
                v.muteButton = MakeButton(hit.gameObject, hit);
                v.muteIcon = Icon(hit.transform, "Icon", "volume_up", ColorRole.Primary, 52f);
                v.muteIcon.GetComponent<ThemedColor>().enabled = false;
                Place(v.muteIcon.rectTransform, 0.5f, 0.5f, 52f, 52f);
                v.soundSprite = IconS("volume_up");
                v.mutedSprite = IconS("volume_off");
            }
            var meters = New("Meters", rt);
            meters.anchorMin = new Vector2(0f, 0f);
            meters.anchorMax = new Vector2(1f, 1f);
            meters.offsetMin = new Vector2(CardPad, 20f);
            meters.offsetMax = new Vector2(-CardPad, -top);
            VLayout(meters.gameObject, 0, 0f).padding = new RectOffset(0, 0, 0, 0);
            v.attention = Meter(meters, "Attention", ColorRole.Primary, meterH, 210f);
            if (threeMeters)
            {
                v.engagement = Meter(meters, "Engagement", ColorRole.Secondary, meterH, 210f);
                v.emotion = Meter(meters, "Emotion", ColorRole.Success, meterH, 210f);
            }
            var ring = Img(rt, "FocusRing", outline, ColorRole.Primary);
            ring.type = Image.Type.Sliced;
            ring.raycastTarget = false;
            ring.gameObject.AddComponent<ThemedShape>().kind = ShapeKind.Card;
            Stretch(ring.rectTransform);
            v.focusRing = ring;
            ring.gameObject.SetActive(false);
            container.SetActive(false);
            return v;
        }

        // a row of small action tiles (Play / Pause, Video / Audio / Image / Text)
        static RectTransform TileRow(Transform parent, string name, float h = 128f)
        {
            var row = New(name, parent);
            LE(row, h);
            HLayout(row.gameObject, 16f);
            return row;
        }

        // ===================================================================== live session
        static AppScreen BuildLive(Transform parent)
        {
            var s = MakeScreen<LiveSessionScreen>(parent, "Live", ScreenId.Live);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            var hdr = Header(s.transform, "Live Session", "No live session", false, out title, out sub, out back);
            s.header = hdr;
            s.titleText = title;
            s.subtitleText = sub;
            title.rectTransform.offsetMax = new Vector2(-520f, title.rectTransform.offsetMax.y);
            sub.rectTransform.offsetMax = new Vector2(-170f, sub.rectTransform.offsetMax.y);
            s.rotateButton = CircleButton(hdr, "Rotate", "swap_horiz", 1f, -48f, 118f);
            Image dot;
            TextMeshProUGUI chipT;
            s.liveChip = StatusChip(hdr, "LiveChip", DemoConfig.LiveRole, "LIVE 00:00:00", 340f, out dot, out chipT);
            s.liveChip.GetComponent<ThemedColor>().enabled = false;
            dot.GetComponent<ThemedColor>().enabled = false;
            chipT.GetComponent<ThemedText>().applyColor = false;
            s.liveChip.rectTransform.anchorMin = s.liveChip.rectTransform.anchorMax = new Vector2(1f, 1f);
            s.liveChip.rectTransform.pivot = new Vector2(1f, 0.5f);
            s.liveChip.rectTransform.anchoredPosition = new Vector2(-176f, -118f);
            s.liveDot = dot;
            s.liveChipText = chipT;

            // ---- panels (scroll lists; positioned by the screen)
            float panelTop = LiveSessionScreen.HeadH + LiveSessionScreen.QuestH + LiveSessionScreen.Gap + LiveSessionScreen.TabsH + 8f;
            // Metrics
            var ml = ScrollList(s.transform, "MetricsPanel", panelTop, LiveSessionScreen.BarH);
            ml.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.metricsPanel = (RectTransform)ml.parent.parent;
            var sec = Section(ml, "Class average");
            s.moodText = Txt(sec, "Mood", "Class mood: Positive", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            LE(s.moodText, TextBoxH(TextRole.Body));
            s.classEmotion = Meter(sec, "Emotional", ColorRole.Success);
            s.classEngagement = Meter(sec, "Engagement", ColorRole.Secondary);
            s.classAttention = Meter(sec, "Attention", ColorRole.Primary);
            sec = Section(ml, "Sound control");
            FieldLabel(sec, "Master volume");
            TextMeshProUGUI vl;
            s.volumeSlider = MakeSlider(sec, "Volume", 0, 100, out vl);
            s.volumeLabel = vl;
            var muteRow = New("MuteAllRow", sec);
            LE(muteRow, 90f);
            var ml2 = BodyText(muteRow, "Label", "Mute all students");
            Stretch(ml2.rectTransform, 0, 0, 160, 0);
            s.muteAllSwitch = Switch(muteRow, "MuteAll");
            Place((RectTransform)s.muteAllSwitch.transform, 1f, 0.5f, 120f, 68f);
            var st = SectionTitle(ml, "StudentsTitle", "Students");
            LE(st, TextBoxH(TextRole.Body));
            s.metricRowTemplate = StudentRow(RowsBox(ml, "MetricRows"), "MetricRow", true, true);

            // Quest
            var ql = ScrollList(s.transform, "QuestPanel", panelTop, LiveSessionScreen.BarH);
            ql.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.questPanel = (RectTransform)ql.parent.parent;
            sec = Section(ql, "Device status");
            s.deviceName = RowTitle(sec, "Device", "Quest-A1B2");
            LE(s.deviceName, TextBoxH(TextRole.Body));
            s.deviceStatus = Secondary(sec, "Status", "Online  •  Connected  •  86% battery");
            LE(s.deviceStatus, TextBoxH(TextRole.Caption));
            s.devicePlaying = BodyText(sec, "Playing", "Playing CPR_Training.mp4");
            LE(s.devicePlaying, TextBoxH(TextRole.Body));
            var pr = TileRow(sec, "Playback");
            s.playButton = ActionTile(pr, "Play", "play_arrow", ColorRole.Success);
            s.pauseButton = ActionTile(pr, "Pause", "pause", ColorRole.Primary);
            sec = Section(ql, "Play content");
            var cr = TileRow(sec, "Content");
            s.videoButton = ActionTile(cr, "Video", "movie");
            s.audioButton = ActionTile(cr, "Audio", "music_note");
            s.imageButton = ActionTile(cr, "Image", "image");
            s.textButton = ActionTile(cr, "Text", "article");
            var more = New("More", ql);
            LE(more, SmallBtnH);
            HLayout(more.gameObject, CardGap);
            s.controlsButton = Btn(more, "AllControls", BtnKind.Tertiary, "tune", "All controls");
            s.sceneButton = Btn(more, "LiveScene", BtnKind.Tertiary, "view_in_ar", "Live Scene");

            // Students
            var sl = ScrollList(s.transform, "StudentsPanel", panelTop, LiveSessionScreen.BarH);
            sl.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.studentsPanel = (RectTransform)sl.parent.parent;
            sec = Section(sl, "Class");
            s.studentCount = RowTitle(sec, "Count", "6 students");
            LE(s.studentCount, TextBoxH(TextRole.Body));
            s.attendanceText = Secondary(sec, "Attendance", "5 present  •  1 absent");
            LE(s.attendanceText, TextBoxH(TextRole.Caption));
            s.devicesText = Secondary(sec, "Devices", "4 of 4 Quests online");
            LE(s.devicesText, TextBoxH(TextRole.Caption));
            s.studentRowTemplate = StudentRow(RowsBox(sl, "StudentRows"), "StudentRow", false, false);

            // student view (no admin controls): own Quest, own status, sound, Leave
            var pl = ScrollList(s.transform, "StudentPanel", LiveSessionScreen.HeadH + LiveSessionScreen.QuestH + LiveSessionScreen.Gap, 0f);
            pl.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.studentPanel = (RectTransform)pl.parent.parent;
            sec = Section(pl, "My Quest");
            s.myQuestText = RowTitle(sec, "Quest", "Quest-A1B2  •  86% battery");
            LE(s.myQuestText, TextBoxH(TextRole.Body));
            s.myStatusText = Secondary(sec, "Status", "In class");
            LE(s.myStatusText, TextBoxH(TextRole.Caption));
            sec = Section(pl, "My focus");
            s.myAttention = Meter(sec, "Attention", ColorRole.Primary);
            s.myEngagement = Meter(sec, "Engagement", ColorRole.Secondary);
            sec = Section(pl, "Sound");
            FieldLabel(sec, "Volume");
            s.myVolume = MakeSlider(sec, "MyVolume", 0, 100, out vl);
            s.myVolumeLabel = vl;
            var mr = New("MuteRow", sec);
            LE(mr, 90f);
            var mlb = BodyText(mr, "Label", "Mute my Quest");
            Stretch(mlb.rectTransform, 0, 0, 160, 0);
            s.myMuteSwitch = Switch(mr, "MyMute");
            Place((RectTransform)s.myMuteSwitch.transform, 1f, 0.5f, 120f, 68f);
            s.leaveButton = Btn(pl, "Leave", BtnKind.Destructive, "close", "Leave session");

            // ---- Quest screen (after the panels: on top of them in full screen)
            var qc = Shape(s.transform, "QuestScreen", ColorRole.Text, ShapeKind.Card);
            qc.GetComponent<ThemedColor>().enabled = false;
            qc.color = Color.white;
            TopBand(qc.rectTransform, LiveSessionScreen.HeadH, LiveSessionScreen.QuestH, Margin);
            s.questCard = qc.rectTransform;
            s.questGradient = Gradient(qc, true);
            s.questGradient.useTheme = false;
            s.questIcon = Icon(qc.transform, "Icon", "vr_headset", ColorRole.OnPrimary, 132f);
            s.questIcon.GetComponent<ThemedColor>().enabled = false;
            s.questIcon.color = new Color(1f, 1f, 1f, 0.9f);
            Place(s.questIcon.rectTransform, 0.5f, 0.5f, 132f, 132f, 0f, 20f);
            s.videoSprite = IconS("movie");
            s.audioSprite = IconS("music_note");
            s.imageSprite = IconS("image");
            s.textSprite = IconS("article");
            s.idleSprite = IconS("vr_headset");
            s.expandSprite = IconS("open_in_full");
            s.collapseSprite = IconS("close");
            var stateBg = Shape(qc.transform, "State", ColorRole.Text, ShapeKind.Pill);
            stateBg.GetComponent<ThemedColor>().enabled = false;
            stateBg.color = new Color(0f, 0f, 0f, 0.45f);
            stateBg.GetComponent<ThemedShape>().pillHeight = 56f;
            stateBg.raycastTarget = false;
            Place(stateBg.rectTransform, 0f, 1f, 300f, 56f, 24f, -24f);
            s.questState = Txt(stateBg.transform, "Text", "LIVE VIEW", TextRole.Small, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(s.questState.rectTransform, 12, 0, 12, 0);
            var fsBg = Img(qc.transform, "Fullscreen", circle, ColorRole.Text);
            fsBg.GetComponent<ThemedColor>().enabled = false;
            fsBg.color = new Color(0f, 0f, 0f, 0.45f);
            Place(fsBg.rectTransform, 1f, 1f, 96f, 96f, -20f, -20f);
            s.fullscreenButton = MakeButton(fsBg.gameObject, fsBg);
            s.fullscreenIcon = Icon(fsBg.transform, "Icon", "open_in_full", ColorRole.OnPrimary, 48f);
            Place(s.fullscreenIcon.rectTransform, 0.5f, 0.5f, 48f, 48f);
            s.questName = Txt(qc.transform, "Name", "Quest-A1B2  •  Arjun", TextRole.Body, ColorRole.OnPrimary, TextWeight.SemiBold);
            BottomBand(s.questName.rectTransform, 40f + TextBoxH(TextRole.Caption), TextBoxH(TextRole.Body), 32f);
            s.questName.rectTransform.offsetMax = new Vector2(-320f, s.questName.rectTransform.offsetMax.y);
            s.questContent = Txt(qc.transform, "Content", "Playing CPR_Training.mp4", TextRole.Caption, ColorRole.OnPrimary);
            BottomBand(s.questContent.rectTransform, 36f, TextBoxH(TextRole.Caption), 32f);
            s.questTime = Txt(qc.transform, "Time", "01:35 / 05:00", TextRole.Small, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.MidlineRight);
            Place(s.questTime.rectTransform, 1f, 0f, 280f, TextBoxH(TextRole.Small), -32f, 40f + TextBoxH(TextRole.Caption));
            var track = Shape(qc.transform, "Progress", ColorRole.OnPrimary, ShapeKind.Pill, 0.3f);
            track.GetComponent<ThemedShape>().pillHeight = 10f;
            track.raycastTarget = false;
            BottomBand(track.rectTransform, 16f, 10f, 32f);
            s.progressFill = Shape(track.transform, "Fill", ColorRole.OnPrimary, ShapeKind.Pill);
            s.progressFill.GetComponent<ThemedShape>().pillHeight = 10f;
            s.progressFill.raycastTarget = false;
            s.progressFill.rectTransform.anchorMin = Vector2.zero;
            s.progressFill.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            s.progressFill.rectTransform.offsetMin = s.progressFill.rectTransform.offsetMax = Vector2.zero;

            // ---- tabs
            s.tabs = Seg(s.transform, "Tabs", new[] { "Metrics", "Quest", "Students" }, LiveSessionScreen.TabsH, true,
                new[] { "tune", "vr_headset", "view_agenda" });
            TopBand((RectTransform)s.tabs.transform, LiveSessionScreen.HeadH + LiveSessionScreen.QuestH + LiveSessionScreen.Gap, LiveSessionScreen.TabsH, Margin);

            // ---- session control bar: START / PAUSE / END
            var bar = New("ControlBar", s.transform);
            BottomBand(bar, 24f, BtnH, Margin);
            HLayout(bar.gameObject, 16f);
            s.controlBar = bar;
            TextMeshProUGUI sl2;
            s.startButton = Btn(bar, "Start", BtnKind.Primary, "play_arrow", "START", out sl2);
            s.startLabel = sl2;
            s.pauseSessionButton = Btn(bar, "Pause", BtnKind.Secondary, "pause", "PAUSE");
            s.endButton = Btn(bar, "End", BtnKind.Destructive, "stop", "END");

            // ---- no live session
            var empty = New("Empty", s.transform);
            Stretch(empty, 0, 0, 0, LiveSessionScreen.HeadH);
            s.emptyState = empty;
            var et = Txt(empty, "Text", "No live session.\nOpen a meeting and tap Start Session.", TextRole.Body, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            et.enableWordWrapping = true;
            TopBand(et.rectTransform, 200f, 140f);
            s.emptyButton = Btn(empty, "OpenMeetings", BtnKind.Primary, "view_agenda", "Open meetings");
            TopBand((RectTransform)s.emptyButton.transform, 380f, BtnH, 200f);
            return s;
        }

        // ===================================================================== report
        static AppScreen BuildReport(Transform parent)
        {
            var s = MakeScreen<SessionReportScreen>(parent, "Report", ScreenId.Report);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "Session Report", "Meeting 01", false, out title, out sub, out back);
            s.subtitleText = sub;
            var list = ScrollList(s.transform, "List", HeadH, 48f + BtnH + CardGap);
            list.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.emptyText = Secondary(list, "Empty", "No session ended yet");
            s.emptyText.alignment = TextAlignmentOptions.Center;
            LE(s.emptyText, 80f);

            var sec = Section(list, "Meeting");
            s.infoText = Txt(sec, "Info", "Meeting", TextRole.Body, ColorRole.Text);
            s.infoText.enableWordWrapping = true;
            s.infoText.overflowMode = TextOverflowModes.Overflow;
            s.infoText.richText = true;

            sec = Section(list, "Attendance");
            var stats = New("Stats", sec);
            LE(stats, 180f);
            HLayout(stats.gameObject, CardGap);
            s.presentValue = StatBox(stats, "Present", ColorRole.Success);
            s.absentValue = StatBox(stats, "Absent", ColorRole.Danger);
            s.absentNames = Secondary(sec, "AbsentNames", "Everyone was present");
            s.absentNames.enableWordWrapping = true;
            s.absentNames.overflowMode = TextOverflowModes.Overflow;

            sec = Section(list, "Engagement and attention");
            FieldLabel(sec, "Class average");
            s.classEngagement = Meter(sec, "Engagement", ColorRole.Secondary);
            s.classAttention = Meter(sec, "Attention", ColorRole.Primary);
            s.classEmotion = Meter(sec, "Emotion", ColorRole.Success, 64f, 250f);
            s.classEmotion.valueText.rectTransform.sizeDelta = new Vector2(230f, s.classEmotion.valueText.rectTransform.sizeDelta.y);
            ((RectTransform)s.classEmotion.transform.Find("Track")).offsetMax = new Vector2(-246f, 9f);
            FieldLabel(sec, "Per student");
            s.studentTemplate = StudentRow(RowsBox(sec, "Students", 16f), "StudentRow", true, false);

            sec = Section(list, "Content played");
            s.contentText = BodyText(sec, "Content", "Nothing was played");
            s.contentText.enableWordWrapping = true;
            s.contentText.overflowMode = TextOverflowModes.Overflow;

            sec = Section(list, "Timeline");
            s.timelineTemplate = TimelineRow(RowsBox(sec, "Events", 10f));

            sec = Section(list, "Session record");
            s.savedText = Secondary(sec, "Saved", "Saved on this phone");
            s.savedText.enableWordWrapping = true;
            s.savedText.overflowMode = TextOverflowModes.Overflow;

            var bar = New("BottomBar", s.transform);
            BottomBand(bar, 48f, BtnH, Margin);
            var hl = HLayout(bar.gameObject, CardGap);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childForceExpandWidth = false;
            s.shareButton = Btn(bar, "Share", BtnKind.Secondary, "upload", "Share");
            LE(s.shareButton, -1f, 300f, 0f);
            s.dashboardButton = Btn(bar, "Dashboard", BtnKind.Primary, "home", "Back to Dashboard");
            LE(s.dashboardButton, -1f, -1f, 1f);
            return s;
        }

        // tinted box: big number + label
        static TextMeshProUGUI StatBox(Transform parent, string label, ColorRole role)
        {
            var bg = Shape(parent, label, role, ShapeKind.Field, 0.12f);
            bg.raycastTarget = false;
            var v = Txt(bg.transform, "Value", "0", TextRole.Title, role, TextWeight.Heading, TextAlignmentOptions.Center);
            TopBand(v.rectTransform, 18f, TextBoxH(TextRole.Title), 8f);
            var l = Txt(bg.transform, "Label", label, TextRole.Small, role, TextWeight.SemiBold, TextAlignmentOptions.Center);
            BottomBand(l.rectTransform, 18f, TextBoxH(TextRole.Small), 8f);
            return v;
        }
    }
}
