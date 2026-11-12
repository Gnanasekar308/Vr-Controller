# MediSimVR phone app: 5-minute demo

Demo mode is ON (Settings > Demo mode). Everything runs on sample data: no Quest, PC or Wi-Fi needed.
Sample Quests: Quest-A1B2 (in use: plays Anatomy_Heart_360.mp4), Quest-C3D4, Quest-E5F6 (offline), Quest-G7H8.
Sample Quests, files, chat messages and replies, demo texts, session colours and timings live in one file:
`Assets/MediSimVR/Scripts/Demo/DemoConfig.cs` (screen titles and button labels are in the UI builder).

Before you start: open the app once, so it is on the Home screen. Keep the phone in portrait.
Back always works the same (arrow at the top left or the Android back button):
Play page > Quest list (Host) or Session (Player) > Home.

## 0:00 Home (15 s)
1. Show **Home**: app name, **Control Mode**, Receiver Mode, and the chip **"Mode: Not in a session"** with the **DEMO** badge.
2. Say: "The phone controls every Quest headset in the training room."

## 0:15 Control Mode > Session (15 s)
3. Tap **Control Mode**: the **Session** screen with two big cards:
   **HOST** "Control all Quests in the room" and **PLAYER** "Play on your own Quest".

## 0:30 HOST > Quest list (60 s)
4. Tap **HOST**. The message **"Host already active"** appears (another phone hosts the room). Tap **Take over**.
5. The **Quest list**: each card shows name, battery + IP, what it plays, and a status chip:
   **Online** (green), **Offline** (grey), **In Use** (blue, Quest-A1B2 "Playing Anatomy_Heart_360.mp4").
6. Tap the filter chips at the top: **In Use (1)**, **Offline (1)**, back to **All (4)**.
7. Tap **Quest-E5F6** (offline): toast "offline: it cannot be selected".
8. Tap **Quest-A1B2** and **Quest-C3D4** (or **Select all online**). The button shows **Continue (2)**. Tap it.

## 1:30 Play page as Host (90 s)
9. The header says **"Mode: Host"**; the chip under it says **"Playing on: Quest-A1B2, Quest-C3D4"** (Change = back to the list).
10. Tap **Video**: the sheet lists the demo videos (and Browse phone). Pick **CPR_Training.mp4**: the preview card shows it.
11. Tap **Send & Play (2)**. Quest-A1B2 already plays another video, so the app asks **"Replace content?"**: tap **Replace**.
12. After a second the controls appear on the same page: progress bar (time moves), **Play, Pause, Resume, Stop**, **Mute**.
    Tap **Pause** (the time stops), then **Resume**.
13. Tap **Image**, pick **Heart_Diagram.png**, **Send & Show**, Replace: the controls change to **Show on Quest / Hide**.
14. Tap the back arrow: the **Quest list** now shows both Quests as **In Use** with what they show.

## 3:00 Live session dashboard (45 s)
15. Back again to **Session**: the card **"Live host session"** is there. Tap it: the live dashboard
    (**"● Recording on PC-LAB1 00:12:3x"**, live voice + who is speaking, timeline of everything you just did).
16. Tap **Text** > **"Look at the patient"**: the Quests reply in chat bubbles after 2 to 4 seconds. Close with **X**.
17. Tap **End session** > **End session**: the **Summary** (duration, Quests, event counts,
    **"Saved on PC: MediSimVR_Sessions/<date>_host.json"**).

## 3:45 PLAYER (60 s)
18. Tap **New session** (or the Session tab) > **PLAYER**: straight to the **Play page**, no list.
    The chip says **"My Quest: Quest-A1B2"** (the last Quest used, or the first online one); header **"Mode: Player"**.
19. Tap **Change**: pick **Quest-C3D4** (offline Quests cannot be picked): back on the Play page with "My Quest: Quest-C3D4".
20. Tap **Audio** > **Briefing_Audio.mp3** > **Send & Play**: the audio controls + progress for that one Quest.
21. Back > Session > Home: Home now shows **"Mode: Player • Quest-C3D4"**.

## 4:45 Look and feel (15 s)
22. The **Devices**, **Send** and **Control** tabs use the same Quest list, preview card and buttons.
23. **Settings** > **App Customization**: switch the theme (Default, Dark, Light, Ocean, Purple); every screen follows.

## If something goes wrong
- Toast "Select at least one online Quest": tap **Select all online** on the Quest list.
- To restart the demo from zero: close the app from the recent-apps list and open it again.
- Demo mode OFF: Settings > **Demo mode** > confirm. The app restarts with the real network (no sample Quests).
  HOST / PLAYER and the Play page then use the real Quests; the live dashboard (voice, chat, timeline) needs the
  backend and says so. Turn Demo mode ON the same way to go back.
