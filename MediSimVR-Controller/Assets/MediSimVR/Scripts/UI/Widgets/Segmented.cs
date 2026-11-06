using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MediSimVR
{
    // Segmented buttons (filters, S/M/L, Rounded/Pill/Square, Edit App categories).
    public class Segmented : ThemedBase
    {
        public List<Button> buttons = new List<Button>();
        public List<Image> backgrounds = new List<Image>();
        public List<TMP_Text> labels = new List<TMP_Text>();
        public List<Image> icons = new List<Image>();
        public int selected;
        public event Action<int> Changed;

        bool wired;

        void Awake() { Wire(); }

        void Wire()
        {
            if (wired || !Application.isPlaying) return;
            wired = true;
            for (int i = 0; i < buttons.Count; i++)
            {
                int k = i;
                buttons[i].onClick.AddListener(() => Select(k, true));
            }
        }

        public void Select(int i, bool notify)
        {
            selected = i;
            Apply();
            if (notify && Changed != null) Changed(i);
        }

        public void SetLabel(int i, string text) { if (i < labels.Count && labels[i] != null) labels[i].text = text; }

        public override void Apply()
        {
            for (int i = 0; i < backgrounds.Count; i++)
            {
                bool on = i == selected;
                if (backgrounds[i] != null) backgrounds[i].color = on ? ThemeManager.Get(ColorRole.Primary) : ThemeManager.Get(ColorRole.Field);
                if (i < labels.Count && labels[i] != null) labels[i].color = on ? ThemeManager.Get(ColorRole.OnPrimary) : ThemeManager.Get(ColorRole.Text);
                if (i < icons.Count && icons[i] != null) icons[i].color = on ? ThemeManager.Get(ColorRole.OnPrimary) : ThemeManager.Get(ColorRole.SubText);
            }
        }
    }
}
