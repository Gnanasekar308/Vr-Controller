using System.Collections.Generic;
using MediSimVR.SceneSync;
using UnityEngine;

namespace MediSimVR
{
    // On the phone's "SyncWorld" root (far away from the UI). Draws every object of the SceneDocument as a matching
    // primitive (or a box for other shapes), so the phone view looks like the Unity scene. Picking = physics ray.
    public class SceneWorldView : MonoBehaviour
    {
        public Transform objectsRoot;
        public Material baseMaterial;     // Standard; copied per object with its colour
        public Material outlineMaterial;  // MediSimVR/Outline
        public Mesh cubeMesh, sphereMesh, cylinderMesh, capsuleMesh, planeMesh;

        private readonly Dictionary<string, SceneObjectView> views = new Dictionary<string, SceneObjectView>();
        private string selectedId;

        public int Count { get { return views.Count; } }

        public void Build(SceneDocument doc)
        {
            Clear();
            foreach (var o in doc.Objects) Create(o);
            SetSelected(doc.Selected != null ? doc.Selected.id : null);
        }

        public void Clear()
        {
            foreach (var v in views.Values) if (v != null) Destroy(v.gameObject);
            views.Clear();
            selectedId = null;
        }

        public void Sync(SceneObjectState o)
        {
            SceneObjectView v;
            if (o != null && views.TryGetValue(o.id, out v) && v != null) v.Apply(o.pose);
        }

        public void SetSelected(string id)
        {
            SceneObjectView v;
            if (selectedId != null && views.TryGetValue(selectedId, out v) && v != null) v.SetOutline(false);
            selectedId = id;
            if (id != null && views.TryGetValue(id, out v) && v != null) v.SetOutline(true);
        }

        public bool TryGetView(string id, out SceneObjectView v) { return views.TryGetValue(id ?? "", out v) && v != null; }

        // nearest object hit by the ray (object colliders only)
        public bool Pick(Ray ray, out string id)
        {
            id = null;
            Physics.SyncTransforms(); // objects may have moved this frame
            var hits = Physics.RaycastAll(ray, 100000f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                var v = h.collider != null ? h.collider.GetComponentInParent<SceneObjectView>() : null;
                if (v == null || !views.ContainsKey(v.id) || h.distance >= best) continue;
                best = h.distance;
                id = v.id;
            }
            return id != null;
        }

        public Vector3 ToWorld(Vector3 local) { return transform.TransformPoint(local); }
        public Vector3 ToLocal(Vector3 world) { return transform.InverseTransformPoint(world); }

        private void Create(SceneObjectState o)
        {
            var root = new GameObject(o.name);
            root.transform.SetParent(objectsRoot != null ? objectsRoot : transform, false);
            var view = root.AddComponent<SceneObjectView>();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var mf = body.AddComponent<MeshFilter>();
            var mr = body.AddComponent<MeshRenderer>();
            switch (o.shape)
            {
                case SceneSyncProtocol.ShapeCube:
                    mf.sharedMesh = cubeMesh;
                    body.AddComponent<BoxCollider>();
                    break;
                case SceneSyncProtocol.ShapeSphere:
                    mf.sharedMesh = sphereMesh;
                    body.AddComponent<SphereCollider>();
                    break;
                case SceneSyncProtocol.ShapeCylinder:
                {
                    mf.sharedMesh = cylinderMesh;
                    var c = body.AddComponent<CapsuleCollider>();
                    c.height = 2f;
                    c.radius = 0.5f;
                    break;
                }
                case SceneSyncProtocol.ShapeCapsule:
                {
                    mf.sharedMesh = capsuleMesh;
                    var c = body.AddComponent<CapsuleCollider>();
                    c.height = 2f;
                    c.radius = 0.5f;
                    break;
                }
                case SceneSyncProtocol.ShapePlane:
                {
                    mf.sharedMesh = planeMesh;
                    var c = body.AddComponent<BoxCollider>();
                    c.size = new Vector3(10f, 0.02f, 10f);
                    break;
                }
                default: // "box": a cube of the reported bounds
                    mf.sharedMesh = cubeMesh;
                    body.transform.localPosition = o.center;
                    body.transform.localScale = new Vector3(Mathf.Max(o.size.x, 0.001f), Mathf.Max(o.size.y, 0.001f), Mathf.Max(o.size.z, 0.001f));
                    body.AddComponent<BoxCollider>();
                    break;
            }

            var mat = new Material(baseMaterial) { name = "Obj_" + o.id, color = o.color };
            mr.sharedMaterial = mat;
            view.Init(o.id, new Renderer[] { mr }, mat, outlineMaterial);
            view.Apply(o.pose);
            views[o.id] = view;
            StationModels.Attach(view, body, baseMaterial);   // PC server backend: the station's real object replaces the shape
        }
    }
}
