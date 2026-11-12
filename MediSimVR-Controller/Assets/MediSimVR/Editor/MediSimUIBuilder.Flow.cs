using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Control Mode flow screens (part of MediSimUIBuilder, design system in MediSimUIBuilder.Kit.cs): the Control tab
    // (last step: Session > Devices > Send > Control), plus the components shared with the other tabs: Devices list
    // (DeviceListView + DeviceRowView), content preview card (MediaPreviewView), playback controls (PlaybackControlsView).
    // Content that can be taller than a small phone screen sits in scroll lists.
    public static partial class MediSimUIBuilder
    {
        // ===================================================================== Control tab
        static AppScreen BuildControl(Transform parent, PhoneVideoPlayer phone)
        {
            var s = MakeScreen<ControlScreen>(parent, "Control", ScreenId.Control);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "Control", "Play and guide the session", false, out title, out sub, out back);
            s.subtitleText = sub;
            s.phone = phone;

            var list = ScrollList(s.transform, "List", HeadH + 4f, 20f);
            list.GetComponent<VerticalLayoutGroup>().spacing = CardGap;

            // ---- target: "Playing on: ..." / "My Quest: ..." + Change
            var chip = Panel(list, "Target", 100f);
            var ti = Icon(chip, "Icon", "vr_headset", ColorRole.Primary, IconSize);
            Place(ti.rectTransform, 0f, 0.5f, IconSize, IconSize, CardPad, 0f);
            s.targetText = Txt(chip, "Target", "Playing on: Quest-A1B2", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.targetText.rectTransform, CardPad + IconSize + 20f, 0, 236, 0);
            s.changeTargetButton = Btn(chip, "Change", BtnKind.Tertiary, null, "Change", 72f);
            Place((RectTransform)s.changeTargetButton.transform, 1f, 0.5f, 196f, 72f, -16f, 0f);

            // ---- player: what plays on the Quests, also on this phone (or its thumbnail)
            var pc = Panel(list, "Player", 900f);
            var area = Shape(pc, "Screen", ColorRole.OnPrimary, ShapeKind.Field);
            area.GetComponent<ThemedColor>().enabled = false;
            area.color = Color.white;
            area.raycastTarget = false;
            TopLeft(area.rectTransform, 24f, 24f, 24f, 512f);
            area.gameObject.AddComponent<RectMask2D>();
            s.thumb = Gradient(area, true);
            s.thumb.useTheme = false;
            s.thumbIcon = Img(area.transform, "TypeIcon", IconS("movie"), ColorRole.OnPrimary);
            s.thumbIcon.GetComponent<ThemedColor>().enabled = false;
            s.thumbIcon.color = new Color(1f, 1f, 1f, 0.92f);
            s.thumbIcon.preserveAspect = true;
            s.thumbIcon.raycastTarget = false;
            Place(s.thumbIcon.rectTransform, 0.5f, 0.5f, 150f, 150f);
            var vid = New("Video", area.transform);
            Stretch(vid);
            s.videoImage = vid.gameObject.AddComponent<RawImage>();
            s.videoImage.raycastTarget = false;
            s.videoFitter = vid.gameObject.AddComponent<AspectRatioFitter>();
            s.videoFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;   // never stretched
            s.videoFitter.aspectRatio = 16f / 9f;
            vid.gameObject.SetActive(false);
            s.videoIcon = IconS("movie");
            s.audioIcon = IconS("music_note");
            s.imageIcon = IconS("image");
            s.textIcon = IconS("article");
            var badge = Shape(area.transform, "Badge", ColorRole.Text, ShapeKind.Pill);
            badge.GetComponent<ThemedColor>().enabled = false;
            badge.color = new Color(0f, 0f, 0f, 0.55f);
            badge.raycastTarget = false;
            Place(badge.rectTransform, 0f, 1f, 260f, 52f, 20f, -20f);
            s.previewBadge = SmallLabel(badge.transform, "Text", "ON THE QUEST", ColorRole.OnPrimary);
            s.previewBadge.alignment = TextAlignmentOptions.Center;
            Stretch(s.previewBadge.rectTransform);
            var stBg = Shape(area.transform, "StatusBg", ColorRole.Text, ShapeKind.Field);
            stBg.GetComponent<ThemedColor>().enabled = false;
            stBg.color = new Color(0f, 0f, 0f, 0.55f);
            stBg.raycastTarget = false;
            BottomBand(stBg.rectTransform, 16f, 100f, 16f);
            s.previewStatus = Txt(stBg.transform, "Text", "", TextRole.Small, ColorRole.OnPrimary);
            s.previewStatus.enableWordWrapping = true;
            s.previewStatus.alignment = TextAlignmentOptions.Center;
            Stretch(s.previewStatus.rectTransform, 20, 6, 20, 6);
            stBg.gameObject.SetActive(true);

            // name + info = a button back to Send (change the content)
            var fb = Img(pc, "File", null, ColorRole.Card, 0f);
            TopLeft(fb.rectTransform, 24f, 548f, 24f, 124f);
            s.fileButton = MakeButton(fb.gameObject, fb);
            s.fileName = CardTitle(fb.transform, "Name", "CPR_Training.mp4");
            TopLeft(s.fileName.rectTransform, 8f, 4f, 60f, TextBoxH(TextRole.Header));
            s.fileInfo = Secondary(fb.transform, "Info", "Video  •  268 MB");
            TopLeft(s.fileInfo.rectTransform, 8f, 74f, 60f, TextBoxH(TextRole.Caption));
            Chevron(fb.transform);

            // progress
            var prog = New("Progress", pc);
            TopLeft(prog, 24f, 684f, 24f, 56f);
            s.progressRow = prog.gameObject;
            s.elapsedText = SmallLabel(prog, "Elapsed", "00:00");
            Place(s.elapsedText.rectTransform, 0f, 0.5f, 120f, 50f, 4f, 0f);
            s.durationText = SmallLabel(prog, "Duration", "00:00");
            s.durationText.alignment = TextAlignmentOptions.MidlineRight;
            Place(s.durationText.rectTransform, 1f, 0.5f, 120f, 50f, -4f, 0f);
            var track = Shape(prog, "Track", ColorRole.Divider, ShapeKind.Pill);
            track.GetComponent<ThemedShape>().pillHeight = 16f;
            track.raycastTarget = false;
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.offsetMin = new Vector2(132f, -8f);
            track.rectTransform.offsetMax = new Vector2(-132f, 8f);
            var fill = Shape(track.transform, "Fill", ColorRole.Primary, ShapeKind.Pill);
            fill.GetComponent<ThemedShape>().pillHeight = 16f;
            fill.raycastTarget = false;
            Gradient(fill);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            s.progressFill = fill.rectTransform;

            // Scene link: opens the Scene (pauses the video until Play / Resume)
            s.sceneButton = Btn(pc, "ViewInScene", BtnKind.Secondary, "view_in_ar", "View in Scene");
            TopLeft((RectTransform)s.sceneButton.transform, 24f, 756f, 24f, BtnH);

            // ---- playback: status, Play / Pause / Resume / Stop, Mute
            s.controls = ControlsBig(list);

            // ---- audio: sound + mic of this phone (sender) and of the Quests (receiver)
            var au = Panel(list, "Audio", 330f);
            var at = SectionTitle(au, "Title", "Audio");
            TopLeft(at.rectTransform, CardPad, 24f, CardPad, 56f);
            s.phoneSoundSwitch = AudioRow(au, 92f, "volume_up", "This phone", "Sender", out s.phoneMicSwitch);
            s.questSoundSwitch = AudioRow(au, 208f, "vr_headset", "Quests", "Receivers", out s.questMicSwitch);

            // ---- live voice
            var vo = Panel(list, "Voice", 128f);
            var micBg = Img(vo, "MicBg", circle, ColorRole.PrimarySoft);
            micBg.raycastTarget = false;
            Place(micBg.rectTransform, 0f, 0.5f, 88f, 88f, CardPad - 8f, 0f);
            var mic = Icon(micBg.transform, "Mic", "volume_up", ColorRole.SubText, 50f);
            mic.GetComponent<ThemedColor>().enabled = false;
            Place(mic.rectTransform, 0.5f, 0.5f, 50f, 50f);
            s.voicePulse = micBg.gameObject.AddComponent<SpeakingPulse>();
            s.voicePulse.target = mic;
            var vt = RowTitle(vo, "Title", "Live voice");
            TopLeft(vt.rectTransform, 132f, 14f, 180f, TextBoxH(TextRole.Body));
            s.speakingText = Secondary(vo, "Speaking", "Nobody is speaking");
            TopLeft(s.speakingText.rectTransform, 132f, 66f, 180f, TextBoxH(TextRole.Caption));
            s.voiceSwitch = Switch(vo, "VoiceSwitch");
            Place((RectTransform)s.voiceSwitch.transform, 1f, 0.5f, 120f, 68f, -CardPad, 0f);

            // ---- lesson notes: the Quests read them aloud
            var no = Panel(list, "LessonNotes", 520f);
            var nt = SectionTitle(no, "Title", "Lesson notes");
            TopLeft(nt.rectTransform, CardPad, 24f, CardPad, 56f);
            var nh = Secondary(no, "Hint", "Type a note: the Quests read it aloud");
            TopLeft(nh.rectTransform, CardPad, 80f, CardPad, TextBoxH(TextRole.Caption));
            s.noteInput = Input(no, "Note", "e.g. Today: chest compressions, 30 per cycle", 220f);
            TopLeft((RectTransform)s.noteInput.transform, CardPad, 136f, CardPad, 220f);
            s.noteInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            s.noteInput.textComponent.enableWordWrapping = true;
            s.noteInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
            ((TMP_Text)s.noteInput.placeholder).alignment = TextAlignmentOptions.TopLeft;
            ((TMP_Text)s.noteInput.placeholder).enableWordWrapping = true;
            Stretch(s.noteInput.textViewport, 32, 20, 32, 20);
            s.notesInfo = Secondary(no, "Info", "0 note(s)");
            s.notesInfo.enableWordWrapping = true;
            TopLeft(s.notesInfo.rectTransform, CardPad, 384f, 470f, 92f);
            s.readNoteButton = Btn(no, "ReadAloud", BtnKind.Secondary, "volume_up", "Read aloud");
            Place((RectTransform)s.readNoteButton.transform, 1f, 1f, 420f, BtnH, -CardPad, -374f);

            // ---- important instructions: quick buttons, read aloud on the Quests
            var ins = Panel(list, "Instructions", 330f);
            var it = SectionTitle(ins, "Title", "Important instructions");
            TopLeft(it.rectTransform, CardPad, 24f, CardPad, 56f);
            var ih = Secondary(ins, "Hint", "Tap one: the Quests read it aloud");
            TopLeft(ih.rectTransform, CardPad, 80f, CardPad, TextBoxH(TextRole.Caption));
            var grid = New("Buttons", ins);
            TopLeft(grid, CardPad, 140f, CardPad, 2f * SmallBtnH + 16f);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(460f, SmallBtnH);
            g.spacing = new Vector2(16f, 16f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            for (int i = 0; i < 4; i++)
            {
                TextMeshProUGUI lbl;
                s.instructionButtons.Add(Btn(grid, "Instruction" + i, BtnKind.Tertiary, "warning",
                    i < DemoConfig.Instructions.Length ? DemoConfig.Instructions[i] : "", out lbl));
                s.instructionLabels.Add(lbl);
            }

            // ---- session record (JSON): where it is saved / sent
            var rc = Panel(list, "Record", 230f);
            var ri = Icon(rc, "Icon", "upload", ColorRole.Primary, IconSize);
            Place(ri.rectTransform, 0f, 1f, IconSize, IconSize, CardPad, -30f);
            var rt = RowTitle(rc, "Title", "Session record (JSON)");
            TopLeft(rt.rectTransform, CardPad + IconSize + 20f, 24f, CardPad, TextBoxH(TextRole.Body));
            s.recordText = Secondary(rc, "Text", "");
            s.recordText.enableWordWrapping = true;
            s.recordText.overflowMode = TextOverflowModes.Ellipsis;
            TopLeft(s.recordText.rectTransform, CardPad + IconSize + 20f, 78f, CardPad, 3f * TextBoxH(TextRole.Caption));
            return s;
        }

        // one Audio row: icon, name + role, Sound switch, Mic switch; returns the Sound switch
        static ToggleSwitch AudioRow(RectTransform card, float y, string icon, string name, string role, out ToggleSwitch micSwitch)
        {
            var row = New(name.Replace(" ", "") + "Row", card);
            TopLeft(row, CardPad, y, CardPad, 104f);
            var ic = Icon(row, "Icon", icon, ColorRole.Primary, 48f);
            Place(ic.rectTransform, 0f, 0.5f, 48f, 48f, 0f, 0f);
            var n = RowTitle(row, "Name", name);
            TopLeft(n.rectTransform, 68f, 4f, 520f, TextBoxH(TextRole.Body));
            var r = Secondary(row, "Role", role);
            TopLeft(r.rectTransform, 68f, 54f, 520f, TextBoxH(TextRole.Caption));
            var sl = SmallLabel(row, "SoundLabel", "Sound");
            sl.alignment = TextAlignmentOptions.MidlineRight;
            Place(sl.rectTransform, 1f, 0.5f, 100f, TextBoxH(TextRole.Small), -392f, 0f);
            var sound = Switch(row, "Sound");
            Place((RectTransform)sound.transform, 1f, 0.5f, 120f, 68f, -264f, 0f);
            var ml = SmallLabel(row, "MicLabel", "Mic");
            ml.alignment = TextAlignmentOptions.MidlineRight;
            Place(ml.rectTransform, 1f, 0.5f, 80f, TextBoxH(TextRole.Small), -128f, 0f);
            micSwitch = Switch(row, "Mic");
            Place((RectTransform)micSwitch.transform, 1f, 0.5f, 120f, 68f, 0f, 0f);
            return sound;
        }

        // ===================================================================== shared: Devices list
        // scroll list of DeviceRowView + empty text; the DeviceListView sits on the scroll view
        static DeviceListView DeviceList(Transform screen, float top, float bottom)
        {
            var content = ScrollList(screen, "List", top, bottom);
            var view = content.GetComponentInParent<ScrollRect>().gameObject.AddComponent<DeviceListView>();
            view.rowTemplate = DeviceRow(content);
            view.rowTemplate.gameObject.SetActive(false);
            view.emptyText = BodyText(screen, "Empty", "No devices", ColorRole.SubText);
            view.emptyText.alignment = TextAlignmentOptions.Center;
            TopBand(view.emptyText.rectTransform, top + 120f, 80f);
            view.emptyText.gameObject.SetActive(false);
            return view;
        }

        // select box | headset | name, battery + IP, what plays | status chip (Online / Offline / In Use)
        static DeviceRowView DeviceRow(Transform list)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, "DeviceRow", 190f, out card, out btn, out container);
            var v = container.AddComponent<DeviceRowView>();
            v.button = btn;
            v.boxFilled = round;
            v.boxOutline = outline;
            v.checkBox = Img(rt, "CheckBox", round, ColorRole.Primary);
            v.checkBox.type = Image.Type.Sliced;
            v.checkBox.pixelsPerUnitMultiplier = 64f / 12f;   // 12 px corner radius
            v.checkBox.GetComponent<ThemedColor>().enabled = false;
            v.checkBox.gameObject.AddComponent<CanvasGroup>();
            Place(v.checkBox.rectTransform, 0f, 0.5f, 56f, 56f, CardPad, 0f);
            v.checkMark = Icon(v.checkBox.transform, "Tick", "check", ColorRole.OnPrimary, 42f);
            Place(v.checkMark.rectTransform, 0.5f, 0.5f, 42f, 42f);
            if (headset != null)
            {
                var hs = Img(rt, "Headset", headset, ColorRole.OnPrimary);
                hs.preserveAspect = true;
                hs.raycastTarget = false;
                Place(hs.rectTransform, 0f, 0.5f, 140f, 88f, 108f, 0f);
            }
            Divider(rt, 270f, true);
            // text boxes are taller than one line even at text size L: TextMeshPro hides a line that does not fit (Ellipsis)
            v.nameText = CardTitle(rt, "Name", "Quest-01");
            TopLeft(v.nameText.rectTransform, 296f, 10f, 270f, 72f);
            v.infoText = Secondary(rt, "Info", "86% battery  •  192.168.1.25");
            TopLeft(v.infoText.rectTransform, 296f, 80f, CardPad, 46f);
            v.contentText = Txt(rt, "Content", "Ready", TextRole.Caption, ColorRole.SubText, TextWeight.SemiBold);
            v.contentText.GetComponent<ThemedText>().applyColor = false;
            TopLeft(v.contentText.rectTransform, 296f, 124f, CardPad, 46f);

            // status chip: DeviceRowView sets the colours (DeviceStatus roles)
            TextMeshProUGUI st;
            v.statusChip = StatusChip(rt, "Status", ColorRole.Success, "Online", 200f, out v.statusDot, out st);
            v.statusText = st;
            v.statusChip.GetComponent<ThemedColor>().enabled = false;
            v.statusDot.GetComponent<ThemedColor>().enabled = false;
            st.GetComponent<ThemedText>().applyColor = false;
            Place(v.statusChip.rectTransform, 1f, 1f, 200f, ChipH, -CardPad + 8f, -18f);
            return v;
        }

        // ===================================================================== shared: content preview card
        // shadow + card in 'holder' (its height decides the picture height: holder - 332): picture / type icon / text paper,
        // duration badge, name, info, Change / Edit (Tertiary) + Remove (Destructive); empty state with 'emptyHint'
        static MediaPreviewView PreviewCard(RectTransform holder, string emptyHint)
        {
            ShadowInside(holder, CardShadow);
            var card = Shape(holder, "Card", ColorRole.Card, ShapeKind.Card);
            Stretch(card.rectTransform);
            var v = holder.gameObject.AddComponent<MediaPreviewView>();
            v.videoIcon = IconS("movie");
            v.audioIcon = IconS("music_note");
            v.imageIcon = IconS("image");
            v.textIcon = IconS("article");

            // ---- filled
            var filled = New("Filled", card.transform);
            Stretch(filled);
            v.filledGroup = filled.gameObject;
            var prev = Shape(filled, "Preview", ColorRole.OnPrimary, ShapeKind.Field);
            prev.GetComponent<ThemedColor>().enabled = false;
            prev.color = Color.white;
            prev.raycastTarget = false;
            v.previewBackground = Gradient(prev, true);
            v.previewBackground.useTheme = false;
            prev.rectTransform.anchorMin = new Vector2(0, 0);
            prev.rectTransform.anchorMax = new Vector2(1, 1);
            prev.rectTransform.offsetMin = new Vector2(CardPad - 8f, 300);
            prev.rectTransform.offsetMax = new Vector2(-(CardPad - 8f), -(CardPad - 8f));
            prev.gameObject.AddComponent<RectMask2D>();
            v.previewImage = Img(prev.transform, "Picture", null, ColorRole.OnPrimary);
            v.previewImage.GetComponent<ThemedColor>().enabled = false;
            v.previewImage.color = Color.white;
            v.previewImage.preserveAspect = true;
            v.previewImage.raycastTarget = false;
            Stretch(v.previewImage.rectTransform);
            v.previewIcon = Img(prev.transform, "TypeIcon", IconS("movie"), ColorRole.OnPrimary);
            v.previewIcon.GetComponent<ThemedColor>().enabled = false;
            v.previewIcon.color = new Color(1f, 1f, 1f, 0.92f);
            v.previewIcon.preserveAspect = true;
            v.previewIcon.raycastTarget = false;
            Place(v.previewIcon.rectTransform, 0.5f, 0.5f, 150f, 150f);
            var paper = New("TextPaper", prev.transform);
            Stretch(paper, 28, 28, 28, 28);
            var paperBg = Shape(paper, "Bg", ColorRole.Card, ShapeKind.Field);
            Stretch(paperBg.rectTransform);
            paperBg.raycastTarget = false;
            v.previewText = BodyText(paper, "Text", "");
            v.previewText.alignment = TextAlignmentOptions.TopLeft;
            v.previewText.enableWordWrapping = true;
            v.previewText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(v.previewText.rectTransform, 32, 28, 32, 28);
            v.textPaper = paper.gameObject;
            paper.gameObject.SetActive(false);
            var badge = Shape(prev.transform, "DurationBadge", ColorRole.Text, ShapeKind.Pill, 0.75f);
            badge.GetComponent<ThemedColor>().enabled = false;
            badge.color = new Color(0f, 0f, 0f, 0.55f);
            badge.raycastTarget = false;
            Place(badge.rectTransform, 0f, 0f, 124f, 48f, 20f, 20f);
            v.badge = badge.gameObject;
            v.badgeText = SmallLabel(badge.transform, "Time", "05:30", ColorRole.OnPrimary);
            v.badgeText.alignment = TextAlignmentOptions.Center;
            Stretch(v.badgeText.rectTransform);

            v.nameText = CardTitle(filled, "Name", "Nature_01.mp4");
            BottomBand(v.nameText.rectTransform, 196f, 72f, CardPad + 8f);
            v.infoText = Secondary(filled, "Info", "Video  •  245 MB  •  05:30");
            BottomBand(v.infoText.rectTransform, 148f, 48f, CardPad + 8f);
            var actions = New("Actions", filled);
            BottomBand(actions, CardPad, SmallBtnH, CardPad);
            var ah = HLayout(actions.gameObject, 16);
            ah.childForceExpandWidth = false;
            ah.childAlignment = TextAnchor.MiddleLeft;
            v.changeButton = Btn(actions, "Change", BtnKind.Tertiary, "swap_horiz", "Change");
            LE(v.changeButton, SmallBtnH, 240f, 0);
            v.editButton = Btn(actions, "Edit", BtnKind.Tertiary, "edit", "Edit");
            LE(v.editButton, SmallBtnH, 190f, 0);
            v.removeButton = Btn(actions, "Remove", BtnKind.Destructive, "close", "Remove", SmallBtnH);
            LE(v.removeButton, SmallBtnH, 240f, 0);

            // ---- empty
            var empty = New("Empty", card.transform);
            Stretch(empty);
            v.emptyGroup = empty.gameObject;
            var ring = Img(empty, "Dashed", outline, ColorRole.Divider);
            ring.type = Image.Type.Sliced;
            ring.raycastTarget = false;
            var rsh = ring.gameObject.AddComponent<ThemedShape>();
            rsh.kind = ShapeKind.Card;
            Stretch(ring.rectTransform, CardPad, CardPad, CardPad, CardPad);
            var ec = Img(empty, "Circle", circle, ColorRole.PrimarySoft);
            ec.raycastTarget = false;
            Place(ec.rectTransform, 0.5f, 0.5f, 150f, 150f, 0f, 100f);
            var ei = Icon(ec.transform, "Icon", "add", ColorRole.Primary, 80f);
            Place(ei.rectTransform, 0.5f, 0.5f, 80f, 80f);
            var et = CardTitle(empty, "Title", "No file selected");
            et.alignment = TextAlignmentOptions.Center;
            Place(et.rectTransform, 0.5f, 0.5f, 800f, 72f, 0f, -36f);
            var es = Secondary(empty, "Hint", emptyHint);
            es.alignment = TextAlignmentOptions.Center;
            Place(es.rectTransform, 0.5f, 0.5f, 860f, 56f, 0f, -100f);
            empty.gameObject.SetActive(false);
            return v;
        }

        // ===================================================================== shared: playback controls
        // Control tab (in its scroll list): status card, big 2 x 2 tiles (Play green, Pause / Resume blue, Stop red),
        // neutral Mute row; image / text: Show on Quest + Hide
        static PlaybackControlsView ControlsBig(Transform list)
        {
            var root = New("Controls", list);
            VLayout(root.gameObject, 0, CardGap);
            var v = root.gameObject.AddComponent<PlaybackControlsView>();

            var status = Panel(root, "Status", 96f);
            var si = Icon(status, "Icon", "info", ColorRole.Primary, 44f);
            Place(si.rectTransform, 0f, 0.5f, 44f, 44f, CardPad, 0f);
            v.stateText = RowTitle(status, "State", "");
            Stretch(v.stateText.rectTransform, CardPad + 64f, 0, CardPad, 0);

            var media = New("MediaControls", root);
            VLayout(media.gameObject, 0, CardGap);
            v.mediaControls = media.gameObject;
            var grid = New("Buttons", media);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(488f, 212f);
            g.spacing = new Vector2(CardGap, CardGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;
            v.playButton = ActionTile(grid, "Play", "play_arrow", ColorRole.Success, true);
            v.pauseButton = ActionTile(grid, "Pause", "pause", ColorRole.Primary, true);
            v.resumeButton = ActionTile(grid, "Resume", "replay", ColorRole.Primary, true);
            v.stopButton = ActionTile(grid, "Stop", "stop", ColorRole.Danger, true);
            var mute = Panel(media, "MuteRow", 128f);
            v.muteSwitch = MuteRowContent(mute);

            var textC = New("TextControls", root);
            v.textControls = textC.gameObject;
            var tg = textC.gameObject.AddComponent<GridLayoutGroup>();
            tg.cellSize = new Vector2(488f, 212f);
            tg.spacing = new Vector2(CardGap, CardGap);
            tg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tg.constraintCount = 2;
            tg.childAlignment = TextAnchor.UpperCenter;
            v.showButton = ActionTile(textC, "Show on Quest", "visibility", ColorRole.Success, true);
            v.hideButton = ActionTile(textC, "Hide", "visibility_off", ColorRole.Primary, true);
            textC.gameObject.SetActive(false);
            return v;
        }

        // icon + "Mute" + switch inside a row card (neutral: Mute is not a destructive action)
        static ToggleSwitch MuteRowContent(Transform row)
        {
            var mi = Icon(row, "Icon", "volume_off", ColorRole.Text, IconSize);
            Place(mi.rectTransform, 0f, 0.5f, IconSize, IconSize, CardPad, 0f);
            var mt = RowTitle(row, "Label", "Mute");
            Stretch(mt.rectTransform, CardPad + IconSize + 24f, 0, 200, 0);
            var sw = Switch(row, "MuteSwitch");
            Place((RectTransform)sw.transform, 1f, 0.5f, 120f, 68f, -CardPad, 0f);
            return sw;
        }
    }
}
