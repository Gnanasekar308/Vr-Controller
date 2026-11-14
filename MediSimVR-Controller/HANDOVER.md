# MediSimVR phone app (Controller): handover guide

For the developer who takes over the phone app and connects it to the backend.
Date: 2026-11-14. Owner until now: Gnanasekar (with Claude Code).

## 1. Status in one minute

- **UI: complete in demo mode.** Every screen works on sample data (no Quest, PC or server needed).
- **Backend: not built yet.** Real Quest control (discover, send, play / pause / stop) already exists. Live classroom
  data (login, meetings, students, metrics, questions, comments, recording, report upload) is mock data today.
  Section 6 says where each part plugs in.
- **Tested** on a Motorola phone (2772 x 1272) with an automatic demo tour: 46 screens, 66 checks, 0 errors.
  Editor layout audit: 236 screens at phone and tablet sizes, 0 problems. Never tested: a real tablet, real users,
  demo mode OFF with real Quests in a classroom.

> Code: GitHub `main` at `527da3c` (2026-11-14) has everything in this guide.

## 1a. What you receive (handover package)

| Item | Where | Why |
|---|---|---|
| **Source code** (whole repo) | private GitHub repo, branch `main` (access from Gnanasekar), or a zip of the repo folder **without** `MediSimVR-Controller/Library`, `Temp`, `Logs`, `Builds` and without `backup/`, `main-backup/`, `*.zip` | the project you work in |
| **This guide** | `HANDOVER.md` (repo root) | start here |
| **Phone APK** (current build) | `MediSimVR-Controller/Builds/MediSimVR-Controller.apk` (not in git, sent as a file) | install and try the app without building |
| **Demo script** | `DEMO.md` (repo root) | how the demo is shown (some screens changed since, see section 4) |
| **Controller + Receiver integration guide** | `MediSimVR_Controller_and_Receiver_Integration_V1_Guide_1.pdf` (repo root) | the protocol between this app and the Quest receiver |
| **Quest receiver project** | separate project `MediSimVR-Receiver` (ask the receiver developer) | the other side of the network; shared files must stay identical (section 6.1) |

Keep the repo and the APK private: the Quest auth key is inside them. Do not send them over public links.

**Demo mode stays.** Settings > Demo mode switches between sample data (ON, default) and the real network (OFF).
Keep it working while you build the backend: it is how the app is shown and how the automatic tests run.

## 2. Get started

| What | Value |
|---|---|
| Repo | PRIVATE GitHub repo (keep it private: the Quest auth key is in the code). Ask for access. |
| Unity | **2021.3.45f2** only. Android build support. |
| Unity project folder | `MediSimVR-Controller/` (inside the repo root) |
| Main scene | `Assets/MediSimVR/Scenes/Main.unity` (generated, see 5.1) |
| App id | `com.medisim.vr` (never change it) |
| Quest receiver app | separate project `MediSimVR-Receiver` (not in this repo) |

Steps:
1. Clone the repo, open `MediSimVR-Controller/` in Unity Hub with 2021.3.45f2.
2. Menu **MediSimVR > Build UI Scene** (creates `Main.unity`), then press Play: the app starts in demo mode.
3. Build the APK: **MediSimVR > Build Phone APK** -> `MediSimVR-Controller/Builds/MediSimVR-Controller.apk`.
4. Install: `adb install -r <apk>` (`-r` keeps the app data). If Android refuses the update (signature mismatch),
   the APK was built on another PC with another debug key: uninstall the old app once, then install.
5. Demo login: on the role screen tap ADMIN or STUDENT, then **Demo admin** / **Demo student**.

## 3. Folder map

Repo root:

| Path | What |
|---|---|
| `MediSimVR-Controller/` | the Unity project |
| `DEMO.md` | click-by-click demo script (partly older than the latest Live redesign) |
| `HANDOVER.md` | this guide |
| `CLAUDE.md`, `AGENTS.md` | rules used by the AI tools (work loop, review checklist) |
| `Tools/capture-demo-screens.sh` | runs the demo tour on a USB phone and saves screenshots to `demo-screens/` |
| `REPORT.md`, `REVIEW.md` | last task report / review (history) |
| `backup/`, `main-backup/`, `*.zip`, `apk-file/` | old copies, ignored by git, do not use |

Inside `MediSimVR-Controller/Assets/MediSimVR/`:

| Folder | What |
|---|---|
| `Scripts/App/` | `AppRoot` (start, picks demo or real services), `Navigator` (screens, back, bottom bar), `ControlFlow`, `ContentChooser` (file picker + send), `PhoneVideoPlayer`, `PhoneSpeech`, `OrientationScaler` |
| `Scripts/Core/` | **data + service interfaces**: `IControllerService`, `ISessionService`, `Models`, `Classroom` (classroom data, today mock), `SessionLog` + `SessionRecord` (JSON session record), `MockControllerService` |
| `Scripts/Net/` | **real Quest network**: `NetworkControllerService`, `QuestLink`, `Protocol`, `Transfer`, `MediSimAuth` |
| `Scripts/SceneSync/` | Live Scene sync with the Quest / PC (**files must stay identical in the receiver project**) |
| `Scripts/Scene3D/`, `Scripts/SceneEdit/` | the 3D Live Scene view and editing (gizmo, camera, undo) |
| `Scripts/Demo/` | `DemoConfig` (**all demo data and texts**), `MockSessionService`, `DemoTour` (automatic test tour), `DemoSceneLink` |
| `Scripts/UI/Screens/` | one script per screen (behaviour) |
| `Scripts/UI/Widgets/` | rows, popups, toggles, meters... |
| `Scripts/Theme/` | themes, colours, text styles |
| `Editor/` | `MediSimUIBuilder*.cs` (**builds the whole UI**), `PhoneBuild`, `DemoSmokeTest`, `LayoutAuditTest`, `SceneSimTest` |
| `Plugins/Android/` | Java helpers: gallery picker / file copy, text-to-speech |
| `Art/`, `Fonts/` | icons (Material), shapes, logo, Inter font |

## 4. App flow and the files behind each screen

Splash -> Role (ADMIN / STUDENT) -> Login (mock) -> Dashboard. Bottom bar: admin Dashboard, Meetings, Live, Devices,
Settings; student Dashboard, Meetings, Settings. Live is full screen landscape (no bottom bar).

| Screen | Behaviour script (`Scripts/UI/Screens/`) | Layout (`Editor/`) |
|---|---|---|
| Splash, Role, Login | `SplashScreen`, `RoleSelectScreen`, `LoginScreen` | `MediSimUIBuilder.Classroom.cs` |
| Dashboard (admin + student) | `DashboardScreen` | `MediSimUIBuilder.Classroom.cs` |
| Meeting Room / Available meetings | `MeetingsScreen` | `MediSimUIBuilder.Classroom.cs` |
| Create Meeting (online Quests only, each with its student; **Start Live** saves the meeting and starts its live class) | `MeetingCreateScreen` | `MediSimUIBuilder.Classroom.cs` |
| Meeting details (attendance, Start / Join, Delete) | `MeetingDetailsScreen` | `MediSimUIBuilder.Classroom.cs` |
| Live classroom (metrics + sliders, questions, comments, Quest view, students, control bar, REC) | `LiveSessionScreen` | `MediSimUIBuilder.Live.cs` |
| Session report | `SessionReportScreen` | `MediSimUIBuilder.Live.cs` |
| Devices, Send, Control, Live Scene | `DevicesScreen`, `SendScreen`, `ControlScreen`, `SceneScreen` | `MediSimUIBuilder.cs`, `.Flow.cs` |
| Settings (account, logout, demo mode), Edit App, Notifications | `SettingsScreen`, `EditAppScreen`, `NotificationsScreen` | `MediSimUIBuilder.cs` |
| Bottom bar | `Scripts/App/Navigator.cs` | `MediSimUIBuilder.cs` (`BuildNav`) |

## 5. How to change the UI

### 5.1 Rules
1. **Never edit `Main.unity` by hand.** It is generated. Change the builder (`Editor/MediSimUIBuilder*.cs`), then
   run **MediSimVR > Build UI Scene**. Hand edits are lost at the next build.
2. Layout / sizes / texts in the builder; what a tap does in the screen script.
3. A new button: create it in the builder, assign it to a public field of the screen script, add the listener in the
   screen's `Init()`.
4. Use the design tokens in `Editor/MediSimUIBuilder.Kit.cs` (Margin, CardGap, BtnH...) and colour roles
   (`ColorRole`), not raw numbers / hex. Theme colours: `Scripts/Theme/ThemeData.cs`.
5. One MonoBehaviour per file, file name = class name.
6. The phone app has **no XR**: never add OpenXR / XR Plugin Management.
7. Never print the auth key (`MediSimAuth`) in logs or documents.

### 5.2 Demo data and texts
Everything the demo shows is in **`Scripts/Demo/DemoConfig.cs`**: admin, 8 students (name, ID, base metrics),
8 Quests (each with the student signed in on it), 3 meetings, questions, comments, instructions, files, timings,
alert levels. Change a value, rebuild, install.

### 5.3 Test after every change
Unity menu **MediSimVR**:
- **Run Demo Smoke Test**: builds the UI and runs the whole demo tour in Play mode. Must end `RESULT ... PASSED`.
- **Run Layout Audit**: renders every screen at phone sizes (1080 x 1920 to 2400, text size M and L) and the Live
  class at phone and tablet landscape. Reports hidden / clipped / overlapping texts and buttons. Must be `0 problems`.
  Pictures in `layout-screens/`.
- **Run Scene Sim Test**: Live Scene sync (needs `MediSimVR-Receiver/Tools/Send-SceneSyncTest.ps1`).
- On a phone (USB debugging on): `bash Tools/capture-demo-screens.sh <serial> demo-screens` runs the tour on the
  phone and saves a screenshot of every step (demo mode must be ON).

Batch mode (Editor closed) example:
```
Unity.exe -batchmode -projectPath <repo>\MediSimVR-Controller -executeMethod MediSimVR.EditorTools.DemoSmokeTest.Run -logFile smoke.log
```
(`LayoutAuditTest.Run`, `SceneSimTest.Run`, `PhoneBuild.Build` the same way; `PhoneBuild.Build` with `-quit`.)

## 6. Connecting the backend

The UI only talks to these classes. Replace the mock behind them; the screens do not need to change.

`Scripts/App/AppRoot.cs` (`Awake`) chooses the services:
- demo ON: `MockControllerService` + `MockSessionService`
- demo OFF: `NetworkControllerService` (real Quests) + **no session service yet** (`Session = null`)

### 6.1 Quest control: `IControllerService` (exists)
`Scripts/Core/IControllerService.cs`, real implementation `Scripts/Net/NetworkControllerService.cs`.
- Discovery: UDP beacon port 8766 (only app "MediSimVR"). Control: TCP 8765, one `QuestLink` per Quest.
  File transfer: TCP 8767 (`Transfer.cs`, SHA-256 checked). Live Scene: UDP 8771 (`SceneSync/`).
- Auth: challenge / HMAC (`MediSimAuth.cs`), the **same file in the receiver project**; keep them identical.
- Methods the UI uses: `Devices`, `Files`, `SelectedOnline()`, `Send`, `Play`, `Pause`, `Resume`, `Stop`, `SetMute`,
  `ShowText`, `Refresh`, `AddManualDevice`, notifications. Phase 1 supports video; check audio / image / text with
  the receiver team.
- To do: make sure the receiver reports per Quest **battery, state, playing file and position** (the Live screen and
  the report show them).

### 6.2 Session: `ISessionService` (to build)
`Scripts/Core/ISessionService.cs`; demo implementation `Scripts/Demo/MockSessionService.cs` shows the expected
behaviour. Implement it against your server, then create it in `AppRoot.Awake` for demo OFF.
- `StartHost` / `StartPlayer` / `End`: called by the Live class Start / Join / End Class / Leave.
- `LiveVoice`, `MicMuted`, `PhoneSound`, `QuestMics`, `SpeakingDeviceId`: voice and mute state.
- `Speak(kind, text, quests)`: Instruction button and lesson notes; the Quests read the text aloud.
- `Instructions`: the instruction list (Instruction button).
- `Upload(fileName, json, done)`: sends the JSON session record to the PC / server at the end.
- `Log`, `Timeline`, `Chat`, `SendText`: events and chat.

### 6.3 Session record (JSON): `SessionLog`
`Scripts/Core/SessionLog.cs` + `SessionRecord.cs`. Saved on the phone every 3 s while a session runs:
`Android/data/com.medisim.vr/files/MediSimVR_Sessions/<date>_<host|player>.json` (role, Quests, audio, notes,
instructions, events, PC status). At the end it calls `ISessionService.Upload` (20 s timeout). Keep the format or
version it if the server needs changes.

### 6.4 Classroom: `Classroom` (mock today, to replace)
`Scripts/Core/Classroom.cs` is a static class filled from `DemoConfig` in `Reset()`. The screens read and call only
this class, so this is where the backend goes. Suggested: create an `IClassroomService` with the same members and
make `Classroom` call it. Points to replace:

| Member | Today (mock) | Backend |
|---|---|---|
| `Login(role, email)` | any email + password works | real login (the password is in `LoginScreen`, currently not checked) |
| `Students`, `Meetings`, `Add`, `Delete`, `Meeting.Absent` | from `DemoConfig`, kept in memory only (lost on restart) | load / create / delete / attendance on the server |
| `StudentOfQuest(questId)` | `DemoConfig.Quests[].StudentId` | the student name + ID entered **on the Quest** (receiver sends it, e.g. in the beacon or on connect) |
| `Tick` metrics (attention, engagement, participation, emotion) | random walk around each student's base | real values from the Quests / analytics |
| `SetLevel(kind, value)` (admin metric sliders) | shifts the demo values | decide with the team what the admin slider controls on the Quest side (e.g. difficulty / target level), then send it |
| `Questions`, `Comments`, `Answer` | demo lines every 25 s / 9 s | real messages from the students |
| `Recording`, `SetRecording` | timer + red REC chip only | start / stop the real class recording |
| `End(...)` -> `SessionReport` | built on the phone | can stay; optionally upload it with the session record |
| `ShowEngagement / ShowAttention / ShowParticipation`, `AlertLevel` | admin display settings (local) | can stay local |

Other mock points:
- `SessionReportScreen` Share button: only a toast (`DemoConfig.ShareText`); connect to e-mail / PDF export.
- Demo mode switch: Settings > Demo mode (stored in `PlayerPrefs`).

### 6.5 Suggested order
1. Test the real Quest link (demo OFF) with the receiver app on one Quest: discovery, connect, send, play / pause.
2. Receiver sends student name + ID per Quest -> `Classroom.StudentOfQuest`.
3. Server login + meetings (`Classroom` / `IClassroomService`).
4. `ISessionService` (start / end session, upload of the JSON record).
5. Live metrics, questions, comments, recording.

## 7. Known limits

- Live class is fixed to landscape-left (the phone's rotation lock stopped auto-rotation); upside-down landscape is not used.
- No real tablet tested; tablet landscape only in the Editor layout audit.
- Real playback of a video picked on the phone and the phone text-to-speech were never checked by ear / eye.
- Meetings created in demo mode are not saved after an app restart.
- `DEMO.md` and `UI_CHANGE_GUIDE.md` describe an earlier version of some screens; this guide is newer.
