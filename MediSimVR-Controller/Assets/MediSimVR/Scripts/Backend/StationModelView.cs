using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    // On the "Model" child of a Live Scene object that shows the station's real object (see StationModels).
    // Owns the meshes, materials and textures created for it and frees them with the object.
    public class StationModelView : MonoBehaviour
    {
        private readonly List<Object> owned = new List<Object>();

        public void Keep(Object asset)
        {
            if (asset != null) owned.Add(asset);
        }

        private void OnDestroy()
        {
            foreach (Object asset in owned) if (asset != null) Destroy(asset);
            owned.Clear();
        }
    }
}
