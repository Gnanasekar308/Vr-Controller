using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Settings > Edit App: every change is applied live and saved.
    public class EditAppScreen : AppScreen
    {
        public Button backButton, resetButton;
        public Segmented categories;
        public List<GameObject> panels = new List<GameObject>();
        public List<PresetTileView> presetTiles = new List<PresetTileView>();
        public List<ColorSwatchView> colorSwatches = new List<ColorSwatchView>();
        public Segmented textSize, buttonStyle, buttonSize, density;
        public ToggleSwitch boldHeadings;
        public Slider cornerRadius, listGap, screenPadding;
        public TMP_Text cornerLabel, gapLabel, paddingLabel;

        static readonly string[] ColorNames = { "Primary", "Secondary", "Background", "Card", "Text", "Accent" };
        bool syncing;

        ThemeData T { get { return ThemeManager.Current.Clone(); } }

        public override void Init(Navigator n)
        {
            base.Init(n);
            backButton.onClick.AddListener(nav.Back);
            resetButton.onClick.AddListener(() => nav.popup.Show("Reset", "Go back to the Default theme?", "Reset",
                () => { ThemeManager.ResetToDefault(); nav.Say("Theme reset"); }, "Cancel"));
            categories.Changed += ShowPanel;

            for (int i = 0; i < presetTiles.Count; i++)
            {
                string name = ThemeData.PresetNames[i];
                presetTiles[i].button.onClick.AddListener(() => PickPreset(name));
            }
            for (int i = 0; i < colorSwatches.Count; i++)
            {
                int k = i;
                colorSwatches[i].button.onClick.AddListener(() =>
                    nav.colorPicker.Open(ColorNames[k], GetColor(ThemeManager.Current, k), c =>
                    {
                        var t = T;
                        SetColor(t, k, c);
                        t.Preset = "Custom";
                        ThemeManager.Set(t);
                    }));
            }
            textSize.Changed += v => Edit(t => t.TextSize = (SizeStep)v);
            boldHeadings.Changed += v => Edit(t => t.BoldHeadings = v);
            buttonStyle.Changed += v => Edit(t => t.ButtonStyle = (ButtonStyle)v);
            buttonSize.Changed += v => Edit(t => t.ButtonSize = (SizeStep)v);
            density.Changed += v => Edit(t => t.Density = (Density)v);
            cornerRadius.onValueChanged.AddListener(v => Edit(t => t.CornerRadius = Mathf.RoundToInt(v)));
            listGap.onValueChanged.AddListener(v => Edit(t => t.ListGap = Mathf.RoundToInt(v)));
            screenPadding.onValueChanged.AddListener(v => Edit(t => t.ScreenPadding = Mathf.RoundToInt(v)));
            ShowPanel(0);
        }

        void ShowPanel(int i)
        {
            categories.Select(i, false);
            for (int p = 0; p < panels.Count; p++) panels[p].SetActive(p == i);
        }

        void Edit(System.Action<ThemeData> change)
        {
            if (syncing) return;
            var t = T;
            change(t);
            ThemeManager.Set(t);
        }

        void PickPreset(string name)
        {
            var cur = ThemeManager.Current;
            ThemeData t;
            if (name == "Custom") { t = cur.Clone(); t.Preset = "Custom"; }
            else
            {
                t = ThemeData.FromPreset(name);
                // keep the user's text / button / layout choices, change only the colours
                t.ButtonStyle = cur.ButtonStyle; t.ButtonSize = cur.ButtonSize; t.CornerRadius = cur.CornerRadius;
                t.TextSize = cur.TextSize; t.BoldHeadings = cur.BoldHeadings; t.Density = cur.Density;
                t.ListGap = cur.ListGap; t.ScreenPadding = cur.ScreenPadding;
            }
            ThemeManager.Set(t);
            if (name == "Custom") ShowPanel(1);
        }

        static Color GetColor(ThemeData t, int k)
        {
            switch (k)
            {
                case 0: return t.Primary;
                case 1: return t.Secondary;
                case 2: return t.Background;
                case 3: return t.Card;
                case 4: return t.Text;
                default: return t.Accent;
            }
        }

        static void SetColor(ThemeData t, int k, Color c)
        {
            switch (k)
            {
                case 0: t.Primary = c; break;
                case 1: t.Secondary = c; break;
                case 2: t.Background = c; break;
                case 3: t.Card = c; break;
                case 4: t.Text = c; break;
                default: t.Accent = c; break;
            }
        }

        public override void Refresh()
        {
            var t = ThemeManager.Current;
            syncing = true;
            for (int i = 0; i < presetTiles.Count; i++)
            {
                string name = ThemeData.PresetNames[i];
                presetTiles[i].check.SetActive(t.Preset == name);
                if (name == "Custom") presetTiles[i].swatch.SetColors(t.Secondary, t.Primary);
                else { var p = ThemeData.FromPreset(name); presetTiles[i].swatch.SetColors(p.Secondary, p.Primary); }
            }
            for (int i = 0; i < colorSwatches.Count; i++) colorSwatches[i].swatch.color = GetColor(t, i);
            textSize.Select((int)t.TextSize, false);
            boldHeadings.Set(t.BoldHeadings, false);
            buttonStyle.Select((int)t.ButtonStyle, false);
            buttonSize.Select((int)t.ButtonSize, false);
            density.Select((int)t.Density, false);
            cornerRadius.SetValueWithoutNotify(t.CornerRadius);
            listGap.SetValueWithoutNotify(t.ListGap);
            screenPadding.SetValueWithoutNotify(t.ScreenPadding);
            cornerLabel.text = t.CornerRadius + " px";
            gapLabel.text = t.ListGap + " px";
            paddingLabel.text = t.ScreenPadding + " px";
            syncing = false;
        }
    }
}
