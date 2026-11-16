# Handover, 2026-11-16

Everything for the backend merge is in this repository (private, it contains the app keys).

| What | Where |
|---|---|
| Unity project (code) | `MediSimVR-Controller/` (open this folder in Unity) |
| Setup + backend guide | `MediSimVR-Controller/HANDOVER.md` (read this first) |
| What changed in this round, "do not change" list | `UI_CHANGES_2026-11-16.md` (repo root) |
| PC server backend (demo mode OFF) | `MediSimVR-Controller/Assets/MediSimVR/Scripts/Backend/` (`MedisimBackend.UseServer` switches it) |
| Ready-made Android build | `handover/MediSimVR-Controller_2026-11-16.apk` (built 2026-11-16 17:52, with the PC server backend + Live Scene Save) |

Install the APK: `adb install -r handover/MediSimVR-Controller_2026-11-16.apk` (package `com.medisim.vr`).

Tests with the backend files (2026-11-16): compile 0 errors, Demo Smoke Test PASSED (47 screens, 73 checks),
Layout Audit PASSED (0 problems), phone install + tour 47 screens / 0 failed / 0 exceptions.
Live Scene Save (17:52 build): smoke PASSED, layout 0 problems; on the phone (demo OFF) the app found the PC server
and listed the station. Save on the phone not tapped yet.
Not tested: real tablet, demo mode OFF with the real PC server on a phone, Quest view, voice.
