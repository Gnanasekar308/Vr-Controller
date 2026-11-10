using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MediSimVR
{
    // Long press on a button (e.g. the Snap magnet = pick the step). The Button's click that follows a long press
    // is skipped by the owner: if (longPress.ConsumeClick()) return;
    public class LongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float seconds = 0.55f;
        public event Action LongPressed;

        private bool down, fired;
        private float t0;

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            fired = false;
            t0 = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData e) { down = false; }
        public void OnPointerExit(PointerEventData e) { down = false; }

        private void OnDisable() { down = false; }

        private void Update()
        {
            if (!down || fired || Time.unscaledTime - t0 < seconds) return;
            Trigger();
        }

        // also used by automated tests
        public void Trigger()
        {
            fired = true;
            down = false;
            if (LongPressed != null) LongPressed();
        }

        // true once after a long press: the owner ignores that click
        public bool ConsumeClick()
        {
            if (!fired) return false;
            fired = false;
            return true;
        }
    }
}
