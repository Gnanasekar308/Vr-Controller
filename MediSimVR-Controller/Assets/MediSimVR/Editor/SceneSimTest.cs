using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MediSimVR.SceneSync;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MediSimVR.EditorTools
{
    // Automated Play-mode test of the Scene tab v2 against the PC sync scene, in ONE Editor process:
    // Main.unity (phone UI) + PCSync.unity (SyncReceiver kind "pc", loaded additively), talking over UDP on 127.0.0.1.
    // Touches are simulated in the 3D view's camera pixels (SceneViewInput.Sim*), the same code path as real fingers
    // after the screen -> view conversion. Optional: runs Tools/Send-SceneSyncTest.ps1 against the PC receiver first.
    //
    // batchmode (no -quit; the test exits Unity itself, exit code 0 = all passed):
    //   Unity.exe -batchmode -projectPath <phone project> -executeMethod MediSimVR.EditorTools.SceneSimTest.Run
    //             [-psScript E:\MediSimVR-Receiver\Tools\Send-SceneSyncTest.ps1] -logFile <log>
    [InitializeOnLoad]
    public static class SceneSimTest
    {
        const string Tag = "[SIM-TEST] ";
        const string RunKey = "MSVR_SCENE_SIM_RUN";
        const string PsKey = "MSVR_SCENE_SIM_PS";
        const string DemoPrevKey = "MSVR_SCENE_SIM_DEMO_PREV";
        const string MainScene = "Assets/MediSimVR/Scenes/Main.unity";

        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static float waitUntil, deadline;
        static Func<bool> waitCond;
        static int passed, failed;
        static bool running;

        static SceneSyncClient Client { get { return screen.Client; } }
        static SceneScreen screen;
        static SceneViewInput input;
        static SyncReceiver receiver;

        static SceneSimTest()
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) Begin(); };
            if (EditorApplication.isPlaying) EditorApplication.delayCall += Begin;
        }

        [MenuItem("MediSimVR/Run Scene Sim Test")]
        public static void Run()
        {
            string ps = "";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-psScript") ps = args[i + 1];
            SessionState.SetString(PsKey, ps);
            // this test talks to the real PC receiver: demo mode OFF while it runs (restored in Finish)
            SessionState.SetInt(DemoPrevKey, PlayerPrefs.GetInt(DemoConfig.PrefKey, 1));
            DemoConfig.Enabled = false;
            MediSimUIBuilder.Build();
            PCSyncSceneBuilder.Build();
            EditorSceneManager.OpenScene(MainScene);
            SessionState.SetBool(RunKey, true);
            Debug.Log(Tag + "entering Play mode");
            EditorApplication.EnterPlaymode();
        }

        static void Begin()
        {
            if (running) return;
            running = true;
            passed = failed = 0;
            stack.Clear();
            stack.Push(Steps());
            deadline = Time.realtimeSinceStartup + 600f;
            EditorApplication.update += Tick;
            Debug.Log(Tag + "started");
        }

        static void Tick()
        {
            if (!running) return;
            float now = Time.realtimeSinceStartup;
            if (now > deadline) { Fail("whole test", "timeout"); Finish(); return; }
            if (waitCond != null)
            {
                bool ok = false;
                try { ok = waitCond(); } catch (Exception) { }
                if (!ok && now < waitUntil) return;
                waitCond = null;
            }
            else if (now < waitUntil) return;

            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    object y = top.Current;
                    if (y is IEnumerator) { stack.Push((IEnumerator)y); continue; }
                    if (y is float) waitUntil = now + (float)y;
                    else if (y is Wait) { var w = (Wait)y; waitCond = w.cond; waitUntil = now + w.timeout; }
                    return; // null / wait: next tick
                }
            }
            catch (Exception e)
            {
                Fail("exception", e.ToString());
            }
            Finish();
        }

        class Wait
        {
            public Func<bool> cond;
            public float timeout;
        }

        static Wait Until(Func<bool> c, float timeout) { return new Wait { cond = c, timeout = timeout }; }

        static void Check(string name, bool ok, string info = "")
        {
            if (ok) { passed++; Debug.Log(Tag + "PASS " + name + (info.Length > 0 ? "  (" + info + ")" : "")); }
            else Fail(name, info);
        }

        static void Fail(string name, string info)
        {
            failed++;
            Debug.LogError(Tag + "FAIL " + name + " :: " + info);
        }

        static void Finish()
        {
            running = false;
            EditorApplication.update -= Tick;
            SessionState.EraseBool(RunKey);
            PlayerPrefs.SetInt(DemoConfig.PrefKey, SessionState.GetInt(DemoPrevKey, 1));
            PlayerPrefs.Save();
            Debug.Log(Tag + "RESULT " + passed + " passed, " + failed + " failed" + (failed == 0 ? "  -> ALL PASSED" : ""));
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        // ---------- helpers ----------

        static Vector2 Px(Vector3 world)
        {
            Vector3 s = screen.worldCamera.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        static Vector3 W(Vector3 local) { return screen.world.ToWorld(local); }

        static IEnumerator Drag(Vector2 from, Vector2 to, bool release = true, int steps = 10)
        {
            var cam = screen.worldCamera;
            var gz = screen.gizmo;
            Debug.Log(Tag + "drag " + from.ToString("F0") + " -> " + to.ToString("F0") + ", view " + cam.pixelWidth + "x" + cam.pixelHeight +
                      ", gizmo " + gz.Tool + " at " + Px(gz.Origin).ToString("F0") + " size " + gz.Size.ToString("F3") + " visible " + gz.Visible +
                      ", pick " + gz.Pick(from, SceneEditController.Tolerance(cam.pixelHeight)) + ", selected " + (screen.Doc.Selected != null ? screen.Doc.Selected.id : "none") +
                      ", input busy " + input.Busy);
            input.SimDown(0, from);
            yield return null;
            for (int i = 1; i <= steps; i++)
            {
                input.SimMove(0, Vector2.Lerp(from, to, i / (float)steps));
                yield return null;
            }
            if (release) input.SimUp(0);
            yield return null;
        }

        static IEnumerator Tap(Vector2 p)
        {
            string hit;
            bool any = screen.world.Pick(screen.worldCamera.ScreenPointToRay(new Vector3(p.x, p.y, 0f)), out hit);
            Debug.Log(Tag + "tap " + p.ToString("F0") + " view " + screen.worldCamera.pixelWidth + "x" + screen.worldCamera.pixelHeight +
                      " ray hits " + (any ? hit : "nothing") + ", input busy " + input.Busy);
            input.SimDown(0, p);
            input.SimUp(0);
            yield return null;
        }

        static SyncObject Pc(string id)
        {
            SyncObject so;
            return receiver.TryGetObject(id, out so) ? so : null;
        }

        // the PC object's pose (relative to its SyncWorld) equals the phone's pose
        static bool PcMatches(SceneObjectState o, float tol = 0.003f)
        {
            var so = Pc(o.id);
            if (so == null) return false;
            Transform t = so.transform;
            return (t.localPosition - o.pose.pos).magnitude < tol && Quaternion.Angle(t.localRotation, o.pose.rot) < 0.3f &&
                   (t.localScale - o.pose.scale).magnitude < tol;
        }

        static string V(Vector3 v) { return v.ToString("F3"); }

        static void Select(string id)
        {
            screen.Doc.Select(id);
            screen.Edit.UpdateGizmo();
        }

        static void SetTool(int tool, bool global)
        {
            screen.toolSeg.Select(SceneScreen.ToolIndex((GizmoTool)tool), true);   // tool bar starts with Select
            screen.spaceSeg.Select(global ? 1 : 0, true);
            Refresh();
        }

        // the gizmo follows the object every frame in SceneScreen.Update; the test may run between two frames,
        // so it places the gizmo itself before computing where a handle is on the screen
        static void Refresh()
        {
            screen.Edit.UpdateGizmo();
            screen.gizmo.UpdateTransform();
        }

        // ---------- the test ----------

        static IEnumerator Steps()
        {
            // --- PC sync scene, additive ---
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(PCSyncSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return Until(() => op.isDone, 15f);
            yield return 0.3f;
            receiver = Object.FindObjectOfType<SyncReceiver>();
            Check("PCSync loaded: SyncReceiver kind pc on SyncWorld, 6 objects", receiver != null && receiver.Kind == SceneSyncProtocol.KindPc &&
                  receiver.name == "SyncWorld" && receiver.ObjectCount == 6, receiver != null ? receiver.name + " " + receiver.Kind + " " + receiver.ObjectCount : "none");
            yield return Until(() => receiver.IsListening, 5f);
            Check("PC receiver listening on UDP " + SceneSyncProtocol.Port, receiver.IsListening, receiver.ListenError ?? "");
            Check("PC name PC-XXXX", receiver.DeviceName.StartsWith("PC-") && receiver.DeviceName.Length == 7, receiver.DeviceName);

            // --- PowerShell test script against the PC receiver (before the phone client starts) ---
            string ps = SessionState.GetString(PsKey, "");
            if (!string.IsNullOrEmpty(ps))
            {
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + ps + "\" -QuestIp 127.0.0.1")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                var proc = Process.Start(psi);
                var outText = new StringBuilder();
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) lock (outText) outText.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (outText) outText.AppendLine("ERR " + e.Data); };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                yield return Until(() => proc.HasExited, 40f);
                bool exited = proc.HasExited;
                if (!exited) proc.Kill();
                proc.WaitForExit(2000);
                string text;
                lock (outText) text = outText.ToString();
                foreach (string line in text.Split('\n')) if (line.Trim().Length > 0) Debug.Log(Tag + "PS> " + line.TrimEnd());
                Check("Send-SceneSyncTest.ps1 against the PC receiver: exit 0 (here, objects, transform, bad sig dropped, old seq ignored)",
                      exited && proc.ExitCode == 0, exited ? "exit " + proc.ExitCode : "did not finish");
                yield return Until(() => !Pc("cube").HasTarget, 5f);
                Check("PC cube moved by the script to (0.6, 1, 0), size 0.6", (Pc("cube").transform.localPosition - new Vector3(0.6f, 1f, 0f)).magnitude < 0.01f &&
                      Mathf.Abs(Pc("cube").transform.localScale.x - 0.6f) < 0.01f, V(Pc("cube").transform.localPosition));
            }
            else Debug.Log(Tag + "no -psScript: PowerShell part skipped");

            // --- phone: Scene tab, manual IP 127.0.0.1 ---
            var nav = Object.FindObjectOfType<Navigator>();
            screen = Object.FindObjectOfType<SceneScreen>(true);
            nav.Tab(ScreenId.Scene);
            input = screen.input;
            Client.SetManualIps("127.0.0.1");
            yield return Until(() => Client.HasScene, 15f);
            Check("phone got the scene from the PC (first receiver = source)", Client.HasScene && Client.SourceName == receiver.DeviceName, Client.SourceName);
            Check("6 objects in the document and in the 3D view", screen.Doc.Objects.Count == 6 && screen.world.Count == 6, screen.Doc.Objects.Count + " / " + screen.world.Count);
            var shapes = new StringBuilder();
            foreach (var o in screen.Doc.Objects) shapes.Append(o.id).Append('=').Append(o.shape).Append(o.locked ? "(locked) " : " ");
            Check("shapes: cube sphere cylinder capsule plane + table=box (locked)", screen.Doc.Get("cube").shape == "cube" && screen.Doc.Get("sphere").shape == "sphere" &&
                  screen.Doc.Get("cylinder").shape == "cylinder" && screen.Doc.Get("capsule").shape == "capsule" && screen.Doc.Get("mat").shape == "plane" &&
                  screen.Doc.Get("table").shape == "box" && screen.Doc.Get("table").locked, shapes.ToString());
            var table = screen.Doc.Get("table");
            Check("table box bounds 1.0 x 0.75 x 0.6 (top 1 x 0.6, legs to the floor)", (table.size - new Vector3(1f, 0.75f, 0.6f)).magnitude < 0.01f, V(table.size));
            var cube = screen.Doc.Get("cube");
            Check("phone cube = PC cube pose (adopted from the source)", PcMatches(cube, 0.01f), V(cube.pose.pos));
            yield return 0.4f;
            Check("status chip lists the PC", screen.statusText.text.Contains(receiver.DeviceName), screen.statusText.text);
            int rows = 0;
            foreach (var r in screen.rowTemplate.transform.parent.GetComponentsInChildren<HierarchyRowView>(false)) rows++;
            Check("Hierarchy shows 6 rows", rows == 6, rows.ToString());
            screen.rig.Finish();
            yield return null;

            // --- tap = select (outline), tap empty = deselect ---
            Vector2 cubePx = Px(W(cube.CenterPoint));
            yield return Tap(cubePx);
            SceneObjectView cv;
            screen.world.TryGetView("cube", out cv);
            Check("tap on the cube selects it, outline on", screen.Doc.Selected == cube && cv.GetComponentInChildren<MeshRenderer>().sharedMaterials.Length == 2);
            yield return 0.5f; // no double tap
            yield return Tap(new Vector2(screen.worldCamera.pixelWidth * 0.5f, screen.worldCamera.pixelHeight * 0.97f));
            Check("tap on empty space deselects", screen.Doc.Selected == null);
            yield return 0.5f;
            screen.rowTemplate.transform.parent.GetComponentsInChildren<HierarchyRowView>(false)[0].button.onClick.Invoke();
            Check("Hierarchy row tap selects (first row = cube)", screen.Doc.Selected == cube);

            // --- Move: X arrow only changes X; PC follows; one undo step ---
            SetTool(0, true);
            var g = screen.gizmo;
            int undo0 = screen.Doc.UndoCount;
            Vector3 p0 = cube.pose.pos;
            Refresh(); Vector2 a = Px(g.Origin + g.Axis(0) * g.Size * 0.9f);
            Vector2 dirX = (Px(g.Origin + g.Axis(0) * g.Size) - Px(g.Origin)).normalized;
            yield return Drag(a, a + dirX * 140f);
            Vector3 d = cube.pose.pos - p0;
            Check("drag X arrow: X changes, Y and Z do not", d.x > 0.05f && Mathf.Abs(d.y) < 1e-4f && Mathf.Abs(d.z) < 1e-4f, V(d));
            Check("one finished drag = one undo step", screen.Doc.UndoCount == undo0 + 1, (screen.Doc.UndoCount - undo0).ToString());
            // touching a handle without moving (or moving less than the dead zone) changes nothing
            Refresh();
            a = Px(g.Origin + g.Axis(0) * g.Size * 0.9f);
            p0 = cube.pose.pos;
            undo0 = screen.Doc.UndoCount;
            yield return 0.5f; // not a double tap
            yield return Drag(a, a + dirX * 4f, true, 2);
            Check("handle touched, finger moved 4 px only: no change, no undo step", (cube.pose.pos - p0).magnitude < 1e-6f && screen.Doc.UndoCount == undo0,
                  V(cube.pose.pos - p0));
            yield return Until(() => PcMatches(cube), 5f);
            Check("PC cube follows (same pose)", PcMatches(cube), V(Pc("cube").transform.localPosition) + " vs " + V(cube.pose.pos));

            // --- Y and Z arrows ---
            p0 = cube.pose.pos;
            Refresh(); a = Px(g.Origin + g.Axis(1) * g.Size * 0.9f);
            Vector2 dirY = (Px(g.Origin + g.Axis(1) * g.Size) - Px(g.Origin)).normalized;
            yield return Drag(a, a + dirY * 100f);
            d = cube.pose.pos - p0;
            Check("drag Y arrow: only Y", d.y > 0.03f && Mathf.Abs(d.x) < 1e-4f && Mathf.Abs(d.z) < 1e-4f, V(d));
            p0 = cube.pose.pos;
            Refresh(); a = Px(g.Origin + g.Axis(2) * g.Size * 0.9f);
            Vector2 dirZ = (Px(g.Origin + g.Axis(2) * g.Size) - Px(g.Origin)).normalized;
            yield return Drag(a, a - dirZ * 100f);
            d = cube.pose.pos - p0;
            Check("drag Z arrow: only Z", d.z < -0.03f && Mathf.Abs(d.x) < 1e-4f && Mathf.Abs(d.y) < 1e-4f, V(d));

            // --- Undo / Redo sync to the PC ---
            Vector3 beforeUndo = cube.pose.pos;
            screen.undoButton.onClick.Invoke();
            Check("Undo restores the previous pose", (cube.pose.pos - (beforeUndo - d)).magnitude < 1e-4f, V(cube.pose.pos));
            yield return Until(() => PcMatches(cube), 5f);
            Check("Undo synced to the PC", PcMatches(cube), V(Pc("cube").transform.localPosition));
            screen.redoButton.onClick.Invoke();
            Check("Redo re-applies", (cube.pose.pos - beforeUndo).magnitude < 1e-4f, V(cube.pose.pos));
            yield return Until(() => PcMatches(cube), 5f);
            Check("Redo synced to the PC", PcMatches(cube));

            // --- free move (centre ball): moves on the camera plane ---
            p0 = cube.pose.pos;
            Refresh(); a = Px(g.Origin);
            yield return Drag(a, a + new Vector2(80f, 60f));
            d = cube.pose.pos - p0;
            Check("centre ball = free move", d.magnitude > 0.05f, V(d));

            // --- Rotate: Y ring turns only around Y; position stays ---
            SetTool(1, true);
            Quaternion r0 = cube.pose.rot;
            p0 = cube.pose.pos;
            Refresh(); Vector3 ringPt = g.Origin + Vector3.ProjectOnPlane(-screen.worldCamera.transform.forward, g.Axis(1)).normalized * TransformGizmo.RingRadius * g.Size;
            a = Px(ringPt);
            Vector2 tangent = (Px(g.Origin + Quaternion.AngleAxis(5f, g.Axis(1)) * (ringPt - g.Origin)) - a).normalized;
            yield return Drag(a, a + tangent * 120f);
            Quaternion dq = cube.pose.rot * Quaternion.Inverse(r0);
            float ang;
            Vector3 axis;
            dq.ToAngleAxis(out ang, out axis);
            Check("Y ring: turns around Y only, position unchanged", ang > 5f && Mathf.Abs(Mathf.Abs(axis.y) - 1f) < 0.01f && (cube.pose.pos - p0).magnitude < 1e-4f,
                  "angle " + ang.ToString("F1") + " axis " + V(axis));
            yield return Until(() => PcMatches(cube), 5f);
            Check("PC cube rotation follows", PcMatches(cube));

            // --- Scale: Z handle changes only Z (rotation set back to 0 first, so the handle points sideways on screen) ---
            screen.Doc.Apply(cube, ScenePose.FromEuler(cube.pose.pos, Vector3.zero, cube.pose.scale), "test");
            SetTool(2, false);
            yield return null;
            Vector3 s0 = cube.pose.scale;
            Refresh(); a = Px(g.Origin + g.Axis(2) * g.Size * TransformGizmo.ScaleCubeAt);
            dirZ = (Px(g.Origin + g.Axis(2) * g.Size) - Px(g.Origin)).normalized;
            yield return Drag(a, a + dirZ * 90f);
            Vector3 ds = cube.pose.scale - s0;
            Check("Z scale handle: only Z", ds.z > 0.05f && Mathf.Abs(ds.x) < 1e-5f && Mathf.Abs(ds.y) < 1e-5f, V(ds));
            s0 = cube.pose.scale;
            Refresh(); a = Px(g.Origin);
            yield return Drag(a, a + new Vector2(60f, 60f));
            Vector3 ratio = new Vector3(cube.pose.scale.x / s0.x, cube.pose.scale.y / s0.y, cube.pose.scale.z / s0.z);
            Check("centre cube = uniform scale", ratio.x > 1.05f && Mathf.Abs(ratio.x - ratio.y) < 1e-3f && Mathf.Abs(ratio.x - ratio.z) < 1e-3f, V(ratio));

            // --- Snap (0.5 m) ---
            screen.SetStep(true, 0.5f);
            screen.snapButton.onClick.Invoke();
            SetTool(0, true);
            p0 = cube.pose.pos;
            Refresh(); a = Px(g.Origin + g.Axis(0) * g.Size * 0.9f);
            dirX = (Px(g.Origin + g.Axis(0) * g.Size) - Px(g.Origin)).normalized;
            yield return Drag(a, a + dirX * 220f);
            float dx = cube.pose.pos.x - p0.x;
            Check("snap on: X moved in 0.5 m steps", screen.Edit.Snap.on && dx > 0.4f && Mathf.Abs(dx / 0.5f - Mathf.Round(dx / 0.5f)) < 1e-3f, dx.ToString("F4"));
            screen.snapButton.onClick.Invoke();
            Check("snap off again", !screen.Edit.Snap.on);
            screen.snapLongPress.Trigger();
            yield return null;
            Check("long press on the magnet opens the step choice", nav.actionSheet.root.activeSelf);
            nav.actionSheet.Hide();
            screen.snapLongPress.ConsumeClick();

            // --- All-in-one: picking of every handle kind ---
            SetTool(3, true);
            float tol = SceneEditController.Tolerance(screen.worldCamera.pixelHeight);
            Refresh(); Check("All: centre = free move", g.Pick(Px(g.Origin), tol) == GizmoHandle.MoveFree);
            Check("All: outer X = move X", g.Pick(Px(g.Origin + g.Axis(0) * g.Size * 0.92f), tol) == GizmoHandle.MoveX);
            Check("All: inner X cube = scale X", g.Pick(Px(g.Origin + g.Axis(0) * g.Size * TransformGizmo.AllScaleAt), tol) == GizmoHandle.ScaleX);
            Refresh(); Vector3 ringAll = g.Origin + Vector3.ProjectOnPlane(-screen.worldCamera.transform.forward, g.Axis(1)).normalized * TransformGizmo.AllRingRadius * g.Size;
            Check("All: ring = rotate Y", g.Pick(Px(ringAll), tol) == GizmoHandle.RotY);
            SetTool(0, false);

            // --- Local vs Global: rotated cube, local X arrow follows the cube ---
            Check("Local: gizmo X axis = cube's own X", Vector3.Angle(g.Axis(0), screen.world.transform.rotation * cube.pose.rot * Vector3.right) < 0.1f);
            SetTool(0, true);
            Check("Global: gizmo X axis = world X", Vector3.Angle(g.Axis(0), Vector3.right) < 0.1f);

            // --- Lock: no accidental moves; lock flag reaches the PC ---
            screen.lockButton.onClick.Invoke();
            Check("Lock on the selected cube", cube.locked && screen.lockChip.isOn);
            p0 = cube.pose.pos;
            Refresh(); a = Px(g.Origin + g.Axis(0) * g.Size * 0.9f);
            yield return Drag(a, a + dirX * 140f);
            Check("locked: dragging the X arrow does nothing", (cube.pose.pos - p0).magnitude < 1e-6f);
            yield return Until(() => Pc("cube").Locked, 5f);
            Check("lock flag synced to the PC (SyncObject.Locked)", Pc("cube").Locked);
            screen.lockButton.onClick.Invoke();
            yield return Until(() => !Pc("cube").Locked, 5f);
            Check("unlock synced", !cube.locked && !Pc("cube").Locked);

            // --- Inspector: type, + / -, scrub, long press reset, bad input, uniform scale ---
            screen.inspectorButton.onClick.Invoke();
            Check("Inspector sheet opens with 9 fields", screen.inspectorSheet.activeSelf && screen.fields.Count == 9 && screen.inspectorTitle.text == "Cube");
            int u = screen.Doc.UndoCount;
            screen.fields[0].SimType("1.5");
            Check("type Position X = 1.5", Mathf.Abs(cube.pose.pos.x - 1.5f) < 1e-5f && screen.Doc.UndoCount == u + 1, V(cube.pose.pos));
            yield return Until(() => PcMatches(cube), 5f);
            Check("typed value synced to the PC", PcMatches(cube));
            float y0 = cube.pose.pos.y;
            screen.fields[1].SimPlus();
            Check("+ on Position Y adds the move step (" + screen.Edit.Snap.move + ")", Mathf.Abs(cube.pose.pos.y - (y0 + screen.Edit.Snap.move)) < 1e-5f, V(cube.pose.pos));
            screen.fields[1].SimMinus();
            Check("- on Position Y goes back", Mathf.Abs(cube.pose.pos.y - y0) < 1e-5f);
            u = screen.Doc.UndoCount;
            float z0 = cube.pose.pos.z;
            screen.fields[2].scrub.SimScrub(250f);
            Check("scrub Position Z label = 250 units -> +1 m, one undo step", Mathf.Abs(cube.pose.pos.z - (z0 + 1f)) < 1e-4f && screen.Doc.UndoCount == u + 1, V(cube.pose.pos));
            screen.fields[0].scrub.SimLongPress();
            Check("long press X label = reset Position X only (0)", Mathf.Abs(cube.pose.pos.x) < 1e-6f && Mathf.Abs(cube.pose.pos.z - (z0 + 1f)) < 1e-4f, V(cube.pose.pos));
            screen.fields[4].SimType("-725");
            Check("rotation is unlimited: Y = -725 deg kept in the Inspector", Mathf.Abs(cube.pose.euler.y + 725f) < 1e-3f &&
                  Quaternion.Angle(cube.pose.rot, Quaternion.Euler(cube.pose.euler)) < 0.01f, V(cube.pose.euler));
            Vector3 before = cube.pose.pos;
            screen.fields[0].SimType("abc");
            Check("bad input 'abc' is ignored", (cube.pose.pos - before).magnitude < 1e-6f);
            screen.fields[6].SimType("-5");
            Check("scale minimum: X = -5 becomes 0.001", Mathf.Abs(cube.pose.scale.x - 0.001f) < 1e-7f, cube.pose.scale.x.ToString("F4"));
            screen.fields[6].SimType("1");
            screen.fields[7].SimType("1");
            screen.fields[8].SimType("1");
            screen.uniformButton.onClick.Invoke();
            screen.fields[6].SimType("2");
            Check("uniform scale: X = 2 scales Y and Z too", (cube.pose.scale - Vector3.one * 2f).magnitude < 1e-4f, V(cube.pose.scale));
            screen.uniformButton.onClick.Invoke();
            screen.fields[0].SimType("400");
            Check("position is unlimited: X = 400 m", Mathf.Abs(cube.pose.pos.x - 400f) < 1e-3f);
            yield return Until(() => PcMatches(cube, 0.01f), 8f);
            Check("far position synced to the PC", PcMatches(cube, 0.01f), V(Pc("cube").transform.localPosition));

            // --- Focus brings a far object back ---
            screen.focusButton.onClick.Invoke();
            screen.rig.Finish();
            yield return null;
            Vector3 vp = screen.worldCamera.WorldToViewportPoint(W(cube.CenterPoint));
            Check("Focus: the far cube is in the middle of the view", vp.z > 0f && Mathf.Abs(vp.x - 0.5f) < 0.05f && Mathf.Abs(vp.y - 0.5f) < 0.05f, V(vp));
            screen.fields[0].SimType("0.5");
            screen.fields[1].SimType("0.3");
            screen.fields[2].SimType("0");

            // --- Copy / Paste to another object ---
            screen.copyButton.onClick.Invoke();
            var sphere = screen.Doc.Get("sphere");
            Select("sphere");
            screen.pasteButton.onClick.Invoke();
            Check("Paste: sphere gets the cube transform", (sphere.pose.pos - cube.pose.pos).magnitude < 1e-5f && (sphere.pose.scale - cube.pose.scale).magnitude < 1e-5f &&
                  Quaternion.Angle(sphere.pose.rot, cube.pose.rot) < 0.01f, V(sphere.pose.pos));
            yield return Until(() => PcMatches(sphere), 5f);
            Check("pasted transform synced to the PC sphere", PcMatches(sphere));
            screen.inspectorClose.onClick.Invoke();

            // --- Reset: all unlocked objects back to the scene start, locked table untouched, one undo step ---
            Vector3 tableBefore = table.pose.pos;
            u = screen.Doc.UndoCount;
            screen.resetButton.onClick.Invoke();
            bool allBack = true;
            foreach (var o in screen.Doc.Objects) if (!o.locked && !o.pose.SameAs(o.initialPose)) allBack = false;
            Check("Reset: every unlocked object at its start pose, one undo step", allBack && screen.Doc.UndoCount == u + 1);
            Check("Reset skips the locked table", (table.pose.pos - tableBefore).magnitude < 1e-6f);
            yield return Until(() => PcMatches(cube) && PcMatches(sphere), 6f);
            Check("Reset synced to the PC", PcMatches(cube) && PcMatches(sphere));
            screen.undoButton.onClick.Invoke();
            Check("Undo of Reset brings the edits back", !sphere.pose.SameAs(sphere.initialPose));
            screen.resetButton.onClick.Invoke();

            // --- at least 50 undo steps ---
            Select("cube");
            for (int i = 0; i < 60; i++) screen.fields[1].SimPlus();
            Check("60 edits -> at least 50 undo steps", screen.Doc.UndoCount >= 60, screen.Doc.UndoCount.ToString());
            float yTop = cube.pose.pos.y;
            for (int i = 0; i < 55; i++) screen.undoButton.onClick.Invoke();
            Check("55 x Undo walks back 55 steps", Mathf.Abs(cube.pose.pos.y - (yTop - 55 * screen.Edit.Snap.move)) < 1e-3f, cube.pose.pos.y.ToString("F3"));
            yield return Until(() => PcMatches(cube), 5f);
            Check("after many undos the PC matches", PcMatches(cube));

            // --- Live vs On release ---
            screen.Edit.FocusSelected();
            screen.rig.Finish();
            yield return null;
            SetTool(0, true);
            screen.syncSeg.Select(1, true);
            Check("On release mode", !screen.LiveSync);
            Vector3 pcBefore = Pc("cube").transform.localPosition;
            Refresh(); a = Px(g.Origin + g.Axis(0) * g.Size * 0.9f);
            dirX = (Px(g.Origin + g.Axis(0) * g.Size) - Px(g.Origin)).normalized;
            yield return Drag(a, a + dirX * 120f, false);
            yield return 0.8f;
            Check("On release: while the finger is down the PC does not move", (Pc("cube").transform.localPosition - pcBefore).magnitude < 1e-4f &&
                  (cube.pose.pos - pcBefore).magnitude > 0.05f, V(Pc("cube").transform.localPosition));
            input.SimUp(0);
            yield return Until(() => PcMatches(cube), 5f);
            Check("On release: after lifting the finger the PC follows", PcMatches(cube));
            screen.syncSeg.Select(0, true);
            pcBefore = Pc("cube").transform.localPosition;
            Refresh(); a = Px(g.Origin + g.Axis(0) * g.Size * 0.9f);
            yield return Drag(a, a + dirX * 120f, false);
            yield return Until(() => (Pc("cube").transform.localPosition - pcBefore).magnitude > 0.05f, 3f);
            Check("Live: the PC moves while the finger is still down", (Pc("cube").transform.localPosition - pcBefore).magnitude > 0.05f);
            input.SimUp(0);
            yield return 0.2f;

            // --- pinch on the selected object = uniform scale ---
            screen.Edit.FocusSelected();
            screen.rig.Finish();
            yield return null;
            s0 = cube.pose.scale;
            cubePx = Px(W(cube.CenterPoint));
            input.SimDown(0, cubePx + new Vector2(-30f, 0f));
            input.SimDown(1, cubePx + new Vector2(30f, 0f));
            for (int i = 1; i <= 8; i++)
            {
                input.SimMove(0, cubePx + new Vector2(-30f - i * 8f, 0f));
                input.SimMove(1, cubePx + new Vector2(30f + i * 8f, 0f));
                yield return null;
            }
            input.SimUp(0);
            input.SimUp(1);
            ratio = new Vector3(cube.pose.scale.x / s0.x, cube.pose.scale.y / s0.y, cube.pose.scale.z / s0.z);
            Check("pinch on the selected cube = uniform scale up", ratio.x > 1.5f && Mathf.Abs(ratio.x - ratio.z) < 1e-3f, V(ratio));
            screen.undoButton.onClick.Invoke();

            // --- camera: orbit, pan, pinch zoom on empty space, double tap focus, view cube ---
            var rig = screen.rig;
            screen.rig.Finish();
            Vector2 empty = new Vector2(screen.worldCamera.pixelWidth * 0.12f, screen.worldCamera.pixelHeight * 0.2f);
            float yaw0 = rig.yaw, pitch0 = rig.pitch;
            yield return Drag(empty, empty + new Vector2(150f, 60f));
            Check("1 finger on empty space = orbit", Mathf.Abs(rig.yaw - yaw0) > 5f && Mathf.Abs(rig.pitch - pitch0) > 2f, rig.yaw.ToString("F1") + " / " + rig.pitch.ToString("F1"));
            Vector3 piv0 = rig.pivot;
            float dist0 = rig.distance;
            input.SimDown(0, empty);
            input.SimDown(1, empty + new Vector2(100f, 0f));
            for (int i = 1; i <= 8; i++)
            {
                input.SimMove(0, empty + new Vector2(-i * 6f, i * 5f));
                input.SimMove(1, empty + new Vector2(100f + i * 6f, i * 5f));
                yield return null;
            }
            input.SimUp(0);
            input.SimUp(1);
            Check("2 fingers = pan", (rig.pivot - piv0).magnitude > 0.01f, V(rig.pivot - piv0));
            Check("pinch out on empty space = zoom in", rig.distance < dist0 * 0.9f, dist0.ToString("F2") + " -> " + rig.distance.ToString("F2"));
            var cyl = screen.Doc.Get("cylinder");
            screen.Doc.Select(null); // no gizmo in the way of the taps
            yield return 0.5f;
            Vector2 cylPx = Px(W(cyl.CenterPoint));
            Vector3 cvp = screen.worldCamera.WorldToViewportPoint(W(cyl.CenterPoint));
            if (cvp.z < 0f || cvp.x < 0.05f || cvp.x > 0.95f || cvp.y < 0.05f || cvp.y > 0.95f)
            {
                rig.Home();
                yield return null;
                cylPx = Px(W(cyl.CenterPoint));
            }
            yield return Tap(cylPx);
            yield return Tap(cylPx);
            rig.Finish();
            Check("double tap on the cylinder = select + focus", screen.Doc.Selected == cyl && (rig.pivot - cyl.CenterPoint).magnitude < 1e-3f, V(rig.pivot));
            screen.viewTopButton.onClick.Invoke(); rig.Finish();
            Check("view Top: looking straight down", Mathf.Abs(rig.pitch - 90f) < 0.01f && Vector3.Angle(screen.worldCamera.transform.forward, Vector3.down) < 0.1f);
            screen.viewFrontButton.onClick.Invoke(); rig.Finish();
            Check("view Front: looking along +Z", Vector3.Angle(screen.worldCamera.transform.forward, Vector3.forward) < 0.1f);
            screen.viewSideButton.onClick.Invoke(); rig.Finish();
            Check("view Side: looking along -X", Vector3.Angle(screen.worldCamera.transform.forward, Vector3.left) < 0.1f);
            screen.view3DButton.onClick.Invoke(); rig.Finish();
            Check("view 3D: back to the angled view", Mathf.Abs(rig.pitch - OrbitCameraRig.HomePitch) < 0.01f && Mathf.Abs(Mathf.DeltaAngle(rig.yaw, OrbitCameraRig.HomeYaw)) < 0.01f);

            // --- a receiver that appears later gets the full state (fake Quest on another port) ---
            using (var fake = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                var phone = new IPEndPoint(IPAddress.Loopback, Client.LocalPort);
                byte[] here = Encoding.UTF8.GetBytes(SceneSyncProtocol.BuildHere("Quest-SIM1", SceneSyncProtocol.KindQuest, 6));
                var got = new Dictionary<string, SceneSyncMessage>();
                float t0 = Time.realtimeSinceStartup, nextHere = 0f;
                while (Time.realtimeSinceStartup - t0 < 6f && got.Count < 6)
                {
                    if (Time.realtimeSinceStartup >= nextHere) { fake.Send(here, here.Length, phone); nextHere = Time.realtimeSinceStartup + 0.5f; }
                    while (fake.Available > 0)
                    {
                        var from = new IPEndPoint(IPAddress.Any, 0);
                        string msg = Encoding.UTF8.GetString(fake.Receive(ref from));
                        SceneSyncMessage m;
                        string err;
                        if (SceneSyncProtocol.TryParse(msg, out m, out err) && m.type == SceneSyncProtocol.TypeTransform && m.sid == Client.Sid) got[m.id] = m;
                    }
                    yield return null;
                }
                bool posesOk = got.Count == 6;
                foreach (var kv in got)
                {
                    var o = screen.Doc.Get(kv.Key);
                    Vector3 gp;
                    if (o == null || !kv.Value.TryGetPos(out gp) || (gp - o.pose.pos).magnitude > 1e-3f || kv.Value.locked != o.locked) posesOk = false;
                }
                Check("late receiver (fake Quest-SIM1) gets all 6 objects with the phone's state", posesOk, got.Count + " objects");
                yield return 0.3f;
                Check("status chip lists PC + Quest", screen.statusText.text.Contains("Quest-SIM1") && screen.statusText.text.Contains(receiver.DeviceName), screen.statusText.text);
            }

            // --- no Save / Load left, nothing written ---
            Check("no cube_state.json written", !System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "cube_state.json")));
        }
    }
}
