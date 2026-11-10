using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // One row of the Scene tab's Hierarchy sheet: object name + shape (+ "Locked"). Tap = select.
    public class HierarchyRowView : MonoBehaviour
    {
        public Button button;
        public TMP_Text nameText, subText;
        public Image highlight, colorDot;
        [HideInInspector] public string id;

        public void Bind(SceneObjectState o, bool selected)
        {
            id = o.id;
            nameText.text = o.name;
            subText.text = Shape(o.shape) + (o.locked ? "  •  Locked" : "");
            if (colorDot != null) colorDot.color = new Color(o.color.r, o.color.g, o.color.b, 1f);
            SetSelected(selected);
        }

        public void SetSelected(bool on)
        {
            if (highlight != null) highlight.gameObject.SetActive(on);
        }

        private static string Shape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Box";
            return s == "box" ? "Box (bounds)" : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
