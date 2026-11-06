using System;
using UnityEngine;

namespace MediSimVR
{
    public enum ColorRole { Background, Card, Primary, Secondary, Accent, Text, SubText, Divider, PrimarySoft, OnPrimary, Danger, Warning, Field }
    public enum ButtonStyle { Rounded, Pill, Square }
    public enum SizeStep { S, M, L }
    public enum Density { Compact, Normal, Comfortable }

    // Everything the "Edit App" screen can change. Saved as JSON in PlayerPrefs.
    [Serializable]
    public class ThemeData
    {
        public string Preset = "Default";
        public Color Primary, Secondary, Background, Card, Text, Accent;
        public ButtonStyle ButtonStyle = ButtonStyle.Rounded;
        public SizeStep ButtonSize = SizeStep.M;
        public int CornerRadius = 16;
        public SizeStep TextSize = SizeStep.M;
        public bool BoldHeadings = true;
        public Density Density = Density.Normal;
        public int ListGap = 20;
        public int ScreenPadding = 40;

        public ThemeData Clone() { return (ThemeData)MemberwiseClone(); }

        static Color H(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        static ThemeData Make(string name, string p, string s, string bg, string card, string text, string acc)
        {
            return new ThemeData { Preset = name, Primary = H(p), Secondary = H(s), Background = H(bg), Card = H(card), Text = H(text), Accent = H(acc) };
        }

        public static readonly string[] PresetNames = { "Default", "Dark", "Light", "Ocean", "Purple", "Custom" };

        public static ThemeData FromPreset(string name)
        {
            switch (name)
            {
                case "Dark": return Make(name, "#3B9CFF", "#22C3B6", "#0F172A", "#1E293B", "#F1F5F9", "#34D399");
                case "Light": return Make(name, "#2563EB", "#0EA5E9", "#FFFFFF", "#F4F7FB", "#0F172A", "#16A34A");
                case "Ocean": return Make(name, "#168AF2", "#12B5A6", "#062C43", "#0B4059", "#F4FAFD", "#00C987");
                case "Purple": return Make(name, "#7C3AED", "#A855F7", "#F5F3FF", "#FFFFFF", "#1E1B4B", "#10B981");
                default: return Make("Default", "#1A7FE8", "#14B8A6", "#F2F6FA", "#FFFFFF", "#1E293B", "#22C55E");
            }
        }
    }
}
