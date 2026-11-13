# MediSimVR phone app: demo script (click by click)

Demo mode is ON (Settings > Demo mode). Everything runs on sample data: no Quest, PC, backend or Wi-Fi needed.
Sample data (all in one file, `Assets/MediSimVR/Scripts/Demo/DemoConfig.cs`):
- 1 admin: Dr. Anitha Rao. 8 students (Arjun Kumar, Divya Menon, Karthik Raj, Sneha Iyer, Rahul Verma, Fatima Khan,
  Vikram Singh, Meera Pillai).
- 6 Quests: Quest-A1B2 (In Use, plays a video), Quest-C3D4, Quest-E5F6 (Offline), Quest-G7H8, Quest-J9K1, Quest-M2N3.
- 3 meetings: Meeting 01 "CPR & Basic Life Support" (today 09:00), Meeting 02 "Heart Anatomy (360 view)" (today
  11:30), Meeting 03 "Airway Management" (tomorrow 14:00).
- Login: any email + password works; the Demo admin / Demo student buttons log in at once.

Before you start: close the app from recent apps and open it again (fresh start). Phone volume up.
Android back works on every screen (one step back; on the first screen it closes the app).

## 0:00 Launch (15 s)
1. Open the app: **splash** (logo + MediSimVR, 1.5 s), then **"Who is using this phone?"** with two cards **ADMIN**
   and **STUDENT** (no bottom bar before login), DEMO chip.

## 0:15 Admin login + dashboard (30 s)
2. Tap **ADMIN**: Admin login. (Tap **Log in** with empty fields: "Enter your email and password".)
3. Tap **Demo admin**: the **Dashboard**. Greeting + name, bell (notifications), **Meeting Room** card, stats
   **8 Students / 5/6 Quests online / 0 Sessions today**, today's meetings.
4. Bottom bar (admin): **Dashboard, Meetings, Live, Devices, Settings**.

## 0:45 Meeting Room: create a meeting (1 min)
5. Tap **Meeting Room**: Meeting 01, 02, 03 (status chip Today / Upcoming).
6. Tap **Create Meeting**. Fill: Meeting name "Meeting 04", Today's topic "Sepsis Recognition", Date (arrows),
   Class timing (Start / End, 15 min steps).
7. **Select students**: tap 3-4 students (or **Select all**).
8. **Assign Quest devices**: tap Quest-C3D4 and Quest-M2N3. Tap **Quest-E5F6** (offline): "cannot be assigned".
9. **Save Meeting**: back to the list, "Meeting 04 saved". **View All Meetings (4)** appears; tap it to see all.

## 1:45 Meeting details > Start Session (30 s)
10. Tap **Meeting 01**: Today's topic, Students, Date, Class timing, **Assigned Quest devices** (status, battery, how
    many students share it), **Attendance** (5 present, 1 absent; tap a student to switch present / absent).
11. Tap **Start Session**: the **Live session** opens in **Ready** state.

## 2:15 Live session (2 min)
12. Top: the **Quest screen** (live preview of the selected student's Quest, always visible) with the expand button.
    Below: tabs **Metrics | Quest | Students**. Bottom: **START / PAUSE / END**.
13. Tap **START**: the chip shows **LIVE 00:00:05**, the metrics start moving.
14. **Metrics**: class average Emotional / Engagement / Attention (+ class mood), **Sound control** (master volume,
    **Mute all students**), per student: attention, engagement, emotion bars + mute button.
15. **Quest** tab: device status (In Use, connected, battery, what plays), **Play / Pause**, **Play content**:
    tap **Video** > CPR_Training.mp4 (Replace) - plays on the meeting's Quests. **All controls** = the full Control
    screen (notes, instructions, audio). **Live Scene** = the 3D scene (the video pauses; back = the live session).
16. **Students** tab: count, attendance, devices online, student list. Tap **Divya Menon**: details (Quest, status,
    attention, engagement, emotion) > **Show Quest screen**: the top panel now shows Divya's Quest.
17. Tap the **expand** button on the Quest screen: full screen (back = normal).
18. Tap the **rotate** button (top right, arrows icon): **landscape** = Metrics | Quest | Students side by side, no
    bottom bar. Tap it again for portrait (leaving the Live session also returns to portrait).
19. **PAUSE** (session + Quests pause), **START** (resume).

## 4:15 End > Session report (45 s)
20. **END** > **End session**: the **Session Report**: meeting, topic, date, timing, duration; attendance
    (present / absent + names); class engagement / attention / emotion; per student; content played; timeline of key
    events; where the JSON record is saved ("Sent to PC-LAB1" after a moment).
21. **Share** (demo: "Report shared"), **Back to Dashboard**: "Sessions today 1".

## 5:00 Devices, Settings, Logout (30 s)
22. **Devices** tab: the 6 Quests (filters All / Online / Offline / In Use).
23. **Settings** tab: Account (Dr. Anitha Rao, Admin), **Log out**, Demo mode, App Customization (themes), Network,
    Connection, Notification, About.
24. **Log out** > Log out: back to the role selection.

## 5:30 Student (1 min)
25. Tap **STUDENT** > **Demo student** (Arjun Kumar): student dashboard (My meetings, Today, My Quest).
    Bottom bar (student): **Dashboard, Meetings, Settings**.
26. **Meetings** tab: only Arjun's meetings (no Create Meeting).
27. Tap **Meeting 01** > **Join Session**: the student view: the same Quest screen (own Quest), own status, my focus
    (attention, engagement), sound (volume, mute my Quest), **Leave session**. No admin controls.
28. **Leave session** > Leave: back to the dashboard.

## If something goes wrong
- Restart from zero: close the app from recent apps and open it again (meetings created in the demo are not kept).
- Demo mode OFF: Settings > Demo mode > confirm. The app restarts with the real network; meetings, students and live
  metrics stay sample data (the classroom backend is not built yet), Quests are the real ones.
- Automatic demo with screenshots: `Tools/capture-demo-screens.sh` (phone on USB): every screen goes into
  `demo-screens/`.
