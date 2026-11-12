# MediSimVR phone app: 5-minute demo

Demo mode is ON (Home > gear icon (Settings) > Demo mode). Everything runs on sample data: no Quest, PC or Wi-Fi needed.
Bottom bar: **Home, Session, Devices, Send, Control** (the Scene opens from Control).
Sample Quests: Quest-A1B2 (in use: plays Anatomy_Heart_360.mp4), Quest-C3D4, Quest-E5F6 (offline), Quest-G7H8.
Sample Quests, files, instructions, chat replies, demo texts and timings live in one file:
`Assets/MediSimVR/Scripts/Demo/DemoConfig.cs`.

Before you start: close the app from recent apps and open it again (fresh session). Portrait. Phone volume up (in demo
the phone speaks what the Quests read aloud).
Back always works the same (arrow at the top left or the Android back button).

## 0:00 Home (15 s)
1. **Home**: Settings (gear, top left), Notifications (bell, top right), **Control Mode**, Receiver Mode, chip
   **"Mode: Not in a session"** with the **DEMO** badge.

## 0:15 Control Mode > Session > HOST (20 s)
2. Tap **Control Mode**: **Session** with **HOST** "Control all Quests in the room" and **PLAYER** "Play on your own Quest".
3. Tap **HOST**: **"Host already active"** (another phone hosts the room) > **Take over**.

## 0:35 Step 1: Devices (45 s)
4. The **Devices** tab opens ("Step 1 of 3"): filter chips **All / Online / Offline / In Use** with counts, Quest cards
   with status (Online green, Offline grey, In Use blue + what plays), battery and IP.
5. Tap **Quest-E5F6** (offline): it cannot be selected. Tap **Quest-A1B2** and **Quest-C3D4** (or **Select all online**).
6. Tap **Connect (2)**.

## 1:20 Step 2: Send (30 s)
7. The **Send** tab opens ("Step 2 of 3"): "Send to: Quest-A1B2, Quest-C3D4" (Change = back to Devices).
8. **Select** > **Video** > **CPR_Training.mp4** (or **Browse phone** for a real video from this phone).
9. **Send (2)**. Quest-A1B2 already plays a video: **Replace**.

## 1:50 Step 3: Control (2 min)
10. The **Control** tab opens ("Step 3 of 3"): the player card shows what plays (**ON THIS PHONE** for a file picked
    on the phone: the video plays here too; **ON THE QUEST** for the demo samples), progress bar, **View in Scene**.
11. **Pause** / **Resume** / **Stop** / **Mute** act on the Quests (and on this phone's preview).
12. **View in Scene**: the video **pauses**; the Live Scene opens (DEMO SCENE badge). Tap the cube, drag an arrow, the
    expand button = full view. Back arrow: Control again, still **paused**; **Resume** continues.
13. Scroll down:
    - **Audio**: This phone (sender) Sound / Mic, Quests (receivers) Sound / Mic.
    - **Live voice** on / off and who is speaking.
    - **Lesson notes**: type a note > **Read aloud**: the Quests read it aloud (demo: you hear it from the phone).
    - **Important instructions**: tap **"Put on your gloves"**: read aloud on the Quests.
    - **Session record (JSON)**: the file name on this phone.

## 3:50 End (30 s)
14. **Session** tab > **Live host session** card: voice, chat, timeline of everything (notes and instructions too).
15. **End session** > **End session**: the **Summary**; "Saved on this phone: MediSimVR_Sessions/<date>_host.json" and,
    after a moment, **"Sent to PC-LAB1"**.

## 4:20 PLAYER (30 s)
16. **New session** > **PLAYER**: straight to **Send** on **My Quest** ("Step 1 of 2"); **Change** picks another Quest.
17. Select > Audio > Briefing_Audio.mp3 > **Send**: **Control** for that one Quest.

## 4:50 Look and feel (10 s)
18. Home > gear > **App Customization**: switch the theme (Default, Dark, Light, Ocean, Purple).

## The JSON session record
- On the phone: `Android/data/com.medisim.vr/files/MediSimVR_Sessions/` (copy over USB), one file per session, saved
  every few seconds while it runs: role, Quests, audio settings, lesson notes, important instructions, every event.
- Sent to the PC recorder when the session ends. In demo mode this is simulated ("Sent to PC-LAB1"); the real PC
  recorder is part of the backend.

## If something goes wrong
- "Select at least one online Quest": tap **Select all online** on Devices.
- To restart the demo from zero: close the app from the recent-apps list and open it again.
- Demo mode OFF: Home > gear > **Demo mode** > confirm. The app restarts with the real network. The flow is the same;
  live voice, read-aloud on the Quests and the PC upload need the backend and say so; the JSON record stays on the phone.
