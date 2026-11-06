using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // Two-colour gradient on any UI graphic (theme roles or fixed colours).
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        public bool useTheme = true;
        public ColorRole from = ColorRole.Secondary;
        public ColorRole to = ColorRole.Primary;
        public Color colorA = Color.white, colorB = Color.black;
        public bool vertical;

        protected override void OnEnable()
        {
            base.OnEnable();
            ThemeManager.Changed += Refresh;
        }

        protected override void OnDisable()
        {
            ThemeManager.Changed -= Refresh;
            base.OnDisable();
        }

        public void SetColors(Color a, Color b)
        {
            useTheme = false;
            colorA = a;
            colorB = b;
            Refresh();
        }

        public void Refresh() { if (graphic != null) graphic.SetVerticesDirty(); }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Color a = useTheme ? ThemeManager.Get(from) : colorA;
            Color b = useTheme ? ThemeManager.Get(to) : colorB;
            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts)
            {
                float p = vertical ? v.position.y : v.position.x;
                if (p < min) min = p;
                if (p > max) max = p;
            }
            float span = Mathf.Max(0.001f, max - min);
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float p = ((vertical ? v.position.y : v.position.x) - min) / span;
                Color c = vertical ? Color.Lerp(b, a, p) : Color.Lerp(a, b, p);
                c.a *= v.color.a / 255f;
                v.color = c;
                verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
