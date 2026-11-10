using UnityEditor;
using UnityEngine;

namespace MediSimVR.EditorTools
{
    // The small 3D world used by the phone "Live Scene" view and the PC sync scene (same look on both):
    // SyncWorld root -> Floor (4 m x 4 m, 1 m grid), SyncCube (local pos 0,0.5,0), Light, Camera.
    // Same floor size / cube colour / cube start pose as the Quest receiver's SyncWorld.
    public static class SyncWorldBuilder
    {
        const string MaterialsDir = "Assets/MediSimVR/Materials";
        public static readonly Vector3 CameraPos = new Vector3(0f, 2.4f, -3.6f);
        public static readonly Vector3 CameraLookAt = new Vector3(0f, 0.5f, 0f);

        public class Result
        {
            public Transform world, cube;
            public Camera camera;
        }

        // origin = world position of the SyncWorld root. cameraName = name of the camera object.
        public static Result Build(Vector3 origin, string cameraName, Color background)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.50f);

            var world = new GameObject("SyncWorld").transform;
            world.position = origin;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(world, false);
            floor.transform.localScale = new Vector3(0.4f, 1f, 0.4f); // 4 m x 4 m
            floor.GetComponent<MeshRenderer>().sharedMaterial = Mat("SyncFloor", new Color(0.80f, 0.80f, 0.80f), 0.1f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            var gridMat = Mat("SyncGrid", new Color(0.55f, 0.58f, 0.62f), 0f);
            var grid = new GameObject("Grid").transform;
            grid.SetParent(world, false);
            for (int i = -2; i <= 2; i++)
            {
                Line(grid, "X" + i, new Vector3(i, 0.002f, 0f), new Vector3(0.012f, 0.001f, 4f), gridMat);
                Line(grid, "Z" + i, new Vector3(0f, 0.002f, i), new Vector3(4f, 0.001f, 0.012f), gridMat);
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SyncCube";
            cube.transform.SetParent(world, false);
            cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = Vector3.one;
            cube.GetComponent<MeshRenderer>().sharedMaterial = Mat("SyncCube", new Color32(0x1A, 0x80, 0xE8, 0xFF), 0.3f);
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(world, false);
            lightGo.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;

            var camGo = new GameObject(cameraName);
            camGo.transform.SetParent(world, false);
            camGo.transform.localPosition = CameraPos;
            camGo.transform.LookAt(world.TransformPoint(CameraLookAt));
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            cam.cullingMask = ~(1 << 5); // everything except UI

            return new Result { world = world, cube = cube.transform, camera = cam };
        }

        static void Line(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static Material Mat(string name, Color c, float smoothness)
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
