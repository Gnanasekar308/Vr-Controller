using UnityEditor;
using UnityEngine;

// Editor only. The demo objects of the sync scene, created under "SyncWorld" by the Quest receiver setup
// (ReceiverProjectSetup) and the PC sync scene builder (phone project). SAME FILE in both projects, so the
// PC and the Quest have the same object ids: cube, sphere, cylinder, capsule, mat (plane), table (box shape, locked).
namespace MediSimVR.SceneSync.EditorTools
{
    public static class SyncDemoObjects
    {
        const string MaterialsDir = "Assets/MediSimVR/Materials";

        public static void Build(Transform world)
        {
            Prim(world, PrimitiveType.Cube, "cube", "Cube", new Vector3(0f, 0.3f, 0f), Vector3.one * 0.6f, Mat("SyncCube", new Color32(0x1A, 0x80, 0xE8, 0xFF), 0.3f));
            Prim(world, PrimitiveType.Sphere, "sphere", "Sphere", new Vector3(-1.1f, 0.35f, 0.5f), Vector3.one * 0.7f, Mat("SyncObj_sphere", new Color32(0xF2, 0x8C, 0x28, 0xFF), 0.4f));
            Prim(world, PrimitiveType.Cylinder, "cylinder", "Cylinder", new Vector3(1.1f, 0.4f, 0.5f), new Vector3(0.5f, 0.4f, 0.5f), Mat("SyncObj_cylinder", new Color32(0x2E, 0xB8, 0x72, 0xFF), 0.3f));
            Prim(world, PrimitiveType.Capsule, "capsule", "Capsule", new Vector3(-0.9f, 0.5f, -0.9f), Vector3.one * 0.5f, Mat("SyncObj_capsule", new Color32(0x8E, 0x5B, 0xD8, 0xFF), 0.3f));
            Prim(world, PrimitiveType.Plane, "mat", "Mat", new Vector3(0f, 0.005f, 1.3f), new Vector3(0.08f, 1f, 0.08f), Mat("SyncObj_mat", new Color32(0xF2, 0xC2, 0x30, 0xFF), 0.1f));

            // a non-primitive object (several meshes) = "box" shape on the phone; starts locked
            var wood = Mat("SyncObj_table", new Color32(0x8B, 0x5A, 0x2B, 0xFF), 0.2f);
            var table = new GameObject("Table");
            table.transform.SetParent(world, false);
            table.transform.localPosition = new Vector3(1.0f, 0f, -0.9f);
            Part(table.transform, "Top", new Vector3(0f, 0.72f, 0f), new Vector3(1.0f, 0.06f, 0.6f), wood);
            for (int i = 0; i < 4; i++)
                Part(table.transform, "Leg" + i, new Vector3(i % 2 == 0 ? -0.44f : 0.44f, 0.35f, i < 2 ? -0.24f : 0.24f), new Vector3(0.06f, 0.7f, 0.06f), wood);
            table.AddComponent<SyncObject>().Setup("table", "Table", true);
        }

        static void Prim(Transform world, PrimitiveType type, string id, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(world, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<SyncObject>().Setup(id, name, false);
        }

        static void Part(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        public static Material Mat(string name, Color c, float smoothness)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets/MediSimVR", "Materials");
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = c;
            mat.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
