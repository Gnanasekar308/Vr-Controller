using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Voice indicator: pulses in the "speaking" colour while someone talks, calm (theme SubText) otherwise.
    public class SpeakingPulse : MonoBehaviour
    {
        public Graphic target;
        public bool speaking;
        public float speed = 7f;

        private Color speakColor;

        public void Set(bool on)
        {
            speaking = on;
            if (!on) Calm();
        }

        private void OnEnable() { Calm(); }

        private void Update()
        {
            if (!speaking || target == null) return;
            speakColor = ThemeManager.Get(DemoConfig.SpeakingRole);   // follows the theme
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            target.rectTransform.localScale = Vector3.one * (1f + 0.22f * k);
            target.color = Color.Lerp(speakColor, Color.white, 0.25f * k);
        }

        private void Calm()
        {
            if (target == null) return;
            target.rectTransform.localScale = Vector3.one;
            target.color = ThemeManager.Get(ColorRole.SubText);
        }
    }
}
