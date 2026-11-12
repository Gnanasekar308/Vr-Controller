# REPORT

## Task (user's change list, with the answers to the open points)
1. Bottom bar: Home / Session / Devices / Send / Control (Scene opens from Control).
2. Flow: Home > Control Mode > Session (Host / Player). Host > **Devices** tab (select + Connect) > **Send** tab >
   **Control** tab.
3. Control: the video plays on the Quests and on the phone ("a video selected on the phone or on the Quest plays on
   both"); a link to the Scene; opening the Scene pauses the video; back from the Scene returns to Control and the
   video stays paused until Play.
4. Control: Play, Pause, Resume, Stop, Mute; lesson notes, voice, important instructions, read aloud on the Quest
   ("Quest-la sathamaa kekkanum"); sound and mic on / off for the sender (phone) and the receivers (Quests).
5. Everything saved as a JSON file and sent to the PC. Everything more professional.

## What is real on the phone, what needs the backend
- Phone side, done: tabs, flow, Control tab, phone player (real playback of a file picked on the phone; the Quests'
  state is mirrored), Scene pause, audio switches, notes / instructions, JSON record on the phone.
- Through the session interface, simulated in demo mode, real when the backend (another developer, paused today)
  implements it: Quests reading notes / instructions aloud (`ISessionService.Speak`; demo: the phone speaks the text),
  sending the record to the PC (`ISessionService.Upload`; demo: "Sent to PC-LAB1" after 1.5 s), Quest mics.
- Not possible on the phone alone: playing a video that is stored only on the Quest on the phone (needs streaming).
  Demo sample files have no media: the player shows the thumbnail and "ON THE QUEST"; a real sample can be set per demo
  file (`DemoConfig.File.Media`, a file in StreamingAssets). No sample videos were added (none provided; the videos found
  on this PC are third-party downloads and were not used).

## Files changed (phone repo)
- Flow / screens: `DevicesScreen.cs` (step 1: filters with counts, select all online, Connect > Send), `SendScreen.cs`
  (step 2: target card + Change, Send > Control), `ControlScreen.cs` (rewritten, below), `SessionStartScreen.cs`
  (HOST > Devices, PLAYER > Send), `SceneScreen.cs` (back button; `PauseForScene` on show: pauses only the Quests that
  play), `SessionHostScreen.cs` / `SessionPlayerScreen.cs` (Scene is pushed, back returns), `SessionSummaryScreen.cs`
  (counts in two columns incl. notes / instructions, where the record is saved / sent), `Navigator.cs` (ScreenIds
  QuestList / Play removed, Scene highlights Control), `ControlFlow.cs` (`Step()`: "Step 2 of 3 ..."). Removed:
  `QuestListScreen.cs`, `PlayScreen.cs` (their parts live on in Devices / Send / Control).
- Control tab: target card, player card (`PhoneVideoPlayer` texture with an AspectRatioFitter = never stretched,
  thumbnail fallback, ON THIS PHONE / ON THE QUEST badge, status, progress), View in Scene, playback controls
  (Play green, Pause / Resume blue, Stop red, Mute), Audio (This phone: Sound, Mic; Quests: Sound, Mic), Live voice,
  Lesson notes (multi-line input + Read aloud), Important instructions (4 quick buttons), Session record card.
- New `Scripts/App/PhoneVideoPlayer.cs` (VideoPlayer / AudioSource built into the scene by the UI builder: a content://
  file is copied to the app cache first, StreamingAssets samples, plain paths; mirrors the Quests; mute = phone sound),
  `Scripts/App/PhoneSpeech.cs` + `Plugins/Android/MQSpeech.java` (Android TextToSpeech, demo read-aloud),
  `Plugins/Android/MQGallery.java` (`copyToFile`: background copy of a picked file).
- Session record: new `Scripts/Core/SessionRecord.cs` (JSON classes), `Scripts/Core/SessionLog.cs` (records actions,
  notes, instructions, audio; file `Application.persistentDataPath/MediSimVR_Sessions/<yyyy-MM-dd_HHmmss>_<role>.json`;
  autosave every 3 s when something changed; with a session service: starts / ends with the session, events = its
  timeline, uploaded at the end; without one: starts with the Control Mode role, stays on the phone), `AppRoot.cs`
  (reset + tick).
- Session interface: `ISessionService.cs` (`PhoneSound`, `QuestMics`, `Instructions`, `Speak`, `Upload`; event kinds
  Note, Instruction, Audio), `MockSessionService.cs` (demo implementation; Quest voices follow the Quest mic switch),
  `DemoConfig.cs` (instructions, `SpeakOnPhone`, `UploadSeconds`, `File.Media`), `MockControllerService.cs`
  (`streaming:` source for a demo file with a sample), `TimelineRowView.cs` (instruction colour) + builder icons.
- UI builder: `MediSimUIBuilder.cs` (5 tabs, PhoneVideo object, Devices / Send layouts, Scene back button),
  `MediSimUIBuilder.Flow.cs` (Control tab; Quest-list / Play-page / compact controls builders removed),
  `MediSimUIBuilder.Kit.cs` (big action tile proportions), `MediSimUIBuilder.Session.cs` (summary counts rich text,
  timeline icons).
- Tests: `DemoTour.cs` (rewritten for the new flow), `LayoutAuditTest.cs` (new screen list, Control at 3 scroll
  positions, 5-tab rule). `DEMO.md` rewritten.

## Tested + real results
Editor (final code):
- Smoke test (`DemoSmokeTest`, UI rebuilt, whole new DemoTour): `UI scene built ... screens 13, tabs 5`,
  `[DEMO-TEST] RESULT screens 46, errors 0  -> PASSED`, 37 `DEMO-TOUR check OK`, 0 FAILED. Checks include: bottom bar
  "Home Session Devices Send Control"; Control Mode opens Session; HOST opens Devices (step 1); offline cannot be
  selected; Connect opens Send (step 2); Send opens Control (step 3); the video plays on the Quests after Send;
  Pause pauses / Resume plays; View in Scene opens the Scene; opening the Scene pauses the video; Select tool / full
  view; back from the Scene returns to Control; the video stays paused until Play / Resume; Resume continues; Quest
  mics and phone mic can be muted; a lesson note and an important instruction are recorded and read aloud; the record is
  saved as JSON on the phone and contains the note, the instruction and the events; the record is sent to the PC at
  the end; PLAYER opens Send (step 1 of 2) with one Quest; Change > My Quest > back to Send; Send opens Control
  (player); back Control > Send > Session > Home; Settings from the Home header and back.
- Layout audit (`LayoutAuditTest`, 19 screens incl. Control at 3 scroll positions x 1080 x 1920 / 2160 / 2340 / 2400 x
  text size M / L): run 1 found 71 problems in 3 places (JSON record line too long, "Video paused for the Scene..."
  toast too long, Summary counts overlapping the saved line at text size L) -> fixed (taller record box + shorter text,
  short toast, counts in two columns with `<pos=52%>`). Final: `[LAYOUT] RESULT 152 screens checked (159 checks),
  0 problems, 0 errors  -> PASSED`. The rendered PNGs also showed the Control tiles' labels over their circles and a
  too technical hint on the player card -> big tile circle 104 / label below it, plain hint text (checked again in the
  final run and on the phone).
- Scene Play-mode test (demo OFF): `[SIM-TEST] RESULT 82 passed, 0 failed  -> ALL PASSED`.
- APK: `Build result: Succeeded`, 16,884,978 bytes (17:19).

Phone ZA223HXPHV (it was not visible to adb at first: empty `adb devices` although Windows showed "ADB Interface";
after the user allowed USB debugging it appeared):
- `adb install -r` -> `Success`, `lastUpdateTime=2026-11-12 17:22:16` (app data kept).
- Tour (`demo-screens/tour_unity_log.txt`, 399 lines, pid 10702): `AppRoot.Awake, demo mode ON` (17:22:22),
  `DEMO-TOUR done 46 screens` (17:25:37); 37 checks OK, 0 FAILED; 0 E/Unity, 0 W/Unity, 0 exceptions.
  `session record started: /storage/emulated/0/Android/data/com.medisim.vr/files/MediSimVR_Sessions/2026-11-12_172233_host.json`,
  `session record sent: Sent to PC-LAB1: MediSimVR_Sessions/2026-11-12_172233_host.json`.
- The JSON pulled from the phone (`adb pull`, 8,830 bytes): role host, start / end / 90 s, audio (liveVoice, phone
  sound / mic, Quest sound / mics), 4 Quests with state and file, `lessonNotes` ("Chest compressions: 30 per cycle,
  then 2 breaths", to Quest-A1B2 + Quest-C3D4, readAloud true), `instructions` ("Put on your gloves"), 37 events
  (Join, Content, Play, Pause incl. "paused for the Scene (2 Quest(s))", Note, Instruction, Audio, Voice, Text, Object,
  Leave), `sentToPc` "Sent to PC-LAB1: ...". A second file for the player session (4,350 bytes) exists too.
- 46 screenshots in `demo-screens/` (git-ignored), checked by eye: Home, Session, Host already active, Devices
  (Step 1 of 3, filters, offline toast, 2 selected), Send (Step 2 of 3, choose video, Replace), Control (Step 3 of 3,
  ON THE QUEST player card + hint, progress, View in Scene, Play green / Pause, Resume blue / Stop red, Mute, Audio,
  Live voice, Lesson notes, Important instructions, Session record), Scene (back button, inspector, objects, full
  view), Control after the Scene ("Playing 0 - Paused 2"), dashboard, chat, End session (red), Summary (two-column
  counts, saved on this phone + sent to PC-LAB1), player Send / My Quest / Control (Step 1-2 of 2), Home "Mode: Player",
  Settings, Edit App, Notifications, and Home / Devices / Control in the 5 themes.

### Review input note
`*.unity` scenes are excluded from the Codex diff (Main.unity is regenerated by MediSimUIBuilder and is too large);
the builder code that generates it is in the diff.

## Not verified
- Real playback of a picked video / audio on the phone (Browse phone opens the system picker, which the automatic tour
  cannot use): the copy (MQGallery.copyToFile), VideoPlayer / AudioSource paths are built and compiled, not run with a
  real file. To check by hand: Send > Select > Video > Browse phone > pick a video > Send > Control.
- Android TextToSpeech on the phone (demo read-aloud): called in the tour, not heard / confirmed (no audio capture).
- Real Quest read-aloud, real upload to the PC, Quest mics: backend (simulated in demo mode).
- Real human taps; demo mode OFF with real Quests.

## Review rounds
- Round 1: PASS with 1 minor: a late `copyToFile` answer could open a file after another file was chosen. Fixed:
  the Java answer now carries the requested path (`OK\t<path>` / `ERR\t<path>\t<message>`), `PhoneVideoPlayer` keeps the
  path it waits for (`pendingCopy`, cleared by `Clear`) and ignores other answers. Rebuilt and checked: smoke test
  `screens 46, errors 0  -> PASSED` (37 checks), APK `Build result: Succeeded` 16,885,326 bytes (Java compiled),
  `adb install -r` -> `Success` (17:30:13), app start on the phone: `AppRoot.Awake, demo mode ON`, 0 E/Unity, 0 exceptions.
- Round 2: FAIL (3 major, 1 minor). Changes / answers:
  1. No change, by design: the task asks the **Quests** to read notes / instructions aloud. The phone speaking them is
     only the demo stand-in for a Quest. With demo mode off there is no Quest receiver yet, so the note is recorded
     (`readAloud: false`) and the screen says "Saved in the session record (Quest receiver not connected)"; making the
     phone talk instead would mislead in a real class.
  2. The mic switches (phone and Quests) belong to live voice, which needs the session backend. Without it they now
     work like the live-voice switch: they stay off and say "... needs the session backend (live voice)", instead of
     changing a local flag that does nothing. With a session service they call `MicMuted` / `QuestMics` (interface
     members the real backend implements).
  3. Upload without an answer: `SessionLog` now has a 20 s timeout ("Not sent to the PC (no answer): saved on this
     phone"). The ended session's record and path are kept for the upload (`pendingRecord` / `pendingPath`), so the
     answer or the timeout is written into that session's own file even if a new session started meanwhile; a late
     answer after the timeout is ignored. The file says "Sending to the PC..." until then. New tour check: the saved
     record has the end time and the PC status.
  4. A preview error (`errorReceived`, audio load error, copy error) now clears `HasMedia` / `Ready`, so the card goes
     back to the thumbnail and "ON THE QUEST".
  Rebuilt and checked: smoke test `screens 46, errors 0  -> PASSED` with 38 checks (new: "the saved record has the end
  time and the PC status"), APK `Build result: Succeeded` 16,885,130 bytes, `adb install -r` -> `Success` (17:35:22),
  app start on the phone: `AppRoot.Awake, demo mode ON`, 0 E/Unity, 0 exceptions.
- Round 3: PASS (REVIEW.md) with 1 minor, not changed: two sessions of the same role started in the same second
  would share one file name (start time has 1 s precision); starting a session takes several taps, so this cannot
  happen in normal use.
