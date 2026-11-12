using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Control Mode flow screens (part of MediSimUIBuilder): Quest list (host) and Play page, plus the components they
    // share with the tabs: Devices list (DeviceListView + DeviceRowView), content preview card (MediaPreviewView),
    // playback controls (PlaybackControlsView, big for the Control tab, compact for the Play page).
    public static partial class MediSimUIBuilder
    {
        // ===================================================================== Quest list (host)
        static AppScreen BuildQuestList(Transform parent)
        {
            var s = MakeScreen<QuestListScreen>(parent, "QuestList", ScreenId.QuestList);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "Select Quests", "Mode: Host", true, out title, out sub, out back);
            s.backButton = back;
            s.subtitleText = sub;

            var filterRow = New("FilterRow", s.transform);
            TopBand(filterRow, HeadH + 4f, 100f, 32f);
            VLayout(filterRow.gameObject, 0, 0);
            s.filterSeg = Seg(filterRow, "Filter", new[] { "All (4)", "Online (2)", "Offline (1)", "In Use (1)" }, 100f, true);

            var selRow = New("SelectRow", s.transform);
            TopBand(selRow, HeadH + 122f, 80f, 32f);
            var sa = Shape(selRow, "SelectAll", ColorRole.PrimarySoft, ShapeKind.Pill);
            sa.GetComponent<ThemedShape>().pillHeight = 80f;
            Place(sa.rectTransform, 0f, 0.5f, 340f, 80f, 0f, 0f);
            s.selectAllButton = MakeButton(sa.gameObject, sa);
            s.selectAllLabel = Txt(sa.transform, "Label", "Select all online", TextRole.Caption, ColorRole.Primary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(s.selectAllLabel.rectTransform, 12, 0, 12, 0);
            s.selectedText = Txt(selRow, "Count", "0 of 3 online selected", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.MidlineRight);
            Stretch(s.selectedText.rectTransform, 360, 0, 8, 0);

            s.list = DeviceList(s.transform, HeadH + 218f, 230f);

            TextMeshProUGUI lbl;
            s.continueButton = GradientButton(s.transform, "Continue", "play_arrow", "Continue (0)", out lbl, true);
            s.continueLabel = lbl;
            Place((RectTransform)s.continueButton.transform, 0.5f, 0f, 660f, 124f, 0f, 70f);
            s.continueButton.GetComponent<ThemedShape>().themeHeight = false;
            s.continueButton.GetComponent<ThemedShape>().pillHeight = 124f;
            ShadowFor((RectTransform)s.continueButton.transform, 0.30f, ColorRole.Primary);
            return s;
        }

        // ===================================================================== Play page (host + player)
        static AppScreen BuildPlay(Transform parent)
        {
            var s = MakeScreen<PlayScreen>(parent, "Play", ScreenId.Play);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            Header(s.transform, "Play", "Mode: Host", true, out title, out sub, out back);
            s.backButton = back;
            s.subtitleText = sub;

            // target: "Playing on: ..." / "My Quest: ..." + Change
            var chip = Shape(s.transform, "TargetChip", ColorRole.Card, ShapeKind.Pill);
            chip.GetComponent<ThemedShape>().pillHeight = 100f;
            chip.raycastTarget = false;
            TopBand(chip.rectTransform, HeadH + 4f, 100f, 32f);
            ShadowFor(chip.rectTransform, 0.08f);
            var ti = Icon(chip.transform, "Icon", "vr_headset", ColorRole.Primary, 56f);
            Place(ti.rectTransform, 0f, 0.5f, 56f, 56f, 34f, 0f);
            s.targetText = Txt(chip.transform, "Target", "Playing on: Quest-A1B2", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.targetText.rectTransform, 110, 0, 222, 0);
            var ch = Shape(chip.transform, "Change", ColorRole.PrimarySoft, ShapeKind.Pill);
            ch.GetComponent<ThemedShape>().pillHeight = 72f;
            Place(ch.rectTransform, 1f, 0.5f, 190f, 72f, -16f, 0f);
            s.changeTargetButton = MakeButton(ch.gameObject, ch);
            s.changeLabel = Txt(ch.transform, "Label", "Change", TextRole.Caption, ColorRole.Primary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(s.changeLabel.rectTransform);

            // content type
            var kindRow = New("KindRow", s.transform);
            TopBand(kindRow, HeadH + 124f, 100f, 32f);
            VLayout(kindRow.gameObject, 0, 0);
            s.kindSeg = Seg(kindRow, "Kind", new[] { "Video", "Audio", "Image", "Text" }, 100f, true,
                new[] { "movie", "music_note", "image", "article" });

            // preview card (same as the Send tab)
            var holder = New("PreviewCard", s.transform);
            TopBand(holder, HeadH + 244f, 600f, 40f);
            s.preview = PreviewCard(holder, "Pick a type above: video, audio, image or text");

            // send
            TextMeshProUGUI lbl;
            s.sendButton = GradientButton(s.transform, "Send", "send", "Send & Play (2)", out lbl, true);
            s.sendLabel = lbl;
            s.sendButton.GetComponent<ThemedShape>().themeHeight = false;
            s.sendButton.GetComponent<ThemedShape>().pillHeight = 120f;
            Place((RectTransform)s.sendButton.transform, 0.5f, 1f, 760f, 120f, 0f, -(HeadH + 870f));
            ShadowFor((RectTransform)s.sendButton.transform, 0.28f, ColorRole.Primary);

            // before sending: a hint; after: the controls of the content type
            s.hintText = Txt(s.transform, "Hint", "", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            s.hintText.enableWordWrapping = true;
            TopBand(s.hintText.rectTransform, HeadH + 1020f, 100f, 60f);
            var ctrl = New("Controls", s.transform);
            TopBand(ctrl, HeadH + 1016f, 380f, 32f);
            s.controlsRoot = ctrl.gameObject;
            s.controls = ControlsCompact(ctrl);
            return s;
        }

        // ===================================================================== shared: Devices list
        // scroll list of DeviceRowView + empty text; the DeviceListView sits on the scroll view
        static DeviceListView DeviceList(Transform screen, float top, float bottom)
        {
            var content = ScrollList(screen, "List", top, bottom);
            var view = content.GetComponentInParent<ScrollRect>().gameObject.AddComponent<DeviceListView>();
            view.rowTemplate = DeviceRow(content);
            view.rowTemplate.gameObject.SetActive(false);
            view.emptyText = Txt(screen, "Empty", "No devices", TextRole.Body, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
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
            Place(v.checkBox.rectTransform, 0f, 0.5f, 60f, 60f, 36f, 0f);
            v.checkMark = Icon(v.checkBox.transform, "Tick", "check", ColorRole.OnPrimary, 46f);
            Place(v.checkMark.rectTransform, 0.5f, 0.5f, 46f, 46f);
            if (headset != null)
            {
                var hs = Img(rt, "Headset", headset, ColorRole.OnPrimary);
                hs.preserveAspect = true;
                hs.raycastTarget = false;
                Place(hs.rectTransform, 0f, 0.5f, 150f, 94f, 116f, 0f);
            }
            Divider(rt, 290f, true);
            // text boxes are taller than one line even at text size L: TextMeshPro hides a line that does not fit (Ellipsis)
            v.nameText = Txt(rt, "Name", "Quest-01", TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            TopLeft(v.nameText.rectTransform, 320f, 8f, 270f, 72f);
            v.infoText = Txt(rt, "Info", "86% battery  •  192.168.1.25", TextRole.Caption, ColorRole.SubText);
            TopLeft(v.infoText.rectTransform, 320f, 78f, 36f, 46f);
            v.contentText = Txt(rt, "Content", "Ready", TextRole.Caption, ColorRole.SubText, TextWeight.SemiBold);
            v.contentText.GetComponent<ThemedText>().applyColor = false;
            TopLeft(v.contentText.rectTransform, 320f, 120f, 36f, 46f);

            v.statusChip = Shape(rt, "Status", ColorRole.Accent, ShapeKind.Pill, 0.15f);
            v.statusChip.GetComponent<ThemedColor>().enabled = false;
            v.statusChip.GetComponent<ThemedShape>().pillHeight = 56f;
            v.statusChip.raycastTarget = false;
            Place(v.statusChip.rectTransform, 1f, 1f, 210f, 56f, -28f, -16f);
            v.statusDot = Img(v.statusChip.transform, "Dot", circle, ColorRole.Accent);
            v.statusDot.GetComponent<ThemedColor>().enabled = false;
            v.statusDot.raycastTarget = false;
            Place(v.statusDot.rectTransform, 0f, 0.5f, 20f, 20f, 24f, 0f);
            v.statusText = Txt(v.statusChip.transform, "Text", "Online", TextRole.Caption, ColorRole.Accent, TextWeight.SemiBold);
            v.statusText.GetComponent<ThemedText>().applyColor = false;
            Stretch(v.statusText.rectTransform, 56, 0, 10, 0);
            return v;
        }

        // ===================================================================== shared: content preview card
        // shadow + card in 'holder': picture / type icon / text paper, duration badge, name, info, Change / Edit / Remove;
        // empty state with 'emptyHint'
        static MediaPreviewView PreviewCard(RectTransform holder, string emptyHint)
        {
            ShadowInside(holder, 0.10f);
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
            prev.rectTransform.offsetMin = new Vector2(32, 300);
            prev.rectTransform.offsetMax = new Vector2(-32, -32);
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
            Place(v.previewIcon.rectTransform, 0.5f, 0.5f, 180f, 180f);
            var paper = New("TextPaper", prev.transform);
            Stretch(paper, 28, 28, 28, 28);
            var paperBg = Shape(paper, "Bg", ColorRole.Card, ShapeKind.Field);
            Stretch(paperBg.rectTransform);
            paperBg.raycastTarget = false;
            v.previewText = Txt(paper, "Text", "", TextRole.Body, ColorRole.Text, TextWeight.Regular, TextAlignmentOptions.TopLeft);
            v.previewText.enableWordWrapping = true;
            v.previewText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(v.previewText.rectTransform, 32, 28, 32, 28);
            v.textPaper = paper.gameObject;
            paper.gameObject.SetActive(false);
            var badge = Shape(prev.transform, "DurationBadge", ColorRole.Text, ShapeKind.Pill, 0.75f);
            badge.GetComponent<ThemedColor>().enabled = false;
            badge.color = new Color(0f, 0f, 0f, 0.55f);
            badge.raycastTarget = false;
            Place(badge.rectTransform, 0f, 0f, 130f, 50f, 20f, 20f);
            v.badge = badge.gameObject;
            v.badgeText = Txt(badge.transform, "Time", "05:30", TextRole.Caption, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(v.badgeText.rectTransform);

            v.nameText = Txt(filled, "Name", "Nature_01.mp4", TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            BottomBand(v.nameText.rectTransform, 200f, 72f, 40f);
            v.infoText = Txt(filled, "Info", "Video  •  245 MB  •  05:30", TextRole.Caption, ColorRole.SubText);
            BottomBand(v.infoText.rectTransform, 150f, 52f, 40f);
            var actions = New("Actions", filled);
            BottomBand(actions, 32f, 96f, 32f);
            var ah = HLayout(actions.gameObject, 20);
            ah.childForceExpandWidth = false;
            ah.childAlignment = TextAnchor.MiddleLeft;
            v.changeButton = SmallAction(actions, "Change", "swap_horiz", "Change", ColorRole.Primary, 250f);
            v.editButton = SmallAction(actions, "Edit", "edit", "Edit", ColorRole.Primary, 210f);
            v.removeButton = SmallAction(actions, "Remove", "close", "Remove", ColorRole.Danger, 250f);

            // ---- empty
            var empty = New("Empty", card.transform);
            Stretch(empty);
            v.emptyGroup = empty.gameObject;
            var ring = Img(empty, "Dashed", outline, ColorRole.Divider);
            ring.type = Image.Type.Sliced;
            ring.raycastTarget = false;
            var rsh = ring.gameObject.AddComponent<ThemedShape>();
            rsh.kind = ShapeKind.Card;
            Stretch(ring.rectTransform, 32, 32, 32, 32);
            var ec = Img(empty, "Circle", circle, ColorRole.PrimarySoft);
            ec.raycastTarget = false;
            Place(ec.rectTransform, 0.5f, 0.5f, 170f, 170f, 0f, 110f);
            var ei = Icon(ec.transform, "Icon", "add", ColorRole.Primary, 90f);
            Place(ei.rectTransform, 0.5f, 0.5f, 90f, 90f);
            var et = Txt(empty, "Title", "No file selected", TextRole.Header, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Place(et.rectTransform, 0.5f, 0.5f, 800f, 80f, 0f, -40f);
            var es = Txt(empty, "Hint", emptyHint, TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            Place(es.rectTransform, 0.5f, 0.5f, 860f, 60f, 0f, -110f);
            empty.gameObject.SetActive(false);
            return v;
        }

        // ===================================================================== shared: playback controls
        // Control tab: big tiles (2 x 2), mute row, state line
        static PlaybackControlsView ControlsBig(Transform screen)
        {
            var root = New("Controls", screen);
            Stretch(root);
            var v = root.gameObject.AddComponent<PlaybackControlsView>();

            var media = New("MediaControls", root);
            Stretch(media);
            v.mediaControls = media.gameObject;
            var grid = New("Buttons", media);
            TopBand(grid, HeadH + 220f, 640f);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(484f, 306f);
            g.spacing = new Vector2(32f, 28f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;
            v.playButton = Tile(grid, "Play", "play_arrow", ColorRole.Accent);
            v.pauseButton = Tile(grid, "Pause", "pause", ColorRole.Primary);
            v.resumeButton = Tile(grid, "Resume", "replay", ColorRole.Primary);
            v.stopButton = Tile(grid, "Stop", "stop", ColorRole.Primary);
            var mute = Shape(media, "MuteRow", ColorRole.Card, ShapeKind.Card);
            TopBand(mute.rectTransform, HeadH + 890f, 150f);
            ShadowFor(mute.rectTransform);
            v.muteSwitch = MuteRowContent(mute.transform);

            var textC = New("TextControls", root);
            Stretch(textC);
            v.textControls = textC.gameObject;
            var tgrid = New("Buttons", textC);
            TopBand(tgrid, HeadH + 220f, 310f);
            var tg = tgrid.gameObject.AddComponent<GridLayoutGroup>();
            tg.cellSize = new Vector2(484f, 306f);
            tg.spacing = new Vector2(32f, 28f);
            tg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tg.constraintCount = 2;
            tg.childAlignment = TextAnchor.UpperCenter;
            v.showButton = Tile(tgrid, "Show on Quest", "visibility", ColorRole.Accent);
            v.hideButton = Tile(tgrid, "Hide", "visibility_off", ColorRole.Primary);
            textC.gameObject.SetActive(false);

            v.stateText = Txt(root, "State", "", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(v.stateText.rectTransform, HeadH + 1060f, 60f);
            return v;
        }

        // Play page: progress bar, one row of 4 tiles + mute row (video / audio) or Show / Hide (image / text), state line
        static PlaybackControlsView ControlsCompact(RectTransform root)
        {
            var v = root.gameObject.AddComponent<PlaybackControlsView>();

            var prog = New("Progress", root);
            TopBand(prog, 0f, 64f, 0f);
            v.progressRow = prog.gameObject;
            v.elapsedText = Txt(prog, "Elapsed", "00:00", TextRole.Small, ColorRole.SubText, TextWeight.SemiBold);
            Place(v.elapsedText.rectTransform, 0f, 0.5f, 120f, 50f, 4f, 0f);
            v.durationText = Txt(prog, "Duration", "00:00", TextRole.Small, ColorRole.SubText, TextWeight.SemiBold, TextAlignmentOptions.MidlineRight);
            Place(v.durationText.rectTransform, 1f, 0.5f, 120f, 50f, -4f, 0f);
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
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            v.progressFill = fill.rectTransform;

            var media = New("MediaControls", root);
            TopBand(media, 80f, 230f, 0f);
            v.mediaControls = media.gameObject;
            var grid = New("Buttons", media);
            TopBand(grid, 0f, 110f, 0f);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(241f, 110f);
            g.spacing = new Vector2(12f, 12f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            g.childAlignment = TextAnchor.UpperCenter;
            v.playButton = ActionTile(grid, "Play", "play_arrow");
            v.pauseButton = ActionTile(grid, "Pause", "pause");
            v.resumeButton = ActionTile(grid, "Resume", "replay");
            v.stopButton = ActionTile(grid, "Stop", "stop");
            var mute = Shape(media, "MuteRow", ColorRole.Card, ShapeKind.Card);
            TopBand(mute.rectTransform, 126f, 104f, 0f);
            ShadowFor(mute.rectTransform, 0.08f);
            v.muteSwitch = MuteRowContent(mute.transform);

            var textC = New("TextControls", root);
            TopBand(textC, 80f, 110f, 0f);
            v.textControls = textC.gameObject;
            var tg = textC.gameObject.AddComponent<GridLayoutGroup>();
            tg.cellSize = new Vector2(494f, 110f);
            tg.spacing = new Vector2(12f, 12f);
            tg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tg.constraintCount = 2;
            tg.childAlignment = TextAnchor.UpperCenter;
            v.showButton = ActionTile(textC, "Show on Quest", "visibility");
            v.hideButton = ActionTile(textC, "Hide", "visibility_off");
            textC.gameObject.SetActive(false);

            v.stateText = Txt(root, "State", "", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(v.stateText.rectTransform, 326f, 50f, 0f);
            return v;
        }

        // icon + "Mute" + switch inside a row card
        static ToggleSwitch MuteRowContent(Transform row)
        {
            var mi = Icon(row, "Icon", "volume_off", ColorRole.Text, 64f);
            Place(mi.rectTransform, 0f, 0.5f, 64f, 64f, 40f, 0f);
            var mt = Txt(row, "Label", "Mute", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            Stretch(mt.rectTransform, 140, 0, 220, 0);
            var sw = Switch(row, "MuteSwitch");
            Place((RectTransform)sw.transform, 1f, 0.5f, 120f, 68f, -40f, 0f);
            return sw;
        }
    }
}
