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
    public static class MediSimUIBuilder
    {
        const string Root = "Assets/MediSimVR/";
        const string ScenePath = Root + "Scenes/Main.unity";
        const float NavH = 214f, HeadH = 250f;

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
            root.navigator = nav;

            nav.screens.Add(BuildHome(screensRoot));
            nav.screens.Add(BuildDevices(screensRoot));
            nav.screens.Add(BuildSend(screensRoot));
            nav.screens.Add(BuildControl(screensRoot));
            nav.screens.Add(BuildScene(screensRoot));
            nav.screens.Add(BuildSettings(screensRoot));
            nav.screens.Add(BuildNotifications(screensRoot));
            nav.screens.Add(BuildEditApp(screensRoot));
            for (int i = 1; i < nav.screens.Count; i++) nav.screens[i].gameObject.SetActive(false);

            BuildNav(safe, nav);
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

        // white circle button with shadow, anchored to the top (y = centre distance from the top)
        static Button CircleButton(Transform parent, string name, string icon, float ax, float x, float y)
        {
            var c = New(name, parent);
            c.anchorMin = c.anchorMax = new Vector2(ax, 1f);
            c.pivot = new Vector2(ax, 0.5f);
            c.sizeDelta = new Vector2(124f, 124f);
            c.anchoredPosition = new Vector2(x, -y);
            ShadowInside(c, 0.12f);
            var bg = Img(c, "Circle", circle, ColorRole.Card);
            Stretch(bg.rectTransform);
            var ic = Icon(bg.transform, "Icon", icon, ColorRole.Text, 62f);
            Place(ic.rectTransform, 0.5f, 0.5f, 62f, 62f);
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

        static Button GradientButton(Transform parent, string name, string icon, string label, out TextMeshProUGUI text, bool pill = false)
        {
            var bg = Shape(parent, name, ColorRole.Primary, pill ? ShapeKind.Pill : ShapeKind.Button);
            var sh = bg.GetComponent<ThemedShape>();
            sh.themeHeight = true;
            Gradient(bg);
            var b = MakeButton(bg.gameObject, bg);
            var row = New("Row", bg.transform);
            Stretch(row);
            var h = HLayout(row.gameObject, 18);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = false;
            h.childControlWidth = true;
            h.childControlHeight = false;
            if (icon != null)
            {
                var ic = Icon(row, "Icon", icon, ColorRole.OnPrimary, 56f);
                LE(ic, 56, 56);
            }
            text = Txt(row, "Label", label, TextRole.Button, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            var le = LE(text, 80);
            le.preferredWidth = -1;
            text.enableAutoSizing = false;
            text.rectTransform.sizeDelta = new Vector2(0, 80);
            return b;
        }

        // segmented control; container = rounded field when 'framed'
        static Segmented Seg(Transform parent, string name, string[] labels, float h, bool framed, string[] icons = null, float[] widths = null)
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
                Stretch(row, 12, 0, 12, 0);
                var hl2 = HLayout(row.gameObject, 12);
                hl2.childAlignment = TextAnchor.MiddleCenter;
                hl2.childForceExpandWidth = false;
                hl2.childControlHeight = false;
                Image ic = null;
                if (icons != null && icons[i] != null)
                {
                    ic = Icon(row, "Icon", icons[i], ColorRole.SubText, 48f);
                    ic.GetComponent<ThemedColor>().enabled = false;
                    LE(ic, 48, 48);
                }
                var t = Txt(row, "Label", labels[i], TextRole.Caption, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
                t.GetComponent<ThemedText>().applyColor = false;
                t.rectTransform.sizeDelta = new Vector2(0, 60);
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
        static AppScreen BuildHome(Transform parent)
        {
            var s = MakeScreen<HomeScreen>(parent, "Home", ScreenId.Home);
            Wave(s.transform);
            var hdr = New("Header", s.transform);
            TopBand(hdr, 0f, HeadH, 0f);
            s.bellButton = CircleButton(hdr, "Bell", "notifications", 1f, -48f, 118f);
            var badge = Img(s.bellButton.transform, "Badge", circle, ColorRole.Danger);
            Place(badge.rectTransform, 0.5f, 0.5f, 44f, 44f, 34f, 34f);
            badge.raycastTarget = false;
            s.badge = badge.gameObject;
            s.badgeText = Txt(badge.transform, "Count", "1", TextRole.Small, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(s.badgeText.rectTransform);

            var tile = Shape(s.transform, "LogoTile", ColorRole.Primary, ShapeKind.Card);
            Gradient(tile);
            Place(tile.rectTransform, 0.5f, 1f, 240f, 240f, 0f, -230f);
            tile.raycastTarget = false;
            ShadowFor(tile.rectTransform, 0.30f, ColorRole.Primary);
            var logo = Img(tile.transform, "Logo", logoWhite, ColorRole.OnPrimary);
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            Stretch(logo.rectTransform, 42, 42, 42, 42);

            var title = Txt(s.transform, "AppName", "MediSimVR", TextRole.Title, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            TopBand(title.rectTransform, 500f, 90f);
            var sub = Txt(s.transform, "Tagline", "Quest Media Control", TextRole.Body, ColorRole.Primary, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(sub.rectTransform, 592f, 60f);

            s.controlModeButton = ModeCard(s.transform, "ControlMode", 740f, true, "sports_esports", "Control Mode", "Control Quest device(s)");
            s.receiverModeButton = ModeCard(s.transform, "ReceiverMode", 1010f, false, "headphones", "Receiver Mode", "Run as Receiver (for Quest)");
            return s;
        }

        static Button ModeCard(Transform parent, string name, float y, bool primary, string icon, string title, string sub)
        {
            var card = Shape(parent, name, primary ? ColorRole.Primary : ColorRole.Card, ShapeKind.Card);
            if (primary) Gradient(card);
            TopBand(card.rectTransform, y, 220f, 56f);
            ShadowFor(card.rectTransform, primary ? 0.28f : 0.10f, primary ? ColorRole.Primary : ColorRole.Text);
            var b = MakeButton(card.gameObject, card);
            ColorRole fg = primary ? ColorRole.OnPrimary : ColorRole.Text;
            var ic = Icon(card.transform, "Icon", icon, primary ? ColorRole.OnPrimary : ColorRole.Primary, 96f);
            Place(ic.rectTransform, 0f, 0.5f, 96f, 96f, 56f, 0f);
            var t = Txt(card.transform, "Title", title, TextRole.Header, fg, TextWeight.SemiBold);
            Stretch(t.rectTransform, 190, 110, 110, 30);
            var st = Txt(card.transform, "Subtitle", sub, TextRole.Caption, primary ? ColorRole.OnPrimary : ColorRole.SubText);
            Stretch(st.rectTransform, 190, 36, 110, 112);
            var ch = Icon(card.transform, "Chevron", "chevron_right", primary ? ColorRole.OnPrimary : ColorRole.SubText, 64f);
            Place(ch.rectTransform, 1f, 0.5f, 64f, 64f, -36f, 0f);
            return b;
        }

        static AppScreen BuildDevices(Transform parent)
        {
            var s = MakeScreen<DevicesScreen>(parent, "Devices", ScreenId.Devices);
            Wave(s.transform);
            TextMeshProUGUI title, sub;
            Button back;
            var hdr = Header(s.transform, "Devices", "Manage and connect Quest devices", false, out title, out sub, out back);
            s.refreshButton = CircleButton(hdr, "Refresh", "refresh", 1f, -48f, 118f);
            s.refreshIcon = (RectTransform)s.refreshButton.transform.Find("Icon");

            // single devices bar
            var bar = Shape(s.transform, "DevicesBar", ColorRole.Primary, ShapeKind.Pill);
            bar.GetComponent<ThemedShape>().pillHeight = 124f;
            Gradient(bar);
            bar.raycastTarget = false;
            TopBand(bar.rectTransform, HeadH + 6f, 124f);
            ShadowFor(bar.rectTransform, 0.22f, ColorRole.Primary);
            var qi = Icon(bar.transform, "QuestIcon", "vr_headset", ColorRole.OnPrimary, 64f);
            Place(qi.rectTransform, 0f, 0.5f, 64f, 64f, 56f, 0f);
            s.questsText = Txt(bar.transform, "Quests", "Quests (4)", TextRole.Body, ColorRole.OnPrimary, TextWeight.SemiBold);
            Stretch(s.questsText.rectTransform, 140, 0, 420, 0);
            var pi = Icon(bar.transform, "InUseIcon", "play_arrow", ColorRole.OnPrimary, 56f);
            Place(pi.rectTransform, 1f, 0.5f, 56f, 56f, -270f, 0f);
            s.inUseText = Txt(bar.transform, "InUse", "In Use (1)", TextRole.Body, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.MidlineRight);
            Place(s.inUseText.rectTransform, 1f, 0.5f, 220f, 80f, -48f, 0f);

            var list = ScrollList(s.transform, "List", HeadH + 156f, 260f);
            s.rowTemplate = DeviceRow(list);
            s.rowTemplate.gameObject.SetActive(false);
            s.emptyText = Txt(s.transform, "Empty", "No devices", TextRole.Body, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(s.emptyText.rectTransform, 760f, 80f);
            s.emptyText.gameObject.SetActive(false);

            TextMeshProUGUI lbl;
            s.connectButton = GradientButton(s.transform, "Connect", "link", "Connect (2)", out lbl, true);
            s.connectLabel = lbl;
            Place((RectTransform)s.connectButton.transform, 0.5f, 0f, 660f, 124f, 0f, 70f);
            s.connectButton.GetComponent<ThemedShape>().themeHeight = false;
            s.connectButton.GetComponent<ThemedShape>().pillHeight = 124f;
            ShadowFor((RectTransform)s.connectButton.transform, 0.30f, ColorRole.Primary);
            return s;
        }

        static DeviceRowView DeviceRow(Transform list)
        {
            Image card;
            Button btn;
            GameObject container;
            var rt = Row(list, "DeviceRow", 176f, out card, out btn, out container);
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
            v.nameText = Txt(rt, "Name", "Quest-01", TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            v.nameText.rectTransform.anchorMin = new Vector2(0, 0.5f);
            v.nameText.rectTransform.anchorMax = new Vector2(1, 1);
            v.nameText.rectTransform.offsetMin = new Vector2(320, -6);
            v.nameText.rectTransform.offsetMax = new Vector2(-270, -16);
            v.ipText = Txt(rt, "Ip", "192.168.1.25", TextRole.Caption, ColorRole.SubText);
            v.ipText.rectTransform.anchorMin = new Vector2(0, 0);
            v.ipText.rectTransform.anchorMax = new Vector2(1, 0.5f);
            v.ipText.rectTransform.offsetMin = new Vector2(320, 18);
            v.ipText.rectTransform.offsetMax = new Vector2(-270, -6);
            Divider(rt, -262f, false);

            var status = New("Status", rt);
            Place(status, 1f, 0.5f, 230f, 60f, -24f, 0f);
            v.statusLine = status;
            v.statusDot = Img(status, "Dot", circle, ColorRole.Accent);
            v.statusDot.raycastTarget = false;
            v.statusDot.GetComponent<ThemedColor>().enabled = false;
            Place(v.statusDot.rectTransform, 0f, 0.5f, 24f, 24f, 22f, 0f);
            v.statusText = Txt(status, "Text", "Online", TextRole.Body, ColorRole.Accent, TextWeight.SemiBold);
            v.statusText.GetComponent<ThemedText>().applyColor = false;
            Stretch(v.statusText.rectTransform, 64, 0, 0, 0);

            var badge = Shape(rt, "InUse", ColorRole.Accent, ShapeKind.Pill, 0.18f);
            badge.raycastTarget = false;
            badge.GetComponent<ThemedShape>().pillHeight = 58f;
            Place(badge.rectTransform, 1f, 0.5f, 200f, 58f, -36f, -34f);
            var bi = Icon(badge.transform, "Icon", "play_arrow", ColorRole.Accent, 40f);
            Place(bi.rectTransform, 0f, 0.5f, 40f, 40f, 22f, 0f);
            var bt = Txt(badge.transform, "Label", "In Use", TextRole.Caption, ColorRole.Accent, TextWeight.SemiBold);
            Stretch(bt.rectTransform, 72, 0, 10, 0);
            v.inUseBadge = badge.gameObject;
            return v;
        }

        // thin vertical divider inside a row; x >= 0 from the left, x < 0 from the right
        static void Divider(RectTransform row, float x, bool fromLeft)
        {
            var d = Img(row, "Divider", null, ColorRole.Divider);
            d.raycastTarget = false;
            Place(d.rectTransform, fromLeft ? 0f : 1f, 0.5f, 2f, 84f, x, 0f);
        }

        static AppScreen BuildSend(Transform parent)
        {
            var s = MakeScreen<SendScreen>(parent, "Send", ScreenId.Send);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            Header(s.transform, "Send", "Send to selected Quests", false, out title, out subT, out backB);
            s.videoIcon = IconS("movie");
            s.audioIcon = IconS("music_note");
            s.imageIcon = IconS("image");
            s.textIcon = IconS("article");

            // one preview card (a Quest shows one item at a time)
            var holder = New("PreviewCard", s.transform);
            holder.anchorMin = new Vector2(0, 0);
            holder.anchorMax = new Vector2(1, 1);
            holder.offsetMin = new Vector2(40, 250);
            holder.offsetMax = new Vector2(-40, -(HeadH + 10f));
            ShadowInside(holder, 0.10f);
            var card = Shape(holder, "Card", ColorRole.Card, ShapeKind.Card);
            Stretch(card.rectTransform);

            // ---- filled
            var filled = New("Filled", card.transform);
            Stretch(filled);
            s.filledGroup = filled.gameObject;
            var prev = Shape(filled, "Preview", ColorRole.OnPrimary, ShapeKind.Field);
            prev.GetComponent<ThemedColor>().enabled = false;
            prev.color = Color.white;
            prev.raycastTarget = false;
            s.previewBackground = Gradient(prev, true);
            s.previewBackground.useTheme = false;
            prev.rectTransform.anchorMin = new Vector2(0, 0);
            prev.rectTransform.anchorMax = new Vector2(1, 1);
            prev.rectTransform.offsetMin = new Vector2(32, 300);
            prev.rectTransform.offsetMax = new Vector2(-32, -32);
            prev.gameObject.AddComponent<RectMask2D>();
            s.previewImage = Img(prev.transform, "Picture", null, ColorRole.OnPrimary);
            s.previewImage.GetComponent<ThemedColor>().enabled = false;
            s.previewImage.color = Color.white;
            s.previewImage.preserveAspect = true;
            s.previewImage.raycastTarget = false;
            Stretch(s.previewImage.rectTransform);
            s.previewIcon = Img(prev.transform, "TypeIcon", IconS("movie"), ColorRole.OnPrimary);
            s.previewIcon.GetComponent<ThemedColor>().enabled = false;
            s.previewIcon.color = new Color(1f, 1f, 1f, 0.92f);
            s.previewIcon.preserveAspect = true;
            s.previewIcon.raycastTarget = false;
            Place(s.previewIcon.rectTransform, 0.5f, 0.5f, 180f, 180f);
            var paper = New("TextPaper", prev.transform);
            Stretch(paper, 28, 28, 28, 28);
            var paperBg = Shape(paper, "Bg", ColorRole.Card, ShapeKind.Field);
            Stretch(paperBg.rectTransform);
            paperBg.raycastTarget = false;
            s.previewText = Txt(paper, "Text", "", TextRole.Body, ColorRole.Text, TextWeight.Regular, TextAlignmentOptions.TopLeft);
            s.previewText.enableWordWrapping = true;
            s.previewText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(s.previewText.rectTransform, 32, 28, 32, 28);
            s.textPaper = paper.gameObject;
            paper.gameObject.SetActive(false);
            var badge = Shape(prev.transform, "DurationBadge", ColorRole.Text, ShapeKind.Pill, 0.75f);
            badge.GetComponent<ThemedColor>().enabled = false;
            badge.color = new Color(0f, 0f, 0f, 0.55f);
            badge.raycastTarget = false;
            Place(badge.rectTransform, 0f, 0f, 130f, 50f, 20f, 20f);
            s.badge = badge.gameObject;
            s.badgeText = Txt(badge.transform, "Time", "05:30", TextRole.Caption, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(s.badgeText.rectTransform);

            s.nameText = Txt(filled, "Name", "Nature_01.mp4", TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            BottomBand(s.nameText.rectTransform, 200f, 72f, 40f);
            s.infoText = Txt(filled, "Info", "Video  \u2022  245 MB  \u2022  05:30", TextRole.Caption, ColorRole.SubText);
            BottomBand(s.infoText.rectTransform, 150f, 52f, 40f);
            var actions = New("Actions", filled);
            BottomBand(actions, 32f, 96f, 32f);
            var ah = HLayout(actions.gameObject, 20);
            ah.childForceExpandWidth = false;
            ah.childAlignment = TextAnchor.MiddleLeft;
            s.changeButton = SmallAction(actions, "Change", "swap_horiz", "Change", ColorRole.Primary, 250f);
            s.editButton = SmallAction(actions, "Edit", "edit", "Edit", ColorRole.Primary, 210f);
            s.removeButton = SmallAction(actions, "Remove", "close", "Remove", ColorRole.Danger, 250f);

            // ---- empty
            var empty = New("Empty", card.transform);
            Stretch(empty);
            s.emptyGroup = empty.gameObject;
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
            var es = Txt(empty, "Hint", "Tap Select to choose a video, audio, image or text", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            Place(es.rectTransform, 0.5f, 0.5f, 860f, 60f, 0f, -110f);
            empty.gameObject.SetActive(false);

            // ---- bottom buttons
            var bar = New("BottomBar", s.transform);
            BottomBand(bar, 60f, 130f, 48f);
            var hl = HLayout(bar.gameObject, 28);
            hl.childAlignment = TextAnchor.MiddleCenter;
            var sel = Shape(bar, "Select", ColorRole.Card, ShapeKind.Pill);
            sel.GetComponent<ThemedShape>().pillHeight = 130f;
            var selBorder = Img(sel.transform, "Border", outline, ColorRole.Primary);
            selBorder.type = Image.Type.Sliced;
            selBorder.raycastTarget = false;
            var bs = selBorder.gameObject.AddComponent<ThemedShape>();
            bs.kind = ShapeKind.Pill;
            bs.pillHeight = 130f;
            Stretch(selBorder.rectTransform);
            s.selectButton = MakeButton(sel.gameObject, sel);
            var srow = New("Row", sel.transform);
            Stretch(srow);
            var shl = HLayout(srow.gameObject, 16);
            shl.childAlignment = TextAnchor.MiddleCenter;
            shl.childForceExpandWidth = false;
            shl.childControlHeight = false;
            var sic = Icon(srow, "Icon", "add", ColorRole.Primary, 56f);
            LE(sic, 56, 56);
            var stx = Txt(srow, "Label", "Select", TextRole.Button, ColorRole.Primary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            stx.rectTransform.sizeDelta = new Vector2(0, 80);
            TextMeshProUGUI lbl;
            s.sendButton = GradientButton(bar, "Send", "send", "Send (2)", out lbl, true);
            s.sendButton.GetComponent<ThemedShape>().themeHeight = false;
            s.sendButton.GetComponent<ThemedShape>().pillHeight = 130f;
            s.sendLabel = lbl;
            return s;
        }

        static Button SmallAction(Transform parent, string name, string icon, string label, ColorRole role, float width)
        {
            var b = Shape(parent, name, ColorRole.Field, ShapeKind.Pill);
            b.GetComponent<ThemedShape>().pillHeight = 96f;
            LE(b, 96, width, 0);
            var btn = MakeButton(b.gameObject, b);
            var ic = Icon(b.transform, "Icon", icon, role, 48f);
            Place(ic.rectTransform, 0f, 0.5f, 48f, 48f, 28f, 0f);
            var t = Txt(b.transform, "Label", label, TextRole.Caption, role, TextWeight.SemiBold);
            Stretch(t.rectTransform, 90, 0, 16, 0);
            return btn;
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

        static AppScreen BuildControl(Transform parent)
        {
            var s = MakeScreen<ControlScreen>(parent, "Control", ScreenId.Control);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            Header(s.transform, "Control", "Selected: Quest-01, Quest-02", false, out title, out subT, out backB);
            s.selectedText = subT;

            var card = Shape(s.transform, "FileCard", ColorRole.Card, ShapeKind.Card);
            TopBand(card.rectTransform, HeadH + 10f, 170f);
            ShadowFor(card.rectTransform);
            s.fileCard = MakeButton(card.gameObject, card);
            var thumb = Shape(card.transform, "Thumb", ColorRole.OnPrimary, ShapeKind.Field);
            thumb.GetComponent<ThemedColor>().enabled = false;
            thumb.color = Color.white;
            thumb.raycastTarget = false;
            s.fileThumb = Gradient(thumb, true);
            s.fileThumb.useTheme = false;
            Place(thumb.rectTransform, 0f, 0.5f, 190f, 122f, 22f, 0f);
            s.fileIcon = Icon(thumb.transform, "TypeIcon", "movie", ColorRole.OnPrimary, 56f);
            s.fileIcon.GetComponent<ThemedColor>().enabled = false;
            s.fileIcon.color = new Color(1f, 1f, 1f, 0.9f);
            Place(s.fileIcon.rectTransform, 0.5f, 0.5f, 56f, 56f);
            s.videoIcon = IconS("movie");
            s.audioIcon = IconS("music_note");
            s.imageIcon = IconS("image");
            s.textIcon = IconS("article");
            s.fileName = Txt(card.transform, "Name", "Nature_01.mp4", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.fileName.rectTransform, 240, 84, 100, 20);
            s.fileInfo = Txt(card.transform, "Info", "Video", TextRole.Caption, ColorRole.SubText);
            Stretch(s.fileInfo.rectTransform, 240, 22, 100, 86);
            Chevron(card.transform);

            // video / audio controls
            var media = New("MediaControls", s.transform);
            Stretch(media);
            s.mediaControls = media.gameObject;
            var grid = New("Buttons", media);
            TopBand(grid, HeadH + 220f, 640f);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(484f, 306f);
            g.spacing = new Vector2(32f, 28f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;
            s.playButton = Tile(grid, "Play", "play_arrow", ColorRole.Accent);
            s.pauseButton = Tile(grid, "Pause", "pause", ColorRole.Primary);
            s.resumeButton = Tile(grid, "Resume", "replay", ColorRole.Primary);
            s.stopButton = Tile(grid, "Stop", "stop", ColorRole.Primary);
            var mute = Shape(media, "MuteRow", ColorRole.Card, ShapeKind.Card);
            TopBand(mute.rectTransform, HeadH + 890f, 150f);
            ShadowFor(mute.rectTransform);
            var mi = Icon(mute.transform, "Icon", "volume_off", ColorRole.Text, 64f);
            Place(mi.rectTransform, 0f, 0.5f, 64f, 64f, 40f, 0f);
            var mt = Txt(mute.transform, "Label", "Mute", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            Stretch(mt.rectTransform, 140, 0, 220, 0);
            s.muteSwitch = Switch(mute.transform, "MuteSwitch");
            Place((RectTransform)s.muteSwitch.transform, 1f, 0.5f, 120f, 68f, -40f, 0f);

            // text controls
            var textC = New("TextControls", s.transform);
            Stretch(textC);
            s.textControls = textC.gameObject;
            var tgrid = New("Buttons", textC);
            TopBand(tgrid, HeadH + 220f, 310f);
            var tg = tgrid.gameObject.AddComponent<GridLayoutGroup>();
            tg.cellSize = new Vector2(484f, 306f);
            tg.spacing = new Vector2(32f, 28f);
            tg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tg.constraintCount = 2;
            tg.childAlignment = TextAnchor.UpperCenter;
            s.showButton = Tile(tgrid, "Show on Quest", "visibility", ColorRole.Accent);
            s.hideButton = Tile(tgrid, "Hide", "visibility_off", ColorRole.Primary);
            textC.gameObject.SetActive(false);

            s.stateText = Txt(s.transform, "State", "", TextRole.Caption, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            TopBand(s.stateText.rectTransform, HeadH + 1060f, 60f);
            return s;
        }

        static Button Tile(Transform grid, string label, string icon, ColorRole circleRole)
        {
            var cell = New(label, grid);
            ShadowInside(cell, 0.10f);
            var card = Shape(cell, "Card", ColorRole.Card, ShapeKind.Card);
            Stretch(card.rectTransform);
            var b = MakeButton(card.gameObject, card);
            var c = Img(card.transform, "Circle", circle, circleRole);
            c.raycastTarget = false;
            Place(c.rectTransform, 0.5f, 1f, 136f, 136f, 0f, -44f);
            var ic = Icon(c.transform, "Icon", icon, ColorRole.OnPrimary, 84f);
            Place(ic.rectTransform, 0.5f, 0.5f, 84f, 84f);
            var t = Txt(card.transform, "Label", label, TextRole.Header, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            BottomBand(t.rectTransform, 30f, 70f, 10f);
            return b;
        }

        // "Live Scene": status chip, 3D view (SyncWorld far from the UI, rendered to a RenderTexture), readout, modes, Save/Load/Reset
        static AppScreen BuildScene(Transform parent)
        {
            var s = MakeScreen<SceneScreen>(parent, "Scene", ScreenId.Scene);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            var hdr = Header(s.transform, "Live Scene", "Move the cube here, it moves on PC and Quest", false, out title, out subT, out backB);
            s.linkButton = CircleButton(hdr, "Link", "link", 1f, -48f, 118f);

            var chip = Shape(s.transform, "StatusChip", ColorRole.Card, ShapeKind.Pill);
            chip.GetComponent<ThemedShape>().pillHeight = 96f;
            chip.raycastTarget = false;
            TopBand(chip.rectTransform, HeadH + 4f, 96f);
            ShadowFor(chip.rectTransform, 0.08f);
            s.statusDot = Img(chip.transform, "Dot", circle, ColorRole.Warning);
            s.statusDot.GetComponent<ThemedColor>().enabled = false; // SceneScreen sets the colour (searching / connected)
            s.statusDot.raycastTarget = false;
            Place(s.statusDot.rectTransform, 0f, 0.5f, 28f, 28f, 40f, 0f);
            s.statusText = Txt(chip.transform, "Status", "Searching PC / Quest on this Wi-Fi...", TextRole.Caption, ColorRole.Text, TextWeight.SemiBold);
            Stretch(s.statusText.rectTransform, 88, 0, 32, 0);

            var frame = Shape(s.transform, "ViewFrame", ColorRole.Card, ShapeKind.Card);
            TopBand(frame.rectTransform, HeadH + 124f, 960f);
            ShadowFor(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true; // rounded corners for the 3D view
            var viewRt = New("View", frame.transform);
            Stretch(viewRt);
            var raw = viewRt.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            s.view = raw;
            var gestures = viewRt.gameObject.AddComponent<CubeGestures>();
            gestures.view = viewRt;
            s.gestures = gestures;
            var hintBg = Shape(frame.transform, "HintPill", ColorRole.Card, ShapeKind.Pill, 0.85f);
            hintBg.GetComponent<ThemedShape>().pillHeight = 64f;
            hintBg.raycastTarget = false;
            BottomBand(hintBg.rectTransform, 18f, 64f, 40f);
            s.hintText = Txt(hintBg.transform, "Hint", "", TextRole.Small, ColorRole.SubText, TextWeight.Regular, TextAlignmentOptions.Center);
            Stretch(s.hintText.rectTransform, 20, 0, 20, 0);

            s.readoutText = Txt(s.transform, "Readout", "", TextRole.Caption, ColorRole.SubText);
            TopBand(s.readoutText.rectTransform, HeadH + 1100f, 150f, 64f);

            var modeRow = New("ModeRow", s.transform);
            TopBand(modeRow, HeadH + 1270f, 120f);
            VLayout(modeRow.gameObject, 0, 0);
            s.modeSeg = Seg(modeRow, "Modes", new[] { "Move", "Rotate", "Scale" }, 120f, true, new[] { "swap_horiz", "refresh", "open_in_full" });

            var bar = New("Buttons", s.transform);
            TopBand(bar, HeadH + 1420f, 96f);
            HLayout(bar.gameObject, 24);
            s.saveButton = SmallAction(bar, "Save", "check", "Save", ColorRole.Primary, 300f);
            s.loadButton = SmallAction(bar, "Load", "folder_open", "Load", ColorRole.Primary, 300f);
            s.resetButton = SmallAction(bar, "Reset", "restart_alt", "Reset", ColorRole.Text, 300f);

            // the 3D world, far away from everything else; its camera is enabled only while the tab is visible
            var w = SyncWorldBuilder.Build(new Vector3(1000f, 0f, 0f), "SceneCamera", new Color(0.90f, 0.93f, 0.97f));
            w.camera.enabled = false;
            s.worldCamera = w.camera;
            s.cube = w.cube;
            gestures.target = w.cube;
            gestures.viewCamera = w.camera;
            return s;
        }

        static AppScreen BuildSettings(Transform parent)
        {
            var s = MakeScreen<SettingsScreen>(parent, "Settings", ScreenId.Settings);
            Wave(s.transform);
            TextMeshProUGUI title, subT;
            Button backB;
            Header(s.transform, "Settings", "App, network and connection", false, out title, out subT, out backB);

            var list = ScrollList(s.transform, "List", HeadH + 10f, 20f);
            TextMeshProUGUI sub;
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
            var tileBg = Shape(rt, "IconTile", ColorRole.PrimarySoft, ShapeKind.Field);
            tileBg.raycastTarget = false;
            Place(tileBg.rectTransform, 0f, 0.5f, 88f, 88f, 32f, 0f);
            var ic = Icon(tileBg.transform, "Icon", icon, ColorRole.Primary, 54f);
            Place(ic.rectTransform, 0.5f, 0.5f, 54f, 54f);
            var t = Txt(rt, "Title", title, TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
            t.rectTransform.anchorMin = new Vector2(0, 0.5f);
            t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(140, 0);
            t.rectTransform.offsetMax = new Vector2(-100, -20);
            subText = Txt(rt, "Subtitle", sub, TextRole.Caption, ColorRole.SubText);
            subText.rectTransform.anchorMin = new Vector2(0, 0);
            subText.rectTransform.anchorMax = new Vector2(1, 0.5f);
            subText.rectTransform.offsetMin = new Vector2(140, 20);
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
            var preview = GradientButton(sec, "PreviewButton", "play_arrow", "Preview", out pl);
            LE(preview, ThemeManager.ButtonHeight);
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
            HLayout(items.gameObject, 6);
            string[] labels = { "Home", "Devices", "Send", "Control", "Scene", "Settings" };
            string[] icons = { "home", "vr_headset", "send", "tune", "view_in_ar", "settings" };
            ScreenId[] ids = { ScreenId.Home, ScreenId.Devices, ScreenId.Send, ScreenId.Control, ScreenId.Scene, ScreenId.Settings };
            for (int i = 0; i < labels.Length; i++)
            {
                var hit = Img(items, labels[i], null, ColorRole.Card, 0f);
                var b = MakeButton(hit.gameObject, hit);
                var hl = Shape(hit.transform, "Highlight", ColorRole.PrimarySoft, ShapeKind.Field);
                hl.GetComponent<ThemedColor>().enabled = false;
                hl.raycastTarget = false;
                Stretch(hl.rectTransform, 6, 4, 6, 4);
                hl.gameObject.SetActive(i == 0);
                var ic = Icon(hit.transform, "Icon", icons[i], ColorRole.SubText, 62f);
                ic.GetComponent<ThemedColor>().enabled = false;
                Place(ic.rectTransform, 0.5f, 1f, 62f, 62f, 0f, -26f);
                var t = Txt(hit.transform, "Label", labels[i], TextRole.Small, ColorRole.SubText, TextWeight.SemiBold, TextAlignmentOptions.Center);
                t.GetComponent<ThemedText>().applyColor = false;
                BottomBand(t.rectTransform, 18f, 48f, 0f);
                nav.navItems.Add(new NavItem { target = ids[i], button = b, icon = ic, label = t, highlight = hl });
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
            p.title = Txt(card.transform, "Title", "Title", TextRole.Header, ColorRole.Text, TextWeight.SemiBold);
            LE(p.title, 70);
            p.message = Txt(card.transform, "Message", "Message", TextRole.Body, ColorRole.SubText);
            p.message.enableWordWrapping = true;
            p.message.overflowMode = TextOverflowModes.Overflow;
            p.input = Input(card.transform, "Input", "", 110f);
            LE(p.input, 110);
            var buttons = New("Buttons", card.transform);
            LE(buttons, ThemeManager.ButtonHeight);
            HLayout(buttons.gameObject, 24);
            var cancel = Shape(buttons, "Cancel", ColorRole.Field, ShapeKind.Button);
            p.cancelButton = MakeButton(cancel.gameObject, cancel);
            p.cancelLabel = Txt(cancel.transform, "Label", "Cancel", TextRole.Button, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(p.cancelLabel.rectTransform);
            var ok = Shape(buttons, "Ok", ColorRole.Primary, ShapeKind.Button);
            Gradient(ok);
            p.okButton = MakeButton(ok.gameObject, ok);
            p.okLabel = Txt(ok.transform, "Label", "OK", TextRole.Button, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(p.okLabel.rectTransform);
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
                LE(opt, 130);
                var b = MakeButton(opt.gameObject, opt);
                var ic = Icon(opt.transform, "Icon", "movie", ColorRole.Primary, 60f);
                Place(ic.rectTransform, 0f, 0.5f, 60f, 60f, 40f, 0f);
                var t = Txt(opt.transform, "Label", "Option", TextRole.Body, ColorRole.Text, TextWeight.SemiBold);
                Stretch(t.rectTransform, 130, 0, 40, 0);
                a.options.Add(b);
                a.labels.Add(t);
                a.icons.Add(ic);
            }
            var cancel = Shape(card.transform, "Cancel", ColorRole.Card, ShapeKind.Field);
            LE(cancel, 120);
            a.cancelButton = MakeButton(cancel.gameObject, cancel);
            var ct = Txt(cancel.transform, "Label", "Cancel", TextRole.Body, ColorRole.Danger, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(ct.rectTransform);
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
            e.header = Txt(hdr, "Title", "New Text", TextRole.Header, ColorRole.Text, TextWeight.Heading, TextAlignmentOptions.Center);
            Stretch(e.header.rectTransform, 240, 0, 240, 0);
            var cancel = Txt(hdr, "Cancel", "Cancel", TextRole.Body, ColorRole.Primary, TextWeight.Regular, TextAlignmentOptions.Center);
            cancel.raycastTarget = true;
            Place(cancel.rectTransform, 0f, 0.5f, 200f, 110f, 24f, 0f);
            e.cancelButton = MakeButton(cancel.gameObject, cancel);
            var save = Txt(hdr, "Save", "Save", TextRole.Body, ColorRole.Primary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            save.raycastTarget = true;
            Place(save.rectTransform, 1f, 0.5f, 170f, 110f, -24f, 0f);
            e.saveButton = MakeButton(save.gameObject, save);
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
            var buttons = New("Buttons", card.transform);
            LE(buttons, ThemeManager.ButtonHeight);
            HLayout(buttons.gameObject, 24);
            var cancel = Shape(buttons, "Cancel", ColorRole.Field, ShapeKind.Button);
            cp.cancelButton = MakeButton(cancel.gameObject, cancel);
            var cl = Txt(cancel.transform, "Label", "Cancel", TextRole.Button, ColorRole.Text, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(cl.rectTransform);
            var ok = Shape(buttons, "Ok", ColorRole.Primary, ShapeKind.Button);
            Gradient(ok);
            cp.okButton = MakeButton(ok.gameObject, ok);
            var ol = Txt(ok.transform, "Label", "Apply", TextRole.Button, ColorRole.OnPrimary, TextWeight.SemiBold, TextAlignmentOptions.Center);
            Stretch(ol.rectTransform);
            cp.root = root.gameObject;
            root.gameObject.SetActive(false);
            return cp;
        }
    }
}
