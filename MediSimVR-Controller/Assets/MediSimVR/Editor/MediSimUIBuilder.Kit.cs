using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // ===================================================================== MediSimVR design system
    // One place for spacing, sizes, text styles and the components every screen is built from, so the same kind of
    // element looks the same everywhere. Colours always come from the theme (ThemeData / ThemeManager):
    //   Primary blue, Secondary teal (primary actions = teal-to-blue gradient), Success green, Danger red,
    //   Warning orange, Text, SubText (secondary grey), Divider (borders), Card (white), Background.
    //
    // Buttons (Btn):
    //   Primary      gradient, white label         main action of a screen (Continue, Send, Connect, OK)
    //   Secondary    card + primary border/label   supporting action and every Cancel
    //   Tertiary     soft primary tint, small      small actions (Change, Edit, Select all, Copy, Objects)
    //   Destructive  soft red tint, red label      Remove, Stop, End session, Leave (confirm popups: solid red)
    //   Icon button  white circle + shadow          Back, Settings, Notifications, Refresh, Clear, Link (CircleButton)
    // Cards: ActionCard (Home Control / Receiver, Session Host / Player), list rows (Row), panels (Panel).
    // Text: ScreenTitle (Header), SectionTitle, CardTitle, RowTitle, BodyText, Secondary, Small.
    public static partial class MediSimUIBuilder
    {
        public enum BtnKind { Primary, Secondary, Tertiary, Destructive }

        // ---------- tokens (reference canvas 1080 x 2340) ----------
        const float Margin = 40f;          // screen side margin
        const float CardGap = 24f;         // between cards
        const float CardPad = 32f;         // inside cards
        const float BtnH = 120f;           // Primary / Secondary / Destructive buttons
        const float SmallBtnH = 84f;       // Tertiary + small destructive buttons, header actions
        const float IconBtnSize = 112f;    // circular icon buttons
        const float IconSize = 56f;        // icons in icon buttons and lists
        const float HeroH = 236f;          // big action cards
        const float ChipH = 64f;           // status chips
        const float CardShadow = 0.08f;    // every white card
        const float AccentShadow = 0.24f;  // gradient (primary) elements

        // ---------- text styles ----------
        static TextMeshProUGUI SectionTitle(Transform p, string name, string text)
        { return Txt(p, name, text, TextRole.Body, ColorRole.Text, TextWeight.Heading); }

        static TextMeshProUGUI CardTitle(Transform p, string name, string text, ColorRole role = ColorRole.Text)
        { return Txt(p, name, text, TextRole.Header, role, TextWeight.SemiBold); }

        static TextMeshProUGUI RowTitle(Transform p, string name, string text, ColorRole role = ColorRole.Text)
        { return Txt(p, name, text, TextRole.Body, role, TextWeight.SemiBold); }

        static TextMeshProUGUI BodyText(Transform p, string name, string text, ColorRole role = ColorRole.Text)
        { return Txt(p, name, text, TextRole.Body, role, TextWeight.Regular); }

        static TextMeshProUGUI Secondary(Transform p, string name, string text, ColorRole role = ColorRole.SubText)
        { return Txt(p, name, text, TextRole.Caption, role, TextWeight.Regular); }

        static TextMeshProUGUI SmallLabel(Transform p, string name, string text, ColorRole role = ColorRole.SubText)
        { return Txt(p, name, text, TextRole.Small, role, TextWeight.SemiBold); }

        // ---------- buttons ----------
        // one component for every text button; the caller places it (Place / layout). h < 0 = default height of the kind.
        static Button Btn(Transform parent, string name, BtnKind kind, string icon, string label, out TextMeshProUGUI text, float h = -1f)
        {
            if (h < 0f) h = kind == BtnKind.Tertiary ? SmallBtnH : BtnH;
            bool small = h < 100f;
            ColorRole bg = kind == BtnKind.Primary ? ColorRole.Primary : kind == BtnKind.Secondary ? ColorRole.Card
                : kind == BtnKind.Tertiary ? ColorRole.PrimarySoft : ColorRole.Danger;
            var img = Shape(parent, name, bg, ShapeKind.Button, kind == BtnKind.Destructive ? 0.12f : 1f);
            img.GetComponent<ThemedShape>().pillHeight = h;
            img.rectTransform.sizeDelta = new Vector2(img.rectTransform.sizeDelta.x, h);
            LE(img, h);
            if (kind == BtnKind.Primary) Gradient(img);
            if (kind == BtnKind.Secondary)
            {
                var border = Img(img.transform, "Border", outline, ColorRole.Primary);
                border.type = Image.Type.Sliced;
                border.raycastTarget = false;
                var bs = border.gameObject.AddComponent<ThemedShape>();
                bs.kind = ShapeKind.Button;
                bs.pillHeight = h;
                Stretch(border.rectTransform);
            }
            var b = MakeButton(img.gameObject, img);
            ColorRole fg = kind == BtnKind.Primary ? ColorRole.OnPrimary : kind == BtnKind.Destructive ? ColorRole.Danger : ColorRole.Primary;
            var row = New("Row", img.transform);
            Stretch(row, 16, 0, 16, 0);
            var hl = HLayout(row.gameObject, small ? 12 : 16);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childForceExpandWidth = false;
            hl.childControlWidth = true;
            hl.childControlHeight = false;
            if (icon != null)
            {
                float isz = small ? 44f : 52f;
                var ic = Icon(row, "Icon", icon, fg, isz);
                LE(ic, isz, isz);
            }
            text = Txt(row, "Label", label, small ? TextRole.Caption : TextRole.Button, fg, TextWeight.SemiBold, TextAlignmentOptions.Center);
            text.rectTransform.sizeDelta = new Vector2(0, small ? 56f : 72f);
            return b;
        }

        static Button Btn(Transform parent, string name, BtnKind kind, string icon, string label, float h = -1f)
        {
            TextMeshProUGUI t;
            return Btn(parent, name, kind, icon, label, out t, h);
        }

        // big button docked at the bottom of a screen (Continue, Connect...): full width minus margins, with shadow
        static Button BottomButton(Transform screen, string name, BtnKind kind, string icon, string label, out TextMeshProUGUI text)
        {
            var b = Btn(screen, name, kind, icon, label, out text);
            var rt = (RectTransform)b.transform;
            BottomBand(rt, 48f, BtnH, Margin);
            if (kind == BtnKind.Primary) ShadowFor(rt, AccentShadow, ColorRole.Primary);
            return b;
        }

        // small action in a screen header, level with the back / icon buttons (End session, Leave)
        static Button HeaderAction(RectTransform hdr, string name, BtnKind kind, string label, float w)
        {
            var b = Btn(hdr, name, kind, null, label, SmallBtnH);
            Place((RectTransform)b.transform, 1f, 1f, w, SmallBtnH, -Margin, -(118f - SmallBtnH * 0.5f));
            return b;
        }

        // ---------- cards ----------
        // big action card: icon circle, title, subtitle, chevron. primary = gradient (main path), else white card.
        static Button ActionCard(Transform parent, string name, float y, bool primary, string icon, string title, string sub)
        {
            var card = Shape(parent, name, primary ? ColorRole.Primary : ColorRole.Card, ShapeKind.Card);
            if (primary) Gradient(card);
            TopBand(card.rectTransform, y, HeroH, Margin);
            ShadowFor(card.rectTransform, primary ? AccentShadow : CardShadow, primary ? ColorRole.Primary : ColorRole.Text);
            var b = MakeButton(card.gameObject, card);
            var bg = Img(card.transform, "IconBg", circle, primary ? ColorRole.OnPrimary : ColorRole.PrimarySoft, primary ? 0.18f : 1f);
            bg.raycastTarget = false;
            Place(bg.rectTransform, 0f, 0.5f, 128f, 128f, CardPad + 4f, 0f);
            var ic = Icon(bg.transform, "Icon", icon, primary ? ColorRole.OnPrimary : ColorRole.Primary, 68f);
            Place(ic.rectTransform, 0.5f, 0.5f, 68f, 68f);
            var t = CardTitle(card.transform, "Title", title, primary ? ColorRole.OnPrimary : ColorRole.Text);
            TopLeft(t.rectTransform, 200f, 44f, 104f, 72f);
            var st = Secondary(card.transform, "Subtitle", sub, primary ? ColorRole.OnPrimary : ColorRole.SubText);
            st.enableWordWrapping = true;
            st.alignment = TextAlignmentOptions.TopLeft;
            TopLeft(st.rectTransform, 200f, 120f, 104f, 84f);
            var ch = Icon(card.transform, "Chevron", "chevron_right", primary ? ColorRole.OnPrimary : ColorRole.SubText, IconSize);
            Place(ch.rectTransform, 1f, 0.5f, IconSize, IconSize, -CardPad, 0f);
            return b;
        }

        // white card in a vertical list (no tap), soft shadow; returns the card (content parent)
        static RectTransform Panel(Transform list, string name, float h)
        {
            var c = New(name, list);
            LE(c, h);
            ShadowInside(c, CardShadow);
            var card = Shape(c, "Card", ColorRole.Card, ShapeKind.Card);
            Stretch(card.rectTransform);
            card.raycastTarget = false;
            return card.rectTransform;
        }

        // status chip: tinted pill + dot + label in the status colour (Online, In Use, LIVE...); 'role' is the colour
        static Image StatusChip(Transform parent, string name, ColorRole role, string label, float w, out Image dot, out TextMeshProUGUI text)
        {
            var bg = Shape(parent, name, role, ShapeKind.Pill, 0.14f);
            bg.GetComponent<ThemedShape>().pillHeight = ChipH;
            bg.raycastTarget = false;
            bg.rectTransform.sizeDelta = new Vector2(w, ChipH);
            dot = Img(bg.transform, "Dot", circle, role);
            dot.raycastTarget = false;
            Place(dot.rectTransform, 0f, 0.5f, 20f, 20f, 24f, 0f);
            text = SmallLabel(bg.transform, "Text", label, role);
            Stretch(text.rectTransform, 56, 0, 14, 0);
            return bg;
        }

        // round icon with a coloured circle above a label: playback / session action tiles. big = Control tab tiles.
        // Colours: Play = Success, Pause / Resume = Primary, Stop = Danger, other actions = Primary.
        static Button ActionTile(Transform grid, string label, string icon, ColorRole role = ColorRole.Primary, bool big = false)
        {
            var cell = New(label, grid);
            ShadowInside(cell, CardShadow);
            var card = Shape(cell, "Card", ColorRole.Card, ShapeKind.Card);
            Stretch(card.rectTransform);
            var b = MakeButton(card.gameObject, card);
            float cs = big ? 128f : 54f, isz = big ? 76f : 32f;
            var c = Img(card.transform, "Circle", circle, role);
            c.raycastTarget = false;
            Place(c.rectTransform, 0.5f, 1f, cs, cs, 0f, big ? -32f : -8f);
            var ic = Icon(c.transform, "Icon", icon, ColorRole.OnPrimary, isz);
            Place(ic.rectTransform, 0.5f, 0.5f, isz, isz);
            var t = Txt(card.transform, "Label", label, big ? TextRole.Button : TextRole.Small, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            BottomBand(t.rectTransform, big ? 24f : 2f, big ? 58f : TextBoxH(TextRole.Small), 6f);
            return b;
        }

        // the smallest box height that shows one line of this text role at every text size (S / M / L): TextMeshPro hides
        // a line that does not fit completely (Ellipsis), so a box must be taller than the line at size L (x1.12)
        static float TextBoxH(TextRole r)
        {
            return Mathf.Ceil(ThemedText.BaseSize(r) * 1.12f * 1.25f) + 2f;
        }
    }
}
