using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    // On/off switch (track + knob). Colours follow the theme.
    public class ToggleSwitch : ThemedBase, IPointerClickHandler
    {
        public Image track;
        public RectTransform knob;
        public bool isOn;
        public event Action<bool> Changed;

        public void Set(bool on, bool notify)
        {
            isOn = on;
            Apply();
            if (notify && Changed != null) Changed(on);
        }

        public void OnPointerClick(PointerEventData e) { Set(!isOn, true); }

        public override void Apply()
        {
            if (track != null) track.color = isOn ? ThemeManager.Get(ColorRole.Primary) : ThemeManager.Get(ColorRole.Divider);
            if (knob != null)
            {
                float x = isOn ? 1f : 0f;
                knob.anchorMin = new Vector2(x, 0.5f);
                knob.anchorMax = new Vector2(x, 0.5f);
                knob.pivot = new Vector2(x, 0.5f);
                knob.anchoredPosition = new Vector2(isOn ? -6f : 6f, 0f);
            }
        }
    }
}
