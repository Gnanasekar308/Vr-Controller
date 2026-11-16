using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MediSimVR
{
    // Demo mode OFF with the Medisim PC server: the Live Scene shows the station's real objects, not plain shapes.
    // The station sends how every object looks (SceneModelDto: meshes, colours, textures, text labels) to the server;
    // ServerSceneLink tells this class which model belongs to which object, SceneWorldView.Create calls Attach for
    // every new object view, and the model replaces the primitive as soon as it is downloaded.
    public static class StationModels
    {
        private class Waiting
        {
            public SceneObjectView view;
            public GameObject body;
            public Material baseMaterial;
        }

        private static readonly Dictionary<string, string> hashOfObject = new Dictionary<string, string>();          // object id -> model hash
        private static readonly Dictionary<string, SceneModelDto> loaded = new Dictionary<string, SceneModelDto>();  // model hash -> model
        private static readonly HashSet<string> loading = new HashSet<string>();
        private static readonly Dictionary<string, Waiting> waiting = new Dictionary<string, Waiting>();             // object id -> view without its model

        // The Scene tab closed (or the backend changed): the next scene starts clean. Downloaded models stay cached.
        public static void Clear()
        {
            hashOfObject.Clear();
            waiting.Clear();
        }

        // ServerSceneLink: the scene's objects with their model hashes (called before the views are created).
        public static void SetScene(ServerSceneLook look)
        {
            Clear();
            if (look == null || look.objects == null) return;

            foreach (ServerObjectLook o in look.objects)
            {
                if (o == null || string.IsNullOrEmpty(o.id) || string.IsNullOrEmpty(o.model)) continue;
                hashOfObject[o.id] = o.model;
                Load(o.model);
            }
        }

        // SceneWorldView.Create: a new object view, still the plain shape (body).
        public static void Attach(SceneObjectView view, GameObject body, Material baseMaterial)
        {
            if (AppRoot.Demo || view == null || body == null) return;

            string hash;
            if (!hashOfObject.TryGetValue(view.id, out hash)) return;

            SceneModelDto model;

            if (loaded.TryGetValue(hash, out model))
            {
                Apply(view, body, baseMaterial, model);
                return;
            }

            waiting[view.id] = new Waiting { view = view, body = body, baseMaterial = baseMaterial };
            Load(hash);
        }

        // ServerSceneLink, every few seconds: download again what failed (e.g. the station had not uploaded it yet).
        public static void Retry()
        {
            if (waiting.Count == 0) return;
            var ids = new List<string>(waiting.Keys);

            foreach (string id in ids)
            {
                string hash;
                if (hashOfObject.TryGetValue(id, out hash)) Load(hash);
            }
        }

        private static void Load(string hash)
        {
            if (loaded.ContainsKey(hash) || !loading.Add(hash)) return;

            MedisimServer.Instance.Request("GET", "/api/scene/models/" + hash, null, r =>
            {
                loading.Remove(hash);

                if (!r.Ok)
                {
                    if (r.Code != 404) Debug.LogWarning("[MediSimVR] Object model not loaded: " + r);
                    return;
                }

                SceneModelDto model = null;
                try { model = JsonUtility.FromJson<SceneModelDto>(r.Text); }
                catch (Exception e) { Debug.LogWarning("[MediSimVR] Bad object model from the server: " + e.Message); }
                if (model == null || model.parts == null || model.parts.Length == 0) return;

                loaded[hash] = model;

                // the views that wait for this model
                var ready = new List<string>();
                foreach (var pair in hashOfObject) if (pair.Value == hash && waiting.ContainsKey(pair.Key)) ready.Add(pair.Key);

                foreach (string id in ready)
                {
                    Waiting w = waiting[id];
                    waiting.Remove(id);
                    if (w.view != null && w.body != null) Apply(w.view, w.body, w.baseMaterial, model);
                }
            }, 30000);
        }

        // Builds the model under the view and hides the primitive.
        private static void Apply(SceneObjectView view, GameObject body, Material baseMaterial, SceneModelDto model)
        {
            GameObject holder = null;

            try
            {
                holder = new GameObject("Model");
                holder.layer = body.layer;
                holder.transform.SetParent(view.transform, false);
                StationModelView owner = holder.AddComponent<StationModelView>();

                // textures
                int textureCount = model.textures != null ? model.textures.Length : 0;
                var textures = new Texture2D[textureCount];

                for (int i = 0; i < textureCount; i++)
                {
                    SceneModelTextureDto t = model.textures[i];
                    if (t == null || string.IsNullOrEmpty(t.png)) continue;
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                    if (texture.LoadImage(Convert.FromBase64String(t.png)))
                    {
                        textures[i] = texture;
                        owner.Keep(texture);
                    }
                    else
                    {
                        Object.Destroy(texture);
                    }
                }

                // materials: solid = a copy of the scene's base material; transparent (text labels, glass) = unlit, blended
                Shader blended = Shader.Find("Sprites/Default");
                int materialCount = model.materials != null ? model.materials.Length : 0;
                var materials = new Material[Mathf.Max(materialCount, 1)];
                var seeThrough = new bool[materials.Length];

                for (int i = 0; i < materials.Length; i++)
                {
                    SceneModelMaterialDto m = i < materialCount ? model.materials[i] : null;
                    bool transparent = m != null && m.transparent && blended != null;
                    Material material = transparent ? new Material(blended) : new Material(baseMaterial);
                    material.name = "Model_" + view.id + "_" + i;

                    if (m != null)
                    {
                        if (m.color != null && m.color.Length == 4)
                            material.color = new Color(Mathf.Clamp01(m.color[0]), Mathf.Clamp01(m.color[1]), Mathf.Clamp01(m.color[2]), Mathf.Clamp01(m.color[3]));
                        if (m.texture >= 0 && m.texture < textureCount && textures[m.texture] != null)
                            material.mainTexture = textures[m.texture];

                        if (!transparent)
                        {
                            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", Mathf.Clamp01(m.smoothness));
                            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", Mathf.Clamp01(m.metallic));
                        }
                    }

                    owner.Keep(material);
                    materials[i] = material;
                    seeThrough[i] = transparent;
                }

                // meshes
                var solid = new List<Renderer>();
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool any = false;

                foreach (SceneModelPartDto p in model.parts)
                {
                    Mesh mesh = BuildMesh(p);
                    if (mesh == null) continue;
                    owner.Keep(mesh);

                    var part = new GameObject("Part");
                    part.layer = body.layer;
                    part.transform.SetParent(holder.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.AddComponent<MeshRenderer>();
                    int index = Mathf.Clamp(p.material, 0, materials.Length - 1);
                    renderer.sharedMaterial = materials[index];

                    if (seeThrough[index])
                    {
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                    }
                    else
                    {
                        solid.Add(renderer);
                    }

                    if (any) bounds.Encapsulate(mesh.bounds);
                    else { bounds = mesh.bounds; any = true; }
                }

                if (!any)
                {
                    Object.Destroy(holder);
                    return;
                }

                // tapping: a box around the model instead of the primitive's collider
                var box = holder.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * 0.01f);
                foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;

                view.UseModel(solid.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MediSimVR] Object model of " + view.id + " not shown: " + e.Message);
                if (holder != null) Object.Destroy(holder);
            }
        }

        private static Mesh BuildMesh(SceneModelPartDto p)
        {
            if (p == null || p.vertices == null || p.vertices.Length < 9 || p.vertices.Length % 3 != 0) return null;
            if (p.triangles == null || p.triangles.Length < 3 || p.triangles.Length % 3 != 0) return null;

            int n = p.vertices.Length / 3;
            foreach (int index in p.triangles) if (index < 0 || index >= n) return null;

            var vertices = new Vector3[n];
            for (int i = 0; i < n; i++) vertices[i] = new Vector3(p.vertices[i * 3], p.vertices[i * 3 + 1], p.vertices[i * 3 + 2]);

            var mesh = new Mesh { name = "StationModel" };
            if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;

            if (p.uv != null && p.uv.Length == n * 2)
            {
                var uv = new Vector2[n];
                for (int i = 0; i < n; i++) uv[i] = new Vector2(p.uv[i * 2], p.uv[i * 2 + 1]);
                mesh.uv = uv;
            }

            mesh.triangles = p.triangles;

            if (p.normals != null && p.normals.Length == n * 3)
            {
                var normals = new Vector3[n];
                for (int i = 0; i < n; i++) normals[i] = new Vector3(p.normals[i * 3], p.normals[i * 3 + 1], p.normals[i * 3 + 2]);
                mesh.normals = normals;
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
