using System;
using TMPro;
using UnityEngine;

namespace MediSimVR
{
    // Current theme for the whole app. Works in Play mode and in the Editor (scene preview).
    public static class ThemeManager
    {
        const string Key = "msvr_theme";
        static ThemeData current;
        static TMP_FontAsset regular, semibold;

        public static event Action Changed;

        public static ThemeData Current
        {
            get { if (current == null) current = ThemeData.FromPreset("Default"); return current; }
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            ThemeData t = null;
            if (json.Length > 0)
            {
                try { t = JsonUtility.FromJson<ThemeData>(json); } catch (Exception) { t = null; }
            }
            current = t ?? ThemeData.FromPreset("Default");
            Notify();
        }

        // apply + save (Edit App calls this on every change)
        public static void Set(ThemeData t)
        {
            current = t;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(t));
            PlayerPrefs.Save();
            Notify();
        }

        public static void ResetToDefault() { Set(ThemeData.FromPreset("Default")); }

        // one broken listener must not stop the others (or the app start)
        public static void Notify()
        {
            if (Changed == null) return;
            foreach (Action h in Changed.GetInvocationList())
            {
                try { h(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static Color Get(ColorRole r)
        {
            var t = Current;
            switch (r)
            {
                case ColorRole.Background: return t.Background;
                case ColorRole.Card: return t.Card;
                case ColorRole.Primary: return t.Primary;
                case ColorRole.Secondary: return t.Secondary;
                case ColorRole.Accent: return t.Accent;
                case ColorRole.Text: return t.Text;
                case ColorRole.SubText: return Color.Lerp(t.Text, t.Card, 0.45f);
                case ColorRole.Divider: return Color.Lerp(t.Card, t.Text, 0.12f);
                case ColorRole.Field: return Color.Lerp(t.Card, t.Text, 0.06f);
                case ColorRole.PrimarySoft: { var c = t.Primary; c.a = 0.14f; return c; }
                case ColorRole.OnPrimary: return Color.white;
                case ColorRole.Danger: return new Color32(239, 68, 68, 255);
                case ColorRole.Warning: return new Color32(245, 158, 11, 255);
            }
            return Color.magenta;
        }

        public static float TextScale
        {
            get { return Current.TextSize == SizeStep.S ? 0.9f : Current.TextSize == SizeStep.L ? 1.12f : 1f; }
        }

        public static float ButtonHeight
        {
            get { return Current.ButtonSize == SizeStep.S ? 100f : Current.ButtonSize == SizeStep.L ? 144f : 120f; }
        }

        public static float DensityScale
        {
            get { return Current.Density == Density.Compact ? 0.86f : Current.Density == Density.Comfortable ? 1.16f : 1f; }
        }

        public static TMP_FontAsset Font(bool bold)
        {
            if (regular == null) regular = Resources.Load<TMP_FontAsset>("MediSimFonts/Inter-Regular SDF");
            if (semibold == null) semibold = Resources.Load<TMP_FontAsset>("MediSimFonts/Inter-SemiBold SDF");
            var f = bold ? semibold : regular;
            return f != null ? f : TMP_Settings.defaultFontAsset;
        }
    }
}
