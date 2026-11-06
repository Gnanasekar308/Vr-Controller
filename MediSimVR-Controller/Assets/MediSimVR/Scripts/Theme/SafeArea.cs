using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Keeps content inside the phone safe area (notch / gesture bar).
    [ExecuteAlways]
    public class SafeArea : MonoBehaviour
    {
        Rect last;

        void Update()
        {
            if (!Application.isPlaying) return;
            Rect sa = Screen.safeArea;
            if (sa == last || Screen.width <= 0 || Screen.height <= 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        }
    }
}
