using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // One Inspector value (e.g. Position X): type a number, - / + buttons, drag the X/Y/Z label left / right to scrub,
    // long press the label (or the number) = reset this value only. Edited(field, value, final): final = false while scrubbing.
    public class InspectorField : MonoBehaviour
    {
        public TMP_Text axisLabel;
        public TMP_InputField input;
        public Button minusButton, plusButton;
        public ScrubArea scrub;
        public LongPress inputLongPress;
        public int group;    // 0 position, 1 rotation, 2 scale
        public int axis;     // 0 x, 1 y, 2 z
        public float step = 0.1f;
        public float scrubPerUnit = 0.004f;   // value change per canvas unit of drag
        public float resetValue;

        public event Action<InspectorField, float, bool> Edited;
        public event Action<InspectorField, string> BadInput;

        public float Value { get { return value; } }

        private float value, scrubStart;
        private bool interactable = true;

        private void Awake()
        {
            if (minusButton != null) minusButton.onClick.AddListener(() => { if (interactable) Emit(value - step, true); });
            if (plusButton != null) plusButton.onClick.AddListener(() => { if (interactable) Emit(value + step, true); });
            if (input != null) input.onEndEdit.AddListener(OnText);
            if (scrub != null)
            {
                scrub.ScrubBegin += () => scrubStart = value;
                scrub.Scrub += d => { if (interactable) Emit(scrubStart + d * scrubPerUnit, false); };
                scrub.ScrubEnd += () => { if (interactable) Emit(value, true); };
                scrub.LongPressed += ResetValue;
            }
            if (inputLongPress != null) inputLongPress.LongPressed += () =>
            {
                if (input != null) input.DeactivateInputField();
                ResetValue();
            };
        }

        // value from the model (does not fire Edited); the text is not touched while the user is typing
        public void Show(float v)
        {
            value = v;
            if (input != null && !input.isFocused) input.SetTextWithoutNotify(Format(v));
        }

        public void SetInteractable(bool on)
        {
            interactable = on;
            if (input != null) input.interactable = on;
            if (minusButton != null) minusButton.interactable = on;
            if (plusButton != null) plusButton.interactable = on;
        }

        public void ResetValue()
        {
            if (interactable) Emit(resetValue, true);
        }

        private void OnText(string text)
        {
            if (!interactable) { Show(value); return; }
            float v;
            if (!TryParse(text, out v))
            {
                Show(value);
                if (BadInput != null) BadInput(this, text);
                return;
            }
            if (Mathf.Approximately(v, value)) { Show(value); return; }
            Emit(v, true);
        }

        private void Emit(float v, bool final)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return;
            value = v;
            if (input != null) input.SetTextWithoutNotify(Format(v));
            if (Edited != null) Edited(this, v, final);
        }

        // "1.5", "-2", "1,5" (comma as decimal mark), "  3 " -> number. No NaN / infinity.
        public static bool TryParse(string text, out float v)
        {
            v = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            if (s.IndexOf('.') < 0 && s.IndexOf(',') == s.LastIndexOf(',')) s = s.Replace(',', '.');
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
            return !(float.IsNaN(v) || float.IsInfinity(v));
        }

        public static string Format(float v)
        {
            if (Mathf.Abs(v) < 0.0005f) return "0";
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // ---------- automated tests ----------
        public void SimType(string text)
        {
            if (input != null) input.SetTextWithoutNotify(text);
            OnText(text);
        }

        public void SimPlus() { if (plusButton != null) plusButton.onClick.Invoke(); }
        public void SimMinus() { if (minusButton != null) minusButton.onClick.Invoke(); }
    }
}
