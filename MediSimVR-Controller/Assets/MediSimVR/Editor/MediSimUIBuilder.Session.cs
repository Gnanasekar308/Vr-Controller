using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Session tab screens (part of MediSimUIBuilder, built from the design system in MediSimUIBuilder.Kit.cs).
    //   Session start (HOST / PLAYER), Host dashboard + text sheet, My Quest list, Player dashboard, Summary.
    public static partial class MediSimUIBuilder
    {
        // ===================================================================== start
        static AppScreen BuildSessionStart(Transform parent)
        {
            var s = MakeScreen<SessionStartScreen>(parent, "SessionStart", ScreenId.Session);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "Session", DemoConfig.SessionSubtitle, false, out title, out sub, out back);
            Image chip;
            s.modeText = ModeChip(s.transform, HeadH + 4f, out s.demoBadge, out chip);

            // HOST = the same primary ActionCard as Home's Control Mode, PLAYER = the secondary one
            float y = HeadH + 128f;
            s.hostButton = ActionCard(s.transform, "HostCard", y, true, "cast_connected", DemoConfig.HostCardTitle, DemoConfig.HostCardText);
            y += HeroH + CardGap;
            s.playerButton = ActionCard(s.transform, "PlayerCard", y, false, "vr_headset", DemoConfig.PlayerCardTitle, DemoConfig.PlayerCardText);
            y += HeroH + CardGap + 16f;

            // running session: white card, green LIVE indicator, duration, whole card tappable
            var act = Shape(s.transform, "ActiveCard", ColorRole.Card, ShapeKind.Card);
            TopBand(act.rectTransform, y, 168f, Margin);
            var actShadow = ShadowFor(act.rectTransform, CardShadow);
            s.activeButton = MakeButton(act.gameObject, act);
            var accent = Shape(act.transform, "Accent", DemoConfig.LiveRole, ShapeKind.Pill);
            accent.GetComponent<ThemedShape>().pillHeight = 8f;
            accent.raycastTarget = false;
            Place(accent.rectTransform, 0f, 0.5f, 8f, 104f, 18f, 0f);
            Image liveDot;
            TextMeshProUGUI liveText;
            var live = StatusChip(act.transform, "Live", DemoConfig.LiveRole, "LIVE", 150f, out liveDot, out liveText);
            Place(live.rectTransform, 0f, 1f, 150f, ChipH, CardPad + 16f, -24f);
            s.livePulse = liveDot.gameObject.AddComponent<SpeakingPulse>();
            s.livePulse.target = liveDot;
            s.activeText = RowTitle(act.transform, "Title", "Live host session");
            TopLeft(s.activeText.rectTransform, CardPad + 186f, 30f, 96f + 250f + 16f, 52f);   // ends before the duration
            s.activeSub = Secondary(act.transform, "Sub", DemoConfig.DashboardHint);
            TopLeft(s.activeSub.rectTransform, CardPad + 16f, 100f, 96f, 44f);
            s.activeTimeText = Txt(act.transform, "Time", "00:00:00", TextRole.Header, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.MidlineRight);
            Place(s.activeTimeText.rectTransform, 1f, 1f, 250f, TextBoxH(TextRole.Header), -96f, -21f);
            Chevron(act.transform);
            s.activeCard = act.gameObject;
            s.activeShadow = actShadow.gameObject;

            var un = BodyText(s.transform, "Unavailable", DemoConfig.BackendMissing, ColorRole.SubText);
            un.alignment = TextAlignmentOptions.Center;
            un.enableWordWrapping = true;
            un.overflowMode = TextOverflowModes.Overflow;
            TopBand(un.rectTransform, y + 16f, 220f, 70f);
            s.unavailable = un.gameObject;
            return s;
        }

        // ===================================================================== host
        static AppScreen BuildSessionHost(Transform parent)
        {
            var s = MakeScreen<SessionHostScreen>(parent, "SessionHost", ScreenId.SessionHost);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            var hdr = Header(s.transform, "Host", "Mode: Host", true, out title, out sub, out back);
            s.backButton = back;
            s.subtitleText = sub;
            s.endButton = HeaderAction(hdr, "End", BtnKind.Destructive, "End session", 290f);

            // live status: recording on the PC (red dot) + session clock
            var rec = Shape(s.transform, "RecordingChip", ColorRole.Card, ShapeKind.Pill);
            rec.GetComponent<ThemedShape>().pillHeight = 80f;
            rec.raycastTarget = false;
            Place(rec.rectTransform, 0f, 1f, 640f, 80f, Margin, -(HeadH + 4f));
            ShadowFor(rec.rectTransform, CardShadow);
            s.recordingDot = Img(rec.transform, "Dot", circle, DemoConfig.RecordingRole);
            s.recordingDot.GetComponent<ThemedColor>().enabled = false;
            s.recordingDot.raycastTarget = false;
            Place(s.recordingDot.rectTransform, 0f, 0.5f, 24f, 24f, 32f, 0f);
            s.recordingText = SmallLabel(rec.transform, "Text", "Recording on PC 00:12:34", ColorRole.Text);
            Stretch(s.recordingText.rectTransform, 70, 0, 20, 0);
            var tim = Shape(s.transform, "TimerChip", ColorRole.Card, ShapeKind.Pill);
            tim.GetComponent<ThemedShape>().pillHeight = 80f;
            tim.raycastTarget = false;
            Place(tim.rectTransform, 1f, 1f, 336f, 80f, -Margin, -(HeadH + 4f));
            ShadowFor(tim.rectTransform, CardShadow);
            s.timerText = SmallLabel(tim.transform, "Text", "Session 00:00:00");
            s.timerText.alignment = TextAlignmentOptions.Center;
            Stretch(s.timerText.rectTransform, 12, 0, 12, 0);

            VoiceBar(s.transform, HeadH + 100f, out s.voiceSwitch, out s.muteMeChip, out s.muteMeButton, out s.hostMic, out s.speakingText);

            // select all + count
            var selRow = New("SelectRow", s.transform);
            TopBand(selRow, HeadH + 248f, SmallBtnH, Margin);
            TextMeshProUGUI saLabel;
            s.selectAllButton = Btn(selRow, "SelectAll", BtnKind.Tertiary, null, "Select all", out saLabel);
            Place((RectTransform)s.selectAllButton.transform, 0f, 0.5f, 270f, SmallBtnH, 0f, 0f);
            s.selectAllLabel = saLabel;
            s.selectedText = Secondary(selRow, "Count", "");
            s.selectedText.alignment = TextAlignmentOptions.MidlineRight;
            Stretch(s.selectedText.rectTransform, 290, 0, 0, 0);

            // Quest cards + live timeline (one scroll list, two groups)
            var list = ScrollList(s.transform, "List", HeadH + 348f, 278f);
            list.GetComponent<VerticalLayoutGroup>().spacing = 16;
            var cardsBox = New("Cards", list);
            VLayout(cardsBox.gameObject, 0, 16);
            s.cardTemplate = QuestCard(cardsBox, true);
            var tlTitle = SectionTitle(list, "TimelineTitle", "Live session timeline");
            LE(tlTitle, 72);
            var tlBox = New("Timeline", list);
            VLayout(tlBox.gameObject, 0, 8);
            s.timelineTemplate = TimelineRow(tlBox);
            s.timelineEmpty = Secondary(list, "TimelineEmpty", "No events yet");
            LE(s.timelineEmpty, 60);

            // session actions: 2 x 4 tiles with the semantic colours (Play green, Stop red, others blue)
            var grid = New("Actions", s.transform);
            BottomBand(grid, 24f, 236f, Margin);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(241f, 110f);
            gl.spacing = new Vector2(12f, 16f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 4;
            gl.childAlignment = TextAnchor.MiddleCenter;
            s.playButton = ActionTile(grid, "Play", "play_arrow", ColorRole.Success);
            s.pauseButton = ActionTile(grid, "Pause", "pause");
            s.resumeButton = ActionTile(grid, "Resume", "replay");
            s.stopButton = ActionTile(grid, "Stop", "stop", ColorRole.Danger);
            s.sendButton = ActionTile(grid, "Send", "send");
            s.textButton = ActionTile(grid, "Text", "article");
            s.muteButton = ActionTile(grid, "Mute", "volume_off", ColorRole.SubText);
            s.sceneButton = ActionTile(grid, "Scene", "view_in_ar");

            // text sheet: quick messages, chat, input
            GameObject root;
            Button close;
            TextMeshProUGUI st;
            var card = Sheet(s.transform, "TextSheet", "Text to Quests", 1420f, out root, out close, out st);
            s.textSheet = root;
            s.textClose = close;
            s.textToLine = SmallLabel(card, "To", "To: ");
            TopBand(s.textToLine.rectTransform, 104f, 44f, 40f);
            var quick = New("Quick", card);
            TopBand(quick, 160f, 184f, 40f);
            var qg = quick.gameObject.AddComponent<GridLayoutGroup>();
            qg.cellSize = new Vector2(494f, SmallBtnH);
            qg.spacing = new Vector2(16f, 16f);
            qg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            qg.constraintCount = 2;
            for (int i = 0; i < 4; i++)
            {
                TextMeshProUGUI ql;
                s.quickButtons.Add(Btn(quick, "Quick" + i, BtnKind.Tertiary, null,
                    i < DemoConfig.QuickMessages.Length ? DemoConfig.QuickMessages[i] : "", out ql));
                s.quickLabels.Add(ql);
            }
            var chat = ScrollList(card, "Chat", 370f, 160f, false);
            chat.GetComponent<VerticalLayoutGroup>().spacing = 14;
            s.chatScroll = chat.GetComponentInParent<ScrollRect>();
            s.chatTemplate = ChatBubble(chat);
            s.chatEmpty = Secondary(card, "ChatEmpty", "No messages yet: tap a quick message or type one");
            s.chatEmpty.alignment = TextAlignmentOptions.Center;
            TopBand(s.chatEmpty.rectTransform, 410f, 80f, 40f);
            var inRow = New("InputRow", card);
            BottomBand(inRow, 30f, BtnH, 32f);
            s.textInput = Input(inRow, "Input", "Type a message", BtnH);
            Stretch((RectTransform)s.textInput.transform, 0, 0, 236, 0);
            TextMeshProUGUI sl;
            s.sendTextButton = Btn(inRow, "SendText", BtnKind.Primary, "send", "Send", out sl);
            Place((RectTransform)s.sendTextButton.transform, 1f, 0.5f, 216f, BtnH, 0f, 0f);
            return s;
        }

        // ===================================================================== player
        static AppScreen BuildSessionPlayerPick(Transform parent)
        {
            var s = MakeScreen<SessionPlayerPickScreen>(parent, "SessionPlayerPick", ScreenId.SessionPlayerPick);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "My Quest", "Tap your online Quest to play on it", true, out title, out sub, out back);
            s.backButton = back;
            s.hintText = sub;
            s.list = DeviceList(s.transform, HeadH + 4f, 20f);   // same list as the Devices tab
            return s;
        }

        static AppScreen BuildSessionPlayer(Transform parent)
        {
            var s = MakeScreen<SessionPlayerScreen>(parent, "SessionPlayer", ScreenId.SessionPlayer);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            var hdr = Header(s.transform, "Player", "Mode: Player", true, out title, out sub, out back);
            s.backButton = back;
            s.subtitleText = sub;
            s.leaveButton = HeaderAction(hdr, "Leave", BtnKind.Destructive, "Leave", 210f);

            var card = Shape(s.transform, "QuestStatus", ColorRole.Card, ShapeKind.Card);
            TopBand(card.rectTransform, HeadH + 4f, 288f, Margin);
            ShadowFor(card.rectTransform, CardShadow);
            var tile = Img(card.transform, "IconBg", circle, ColorRole.PrimarySoft);
            tile.raycastTarget = false;
            Place(tile.rectTransform, 0f, 1f, 128f, 128f, CardPad + 4f, -36f);
            var ti = Icon(tile.transform, "Icon", "vr_headset", ColorRole.Primary, 72f);
            Place(ti.rectTransform, 0.5f, 0.5f, 72f, 72f);
            s.questName = CardTitle(card.transform, "Name", "Quest");
            TopLeft(s.questName.rectTransform, 200f, 36f, 150f, 72f);
            s.statusDot = Img(card.transform, "Dot", circle, ColorRole.Success);
            s.statusDot.GetComponent<ThemedColor>().enabled = false;
            s.statusDot.raycastTarget = false;
            Place(s.statusDot.rectTransform, 0f, 1f, 20f, 20f, 204f, -132f);
            s.statusText = Secondary(card.transform, "Status", "");
            TopLeft(s.statusText.rectTransform, 238f, 120f, 40f, 46f);
            s.contentText = RowTitle(card.transform, "Content", "");
            TopLeft(s.contentText.rectTransform, CardPad + 4f, 200f, 40f, 56f);
            var micBg = Img(card.transform, "MicBg", circle, ColorRole.PrimarySoft);
            micBg.raycastTarget = false;
            Place(micBg.rectTransform, 1f, 1f, 92f, 92f, -CardPad, -36f);
            var mic = Icon(micBg.transform, "Mic", "volume_up", ColorRole.SubText, 52f);
            mic.GetComponent<ThemedColor>().enabled = false;
            Place(mic.rectTransform, 0.5f, 0.5f, 52f, 52f);
            s.questMic = micBg.gameObject.AddComponent<SpeakingPulse>();
            s.questMic.target = mic;

            VoiceBar(s.transform, HeadH + 316f, out s.voiceSwitch, out s.muteMeChip, out s.muteMeButton, out s.myMic, out s.speakingText);

            var grid = New("Controls", s.transform);
            TopBand(grid, HeadH + 472f, 236f, Margin);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(494f, 110f);
            gl.spacing = new Vector2(12f, 16f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            s.playButton = ActionTile(grid, "Play", "play_arrow", ColorRole.Success);
            s.pauseButton = ActionTile(grid, "Pause", "pause");
            s.stopButton = ActionTile(grid, "Stop", "stop", ColorRole.Danger);
            s.muteButton = ActionTile(grid, "Mute", "volume_off", ColorRole.SubText);
            s.muteLabel = s.muteButton.transform.Find("Label").GetComponent<TMP_Text>();

            s.sceneButton = Btn(s.transform, "OpenScene", BtnKind.Primary, "view_in_ar", "Open in Scene");
            TopBand((RectTransform)s.sceneButton.transform, HeadH + 740f, BtnH, Margin);
            ShadowFor((RectTransform)s.sceneButton.transform, AccentShadow, ColorRole.Primary);
            return s;
        }

        // ===================================================================== summary
        static AppScreen BuildSessionSummary(Transform parent)
        {
            var s = MakeScreen<SessionSummaryScreen>(parent, "SessionSummary", ScreenId.SessionSummary);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "Session ended", "Summary of this session", false, out title, out sub, out back);
            var list = ScrollList(s.transform, "List", HeadH + 4f, 2f * BtnH + 48f + 2f * CardGap);
            var card = Panel(list, "SummaryCard", 1060f);
            Label(card, "Duration", 36f);
            s.durationText = Txt(card, "DurationValue", "--:--:--", TextRole.Title, ColorRole.Primary, TextWeight.Heading);
            TopBand(s.durationText.rectTransform, 86f, 90f, 48f);
            Label(card, "Devices", 196f);
            s.devicesText = RowTitle(card, "DevicesValue", "");
            TopBand(s.devicesText.rectTransform, 244f, 60f, 48f);
            Label(card, "Events", 326f);
            s.countsText = BodyText(card, "Counts", "");
            s.countsText.enableWordWrapping = true;
            s.countsText.overflowMode = TextOverflowModes.Overflow;
            s.countsText.alignment = TextAlignmentOptions.TopLeft;
            TopBand(s.countsText.rectTransform, 376f, 540f, 48f);
            s.savedText = Txt(card, "Saved", "", TextRole.Caption, ColorRole.Primary, TextWeight.SemiBold);
            s.savedText.enableWordWrapping = true;
            s.savedText.overflowMode = TextOverflowModes.Overflow;
            TopBand(s.savedText.rectTransform, 930f, 110f, 48f);

            // New session = Primary, Home = Secondary
            s.newButton = Btn(s.transform, "NewSession", BtnKind.Primary, "replay", "New session");
            BottomBand((RectTransform)s.newButton.transform, 48f + BtnH + CardGap, BtnH, Margin);
            ShadowFor((RectTransform)s.newButton.transform, AccentShadow, ColorRole.Primary);
            s.homeButton = Btn(s.transform, "Home", BtnKind.Secondary, "home", "Home");
            BottomBand((RectTransform)s.homeButton.transform, 48f, BtnH, Margin);
            return s;
        }

        // ===================================================================== helpers

        static void Label(Transform card, string text, float y)
        {
            var t = SmallLabel(card, text + "Label", text.ToUpperInvariant());
            TopBand(t.rectTransform, y, 48f, 48f);
        }

        // left / top distance + right inset + height, anchored to the top-left of the parent
        static void TopLeft(RectTransform rt, float left, float top, float right, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(left, -top - h);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // "Mode: ..." pill with a DEMO badge on the right; returns the text, the badge and the pill
        static TextMeshProUGUI ModeChip(Transform parent, float y, out GameObject demoBadge, out Image chip)
        {
            chip = Shape(parent, "ModeChip", ColorRole.Card, ShapeKind.Pill);
            chip.GetComponent<ThemedShape>().pillHeight = 88f;
            chip.raycastTarget = false;
            TopBand(chip.rectTransform, y, 88f, Margin);
            ShadowFor(chip.rectTransform, CardShadow);
            var dot = Img(chip.transform, "Dot", circle, ColorRole.Primary);
            dot.raycastTarget = false;
            Place(dot.rectTransform, 0f, 0.5f, 24f, 24f, 36f, 0f);
            var t = Txt(chip.transform, "Mode", "Mode: " + DemoConfig.ModeNone, TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(t.rectTransform, 80, 0, 180, 0);
            var badge = Shape(chip.transform, "Demo", ColorRole.Warning, ShapeKind.Pill);
            badge.GetComponent<ThemedShape>().pillHeight = 52f;
            badge.raycastTarget = false;
            Place(badge.rectTransform, 1f, 0.5f, 136f, 52f, -20f, 0f);
            var bt = Txt(badge.transform, "Label", DemoConfig.DemoBadge, TextRole.Small, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(bt.rectTransform);
            demoBadge = badge.gameObject;
            return t;
        }

        // live voice switch, mute me, who is speaking
        static void VoiceBar(Transform screen, float y, out ToggleSwitch sw, out ChipToggle muteMe, out Button muteMeButton,
                             out SpeakingPulse mic, out TMP_Text speaking)
        {
            var bar = Shape(screen, "VoiceBar", ColorRole.Card, ShapeKind.Card);
            TopBand(bar.rectTransform, y, 128f, Margin);
            ShadowFor(bar.rectTransform, CardShadow);
            var micBg = Img(bar.transform, "MicBg", circle, ColorRole.PrimarySoft);
            micBg.raycastTarget = false;
            Place(micBg.rectTransform, 0f, 0.5f, 88f, 88f, CardPad - 8f, 0f);
            var ic = Icon(micBg.transform, "Mic", "volume_up", ColorRole.SubText, 50f);
            ic.GetComponent<ThemedColor>().enabled = false;
            Place(ic.rectTransform, 0.5f, 0.5f, 50f, 50f);
            mic = micBg.gameObject.AddComponent<SpeakingPulse>();
            mic.target = ic;
            var vt = RowTitle(bar.transform, "Title", "Live voice");
            vt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            vt.rectTransform.anchorMax = new Vector2(1f, 1f);
            vt.rectTransform.offsetMin = new Vector2(132f, -4f);
            vt.rectTransform.offsetMax = new Vector2(-360f, -10f);
            speaking = Secondary(bar.transform, "Speaking", "Nobody is speaking");
            speaking.rectTransform.anchorMin = new Vector2(0f, 0f);
            speaking.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            speaking.rectTransform.offsetMin = new Vector2(132f, 10f);
            speaking.rectTransform.offsetMax = new Vector2(-360f, 4f);
            sw = Switch(bar.transform, "VoiceSwitch");
            Place((RectTransform)sw.transform, 1f, 0.5f, 120f, 68f, -228f, 0f);
            muteMe = Chip(bar.transform, "MuteMe", "Mute me", 190f, 68f, out muteMeButton);
            Place(muteMe.background.rectTransform, 1f, 0.5f, 190f, 68f, -22f, 0f);
        }

        // one Quest (Session dashboard); withCheck = host selection box
        static QuestCardView QuestCard(Transform list, bool withCheck)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, "QuestCard", 176f, out card, out btn, out container);
            var v = container.AddComponent<QuestCardView>();
            v.button = btn;
            v.boxFilled = round;
            v.boxOutline = outline;
            v.group = card.gameObject.AddComponent<CanvasGroup>();
            v.checkBox = Img(rt, "CheckBox", round, ColorRole.Primary);
            v.checkBox.type = Image.Type.Sliced;
            v.checkBox.pixelsPerUnitMultiplier = 64f / 12f;
            v.checkBox.GetComponent<ThemedColor>().enabled = false;
            v.checkBox.raycastTarget = false;
            Place(v.checkBox.rectTransform, 0f, 0.5f, 56f, 56f, CardPad, 0f);
            v.checkMark = Icon(v.checkBox.transform, "Tick", "check", ColorRole.OnPrimary, 42f);
            Place(v.checkMark.rectTransform, 0.5f, 0.5f, 42f, 42f);
            float x0 = withCheck ? CardPad + 80f : CardPad;
            var tile = Img(rt, "IconBg", circle, ColorRole.PrimarySoft);
            tile.raycastTarget = false;
            Place(tile.rectTransform, 0f, 0.5f, 96f, 96f, x0, 0f);
            var ti = Icon(tile.transform, "Icon", "vr_headset", ColorRole.Primary, 56f);
            Place(ti.rectTransform, 0.5f, 0.5f, 56f, 56f);
            float x1 = x0 + 120f;
            v.nameText = CardTitle(rt, "Name", "Quest");
            TopLeft(v.nameText.rectTransform, x1, 8f, 136f, TextBoxH(TextRole.Header));   // taller than one line also at text size L
            v.statusDot = Img(rt, "Dot", circle, ColorRole.Success);
            v.statusDot.GetComponent<ThemedColor>().enabled = false;
            v.statusDot.raycastTarget = false;
            Place(v.statusDot.rectTransform, 0f, 1f, 20f, 20f, x1 + 4f, -93f);
            v.statusText = Secondary(rt, "Status", "Online");
            TopLeft(v.statusText.rectTransform, x1 + 34f, 80f, 136f, TextBoxH(TextRole.Caption));
            v.contentText = Txt(rt, "Content", "", TextRole.Caption, ColorRole.Text);
            TopLeft(v.contentText.rectTransform, x1, 126f, 136f, TextBoxH(TextRole.Caption));
            var micBg = Img(rt, "MicBg", circle, ColorRole.PrimarySoft);
            micBg.raycastTarget = false;
            Place(micBg.rectTransform, 1f, 0.5f, 84f, 84f, -CardPad + 4f, 16f);
            var mic = Icon(micBg.transform, "Mic", "volume_up", ColorRole.SubText, 48f);
            mic.GetComponent<ThemedColor>().enabled = false;
            Place(mic.rectTransform, 0.5f, 0.5f, 48f, 48f);
            v.mic = micBg.gameObject.AddComponent<SpeakingPulse>();
            v.mic.target = mic;
            var muted = Icon(rt, "Muted", "volume_off", DemoConfig.MutedRole, 40f);
            Place(muted.rectTransform, 1f, 0.5f, 40f, 40f, -CardPad - 18f, -48f);
            v.mutedIcon = muted.gameObject;
            container.SetActive(false);
            return v;
        }

        // one line of the session timeline
        static TimelineRowView TimelineRow(Transform box)
        {
            var r = New("TimelineRow", box);
            LE(r, 84);
            var v = r.gameObject.AddComponent<TimelineRowView>();
            var bg = Shape(r, "Bg", ColorRole.Card, ShapeKind.Field);
            bg.raycastTarget = false;
            Stretch(bg.rectTransform);
            v.icon = Icon(r, "Icon", "info", ColorRole.Primary, 44f);
            v.icon.GetComponent<ThemedColor>().enabled = false;
            Place(v.icon.rectTransform, 0f, 0.5f, 44f, 44f, 24f, 0f);
            v.timeText = Txt(r, "Time", "00:00:00", TextRole.Small, ColorRole.SubText);
            Place(v.timeText.rectTransform, 0f, 0.5f, 140f, 60f, 84f, 0f);
            v.messageText = Txt(r, "Message", "", TextRole.Caption, ColorRole.Text);
            v.messageText.richText = true;
            Stretch(v.messageText.rectTransform, 230, 0, 20, 0);
            // icon per SessionEventKind: Play, Pause, Stop, Content, Text, Voice, Join, Leave, Object, Mute, Info
            foreach (var n in new[] { "play_arrow", "pause", "stop", "send", "article", "volume_up", "check", "close", "view_in_ar", "volume_off", "info" })
                v.kindIcons.Add(IconS(n));
            r.gameObject.SetActive(false);
            return v;
        }

        // one chat message (bubble left or right)
        static ChatBubbleView ChatBubble(Transform list)
        {
            var r = New("ChatRow", list);
            var v = r.gameObject.AddComponent<ChatBubbleView>();
            v.layout = LE(r, 120);
            var bub = Shape(r, "Bubble", ColorRole.Field, ShapeKind.Card);
            bub.GetComponent<ThemedColor>().enabled = false;
            bub.raycastTarget = false;
            v.background = bub;
            v.bubble = bub.rectTransform;
            v.fromText = Txt(bub.transform, "From", "", TextRole.Small, ColorRole.SubText);
            v.fromText.GetComponent<ThemedText>().applyColor = false;
            TopBand(v.fromText.rectTransform, 12f, 40f, 32f);
            v.messageText = Txt(bub.transform, "Message", "", TextRole.Body, ColorRole.Text);
            v.messageText.GetComponent<ThemedText>().applyColor = false;
            v.messageText.enableWordWrapping = true;
            v.messageText.overflowMode = TextOverflowModes.Overflow;
            Stretch(v.messageText.rectTransform, 32, 18, 32, 54);
            r.gameObject.SetActive(false);
            return v;
        }
    }
}
