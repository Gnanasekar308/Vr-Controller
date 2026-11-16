using UnityEngine;

namespace MediSimVR
{
    // The phone's 3D copy of one receiver object (a primitive, or a box for other shapes). Selected = outline.
    public class SceneObjectView : MonoBehaviour
    {
        public string id;

        private Renderer[] renderers;
        private Material baseMaterial;   // own instance (colour of this object)
        private Material outlineMaterial;
        private Material[] plain, outlined;
        private Material[][] modelPlain;  // UseModel: the own materials of every model renderer
        private bool outlineOn;

        public void Init(string objectId, Renderer[] rs, Material baseMat, Material outlineMat)
        {
            id = objectId;
            renderers = rs;
            baseMaterial = baseMat;
            outlineMaterial = outlineMat;
            plain = new[] { baseMat };
            outlined = outlineMat != null ? new[] { baseMat, outlineMat } : plain;
            SetOutline(false);
        }

        // second material on the same mesh = inverted-hull outline (drawn around the object)
        public void SetOutline(bool on)
        {
            outlineOn = on;
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (modelPlain == null) { r.sharedMaterials = on ? outlined : plain; continue; }
                var own = modelPlain[i];
                if (!on || outlineMaterial == null) { r.sharedMaterials = own; continue; }
                var with = new Material[own.Length + 1];
                own.CopyTo(with, 0);
                with[own.Length] = outlineMaterial;
                r.sharedMaterials = with;
            }
        }

        // PC server backend (StationModels): the station's real object is drawn instead of the primitive.
        // modelRenderers = its solid meshes (one material each); the selection outline goes around them.
        public void UseModel(Renderer[] modelRenderers)
        {
            if (renderers != null)
                foreach (var r in renderers) if (r != null) { r.sharedMaterials = plain; r.enabled = false; }
            renderers = modelRenderers;
            modelPlain = new Material[modelRenderers.Length][];
            for (int i = 0; i < modelRenderers.Length; i++) modelPlain[i] = modelRenderers[i].sharedMaterials;
            SetOutline(outlineOn);
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
