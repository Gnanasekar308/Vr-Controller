using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Control Mode flow screens (part of MediSimUIBuilder, design system in MediSimUIBuilder.Kit.cs): Quest list (host)
    // and Play page, plus the components they share with the tabs: Devices list (DeviceListView + DeviceRowView), content
    // preview card (MediaPreviewView), playback controls (PlaybackControlsView, big for the Control tab, compact for the
    // Play page). Content that can be taller than a small phone screen sits in scroll lists.
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
            TopBand(filterRow, HeadH + 4f, 96f, Margin);
            VLayout(filterRow.gameObject, 0, 0);
            s.filterSeg = Seg(filterRow, "Filter", new[] { "All (4)", "Online (2)", "Offline (1)", "In Use (1)" }, 96f, true);

            var selRow = New("SelectRow", s.transform);
            TopBand(selRow, HeadH + 116f, SmallBtnH, Margin);
            TextMeshProUGUI saLabel;
            s.selectAllButton = Btn(selRow, "SelectAll", BtnKind.Tertiary, null, "Select all online", out saLabel);
            Place((RectTransform)s.selectAllButton.transform, 0f, 0.5f, 340f, SmallBtnH, 0f, 0f);
            s.selectAllLabel = saLabel;
            s.selectedText = Secondary(selRow, "Count", "0 of 3 online selected");
            s.selectedText.alignment = TextAlignmentOptions.MidlineRight;
            Stretch(s.selectedText.rectTransform, 360, 0, 0, 0);

            s.list = DeviceList(s.transform, HeadH + 216f, 48f + BtnH + CardGap);

            TextMeshProUGUI lbl;
            s.continueButton = BottomButton(s.transform, "Continue", BtnKind.Primary, "play_arrow", "Continue (0)", out lbl);
            s.continueLabel = lbl;
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

            // one scroll list: target, content type, preview, Send, then the controls (scrolls on short screens)
            var list = ScrollList(s.transform, "List", HeadH + 4f, 20f);
            list.GetComponent<VerticalLayoutGroup>().spacing = CardGap;

            // target: "Playing on: ..." / "My Quest: ..." + Change
            var chip = Panel(list, "TargetChip", 100f);
            var ti = Icon(chip, "Icon", "vr_headset", ColorRole.Primary, IconSize);
            Place(ti.rectTransform, 0f, 0.5f, IconSize, IconSize, CardPad, 0f);
            s.targetText = Txt(chip, "Target", "Playing on: Quest-A1B2", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.targetText.rectTransform, CardPad + IconSize + 20f, 0, 236, 0);
            TextMeshProUGUI chLabel;
            s.changeTargetButton = Btn(chip, "Change", BtnKind.Tertiary, null, "Change", out chLabel, 72f);
            Place((RectTransform)s.changeTargetButton.transform, 1f, 0.5f, 196f, 72f, -16f, 0f);
            s.changeLabel = chLabel;

            // content type
            s.kindSeg = Seg(list, "Kind", new[] { "Video", "Audio", "Image", "Text" }, 96f, true,
                new[] { "movie", "music_note", "image", "article" });

            // preview card (same component as the Send tab, smaller picture)
            var holder = New("PreviewCard", list);
            LE(holder, 660f);
            s.preview = PreviewCard(holder, "Pick a type above: video, audio, image or text");

            // send (primary)
            TextMeshProUGUI lbl;
            s.sendButton = Btn(list, "Send", BtnKind.Primary, "send", "Send & Play (2)", out lbl);
            s.sendLabel = lbl;

            // before sending: a hint; after: the controls of the content type
            s.hintText = Secondary(list, "Hint", "");
            s.hintText.alignment = TextAlignmentOptions.Center;
            s.hintText.enableWordWrapping = true;
            LE(s.hintText, 90f);
            var ctrl = New("Controls", list);
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
            g.cellSize = new Vector2(488f, 248f);
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
            tg.cellSize = new Vector2(488f, 248f);
            tg.spacing = new Vector2(CardGap, CardGap);
            tg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tg.constraintCount = 2;
            tg.childAlignment = TextAnchor.UpperCenter;
            v.showButton = ActionTile(textC, "Show on Quest", "visibility", ColorRole.Success, true);
            v.hideButton = ActionTile(textC, "Hide", "visibility_off", ColorRole.Primary, true);
            textC.gameObject.SetActive(false);
            return v;
        }

        // Play page (in its scroll list): progress bar, one row of 4 tiles + Mute row (video / audio) or Show / Hide
        // (image / text), state line
        static PlaybackControlsView ControlsCompact(RectTransform root)
        {
            VLayout(root.gameObject, 0, 16);
            var v = root.gameObject.AddComponent<PlaybackControlsView>();

            var prog = New("Progress", root);
            LE(prog, 56f);
            v.progressRow = prog.gameObject;
            v.elapsedText = SmallLabel(prog, "Elapsed", "00:00");
            Place(v.elapsedText.rectTransform, 0f, 0.5f, 120f, 50f, 4f, 0f);
            v.durationText = SmallLabel(prog, "Duration", "00:00");
            v.durationText.alignment = TextAlignmentOptions.MidlineRight;
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
            Gradient(fill);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            v.progressFill = fill.rectTransform;

            var media = New("MediaControls", root);
            VLayout(media.gameObject, 0, 16);
            v.mediaControls = media.gameObject;
            var grid = New("Buttons", media);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(238f, 120f);
            g.spacing = new Vector2(16f, 16f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            g.childAlignment = TextAnchor.UpperCenter;
            v.playButton = ActionTile(grid, "Play", "play_arrow", ColorRole.Success);
            v.pauseButton = ActionTile(grid, "Pause", "pause");
            v.resumeButton = ActionTile(grid, "Resume", "replay");
            v.stopButton = ActionTile(grid, "Stop", "stop", ColorRole.Danger);
            var mute = Panel(media, "MuteRow", 104f);
            v.muteSwitch = MuteRowContent(mute);

            var textC = New("TextControls", root);
            v.textControls = textC.gameObject;
            var tg = textC.gameObject.AddComponent<GridLayoutGroup>();
            tg.cellSize = new Vector2(492f, 120f);
            tg.spacing = new Vector2(16f, 16f);
            tg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tg.constraintCount = 2;
            tg.childAlignment = TextAnchor.UpperCenter;
            v.showButton = ActionTile(textC, "Show on Quest", "visibility", ColorRole.Success);
            v.hideButton = ActionTile(textC, "Hide", "visibility_off");
            textC.gameObject.SetActive(false);

            v.stateText = Secondary(root, "State", "");
            v.stateText.alignment = TextAlignmentOptions.Center;
            LE(v.stateText, 50f);
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
