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
            Place(v.valueText.rectTransform, 1f, 0.5f, 104f, TextBoxH(TextRole.Caption));
            var track = Shape(row, "Track", ColorRole.Divider, ShapeKind.Pill);
            track.GetComponent<ThemedShape>().pillHeight = 18f;
            track.raycastTarget = false;
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.offsetMin = new Vector2(labelW > 0f ? labelW + 16f : 0f, -9f);
            track.rectTransform.offsetMax = new Vector2(-116f, 9f);
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
        static StudentRowView StudentRow(Transform box, string name, bool threeMeters, bool withMute, bool participation = false)
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
                if (participation) v.participation = Meter(meters, "Participation", ColorRole.Success, meterH, 210f);
                else v.emotion = Meter(meters, "Emotion", ColorRole.Success, meterH, 210f);
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
        // round icon button on the Quest screen (dark glass circle)
        static Button CardIcon(Transform parent, string name, string icon, float x, out Image iconImg)
        {
            var bg = Img(parent, name, circle, ColorRole.Text);
            bg.GetComponent<ThemedColor>().enabled = false;
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            Place(bg.rectTransform, 1f, 1f, 92f, 92f, x, -20f);
            var b = MakeButton(bg.gameObject, bg);
            iconImg = Icon(bg.transform, "Icon", icon, ColorRole.OnPrimary, 46f);
            Place(iconImg.rectTransform, 0.5f, 0.5f, 46f, 46f);
            return b;
        }

        // initials in a soft circle
        static TextMeshProUGUI Avatar(Transform parent, float size, float x)
        {
            var c = Img(parent, "Avatar", circle, ColorRole.PrimarySoft);
            c.raycastTarget = false;
            Place(c.rectTransform, 0f, 0.5f, size, size, x, 0f);
            var t = Txt(c.transform, "Initials", "AK", size > 70f ? TextRole.Caption : TextRole.Small, ColorRole.Primary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            return t;
        }

        // question (with time + New chip under the avatar) or comment row; the row grows with its text
        static LiveLineView LineRow(Transform box, string name, bool question)
        {
            var r = New(name, box);
            var vl = VLayout(r.gameObject, 0, 2f);
            vl.padding = new RectOffset(100, 18, 12, 14);
            var le = LE(r, -1f);
            le.minHeight = question ? 132f : 96f;
            var bg = Shape(r, "Bg", ColorRole.Field, ShapeKind.Field);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(bg.rectTransform);
            var v = r.gameObject.AddComponent<LiveLineView>();
            if (question) v.button = MakeButton(bg.gameObject, bg);
            else bg.raycastTarget = false;
            v.avatarText = Avatar(r, 64f, 18f);
            var av = (RectTransform)v.avatarText.transform.parent;
            av.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            av.anchorMin = av.anchorMax = new Vector2(0f, 1f);
            av.pivot = new Vector2(0f, 1f);
            av.anchoredPosition = new Vector2(18f, -14f);
            var head = New("Head", r);
            LE(head, TextBoxH(TextRole.Small));
            v.nameText = Txt(head, "Name", "Student", TextRole.Small, ColorRole.Text, TextWeight.SemiBold);
            Stretch(v.nameText.rectTransform, 0, 0, 110, 0);
            v.timeText = Txt(head, "Time", "10:22", TextRole.Small, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.MidlineRight);
            Place(v.timeText.rectTransform, 1f, 0.5f, 104f, TextBoxH(TextRole.Small));
            v.messageText = Txt(r, "Message", "", TextRole.Small, ColorRole.Text);
            v.messageText.enableWordWrapping = true;
            v.messageText.overflowMode = TextOverflowModes.Overflow;
            v.messageText.alignment = TextAlignmentOptions.TopLeft;
            if (question)
            {
                Image dot;
                TextMeshProUGUI ct;
                var chip = StatusChip(r, "New", ColorRole.Primary, "New", 84f, out dot, out ct);
                chip.GetComponent<ThemedShape>().pillHeight = 40f;
                chip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Place(chip.rectTransform, 0f, 1f, 84f, 40f, 8f, -86f);
                Place(dot.rectTransform, 0f, 0.5f, 0f, 0f);
                Stretch(ct.rectTransform, 4, 0, 4, 0);
                ct.alignment = TextAlignmentOptions.Center;
                v.newChip = chip.gameObject;
                v.chipText = ct;
            }
            r.gameObject.SetActive(false);
            return v;
        }

        // compact student row (Students column): avatar with an online dot, name / Quest, mute, battery
        static StudentRowView CompactStudentRow(Transform box)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(box, "StudentRow", 124f, out card, out btn, out container);
            var v = container.AddComponent<StudentRowView>();
            v.button = btn;
            v.avatarText = Avatar(rt, 76f, 22f);
            v.statusDot = Img(rt, "Dot", circle, ColorRole.Success);
            v.statusDot.GetComponent<ThemedColor>().enabled = false;
            v.statusDot.raycastTarget = false;
            Place(v.statusDot.rectTransform, 0f, 0.5f, 22f, 22f, 78f, -26f);
            v.nameText = Txt(rt, "Name", "Student", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            TopLeft(v.nameText.rectTransform, 116f, 18f, 196f, TextBoxH(TextRole.Caption));
            v.subText = Secondary(rt, "Sub", "Quest");
            v.subText.GetComponent<ThemedText>().size = TextRole.Small;
            Shrink(v.subText);
            TopLeft(v.subText.rectTransform, 116f, 18f + TextBoxH(TextRole.Caption), 196f, TextBoxH(TextRole.Small));
            var hit = Img(rt, "Mute", circle, ColorRole.PrimarySoft);
            Place(hit.rectTransform, 1f, 0.5f, 72f, 72f, -100f, 0f);
            v.muteButton = MakeButton(hit.gameObject, hit);
            v.muteIcon = Icon(hit.transform, "Icon", "volume_up", ColorRole.Primary, 40f);
            v.muteIcon.GetComponent<ThemedColor>().enabled = false;
            Place(v.muteIcon.rectTransform, 0.5f, 0.5f, 40f, 40f);
            v.soundSprite = IconS("volume_up");
            v.mutedSprite = IconS("volume_off");
            v.batteryText = Txt(rt, "Battery", "90%", TextRole.Small, ColorRole.SubText, TextWeight.SemiBold, TextAlignmentOptions.MidlineRight);
            Place(v.batteryText.rectTransform, 1f, 0.5f, 84f, TextBoxH(TextRole.Small), -12f, 0f);
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

        // section card whose title row has a chip on the right; returns the card
        static RectTransform SectionWithChip(Transform list, string title, string chip, ColorRole role, out TMP_Text titleText, out GameObject chipGo)
        {
            var sec = Section(list, title);
            titleText = sec.Find("Title").GetComponent<TextMeshProUGUI>();
            Image dot;
            TextMeshProUGUI ct;
            var c = StatusChip(sec, "Chip", role, chip, 140f, out dot, out ct);
            c.GetComponent<ThemedShape>().pillHeight = 52f;
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            Place(c.rectTransform, 1f, 1f, 140f, 52f, -40f, -38f);
            chipGo = c.gameObject;
            return sec;
        }

        static AppScreen BuildLive(Transform parent)
        {
            var s = MakeScreen<LiveSessionScreen>(parent, "Live", ScreenId.Live);
            Wave(s.transform);
            float H = LiveSessionScreen.HeadH;
            // compact header (landscape): back, title + subtitle, REC chip (while recording), LIVE chip
            var hdr = New("Header", s.transform);
            TopBand(hdr, 0f, H, 0f);
            s.header = hdr;
            s.backButton = CircleButton(hdr, "Back", "arrow_back", 0f, 40f, 84f);
            s.titleText = Txt(hdr, "Title", "Live Session", TextRole.Title, ColorRole.Text, TextWeight.Heading);
            TopLeft(s.titleText.rectTransform, 180f, 26f, 760f, TextBoxH(TextRole.Title));
            s.subtitleText = Txt(hdr, "Subtitle", "No live session", TextRole.Caption, ColorRole.SubText);
            TopLeft(s.subtitleText.rectTransform, 180f, 26f + TextBoxH(TextRole.Title), 760f, TextBoxH(TextRole.Caption));
            Image dot;
            TextMeshProUGUI chipT;
            s.liveChip = StatusChip(hdr, "LiveChip", DemoConfig.LiveRole, "LIVE 00:00:00", 340f, out dot, out chipT);
            s.liveChip.GetComponent<ThemedColor>().enabled = false;
            dot.GetComponent<ThemedColor>().enabled = false;
            chipT.GetComponent<ThemedText>().applyColor = false;
            Place(s.liveChip.rectTransform, 1f, 1f, 340f, ChipH, -40f, -52f);
            s.liveDot = dot;
            s.liveChipText = chipT;
            var rec = StatusChip(hdr, "RecChip", ColorRole.Danger, "REC  00:00", 260f, out s.recDot, out chipT);
            rec.color = ThemeManager.Get(ColorRole.Danger);
            rec.GetComponent<ThemedColor>().alpha = 1f;
            s.recDot.GetComponent<ThemedColor>().role = ColorRole.OnPrimary;
            chipT.GetComponent<ThemedText>().role = ColorRole.OnPrimary;
            Place(rec.rectTransform, 1f, 1f, 260f, ChipH, -400f, -52f);
            s.recChip = rec.gameObject;
            s.recText = chipT;
            rec.gameObject.SetActive(false);

            // ---- left: Live metrics (admin sliders), questions, comments, sound, students
            var ml = ScrollList(s.transform, "MetricsPanel", H, 0f);
            ml.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.metricsPanel = (RectTransform)ml.parent.parent;
            // demo mode OFF: shown instead of the simulated metrics / questions / comments / recording
            var noteSec = Section(ml, "Classroom data");
            var note = Secondary(noteSec, "Note", "Live metrics, questions, comments and recording come from the classroom server. They show here once it is connected.");
            note.enableWordWrapping = true;
            note.overflowMode = TextOverflowModes.Overflow;
            s.backendNote = noteSec.gameObject;
            noteSec.gameObject.SetActive(false);
            var sec = Section(ml, "Live metrics");
            s.simulatedOnly.Add(sec.gameObject);
            var setBg = Img(sec, "Settings", circle, ColorRole.PrimarySoft);
            setBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(setBg.rectTransform, 1f, 1f, 76f, 76f, -32f, -24f);
            s.metricsSettingsButton = MakeButton(setBg.gameObject, setBg);
            var setIc = Icon(setBg.transform, "Icon", "tune", ColorRole.Primary, 42f);
            Place(setIc.rectTransform, 0.5f, 0.5f, 42f, 42f);
            string[] levelNames = { "Engagement", "Attention", "Participation" };
            for (int i = 0; i < 3; i++)
            {
                var box = New(levelNames[i] + "Level", sec);
                VLayout(box.gameObject, 0, 0f).padding = new RectOffset(0, 0, 0, 0);
                FieldLabel(box, levelNames[i]);
                TextMeshProUGUI val;
                s.levelSliders[i] = MakeSlider(box, levelNames[i] + "Slider", 0, 100, out val);
                s.levelValues[i] = val;
                s.levelBoxes[i] = box;
            }
            s.alertText = Secondary(sec, "Alert", "Everyone above 60% attention");
            s.alertText.GetComponent<ThemedText>().applyColor = false;
            s.alertText.enableWordWrapping = true;
            s.alertText.overflowMode = TextOverflowModes.Overflow;
            s.moodText = Secondary(sec, "Mood", "Class mood: Positive");
            LE(s.moodText, TextBoxH(TextRole.Caption));
            GameObject newChip;
            sec = SectionWithChip(ml, "Questions (0)", "New", ColorRole.Primary, out s.questionsTitle, out newChip);
            s.simulatedOnly.Add(sec.gameObject);
            s.questionsNew = newChip;
            s.questionTemplate = LineRow(RowsBox(sec, "Questions", 12f), "QuestionRow", true);
            s.noQuestions = Secondary(sec, "None", "No questions yet");
            LE(s.noQuestions, TextBoxH(TextRole.Caption));
            sec = Section(ml, "Live comments");
            s.simulatedOnly.Add(sec.gameObject);
            s.commentsTitle = sec.Find("Title").GetComponent<TextMeshProUGUI>();
            s.commentTemplate = LineRow(RowsBox(sec, "Comments", 12f), "CommentRow", false);
            sec = Section(ml, "Sound control");
            var muteRow = New("MuteAllRow", sec);
            LE(muteRow, 90f);
            var ml2 = Txt(muteRow, "Label", "Mute all students", TextRole.Caption, ColorRole.Text);
            Stretch(ml2.rectTransform, 0, 0, 160, 0);
            s.muteAllSwitch = Switch(muteRow, "MuteAll");
            Place((RectTransform)s.muteAllSwitch.transform, 1f, 0.5f, 120f, 68f);
            TextMeshProUGUI vl;
            var st = SectionTitle(ml, "StudentsTitle", "Students");
            s.simulatedOnly.Add(st.gameObject);
            LE(st, TextBoxH(TextRole.Body));
            var metricRows = RowsBox(ml, "MetricRows");
            s.simulatedOnly.Add(metricRows.gameObject);
            s.metricRowTemplate = StudentRow(metricRows, "MetricRow", true, true, true);

            // ---- right: class overview + compact student rows
            var sl = ScrollList(s.transform, "StudentsPanel", H, LiveSessionScreen.BarH);
            sl.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.studentsPanel = (RectTransform)sl.parent.parent;
            sec = Section(sl, "Class overview");
            s.overviewText = Txt(sec, "Overview", "Subject", TextRole.Caption, ColorRole.Text);
            s.overviewText.enableWordWrapping = true;
            s.overviewText.overflowMode = TextOverflowModes.Overflow;
            s.overviewText.richText = true;
            var st2 = SectionTitle(sl, "StudentsTitle", "Students");
            LE(st2, TextBoxH(TextRole.Body));
            s.studentRowTemplate = CompactStudentRow(RowsBox(sl, "StudentRows", 16f));

            // ---- student view (no admin controls): own Quest, focus, sound, Leave
            var pl = ScrollList(s.transform, "StudentPanel", H, 0f);
            pl.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            s.studentPanel = (RectTransform)pl.parent.parent;
            sec = Section(pl, "My Quest");
            s.myQuestText = Shrink(RowTitle(sec, "Quest", "Quest-A1B2  •  86% battery"));
            LE(s.myQuestText, TextBoxH(TextRole.Body));
            s.myStatusText = Secondary(sec, "Status", "In class");
            LE(s.myStatusText, TextBoxH(TextRole.Caption));
            sec = Section(pl, "My focus");
            s.simulatedOnly.Add(sec.gameObject);
            s.myAttention = Meter(sec, "Attention", ColorRole.Primary);
            s.myEngagement = Meter(sec, "Engagement", ColorRole.Success);
            sec = Section(pl, "Sound");
            FieldLabel(sec, "Volume");
            s.myVolume = MakeSlider(sec, "MyVolume", 0, 100, out vl);
            s.myVolumeLabel = vl;
            var mr = New("MuteRow", sec);
            LE(mr, 90f);
            var mlb = Txt(mr, "Label", "Mute my Quest", TextRole.Caption, ColorRole.Text);
            Stretch(mlb.rectTransform, 0, 0, 160, 0);
            s.myMuteSwitch = Switch(mr, "MyMute");
            Place((RectTransform)s.myMuteSwitch.transform, 1f, 0.5f, 120f, 68f);
            s.leaveButton = Btn(pl, "Leave", BtnKind.Destructive, "close", "Leave session");

            // ---- centre: the Quest screen (after the panels: on top of them in full screen)
            var qc = Shape(s.transform, "QuestScreen", ColorRole.Text, ShapeKind.Card);
            qc.GetComponent<ThemedColor>().enabled = false;
            qc.color = Color.white;
            TopBand(qc.rectTransform, H, 600f, Margin);
            s.questCard = qc.rectTransform;
            s.questGradient = Gradient(qc, true);
            s.questGradient.useTheme = false;
            s.questIcon = Icon(qc.transform, "Icon", "vr_headset", ColorRole.OnPrimary, 140f);
            s.questIcon.GetComponent<ThemedColor>().enabled = false;
            s.questIcon.color = new Color(1f, 1f, 1f, 0.9f);
            Place(s.questIcon.rectTransform, 0.5f, 0.5f, 140f, 140f, 0f, 20f);
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
            s.fullscreenButton = CardIcon(qc.transform, "Fullscreen", "open_in_full", -20f, out s.fullscreenIcon);
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

            // ---- control bar under the Quest screen: volume, Start, Pause, End Class, Record, Instruction, Content
            var bar = New("ControlBar", s.transform);
            BottomBand(bar, 24f, BtnH, Margin);
            s.controlBar = bar;
            var vb = Shape(bar, "Volume", ColorRole.Card, ShapeKind.Card);
            vb.raycastTarget = false;
            vb.rectTransform.anchorMin = new Vector2(0f, 0f);
            vb.rectTransform.anchorMax = new Vector2(0f, 1f);
            vb.rectTransform.pivot = new Vector2(0f, 0.5f);
            vb.rectTransform.sizeDelta = new Vector2(360f, 0f);
            vb.rectTransform.anchoredPosition = Vector2.zero;
            s.volumeBox = vb.rectTransform;
            var vIcon = Icon(vb.transform, "Icon", "volume_up", ColorRole.Primary, 52f);
            Place(vIcon.rectTransform, 0f, 0.5f, 52f, 52f, 20f, 0f);
            var vsHolder = New("SliderBox", vb.transform);
            Stretch(vsHolder, 84f, 15f, 12f, 15f);
            VLayout(vsHolder.gameObject, 0, 0f).padding = new RectOffset(0, 0, 0, 0);
            TextMeshProUGUI bvl;
            s.barVolume = MakeSlider(vsHolder, "BarVolume", 0, 100, out bvl);
            s.volumeLabel = bvl;
            s.volumeLabel.gameObject.SetActive(false);   // the bar slider has no value label: full width
            ((RectTransform)s.barVolume.transform).offsetMax = Vector2.zero;
            var tiles = New("Tiles", bar);
            Stretch(tiles, 360f + 16f, 0f, 0f, 0f);
            HLayout(tiles.gameObject, 12f);
            s.startButton = ActionTile(tiles, "Start", "play_arrow", ColorRole.Success);
            s.startLabel = s.startButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            s.pauseSessionButton = ActionTile(tiles, "Pause", "pause", ColorRole.Primary);
            s.endButton = ActionTile(tiles, "End Class", "stop", ColorRole.Danger);
            s.recordButton = ActionTile(tiles, "Record", "circle", ColorRole.Danger);
            s.simulatedOnly.Add(s.recordButton.transform.parent.gameObject);   // only a timer today
            s.recordLabel = s.recordButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            s.recordCard = (Image)s.recordButton.targetGraphic;
            s.recordCard.GetComponent<ThemedColor>().enabled = false;   // the screen colours it (red while recording)
            s.instructionButton = ActionTile(tiles, "Instruction", "article", ColorRole.Primary);
            s.contentButton = ActionTile(tiles, "Content", "add", ColorRole.Primary);

            // ---- no live session
            var empty = New("Empty", s.transform);
            Stretch(empty, 0, 0, 0, H);
            s.emptyState = empty;
            var et = Txt(empty, "Text", "No live session.\nOpen a meeting and tap Start Session.", TextRole.Body, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            et.enableWordWrapping = true;
            TopBand(et.rectTransform, 120f, 140f);
            s.emptyButton = Btn(empty, "OpenMeetings", BtnKind.Primary, "view_agenda", "Open meetings");
            Place((RectTransform)s.emptyButton.transform, 0.5f, 1f, 600f, BtnH, 0f, -300f);
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
            s.engagementSection = sec.gameObject;
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
