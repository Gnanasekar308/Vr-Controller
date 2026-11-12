#!/usr/bin/env bash
# Takes the demo screenshots without touching the phone (Git Bash on the PC, phone on USB, demo mode ON).
# Starts the app with "-demotour": the app walks through every main screen and theme preset itself and logs
# "[MediSimVR] DEMO-TOUR <nn> <name>"; this script takes an adb screenshot for each line.
#   bash Tools/capture-demo-screens.sh [serial] [out-dir]
set -u
ADB="/c/Program Files/Unity/Hub/Editor/2021.3.45f2/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
SERIAL="${1:-ZA223HXPHV}"
OUT="${2:-demo-screens}"
export MSYS_NO_PATHCONV=1
mkdir -p "$OUT"

"$ADB" -s "$SERIAL" shell am force-stop com.medisim.vr
"$ADB" -s "$SERIAL" logcat -c
sleep 2
"$ADB" -s "$SERIAL" shell am start -S -W -n com.medisim.vr/com.unity3d.player.UnityPlayerActivity -e unity "-demotour"
echo "app started with -demotour, waiting for the tour..."

count=0
finished=0
while IFS= read -r line; do
    line="${line%$'\r'}"
    case "$line" in
        *"DEMO-TOUR done"*)
            finished=1
            echo "tour finished: $count screenshots in $OUT"
            break ;;
        *"[MediSimVR] DEMO-TOUR "[0-9][0-9]" "*)
            rest="${line#*DEMO-TOUR }"
            num="${rest%% *}"
            name="${rest#* }"
            "$ADB" -s "$SERIAL" exec-out screencap -p > "$OUT/${num}_${name}.png"
            count=$((count + 1))
            echo "$num $name" ;;
    esac
done < <(timeout 600 "$ADB" -s "$SERIAL" logcat -v time -s Unity:I | tee "$OUT/tour_unity_log.txt")
# tour_unity_log.txt = the whole Unity log of the tour (I / W / E), kept even when the phone's log buffer rotates

# no "DEMO-TOUR done" line (adb lost the phone, app crashed, timeout): the screenshots are incomplete
if [ "$finished" -ne 1 ]; then
    echo "TOUR NOT FINISHED: only $count screenshots (see $OUT/tour_unity_log.txt)" >&2
    exit 1
fi
