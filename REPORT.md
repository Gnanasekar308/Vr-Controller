# REPORT

## Task
New app flow, UI only (demo mode / mock data, no backend), built by MediSimUIBuilder in the same style:
1. Home > **Control Mode** opens the **Session** screen.
2. Session: two big cards, **HOST** "Control all Quests in the room" and **PLAYER** "Play on your own Quest".
3. HOST > **Quest list**: cards with name, status chip Online (green) / Offline (grey) / In Use (blue, with what plays),
   battery; filter chips All / Online / Offline / In Use with counts; offline cannot be selected; select one / several /
   **Select all online**; **Continue (n)** > Play page for the selected Quests.
4. PLAYER > straight to the **Play page** on "My Quest" (last used, else the first online one); chip
   "My Quest: Quest-XXXX" with **Change**.
5. **Play page** (same for both): target in the header area ("Playing on: ..." / "My Quest: ..."); pick content
   Video / Audio / Image / Text (Send picker + preview card reused); **Send & Play** / **Send & Show**; then the controls
   of that type on the same page: Play, Pause, Resume, Stop, Mute, progress bar (video / audio) or Show on Quest, Hide
   (image / text). The Quests switch to In Use in the Quest list after playing (mock).
6. Back: Play page > Quest list (host) or Session (player) > Home, the same with the Android back button. The mode
   (Host / Player) shows in the headers and on Home.
- Keep the existing tabs working; reuse the Devices list, Send picker and Control buttons as components. Demo mode ON
  by default with the 4 mock Quests (one offline, one in use).
- Finish: build + install on ZA223HXPHV (the user allowed: uninstall the old app, install the new one), logcat,
  screenshots of the new screens in `demo-screens/`, DEMO.md updated.

## Files changed (phone repo)
- New shared components (each MonoBehaviour in its own file):
  - `Scripts/UI/Widgets/DeviceListView.cs`: the Devices list (rows kept between refreshes, empty text, shared
    "tap = select, offline cannot be selected" rule). Used by the Devices tab, the Quest list and the My Quest list.
  - `Scripts/UI/Widgets/MediaPreviewView.cs`: the Send tab preview card (picture / icon / text paper, badge, name, info,
    Change / Edit / Remove). Used by the Send tab and the Play page.
  - `Scripts/UI/Widgets/PlaybackControlsView.cs`: Play / Pause / Resume / Stop / Mute or Show / Hide, state line,
    optional progress bar; `Acted` event for the session timeline. Used by the Control tab (big tiles) and the Play page
    (compact row + progress).
  - `Scripts/App/ContentChooser.cs` (on the App object): type sheet > files of that type in the app (demo files, earlier
    picks) or Browse phone (Android picker) or Write text; edit text; send with the "Replace content?" question. The only
    listener of the file picker (a picked file is added once). Used by the Send tab and the Play page.
- New non-MonoBehaviour helpers: `Scripts/UI/Widgets/DeviceStatus.cs` (Online / Offline / In Use, colours, "Playing ..."
  text, battery text; replaces `DeviceRowView.InUse` and `QuestCardView.Content`), `Scripts/App/ControlFlow.cs` (role of
  the flow: from the session service when there is one, else kept locally; My Quest in PlayerPrefs `msvr_my_quest`).
- New screens: `QuestListScreen.cs` (ScreenId.QuestList), `PlayScreen.cs` (ScreenId.Play).
- Changed screens: `HomeScreen` (Control Mode > Session), `SessionStartScreen` (HOST > Quest list, PLAYER > Play page,
  role switch asks first, live session card > dashboard, `ModeLine` via ControlFlow), `SessionPlayerPickScreen` (now the
  "Change My Quest" list, built from DeviceListView), `DevicesScreen` / `SendScreen` / `ControlScreen` (use the shared
  components).
- `DeviceRowView.cs`: card redesigned (battery + IP line, what plays, status chip); `QuestCardView.cs`: `Content` moved.
- `Navigator.cs`: ScreenIds `QuestList`, `Play` appended; Back uses a screen's fixed parent
  (`AppScreen.TryGetParent`) before the back stack. `AppScreen.cs`: `TryGetParent`, `Keep` is public.
- Data: `Models.cs` (`QuestDevice.PositionSec`), `MockControllerService.cs` (Quests start unselected; playback position
  moves while playing, resets on new file / Stop / send; Quest stops at the end of the file), `DemoConfig.cs` (card texts,
  dashboard hint, Quest-C3D4 now idle = one in use, start position of Quest-A1B2, new demo image Heart_Diagram.png),
  `AppRoot.cs` (resets ControlFlow on start).
- UI builder: `MediSimUIBuilder.cs` (Devices / Send / Control use the shared builders, 15 screens, ContentChooser on the
  App object), new `MediSimUIBuilder.Flow.cs` (Quest list, Play page, DeviceList / DeviceRow, PreviewCard, ControlsBig,
  ControlsCompact), `MediSimUIBuilder.Session.cs` (My Quest list). `Scenes/Main.unity` regenerated.
- `DemoTour.cs`: walks the new flow, presses the app's own buttons, checks every back step and the offline rule (a failed
  check is an error, so the smoke test fails on it); themes on the Quest list + Play page.
- `Tools/capture-demo-screens.sh` (logcat wait 600 s for the longer tour; the tour's whole Unity log is saved as
  `demo-screens/tour_unity_log.txt`, because the phone's log buffer dropped the first lines in run 1), `DEMO.md`
  (new 5-minute script).

## Tested + real results
Run 1 (first build of the flow):
- First compile failed: `error CS0723 / CS0721 'QuestStatus'` (name clash with `QuestStatus` in `Net/Protocol.cs`);
  the new helper was renamed `DeviceStatus`.
- Editor smoke test (batchmode `DemoSmokeTest`, UI rebuilt, Play mode, whole DemoTour):
  `UI scene built ... screens 15, tabs 7`, `[DEMO-TEST] RESULT screens 42, errors 0  -> PASSED`, 14 `DEMO-TOUR check OK`
  (Control Mode > Session; HOST > Quest list; offline cannot be selected; two selected; Continue > Play; back Play >
  Quest list; back Quest list > Session; live card > host dashboard; PLAYER > Play; player acts on one Quest; Change >
  My Quest list; pick > Play; back Play > Session (player); back Session > Home).
- Scene Play-mode test (demo forced OFF): `[SIM-TEST] RESULT 82 passed, 0 failed  -> ALL PASSED`.
- APK: `Build result: Succeeded`, 16,351,574 bytes. Phone ZA223HXPHV (user allowed uninstall + install):
  `adb uninstall com.medisim.vr` -> `Success`, `adb install` -> `Success`, `firstInstallTime=2026-11-12 12:42:21`.
  Tour: 42 screenshots, `DEMO-TOUR done 42 screens`; the phone's log buffer had rotated, the kept app lines showed
  12 checks OK, 0 FAILED, 0 exceptions, 0 E/Unity.
- Found on the screenshots: the Quest name was missing on every DeviceRowView card (Quest list, Devices tab, My Quest
  list): the name box was 58 px high and an Inter 48 pt line needs ~58.1 px, so TextMeshPro (Ellipsis) hid the line.
  Fixed: name box 72 px, info / content boxes 46 px (fit also at text size L), IP line full width; host dashboard card
  name box 70 px for the same reason. Also "Playing on: Quest-A1B2, Quest-C3D4, Que..." with 3 Quests: now
  "Quest-A1B2, Quest-C3D4 +1". One screenshot of run 1 also caught a phone notification; run 2 replaced all screenshots.

Run 2 (final build, after these fixes):
- Editor smoke test: `screens 15, tabs 7`, `[DEMO-TEST] RESULT screens 42, errors 0  -> PASSED`, 14 checks OK.
- APK: `Build result: Succeeded`, 16,346,870 bytes (12:50). `adb install -r` -> `Success`,
  `lastUpdateTime=2026-11-12 12:50:14` (same install as run 1, `firstInstallTime` 12:42:21).
- Phone tour, full Unity log `demo-screens/tour_unity_log.txt` (277 lines, app pid 11559):
  `[MediSimVR] AppRoot.Awake, demo mode ON` (12:50:20), `DEMO-TOUR start`, `Demo scene loaded (6 objects)`,
  `DEMO-TOUR done 42 screens` (12:53:22); 14 `check OK`, 0 `check FAILED`; 0 E/Unity, 0 W/Unity, 0 exceptions,
  0 `not a Unity engine type`.
- Screenshots (42, `demo-screens/`, git-ignored), checked by eye: 01 home, 02 session_start, 03 host_already_active,
  04 quest_list, 05 filter_in_use, 06 filter_offline, 07 quest_list_selected (toast "offline: it cannot be selected"),
  08 play_choose_video, 09 play_host_ready, 10 play_replace_content, 11 play_host_playing (progress 00:04 / 05:00),
  12 play_host_paused, 13 play_host_image (Show on Quest / Hide), 14 quest_list_in_use (both In Use with what they show),
  15 session_start_live, 16-18 host dashboard / timeline / chat, 19-21 scene, 22 session_summary, 23 play_player
  ("My Quest: Quest-A1B2", "Mode: Player"), 24 player_change_quest, 25 play_player_audio, 26 home_player_mode
  ("Mode: Player • Quest-C3D4"), 27 devices, 28 send, 29 control, 30 settings, 31 edit_app, 32 notifications,
  33-42 Quest list + Play page in the Default / Dark / Light / Ocean / Purple themes. Names, status chips, filters,
  counts and controls are visible in all of them.
- Scene Play-mode test not rerun for run 2: only builder layout values and the Play page target text changed.

Run 4 (after the review round 2 fixes; this is the build on the phone now):
- Editor smoke test: `screens 15, tabs 7`, `[DEMO-TEST] RESULT screens 42, errors 0  -> PASSED`, 16 checks OK.
- APK: `Build result: Succeeded`, 16,345,954 bytes (13:04). `adb install -r` -> `Success`,
  `lastUpdateTime=2026-11-12 13:04:46`.
- Phone tour (`tour_unity_log.txt`, 287 lines, pid 15851): `AppRoot.Awake, demo mode ON` (13:04:52),
  `DEMO-TOUR done 42 screens` (13:07:54), 16 checks OK incl. `Change on the Play page goes to the Quest list (host)`
  and `Continue goes back to the Play page`, 0 FAILED, 0 E/Unity, 0 W/Unity, 0 exceptions; capture exit 0.
  42 screenshots replaced, checked by eye: same as run 2 / 3.

Run 3 (after the review round 1 fixes):
- Editor smoke test: `screens 15, tabs 7`, `[DEMO-TEST] RESULT screens 42, errors 0  -> PASSED`, 14 checks OK.
- APK: `Build result: Succeeded`, 16,345,062 bytes (12:57). `adb install -r` -> `Success`,
  `lastUpdateTime=2026-11-12 12:57:55`.
- Phone tour (`tour_unity_log.txt`, 277 lines, pid 13656): `AppRoot.Awake, demo mode ON` (12:58:01),
  `DEMO-TOUR done 42 screens` (13:01:03), 14 checks OK, 0 FAILED, 0 E/Unity, 0 W/Unity, 0 exceptions;
  capture script exit 0 (new "finished" check). 42 screenshots replaced again, checked by eye: same as run 2.
- Not separately tested: the "File picker not available" message (the picker always exists in the app) and
  "TOUR NOT FINISHED" (needs a broken tour); both are small guard branches.

### Review input note
`*.unity` scenes are excluded from the Codex diff (Main.unity is regenerated by MediSimUIBuilder and is too large);
the builder code that generates it is in the diff.

## Not verified
- Real human taps on the phone: the tour presses the app's own buttons from code. The presenter should do one dry run
  with DEMO.md.
- Demo mode OFF on the phone (real network): the flow uses only IControllerService, so HOST / PLAYER / Play page work
  with real Quests in code, but this was not run against real Quests today. The progress bar stays at 0:00 with real
  Quests (the receiver does not report a position yet).
- Browse phone (Android file picker) from the Play page: same picker code as the Send tab, not opened in the tour (it
  leaves the app). Same for "Write new text" (keyboard input).
- With the real service: when `FileId` is set after a send (the Play page shows the controls on it), and a failed send
  (the controls hide again after the sending time).

## Review rounds
- Round 1: FAIL (7 major, 1 minor). Changes / answers:
  1. ContentChooser hooks the picker in Start: Unity runs every Awake (AppRoot.Awake creates the picker) before any
     Start of the scene objects, so this did not fail; still made lazy: `Picker()` hooks on first use too (falls back to
     the FilePicker on the same object).
  2. Real: Player Play page with no online My Quest kept the old selection. `ControlFlow.SelectOnly(null)` now clears
     every selection, and `PlayScreen.OnShow` always calls it in player mode (an offline My Quest is never a target:
     `SelectedOnline` skips it).
  3. No change: without a session service the role is set directly by `ControlFlow.StartLocalHost()` (HOST) and
     `ControlFlow.UsePlayerQuest()` (`localRole = Player`), so switching works in demo off; the "Switch to ...?" popup
     only exists when a session service exists.
  4. No change: after "Switch to Host?" ends this phone's player session, `StartHost(false)` asking "Host already
     active" is the demo's simulated OTHER phone (shown until someone takes over), which is the intended demo message.
  5. Picks go through `ContentChooser.Pick()`, which says "File picker not available" if there is none.
  6. The report is right: the builder log says `screens 15` (13 + Quest list + Play); the scene is generated by the
     builder code in the diff (see Review input note). The My Quest list uses the same `DeviceList()` builder helper
     and `DeviceListView` as the Devices tab and the Quest list.
  7. Real: `capture-demo-screens.sh` now exits 1 with "TOUR NOT FINISHED" when no "DEMO-TOUR done" line arrived.
  8. `TxtName` trims, uses "Text" for an empty title, checks ".txt" without case.
- Round 2: FAIL (3 major). Changes / answers:
  1. No change, by design: the Play page shows the controls while a send is running (state line "Sending <file>...")
     and when a target has the file (`QuestDevice.FileId`, set by the service when the send finished: mock after
     `FakeSendSeconds`). A send that never finishes hides the controls again after `FakeSendSeconds + 3 s`. When the
     real service sets `FileId` is not verified today (see Not verified).
  2. `ContentChooser`: the `done` callback is now also called after "Write new text" and after a file picked on the
     phone (kept as `pendingDone` until the picker answers; cleared on a picker error). Before, those paths refreshed
     the screen only through the service's Changed event (`AddFile` fires it in both services).
  3. Host "Change" on the Play page now opens the Quest list directly (`nav.Tab(ScreenId.QuestList)`); before, it used
     Back, which also always went to the Quest list for the host (fixed parent). New tour checks: "Change on the Play
     page goes to the Quest list (host)" and "Continue goes back to the Play page" (16 checks now).
- Round 3: PASS, "Issues: none" (REVIEW.md).
