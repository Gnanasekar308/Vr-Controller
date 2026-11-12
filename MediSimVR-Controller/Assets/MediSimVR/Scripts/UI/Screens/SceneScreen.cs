using System.Collections.Generic;
using System.Text;
using MediSimVR.SceneSync;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // "Live Scene" v2: edit the objects of the Unity scene on every connected receiver (PC Play mode, Quests) like in Unity.
    //   Hierarchy sheet (tap = select, outline), Move / Rotate / Scale / All-in-one gizmo, Local / Global, Pivot / Center,
    //   Snap (long press = step), Lock, Inspector sheet (type, - / +, scrub, long press = reset, uniform scale, Copy / Paste),
    //   orbit / pan / zoom / focus camera + Top / Front / Side / 3D, Undo / Redo (one step per finished edit), Reset,
    //   "Live" (send while dragging) or "On release". The first receiver that answers is the scene source.
    public class SceneScreen : AppScreen
    {
        public TMP_Text statusText, hintText;
        public Image statusDot;
        public Button linkButton;

        public Segmented toolSeg, spaceSeg, pivotSeg, syncSeg;
        public Button snapButton, lockButton;
        public ChipToggle snapChip, lockChip;
        public LongPress snapLongPress;

        public RawImage view;
        public SceneViewInput input;
        public Camera worldCamera;
        public SceneWorldView world;
        public TransformGizmo gizmo;
        public OrbitCameraRig rig;
        public Button undoButton, redoButton, viewTopButton, viewFrontButton, viewSideButton, view3DButton, focusButton;

        public Button objectsButton, inspectorButton, resetButton;

        // viewport: placeholder until a scene arrives, demo badge, full view (hides the header, toolbars and bottom bar)
        public GameObject placeholder, demoBadge;
        public TMP_Text placeholderTitle, placeholderText;
        public Button expandButton;
        public Image expandIcon;
        public Sprite expandSprite, collapseSprite;
        public RectTransform viewFrame;
        public Vector2 frameMin, frameMax, fullMin, fullMax;          // viewport offsets: normal / full view
        public List<GameObject> chrome = new List<GameObject>();      // hidden in full view

        public GameObject hierarchySheet;
        public Button hierarchyClose;
        public HierarchyRowView rowTemplate;
        public TMP_Text hierarchyEmpty;

        public GameObject inspectorSheet;
        public Button inspectorClose;
        public TMP_Text inspectorTitle, inspectorNote;
        public List<InspectorField> fields = new List<InspectorField>();   // position x y z, rotation x y z, scale x y z
        public Button uniformButton, copyButton, pasteButton;
        public ChipToggle uniformChip;

        public const string IpsKey = "msvr_scene_ips";

        private ISceneLink client;   // SceneSyncClient (network) or DemoSceneLink (demo mode)
        private SceneDocument doc;
        private SceneEditController edit;
        private RenderTexture rt;
        private readonly StringBuilder sb = new StringBuilder();
        private readonly List<HierarchyRowView> rows = new List<HierarchyRowView>();
        private bool liveSync = true, uniformScale;
        private InspectorField editingField;
        private float nextStatus;
        private string showOnly = "";
        private bool fullView;

        // tool bar order: Select (no gizmo, tap = select), Move, Rotate, Scale, All
        public const int SelectToolIndex = 0;
        public static int ToolIndex(GizmoTool t) { return (int)t + 1; }

        public bool FullView { get { return fullView; } }

        public SceneSyncClient Client { get { return client as SceneSyncClient; } }   // null in demo mode
        public DemoSceneLink DemoLink { get { return client as DemoSceneLink; } }    // null with the real network
        public SceneDocument Doc { get { return doc; } }
        public SceneEditController Edit { get { return edit; } }
        public bool LiveSync { get { return liveSync; } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            RemoveOldSaveFile();
            doc = new SceneDocument();
            client = AppRoot.Demo ? (ISceneLink)new DemoSceneLink() : new SceneSyncClient();
            client.SetManualIps(PlayerPrefs.GetString(IpsKey, ""));
            edit = new SceneEditController(doc, world, gizmo, rig, worldCamera);
            input.Controller = edit;

            client.TargetsChanged += RefreshStatus;
            client.SceneReceived += OnScene;
            doc.Loaded += OnLoaded;
            doc.SelectionChanged += OnSelection;
            doc.PoseChanged += OnPose;
            doc.LockChanged += OnLock;
            doc.HistoryChanged += RefreshHistory;

            toolSeg.Changed += i =>
            {
                edit.SelectOnly = i == SelectToolIndex;
                if (i != SelectToolIndex) edit.Tool = (GizmoTool)(i - 1);
                UpdateHint();
            };
            toolSeg.Select(ToolIndex(edit.Tool), false);
            if (expandButton != null) expandButton.onClick.AddListener(() => SetFullView(!fullView));
            spaceSeg.Changed += i => edit.Global = i == 1;
            pivotSeg.Changed += i => edit.CenterPivot = i == 1;
            syncSeg.Changed += i =>
            {
                liveSync = i == 0;
                nav.Say(liveSync ? "Live: PC / Quest follow while you drag" : "On release: sent when you lift the finger");
            };
            snapButton.onClick.AddListener(ToggleSnap);
            snapLongPress.LongPressed += PickSnapStep;
            lockButton.onClick.AddListener(ToggleLock);
            linkButton.onClick.AddListener(AskIps);

            undoButton.onClick.AddListener(() => { string l = doc.Undo(); if (l != null) nav.Say("Undo: " + l); });
            redoButton.onClick.AddListener(() => { string l = doc.Redo(); if (l != null) nav.Say("Redo: " + l); });
            viewTopButton.onClick.AddListener(() => rig.SetView(CameraView.Top));
            viewFrontButton.onClick.AddListener(() => rig.SetView(CameraView.Front));
            viewSideButton.onClick.AddListener(() => rig.SetView(CameraView.Side));
            view3DButton.onClick.AddListener(() => rig.SetView(CameraView.Persp));
            focusButton.onClick.AddListener(() => { if (!edit.FocusSelected()) FrameAll(); });

            objectsButton.onClick.AddListener(() => ShowSheet(hierarchySheet.activeSelf ? null : hierarchySheet));
            inspectorButton.onClick.AddListener(() => ShowSheet(inspectorSheet.activeSelf ? null : inspectorSheet));
            hierarchyClose.onClick.AddListener(() => ShowSheet(null));
            inspectorClose.onClick.AddListener(() => ShowSheet(null));
            resetButton.onClick.AddListener(ResetAll);
            copyButton.onClick.AddListener(Copy);
            pasteButton.onClick.AddListener(Paste);
            uniformButton.onClick.AddListener(() => { uniformScale = !uniformScale; uniformChip.Set(uniformScale); });

            foreach (var f in fields)
            {
                f.Edited += OnField;
                f.BadInput += (field, text) => nav.Say("Not a number: " + text);
            }

            ApplySteps();
            snapChip.Set(false);
            lockChip.Set(false);
            uniformChip.Set(false);
            rowTemplate.gameObject.SetActive(false);
            hierarchySheet.SetActive(false);
            inspectorSheet.SetActive(false);
            if (worldCamera != null) worldCamera.enabled = false; // only renders while this tab is visible
            rig.Home();
            RefreshHistory();
        }

        public override void OnShow()
        {
            EnsureTexture();
            if (worldCamera != null) worldCamera.enabled = true;
            client.Start();
            base.OnShow();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (worldCamera != null) worldCamera.enabled = false; // battery: no 3D rendering on other tabs
            if (showOnly.Length > 0 && client != null) ShowFor(""); // "Open in Scene" for one Quest ends when the tab is left
        }

        private void OnDestroy()
        {
            if (client != null) client.Dispose();
            if (rt != null) { rt.Release(); Destroy(rt); }
        }

        // full view: the viewport uses almost the whole screen (the bottom navigation stays); header, toolbars and the
        // bottom bar are hidden. The view's RenderTexture follows the new size (EnsureTexture), so nothing is stretched.
        public void SetFullView(bool on)
        {
            fullView = on;
            foreach (var g in chrome) if (g != null) g.SetActive(!on);
            if (on) ShowSheet(null);
            if (viewFrame != null)
            {
                viewFrame.offsetMin = on ? fullMin : frameMin;
                viewFrame.offsetMax = on ? fullMax : frameMax;
            }
            if (expandIcon != null) expandIcon.sprite = on ? collapseSprite : expandSprite;
        }

        // Android back: close an open sheet, then leave the full view, then the screen
        public override bool HandleBack()
        {
            if (hierarchySheet.activeSelf || inspectorSheet.activeSelf) { ShowSheet(null); return true; }
            if (fullView) { SetFullView(false); return true; }
            return false;
        }

        private void Update()
        {
            if (client == null) return;
            EnsureTexture(); // cheap when the size is unchanged; recreates the texture if the view was resized
            client.Tick(Time.unscaledTime);
            edit.UpdateGizmo();
            if (Time.unscaledTime >= nextStatus)
            {
                nextStatus = Time.unscaledTime + 0.5f;
                RefreshStatus();
            }
        }

        public override void Refresh()
        {
            RefreshStatus();
            UpdateHint();
            RefreshHistory();
        }

        // ---------- scene / selection ----------

        private void OnScene(List<SyncObjectInfo> objs, string from)
        {
            doc.Load(objs);
            nav.Say("Scene from " + from + ": " + doc.Objects.Count + " object(s)");
        }

        private void OnLoaded()
        {
            world.Build(doc);
            FillHierarchy();
            FrameAll();
            RefreshStatus();
            UpdateHint();
        }

        private void OnSelection()
        {
            var o = doc.Selected;
            editingField = null;
            world.SetSelected(o != null ? o.id : null);
            foreach (var r in rows) r.SetSelected(o != null && r.id == o.id);
            lockChip.Set(o != null && o.locked);
            BindInspector();
            UpdateHint();
        }

        private void OnPose(SceneObjectState o, bool final)
        {
            world.Sync(o);
            if (liveSync || final) client.SetObjectState(o.id, o.pose.pos, o.pose.rot, o.pose.scale, o.locked);
            if (o == doc.Selected) ShowInspectorValues();
            if (final && editingField != null && !doc.Editing) editingField = null;
            if (final && AppRoot.Session != null && AppRoot.Session.Active) // the session timeline shows object moves
                AppRoot.Session.Log(SessionEventKind.Object, "Host", "moved " + o.name + " to " +
                    o.pose.pos.ToString("F1") + (showOnly.Length > 0 ? " (" + showOnly + ")" : ""));
        }

        // Session "Open in Scene" for one Quest ("" = all). SceneSync has ONE shared scene: every receiver (PC, each Quest)
        // holds the same objects with the same ids, and an edit goes to all of them. So there is no per-Quest scene to load
        // or clear: this only changes which Quest the status line names (demo) and tags the timeline entries; the loaded
        // scene and its undo history stay when the filter is cleared.
        public void ShowFor(string questName)
        {
            showOnly = questName ?? "";
            var demo = DemoLink;
            if (demo != null) demo.ShowOnly(showOnly);
            RefreshStatus();
        }

        private void OnLock(SceneObjectState o)
        {
            client.SetObjectState(o.id, o.pose.pos, o.pose.rot, o.pose.scale, o.locked);
            if (o == doc.Selected)
            {
                lockChip.Set(o.locked);
                BindInspector();
            }
            foreach (var r in rows) if (r.id == o.id) r.Bind(o, o == doc.Selected);
            UpdateHint();
            nav.Say((o.locked ? "Locked: " : "Unlocked: ") + o.name);
        }

        private void FrameAll()
        {
            if (doc.Objects.Count == 0) { rig.Home(); return; }
            var b = new Bounds(doc.Objects[0].CenterPoint, Vector3.zero);
            foreach (var o in doc.Objects)
                b.Encapsulate(new Bounds(o.CenterPoint, Vector3.one * (o.Radius * 2f)));
            rig.yaw = OrbitCameraRig.HomeYaw;
            rig.pitch = OrbitCameraRig.HomePitch;
            rig.Focus(b.center, Mathf.Max(0.5f, b.extents.magnitude * 0.8f));
        }

        // ---------- hierarchy ----------

        private void FillHierarchy()
        {
            rows.Clear();
            rows.AddRange(Fill(rowTemplate, doc.Objects.Count));
            for (int i = 0; i < rows.Count; i++)
            {
                var o = doc.Objects[i];
                var row = rows[i];
                row.Bind(o, o == doc.Selected);
                string id = o.id;
                row.button.onClick.RemoveAllListeners();
                row.button.onClick.AddListener(() => doc.Select(id));
            }
            if (hierarchyEmpty != null) hierarchyEmpty.gameObject.SetActive(doc.Objects.Count == 0);
        }

        private void ShowSheet(GameObject sheet)
        {
            hierarchySheet.SetActive(sheet == hierarchySheet);
            inspectorSheet.SetActive(sheet == inspectorSheet);
            if (sheet == inspectorSheet) BindInspector();
        }

        // ---------- inspector ----------

        private void BindInspector()
        {
            var o = doc.Selected;
            bool on = o != null && !o.locked;
            inspectorTitle.text = o != null ? o.name : "No object selected";
            inspectorNote.text = o == null ? "Tap an object in the view or in Objects." : o.locked ? "Locked: tap Lock to edit." : "Drag X / Y / Z to scrub  •  long press = reset";
            foreach (var f in fields) f.SetInteractable(on);
            copyButton.interactable = o != null;
            pasteButton.interactable = on && doc.HasClipboard;
            ShowInspectorValues();
        }

        private void ShowInspectorValues()
        {
            var o = doc.Selected;
            if (o == null || fields.Count < 9) return;
            for (int i = 0; i < 3; i++)
            {
                fields[i].Show(o.pose.pos[i]);
                fields[3 + i].Show(o.pose.euler[i]);
                fields[6 + i].Show(o.pose.scale[i]);
            }
        }

        private void ApplySteps()
        {
            for (int i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f.group == 0) { f.step = edit.Snap.move; f.scrubPerUnit = 0.004f; f.resetValue = 0f; }
                else if (f.group == 1) { f.step = edit.Snap.rotate; f.scrubPerUnit = 0.4f; f.resetValue = 0f; }
                else { f.step = edit.Snap.scale; f.scrubPerUnit = 0.004f; f.resetValue = 1f; }
            }
            snapChip.SetLabel(edit.Snap.on ? "Snap " + F(edit.Snap.move) + " m" : "Snap");
        }

        // one Inspector value: a scrub = one undo step when it ends; a typed value / - + / reset = one step
        private void OnField(InspectorField f, float v, bool final)
        {
            var o = doc.Selected;
            if (o == null || o.locked) return;
            if (!doc.Editing || doc.EditObject != o || editingField != f)
            {
                if (doc.Editing) doc.CommitEdit();
                if (!doc.BeginEdit(o, (f.group == 0 ? "Position " : f.group == 1 ? "Rotation " : "Scale ") + "XYZ"[f.axis])) return;
                editingField = f;
            }
            ScenePose start = doc.EditStart, p = start;
            if (f.group == 0) p.pos[f.axis] = v;
            else if (f.group == 1)
            {
                Vector3 e = start.euler;
                e[f.axis] = v;
                p = ScenePose.FromEuler(start.pos, e, start.scale);
            }
            else
            {
                Vector3 s = start.scale;
                float nv = SceneSyncProtocol.ClampScale(v);
                if (uniformScale && start.scale[f.axis] > 0f) s = start.scale * (nv / start.scale[f.axis]);
                else s[f.axis] = nv;
                p.scale = SceneSyncProtocol.ClampScale(s);
            }
            doc.Preview(p);
            if (final)
            {
                editingField = null;
                doc.CommitEdit();
            }
        }

        private void Copy()
        {
            var o = doc.Selected;
            if (o == null) return;
            doc.Copy(o);
            pasteButton.interactable = !o.locked;
            nav.Say("Copied transform of " + o.name);
        }

        private void Paste()
        {
            var o = doc.Selected;
            if (o == null || !doc.HasClipboard) return;
            if (o.locked) { nav.Say(o.name + " is locked"); return; }
            doc.Paste(o);
            nav.Say("Pasted transform to " + o.name);
        }

        // ---------- toolbar ----------

        private void ToggleSnap()
        {
            if (snapLongPress.ConsumeClick()) return;
            edit.Snap.on = !edit.Snap.on;
            snapChip.Set(edit.Snap.on);
            ApplySteps();
            nav.Say(edit.Snap.on ? "Snap on: move " + F(edit.Snap.move) + " m, rotate " + F(edit.Snap.rotate) + " deg, scale " + F(edit.Snap.scale) : "Snap off");
        }

        private void PickSnapStep()
        {
            nav.actionSheet.Show("Snap step (long press)", new[]
            {
                "Move step: " + F(edit.Snap.move) + " m",
                "Rotate step: " + F(edit.Snap.rotate) + " deg",
                "Scale step: " + F(edit.Snap.scale) + " (fixed)"
            }, null, i =>
            {
                if (i == 0)
                    nav.actionSheet.Show("Move snap step", new[] { "0.1 m", "0.5 m", "1 m" }, null, k => SetStep(true, new[] { 0.1f, 0.5f, 1f }[k]));
                else if (i == 1)
                    nav.actionSheet.Show("Rotate snap step", new[] { "15 deg", "45 deg", "90 deg" }, null, k => SetStep(false, new[] { 15f, 45f, 90f }[k]));
            });
        }

        public void SetStep(bool move, float value)
        {
            if (move) edit.Snap.move = value; else edit.Snap.rotate = value;
            ApplySteps();
            nav.Say((move ? "Move snap " + F(value) + " m" : "Rotate snap " + F(value) + " deg") + (edit.Snap.on ? "" : " (tap the magnet to turn snap on)"));
        }

        private void ToggleLock()
        {
            var o = doc.Selected;
            if (o == null) { nav.Say("Select an object first"); return; }
            doc.SetLocked(o, !o.locked);
        }

        private void ResetAll()
        {
            int n = doc.ResetAll();
            nav.Say(n > 0 ? "Reset " + n + " object(s) to the scene start (Undo brings it back)" : "Nothing to reset");
        }

        private void RefreshHistory()
        {
            if (undoButton != null) undoButton.interactable = doc != null && doc.UndoCount > 0;
            if (redoButton != null) redoButton.interactable = doc != null && doc.RedoCount > 0;
        }

        private void UpdateHint()
        {
            if (hintText == null || doc == null) return;
            var o = doc.Selected;
            if (!client.HasScene) hintText.text = "Waiting for the scene from a PC / Quest";
            else if (o == null) hintText.text = "Tap an object  •  drag = orbit  •  2 fingers = pan / zoom";
            else if (o.locked) hintText.text = o.name + " is locked  •  tap Lock to edit";
            else if (edit.SelectOnly) hintText.text = o.name + " selected  •  pick Move / Rotate / Scale to edit";
            else
            {
                switch (edit.Tool)
                {
                    case GizmoTool.Move: hintText.text = o.name + ": drag an arrow = 1 axis  •  ball = free"; break;
                    case GizmoTool.Rotate: hintText.text = o.name + ": drag a ring to turn"; break;
                    case GizmoTool.Scale: hintText.text = o.name + ": drag a cube = 1 axis  •  centre = all"; break;
                    default: hintText.text = o.name + ": arrows move  •  rings turn  •  cubes scale"; break;
                }
            }
        }

        // ---------- view ----------

        private void EnsureTexture()
        {
            if (worldCamera == null || view == null) return;
            Canvas c = view.canvas != null ? view.canvas.rootCanvas : null;
            float f = c != null ? c.scaleFactor : 1f;
            Rect r = view.rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return; // not laid out yet: Update() tries again next frame
            // keep the viewport's aspect ratio when the size is limited to 2048 (full view on a tall phone), so the
            // scene is never stretched
            float pw = r.width * f, ph = r.height * f;
            float k = Mathf.Min(1f, 2048f / Mathf.Max(pw, ph));
            int w = Mathf.Max(64, Mathf.RoundToInt(pw * k));
            int h = Mathf.Max(64, Mathf.RoundToInt(ph * k));
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { worldCamera.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "SceneView", antiAliasing = 2 };
            rt.Create();
            worldCamera.targetTexture = rt;
            view.texture = rt;
            Debug.Log("[MediSimVR] Scene view RenderTexture " + w + "x" + h);
        }

        private void RefreshStatus()
        {
            if (statusText == null || client == null) return;
            RefreshViewport();
            var list = client.TargetNames;
            if (list.Count == 0)
            {
                statusText.text = client.Error != null ? "Network error: " + client.Error : "Searching PC / Quest on this Wi-Fi...";
                statusDot.color = ThemeManager.Get(ColorRole.Warning);
                return;
            }
            sb.Length = 0;
            // demo mode shows sample objects (badge in the view), never presented as a live Unity scene
            sb.Append(AppRoot.Demo ? "Demo scene: " : "Live from Unity: ");
            for (int i = 0; i < list.Count && i < 2; i++)   // short: "Quest-A1B2, Quest-C3D4 +1"
            {
                if (i > 0) sb.Append(", ");
                sb.Append(list[i]);
            }
            if (list.Count > 2) sb.Append(" +").Append(list.Count - 2);
            if (client.WaitingForScene) sb.Append("  •  loading...");
            statusText.text = sb.ToString();
            statusDot.color = ThemeManager.Get(client.HasScene ? ColorRole.Success : ColorRole.Warning);
        }

        // placeholder until the first scene arrives; demo badge in demo mode
        private void RefreshViewport()
        {
            bool has = client.HasScene;
            if (placeholder != null && placeholder.activeSelf == has) placeholder.SetActive(!has);
            if (demoBadge != null && demoBadge.activeSelf != (AppRoot.Demo && has)) demoBadge.SetActive(AppRoot.Demo && has);
            if (has || placeholderText == null) return;
            var list = client.TargetNames;
            placeholderTitle.text = AppRoot.Demo ? "Loading demo scene" : "Waiting for Unity Scene";
            placeholderText.text = client.Error != null ? "Network error: " + client.Error
                : list.Count == 0 ? "Start the Unity scene on the PC (Play mode) or a Quest on this Wi-Fi"
                : "Loading the scene from " + list[0] + "...";
        }

        private void AskIps()
        {
            if (AppRoot.Demo) { nav.Say("Demo mode: no network. Turn demo mode off in Settings to connect."); return; }
            string current = PlayerPrefs.GetString(IpsKey, "");
            nav.popup.Show("Receiver IPs",
                "Type the IP of each PC / Quest, separated by commas. Leave empty to use Wi-Fi discovery only.",
                "Save", null, "Cancel", "192.168.1.20, 192.168.1.31", text =>
                {
                    List<string> bad;
                    var ok = SceneSyncClient.ParseIps(text, out bad);
                    client.SetManualIps(text);
                    sb.Length = 0;
                    foreach (var a in ok) { if (sb.Length > 0) sb.Append(", "); sb.Append(a); }
                    PlayerPrefs.SetString(IpsKey, sb.ToString());
                    PlayerPrefs.Save();
                    nav.Say(bad.Count > 0 ? "Not an IP: " + string.Join(", ", bad.ToArray())
                        : ok.Count == 0 ? "Manual IPs cleared" : "Saved " + ok.Count + " IP(s)");
                    RefreshStatus();
                });
            nav.popup.input.text = current;
        }

        private static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }

        // Save / Load was removed in Scene tab v2: delete the old M2 save file (and its backup / temp file) if it is still there
        private static void RemoveOldSaveFile()
        {
            foreach (string name in new[] { "cube_state.json", "cube_state.json.bak", "cube_state.json.tmp" })
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, name);
                try
                {
                    if (!System.IO.File.Exists(path)) continue;
                    System.IO.File.Delete(path);
                    Debug.Log("[MediSimVR] Removed old Save/Load file " + path);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[MediSimVR] Could not remove old Save/Load file " + path + ": " + e.Message);
                }
            }
        }
    }
}
