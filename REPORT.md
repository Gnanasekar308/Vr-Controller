# REPORT

## Task
UI-only redesign for consistency (no backend, API, network, Quest or Unity communication changes):
1. One design system on every screen: palette (Primary Blue #2385ED, Teal #16B9A9, Dark Text #202D3D, Secondary Text
   #89919E, Background #F1F6FA, Card #FFFFFF, success / warning / error), one typography scale, one spacing scale.
2. Consistent buttons by meaning: Primary (teal-to-blue gradient), Secondary, Tertiary, Destructive, Icon button;
   Home Control Mode = Session HOST style; every Cancel the same; Stop / Remove / End session destructive.
3. Home: Settings icon (left) + Notifications (right) in the header, same icon button design; Settings removed from the
   bottom bar (6 tabs); Settings with back navigation.
4. Session: HOST / PLAYER in the same card system as Home; clear active-session card (duration, LIVE indicator, tappable).
5. Devices, Send, Control, session dashboard: consistent cards, badges, buttons; Control: Play green, Pause / Resume
   blue, Stop red, Mute neutral, two columns.
6. Scene tab: large viewport as the main element, correct aspect ratio (no stretching), full view, connection
   indicator, "Waiting for Unity Scene" placeholder, no fake scene presented as live Unity; main toolbar Select / Move /
   Rotate / Scale, secondary toolbar Local-Global / Pivot-Center / Snap / Lock, camera Top / Front / Side / 3D / Focus,
   Undo / Redo / Reset; Objects and Inspector kept.
7. Responsive: no clipped text, no overlapping buttons, scrolling where needed, on different Android screen sizes.

## What was found (inspection)
- Buttons came from ~15 separate builder helpers (GradientButton, SmallAction, BarButton, HeaderPill, ActionTile, Tile,
  ViewPill, popup / action sheet / color picker buttons...), so the same kind of action looked different per screen.
- Home `ModeCard` (220 high, icon without circle, title 48) and Session `RoleCard` (300 high, icon in circle, title 62)
  were two different designs for the same kind of card.
- Hard-coded colours outside the palette: In Use `#2563EB`, Online `#22C55E`, chat `#1A7FE8` (old primary),
  `DemoConfig` hex colours for speaking / recording / muted.
- Scene tab: the existing integration (SceneSync) receives the scene's objects (shape, colour, pose) from the running
  Unity (PC Play mode / Quest) and the phone renders them with its own camera into a RenderTexture. It is kept as it is.
  Found: the RenderTexture was clamped to 2048 per side separately, so a full-height view on the 1272x2772 phone
  (~2360 px tall) would have been stretched.

## Files changed (phone repo)
- New `Editor/MediSimUIBuilder.Kit.cs`: the design system: tokens (margin 40, card gap 24, card padding 32, button 120,
  small button 84, icon button 112, hero card 236, chip 64, shadows), text styles (SectionTitle, CardTitle, RowTitle,
  BodyText, Secondary, SmallLabel), `TextBoxH()` (box height that shows one line at text size L), components `Btn`
  (Primary / Secondary / Tertiary / Destructive), `BottomButton`, `HeaderAction`, `ActionCard`, `Panel`, `StatusChip`,
  `ActionTile` (round coloured icon + label).
- `Editor/MediSimUIBuilder.cs`: icon buttons 112 (CircleButton), Home header (Settings left, Notifications right) +
  ActionCards, Devices summary card + bottom Primary button, Send (scroll list, Select Secondary + Send Primary),
  Control (scroll list), Scene (new layout, below), Settings (back button, rows), bottom bar 6 tabs (NavH 200),
  popup / action sheet / color picker / text editor buttons from `Btn`; `GradientButton`, `ModeCard`, `SmallAction`,
  `Tile` removed; `Seg` got a stacked variant (icon above label).
- `Editor/MediSimUIBuilder.Session.cs`: rewritten on the kit: HOST / PLAYER = ActionCard, active-session card (green
  accent bar, LIVE chip with pulsing dot, duration), dashboard with destructive End session, tiles with semantic
  colours, text sheet quick messages (Tertiary), Player dashboard, Summary (New session Primary + Home Secondary).
- `Editor/MediSimUIBuilder.Flow.cs`: Quest list, Play page (one scroll list), Devices card + StatusChip, preview card,
  `ControlsBig` / `ControlsCompact` on the kit (Play green, Pause / Resume blue, Stop red, Mute neutral).
- Scene: `SceneScreen.cs` (Select tool index 0, full view `SetFullView` + `HandleBack` (closes a sheet, then the full
  view), placeholder "Waiting for Unity Scene" / "Loading demo scene", demo badge "DEMO SCENE: sample objects", status
  "Demo scene: ..." / "Live from Unity: ..." with a short Quest list, RenderTexture keeps the viewport aspect when
  limited to 2048), `SceneEditController.cs` (`SelectOnly`: no gizmo, no gizmo drag or pinch-scale; taps still select).
- Theme: `ThemeData.cs` (Default preset = the MediSimVR palette, presets with pill buttons and 20 px corners,
  `ColorRole.Success`), `ThemeManager.cs` (SubText = #8B929A for the default theme; a saved named preset takes the
  preset's current colours on load).
- Colours from the theme instead of hex: `DeviceStatus.cs` (Online Success, In Use Primary, Offline SubText),
  `DemoConfig.cs` (speaking / recording / muted / own chat / LIVE as theme roles), `SpeakingPulse.cs`,
  `TimelineRowView.cs`, `ChatBubbleView.cs`, `SessionHostScreen.cs`.
- Screens: `HomeScreen.cs` (settingsButton), `SettingsScreen.cs` (backButton), `SessionStartScreen.cs` (active card
  fields), `SendScreen.cs` (subtitle "Send to: ..."), `Popup.cs` (`destructive`: solid red OK) used by End session,
  Leave, Switch role; `Navigator.cs` (Settings / Edit App highlight Home).
- Tests: new `Editor/LayoutAuditTest.cs` (below), `DemoTour.cs` (Settings from the Home header + back checks, 6 tabs,
  Select tool, full view + back, Home in every theme), `SceneSimTest.cs` (tool index shifted by the Select tool).
- `DEMO.md` (Settings via the gear icon, Scene full view), `.gitignore` (`/layout-screens/`).

## Tested + real results
New check: `Editor/LayoutAuditTest.cs` (batchmode `LayoutAuditTest.Run`). It plays the app in demo mode and lays the
real UI out in world space at 1080 x 1920 (16:9), 2160 (18:9), 2340 (19.5:9) and 2400 (20:9), each at text size M and
L, opens 18 screens (Home, Session, Quest list, Play host / player, host dashboard, Devices, Send, Control, Scene,
Scene full view, Settings, Edit App, Notifications, My Quest, Player dashboard, Summary, End-session popup) and checks
hidden text (source text non-empty but nothing shown), clipped text ("..."), overlapping texts / buttons, buttons off
the screen or wider than their list; plus the design rules (Home Control Mode card = Session HOST card, Receiver Mode =
PLAYER, 6 tabs without Settings, Settings button in the Home header with the same size as Notifications, every primary
button 120 high, every Cancel the secondary style). Each screen is saved as a PNG in `layout-screens/` (git-ignored).

What it found and what was fixed (runs 2-5):
- Compile error CS1503 (out parameter type, Devices summary) - fixed.
- Home vs Session cards not identical (first check), colour picker popup still had old 100-high OK / Cancel -> now
  `Btn` Secondary + Primary; after that: "Home Control Mode and Session HOST use the same primary card" OK,
  "every Cancel button is the secondary style (4/4)" OK, "primary buttons found: 11" all 120 high.
- Clipped text: LIVE chip, Scene tool labels ("Rotat..."), "On release", demo badge, Scene status line -> chips wider,
  5 tools stacked (icon above label), status list shortened ("Quest-A1B2, Quest-C3D4 +1").
- Rendered PNGs showed the compact action tiles and the Scene toolbar without labels: their 36-high boxes do not show a
  27 pt line at text size L (TextMeshPro hides it; this PC's Editor theme uses L). The audit then missed it because it
  read the rendered text; it now reads the source text and runs every screen at M and L. Boxes now use `TextBoxH()`.
- Text size L: live-session duration (64 box, 53.8 pt) and dashboard Quest card content line (42 box, 34.7 pt) hidden ->
  `TextBoxH()` (70 / 46), Quest card 176 high.
- Final run 5: `[LAYOUT] RESULT 144 screens checked (151 checks), 0 problems, 0 errors  -> PASSED`.

Other tests (final code):
- Editor smoke test (`DemoSmokeTest`, UI rebuilt, whole DemoTour): `UI scene built ... screens 15, tabs 6`,
  `[DEMO-TEST] RESULT screens 48, errors 0  -> PASSED`, 24 `DEMO-TOUR check OK` (incl. "bottom navigation: 6 tabs,
  Settings is in the Home header", "the Settings icon in the Home header opens Settings", "back from Edit App goes to
  Settings", "the Settings back button goes Home", "Select tool hides the gizmo", "Move tool shows the gizmo again",
  "the expand button opens the full view", "back leaves the full view and stays on Scene", and all Control Mode flow
  checks).
- Scene Play-mode test (`SceneSimTest`, demo forced OFF, real SceneSync path): `[SIM-TEST] RESULT 82 passed, 0 failed
  -> ALL PASSED`.
- APK: `Build result: Succeeded`, 16,358,356 bytes (15:55). Phone ZA223HXPHV: `adb install -r` -> `Success`,
  `lastUpdateTime=2026-11-12 15:55:20` (update in place, app data kept).
- Phone tour (`demo-screens/tour_unity_log.txt`, 343 lines, pid 23618): `AppRoot.Awake, demo mode ON` (15:55:26),
  `Demo scene loaded (6 objects)`, `DEMO-TOUR done 48 screens` (15:58:51); 24 checks OK, 0 FAILED; 0 E/Unity,
  0 W/Unity, 0 exceptions, 0 `not a Unity engine type`. 48 screenshots in `demo-screens/` (git-ignored), checked by eye:
  Home (Settings left, Notifications right), Session (HOST = Control Mode card, PLAYER = Receiver Mode card, LIVE card
  with duration), Quest list, Play page, dashboard, chat, Scene (toolbars, viewport, inspector, objects, full view, demo
  badge), Summary, Player, Devices, Send, Control (Play green, Pause / Resume blue, Stop red, Mute neutral), Settings
  (back, Home tab highlighted), Edit App, Notifications, and Home / Quest list / Play page in the Default, Dark, Light,
  Ocean and Purple themes.

### Review input note
`*.unity` scenes are excluded from the Codex diff (Main.unity is regenerated by MediSimUIBuilder and is too large);
the builder code that generates it is in the diff.

## Not verified
- Real human taps on the phone: the tour presses the app's own buttons from code.
- Live Unity scene with demo mode OFF (PC Play mode / Quest on the Wi-Fi): the Scene viewport, placeholder and full view
  are tested with the demo scene and in the Editor Scene test only. Note: the phone shows the scene's objects sent by
  SceneSync (shapes, colours, poses), not a video picture of the Unity camera; a pixel-exact stream would need backend
  work and is out of this UI task.
- Screen sizes: checked in the Editor at 1080 x 1920 / 2160 / 2340 / 2400 (text size M and L) and on one real phone
  (1272 x 2772); not on a small physical phone or a tablet.

## Review rounds
- Round 1: PASS, "Issues: none" (REVIEW.md).
