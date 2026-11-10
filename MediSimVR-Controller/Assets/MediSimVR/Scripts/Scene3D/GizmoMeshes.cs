using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    // Small procedural meshes for the move / rotate / scale gizmo (made once, shared).
    public static class GizmoMeshes
    {
        private static Mesh cone, torus;

        // base circle (radius 0.5) at y = 0, tip at y = 1
        public static Mesh Cone
        {
            get
            {
                if (cone != null) return cone;
                const int n = 20;
                var v = new List<Vector3>();
                var t = new List<int>();
                v.Add(new Vector3(0f, 1f, 0f));   // 0 tip
                v.Add(Vector3.zero);              // 1 base centre
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = 2 + i, b = 2 + (i + 1) % n;
                    t.Add(0); t.Add(b); t.Add(a);   // side
                    t.Add(1); t.Add(a); t.Add(b);   // base
                }
                cone = Build("GizmoCone", v, t);
                return cone;
            }
        }

        // ring in the XZ plane (normal = Y), major radius 1, tube radius 0.022
        public static Mesh Torus
        {
            get
            {
                if (torus != null) return torus;
                const int seg = 72, tube = 6;
                const float r = 0.022f;
                var v = new List<Vector3>();
                var t = new List<int>();
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int j = 0; j < tube; j++)
                    {
                        float b = j * Mathf.PI * 2f / tube;
                        v.Add(c + c * (Mathf.Cos(b) * r) + Vector3.up * (Mathf.Sin(b) * r));
                    }
                }
                for (int i = 0; i < seg; i++)
                {
                    int i2 = (i + 1) % seg;
                    for (int j = 0; j < tube; j++)
                    {
                        int j2 = (j + 1) % tube;
                        int a = i * tube + j, b = i2 * tube + j, c = i2 * tube + j2, d = i * tube + j2;
                        t.Add(a); t.Add(b); t.Add(c);
                        t.Add(a); t.Add(c); t.Add(d);
                    }
                }
                torus = Build("GizmoRing", v, t);
                return torus;
            }
        }

        private static Mesh Build(string name, List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
