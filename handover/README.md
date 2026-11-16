# Handover, 2026-11-16

Everything for the backend merge is in this repository (private, it contains the app keys).

| What | Where |
|---|---|
| Unity project (code) | `MediSimVR-Controller/` (open this folder in Unity) |
| Setup + backend guide | `MediSimVR-Controller/HANDOVER.md` (read this first) |
| What changed in this round, "do not change" list | `UI_CHANGES_2026-11-16.md` (repo root) |
| Ready-made Android build | `handover/MediSimVR-Controller_2026-11-16.apk` (built 2026-11-16 12:51 from commit `4007ff0`) |

Install the APK: `adb install -r handover/MediSimVR-Controller_2026-11-16.apk` (package `com.medisim.vr`).

Tests at this commit: Demo Smoke Test PASSED (47 screens, 73 checks), Layout Audit PASSED (0 problems),
phone tour 47 screens / 0 failed. Not tested: real tablet, demo mode OFF on a device, real backend.
