# REPORT

## Task
Scene tab v2: object editing like Unity, mobile-friendly. Replaces the M2 Scene tab design; the receivers (PC sync scene
here, Quest receiver in the receiver repo) were updated with it.
- Removed: Save / Load and `cube_state.json` (UI, code, file). Kept Reset. No poses / bookmarks. No movement limits
  (position and rotation unlimited, scale only a minimum of 0.001 per axis).
- Objects: every receiver object with `SyncObject` (id, name, locked) is sent to the phone (id, name, shape, colour,
  pos / rot / scale); the phone draws matching primitives (or a bounds box). Hierarchy sheet: tap = select, outline on
  the selected object. Every transform carries the object id and goes to ALL receivers (signed). The first receiver that
  answers is the scene source; the phone then pushes its state to all others.
- Tools: Move / Rotate / Scale / All-in-one (big buttons), gizmo red X / green Y / blue Z (axis arrows, centre = free move,
  rings, scale handles + centre = uniform), Local / Global, Pivot / Center, Snap (move 0.1 / 0.5 / 1 m, rotate 15 / 45 / 90,
  scale 0.1; long press = pick the step), Lock.
- Inspector sheet: Position / Rotation (deg) / Scale X Y Z: type, - / +, drag the X/Y/Z label to scrub, long press = reset
  that value; uniform scale toggle; Copy / Paste transform.
- Camera: 1 finger on empty space = orbit, 2 fingers = pan, pinch on empty space = zoom, double tap object = focus,
  Top / Front / Side / 3D, Focus (far objects come back).
- Undo / Redo: 100 steps (>= 50), one step per finished gesture / Inspector edit, synced to PC + Quest.
- Sync: "Live" (~30 / s while dragging, default) or "On release"; keep-alive + full state for late receivers.

## Files changed (phone repo, Unity project `MediSimVR-Controller/`)
- `Assets/MediSimVR/SceneSync/` (shared protocol v2, identical to the receiver repo: `diff -r` = no difference):
  `SceneSyncProtocol.cs`, `SceneSyncMessage.cs`, `SyncReceiver.cs`, new `SyncObject.cs`, `SyncObjectInfo.cs`,
  `Editor/SyncDemoObjects.cs` (same demo object ids on PC and Quest).
- Removed: `Scripts/SceneSync/CubeStateFile.cs` (Save / Load), `Scripts/UI/Widgets/CubeGestures.cs` (old gestures).
- New `Scripts/SceneEdit/`: `ScenePose.cs`, `SceneObjectState.cs`, `SceneDocument.cs` (objects, selection, edits,
  Undo / Redo, copy / paste, reset), `SceneEditController.cs` (gizmo drags, pinch scale, tap / double tap, focus).
- New `Scripts/Scene3D/`: `SceneWorldView.cs`, `SceneObjectView.cs`, `OrbitCameraRig.cs`, `TransformGizmo.cs`,
  `GizmoDrag.cs` (+ `SnapSettings`), `GizmoMeshes.cs`, `SceneViewInput.cs` (touch / mouse; dead zone on handles,
  double tap on a handle = focus, 2nd finger cancels an unmoved handle grab = pinch).
- New widgets (each MonoBehaviour in its own file): `ChipToggle.cs`, `LongPress.cs`, `ScrubArea.cs`, `InspectorField.cs`,
  `HierarchyRowView.cs`.
- `Scripts/SceneSync/SceneSyncClient.cs`: v2 (scene source, object list parts, per-object transforms to all targets,
  dirty sends <= ~30 / s, keep-alive round robin, full state for new targets, same receiver on a 2nd address de-duplicated).
- `Scripts/UI/Screens/SceneScreen.cs`: v2 wiring (no Save / Load).
- `Editor/MediSimUIBuilder.cs`: `BuildScene` v2 (tool row, option row, view with Undo / Redo / Reset + Top / Front / Side /
  3D / Focus, bottom bar Objects / Inspector / Live-On release, Objects + Inspector sheets); `Scenes/Main.unity` regenerated
  by it (not hand-edited).
- `Editor/SyncWorldBuilder.cs`: no fixed cube any more; outline / gizmo materials; built-in primitive mesh references.
- New `Shaders/SyncOutline.shader` (selection outline), `Shaders/SyncGizmo.shader` (handles on top); materials
  `SyncOutline.mat`, `SyncGizmo.mat`, `SyncObjectBase.mat`, `SyncObj_*.mat` (demo object colours).
- M3 files, now v2: `Editor/PCSyncSceneBuilder.cs` (menu MediSimVR > Build PC Sync Scene: SyncWorld + demo objects +
  SyncReceiver kind "pc" on SyncWorld + PCSyncStatus; not in the build settings), `Scripts/SceneSync/PCSyncStatus.cs`
  (OnGUI: name, IPs, port, object count, counters), `Scenes/PCSync.unity` (generated).
- New `Editor/SceneSimTest.cs`: automated Play-mode test (below).
- Not in this commit (parked in `git stash`, M4): beacon signature check + `Net.IsVr`.

## Tested + real results
Logs in `MediSimVR-Controller/Logs/` (git-ignored).
- Build: `v2_ui.log` `MediSimUIBuilder.Build`: `UI scene built ... screens 8, tabs 6`, exit 0, no compiler errors.
- Automated Play-mode test `SceneSimTest.Run` (batchmode, Main.unity + PCSync.unity in ONE Editor, UDP over 127.0.0.1,
  touches simulated in the view's camera pixels through `SceneViewInput`, the same code as real fingers after the
  screen -> view conversion; PC receiver = the shared `SyncReceiver` + `SyncObject` code):
  Final code (after the last change): `v2_sim6.log` `[SIM-TEST] RESULT 82 passed, 0 failed -> ALL PASSED`, Unity exit 0
  (also `v2_sim5.log` 82/82 and `v2_sim4.log` 82/82 before the two small last changes). The 82 checks (values from `v2_sim4.log`):
  - PC side: PCSync loaded, `SyncReceiver` kind pc on SyncWorld with 6 objects, listening on UDP 8771, name `PC-B5A0`.
    `Send-SceneSyncTest.ps1 -QuestIp 127.0.0.1` (receiver repo) run by the test: `RESULT: ALL PASSED`, exit 0 (here + count 6,
    object list 2/2 parts, transform applied, bad sig dropped, old seq ignored, receiver reports the right pose).
  - Scene from the source: phone got 6 objects from `PC-B5A0`; shapes cube / sphere / cylinder / capsule / mat=plane /
    table=box (locked), table bounds 1.0 x 0.75 x 0.6; phone pose = PC pose; status chip "Connected: PC-B5A0"; 6 Hierarchy rows.
  - Selection: tap on the cube selects it + outline (2 materials); tap on empty space deselects; Hierarchy row tap selects.
  - Move: X arrow -> only X (+1.177), Y arrow -> only Y (+0.795), Z arrow -> only Z (-1.901); one drag = one undo step;
    handle touched + 4 px = no change, no undo step; centre ball = free move; PC follows each time.
  - Undo / Redo restore / re-apply and reach the PC.
  - Rotate: Y ring -> 55.7 deg around (0, 1, 0) only, position unchanged, PC follows.
  - Scale: Z handle -> only Z, centre cube -> uniform x1.868.
  - Snap on (0.5 m) -> X moved exactly 1.0; snap off; long press on the magnet opens the step choice.
  - All-in-one picking: centre = free move, outer X = move X, inner X cube = scale X, ring = rotate Y.
  - Local: gizmo X = the cube's own X; Global: gizmo X = world X.
  - Lock: locked cube does not move on drag; lock and unlock reach the PC (`SyncObject.Locked`).
  - Inspector: 9 fields; typed X = 1.5 (synced), + / - on Y = +/- the step, scrub Z label 250 units = +1 m (one undo step),
    long press X label = X reset to 0 only, rotation Y = -725 kept, "abc" ignored, scale -5 -> 0.001, uniform: X = 2 -> 2,2,2,
    X = 400 m (unlimited) synced to the PC (399.990 while settling), Focus puts the far cube in the view centre (0.5, 0.5).
  - Copy / Paste: sphere gets the cube transform, PC sphere follows.
  - Reset: all unlocked objects back to the scene start in one undo step, locked table untouched, synced; Undo brings the edits back.
  - Undo depth: 60 edits -> 86 steps in the history; 55 x Undo walks back exactly 55 steps; PC matches afterwards.
  - On release: PC does not move while the finger is down, follows after lifting; Live: PC moves while the finger is down.
  - Pinch on the selected cube = uniform scale x3.133.
  - Camera: orbit (yaw 101.8 / pitch 12.7), 2-finger pan, pinch-out zoom 1.41 -> 0.72, double tap on the cylinder = select +
    focus (pivot = its centre), Top / Front / Side / 3D view directions.
  - Late receiver: a fake "Quest-SIM1" that answers later gets all 6 objects with the phone's current state;
    status chip "Connected: PC-B5A0, Quest-SIM1".
  - No `cube_state.json` written.
- Phone (Motorola edge 70 fusion ZA223HXPHV, 1272x2772), APK from `v2_apk2.log` (`Build Finished, Result: Success.`,
  16,195,470 bytes), `adb install -r` = Success (same package `com.medisim.vr`, data kept). Logcat:
  `[MediSimVR] Removed old Save/Load file /storage/emulated/0/Android/data/com.medisim.vr/files/cube_state.json`
  (the old M2 save file is gone; `run-as` is not possible on this non-debuggable build, so the app removes it itself, once),
  `Scene view RenderTexture 1197x1532`, `SceneSync client on UDP 52997, discover to 255.255.255.255, 192.0.0.31, 10.65.167.255`,
  no exceptions. Screenshots (adb screencap): Scene tab (status chip "Searching PC / Quest on this Wi-Fi...", Move / Rotate /
  Scale / All, Local / Global, Pivot / Center, Snap, Lock, Undo / Redo greyed, Reset, Top / Front / Side / 3D / Focus,
  hint "Waiting for the scene from a PC / Quest", Objects, Inspector, Live / On release), Objects sheet (empty text),
  Inspector sheet ("No object selected", Position / Rotation / Scale rows with red / green / blue X Y Z, - / +, Uniform),
  tap on "Move" selects it. Found on the phone and fixed: the "On release" label was cut ("On rele...") -> the Live / On
  release control is wider now (builder change; rebuilt, see below). Device testing stopped when an incoming call appeared
  on the phone.
- Earlier runs found and fixed (all in this change): run 1 76/81 (test bugs: wrong table-bounds expectation, stale gizmo
  position in the test; app bug: a double tap on an object landed on its new gizmo -> fixed with a handle dead zone + double
  tap on an unmoved handle = focus), run 3 81/82 (app: the first pinch finger on a handle blocked the pinch -> a 2nd finger now
  cancels an unmoved handle grab).

## Not verified
- Real fingers on the phone (pinch, two-finger pan, scrub feel, long press timing, button sizes) and the Editor mouse:
  the test drives the gesture logic with simulated pointer events, not the touch screen.
- Phone + PC + Quest over a real network (Wi-Fi / USB tethering + Windows Firewall), and the Quest headset view:
  Quest not connected, phone and PC not on one network yet.
- The final APK (`v2_apk3.log`: `Build Finished, Result: Success.`, 16,195,350 bytes, wider Live / On release control) is built
  but not installed / looked at on the phone yet (incoming call).
- Seen once during the adb taps on the phone, not explained: after closing the Objects sheet and opening the Inspector, the
  tool row showed "All" selected although "All" was not tapped (the next tap on "Move" worked normally). The first tap on
  "Inspector" also did not open the sheet (the second did). Needs the hand test on the phone.

## Review input note
The generated scene files `Scenes/Main.unity` (about 90,000 changed lines) and `Scenes/PCSync.unity` are left out of the
diff given to the reviewer because of their size; they are produced by `MediSimUIBuilder.Build` / `PCSyncSceneBuilder.Build`
(not hand-edited) and can be read from the working tree. Everything else is in the diff.

## Review rounds
- Round 1: see REVIEW.md.
