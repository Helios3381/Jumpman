using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeddingRun
{
    /// <summary>Small procedural mesh toolkit (flat-shaded low-poly look) + mesh merging.</summary>
    public static class MeshKit
    {
        class Builder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<int> t = new List<int>();

            /// <summary>Adds a triangle, flipping it so its front face points along <paramref name="outward"/>.</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { var tmp = b; b = c; c = tmp; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetTriangles(t, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (!cache.TryGetValue(key, out var m) || m == null) { m = make(); cache[key] = m; }
            return m;
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            return Cached("prim_" + type, () =>
            {
                // Built-in meshes first (no collider → no "SphereCollider doesn't exist" errors on stripped WebGL builds).
                string file = type == PrimitiveType.Cube ? "Cube.fbx" : type == PrimitiveType.Quad ? "Quad.fbx" : $"New-{type}.fbx";
                Mesh builtin = null;
                try { builtin = Resources.GetBuiltinResource<Mesh>(file); } catch { }
                if (builtin != null) return builtin;
                var go = GameObject.CreatePrimitive(type);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(go);
                return mesh;
            });
        }

        public static Mesh Cube => PrimitiveMesh(PrimitiveType.Cube);
        public static Mesh Sphere => PrimitiveMesh(PrimitiveType.Sphere);
        public static Mesh Capsule => PrimitiveMesh(PrimitiveType.Capsule);
        public static Mesh Cylinder => PrimitiveMesh(PrimitiveType.Cylinder);
        public static Mesh QuadMesh => PrimitiveMesh(PrimitiveType.Quad);

        /// <summary>Frustum / cone of height 1 standing on y=0 (radius at bottom/top).</summary>
        public static Mesh Frustum(float rBottom, float rTop, int seg = 12)
        {
            return Cached($"frustum_{rBottom}_{rTop}_{seg}", () =>
            {
                var b = new Builder();
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    Vector3 b0 = d0 * rBottom, b1 = d1 * rBottom;
                    Vector3 t0 = d0 * rTop + Vector3.up, t1 = d1 * rTop + Vector3.up;
                    Vector3 outward = (d0 + d1).normalized;
                    if (rTop > 0.0001f) b.Quad(b0, t0, t1, b1, outward); else b.Tri(b0, t0, b1, outward);
                    b.Tri(Vector3.zero, b0, b1, Vector3.down);
                    if (rTop > 0.0001f) b.Tri(Vector3.up, t0, t1, Vector3.up);
                }
                return b.Build("Frustum");
            });
        }

        public static Mesh Cone(int seg = 12) => Frustum(0.5f, 0f, seg);

        /// <summary>Torus in the XY plane (faces the camera), major radius R, tube radius r.</summary>
        public static Mesh Torus(float R, float r, int seg = 20, int side = 8, float arc = 360f)
        {
            return Cached($"torus_{R}_{r}_{seg}_{side}_{arc}", () =>
            {
                var b = new Builder();
                float arcRad = arc * Mathf.Deg2Rad;
                Vector3 P(int i, int j, out Vector3 center)
                {
                    float u = i * arcRad / seg, w = j * Mathf.PI * 2 / side;
                    Vector3 dir = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0);
                    center = dir * R;
                    return center + (dir * Mathf.Cos(w) + Vector3.forward * Mathf.Sin(w)) * r;
                }
                for (int i = 0; i < seg; i++)
                    for (int j = 0; j < side; j++)
                    {
                        var a = P(i, j, out var c0); var bb = P(i + 1, j, out var c1);
                        var c = P(i + 1, j + 1, out _); var d = P(i, j + 1, out _);
                        Vector3 mid = (a + bb + c + d) / 4f;
                        b.Quad(a, bb, c, d, mid - (c0 + c1) / 2f);
                    }
                return b.Build("Torus");
            });
        }

        /// <summary>Flat-shaded diamond (octahedron) of radius 0.5.</summary>
        public static Mesh Diamond()
        {
            return Cached("diamond", () =>
            {
                var b = new Builder();
                Vector3 top = Vector3.up * 0.35f, bottom = Vector3.down * 0.5f;
                int seg = 8;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                    Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                    Vector3 q0 = p0 * 0.55f + top, q1 = p1 * 0.55f + top;
                    b.Quad(p0, q0, q1, p1, (p0 + p1).normalized + Vector3.up * 0.3f);
                    b.Tri(top, q0, q1, Vector3.up);
                    b.Tri(bottom, p0, p1, (p0 + p1).normalized - Vector3.up * 0.5f);
                }
                return b.Build("Diamond");
            });
        }

        /// <summary>Extruded heart, roughly 1 unit wide, centred, facing -Z.</summary>
        public static Mesh Heart(float depth = 0.35f)
        {
            return Cached($"heart_{depth}", () =>
            {
                var b = new Builder();
                int n = 40;
                var pts = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i * Mathf.PI * 2 / n;
                    float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
                    float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                    pts[i] = new Vector3(x, y + 2.5f, 0) / 34f;
                }
                Vector3 f = Vector3.back * depth / 2, k = Vector3.forward * depth / 2;
                Vector3 center = new Vector3(0, -0.02f, 0);
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = pts[i], c = pts[(i + 1) % n];
                    b.Tri(center + f, a + f, c + f, Vector3.back);
                    b.Tri(center + k, a + k, c + k, Vector3.forward);
                    Vector3 mid = (a + c) / 2;
                    b.Quad(a + f, c + f, c + k, a + k, mid - center);
                }
                return b.Build("Heart");
            });
        }

        /// <summary>A gable roof prism (triangle cross-section) 1x1x1, ridge along X.</summary>
        public static Mesh Prism()
        {
            return Cached("prism", () =>
            {
                var b = new Builder();
                Vector3 l0 = new Vector3(-0.5f, 0, -0.5f), l1 = new Vector3(-0.5f, 0, 0.5f), lt = new Vector3(-0.5f, 1, 0);
                Vector3 r0 = new Vector3(0.5f, 0, -0.5f), r1 = new Vector3(0.5f, 0, 0.5f), rt = new Vector3(0.5f, 1, 0);
                b.Tri(l0, l1, lt, Vector3.left);
                b.Tri(r0, r1, rt, Vector3.right);
                b.Quad(l0, lt, rt, r0, new Vector3(0, 0.5f, -1));
                b.Quad(l1, lt, rt, r1, new Vector3(0, 0.5f, 1));
                b.Quad(l0, l1, r1, r0, Vector3.down);
                return b.Build("Prism");
            });
        }

        /// <summary>Copy of a mesh with white vertex colours (for the fog-free vertex-colour material).</summary>
        public static Mesh WithVertexColors(Mesh src)
        {
            return Cached("vc_" + src.name + src.GetInstanceID(), () =>
            {
                var m = Object.Instantiate(src);
                var cols = new Color[m.vertexCount];
                for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
                m.colors = cols;
                return m;
            });
        }

        /// <summary>Vertical gradient quad (vertex colours) for the sky.</summary>
        public static Mesh GradientQuad(Color top, Color bottom, Mesh reuse = null)
        {
            var m = reuse ?? new Mesh { name = "Gradient" };
            m.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) });
            m.SetColors(new List<Color> { bottom, bottom, top, top });
            m.SetUVs(0, new List<Vector2> { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Merge every static child mesh under <paramref name="root"/> into one mesh per material
        /// (huge draw-call saving for skylines). TextMeshPro objects are left untouched.
        /// </summary>
        public static void Merge(Transform root, bool castShadows)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toDelete = new List<GameObject>();
            Matrix4x4 w2l = root.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.transform == root) continue;
                if (mf.GetComponent<TMP_Text>() != null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var mat = mr.sharedMaterial;
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = w2l * mf.transform.localToWorldMatrix });
                toDelete.Add(mf.gameObject);
            }
            foreach (var kv in groups)
            {
                var go = new GameObject("Merged_" + kv.Key.name);
                go.transform.SetParent(root, false);
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = castShadows;
            }
            // Delete leaf parts only; keep any transform that still has TMP children.
            foreach (var go in toDelete)
            {
                if (!go) continue;
                if (go.GetComponentInChildren<TMP_Text>(true) != null)
                {
                    Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(go.GetComponent<MeshFilter>());
                }
                else if (go) Object.DestroyImmediate(go); // immediate: the strip is cloned right after merging
            }
        }
    }
}
