using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    [RequireComponent(typeof(Graphic))]
    public class ThemedColor : ThemedBase
    {
        public ColorRole role = ColorRole.Card;
        [Range(0f, 1f)] public float alpha = 1f;

        public override void Apply()
        {
            var g = GetComponent<Graphic>();
            if (g == null) return;
            var c = ThemeManager.Get(role);
            c.a *= alpha;
            g.color = c;
        }
    }
}
