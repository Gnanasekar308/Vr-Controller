# MediSimVR phone app: 5-minute demo

Demo mode is ON (Settings > Demo mode). Everything runs on sample data: no Quest, PC or Wi-Fi needed.
Sample Quests: Quest-A1B2, Quest-C3D4, Quest-E5F6 (offline), Quest-G7H8.
Sample Quests, files, chat messages and replies, demo texts, session colours and timings live in one file:
`Assets/MediSimVR/Scripts/Demo/DemoConfig.cs` (screen titles and button labels are in the UI builder).

Before you start: open the app once, so it is on the Home screen. Keep the phone in portrait.

## 0:00 Home (20 s)
1. Show the **Home** screen: app name, Control Mode / Receiver Mode, and the chip **"Mode: Not in a session"** with the **DEMO** badge.
2. Say: "The phone controls every Quest headset in the training room."

## 0:20 Devices, Send, Control (60 s)
3. Tap **Devices** (bottom bar): 4 Quests, battery, online / offline, "In Use" badge on the Quests that play something.
4. Tap **Send**: one item at a time (video, audio, image or text) goes to the selected Quests.
5. Tap **Control**: Play / Pause / Resume / Stop / Mute for the selected Quests.

## 1:20 Session: Host (2 min)
6. Tap **Session** (bottom bar): two big cards, **HOST** and **PLAYER**.
7. Tap **HOST**. The message **"Host already active"** appears (another phone hosts the room). Tap **Take over**.
8. The **Host** dashboard:
   - Top: **"● Recording on PC-LAB1 00:12:3x"** (the PC records the session) and the session clock.
   - **Live voice** bar: switch, **Mute me**, and **"Speaking: Quest-..."**. The green speaker icon on a Quest card pulses while that trainee talks.
   - Quest cards: name, online + battery, what plays, muted icon. Tap a card to select / unselect, or **Select all**.
9. Tap **Play**, then **Pause**: toast "... on 3 Quest(s)", and the cards change to "Playing" / "Paused".
10. Tap **Send**, pick **CPR_Training.mp4**: it is sent to the selected Quests.
11. Tap **Text**: tap the quick message **"Look at the patient"**. After 2 to 4 seconds the Quests reply in chat bubbles ("OK, starting now", "Done!"...). Close the sheet with **X**.
12. Scroll down: the **Live session timeline** lists every action: play, pause, content, text + replies, voice, joined / left. (About 50 s into the session Quest-G7H8 "left (Wi-Fi lost)" and joins again 15 s later.)

## 3:20 Scene (45 s)
13. Select ONE Quest card, tap **Scene**: the Scene tab opens for that Quest ("Demo: Quest-...").
14. Tap the blue **Cube**: orange outline + red / green / blue arrows. Drag the red arrow: the cube moves only sideways.
15. Tap **Inspector**: type a number, or drag the X label left / right. **Undo** / **Redo** at the top left.
16. Go back to **Session** > tap the running-session card: the timeline now also shows "moved Cube ...".

## 4:05 End + Player (45 s)
17. Tap **End session** > **End session**: the **Summary** shows duration, number of Quests, event counts and
    **"Saved on PC: MediSimVR_Sessions/<date>_host.json"**.
18. Tap **New session** > **PLAYER** > pick **Quest-C3D4**: the single-Quest screen (status, Play / Pause / Stop / Mute,
    live voice, **Open in Scene**). Tap **Leave** to finish.

## 4:50 Look and feel (10 s)
19. **Settings** > **App Customization**: switch the theme (Default, Dark, Light, Ocean, Purple); every screen follows.

## If something goes wrong
- Toast "Select at least one online Quest": tap **Select all** on the Host dashboard.
- To restart the demo from zero: close the app from the recent-apps list and open it again.
- Demo mode OFF: Settings > **Demo mode** > confirm. The app restarts with the real network (no sample Quests).
  Open the **Session** tab: it says the session backend is not connected yet. Turn Demo mode ON the same way to go back.
