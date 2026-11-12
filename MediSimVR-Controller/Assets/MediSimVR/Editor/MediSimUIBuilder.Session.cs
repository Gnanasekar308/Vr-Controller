using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Session tab screens (part of MediSimUIBuilder: same helpers, same look as the other screens).
    //   Session start (HOST / PLAYER), Host dashboard + text sheet, Player pick, Player, Summary.
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

            s.hostButton = RoleCard(s.transform, "HostCard", HeadH + 140f, true, "cast_connected", DemoConfig.HostCardTitle, DemoConfig.HostCardText);
            s.playerButton = RoleCard(s.transform, "PlayerCard", HeadH + 480f, false, "vr_headset", DemoConfig.PlayerCardTitle, DemoConfig.PlayerCardText);

            var act = Shape(s.transform, "ActiveCard", ColorRole.PrimarySoft, ShapeKind.Card);
            TopBand(act.rectTransform, HeadH + 830f, 150f, 56f);
            s.activeButton = MakeButton(act.gameObject, act);
            var ai = Icon(act.transform, "Icon", "play_arrow", ColorRole.Primary, 64f);
            Place(ai.rectTransform, 0f, 0.5f, 64f, 64f, 40f, 0f);
            s.activeText = Txt(act.transform, "Text", "", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            s.activeText.enableWordWrapping = true;
            Stretch(s.activeText.rectTransform, 130, 10, 100, 10);
            Chevron(act.transform);
            s.activeCard = act.gameObject;

            var un = Txt(s.transform, "Unavailable", DemoConfig.BackendMissing, TextRole.Body, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            un.enableWordWrapping = true;
            un.overflowMode = TextOverflowModes.Overflow;
            TopBand(un.rectTransform, HeadH + 1010f, 220f, 70f);
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
            s.endButton = HeaderPill(hdr, "End", "End session", ColorRole.Danger, 300f);

            // recording + session clock
            var rec = Shape(s.transform, "RecordingChip", ColorRole.Card, ShapeKind.Pill);
            rec.GetComponent<ThemedShape>().pillHeight = 84f;
            rec.raycastTarget = false;
            Place(rec.rectTransform, 0f, 1f, 650f, 84f, 32f, -(HeadH + 4f));
            ShadowFor(rec.rectTransform, 0.08f);
            s.recordingDot = Img(rec.transform, "Dot", circle, ColorRole.Danger);
            s.recordingDot.GetComponent<ThemedColor>().enabled = false;
            s.recordingDot.raycastTarget = false;
            Place(s.recordingDot.rectTransform, 0f, 0.5f, 26f, 26f, 34f, 0f);
            s.recordingText = Txt(rec.transform, "Text", "Recording on PC 00:12:34", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.recordingText.rectTransform, 76, 0, 20, 0);
            var tim = Shape(s.transform, "TimerChip", ColorRole.Card, ShapeKind.Pill);
            tim.GetComponent<ThemedShape>().pillHeight = 84f;
            tim.raycastTarget = false;
            Place(tim.rectTransform, 1f, 1f, 330f, 84f, -32f, -(HeadH + 4f));
            ShadowFor(tim.rectTransform, 0.08f);
            s.timerText = Txt(tim.transform, "Text", "Session 00:00:00", TextRole.Caption, ColorRole.SubText, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(s.timerText.rectTransform, 12, 0, 12, 0);

            VoiceBar(s.transform, HeadH + 104f, out s.voiceSwitch, out s.muteMeChip, out s.muteMeButton, out s.hostMic, out s.speakingText);

            // select all + count
            var selRow = New("SelectRow", s.transform);
            TopBand(selRow, HeadH + 252f, 76f, 32f);
            var sa = Shape(selRow, "SelectAll", ColorRole.PrimarySoft, ShapeKind.Pill);
            sa.GetComponent<ThemedShape>().pillHeight = 76f;
            Place(sa.rectTransform, 0f, 0.5f, 250f, 76f, 0f, 0f);
            s.selectAllButton = MakeButton(sa.gameObject, sa);
            s.selectAllLabel = Txt(sa.transform, "Label", "Select all", TextRole.Caption, ColorRole.Primary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(s.selectAllLabel.rectTransform);
            s.selectedText = Txt(selRow, "Count", "", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.MidlineRight);
            Stretch(s.selectedText.rectTransform, 270, 0, 8, 0);

            // Quest cards + live timeline (one scroll list, two boxes)
            var list = ScrollList(s.transform, "List", HeadH + 340f, 272f);
            list.GetComponent<VerticalLayoutGroup>().spacing = 16;
            var cardsBox = New("Cards", list);
            VLayout(cardsBox.gameObject, 0, 16);
            s.cardTemplate = QuestCard(cardsBox, true);
            var tlTitle = Txt(list, "TimelineTitle", "Live session timeline", TextRole.Body, ColorRole.Text, TextWeight.Heading);
            LE(tlTitle, 72);
            var tlBox = New("Timeline", list);
            VLayout(tlBox.gameObject, 0, 8);
            s.timelineTemplate = TimelineRow(tlBox);
            s.timelineEmpty = Txt(list, "TimelineEmpty", "No events yet", TextRole.Caption, ColorRole.SubText);
            LE(s.timelineEmpty, 60);

            // actions
            var grid = New("Actions", s.transform);
            BottomBand(grid, 22f, 236f, 32f);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(241f, 110f);
            gl.spacing = new Vector2(12f, 16f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 4;
            gl.childAlignment = TextAnchor.MiddleCenter;
            s.playButton = ActionTile(grid, "Play", "play_arrow");
            s.pauseButton = ActionTile(grid, "Pause", "pause");
            s.resumeButton = ActionTile(grid, "Resume", "replay");
            s.stopButton = ActionTile(grid, "Stop", "stop");
            s.sendButton = ActionTile(grid, "Send", "send");
            s.textButton = ActionTile(grid, "Text", "article");
            s.muteButton = ActionTile(grid, "Mute", "volume_off");
            s.sceneButton = ActionTile(grid, "Scene", "view_in_ar");

            // text sheet: quick messages, chat, input
            GameObject root;
            Button close;
            TextMeshProUGUI st;
            var card = Sheet(s.transform, "TextSheet", "Text to Quests", 1420f, out root, out close, out st);
            s.textSheet = root;
            s.textClose = close;
            s.textToLine = Txt(card, "To", "To: ", TextRole.Small, ColorRole.SubText);
            TopBand(s.textToLine.rectTransform, 104f, 44f, 40f);
            var quick = New("Quick", card);
            TopBand(quick, 162f, 196f, 40f);
            var qg = quick.gameObject.AddComponent<GridLayoutGroup>();
            qg.cellSize = new Vector2(494f, 88f);
            qg.spacing = new Vector2(16f, 16f);
            qg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            qg.constraintCount = 2;
            for (int i = 0; i < 4; i++)
            {
                var q = Shape(quick, "Quick" + i, ColorRole.PrimarySoft, ShapeKind.Pill);
                q.GetComponent<ThemedShape>().pillHeight = 88f;
                s.quickButtons.Add(MakeButton(q.gameObject, q));
                var ql = Txt(q.transform, "Label", i < DemoConfig.QuickMessages.Length ? DemoConfig.QuickMessages[i] : "", TextRole.Caption, ColorRole.Primary, TextWeight.SemiBold, TextAlignmentOptions.Center);
                Stretch(ql.rectTransform, 16, 0, 16, 0);
                s.quickLabels.Add(ql);
            }
            var chat = ScrollList(card, "Chat", 380f, 160f, false);
            chat.GetComponent<VerticalLayoutGroup>().spacing = 14;
            s.chatScroll = chat.GetComponentInParent<ScrollRect>();
            s.chatTemplate = ChatBubble(chat);
            s.chatEmpty = Txt(card, "ChatEmpty", "No messages yet: tap a quick message or type one", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(s.chatEmpty.rectTransform, 420f, 80f, 40f);
            var inRow = New("InputRow", card);
            BottomBand(inRow, 30f, 110f, 32f);
            s.textInput = Input(inRow, "Input", "Type a message", 110f);
            Stretch((RectTransform)s.textInput.transform, 0, 0, 222, 0);
            TextMeshProUGUI sl;
            s.sendTextButton = GradientButton(inRow, "SendText", "send", "Send", out sl, true);
            s.sendTextButton.GetComponent<ThemedShape>().themeHeight = false;
            s.sendTextButton.GetComponent<ThemedShape>().pillHeight = 110f;
            Place((RectTransform)s.sendTextButton.transform, 1f, 0.5f, 206f, 110f, 0f, 0f);
            return s;
        }

        // ===================================================================== player
        static AppScreen BuildSessionPlayerPick(Transform parent)
        {
            var s = MakeScreen<SessionPlayerPickScreen>(parent, "SessionPlayerPick", ScreenId.SessionPlayerPick);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "Join as Player", "Pick your Quest", true, out title, out sub, out back);
            s.backButton = back;
            s.hintText = sub;
            var list = ScrollList(s.transform, "List", HeadH + 10f, 20f);
            s.cardTemplate = QuestCard(list, false);
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
            s.leaveButton = HeaderPill(hdr, "Leave", "Leave", ColorRole.Danger, 220f);

            var card = Shape(s.transform, "QuestStatus", ColorRole.Card, ShapeKind.Card);
            TopBand(card.rectTransform, HeadH + 10f, 300f, 32f);
            ShadowFor(card.rectTransform);
            var tile = Shape(card.transform, "Tile", ColorRole.PrimarySoft, ShapeKind.Field);
            tile.raycastTarget = false;
            Place(tile.rectTransform, 0f, 1f, 140f, 140f, 36f, -40f);
            var ti = Icon(tile.transform, "Icon", "vr_headset", ColorRole.Primary, 84f);
            Place(ti.rectTransform, 0.5f, 0.5f, 84f, 84f);
            s.questName = Txt(card.transform, "Name", "Quest", TextRole.Title, ColorRole.Text, TextWeight.Heading);
            TopLeft(s.questName.rectTransform, 210f, 36f, 150f, 80f);
            s.statusDot = Img(card.transform, "Dot", circle, ColorRole.Accent);
            s.statusDot.GetComponent<ThemedColor>().enabled = false;
            s.statusDot.raycastTarget = false;
            Place(s.statusDot.rectTransform, 0f, 1f, 24f, 24f, 214f, -138f);
            s.statusText = Txt(card.transform, "Status", "", TextRole.Caption, ColorRole.SubText);
            TopLeft(s.statusText.rectTransform, 252f, 126f, 40f, 48f);
            s.contentText = Txt(card.transform, "Content", "", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            TopLeft(s.contentText.rectTransform, 40f, 210f, 40f, 60f);
            var micBg = Img(card.transform, "MicBg", circle, ColorRole.PrimarySoft);
            micBg.raycastTarget = false;
            Place(micBg.rectTransform, 1f, 1f, 96f, 96f, -32f, -36f);
            var mic = Icon(micBg.transform, "Mic", "volume_up", ColorRole.SubText, 54f);
            mic.GetComponent<ThemedColor>().enabled = false;
            Place(mic.rectTransform, 0.5f, 0.5f, 54f, 54f);
            s.questMic = micBg.gameObject.AddComponent<SpeakingPulse>();
            s.questMic.target = mic;

            VoiceBar(s.transform, HeadH + 330f, out s.voiceSwitch, out s.muteMeChip, out s.muteMeButton, out s.myMic, out s.speakingText);

            var grid = New("Controls", s.transform);
            TopBand(grid, HeadH + 490f, 236f, 32f);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(494f, 110f);
            gl.spacing = new Vector2(12f, 16f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            s.playButton = ActionTile(grid, "Play", "play_arrow");
            s.pauseButton = ActionTile(grid, "Pause", "pause");
            s.stopButton = ActionTile(grid, "Stop", "stop");
            s.muteButton = ActionTile(grid, "Mute", "volume_off");
            s.muteLabel = s.muteButton.transform.Find("Label").GetComponent<TMP_Text>();

            TextMeshProUGUI lbl;
            s.sceneButton = GradientButton(s.transform, "OpenScene", "view_in_ar", "Open in Scene", out lbl, true);
            s.sceneButton.GetComponent<ThemedShape>().themeHeight = false;
            s.sceneButton.GetComponent<ThemedShape>().pillHeight = 124f;
            Place((RectTransform)s.sceneButton.transform, 0.5f, 1f, 760f, 124f, 0f, -(HeadH + 770f));
            ShadowFor((RectTransform)s.sceneButton.transform, 0.30f, ColorRole.Primary);
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
            var card = Shape(s.transform, "SummaryCard", ColorRole.Card, ShapeKind.Card);
            TopBand(card.rectTransform, HeadH + 10f, 1100f, 40f);
            ShadowFor(card.rectTransform);
            Label(card.transform, "Duration", 36f);
            s.durationText = Txt(card.transform, "DurationValue", "--:--:--", TextRole.Title, ColorRole.Primary, TextWeight.Heading);
            TopBand(s.durationText.rectTransform, 86f, 90f, 48f);
            Label(card.transform, "Devices", 196f);
            s.devicesText = Txt(card.transform, "DevicesValue", "", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            TopBand(s.devicesText.rectTransform, 244f, 60f, 48f);
            Label(card.transform, "Events", 326f);
            s.countsText = Txt(card.transform, "Counts", "", TextRole.Body, ColorRole.Text);
            s.countsText.enableWordWrapping = true;
            s.countsText.overflowMode = TextOverflowModes.Overflow;
            s.countsText.alignment = TextAlignmentOptions.TopLeft;
            TopBand(s.countsText.rectTransform, 376f, 540f, 48f);
            s.savedText = Txt(card.transform, "Saved", "", TextRole.Caption, ColorRole.Primary, TextWeight.SemiBold);
            s.savedText.enableWordWrapping = true;
            s.savedText.overflowMode = TextOverflowModes.Overflow;
            TopBand(s.savedText.rectTransform, 940f, 130f, 48f);

            TextMeshProUGUI lbl;
            s.newButton = GradientButton(s.transform, "NewSession", "replay", "New session", out lbl, true);
            s.newButton.GetComponent<ThemedShape>().themeHeight = false;
            s.newButton.GetComponent<ThemedShape>().pillHeight = 116f;
            Place((RectTransform)s.newButton.transform, 0.5f, 0f, 640f, 116f, 0f, 160f);
            ShadowFor((RectTransform)s.newButton.transform, 0.28f, ColorRole.Primary);
            var home = Shape(s.transform, "Home", ColorRole.Field, ShapeKind.Pill);
            home.GetComponent<ThemedShape>().pillHeight = 96f;
            Place(home.rectTransform, 0.5f, 0f, 640f, 96f, 0f, 40f);
            s.homeButton = MakeButton(home.gameObject, home);
            var ht = Txt(home.transform, "Label", "Home", TextRole.Button, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(ht.rectTransform);
            return s;
        }

        // ===================================================================== helpers

        static void Label(Transform card, string text, float y)
        {
            var t = Txt(card, text + "Label", text, TextRole.Caption, ColorRole.SubText, TextWeight.SemiBold);
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
            chip.GetComponent<ThemedShape>().pillHeight = 96f;
            chip.raycastTarget = false;
            TopBand(chip.rectTransform, y, 96f);
            ShadowFor(chip.rectTransform, 0.08f);
            var dot = Img(chip.transform, "Dot", circle, ColorRole.Primary);
            dot.raycastTarget = false;
            Place(dot.rectTransform, 0f, 0.5f, 28f, 28f, 40f, 0f);
            var t = Txt(chip.transform, "Mode", "Mode: " + DemoConfig.ModeNone, TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(t.rectTransform, 88, 0, 180, 0);
            var badge = Shape(chip.transform, "Demo", ColorRole.Warning, ShapeKind.Pill);
            badge.GetComponent<ThemedShape>().pillHeight = 56f;
            badge.raycastTarget = false;
            Place(badge.rectTransform, 1f, 0.5f, 140f, 56f, -24f, 0f);
            var bt = Txt(badge.transform, "Label", DemoConfig.DemoBadge, TextRole.Small, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(bt.rectTransform);
            demoBadge = badge.gameObject;
            return t;
        }

        // big HOST / PLAYER card
        static Button RoleCard(Transform parent, string name, float y, bool primary, string icon, string title, string text)
        {
            var card = Shape(parent, name, primary ? ColorRole.Primary : ColorRole.Card, ShapeKind.Card);
            if (primary) Gradient(card);
            TopBand(card.rectTransform, y, 300f, 56f);
            ShadowFor(card.rectTransform, primary ? 0.28f : 0.10f, primary ? ColorRole.Primary : ColorRole.Text);
            var b = MakeButton(card.gameObject, card);
            ColorRole fg = primary ? ColorRole.OnPrimary : ColorRole.Text;
            var bg = Img(card.transform, "IconBg", circle, primary ? ColorRole.OnPrimary : ColorRole.PrimarySoft, primary ? 0.18f : 1f);
            bg.raycastTarget = false;
            Place(bg.rectTransform, 0f, 0.5f, 150f, 150f, 50f, 0f);
            var ic = Icon(bg.transform, "Icon", icon, primary ? ColorRole.OnPrimary : ColorRole.Primary, 84f);
            Place(ic.rectTransform, 0.5f, 0.5f, 84f, 84f);
            var t = Txt(card.transform, "Title", title, TextRole.Title, fg, TextWeight.Heading);
            Stretch(t.rectTransform, 240, 150, 100, 46);
            var st = Txt(card.transform, "Text", text, TextRole.Caption, primary ? ColorRole.OnPrimary : ColorRole.SubText);
            st.enableWordWrapping = true;
            st.overflowMode = TextOverflowModes.Overflow;
            st.alignment = TextAlignmentOptions.TopLeft;
            Stretch(st.rectTransform, 240, 30, 100, 160);
            var ch = Icon(card.transform, "Chevron", "chevron_right", primary ? ColorRole.OnPrimary : ColorRole.SubText, 64f);
            Place(ch.rectTransform, 1f, 0.5f, 64f, 64f, -36f, 0f);
            return b;
        }

        // coloured pill in a header, right side, level with the back button
        static Button HeaderPill(RectTransform hdr, string name, string label, ColorRole role, float w)
        {
            var b = Shape(hdr, name, role, ShapeKind.Pill);
            b.GetComponent<ThemedShape>().pillHeight = 96f;
            Place(b.rectTransform, 1f, 1f, w, 96f, -40f, -70f);
            var t = Txt(b.transform, "Label", label, TextRole.Caption, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 12, 0, 12, 0);
            return MakeButton(b.gameObject, b);
        }

        // card tile with an icon above a label (action grids)
        static Button ActionTile(Transform grid, string label, string icon)
        {
            var bg = Shape(grid, label, ColorRole.Card, ShapeKind.Card);
            var b = MakeButton(bg.gameObject, bg);
            var ic = Icon(bg.transform, "Icon", icon, ColorRole.Primary, 52f);
            Place(ic.rectTransform, 0.5f, 1f, 52f, 52f, 0f, -14f);
            var t = Txt(bg.transform, "Label", label, TextRole.Small, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            BottomBand(t.rectTransform, 8f, 40f, 4f);
            return b;
        }

        // live voice switch, mute me, who is speaking
        static void VoiceBar(Transform screen, float y, out ToggleSwitch sw, out ChipToggle muteMe, out Button muteMeButton,
                             out SpeakingPulse mic, out TMP_Text speaking)
        {
            var bar = Shape(screen, "VoiceBar", ColorRole.Card, ShapeKind.Card);
            TopBand(bar.rectTransform, y, 132f, 32f);
            ShadowFor(bar.rectTransform, 0.08f);
            var micBg = Img(bar.transform, "MicBg", circle, ColorRole.PrimarySoft);
            micBg.raycastTarget = false;
            Place(micBg.rectTransform, 0f, 0.5f, 92f, 92f, 22f, 0f);
            var ic = Icon(micBg.transform, "Mic", "volume_up", ColorRole.SubText, 54f);
            ic.GetComponent<ThemedColor>().enabled = false;
            Place(ic.rectTransform, 0.5f, 0.5f, 54f, 54f);
            mic = micBg.gameObject.AddComponent<SpeakingPulse>();
            mic.target = ic;
            var vt = Txt(bar.transform, "Title", "Live voice", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            vt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            vt.rectTransform.anchorMax = new Vector2(1f, 1f);
            vt.rectTransform.offsetMin = new Vector2(132f, 0f);
            vt.rectTransform.offsetMax = new Vector2(-360f, -12f);
            speaking = Txt(bar.transform, "Speaking", "Nobody is speaking", TextRole.Caption, ColorRole.SubText);
            speaking.rectTransform.anchorMin = new Vector2(0f, 0f);
            speaking.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            speaking.rectTransform.offsetMin = new Vector2(132f, 12f);
            speaking.rectTransform.offsetMax = new Vector2(-360f, 0f);
            sw = Switch(bar.transform, "VoiceSwitch");
            Place((RectTransform)sw.transform, 1f, 0.5f, 120f, 68f, -228f, 0f);
            muteMe = Chip(bar.transform, "MuteMe", "Mute me", 190f, 68f, out muteMeButton);
            Place(muteMe.background.rectTransform, 1f, 0.5f, 190f, 68f, -22f, 0f);
        }

        // one Quest (Session screens); withCheck = host selection box
        static QuestCardView QuestCard(Transform list, bool withCheck)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, "QuestCard", 168f, out card, out btn, out container);
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
            Place(v.checkBox.rectTransform, 0f, 0.5f, 56f, 56f, 32f, 0f);
            v.checkMark = Icon(v.checkBox.transform, "Tick", "check", ColorRole.OnPrimary, 42f);
            Place(v.checkMark.rectTransform, 0.5f, 0.5f, 42f, 42f);
            float x0 = withCheck ? 112f : 32f;
            var tile = Shape(rt, "Tile", ColorRole.PrimarySoft, ShapeKind.Field);
            tile.raycastTarget = false;
            Place(tile.rectTransform, 0f, 0.5f, 100f, 100f, x0, 0f);
            var ti = Icon(tile.transform, "Icon", "vr_headset", ColorRole.Primary, 62f);
            Place(ti.rectTransform, 0.5f, 0.5f, 62f, 62f);
            float x1 = x0 + 124f;
            v.nameText = Txt(rt, "Name", "Quest", TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            TopLeft(v.nameText.rectTransform, x1, 16f, 136f, 60f);
            v.statusDot = Img(rt, "Dot", circle, ColorRole.Accent);
            v.statusDot.GetComponent<ThemedColor>().enabled = false;
            v.statusDot.raycastTarget = false;
            Place(v.statusDot.rectTransform, 0f, 1f, 20f, 20f, x1 + 4f, -94f);
            v.statusText = Txt(rt, "Status", "Online", TextRole.Caption, ColorRole.SubText);
            TopLeft(v.statusText.rectTransform, x1 + 34f, 82f, 136f, 44f);
            v.contentText = Txt(rt, "Content", "", TextRole.Caption, ColorRole.Text);
            TopLeft(v.contentText.rectTransform, x1, 122f, 136f, 40f);
            var micBg = Img(rt, "MicBg", circle, ColorRole.PrimarySoft);
            micBg.raycastTarget = false;
            Place(micBg.rectTransform, 1f, 0.5f, 84f, 84f, -30f, 16f);
            var mic = Icon(micBg.transform, "Mic", "volume_up", ColorRole.SubText, 48f);
            mic.GetComponent<ThemedColor>().enabled = false;
            Place(mic.rectTransform, 0.5f, 0.5f, 48f, 48f);
            v.mic = micBg.gameObject.AddComponent<SpeakingPulse>();
            v.mic.target = mic;
            var muted = Icon(rt, "Muted", "volume_off", ColorRole.Danger, 40f);
            Place(muted.rectTransform, 1f, 0.5f, 40f, 40f, -52f, -48f);
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
