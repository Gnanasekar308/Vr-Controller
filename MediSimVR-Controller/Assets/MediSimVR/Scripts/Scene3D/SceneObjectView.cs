using UnityEngine;

namespace MediSimVR
{
    // The phone's 3D copy of one receiver object (a primitive, or a box for other shapes). Selected = outline.
    public class SceneObjectView : MonoBehaviour
    {
        public string id;

        private Renderer[] renderers;
        private Material baseMaterial;   // own instance (colour of this object)
        private Material[] plain, outlined;

        public void Init(string objectId, Renderer[] rs, Material baseMat, Material outlineMat)
        {
            id = objectId;
            renderers = rs;
            baseMaterial = baseMat;
            plain = new[] { baseMat };
            outlined = outlineMat != null ? new[] { baseMat, outlineMat } : plain;
            SetOutline(false);
        }

        // second material on the same mesh = inverted-hull outline (drawn around the object)
        public void SetOutline(bool on)
        {
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null) r.sharedMaterials = on ? outlined : plain;
        }

        public void Apply(ScenePose p)
        {
            transform.localPosition = p.pos;
            transform.localRotation = p.rot;
            transform.localScale = p.scale;
        }

        private void OnDestroy()
        {
            if (baseMaterial != null) Destroy(baseMaterial);
        }
    }
}
