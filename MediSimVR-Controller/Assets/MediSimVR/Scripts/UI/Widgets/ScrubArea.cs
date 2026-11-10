using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    // The X / Y / Z label of an Inspector field: drag left / right = scrub the value (like Unity), long press = reset.
    // A mostly vertical drag is handed to the parent ScrollRect, so the Inspector still scrolls.
    public class ScrubArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler,
                             IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public float longPressSeconds = 0.6f;

        public event Action ScrubBegin;
        public event Action<float> Scrub;       // total horizontal distance since ScrubBegin, in canvas units (1080 wide)
        public event Action ScrubEnd;
        public event Action LongPressed;

        private ScrollRect scroll;
        private Canvas canvas;
        private bool down, scrubbing, toParent;
        private float t0, startX;

        private void Awake()
        {
            scroll = GetComponentInParent<ScrollRect>();
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            t0 = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData e) { down = false; }

        public void OnInitializePotentialDrag(PointerEventData e)
        {
            if (scroll != null) scroll.OnInitializePotentialDrag(e);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            down = false; // a drag is never a long press
            if (Mathf.Abs(e.delta.x) >= Mathf.Abs(e.delta.y))
            {
                scrubbing = true;
                startX = e.position.x;
                if (ScrubBegin != null) ScrubBegin();
            }
            else if (scroll != null)
            {
                toParent = true;
                scroll.OnBeginDrag(e);
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (scrubbing)
            {
                float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
                if (Scrub != null) Scrub((e.position.x - startX) / Mathf.Max(0.01f, scale));
            }
            else if (toParent) scroll.OnDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (scrubbing)
            {
                scrubbing = false;
                if (ScrubEnd != null) ScrubEnd();
            }
            else if (toParent)
            {
                toParent = false;
                scroll.OnEndDrag(e);
            }
        }

        private void OnDisable()
        {
            down = false;
            if (scrubbing)
            {
                scrubbing = false;
                if (ScrubEnd != null) ScrubEnd();
            }
        }

        private void Update()
        {
            if (!down || Time.unscaledTime - t0 < longPressSeconds) return;
            down = false;
            if (LongPressed != null) LongPressed();
        }

        // ---------- automated tests ----------
        public void SimScrub(float distance)
        {
            if (ScrubBegin != null) ScrubBegin();
            if (Scrub != null) Scrub(distance * 0.5f);
            if (Scrub != null) Scrub(distance);
            if (ScrubEnd != null) ScrubEnd();
        }

        public void SimLongPress() { if (LongPressed != null) LongPressed(); }
    }
}
