using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Base: re-applies itself whenever the theme changes (also in the Editor scene view).
    [ExecuteAlways]
    public abstract class ThemedBase : MonoBehaviour
    {
        protected virtual void OnEnable()
        {
            ThemeManager.Changed += Apply;
            Apply();
        }

        protected virtual void OnDisable() { ThemeManager.Changed -= Apply; }

        public abstract void Apply();
    }
}
