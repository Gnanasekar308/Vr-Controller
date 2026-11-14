using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Menu: MediSimVR > Build UI Scene
    // Creates Assets/MediSimVR/Scenes/Main.unity with the full phone UI (Canvas + TextMeshPro).
    // Everything is normal GameObjects afterwards: move / resize / recolour in the Inspector as you like.
    public static partial class MediSimUIBuilder
    {
        const string Root = "Assets/MediSimVR/";
        const string ScenePath = Root + "Scenes/Main.unity";
        const float NavH = 200f, HeadH = 250f;

        static Sprite round, circle, outline, hueBar, headset, wave, waveTop, shadowSp, logoWhite;

        // ===================================================================== entry
        [MenuItem("MediSimVR/Build UI Scene")]
        public static void Build()
        {
            // batchmode (-executeMethod MediSimVR.EditorTools.MediSimUIBuilder.Build -quit): no dialogs
            bool batch = Application.isBatchMode;
            if (!batch && File.Exists(ScenePath) && !EditorUtility.DisplayDialog("MediSimVR",
                "Main.unity already exists. Rebuild it? (changes made in that scene will be lost)", "Rebuild", "Cancel")) return;
            if (!LoadSprites())
            {
                if (batch) EditorApplication.Exit(1);
                return;
            }
            EnsureFonts();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            cam.cullingMask = 0; // the UI is a screen-space overlay; the 3D "Live Scene" has its own camera

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<OrientationScaler>();   // landscape (Live session): 2340 x 1080 reference

            var bg = Img(canvasGo.transform, "Background", null, ColorRole.Background);
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            var safe = New("SafeArea", canvasGo.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var screensRoot = New("Screens", safe);
            Stretch(screensRoot, 0, NavH, 0, 0);

            var app = new GameObject("App");
            var root = app.AddComponent<AppRoot>();
            var nav = app.AddComponent<Navigator>();
            app.AddComponent<FilePicker>();
            app.AddComponent<DemoTour>().nav = nav;   // demo screenshots (-demotour), see DemoTour
            app.AddComponent<ContentChooser>().nav = nav;   // content picker + send, shared by the Send tab
            // this phone's player for the Control tab; VideoPlayer + AudioSource exist in the scene, so IL2CPP keeps them
            var phoneGo = new GameObject("PhoneVideo");
            phoneGo.transform.SetParent(app.transform, false);
            var phone = phoneGo.AddComponent<PhoneVideoPlayer>();
            phone.video = phoneGo.AddComponent<UnityEngine.Video.VideoPlayer>();
            phone.video.playOnAwake = false;
            phone.audioSource = phoneGo.AddComponent<AudioSource>();
            phone.audioSource.playOnAwake = false;
            root.navigator = nav;

            nav.screens.Add(BuildSplash(screensRoot));
            nav.screens.Add(BuildRoleSelect(screensRoot));
            nav.screens.Add(BuildLogin(screensRoot));
            nav.screens.Add(BuildDashboard(screensRoot));
            nav.screens.Add(BuildMeetings(screensRoot));
            nav.screens.Add(BuildMeetingCreate(screensRoot));
            nav.screens.Add(BuildMeetingContent(screensRoot));
            nav.screens.Add(BuildMeetingDetails(screensRoot));
            nav.screens.Add(BuildLive(screensRoot));
            nav.screens.Add(BuildReport(screensRoot));
            nav.screens.Add(BuildDevices(screensRoot));
            nav.screens.Add(BuildSend(screensRoot));
            nav.screens.Add(BuildControl(screensRoot, phone));
            nav.screens.Add(BuildScene(screensRoot));
            nav.screens.Add(BuildSettings(screensRoot));
            nav.screens.Add(BuildNotifications(screensRoot));
            nav.screens.Add(BuildEditApp(screensRoot));
            for (int i = 1; i < nav.screens.Count; i++) nav.screens[i].gameObject.SetActive(false);

            BuildNav(safe, nav);
            nav.screensRoot = screensRoot;
            nav.navHeight = NavH;
            var overlay = New("Overlay", safe);
            Stretch(overlay);
            nav.toast = BuildToast(overlay);
            nav.popup = BuildPopup(overlay);
            nav.colorPicker = BuildColorPicker(overlay);
            nav.actionSheet = BuildActionSheet(overlay);
            nav.textEditor = BuildTextEditor(overlay);

            Directory.CreateDirectory(Root + "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[MediSimVR] UI scene built: " + ScenePath + " (added to Build Settings as scene 0), screens " + nav.screens.Count + ", tabs " + nav.navItems.Count);
            if (!batch) EditorUtility.DisplayDialog("MediSimVR", "UI scene ready:\n" + ScenePath + "\n\nPress Play to try it.", "OK");
        }

        // ===================================================================== assets
        static Sprite S(string p) { return AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/" + p); }
        static Sprite IconS(string n) { return S("Icons/" + n + ".png"); }

        static bool LoadSprites()
        {
            round = S("Shapes/rounded_64.png");
            circle = S("Shapes/circle.png");
            outline = S("Shapes/rounded_outline_64.png");
            hueBar = S("Shapes/hue_bar.png");
            headset = S("Decor/headset.png");
            wave = S("Decor/wave.png");
            waveTop = S("Decor/wave_top.png");
            shadowSp = S("Shapes/shadow.png");
            logoWhite = S("Branding/logo_white.png");
            if (round == null || circle == null || IconS("home") == null || logoWhite == null)
            {
                Debug.LogError("[MediSimVR] Art sprites not found under Assets/MediSimVR/Art");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("MediSimVR", "Art sprites not found under Assets/MediSimVR/Art.\nImport the Step 5 and Step 7 zips first.", "OK");
                return false;
            }
            return true;
        }

        static void EnsureFonts()
        {
            MakeFont("Inter-Regular");
            MakeFont("Inter-SemiBold");
        }

        static void MakeFont(string n)
        {
            string dir = Root + "Resources/MediSimFonts";
            string dst = dir + "/" + n + " SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(dst) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/" + n + ".ttf");
            if (font == null) { Debug.LogWarning("[MediSimVR] Font missing: " + n + ".ttf (TMP default font will be used)"); return; }
            Directory.CreateDirectory(dir);
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fa.name = n + " SDF";
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u2022\u2014\u00A9");
            AssetDatabase.CreateAsset(fa, dst);
            fa.atlasTextures[0].name = n + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = n + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(dst);
        }

        // ===================================================================== basic helpers
        static RectTransform New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        // anchor point (ax, ay) + pivot, fixed size, offset from that point
        static void Place(RectTransform rt, float ax, float ay, float w, float h, float x = 0, float y = 0)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        // full width band measured from the top: y = distance from top, h = height, side padding
        static void TopBand(RectTransform rt, float y, float h, float side = 40f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(side, -y - h);
            rt.offsetMax = new Vector2(-side, -y);
        }

        static void BottomBand(RectTransform rt, float y, float h, float side = 40f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(side, y);
            rt.offsetMax = new Vector2(-side, y + h);
        }

        static Image Img(Transform parent, string name, Sprite sp, ColorRole role, float alpha = 1f)
        {
            var rt = New(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp;
            var tc = rt.gameObject.AddComponent<ThemedColor>();
            tc.role = role;
            tc.alpha = alpha;
            tc.Apply();
            return img;
        }

        static Image Shape(Transform parent, string name, ColorRole role, ShapeKind kind, float alpha = 1f)
        {
            var img = Img(parent, name, kind == ShapeKind.Circle ? circle : round, role, alpha);
            img.type = kind == ShapeKind.Circle ? Image.Type.Simple : Image.Type.Sliced;
            if (kind != ShapeKind.Circle)
            {
                var sh = img.gameObject.AddComponent<ThemedShape>();
                sh.kind = kind;
                sh.Apply();
            }
            return img;
        }

        static UIGradient Gradient(Graphic g, bool vertical = false)
        {
            var gr = g.gameObject.AddComponent<UIGradient>();
            gr.vertical = vertical;
            return gr;
        }

        static TextMeshProUGUI Txt(Transform parent, string name, string text, TextRole size, ColorRole role,
            TextWeight w = TextWeight.Regular, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = New(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            var th = rt.gameObject.AddComponent<ThemedText>();
            th.size = size;
            th.role = role;
            th.weight = w;
            th.Apply();
            return t;
        }

        static Image Icon(Transform parent, string name, string icon, ColorRole role, float size)
        {
            var img = Img(parent, name, IconS(icon), role);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        static Button MakeButton(GameObject go, Graphic target)
        {
            var b = go.AddComponent<Button>();
            b.targetGraphic = target;
            var c = b.colors;
            c.normalColor = Color.white;
            c.highlightedColor = Color.white;
            c.selectedColor = Color.white;
            c.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            c.fadeDuration = 0.06f;
            b.colors = c;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            return b;
        }

        // transparent tap area with an icon (header buttons)
        static Button IconButton(Transform parent, string name, string icon, ColorRole role, float ax, float x)
        {
            var hit = Img(parent, name, null, ColorRole.Card, 0f);
            Place(hit.rectTransform, ax, 0.5f, 120f, 120f, x, 0f);
            var ic = Icon(hit.transform, "Icon", icon, role, 64f);
            Place(ic.rectTransform, 0.5f, 0.5f, 64f, 64f);
            return MakeButton(hit.gameObject, hit);
        }

        static LayoutElement LE(Component c, float h = -1f, float w = -1f, float flexW = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (h >= 0) le.preferredHeight = h;
            if (w >= 0) le.preferredWidth = w;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        static VerticalLayoutGroup VLayout(GameObject go, int pad, float spacing)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        static HorizontalLayoutGroup HLayout(GameObject go, float spacing)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }

        // vertical scroll list; returns the content (rows go there)
        static RectTransform ScrollList(Transform parent, string name, float top, float bottom, bool themedList = true)
        {
            var sr = New(name, parent);
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(0, bottom);
            sr.offsetMax = new Vector2(0, -top);
            var scroll = sr.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            var vp = Img(sr, "Viewport", null, ColorRole.Card, 0f);
            Stretch(vp.rectTransform);
            vp.gameObject.AddComponent<RectMask2D>();
            var content = New("Content", vp.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var v = VLayout(content.gameObject, 0, 20);
            v.padding = new RectOffset(40, 40, 12, 40);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (themedList)
            {
                var tl = content.gameObject.AddComponent<ThemedLayout>();
                tl.kind = LayoutKind.List;
            }
            scroll.viewport = vp.rectTransform;
            scroll.content = content;
            return content;
        }

        // list row = container (layout) + soft shadow + card (content goes on the card)
        static RectTransform Row(Transform list, string name, float h, out Image card, out Button button, out GameObject container)
        {
            var c = New(name, list);
            LE(c, h);
            var tl = c.gameObject.AddComponent<ThemedLayout>();
            tl.kind = LayoutKind.Row;
            tl.baseHeight = h;
            ShadowInside(c, 0.09f);
            card = Shape(c, "Card", ColorRole.Card, ShapeKind.Card);
            Stretch(card.rectTransform);
            button = MakeButton(card.gameObject, card);
            container = c.gameObject;
            return card.rectTransform;
        }

        static Image ShadowInside(RectTransform c, float alpha, ColorRole role = ColorRole.Text)
        {
            var sh = Img(c, "Shadow", shadowSp, role, alpha);
            sh.type = Image.Type.Sliced;
            sh.raycastTarget = false;
            Stretch(sh.rectTransform, -44, -58, -44, -34);
            sh.transform.SetAsFirstSibling();
            return sh;
        }

        // soft shadow behind an absolutely placed element (sibling, drawn first)
        static Image ShadowFor(RectTransform target, float alpha = 0.10f, ColorRole role = ColorRole.Text)
        {
            var sh = Img(target.parent, target.name + "Shadow", shadowSp, role, alpha);
            sh.type = Image.Type.Sliced;
            sh.raycastTarget = false;
            var r = sh.rectTransform;
            r.anchorMin = target.anchorMin;
            r.anchorMax = target.anchorMax;
            r.pivot = target.pivot;
            r.anchoredPosition = target.anchoredPosition;
            r.sizeDelta = target.sizeDelta;
            r.offsetMin = target.offsetMin + new Vector2(-44, -58);
            r.offsetMax = target.offsetMax + new Vector2(44, 20);
            sh.transform.SetSiblingIndex(target.GetSiblingIndex());
            return sh;
        }

        // design system icon button (Back, Settings, Notifications, Refresh, Clear, Link): white circle with a soft shadow,
        // icon in the text colour, anchored to the top (y = centre distance from the top)
        static Button CircleButton(Transform parent, string name, string icon, float ax, float x, float y)
        {
            var c = New(name, parent);
            c.anchorMin = c.anchorMax = new Vector2(ax, 1f);
            c.pivot = new Vector2(ax, 0.5f);
            c.sizeDelta = new Vector2(IconBtnSize, IconBtnSize);
            c.anchoredPosition = new Vector2(x, -y);
            ShadowInside(c, 0.10f);
            var bg = Img(c, "Circle", circle, ColorRole.Card);
            Stretch(bg.rectTransform);
            var ic = Icon(bg.transform, "Icon", icon, ColorRole.Text, IconSize);
            Place(ic.rectTransform, 0.5f, 0.5f, IconSize, IconSize);
            return MakeButton(bg.gameObject, bg);
        }

        static void Chevron(Transform parent)
        {
            var c = Icon(parent, "Chevron", "chevron_right", ColorRole.SubText, 56f);
            Place(c.rectTransform, 1f, 0.5f, 56f, 56f, -28f, 0f);
        }

        // large left title + subtitle; optional back circle on the left
        static RectTransform Header(Transform screen, string title, string subtitle, bool back,
            out TextMeshProUGUI titleText, out TextMeshProUGUI subText, out Button backButton)
        {
            var hdr = New("Header", screen);
            TopBand(hdr, 0f, HeadH, 0f);
            float x0 = 64f;
            backButton = null;
            if (back)
            {
                backButton = CircleButton(hdr, "Back", "arrow_back", 0f, 48f, 118f);
                x0 = 200f;
            }
            titleText = Txt(hdr, "Title", title, TextRole.Title, ColorRole.Text, TextWeight.Heading);
            titleText.rectTransform.anchorMin = new Vector2(0, 1);
            titleText.rectTransform.anchorMax = new Vector2(1, 1);
            titleText.rectTransform.pivot = new Vector2(0, 1);
            titleText.rectTransform.offsetMin = new Vector2(x0, -168f);
            titleText.rectTransform.offsetMax = new Vector2(-200f, -64f);
            subText = Txt(hdr, "Subtitle", subtitle, TextRole.Caption, ColorRole.SubText);
            subText.rectTransform.anchorMin = new Vector2(0, 1);
            subText.rectTransform.anchorMax = new Vector2(1, 1);
            subText.rectTransform.pivot = new Vector2(0, 1);
            subText.rectTransform.offsetMin = new Vector2(x0, -226f);
            subText.rectTransform.offsetMax = new Vector2(-200f, -168f);
            return hdr;
        }

        static T MakeScreen<T>(Transform parent, string name, ScreenId id) where T : AppScreen
        {
            var rt = New(name, parent);
            Stretch(rt);
            var s = rt.gameObject.AddComponent<T>();
            s.id = id;
            return s;
        }

        static void Wave(Transform screen)
        {
            if (waveTop != null)
            {
                var t = Img(screen, "WaveTop", waveTop, ColorRole.Secondary, 0.30f);
                t.raycastTarget = false;
                t.rectTransform.anchorMin = new Vector2(0, 1);
                t.rectTransform.anchorMax = new Vector2(1, 1);
                t.rectTransform.pivot = new Vector2(0.5f, 1);
                t.rectTransform.sizeDelta = new Vector2(0, 480);
                t.rectTransform.anchoredPosition = Vector2.zero;
                t.transform.SetAsFirstSibling();
            }
            if (wave == null) return;
            var w = Img(screen, "Wave", wave, ColorRole.Secondary, 0.26f);
            w.raycastTarget = false;
            w.rectTransform.anchorMin = new Vector2(0, 0);
            w.rectTransform.anchorMax = new Vector2(1, 0);
            w.rectTransform.pivot = new Vector2(0.5f, 0);
            w.rectTransform.sizeDelta = new Vector2(0, 300);
            w.rectTransform.anchoredPosition = Vector2.zero;
            w.transform.SetAsFirstSibling();
        }

        // segmented control; container = rounded field when 'framed'
        // stacked = icon above a small label (narrow options, e.g. the 5 Scene tools)
        static Segmented Seg(Transform parent, string name, string[] labels, float h, bool framed, string[] icons = null, float[] widths = null,
                             bool stacked = false)
        {
            Image frame = framed ? Shape(parent, name, ColorRole.Field, ShapeKind.Pill) : null;
            Transform holder;
            if (frame != null)
            {
                frame.GetComponent<ThemedShape>().pillHeight = h;
                LE(frame, h);
                holder = frame.transform;
            }
            else
            {
                var r = New(name, parent);
                LE(r, h);
                holder = r;
            }
            var hl = HLayout(holder.gameObject, framed ? 8 : 16);
            hl.padding = framed ? new RectOffset(8, 8, 8, 8) : new RectOffset(0, 0, 0, 0);
            var seg = holder.gameObject.AddComponent<Segmented>();
            for (int i = 0; i < labels.Length; i++)
            {
                var b = Shape(holder, "Option" + i, ColorRole.Field, ShapeKind.Pill);
                b.GetComponent<ThemedShape>().pillHeight = framed ? h - 16 : h;
                b.GetComponent<ThemedColor>().enabled = false; // Segmented owns the colours
                if (widths != null) LE(b, -1, widths[i], 0); else LE(b, -1, -1, 1);
                var btn = MakeButton(b.gameObject, b);
                var row = New("Row", b.transform);
                Stretch(row, stacked ? 4 : 12, 0, stacked ? 4 : 12, 0);
                HorizontalOrVerticalLayoutGroup hl2;
                if (stacked)
                {
                    hl2 = row.gameObject.AddComponent<VerticalLayoutGroup>();
                    hl2.spacing = 2;
                    hl2.childControlWidth = false;
                }
                else
                {
                    hl2 = HLayout(row.gameObject, 12);
                    hl2.childForceExpandWidth = false;
                }
                hl2.childAlignment = TextAnchor.MiddleCenter;
                hl2.childControlHeight = false;
                hl2.childForceExpandHeight = false;
                Image ic = null;
                float isz = stacked ? 34f : 48f;
                if (icons != null && icons[i] != null)
                {
                    ic = Icon(row, "Icon", icons[i], ColorRole.SubText, isz);
                    ic.GetComponent<ThemedColor>().enabled = false;
                    LE(ic, isz, isz);
                }
                var t = Txt(row, "Label", labels[i], stacked ? TextRole.Small : TextRole.Caption, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
                t.GetComponent<ThemedText>().applyColor = false;
                t.rectTransform.sizeDelta = stacked ? new Vector2(170f, TextBoxH(TextRole.Small)) : new Vector2(0, 60);
                seg.buttons.Add(btn);
                seg.backgrounds.Add(b);
                seg.labels.Add(t);
                seg.icons.Add(ic);
            }
            seg.Apply();
            return seg;
        }

        static ToggleSwitch Switch(Transform parent, string name)
        {
            var track = Shape(parent, name, ColorRole.Divider, ShapeKind.Pill);
            track.GetComponent<ThemedColor>().enabled = false;
            track.GetComponent<ThemedShape>().pillHeight = 68f;
            var sw = track.gameObject.AddComponent<ToggleSwitch>();
            sw.track = track;
            var knob = Img(track.transform, "Knob", circle, ColorRole.OnPrimary);
            knob.raycastTarget = false;
            knob.rectTransform.sizeDelta = new Vector2(56, 56);
            sw.knob = knob.rectTransform;
            sw.Apply();
            return sw;
        }

        static Slider MakeSlider(Transform parent, string name, float min, float max, out TextMeshProUGUI valueLabel)
        {
            var row = New(name, parent);
            LE(row, 90);
            valueLabel = Txt(row, "Value", "", TextRole.Body, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.MidlineRight);
            valueLabel.rectTransform.anchorMin = new Vector2(1, 0);
            valueLabel.rectTransform.anchorMax = new Vector2(1, 1);
            valueLabel.rectTransform.pivot = new Vector2(1, 0.5f);
            valueLabel.rectTransform.sizeDelta = new Vector2(150, 0);
            valueLabel.rectTransform.anchoredPosition = Vector2.zero;

            var s = New("Slider", row);
            Stretch(s, 0, 0, 180, 0);
            var slider = s.gameObject.AddComponent<Slider>();
            var track = Shape(s, "Track", ColorRole.Divider, ShapeKind.Pill);
            track.GetComponent<ThemedShape>().pillHeight = 16f;
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, 16);
            var fillArea = New("Fill Area", s);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 16);
            var fill = Shape(fillArea, "Fill", ColorRole.Primary, ShapeKind.Pill);
            fill.GetComponent<ThemedShape>().pillHeight = 16f;
            fill.rectTransform.sizeDelta = Vector2.zero;
            // fill ends exactly under the knob centre
            fillArea.offsetMin = new Vector2(28, fillArea.offsetMin.y);
            fillArea.offsetMax = new Vector2(-28, fillArea.offsetMax.y);
            fill.rectTransform.offsetMin = new Vector2(-28, 0);
            fill.rectTransform.offsetMax = Vector2.zero;
            // knob: the slide area is exactly as tall as the knob, so the Slider cannot stretch it into an oval
            var handleArea = New("Handle Slide Area", s);
            handleArea.anchorMin = new Vector2(0, 0.5f);
            handleArea.anchorMax = new Vector2(1, 0.5f);
            handleArea.sizeDelta = new Vector2(-56, 56);
            handleArea.anchoredPosition = Vector2.zero;
            var handle = Img(handleArea, "Handle", circle, ColorRole.Primary);
            handle.preserveAspect = true;
            handle.rectTransform.sizeDelta = new Vector2(56, 0);
            var knobDot = Img(handle.transform, "Dot", circle, ColorRole.OnPrimary);
            knobDot.raycastTarget = false;
            Place(knobDot.rectTransform, 0.5f, 0.5f, 22f, 22f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            return slider;
        }

        static TMP_InputField Input(Transform parent, string name, string hint, float h)
        {
            var bg = Shape(parent, name, ColorRole.Field, ShapeKind.Field);
            bg.GetComponent<ThemedShape>().pillHeight = h;
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            var area = New("Text Area", bg.transform);
            Stretch(area, 36, 8, 36, 8);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Txt(area, "Placeholder", hint, TextRole.Body, ColorRole.SubText);
            Stretch(ph.rectTransform);
            var tx = Txt(area, "Text", "", TextRole.Body, ColorRole.Text);
            Stretch(tx.rectTransform);
            field.textViewport = area;
            field.textComponent = tx;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.fontAsset = tx.font;
            field.pointSize = ThemedText.BaseSize(TextRole.Body);
            return field;
        }

        static RectTransform Section(Transform list, string title)
        {
            var card = Shape(list, title.Replace(" ", "") + "Section", ColorRole.Card, ShapeKind.Card);
            var v = VLayout(card.gameObject, 36, 24);
            v.padding = new RectOffset(40, 40, 36, 40);
            var t = Txt(card.transform, "Title", title, TextRole.Body, ColorRole.Text, TextWeight.Heading);
            LE(t, 56);
            return card.rectTransform;
        }

        // ===================================================================== screens
        // Devices tab = step 1 of the host flow: filter chips (with counts), select all online, Quest cards, Connect
        static AppScreen BuildDevices(Transform parent)
        {
            var s = MakeScreen<DevicesScreen>(parent, "Devices", ScreenId.Devices);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            var hdr = Header(s.transform, "Devices", "Manage and connect Quest devices", false, out title, out sub, out back);
            s.subtitleText = sub;
            s.refreshButton = CircleButton(hdr, "Refresh", "refresh", 1f, -48f, 118f);
            s.refreshIcon = (RectTransform)s.refreshButton.transform.Find("Icon");

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
            s.connectButton = BottomButton(s.transform, "Connect", BtnKind.Primary, "link", "Connect (2)", out lbl);
            s.connectLabel = lbl;
            return s;
        }

        // thin vertical divider inside a row; x >= 0 from the left, x < 0 from the right
        static void Divider(RectTransform row, float x, bool fromLeft)
        {
            var d = Img(row, "Divider", null, ColorRole.Divider);
            d.raycastTarget = false;
            Place(d.rectTransform, fromLeft ? 0f : 1f, 0.5f, 2f, 84f, x, 0f);
        }

        // Send tab = step 2 of the host flow (1 for a player): target card, preview card, Select + Send
        static AppScreen BuildSend(Transform parent)
        {
            var s = MakeScreen<SendScreen>(parent, "Send", ScreenId.Send);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            Header(s.transform, "Send", "Send to the selected Quests", false, out title, out subT, out backB);
            s.subtitleText = subT;

            var list = ScrollList(s.transform, "List", HeadH + 4f, 48f + BtnH + CardGap);
            list.GetComponent<VerticalLayoutGroup>().spacing = CardGap;
            // target: "Send to: ..." / "My Quest: ..." + Change
            var chip = Panel(list, "Target", 100f);
            var ti = Icon(chip, "Icon", "vr_headset", ColorRole.Primary, IconSize);
            Place(ti.rectTransform, 0f, 0.5f, IconSize, IconSize, CardPad, 0f);
            s.targetText = Txt(chip, "Target", "Send to: Quest-A1B2", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.targetText.rectTransform, CardPad + IconSize + 20f, 0, 236, 0);
            s.changeTargetButton = Btn(chip, "Change", BtnKind.Tertiary, null, "Change", 72f);
            Place((RectTransform)s.changeTargetButton.transform, 1f, 0.5f, 196f, 72f, -16f, 0f);
            // one preview card (a Quest shows one item at a time), 16:9 picture
            var holder = New("PreviewCard", list);
            LE(holder, 880f);
            s.preview = PreviewCard(holder, "Tap Select to choose a video, audio, image or text");

            // ---- bottom buttons: Select (secondary) + Send (primary), same height
            var bar = New("BottomBar", s.transform);
            BottomBand(bar, 48f, BtnH, Margin);
            var hl = HLayout(bar.gameObject, CardGap);
            hl.childAlignment = TextAnchor.MiddleCenter;
            s.selectButton = Btn(bar, "Select", BtnKind.Secondary, "add", "Select");
            TextMeshProUGUI lbl;
            s.sendButton = Btn(bar, "Send", BtnKind.Primary, "send", "Send (2)", out lbl);
            s.sendLabel = lbl;
            return s;
        }

        static FileRowView FileRow(Transform list)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, "FileRow", 170f, out card, out btn, out container);
            var v = container.AddComponent<FileRowView>();
            v.button = btn;
            v.videoIcon = IconS("movie");
            v.audioIcon = IconS("music_note");
            v.textIcon = IconS("article");
            v.thumb = Shape(rt, "Thumb", ColorRole.OnPrimary, ShapeKind.Field);
            v.thumb.raycastTarget = false;
            v.thumb.GetComponent<ThemedColor>().enabled = false;
            v.thumb.color = Color.white;
            v.thumbGradient = Gradient(v.thumb, true);
            v.thumbGradient.useTheme = false;
            Place(v.thumb.rectTransform, 0f, 0.5f, 200f, 128f, 22f, 0f);
            v.typeIcon = Icon(v.thumb.transform, "TypeIcon", "movie", ColorRole.OnPrimary, 60f);
            v.typeIcon.GetComponent<ThemedColor>().enabled = false;
            v.typeIcon.color = new Color(1f, 1f, 1f, 0.9f);
            Place(v.typeIcon.rectTransform, 0.5f, 0.5f, 60f, 60f, 0f, 8f);
            v.badge = Shape(v.thumb.transform, "DurationBadge", ColorRole.Text, ShapeKind.Pill, 0.75f);
            v.badge.GetComponent<ThemedColor>().enabled = false;
            v.badge.color = new Color(0f, 0f, 0f, 0.55f);
            v.badge.raycastTarget = false;
            Place(v.badge.rectTransform, 0f, 0f, 104f, 40f, 10f, 10f);
            v.duration = Txt(v.badge.transform, "Time", "05:30", TextRole.Small, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(v.duration.rectTransform);
            v.nameText = Txt(rt, "Name", "Nature_01.mp4", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            v.nameText.rectTransform.anchorMin = new Vector2(0, 0.5f);
            v.nameText.rectTransform.anchorMax = new Vector2(1, 1);
            v.nameText.rectTransform.offsetMin = new Vector2(250, 0);
            v.nameText.rectTransform.offsetMax = new Vector2(-130, -16);
            v.infoText = Txt(rt, "Info", "Video  \u2022  245 MB", TextRole.Caption, ColorRole.SubText);
            v.infoText.rectTransform.anchorMin = new Vector2(0, 0);
            v.infoText.rectTransform.anchorMax = new Vector2(1, 0.5f);
            v.infoText.rectTransform.offsetMin = new Vector2(250, 16);
            v.infoText.rectTransform.offsetMax = new Vector2(-130, 0);
            v.moreButton = IconButton(rt, "More", "more_horiz", ColorRole.SubText, 1f, -10f);
            var ring = Img(rt, "SelectedRing", outline, ColorRole.Primary);
            ring.type = Image.Type.Sliced;
            ring.raycastTarget = false;
            var rs = ring.gameObject.AddComponent<ThemedShape>();
            rs.kind = ShapeKind.Card;
            Stretch(ring.rectTransform);
            v.selectedRing = ring;
            return v;
        }

        // "Live Scene" v2: status chip, tool + option rows, 3D view (SyncWorld far from the UI, rendered to a RenderTexture)
        // with Undo / Redo / Reset and Top / Front / Side / 3D / Focus, bottom bar (Objects, Inspector, Live / On release),
        // Objects (hierarchy) and Inspector bottom sheets.
        static AppScreen BuildScene(Transform parent)
        {
            var s = MakeScreen<SceneScreen>(parent, "Scene", ScreenId.Scene);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            // header: title + Link (manual IPs); the connection status chip sits in the subtitle line
            var hdr = Header(s.transform, "Live Scene", "", false, out title, out subT, out backB);
            s.linkButton = CircleButton(hdr, "Link", "link", 1f, -48f, 118f);
            s.chrome.Add(hdr.gameObject);

            var chip = Shape(s.transform, "StatusChip", ColorRole.Card, ShapeKind.Pill);
            chip.GetComponent<ThemedShape>().pillHeight = 76f;
            chip.raycastTarget = false;
            TopBand(chip.rectTransform, 178f, 76f, Margin);
            ShadowFor(chip.rectTransform, CardShadow);
            s.statusDot = Img(chip.transform, "Dot", circle, ColorRole.Warning);
            s.statusDot.GetComponent<ThemedColor>().enabled = false; // SceneScreen sets the colour (searching / connected)
            s.statusDot.raycastTarget = false;
            Place(s.statusDot.rectTransform, 0f, 0.5f, 24f, 24f, 32f, 0f);
            s.statusText = Txt(chip.transform, "Status", "Searching PC / Quest on this Wi-Fi...", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.statusText.rectTransform, 72, 0, 28, 0);
            s.chrome.Add(chip.gameObject);
            s.chrome.Add(chip.transform.parent.Find(chip.name + "Shadow").gameObject);

            // main toolbar (frequent): Select / Move / Rotate / Scale / All, like Unity Q / W / E / R / T
            var toolRow = New("ToolRow", s.transform);
            TopBand(toolRow, 268f, 96f, Margin);
            VLayout(toolRow.gameObject, 0, 0);
            s.toolSeg = Seg(toolRow, "Tools", new[] { "Select", "Move", "Rotate", "Scale", "All" }, 96f, true,
                new[] { "smart_button", "swap_horiz", "refresh", "open_in_full", "view_in_ar" }, null, true);
            s.chrome.Add(toolRow.gameObject);

            // secondary toolbar (compact): Local / Global, Pivot / Center, Snap, Lock
            var optRow = New("OptionRow", s.transform);
            TopBand(optRow, 376f, 72f, Margin);
            HLayout(optRow.gameObject, 10).childForceExpandWidth = false;
            s.spaceSeg = Seg(optRow, "Space", new[] { "Local", "Global" }, 72f, true);
            LE(s.spaceSeg, 72, 300, 0);
            s.pivotSeg = Seg(optRow, "PivotMode", new[] { "Pivot", "Center" }, 72f, true);
            LE(s.pivotSeg, 72, 300, 0);
            Button chipButton;
            s.snapChip = Chip(optRow, "Snap", "Snap", 180f, 72f, out chipButton);
            s.snapButton = chipButton;
            s.snapLongPress = chipButton.gameObject.AddComponent<LongPress>();
            s.lockChip = Chip(optRow, "Lock", "Lock", 180f, 72f, out chipButton);
            s.lockButton = chipButton;
            s.chrome.Add(optRow.gameObject);

            // 3D viewport: the main element, between the toolbars and the bottom bar; full view = almost the whole screen.
            // It shows the scene the connected Unity (PC Play mode / Quest) sends (SceneSync); the camera renders into a
            // RenderTexture of the viewport's own pixel size, so the picture is never stretched.
            var frame = Shape(s.transform, "ViewFrame", ColorRole.Card, ShapeKind.Card);
            var fr = frame.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            s.frameMin = new Vector2(Margin - 16f, 48f + SmallBtnH + 20f);
            s.frameMax = new Vector2(-(Margin - 16f), -464f);
            s.fullMin = new Vector2(16f, 16f);
            s.fullMax = new Vector2(-16f, -16f);
            fr.offsetMin = s.frameMin;
            fr.offsetMax = s.frameMax;
            s.viewFrame = fr;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true; // rounded corners for the 3D view
            var viewRt = New("View", frame.transform);
            Stretch(viewRt);
            var raw = viewRt.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            s.view = raw;
            var input = viewRt.gameObject.AddComponent<SceneViewInput>();
            input.view = raw;
            s.input = input;

            // no scene yet: placeholder (never a fake scene)
            var ph = New("Placeholder", frame.transform);
            Stretch(ph);
            var phBg = Img(ph, "Bg", null, ColorRole.Background);
            Stretch(phBg.rectTransform);
            var phCircle = Img(ph, "IconBg", circle, ColorRole.PrimarySoft);
            phCircle.raycastTarget = false;
            Place(phCircle.rectTransform, 0.5f, 0.5f, 180f, 180f, 0f, 130f);
            var phIcon = Icon(phCircle.transform, "Icon", "view_in_ar", ColorRole.Primary, 100f);
            Place(phIcon.rectTransform, 0.5f, 0.5f, 100f, 100f);
            s.placeholderTitle = CardTitle(ph, "Title", "Waiting for Unity Scene");
            s.placeholderTitle.alignment = TextAlignmentOptions.Center;
            Place(s.placeholderTitle.rectTransform, 0.5f, 0.5f, 900f, 72f, 0f, -10f);
            s.placeholderText = Secondary(ph, "Text", "Searching PC / Quest on this Wi-Fi...");
            s.placeholderText.alignment = TextAlignmentOptions.Center;
            s.placeholderText.enableWordWrapping = true;
            Place(s.placeholderText.rectTransform, 0.5f, 0.5f, 820f, 100f, 0f, -100f);
            s.placeholder = ph.gameObject;

            // viewport overlays: editing (top left), camera (top right column), demo badge + full view (bottom), hint
            s.undoButton = ViewPill(frame.transform, "Undo", "Undo", 0f, 20f, 20f, 116f);
            s.redoButton = ViewPill(frame.transform, "Redo", "Redo", 0f, 144f, 20f, 116f);
            s.resetButton = ViewPill(frame.transform, "Reset", "Reset", 0f, 268f, 20f, 116f);
            s.viewTopButton = ViewPill(frame.transform, "ViewTop", "Top", 1f, -20f, 20f, 116f);
            s.viewFrontButton = ViewPill(frame.transform, "ViewFront", "Front", 1f, -20f, 96f, 116f);
            s.viewSideButton = ViewPill(frame.transform, "ViewSide", "Side", 1f, -20f, 172f, 116f);
            s.view3DButton = ViewPill(frame.transform, "View3D", "3D", 1f, -20f, 248f, 116f);
            s.focusButton = ViewPill(frame.transform, "Focus", "Focus", 1f, -20f, 324f, 116f);

            var hintBg = Shape(frame.transform, "HintPill", ColorRole.Card, ShapeKind.Pill, 0.88f);
            hintBg.GetComponent<ThemedShape>().pillHeight = 60f;
            hintBg.raycastTarget = false;
            BottomBand(hintBg.rectTransform, 18f, 60f, 20f);
            s.hintText = Txt(hintBg.transform, "Hint", "", TextRole.Small, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            Stretch(s.hintText.rectTransform, 20, 0, 20, 0);

            Image demoDot;
            TextMeshProUGUI demoText;
            var demo = StatusChip(frame.transform, "DemoBadge", ColorRole.Warning, "DEMO SCENE: sample objects", 560f, out demoDot, out demoText);
            Place(demo.rectTransform, 0f, 0f, 560f, ChipH, 20f, 92f);
            demo.color = ThemeManager.Get(ColorRole.Card);   // solid, readable over the 3D view
            demo.GetComponent<ThemedColor>().role = ColorRole.Card;
            demo.GetComponent<ThemedColor>().alpha = 0.92f;
            s.demoBadge = demo.gameObject;

            var ex = Img(frame.transform, "FullView", circle, ColorRole.Card, 0.92f);
            Place(ex.rectTransform, 1f, 0f, 88f, 88f, -20f, 88f);
            s.expandIcon = Icon(ex.transform, "Icon", "open_in_full", ColorRole.Text, 44f);
            Place(s.expandIcon.rectTransform, 0.5f, 0.5f, 44f, 44f);
            s.expandSprite = IconS("open_in_full");
            s.collapseSprite = IconS("close");
            s.expandButton = MakeButton(ex.gameObject, ex);

            // bottom bar: Objects / Inspector sheets, Live or On release
            var bar = New("BottomBar", s.transform);
            BottomBand(bar, 48f, SmallBtnH, Margin);
            HLayout(bar.gameObject, 12).childForceExpandWidth = false;
            s.objectsButton = BarButton(bar, "Objects", "view_agenda", "Objects");
            s.inspectorButton = BarButton(bar, "Inspector", "tune", "Inspector");
            s.syncSeg = Seg(bar, "Sync", new[] { "Live", "On release" }, SmallBtnH, true);
            LE(s.syncSeg, SmallBtnH, 480, 0); // wide enough for "On release" on a phone
            s.chrome.Add(bar.gameObject);

            // Hierarchy sheet
            GameObject sheetRoot;
            Button close;
            TextMeshProUGUI sheetTitle;
            var hCard = Sheet(s.transform, "ObjectsSheet", "Objects", 1000f, out sheetRoot, out close, out sheetTitle);
            s.hierarchySheet = sheetRoot;
            s.hierarchyClose = close;
            var hList = ScrollList(hCard, "List", 120f, 20f, false);
            hList.GetComponent<VerticalLayoutGroup>().spacing = 14;
            s.rowTemplate = HierarchyRow(hList);
            s.hierarchyEmpty = Txt(hCard, "Empty", "No objects yet: waiting for the scene from a PC / Quest", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(s.hierarchyEmpty.rectTransform, 150f, 80f, 40f);

            // Inspector sheet
            var iCard = Sheet(s.transform, "InspectorSheet", "Inspector", 1180f, out sheetRoot, out close, out sheetTitle);
            s.inspectorSheet = sheetRoot;
            s.inspectorClose = close;
            s.inspectorTitle = sheetTitle;
            s.inspectorNote = Txt(iCard, "Note", "", TextRole.Small, ColorRole.SubText);
            TopBand(s.inspectorNote.rectTransform, 104f, 44f, 40f);
            var iList = ScrollList(iCard, "Fields", 156f, 16f, false);
            iList.GetComponent<VerticalLayoutGroup>().spacing = 12;
            string[] groups = { "Position (m)", "Rotation (deg)", "Scale" };
            for (int g = 0; g < 3; g++)
            {
                var head = New("Head" + g, iList);
                LE(head, 64);
                HLayout(head.gameObject, 12).childForceExpandWidth = false;
                var ht = Txt(head, "Title", groups[g], TextRole.Body, ColorRole.Text, TextWeight.Heading);
                LE(ht, 64, -1, 1);
                if (g == 2)
                {
                    s.uniformChip = Chip(head, "Uniform", "Uniform", 220f, 64f, out chipButton);
                    s.uniformButton = chipButton;
                }
                for (int a = 0; a < 3; a++) s.fields.Add(FieldRow(iList, g, a));
            }
            var cp = New("CopyPaste", iList);
            LE(cp, 96);
            HLayout(cp.gameObject, 16);
            s.copyButton = BarButton(cp, "Copy", "article", "Copy");
            s.pasteButton = BarButton(cp, "Paste", "note_add", "Paste");

            // the 3D world, far away from everything else; its camera is enabled only while the tab is visible
            var w = SyncWorldBuilder.Build(new Vector3(1000f, 0f, 0f), "SceneCamera", new Color(0.90f, 0.93f, 0.97f));
            w.camera.enabled = false;
            s.worldCamera = w.camera;
            var objects = new GameObject("Objects").transform;
            // the colliders SceneWorldView adds at runtime must be in the scene once, or the IL2CPP build strips them
            // ("AddComponent asking for SphereCollider which is not a Unity engine type" on the phone)
            var keep = new GameObject("KeepColliderTypes");
            keep.transform.SetParent(w.world, false);
            keep.AddComponent<BoxCollider>();
            keep.AddComponent<SphereCollider>();
            keep.AddComponent<CapsuleCollider>();
            keep.SetActive(false);
            objects.SetParent(w.world, false);
            var wv = w.world.gameObject.AddComponent<SceneWorldView>();
            wv.objectsRoot = objects;
            wv.baseMaterial = SyncWorldBuilder.Mat("SyncObjectBase", Color.white, 0.3f);
            wv.outlineMaterial = SyncWorldBuilder.OutlineMat();
            wv.cubeMesh = SyncWorldBuilder.BuiltinMesh("Cube");
            wv.sphereMesh = SyncWorldBuilder.BuiltinMesh("Sphere");
            wv.cylinderMesh = SyncWorldBuilder.BuiltinMesh("Cylinder");
            wv.capsuleMesh = SyncWorldBuilder.BuiltinMesh("Capsule");
            wv.planeMesh = SyncWorldBuilder.BuiltinMesh("Plane");
            s.world = wv;
            var gz = new GameObject("Gizmo");
            gz.transform.SetParent(w.world, false);
            var gizmo = gz.AddComponent<TransformGizmo>();
            gizmo.material = SyncWorldBuilder.GizmoMat();
            gizmo.cylinderMesh = wv.cylinderMesh;
            gizmo.cubeMesh = wv.cubeMesh;
            gizmo.sphereMesh = wv.sphereMesh;
            gizmo.cam = w.camera;
            s.gizmo = gizmo;
            var rig = w.camera.gameObject.AddComponent<OrbitCameraRig>();
            rig.worldRoot = w.world;
            rig.cam = w.camera;
            s.rig = rig;
            input.rig = rig;
            return s;
        }

        // on / off pill (Snap, Lock, Uniform); ChipToggle owns the colours
        static ChipToggle Chip(Transform parent, string name, string label, float w, float h, out Button button)
        {
            var bg = Shape(parent, name, ColorRole.Field, ShapeKind.Pill);
            bg.GetComponent<ThemedShape>().pillHeight = h;
            bg.GetComponent<ThemedColor>().enabled = false;
            LE(bg, h, w, 0);
            button = MakeButton(bg.gameObject, bg);
            var t = Txt(bg.transform, "Label", label, TextRole.Caption, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            t.GetComponent<ThemedText>().applyColor = false;
            Stretch(t.rectTransform, 10, 0, 10, 0);
            var c = bg.gameObject.AddComponent<ChipToggle>();
            c.background = bg;
            c.label = t;
            c.Apply();
            return c;
        }

        // small text pill floating on the 3D view; ax 0 = from the left edge, 1 = from the right edge; y from the top
        static Button ViewPill(Transform frame, string name, string label, float ax, float x, float y, float w)
        {
            var bg = Shape(frame, name, ColorRole.Card, ShapeKind.Pill, 0.92f);
            bg.GetComponent<ThemedShape>().pillHeight = 68f;
            var r = bg.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(ax, 1f);
            r.pivot = new Vector2(ax, 1f);
            r.sizeDelta = new Vector2(w, 68f);
            r.anchoredPosition = new Vector2(x, -y);
            var t = Txt(bg.transform, "Label", label, TextRole.Small, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            return MakeButton(bg.gameObject, bg);
        }

        // flexible-width Tertiary button in a row (Scene bottom bar, Copy / Paste)
        static Button BarButton(Transform parent, string name, string icon, string label)
        {
            var b = Btn(parent, name, BtnKind.Tertiary, icon, label);
            LE(b, SmallBtnH, -1, 1);
            return b;
        }

        // bottom sheet over the lower part of the Scene tab: grabber, title, close button. Returns the card (content parent).
        static RectTransform Sheet(Transform parent, string name, string title, float h, out GameObject root, out Button close, out TextMeshProUGUI titleText)
        {
            var r = New(name, parent);
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(12f, 8f);
            r.offsetMax = new Vector2(-12f, 8f + h);
            ShadowInside(r, 0.22f);
            var card = Shape(r, "Card", ColorRole.Card, ShapeKind.Card);
            Stretch(card.rectTransform);
            var grab = Shape(card.transform, "Grabber", ColorRole.Divider, ShapeKind.Pill);
            grab.GetComponent<ThemedShape>().pillHeight = 12f;
            grab.raycastTarget = false;
            Place(grab.rectTransform, 0.5f, 1f, 120f, 12f, 0f, -18f);
            titleText = Txt(card.transform, "Title", title, TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            var tr = titleText.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.offsetMin = new Vector2(40f, -104f);
            tr.offsetMax = new Vector2(-150f, -36f);
            var hit = Img(card.transform, "Close", null, ColorRole.Card, 0f);
            Place(hit.rectTransform, 1f, 1f, 110f, 110f, -16f, -20f);
            var ic = Icon(hit.transform, "Icon", "close", ColorRole.Text, 56f);
            Place(ic.rectTransform, 0.5f, 0.5f, 56f, 56f);
            close = MakeButton(hit.gameObject, hit);
            root = r.gameObject;
            return card.rectTransform;
        }

        // Inspector value row: [X] [-] [ 1.25 ] [+]   (X = drag to scrub, long press = reset)
        static InspectorField FieldRow(Transform list, int group, int axis)
        {
            var row = New("Field" + "PRS"[group] + "XYZ"[axis], list);
            LE(row, 100);
            HLayout(row.gameObject, 12).childForceExpandWidth = false;
            var f = row.gameObject.AddComponent<InspectorField>();
            f.group = group;
            f.axis = axis;

            var lab = Shape(row, "Axis", ColorRole.Primary, ShapeKind.Field);
            lab.GetComponent<ThemedColor>().enabled = false;
            lab.color = axis == 0 ? new Color(0.90f, 0.27f, 0.23f) : axis == 1 ? new Color(0.30f, 0.66f, 0.18f) : new Color(0.22f, 0.48f, 0.96f);
            LE(lab, 100, 116, 0);
            f.axisLabel = Txt(lab.transform, "Label", "XYZ"[axis].ToString(), TextRole.Button, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            f.axisLabel.GetComponent<ThemedText>().applyColor = false;
            f.axisLabel.color = Color.white;
            Stretch(f.axisLabel.rectTransform);
            f.scrub = lab.gameObject.AddComponent<ScrubArea>();

            var minus = Shape(row, "Minus", ColorRole.Field, ShapeKind.Pill);
            minus.GetComponent<ThemedShape>().pillHeight = 100f;
            LE(minus, 100, 104, 0);
            f.minusButton = MakeButton(minus.gameObject, minus);
            var mt = Txt(minus.transform, "Label", "-", TextRole.Header, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(mt.rectTransform);

            f.input = Input(row, "Value", "0", 100f);
            LE(f.input, 100, -1, 1);
            f.input.contentType = TMP_InputField.ContentType.DecimalNumber;
            f.inputLongPress = f.input.gameObject.AddComponent<LongPress>();
            f.inputLongPress.seconds = 0.6f;

            var plus = Shape(row, "Plus", ColorRole.Field, ShapeKind.Pill);
            plus.GetComponent<ThemedShape>().pillHeight = 100f;
            LE(plus, 100, 104, 0);
            f.plusButton = MakeButton(plus.gameObject, plus);
            var pt = Txt(plus.transform, "Label", "+", TextRole.Header, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(pt.rectTransform);
            return f;
        }

        // Hierarchy row template (inactive): colour dot, name, shape / locked
        static HierarchyRowView HierarchyRow(Transform list)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, "ObjRow", 128f, out card, out btn, out container);
            var v = container.AddComponent<HierarchyRowView>();
            v.button = btn;
            v.highlight = Shape(rt, "Selected", ColorRole.PrimarySoft, ShapeKind.Card);
            v.highlight.raycastTarget = false;
            Stretch(v.highlight.rectTransform);
            v.colorDot = Img(rt, "Dot", circle, ColorRole.Primary);
            v.colorDot.GetComponent<ThemedColor>().enabled = false;
            v.colorDot.raycastTarget = false;
            Place(v.colorDot.rectTransform, 0f, 0.5f, 44f, 44f, 40f, 0f);
            v.nameText = Txt(rt, "Name", "Object", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            Stretch(v.nameText.rectTransform, 112, 60, 40, 14);
            v.subText = Txt(rt, "Sub", "Cube", TextRole.Caption, ColorRole.SubText);
            Stretch(v.subText.rectTransform, 112, 14, 40, 64);
            container.SetActive(false);
            return v;
        }

        static AppScreen BuildSettings(Transform parent)
        {
            var s = MakeScreen<SettingsScreen>(parent, "Settings", ScreenId.Settings);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            Header(s.transform, "Settings", "Account, app, network and connection", false, out title, out subT, out backB);

            var list = ScrollList(s.transform, "List", HeadH + 4f, 20f);
            TextMeshProUGUI sub;
            // account: who is logged in + Log out (back to the role selection)
            TextMeshProUGUI accName;
            SettingRow(list, "cast_connected", "Account", "Admin", out sub);
            var accRow = list.GetChild(list.childCount - 1);
            accName = accRow.Find("Card/Title").GetComponent<TextMeshProUGUI>();
            accRow.Find("Card").GetComponent<Button>().enabled = false;   // information only
            Object.DestroyImmediate(accRow.Find("Card/Chevron").gameObject);
            s.accountName = accName;
            s.accountSub = sub;
            s.logoutRow = SettingRow(list, "swap_horiz", "Log out", "Back to the role selection", out sub);
            s.logoutSub = sub;
            s.demoRow = SettingRow(list, "smart_display", "Demo mode", "ON: sample Quests and sessions, no network", out sub);
            s.demoSub = sub;
            s.customizationRow = SettingRow(list, "palette", "App Customization", "Color, Text, Buttons, Layout", out sub);
            s.networkRow = SettingRow(list, "wifi", "Network", "Discovery (UDP 8766)", out sub);
            s.connectionRow = SettingRow(list, "link", "Connection", "Manual IP Connect", out sub);
            s.notificationRow = SettingRow(list, "notifications", "Notification", "Enable / Disable", out sub);
            s.notificationSub = sub;
            s.aboutRow = SettingRow(list, "info", "About", "MediSimVR v1.0", out sub);
            s.aboutSub = sub;
            return s;
        }

        static Button SettingRow(Transform list, string icon, string title, string sub, out TextMeshProUGUI subText)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, title.Replace(" ", "") + "Row", 160f, out card, out btn, out container);
            // icon circle | title + secondary line | chevron (same structure as the other list rows)
            var tileBg = Img(rt, "IconBg", circle, ColorRole.PrimarySoft);
            tileBg.raycastTarget = false;
            Place(tileBg.rectTransform, 0f, 0.5f, 88f, 88f, CardPad, 0f);
            var ic = Icon(tileBg.transform, "Icon", icon, ColorRole.Primary, 50f);
            Place(ic.rectTransform, 0.5f, 0.5f, 50f, 50f);
            var t = RowTitle(rt, "Title", title);
            t.rectTransform.anchorMin = new Vector2(0, 0.5f);
            t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(150, 0);
            t.rectTransform.offsetMax = new Vector2(-100, -14);
            subText = Secondary(rt, "Subtitle", sub);
            subText.rectTransform.anchorMin = new Vector2(0, 0);
            subText.rectTransform.anchorMax = new Vector2(1, 0.5f);
            subText.rectTransform.offsetMin = new Vector2(150, 14);
            subText.rectTransform.offsetMax = new Vector2(-100, 0);
            Chevron(rt);
            return btn;
        }

        static AppScreen BuildNotifications(Transform parent)
        {
            var s = MakeScreen<NotificationsScreen>(parent, "Notifications", ScreenId.Notifications);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            var hdr = Header(s.transform, "Notifications", "Connection, transfer and playback", true, out title, out subT, out backB);
            s.backButton = backB;
            s.clearButton = CircleButton(hdr, "Clear", "delete", 1f, -48f, 118f);

            var list = ScrollList(s.transform, "List", HeadH + 10f, 20f);
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, "NotificationRow", 150f, out card, out btn, out container);
            btn.enabled = false;
            var v = container.AddComponent<NotificationRowView>();
            v.info = IconS("info");
            v.ok = IconS("check");
            v.warn = IconS("warning");
            v.error = IconS("error");
            v.icon = Icon(rt, "Icon", "info", ColorRole.Primary, 60f);
            v.icon.GetComponent<ThemedColor>().enabled = false;
            Place(v.icon.rectTransform, 0f, 0.5f, 60f, 60f, 40f, 0f);
            v.device = Txt(rt, "Device", "Quest-01", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            v.device.rectTransform.anchorMin = new Vector2(0, 0.5f);
            v.device.rectTransform.anchorMax = new Vector2(1, 1);
            v.device.rectTransform.offsetMin = new Vector2(130, 0);
            v.device.rectTransform.offsetMax = new Vector2(-150, -18);
            v.message = Txt(rt, "Message", "Connected", TextRole.Caption, ColorRole.SubText);
            v.message.rectTransform.anchorMin = new Vector2(0, 0);
            v.message.rectTransform.anchorMax = new Vector2(1, 0.5f);
            v.message.rectTransform.offsetMin = new Vector2(130, 18);
            v.message.rectTransform.offsetMax = new Vector2(-40, 0);
            v.time = Txt(rt, "Time", "now", TextRole.Small, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.TopRight);
            Place(v.time.rectTransform, 1f, 1f, 140f, 50f, -36f, -22f);
            s.rowTemplate = v;
            container.SetActive(false);

            s.emptyText = Txt(s.transform, "Empty", "No notifications", TextRole.Body, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(s.emptyText.rectTransform, 700f, 80f);
            s.emptyText.gameObject.SetActive(false);
            return s;
        }

        static AppScreen BuildEditApp(Transform parent)
        {
            var s = MakeScreen<EditAppScreen>(parent, "EditApp", ScreenId.EditApp);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            var hdr = Header(s.transform, "Edit App", "Theme, colors, text, buttons, layout", true, out title, out subT, out backB);
            s.backButton = backB;
            s.resetButton = CircleButton(hdr, "Reset", "restart_alt", 1f, -48f, 118f);

            // categories (horizontal scroll)
            var catScroll = New("Categories", s.transform);
            TopBand(catScroll, HeadH + 6f, 110f, 0f);
            var sr = catScroll.gameObject.AddComponent<ScrollRect>();
            sr.vertical = false;
            var vp = Img(catScroll, "Viewport", null, ColorRole.Card, 0f);
            Stretch(vp.rectTransform);
            vp.gameObject.AddComponent<RectMask2D>();
            var cc = New("Content", vp.transform);
            cc.anchorMin = new Vector2(0, 0);
            cc.anchorMax = new Vector2(0, 1);
            cc.pivot = new Vector2(0, 0.5f);
            var catHl = cc.gameObject.AddComponent<HorizontalLayoutGroup>();
            catHl.padding = new RectOffset(40, 40, 0, 0);
            catHl.childControlWidth = true;
            catHl.childControlHeight = true;
            catHl.childForceExpandWidth = false;
            cc.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vp.rectTransform;
            sr.content = cc;
            s.categories = Seg(cc, "Tabs", new[] { "Theme", "Colors", "Text", "Buttons", "Layout", "Size & Spacing" }, 100f, false,
                new[] { "palette", "water_drop", "title", "smart_button", "view_agenda", "open_in_full" },
                new[] { 230f, 240f, 200f, 260f, 240f, 360f });
            var tabsRt = (RectTransform)s.categories.transform;
            LE(tabsRt, 100, 1530);

            float top = HeadH + 130f;
            // ---- Theme
            var p = ScrollList(s.transform, "ThemePanel", top, 20f);
            var sec = Section(p, "Theme Presets");
            var grid = New("Grid", sec);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(270f, 240f);
            gl.spacing = new Vector2(24f, 24f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            foreach (string name in ThemeData.PresetNames)
            {
                var cell = New(name, grid);
                var tv = cell.gameObject.AddComponent<PresetTileView>();
                var sw = Shape(cell, "Swatch", ColorRole.OnPrimary, ShapeKind.Field);
                sw.GetComponent<ThemedColor>().enabled = false;
                sw.color = Color.white;
                TopBand(sw.rectTransform, 0f, 170f, 0f);
                tv.swatch = Gradient(sw);
                tv.swatch.useTheme = false;
                var pr = ThemeData.FromPreset(name);
                tv.swatch.colorA = pr.Secondary;
                tv.swatch.colorB = pr.Primary;
                tv.button = MakeButton(sw.gameObject, sw);
                tv.label = Txt(cell, "Label", name, TextRole.Caption, ColorRole.Text, TextWeight.Regular, TextAlignmentOptions.Center);
                BottomBand(tv.label.rectTransform, 0f, 60f, 0f);
                var chk = Img(sw.transform, "Check", circle, ColorRole.OnPrimary);
                chk.raycastTarget = false;
                Place(chk.rectTransform, 1f, 1f, 52f, 52f, -12f, -12f);
                var ck = Icon(chk.transform, "Icon", "check", ColorRole.Primary, 40f);
                Place(ck.rectTransform, 0.5f, 0.5f, 40f, 40f);
                tv.check = chk.gameObject;
                s.presetTiles.Add(tv);
            }
            s.panels.Add(p.parent.parent.gameObject);

            // ---- Colors
            p = ScrollList(s.transform, "ColorsPanel", top, 20f);
            sec = Section(p, "App Colors");
            grid = New("Grid", sec);
            gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(270f, 210f);
            gl.spacing = new Vector2(24f, 24f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            foreach (string name in new[] { "Primary", "Secondary", "Background", "Card", "Text", "Accent" })
            {
                var cell = New(name, grid);
                var cv = cell.gameObject.AddComponent<ColorSwatchView>();
                var border = Shape(cell, "Border", ColorRole.Divider, ShapeKind.Field);
                TopBand(border.rectTransform, 0f, 140f, 0f);
                cv.swatch = Shape(border.transform, "Swatch", ColorRole.Primary, ShapeKind.Field);
                cv.swatch.GetComponent<ThemedColor>().enabled = false;
                Stretch(cv.swatch.rectTransform, 4, 4, 4, 4);
                cv.button = MakeButton(border.gameObject, cv.swatch);
                cv.label = Txt(cell, "Label", name, TextRole.Caption, ColorRole.Text, TextWeight.Regular, TextAlignmentOptions.Center);
                BottomBand(cv.label.rectTransform, 0f, 60f, 0f);
                s.colorSwatches.Add(cv);
            }
            var hint = Txt(sec, "Hint", "Tap a colour to change it. Theme becomes \"Custom\".", TextRole.Small, ColorRole.SubText);
            LE(hint, 50);
            s.panels.Add(p.parent.parent.gameObject);

            // ---- Text
            p = ScrollList(s.transform, "TextPanel", top, 20f);
            sec = Section(p, "Text Size");
            s.textSize = Seg(sec, "TextSize", new[] { "S", "M", "L" }, 100f, true);
            sec = Section(p, "Headings");
            var row = New("BoldRow", sec);
            LE(row, 90);
            var bl = Txt(row, "Label", "Bold headings", TextRole.Body, ColorRole.Text);
            Stretch(bl.rectTransform, 0, 0, 160, 0);
            s.boldHeadings = Switch(row, "BoldSwitch");
            Place((RectTransform)s.boldHeadings.transform, 1f, 0.5f, 120f, 68f);
            var sample = Txt(sec, "Sample", "Sample heading", TextRole.Header, ColorRole.Text, TextWeight.Heading);
            LE(sample, 80);
            s.panels.Add(p.parent.parent.gameObject);

            // ---- Buttons
            p = ScrollList(s.transform, "ButtonsPanel", top, 20f);
            sec = Section(p, "Button Style");
            s.buttonStyle = Seg(sec, "ButtonStyle", new[] { "Rounded", "Pill", "Square" }, 100f, true);
            sec = Section(p, "Button Size");
            s.buttonSize = Seg(sec, "ButtonSize", new[] { "S", "M", "L" }, 100f, true);
            sec = Section(p, "Corner Radius");
            TextMeshProUGUI val;
            s.cornerRadius = MakeSlider(sec, "CornerRadius", 0, 40, out val);
            s.cornerLabel = val;
            sec = Section(p, "Preview");
            TextMeshProUGUI pl;
            var preview = Btn(sec, "PreviewButton", BtnKind.Primary, "play_arrow", "Preview", out pl);   // the app's primary button
            s.panels.Add(p.parent.parent.gameObject);

            // ---- Layout
            p = ScrollList(s.transform, "LayoutPanel", top, 20f);
            sec = Section(p, "List Density");
            s.density = Seg(sec, "Density", new[] { "Compact", "Normal", "Comfortable" }, 100f, true);
            var dh = Txt(sec, "Hint", "Row height of device, video and setting lists.", TextRole.Small, ColorRole.SubText);
            LE(dh, 50);
            s.panels.Add(p.parent.parent.gameObject);

            // ---- Size & Spacing
            p = ScrollList(s.transform, "SpacingPanel", top, 20f);
            sec = Section(p, "List Spacing");
            s.listGap = MakeSlider(sec, "ListGap", 8, 40, out val);
            s.gapLabel = val;
            sec = Section(p, "Screen Padding");
            s.screenPadding = MakeSlider(sec, "ScreenPadding", 16, 72, out val);
            s.paddingLabel = val;
            s.panels.Add(p.parent.parent.gameObject);
            for (int i = 1; i < s.panels.Count; i++) s.panels[i].SetActive(false);
            return s;
        }

        // ===================================================================== chrome
        static void BuildNav(Transform safe, Navigator nav)
        {
            var holder = New("BottomNav", safe);
            nav.navBar = holder.gameObject;
            holder.anchorMin = new Vector2(0, 0);
            holder.anchorMax = new Vector2(1, 0);
            holder.pivot = new Vector2(0.5f, 0);
            holder.offsetMin = new Vector2(24, 20);
            holder.offsetMax = new Vector2(-24, NavH - 24);
            ShadowInside(holder, 0.12f);
            var bar = Shape(holder, "Bar", ColorRole.Card, ShapeKind.Card);
            Stretch(bar.rectTransform);
            var items = New("Items", bar.transform);
            Stretch(items, 12, 10, 12, 10);
            HLayout(items.gameObject, 8);
            // role-based tabs: Admin = all five; Student = Dashboard, Meetings, Settings (Live and Devices are admin only)
            string[] labels = { "Dashboard", "Meetings", "Live", "Devices", "Settings" };
            string[] icons = { "home", "view_agenda", "smart_display", "vr_headset", "settings" };
            ScreenId[] ids = { ScreenId.Dashboard, ScreenId.Meetings, ScreenId.Live, ScreenId.Devices, ScreenId.Settings };
            bool[] adminOnly = { false, false, true, true, false };
            for (int i = 0; i < labels.Length; i++)
            {
                var hit = Img(items, labels[i], null, ColorRole.Card, 0f);
                var b = MakeButton(hit.gameObject, hit);
                var hl = Shape(hit.transform, "Highlight", ColorRole.PrimarySoft, ShapeKind.Field);
                hl.GetComponent<ThemedColor>().enabled = false;
                hl.raycastTarget = false;
                Stretch(hl.rectTransform, 8, 6, 8, 6);
                hl.gameObject.SetActive(i == 0);
                var ic = Icon(hit.transform, "Icon", icons[i], ColorRole.SubText, IconSize);
                ic.GetComponent<ThemedColor>().enabled = false;
                Place(ic.rectTransform, 0.5f, 1f, IconSize, IconSize, 0f, -18f);
                var t = Txt(hit.transform, "Label", labels[i], TextRole.Small, ColorRole.SubText, TextWeight.SemiBold, TextAlignmentOptions.Center);
                t.GetComponent<ThemedText>().applyColor = false;
                BottomBand(t.rectTransform, 14f, 44f, 0f);
                nav.navItems.Add(new NavItem { target = ids[i], button = b, icon = ic, label = t, highlight = hl, adminOnly = adminOnly[i] });
            }
        }

        static Toast BuildToast(Transform overlay)
        {
            var root = New("Toast", overlay);
            var t = root.gameObject.AddComponent<Toast>();
            var pill = Shape(root, "Pill", ColorRole.Text, ShapeKind.Pill);
            pill.GetComponent<ThemedColor>().enabled = false;
            pill.color = new Color(0.12f, 0.16f, 0.22f, 0.92f);
            pill.raycastTarget = false;
            Place(pill.rectTransform, 0.5f, 0f, 900f, 104f, 0f, NavH + 210f);
            var tx = Txt(pill.transform, "Text", "", TextRole.Caption, ColorRole.OnPrimary, TextWeight.Regular, TextAlignmentOptions.Center);
            tx.GetComponent<ThemedText>().role = ColorRole.OnPrimary;
            Stretch(tx.rectTransform, 30, 0, 30, 0);
            t.root = pill.gameObject;
            t.text = tx;
            return t;
        }

        static Popup BuildPopup(Transform overlay)
        {
            var root = New("Popup", overlay);
            Stretch(root);
            var p = overlay.gameObject.AddComponent<Popup>();
            var dim = Img(root, "Dim", null, ColorRole.Text, 0f);
            dim.GetComponent<ThemedColor>().enabled = false;
            dim.color = new Color(0, 0, 0, 0.45f);
            Stretch(dim.rectTransform);
            p.dimButton = MakeButton(dim.gameObject, dim);
            var card = Shape(root, "Card", ColorRole.Card, ShapeKind.Card);
            Place(card.rectTransform, 0.5f, 0.5f, 920f, 0f);
            var v = VLayout(card.gameObject, 48, 28);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            p.title = CardTitle(card.transform, "Title", "Title");
            LE(p.title, 72);
            p.message = BodyText(card.transform, "Message", "Message", ColorRole.SubText);
            p.message.enableWordWrapping = true;
            p.message.overflowMode = TextOverflowModes.Overflow;
            p.input = Input(card.transform, "Input", "", 110f);
            LE(p.input, 110);
            // Cancel = Secondary, OK = Primary (or solid red for destructive confirmations: End session, Leave, Switch)
            var buttons = New("Buttons", card.transform);
            LE(buttons, BtnH);
            HLayout(buttons.gameObject, CardGap);
            TextMeshProUGUI lbl;
            p.cancelButton = Btn(buttons, "Cancel", BtnKind.Secondary, null, "Cancel", out lbl);
            p.cancelLabel = lbl;
            p.okButton = Btn(buttons, "Ok", BtnKind.Primary, null, "OK", out lbl);
            p.okLabel = lbl;
            p.okColor = p.okButton.GetComponent<ThemedColor>();
            p.okGradient = p.okButton.GetComponent<UIGradient>();
            p.root = root.gameObject;
            root.gameObject.SetActive(false);
            return p;
        }

        static ActionSheet BuildActionSheet(Transform overlay)
        {
            var root = New("ActionSheet", overlay);
            Stretch(root);
            var a = overlay.gameObject.AddComponent<ActionSheet>();
            var dim = Img(root, "Dim", null, ColorRole.Text, 0f);
            dim.GetComponent<ThemedColor>().enabled = false;
            dim.color = new Color(0, 0, 0, 0.45f);
            Stretch(dim.rectTransform);
            a.dimButton = MakeButton(dim.gameObject, dim);
            var card = Shape(root, "Sheet", ColorRole.Card, ShapeKind.Card);
            card.rectTransform.anchorMin = new Vector2(0, 0);
            card.rectTransform.anchorMax = new Vector2(1, 0);
            card.rectTransform.pivot = new Vector2(0.5f, 0);
            card.rectTransform.offsetMin = new Vector2(24, 24);
            card.rectTransform.offsetMax = new Vector2(-24, 24);
            var v = VLayout(card.gameObject, 36, 18);
            v.padding = new RectOffset(36, 36, 40, 36);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            a.title = Txt(card.transform, "Title", "Select", TextRole.Body, ColorRole.SubText, TextWeight.SemiBold, TextAlignmentOptions.Center);
            LE(a.title, 64);
            for (int i = 0; i < 4; i++)
            {
                var opt = Shape(card.transform, "Option" + i, ColorRole.Field, ShapeKind.Field);
                LE(opt, 120);
                var b = MakeButton(opt.gameObject, opt);
                var ic = Icon(opt.transform, "Icon", "movie", ColorRole.Primary, IconSize);
                Place(ic.rectTransform, 0f, 0.5f, IconSize, IconSize, CardPad + 8f, 0f);
                var t = RowTitle(opt.transform, "Label", "Option");
                Stretch(t.rectTransform, 130, 0, CardPad, 0);
                a.options.Add(b);
                a.labels.Add(t);
                a.icons.Add(ic);
            }
            a.cancelButton = Btn(card.transform, "Cancel", BtnKind.Secondary, null, "Cancel");   // every Cancel looks the same
            foreach (var n in new[] { "movie", "music_note", "image", "article", "note_add", "folder_open", "edit", "delete" })
            {
                var sp = IconS(n);
                if (sp != null) a.iconSprites.Add(sp);
            }
            a.root = root.gameObject;
            root.gameObject.SetActive(false);
            return a;
        }

        static TextEditorPopup BuildTextEditor(Transform overlay)
        {
            var root = New("TextEditor", overlay);
            Stretch(root);
            var e = overlay.gameObject.AddComponent<TextEditorPopup>();
            var bg = Img(root, "Background", null, ColorRole.Background);
            Stretch(bg.rectTransform);
            var hdr = New("Header", root);
            TopBand(hdr, 0f, HeadH, 0f);
            e.header = CardTitle(hdr, "Title", "New Text");
            e.header.alignment = TextAlignmentOptions.Center;
            Stretch(e.header.rectTransform, 260, 0, 260, 0);
            // Cancel = Secondary, Save = Primary (the same buttons as everywhere else)
            e.cancelButton = Btn(hdr, "Cancel", BtnKind.Secondary, null, "Cancel");
            Place((RectTransform)e.cancelButton.transform, 0f, 0.5f, 220f, BtnH, Margin, 0f);
            e.saveButton = Btn(hdr, "Save", BtnKind.Primary, null, "Save");
            Place((RectTransform)e.saveButton.transform, 1f, 0.5f, 220f, BtnH, -Margin, 0f);
            var tl = Txt(root, "TitleLabel", "Title", TextRole.Caption, ColorRole.SubText);
            TopBand(tl.rectTransform, HeadH + 10f, 50f, 56f);
            e.titleInput = Input(root, "TitleInput", "e.g. Safety_Instructions", 110f);
            TopBand((RectTransform)e.titleInput.transform, HeadH + 64f, 110f);
            var cl = Txt(root, "ContentLabel", "Text", TextRole.Caption, ColorRole.SubText);
            TopBand(cl.rectTransform, HeadH + 200f, 50f, 56f);
            e.contentInput = Input(root, "ContentInput", "Type the text to show on the Quest...", 900f);
            var crt = (RectTransform)e.contentInput.transform;
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(1, 1);
            crt.offsetMin = new Vector2(40, 60);
            crt.offsetMax = new Vector2(-40, -(HeadH + 254f));
            e.contentInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            e.contentInput.textComponent.enableWordWrapping = true;
            e.contentInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
            ((TMP_Text)e.contentInput.placeholder).alignment = TextAlignmentOptions.TopLeft;
            ((TMP_Text)e.contentInput.placeholder).enableWordWrapping = true;
            Stretch(e.contentInput.textViewport, 36, 28, 36, 28);
            e.root = root.gameObject;
            root.gameObject.SetActive(false);
            return e;
        }

        static ColorPickerPopup BuildColorPicker(Transform overlay)
        {
            var root = New("ColorPicker", overlay);
            Stretch(root);
            var cp = overlay.gameObject.AddComponent<ColorPickerPopup>();
            var dim = Img(root, "Dim", null, ColorRole.Text, 0f);
            dim.GetComponent<ThemedColor>().enabled = false;
            dim.color = new Color(0, 0, 0, 0.45f);
            Stretch(dim.rectTransform);
            var card = Shape(root, "Card", ColorRole.Card, ShapeKind.Card);
            Place(card.rectTransform, 0.5f, 0.5f, 940f, 0f);
            VLayout(card.gameObject, 48, 26);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cp.title = Txt(card.transform, "Title", "Primary", TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            LE(cp.title, 70);
            cp.preview = Shape(card.transform, "Preview", ColorRole.Primary, ShapeKind.Field);
            cp.preview.GetComponent<ThemedColor>().enabled = false;
            LE(cp.preview, 150);
            TextMeshProUGUI dummy;
            Txt(card.transform, "HueLabel", "Hue", TextRole.Caption, ColorRole.SubText);
            cp.hue = MakeSlider(card.transform, "Hue", 0, 1, out dummy);
            cp.hue.wholeNumbers = false;
            var hueTrack = cp.hue.transform.Find("Track").GetComponent<Image>();
            hueTrack.sprite = hueBar;
            hueTrack.type = Image.Type.Simple;
            hueTrack.GetComponent<ThemedColor>().enabled = false;
            hueTrack.color = Color.white;
            hueTrack.rectTransform.sizeDelta = new Vector2(0, 24);
            cp.hue.fillRect.gameObject.SetActive(false);
            Txt(card.transform, "SatLabel", "Saturation", TextRole.Caption, ColorRole.SubText);
            cp.saturation = MakeSlider(card.transform, "Saturation", 0, 1, out dummy);
            cp.saturation.wholeNumbers = false;
            cp.satTrack = cp.saturation.transform.Find("Track").GetComponent<Image>();
            cp.satTrack.GetComponent<ThemedColor>().enabled = false;
            Txt(card.transform, "ValLabel", "Brightness", TextRole.Caption, ColorRole.SubText);
            cp.value = MakeSlider(card.transform, "Value", 0, 1, out dummy);
            cp.value.wholeNumbers = false;
            cp.valTrack = cp.value.transform.Find("Track").GetComponent<Image>();
            cp.valTrack.GetComponent<ThemedColor>().enabled = false;
            cp.hex = Input(card.transform, "Hex", "#RRGGBB", 110f);
            LE(cp.hex, 110);
            // Cancel = Secondary, Apply = Primary (same as every popup)
            var buttons = New("Buttons", card.transform);
            LE(buttons, BtnH);
            HLayout(buttons.gameObject, CardGap);
            cp.cancelButton = Btn(buttons, "Cancel", BtnKind.Secondary, null, "Cancel");
            cp.okButton = Btn(buttons, "Ok", BtnKind.Primary, null, "Apply");
            cp.root = root.gameObject;
            root.gameObject.SetActive(false);
            return cp;
        }
    }
}
