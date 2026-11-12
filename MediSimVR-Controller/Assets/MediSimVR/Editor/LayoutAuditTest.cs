using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR.EditorTools
{
    // Layout audit (batchmode, no -quit; exit code 0 = no problem):
    //   Unity.exe -batchmode -projectPath <phone project> -executeMethod MediSimVR.EditorTools.LayoutAuditTest.Run -logFile <log>
    // Plays Main.unity in demo mode and lays the real UI out at several phone shapes (1080 wide: 1920 = 16:9, 2160 = 18:9,
    // 2340 = 19.5:9, 2400 = 20:9; the app's CanvasScaler keeps 1080 units across the width, so only the height changes).
    // Every shape runs at text size M and L (Edit App setting): a text box must show its line at both.
    // For every main screen it checks:
    //   - hidden text: a text whose box is too small shows nothing (TextMeshPro + Ellipsis)
    //   - clipped text: a text cut with "..."
    //   - overlap: two texts, or two buttons, drawn on top of each other
    //   - off screen: a button outside the screen area (inside a scroll list: outside its width)
    // plus the design system rules: Home Control Mode = Session HOST card, Receiver Mode = PLAYER card, 5 bottom tabs
    // without Settings / Scene, every primary button BtnH high. Each screen is rendered to layout-screens/<height>/<nn>_<name>.png
    // (git-ignored) for a visual check. The canvas is switched to world space for this, nothing on a device changes.
    [InitializeOnLoad]
    public static class LayoutAuditTest
    {
        const string Tag = "[LAYOUT] ";
        const string RunKey = "MSVR_LAYOUT_AUDIT_RUN";
        const string PrevKey = "MSVR_LAYOUT_AUDIT_PREV";
        static readonly int[] Heights = { 1920, 2160, 2340, 2400 };
        const float Width = 1080f, Tol = 1.5f;

        static bool running;
        static IEnumerator routine;
        static float deadline;
        static int problems, checks, shots, errors;
        static readonly List<string> report = new List<string>();

        static LayoutAuditTest()
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) Begin(); };
            if (EditorApplication.isPlaying) EditorApplication.delayCall += Begin;
        }

        [MenuItem("MediSimVR/Run Layout Audit")]
        public static void Run()
        {
            SessionState.SetInt(PrevKey, PlayerPrefs.GetInt(DemoConfig.PrefKey, 1));
            DemoConfig.Enabled = true;
            EditorSceneManager.OpenScene("Assets/MediSimVR/Scenes/Main.unity");
            SessionState.SetBool(RunKey, true);
            Debug.Log(Tag + "entering Play mode (demo ON)");
            EditorApplication.EnterPlaymode();
        }

        static void Begin()
        {
            if (running) return;
            running = true;
            problems = checks = shots = errors = 0;
            report.Clear();
            stack.Clear();
            deadline = Time.realtimeSinceStartup + 600f;
            Application.logMessageReceived += OnLog;
            routine = Audit();
            EditorApplication.update += Tick;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (msg.StartsWith(Tag)) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; Debug.Log(Tag + "ERROR SEEN: " + msg); }
        }

        // runs the audit like a coroutine (nested IEnumerators run first; 'yield return null' = next editor update)
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

        static void Tick()
        {
            if (!running) return;
            if (Time.realtimeSinceStartup > deadline) { Finish("timeout"); return; }
            if (stack.Count == 0) stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    var inner = top.Current as IEnumerator;
                    if (inner != null) { stack.Push(inner); continue; }
                    return;   // yielded null: wait for the next update
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish("exception: " + e.Message); return; }
            Finish(null);
        }

        static void Finish(string problem)
        {
            running = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.EraseBool(RunKey);
            PlayerPrefs.SetInt(DemoConfig.PrefKey, SessionState.GetInt(PrevKey, 1));
            PlayerPrefs.Save();
            bool ok = problem == null && problems == 0 && errors == 0 && shots > 0;
            Debug.Log(Tag + "RESULT " + shots + " screens checked (" + checks + " checks), " + problems + " problems, " + errors + " errors" +
                      (problem != null ? ", " + problem : "") + (ok ? "  -> PASSED" : "  -> FAILED"));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        static void Fail(string where, string what)
        {
            problems++;
            Debug.Log(Tag + "PROBLEM " + where + ": " + what);
        }

        static void Check(bool ok, string what)
        {
            checks++;
            if (ok) Debug.Log(Tag + "check OK: " + what);
            else Fail("rule", what);
        }

        // ------------------------------------------------------------------ the run

        static Navigator nav;
        static RectTransform canvasRt, screensRt;
        static Camera cam;
        static RenderTexture rt;

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static IEnumerator Audit()
        {
            while (AppRoot.Service == null || UnityEngine.Object.FindObjectOfType<Navigator>() == null) yield return null;
            yield return Wait(0.5f);
            nav = UnityEngine.Object.FindObjectOfType<Navigator>();
            var svc = AppRoot.Service;
            var ses = AppRoot.Session;
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            canvasRt = (RectTransform)canvas.transform;
            screensRt = (RectTransform)canvasRt.Find("SafeArea/Screens");
            Debug.Log(Tag + "started: " + svc.GetType().Name + ", screens " + nav.screens.Count);

            DesignRules();

            // world-space canvas of a chosen size + an orthographic camera that renders only the UI layer
            canvas.renderMode = RenderMode.WorldSpace;
            canvasRt.localScale = Vector3.one;
            canvasRt.position = new Vector3(0f, 20000f, 0f);
            var camGo = new GameObject("LayoutAuditCamera");
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = ~0;   // the canvas root is on the Default layer; nothing else is near y = 20000
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.enabled = false;
            cam.transform.position = canvasRt.position + new Vector3(0f, 0f, -1000f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 3000f;
            var safe = canvasRt.Find("SafeArea").GetComponent<SafeArea>();
            if (safe != null) safe.enabled = false;
            var safeRt = (RectTransform)canvasRt.Find("SafeArea");
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;

            // text size M and L (a user setting in Edit App): every box must fit both
            var baseTheme = ThemeManager.Current.Clone();
            foreach (var size in new[] { SizeStep.M, SizeStep.L })
            {
            var th = baseTheme.Clone();
            th.TextSize = size;
            ThemeManager.Preview(th);   // not saved
            sizeTag = size == SizeStep.M ? "" : "_textL";
            yield return null;
            foreach (int h in Heights)
            {
                canvasRt.sizeDelta = new Vector2(Width, h);
                cam.orthographicSize = h * 0.5f;
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                rt = new RenderTexture(540, h / 2, 24);
                cam.targetTexture = rt;
                int n = 0;

                // host state: session, two Quests selected, CPR_Training.mp4 sent to them
                if (ses != null) { if (ses.Active) ses.End(); ses.StartHost(true); }
                foreach (var d in svc.Devices) d.Selected = d.Id == "q1" || d.Id == "q2";
                svc.ControlFileId = "f2";
                svc.Send(svc.ControlFile);
                yield return Wait(DemoConfig.FakeSendSeconds + 0.4f);

                yield return Visit(h, ++n, "home", () => nav.Tab(ScreenId.Home));
                yield return Visit(h, ++n, "session", () => nav.Tab(ScreenId.Session));
                yield return Visit(h, ++n, "devices", () => { nav.Tab(ScreenId.Session); nav.Push(ScreenId.Devices); });
                yield return Visit(h, ++n, "send", () => nav.Push(ScreenId.Send));
                yield return Visit(h, ++n, "control_top", () => { nav.Push(ScreenId.Control); ScrollTo(1f); });
                yield return Visit(h, ++n, "control_middle", () => ScrollTo(0.5f));
                yield return Visit(h, ++n, "control_bottom", () => ScrollTo(0f));
                yield return Visit(h, ++n, "session_host", () => { nav.Tab(ScreenId.Session); nav.Push(ScreenId.SessionHost); });
                nav.Tab(ScreenId.Control);
                nav.Push(ScreenId.Scene);
                yield return Wait(DemoConfig.SceneLoadSeconds + 0.6f);   // the demo scene arrives
                var scene = nav.Get<SceneScreen>();
                if (scene.Doc != null && scene.Doc.Get("cube") != null) scene.Doc.Select("cube");
                yield return Visit(h, ++n, "scene", () => { });
                yield return Visit(h, ++n, "scene_full_view", () => scene.SetFullView(true));
                scene.SetFullView(false);
                yield return Visit(h, ++n, "settings", () => { nav.Tab(ScreenId.Home); nav.Push(ScreenId.Settings); });
                yield return Visit(h, ++n, "edit_app", () => nav.Push(ScreenId.EditApp));
                yield return Visit(h, ++n, "notifications", () => { nav.Tab(ScreenId.Home); nav.Push(ScreenId.Notifications); });

                // player state: My Quest, audio sent
                if (ses != null && ses.Active) ses.End();
                var my = ControlFlow.MyQuest(svc);
                ControlFlow.UsePlayerQuest(svc, my);
                svc.ControlFileId = "f3";
                svc.Send(svc.ControlFile);
                yield return Wait(DemoConfig.FakeSendSeconds + 0.4f);
                yield return Visit(h, ++n, "player_send", () => { nav.Tab(ScreenId.Session); nav.Push(ScreenId.Send); });
                yield return Visit(h, ++n, "my_quest", () => nav.Push(ScreenId.SessionPlayerPick));
                yield return Visit(h, ++n, "player_control", () => { nav.Tab(ScreenId.Control); ScrollTo(1f); });
                yield return Visit(h, ++n, "session_player", () => { nav.Tab(ScreenId.Session); nav.Push(ScreenId.SessionPlayer); });
                var sum = nav.Get<SessionSummaryScreen>();
                if (ses != null && sum != null) sum.summary = ses.End();
                yield return Visit(h, ++n, "summary", () => nav.Tab(ScreenId.SessionSummary));
                yield return Visit(h, ++n, "popup_end_session", () =>
                {
                    nav.Tab(ScreenId.Home);
                    nav.popup.Show("End session?", "All Quests leave the session and the PC stops recording.", "End session", null, "Cancel", destructive: true);
                });
                nav.popup.Hide();
            }
            }
            ThemeManager.Load();   // the saved theme again
        }

        static string sizeTag = "";

        // scroll the current screen's list (1 = top, 0 = bottom), so every part of a long screen is checked
        static void ScrollTo(float y)
        {
            foreach (var s in nav.screens)
                if (s.id == nav.Current)
                {
                    var sc = s.GetComponentInChildren<ScrollRect>();
                    if (sc != null) { Canvas.ForceUpdateCanvases(); sc.verticalNormalizedPosition = y; }
                }
        }

        // ------------------------------------------------------------------ design system rules (once)

        static void DesignRules()
        {
            var home = nav.Get<HomeScreen>();
            var start = nav.Get<SessionStartScreen>();
            SameCard(home.controlModeButton, start.hostButton, "Home Control Mode and Session HOST use the same primary card");
            SameCard(home.receiverModeButton, start.playerButton, "Home Receiver Mode and Session PLAYER use the same secondary card");
            bool settingsTab = false;
            foreach (var i in nav.navItems) if (i.target == ScreenId.Settings) settingsTab = true;
            string tabs = "";
            foreach (var i in nav.navItems) tabs += i.target + " ";
            Check(tabs == "Home Session Devices Send Control " && !settingsTab, "bottom navigation: Home, Session, Devices, Send, Control (" + tabs.Trim() + ")");
            Check(home.settingsButton != null && home.settingsButton.transform.parent.parent.name == "Header", "Settings button in the Home header");
            var bell = (RectTransform)home.bellButton.transform.parent;
            var set = (RectTransform)home.settingsButton.transform.parent;
            Check(Mathf.Abs(bell.rect.width - set.rect.width) < 0.5f, "Settings and Notifications icon buttons have the same size");

            // every primary (gradient) button has the same height, every Cancel is the secondary style
            int primaries = 0;
            foreach (var g in canvasRt.GetComponentsInChildren<UIGradient>(true))
            {
                var shape = g.GetComponent<ThemedShape>();
                if (shape == null || shape.kind != ShapeKind.Button || g.GetComponent<Button>() == null) continue;
                primaries++;
                float hgt = ((RectTransform)g.transform).rect.height;
                if (Mathf.Abs(hgt - 120f) > 0.5f) Fail("rule", "primary button " + Path(g.transform) + " is " + hgt + " high (design: 120)");
            }
            Check(primaries >= 8, "primary buttons found: " + primaries);
            int cancels = 0, cancelOk = 0;
            foreach (var b in canvasRt.GetComponentsInChildren<Button>(true))
            {
                if (b.name != "Cancel") continue;
                cancels++;
                var c = b.GetComponent<ThemedColor>();
                if (c != null && c.role == ColorRole.Card && b.transform.Find("Border") != null) cancelOk++;
                else Fail("rule", "Cancel button " + Path(b.transform) + " is not the secondary style");
            }
            Check(cancels >= 2 && cancels == cancelOk, "every Cancel button is the secondary style (" + cancelOk + "/" + cancels + ")");
        }

        static void SameCard(Button a, Button b, string what)
        {
            var ra = (RectTransform)a.transform;
            var rb = (RectTransform)b.transform;
            string diff = "";
            if (Mathf.Abs(ra.rect.height - rb.rect.height) > 0.5f) diff += " height " + ra.rect.height + "/" + rb.rect.height;
            if ((a.GetComponent<UIGradient>() != null) != (b.GetComponent<UIGradient>() != null)) diff += " gradient";
            if (a.GetComponent<ThemedColor>().role != b.GetComponent<ThemedColor>().role) diff += " colour role";
            foreach (var child in new[] { "Title", "Subtitle", "IconBg", "IconBg/Icon", "Chevron" })
            {
                var ca = ra.Find(child);
                var cb = rb.Find(child);
                if (ca == null || cb == null) { diff += " missing " + child; continue; }
                var ta = ca.GetComponent<TMP_Text>();
                var tb = cb.GetComponent<TMP_Text>();
                var tha = ca.GetComponent<ThemedText>();
                var thb = cb.GetComponent<ThemedText>();
                if (tha != null && thb != null && (tha.size != thb.size || tha.weight != thb.weight || tha.role != thb.role))
                    diff += " " + child + " text style";
                if (ta != null && tb != null && ta.font != tb.font) diff += " " + child + " font";
                var ka = (RectTransform)ca;
                var kb = (RectTransform)cb;
                if (ka.offsetMin != kb.offsetMin || ka.offsetMax != kb.offsetMax || ka.anchorMin != kb.anchorMin || ka.sizeDelta != kb.sizeDelta)
                    diff += " " + child + " layout";
            }
            Check(diff.Length == 0, what + (diff.Length > 0 ? ":" + diff : ""));
        }

        // ------------------------------------------------------------------ one screen

        static IEnumerator Visit(int h, int n, string name, Action open)
        {
            open();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRt);
            yield return null;
            Canvas.ForceUpdateCanvases();

            AppScreen screen = null;
            foreach (var s in nav.screens) if (s.id == nav.Current) screen = s;
            string where = h + sizeTag + " " + name;
            int before = problems;
            AuditRoot(where, screen != null ? screen.transform : screensRt, true);
            var navBar = canvasRt.Find("SafeArea/BottomNav");
            if (navBar != null) AuditRoot(where + " (nav)", navBar, false);
            var overlay = canvasRt.Find("SafeArea/Overlay");
            if (overlay != null) AuditRoot(where + " (overlay)", overlay, false);
            checks++;
            shots++;
            Debug.Log(Tag + where + (problems == before ? ": OK" : ": " + (problems - before) + " problem(s)"));

            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../layout-screens/" + h + sizeTag));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(System.IO.Path.Combine(dir, n.ToString("00") + "_" + name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        struct Item { public Rect r; public Transform t; public ScrollRect scroll; public string label; }

        static void AuditRoot(string where, Transform root, bool isScreen)
        {
            Rect screenRect = Local(screensRt);
            var texts = new List<Item>();
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!t.isActiveAndEnabled || t.GetComponentInParent<TMP_InputField>() != null) continue;
                // the source text (a line that does not fit is not in the parsed / rendered text at all)
                string plain = System.Text.RegularExpressions.Regex.Replace(t.text ?? "", "<[^>]*>", "");
                if (plain.Trim().Length == 0) continue;
                t.ForceMeshUpdate();
                var ti = t.textInfo;
                int vis = 0;
                for (int i = 0; i < ti.characterCount; i++) if (ti.characterInfo[i].isVisible) vis++;
                if (vis == 0) { Fail(where, "hidden text \"" + Short(plain) + "\" (box " + t.rectTransform.rect.size + ", " + t.fontSize + " pt) " + Path(t.transform)); continue; }
                if (t.isTextTruncated) Fail(where, "clipped text \"" + plain + "\" " + Path(t.transform));
                var b = t.textBounds;
                var r = Local(t.transform, b.min, b.max);
                var sc = t.GetComponentInParent<ScrollRect>();
                if (sc != null && !Visible(sc, r)) continue;   // scrolled out of view
                texts.Add(new Item { r = r, t = t.transform, scroll = sc, label = plain });
            }
            for (int i = 0; i < texts.Count; i++)
                for (int j = i + 1; j < texts.Count; j++)
                {
                    var a = texts[i];
                    var c = texts[j];
                    if (a.scroll != c.scroll) continue;
                    if (Overlap(a.r, c.r) > 6f) Fail(where, "texts overlap \"" + Short(a.label) + "\" / \"" + Short(c.label) + "\"");
                }

            var buttons = new List<Item>();
            foreach (var s in root.GetComponentsInChildren<Selectable>(false))
            {
                if (!s.isActiveAndEnabled) continue;
                var r = Local((RectTransform)s.transform);
                var sc = s.GetComponentInParent<ScrollRect>();
                if (sc != null)
                {
                    // a vertical list must keep its buttons inside its width; a horizontal scroller (Edit App tabs) scrolls sideways
                    var vp = Local(sc.viewport != null ? sc.viewport : (RectTransform)sc.transform);
                    if (!sc.horizontal && (r.xMin < vp.xMin - Tol || r.xMax > vp.xMax + Tol)) Fail(where, "button wider than its list " + Path(s.transform));
                    if (!Visible(sc, r)) continue;
                }
                else if (isScreen && (r.xMin < screenRect.xMin - Tol || r.xMax > screenRect.xMax + Tol ||
                                      r.yMin < screenRect.yMin - Tol || r.yMax > screenRect.yMax + Tol))
                    Fail(where, "button outside the screen " + Path(s.transform) + " " + r);
                buttons.Add(new Item { r = r, t = s.transform, scroll = sc, label = s.name });
            }
            for (int i = 0; i < buttons.Count; i++)
                for (int j = i + 1; j < buttons.Count; j++)
                {
                    var a = buttons[i];
                    var c = buttons[j];
                    if (a.t.IsChildOf(c.t) || c.t.IsChildOf(a.t)) continue;
                    if (Contains(a.r, c.r) || Contains(c.r, a.r)) continue;   // a button on a card
                    if (Overlap(a.r, c.r) > 16f) Fail(where, "buttons overlap " + Path(a.t) + " / " + Path(c.t));
                }
        }

        // ------------------------------------------------------------------ geometry (canvas units, origin bottom-left)

        static Rect Local(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return Local(r, c[0], c[2], true);
        }

        static Rect Local(Transform t, Vector3 min, Vector3 max, bool world = false)
        {
            Vector3 a = world ? min : t.TransformPoint(min);
            Vector3 b = world ? max : t.TransformPoint(max);
            a = canvasRt.InverseTransformPoint(a);
            b = canvasRt.InverseTransformPoint(b);
            var size = canvasRt.rect.size;
            float x0 = Mathf.Min(a.x, b.x) + size.x * 0.5f, x1 = Mathf.Max(a.x, b.x) + size.x * 0.5f;
            float y0 = Mathf.Min(a.y, b.y) + size.y * 0.5f, y1 = Mathf.Max(a.y, b.y) + size.y * 0.5f;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static bool Visible(ScrollRect sc, Rect r)
        {
            var vp = Local(sc.viewport != null ? sc.viewport : (RectTransform)sc.transform);
            return r.yMax > vp.yMin + 1f && r.yMin < vp.yMax - 1f;
        }

        static float Overlap(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? Mathf.Min(w, h) : 0f;
        }

        static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - Tol && inner.xMax <= outer.xMax + Tol && inner.yMin >= outer.yMin - Tol && inner.yMax <= outer.yMax + Tol;
        }

        static string Short(string s) { s = s.Replace("\n", " "); return s.Length > 40 ? s.Substring(0, 40) + "..." : s; }

        static string Path(Transform t)
        {
            string p = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
