using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // The playback controls (Control tab and Play page) for the selected Quests:
    //   video / audio: Play, Pause, Resume, Stop, Mute (+ progress bar where the layout has one)
    //   image / text:  Show on Quest, Hide
    // Acted reports each action (kind + verb) so a screen can log it to the session timeline.
    public class PlaybackControlsView : MonoBehaviour
    {
        public Button playButton, pauseButton, resumeButton, stopButton, showButton, hideButton;
        public GameObject mediaControls, textControls;
        public ToggleSwitch muteSwitch;
        public TMP_Text stateText;
        // optional progress (Play page)
        public GameObject progressRow;
        public RectTransform progressFill;
        public TMP_Text elapsedText, durationText;

        public event Action<SessionEventKind, string> Acted;

        private IControllerService Svc { get { return AppRoot.Service; } }

        public void Init()
        {
            playButton.onClick.AddListener(() => Do(SessionEventKind.Play, "play", () => Svc.Play()));
            pauseButton.onClick.AddListener(() => Do(SessionEventKind.Pause, "pause", () => Svc.Pause()));
            resumeButton.onClick.AddListener(() => Do(SessionEventKind.Play, "resume", () => Svc.Resume()));
            stopButton.onClick.AddListener(() => Do(SessionEventKind.Stop, "stop", () => Svc.Stop()));
            showButton.onClick.AddListener(() => Do(SessionEventKind.Content, "show", () => Svc.ShowText()));
            hideButton.onClick.AddListener(() => Do(SessionEventKind.Content, "hide", () => Svc.HideText()));
            muteSwitch.Changed += m => Do(SessionEventKind.Mute, m ? "mute" : "unmute", () => Svc.SetMute(m));
        }

        private void Do(SessionEventKind kind, string verb, Action act)
        {
            act();
            if (Acted != null) Acted(kind, verb);
        }

        public static bool IsVisual(MediaFile f) { return f != null && (f.Kind == MediaKind.Text || f.Kind == MediaKind.Image); }

        // f = current content, targets = the Quests the buttons act on, note = state line to show instead (null = counts)
        public void Bind(MediaFile f, List<QuestDevice> targets, string emptyHint, string note)
        {
            bool visual = IsVisual(f);
            mediaControls.SetActive(!visual);
            textControls.SetActive(visual);

            int playing = 0, paused = 0, showing = 0, muted = 0;
            foreach (var d in targets)
            {
                if (d.State == DeviceState.Playing) playing++;
                if (d.State == DeviceState.Paused) paused++;
                if (d.State == DeviceState.ShowingText) showing++;
                if (d.Muted) muted++;
            }
            if (stateText != null)
                stateText.text = note ?? (targets.Count == 0 ? emptyHint
                    : visual ? "Showing " + showing + " of " + targets.Count
                    : "Playing " + playing + "  •  Paused " + paused + "  •  " + targets.Count + " selected");
            muteSwitch.Set(targets.Count > 0 && muted == targets.Count, false);
            UpdateProgress(f, targets);
        }

        // position of the first target that plays / paused this file (mock: moves while playing; real Quests: 0 until
        // the receiver reports it)
        public void UpdateProgress(MediaFile f, List<QuestDevice> targets)
        {
            if (progressRow == null) return;
            bool timed = f != null && !IsVisual(f) && f.DurationSec > 0;
            progressRow.SetActive(timed);
            if (!timed) return;
            float pos = 0f;
            foreach (var d in targets)
                if (d.FileId == f.Id && (d.State == DeviceState.Playing || d.State == DeviceState.Paused)) { pos = d.PositionSec; break; }
            float k = Mathf.Clamp01(pos / f.DurationSec);
            progressFill.anchorMax = new Vector2(k, 1f);
            elapsedText.text = Fmt.Dur(Mathf.FloorToInt(pos));
            durationText.text = Fmt.Dur(f.DurationSec);
        }
    }
}
