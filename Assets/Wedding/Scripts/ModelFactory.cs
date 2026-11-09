using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeddingRun
{
    /// <summary>Builds every character / item / obstacle model out of simple low-poly parts.</summary>
    public static class ModelFactory
    {
        static Material litTemplate, unlitTemplate, vertexTemplate, fadeTemplate;
        public static TMP_FontAsset Font;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Transform prototypeRoot;
        static readonly Dictionary<ModelId, GameObject> prototypes = new Dictionary<ModelId, GameObject>();

        public static void Init(Material lit, Material unlit, Material vertex, Material fade, TMP_FontAsset font)
        {
            litTemplate = lit; unlitTemplate = unlit; vertexTemplate = vertex; fadeTemplate = fade; Font = font;
        }

        public static Material VertexMaterial => vertexTemplate;

        public static Material Mat(Color c, bool unlit = false, float smooth = 0.25f)
        {
            string key = (unlit ? "u" : "l") + ColorUtility.ToHtmlStringRGBA(c) + smooth;
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            if (c.a < 0.999f)
            {
                m = new Material(fadeTemplate) { color = c };
            }
            else
            {
                m = new Material(unlit ? unlitTemplate : litTemplate) { color = c };
                if (!unlit && m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            }
            m.name = key;
            mats[key] = m;
            return m;
        }

        public static Color H(string hex) => Stages.C(hex);

        // ------------------------------------------------------------------ parts

        public static Transform Node(Transform parent, string name, Vector3 pos, Vector3 euler = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            return t;
        }

        public static Transform Part(Transform parent, Mesh mesh, Color c, Vector3 pos, Vector3 scale, Vector3 euler = default, bool unlit = false, float smooth = 0.25f)
        {
            var go = new GameObject(mesh.name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            t.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(c, unlit, smooth);
            if (unlit) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            return t;
        }

        public static Transform Box(Transform p, Color c, Vector3 pos, Vector3 size, Vector3 euler = default, bool unlit = false)
            => Part(p, MeshKit.Cube, c, pos, size, euler, unlit);

        public static Transform Ball(Transform p, Color c, Vector3 pos, Vector3 size, Vector3 euler = default, bool unlit = false)
            => Part(p, MeshKit.Sphere, c, pos, size, euler, unlit);

        /// <summary>Unity cylinder is 2 units tall; this takes the real height.</summary>
        public static Transform Cyl(Transform p, Color c, Vector3 pos, float radius, float height, Vector3 euler = default, bool unlit = false)
            => Part(p, MeshKit.Cylinder, c, pos, new Vector3(radius * 2, height / 2, radius * 2), euler, unlit);

        /// <summary>Frustum standing on <paramref name="pos"/>.</summary>
        public static Transform Frus(Transform p, Color c, Vector3 pos, float rBottom, float rTop, float height, Vector3 euler = default, int seg = 12, bool unlit = false)
        {
            float k = Mathf.Max(rBottom, rTop);
            return Part(p, MeshKit.Frustum(rBottom / k, rTop / k, seg), c, pos, new Vector3(k, height, k), euler, unlit);
        }

        public static TextMeshPro Text3D(Transform parent, string text, Color c, Vector3 pos, float size, Vector3 euler = default)
        {
            var go = new GameObject("Text_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var t = go.AddComponent<TextMeshPro>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.rectTransform.sizeDelta = new Vector2(4, 2);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        // ------------------------------------------------------------------ items

        /// <summary>Returns a fresh instance of the model (prototypes are built once and cloned).</summary>
        public static GameObject Spawn(ModelId id, Transform parent)
        {
            if (prototypeRoot == null)
            {
                // Root stays active (TextMeshPro needs Awake to run while building); each prototype is disabled after.
                prototypeRoot = new GameObject("[Prototypes]").transform;
                prototypeRoot.position = new Vector3(0, -5000, 0);
                Object.DontDestroyOnLoad(prototypeRoot.gameObject);
            }
            if (!prototypes.TryGetValue(id, out var proto) || proto == null)
            {
                proto = new GameObject(id.ToString());
                proto.transform.SetParent(prototypeRoot, false);
                BuildModel(id, proto.transform);
                proto.SetActive(false);
                prototypes[id] = proto;
            }
            var go = Object.Instantiate(proto, parent);
            go.name = id.ToString();
            go.transform.localPosition = Vector3.zero;
            go.SetActive(true);
            return go;
        }

        static void BuildModel(ModelId id, Transform r)
        {
            Color white = Color.white, black = H("#2A2230"), gold = Palette.Gold, red = H("#D7263D");
            switch (id)
            {
                case ModelId.Heart:
                    Part(r, MeshKit.Heart(0.32f), H("#FF4F7B"), Vector3.zero, Vector3.one * 1.15f, default, false, 0.6f);
                    Ball(r, H("#FFC2D3"), new Vector3(-0.2f, 0.16f, -0.17f), new Vector3(0.14f, 0.1f, 0.05f), new Vector3(0, 0, 30), true);
                    break;

                case ModelId.Ring:
                    Part(r, MeshKit.Torus(0.32f, 0.075f, 24, 8), gold, new Vector3(0, -0.12f, 0), Vector3.one, default, false, 0.85f);
                    Part(r, MeshKit.Diamond(), H("#BFF3FF"), new Vector3(0, 0.33f, 0), Vector3.one * 0.34f, default, false, 0.9f);
                    Box(r, gold, new Vector3(0, 0.2f, 0), new Vector3(0.12f, 0.08f, 0.12f));
                    break;

                case ModelId.RedPacket:
                    Box(r, red, Vector3.zero, new Vector3(0.62f, 0.85f, 0.08f));
                    Box(r, H("#B3182E"), new Vector3(0, 0.27f, -0.02f), new Vector3(0.62f, 0.3f, 0.06f), new Vector3(0, 0, 0));
                    Cyl(r, gold, new Vector3(0, 0.1f, -0.05f), 0.16f, 0.02f, new Vector3(90, 0, 0));
                    Text3D(r, "囍", red, new Vector3(0, 0.1f, -0.065f), 2.4f);
                    Text3D(r, "福", gold, new Vector3(0, -0.22f, -0.05f), 2.2f);
                    break;

                case ModelId.PineappleBun:
                    {
                        // Hong Kong pineapple bun: a golden dome covered in a cracked, checkered sugar crust.
                        // Tilted towards the camera so the famous top is what you see.
                        var bun = Node(r, "Bun", new Vector3(0, -0.05f, 0), new Vector3(-32, 0, 0));
                        Ball(bun, H("#B8742A"), new Vector3(0, -0.02f, 0), new Vector3(0.92f, 0.5f, 0.92f));   // bread + crack colour
                        Cyl(bun, H("#C98A3E"), new Vector3(0, -0.2f, 0), 0.43f, 0.08f);                       // flat bottom
                        float rx = 0.47f, ry = 0.27f;
                        for (int gx = -3; gx <= 3; gx++)
                            for (int gz = -3; gz <= 3; gz++)
                            {
                                // diamond grid like the real crust
                                float u = (gx + gz) * 0.115f, v = (gx - gz) * 0.115f;
                                float d = (u * u + v * v) / (rx * rx * 0.86f);
                                if (d > 1f) continue;
                                float y = ry * Mathf.Sqrt(1 - Mathf.Min(1, (u * u + v * v) / (rx * rx))) - 0.02f;
                                Vector3 n = new Vector3(u / (rx * rx), (y + 0.02f) / (ry * ry), v / (rx * rx)).normalized;
                                var tile = Part(bun, MeshKit.Sphere, (gx + gz) % 2 == 0 ? H("#F7C44F") : H("#F2B640"),
                                    new Vector3(u, y, v), new Vector3(0.15f, 0.07f, 0.15f), default, false, 0.45f);
                                tile.localRotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, 45, 0);
                            }
                    }
                    break;

                case ModelId.GongCandy:
                    Cyl(r, red, Vector3.zero, 0.2f, 0.55f, new Vector3(0, 0, 90));
                    Cyl(r, gold, Vector3.zero, 0.205f, 0.12f, new Vector3(0, 0, 90));
                    Frus(r, red, new Vector3(0.27f, 0, 0), 0.06f, 0.22f, 0.22f, new Vector3(0, 0, -90), 8);
                    Frus(r, red, new Vector3(-0.27f, 0, 0), 0.06f, 0.22f, 0.22f, new Vector3(0, 0, 90), 8);
                    Text3D(r, "貢", gold, new Vector3(0, 0, -0.21f), 1.6f);
                    break;

                case ModelId.BubbleTea:
                    {
                        var cup = Node(r, "Cup", new Vector3(0, -0.45f, 0));
                        Frus(cup, H("#C99367"), Vector3.zero, 0.24f, 0.31f, 0.78f, default, 14);
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * 60f * Mathf.Deg2Rad;
                            Ball(cup, H("#2B1A14"), new Vector3(Mathf.Cos(a) * 0.2f, 0.1f + (i % 2) * 0.08f, Mathf.Sin(a) * 0.2f - 0.04f), Vector3.one * 0.12f);
                        }
                        Cyl(cup, white, new Vector3(0, 0.8f, 0), 0.32f, 0.05f);
                        Cyl(cup, H("#FF7FA8"), new Vector3(0.08f, 1.0f, 0), 0.05f, 0.5f, new Vector3(0, 0, -12));
                        Box(cup, white, new Vector3(0, 0.4f, -0.27f), new Vector3(0.3f, 0.2f, 0.02f), new Vector3(-6, 0, 0));
                        Text3D(cup, "珍奶", H("#8A4B2F"), new Vector3(0, 0.4f, -0.29f), 1.1f, new Vector3(-6, 0, 0));
                    }
                    break;

                case ModelId.EbiFry:
                    Part(r, MeshKit.Capsule, H("#D98A2B"), new Vector3(-0.05f, 0, 0), new Vector3(0.32f, 0.42f, 0.32f), new Vector3(0, 0, 70));
                    for (int i = 0; i < 5; i++)
                        Ball(r, H("#C0731E"), new Vector3(-0.35f + i * 0.13f, 0.1f - i * 0.04f, -0.13f), Vector3.one * 0.08f);
                    Box(r, H("#F25C3A"), new Vector3(0.42f, -0.08f, 0), new Vector3(0.22f, 0.08f, 0.06f), new Vector3(0, 0, 30));
                    Box(r, H("#F25C3A"), new Vector3(0.42f, -0.2f, 0), new Vector3(0.22f, 0.08f, 0.06f), new Vector3(0, 0, -20));
                    Cyl(r, H("#FFE45C"), new Vector3(-0.35f, -0.3f, -0.12f), 0.16f, 0.04f, new Vector3(70, 0, 0));
                    break;

                case ModelId.MisoKatsu:
                    {
                        // Nagoya miso katsu: sliced cutlet with dark miso sauce, sesame and cabbage
                        var k = Node(r, "Katsu", Vector3.zero, new Vector3(-25, 0, 0));
                        Cyl(k, Color.white, new Vector3(0, -0.14f, 0), 0.48f, 0.05f);                     // plate
                        Ball(k, H("#7CC46B"), new Vector3(-0.28f, -0.04f, 0.15f), new Vector3(0.32f, 0.18f, 0.3f)); // cabbage
                        for (int i = 0; i < 4; i++)
                        {
                            float x = -0.24f + i * 0.17f;
                            Box(k, H("#D9963B"), new Vector3(x, -0.02f, -0.02f), new Vector3(0.15f, 0.16f, 0.42f), new Vector3(0, 0, -6));
                            Box(k, H("#5A2E1A"), new Vector3(x, 0.07f, -0.02f), new Vector3(0.155f, 0.05f, 0.43f), new Vector3(0, 0, -6)); // miso sauce
                            Box(k, H("#F5EEDB"), new Vector3(x - 0.02f, 0.1f, -0.1f + (i % 2) * 0.12f), new Vector3(0.03f, 0.015f, 0.02f), default, true);
                            Box(k, H("#F5EEDB"), new Vector3(x + 0.03f, 0.1f, 0.05f - (i % 2) * 0.1f), new Vector3(0.03f, 0.015f, 0.02f), default, true);
                        }
                    }
                    break;

                case ModelId.WeddingCookie:
                    Cyl(r, red, Vector3.zero, 0.42f, 0.2f, new Vector3(90, 0, 0));
                    Part(r, MeshKit.Torus(0.36f, 0.025f, 24, 6), gold, new Vector3(0, 0, -0.1f), Vector3.one);
                    Text3D(r, "囍", gold, new Vector3(0, 0.01f, -0.11f), 3.2f);
                    break;

                case ModelId.Rose:
                    {
                        Color petal = H("#E8264D"), dark = H("#A8152F");
                        Cyl(r, H("#4E9A5B"), new Vector3(0, -0.28f, 0), 0.028f, 0.74f);
                        Ball(r, H("#5BAA6A"), new Vector3(0.14f, -0.3f, 0), new Vector3(0.28f, 0.1f, 0.08f), new Vector3(0, 0, 28));
                        Ball(r, H("#5BAA6A"), new Vector3(-0.14f, -0.42f, 0), new Vector3(0.28f, 0.1f, 0.08f), new Vector3(0, 0, -28));
                        Ball(r, dark, new Vector3(0, 0.26f, 0), Vector3.one * 0.44f);
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * 60f * Mathf.Deg2Rad;
                            Ball(r, petal, new Vector3(Mathf.Cos(a) * 0.19f, 0.26f + Mathf.Sin(a) * 0.17f, -0.12f), Vector3.one * 0.25f);
                        }
                        Ball(r, dark, new Vector3(0, 0.26f, -0.24f), Vector3.one * 0.16f);
                        Ball(r, H("#FF6B8A"), new Vector3(-0.02f, 0.28f, -0.31f), Vector3.one * 0.07f);
                    }
                    break;

                case ModelId.Shield:
                    {
                        // A real shield silhouette: gold rim, red face, white 囍, plus a soft glow and sparkles
                        var sh = Node(r, "ShieldBody", new Vector3(0, 0.03f, 0));
                        Part(sh, MeshKit.ShieldShape(0.16f), gold, Vector3.zero, Vector3.one * 0.95f, default, false, 0.85f);
                        Part(sh, MeshKit.ShieldShape(0.1f), H("#D7263D"), new Vector3(0, 0.02f, -0.05f), Vector3.one * 0.74f, default, false, 0.5f);
                        Part(sh, MeshKit.ShieldShape(0.02f), H("#FF6B7F"), new Vector3(-0.1f, 0.17f, -0.105f), new Vector3(0.22f, 0.26f, 1f), default, true); // highlight
                        Box(sh, gold, new Vector3(0, 0.32f, -0.1f), new Vector3(0.62f, 0.05f, 0.03f));
                        Text3D(sh, "囍", Color.white, new Vector3(0, -0.04f, -0.11f), 3.2f);
                        Ball(r, new Color(1f, 0.88f, 0.45f, 0.25f), new Vector3(0, 0, 0.08f), new Vector3(1.35f, 1.35f, 0.3f));        // glow
                        for (int i = 0; i < 4; i++)
                        {
                            float a = (45 + i * 90) * Mathf.Deg2Rad;
                            Part(r, MeshKit.Diamond(), H("#FFF3B0"), new Vector3(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.62f, -0.05f), new Vector3(0.12f, 0.18f, 0.12f), default, true);
                        }
                    }
                    break;

                case ModelId.AlarmClock:
                    Cyl(r, H("#E5484D"), Vector3.zero, 0.36f, 0.24f, new Vector3(90, 0, 0));
                    Cyl(r, white, new Vector3(0, 0, -0.1f), 0.29f, 0.06f, new Vector3(90, 0, 0), true);
                    Box(r, black, new Vector3(0, 0.08f, -0.14f), new Vector3(0.04f, 0.2f, 0.02f), default, true);
                    Box(r, black, new Vector3(0.07f, 0, -0.14f), new Vector3(0.16f, 0.04f, 0.02f), new Vector3(0, 0, -20), true);
                    Ball(r, H("#F2C14E"), new Vector3(-0.24f, 0.36f, 0), Vector3.one * 0.2f);
                    Ball(r, H("#F2C14E"), new Vector3(0.24f, 0.36f, 0), Vector3.one * 0.2f);
                    Box(r, black, new Vector3(-0.22f, -0.38f, 0), new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0, 0, -25));
                    Box(r, black, new Vector3(0.22f, -0.38f, 0), new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0, 0, 25));
                    break;

                case ModelId.Briefcase:
                    Box(r, H("#5B3B2E"), Vector3.zero, new Vector3(0.85f, 0.58f, 0.26f));
                    Box(r, H("#47291F"), new Vector3(0, 0.18f, 0), new Vector3(0.87f, 0.04f, 0.28f));
                    Part(r, MeshKit.Torus(0.13f, 0.035f, 12, 6, 180f), black, new Vector3(0, 0.29f, 0), Vector3.one);
                    Box(r, gold, new Vector3(-0.22f, 0.18f, -0.14f), new Vector3(0.08f, 0.06f, 0.02f));
                    Box(r, gold, new Vector3(0.22f, 0.18f, -0.14f), new Vector3(0.08f, 0.06f, 0.02f));
                    Text3D(r, "加班", white, new Vector3(0, -0.06f, -0.14f), 1.3f);
                    break;

                case ModelId.RainCloud:
                    {
                        Color cloud = H("#7D8296");
                        Ball(r, cloud, new Vector3(0, 0.1f, 0), new Vector3(0.75f, 0.6f, 0.6f));
                        Ball(r, cloud, new Vector3(-0.38f, -0.02f, 0), new Vector3(0.55f, 0.45f, 0.5f));
                        Ball(r, cloud, new Vector3(0.4f, -0.02f, 0), new Vector3(0.6f, 0.48f, 0.5f));
                        Ball(r, H("#9599AB"), new Vector3(0.15f, 0.28f, -0.05f), new Vector3(0.45f, 0.38f, 0.4f));
                        Ball(r, black, new Vector3(-0.12f, 0.05f, -0.3f), new Vector3(0.07f, 0.1f, 0.03f), default, true);
                        Ball(r, black, new Vector3(0.12f, 0.05f, -0.3f), new Vector3(0.07f, 0.1f, 0.03f), default, true);
                        Box(r, black, new Vector3(0, -0.07f, -0.3f), new Vector3(0.14f, 0.025f, 0.02f), new Vector3(0, 0, 0), true);
                        for (int i = 0; i < 4; i++)
                            Part(r, MeshKit.Capsule, H("#5FA8FF"), new Vector3(-0.36f + i * 0.24f, -0.42f - (i % 2) * 0.12f, 0), new Vector3(0.06f, 0.09f, 0.06f), default, true);
                        Part(r, MeshKit.Prism(), H("#FFD43B"), new Vector3(0.18f, -0.42f, -0.05f), new Vector3(0.1f, 0.35f, 0.05f), new Vector3(0, 0, 160), true);
                    }
                    break;

                // ------------------------------------------------ obstacles (base on y=0)

                case ModelId.Cone:
                    Box(r, H("#FF6A13"), new Vector3(0, 0.04f, 0), new Vector3(0.85f, 0.08f, 0.85f));
                    Frus(r, H("#FF7A1A"), new Vector3(0, 0.08f, 0), 0.36f, 0.05f, 1.02f, default, 14);
                    Frus(r, white, new Vector3(0, 0.42f, 0), 0.262f, 0.205f, 0.18f, default, 14);
                    Frus(r, white, new Vector3(0, 0.72f, 0), 0.17f, 0.12f, 0.12f, default, 14);
                    break;

                case ModelId.Barrier:
                    Cyl(r, H("#B9BCC6"), new Vector3(-0.72f, 0.6f, 0), 0.06f, 1.2f);
                    Cyl(r, H("#B9BCC6"), new Vector3(0.72f, 0.6f, 0), 0.06f, 1.2f);
                    for (int row = 0; row < 2; row++)
                        for (int i = 0; i < 6; i++)
                            Box(r, (i + row) % 2 == 0 ? H("#E5383B") : white, new Vector3(-0.6f + i * 0.24f, 0.55f + row * 0.45f, 0), new Vector3(0.24f, 0.2f, 0.08f));
                    Box(r, H("#9EA1AA"), new Vector3(-0.72f, 0.03f, 0), new Vector3(0.35f, 0.06f, 0.5f));
                    Box(r, H("#9EA1AA"), new Vector3(0.72f, 0.03f, 0), new Vector3(0.35f, 0.06f, 0.5f));
                    break;

                case ModelId.GiftStack1: Gifts(r, 1); break;
                case ModelId.GiftStack2: Gifts(r, 2); break;
                case ModelId.GiftStack3: Gifts(r, 3); break;

                case ModelId.LiquorJar:
                    Frus(r, H("#5A3A22"), Vector3.zero, 0.32f, 0.42f, 0.2f, default, 14);
                    Part(r, MeshKit.Sphere, H("#7A4E2C"), new Vector3(0, 0.6f, 0), new Vector3(1.05f, 0.95f, 1.05f), default, false, 0.7f);
                    Frus(r, H("#7A4E2C"), new Vector3(0, 1.0f, 0), 0.3f, 0.2f, 0.2f, default, 14);
                    Ball(r, red, new Vector3(0, 1.22f, 0), new Vector3(0.5f, 0.28f, 0.5f));
                    Part(r, MeshKit.Torus(0.21f, 0.03f, 16, 5), gold, new Vector3(0, 1.17f, 0), Vector3.one, new Vector3(90, 0, 0));
                    Box(r, red, new Vector3(0, 0.62f, -0.48f), new Vector3(0.42f, 0.42f, 0.04f), new Vector3(0, 0, 45));
                    Text3D(r, "酒", gold, new Vector3(0, 0.62f, -0.51f), 3f);
                    break;

                case ModelId.BrickWall:
                    Box(r, H("#B5523B"), new Vector3(0, 0.42f, 0), new Vector3(2.2f, 0.84f, 0.6f));
                    for (int i = 1; i < 4; i++)
                        Box(r, H("#E8C9B5"), new Vector3(0, i * 0.21f, -0.301f), new Vector3(2.2f, 0.025f, 0.01f), default, true);
                    for (int row = 0; row < 4; row++)
                        for (int i = 0; i < 5; i++)
                            Box(r, H("#E8C9B5"), new Vector3(-0.9f + i * 0.45f + (row % 2) * 0.22f, row * 0.21f + 0.105f, -0.301f), new Vector3(0.025f, 0.21f, 0.01f), default, true);
                    Box(r, H("#9A9089"), new Vector3(0, 0.9f, 0), new Vector3(2.3f, 0.12f, 0.7f));
                    break;

                case ModelId.Scooter:
                    {
                        Color body = H("#58C4B0");
                        foreach (float x in new[] { -0.62f, 0.62f })
                        {
                            Cyl(r, black, new Vector3(x, 0.28f, 0), 0.28f, 0.16f, new Vector3(90, 0, 0));
                            Cyl(r, H("#C9CCD6"), new Vector3(x, 0.28f, -0.01f), 0.12f, 0.18f, new Vector3(90, 0, 0));
                        }
                        Box(r, body, new Vector3(0, 0.38f, 0), new Vector3(0.9f, 0.16f, 0.42f));
                        Ball(r, body, new Vector3(-0.42f, 0.62f, 0), new Vector3(0.85f, 0.5f, 0.5f));
                        Box(r, H("#2B2B33"), new Vector3(-0.38f, 0.9f, 0), new Vector3(0.7f, 0.12f, 0.36f));
                        Box(r, body, new Vector3(0.55f, 0.75f, 0), new Vector3(0.22f, 0.8f, 0.4f), new Vector3(0, 0, -15));
                        Box(r, H("#3A3A44"), new Vector3(0.66f, 1.18f, 0), new Vector3(0.08f, 0.08f, 0.8f));
                        Ball(r, H("#FFF3B0"), new Vector3(0.7f, 0.98f, 0), new Vector3(0.12f, 0.16f, 0.2f), default, true);
                        Box(r, H("#3A3A44"), new Vector3(-0.85f, 0.95f, 0), new Vector3(0.3f, 0.3f, 0.35f)); // rear box
                    }
                    break;

                case ModelId.Suitcase:
                    {
                        Color c = H("#26B3C4");
                        Box(r, c, new Vector3(0, 0.62f, 0), new Vector3(0.75f, 1.0f, 0.42f));
                        for (int i = -1; i <= 1; i++)
                            Box(r, H("#1D8F9C"), new Vector3(i * 0.22f, 0.62f, -0.215f), new Vector3(0.05f, 0.92f, 0.02f));
                        Cyl(r, H("#9EA1AA"), new Vector3(-0.2f, 1.25f, 0), 0.025f, 0.32f);
                        Cyl(r, H("#9EA1AA"), new Vector3(0.2f, 1.25f, 0), 0.025f, 0.32f);
                        Box(r, black, new Vector3(0, 1.41f, 0), new Vector3(0.5f, 0.06f, 0.08f));
                        Ball(r, black, new Vector3(-0.28f, 0.07f, 0), Vector3.one * 0.14f);
                        Ball(r, black, new Vector3(0.28f, 0.07f, 0), Vector3.one * 0.14f);
                        Part(r, MeshKit.Heart(0.02f), H("#FF6F91"), new Vector3(0.12f, 0.85f, -0.235f), Vector3.one * 0.28f);
                        Box(r, white, new Vector3(-0.12f, 0.45f, -0.225f), new Vector3(0.3f, 0.16f, 0.01f), new Vector3(0, 0, 8));
                        Text3D(r, "NGO", H("#1D8F9C"), new Vector3(-0.12f, 0.45f, -0.235f), 0.9f, new Vector3(0, 0, 8));
                    }
                    break;

                case ModelId.ChampagneTower:
                    {
                        Box(r, white, new Vector3(0, 0.12f, 0), new Vector3(1.3f, 0.24f, 0.6f));
                        Box(r, gold, new Vector3(0, 0.22f, -0.305f), new Vector3(1.3f, 0.04f, 0.01f), default, true);
                        Color glass = H("#F6E3A1"), stem = H("#E9EEF5");
                        int rows = 5; float gh = 0.44f;
                        for (int row = 0; row < rows; row++)
                        {
                            int count = rows - row;
                            for (int i = 0; i < count; i++)
                            {
                                float x = (i - (count - 1) / 2f) * 0.26f;
                                float y = 0.24f + row * gh;
                                Cyl(r, stem, new Vector3(x, y + 0.08f, 0), 0.02f, 0.16f);
                                Frus(r, glass, new Vector3(x, y + 0.16f, 0), 0.04f, 0.13f, 0.26f, default, 10);
                            }
                        }
                        Ball(r, H("#FFF8DD"), new Vector3(0, 0.24f + rows * gh - 0.02f, 0), new Vector3(0.18f, 0.08f, 0.18f), default, true);
                    }
                    break;

                // ------------------------------------------------ special obstacles

                case ModelId.SumoPig:
                    {
                        // generic Nagoya "miso pig" in a red apron and headband, built facing -X (it charges at the player)
                        Color pink = H("#F7A6B8"), pinkDk = H("#E5869D"), apron = H("#D7263D");
                        var pg = Node(r, "Pig", Vector3.zero);
                        foreach (float z in new[] { -0.28f, 0.28f })
                        {
                            Cyl(pg, pinkDk, new Vector3(0.1f, 0.22f, z), 0.17f, 0.44f);                       // legs
                            Ball(pg, H("#C96A82"), new Vector3(0.06f, 0.03f, z), new Vector3(0.36f, 0.12f, 0.3f));   // hooves
                        }
                        Ball(pg, pink, new Vector3(0, 0.82f, 0), new Vector3(1.2f, 1.05f, 0.95f));             // round body
                        Box(pg, apron, new Vector3(-0.5f, 0.72f, 0), new Vector3(0.12f, 0.75f, 0.62f), new Vector3(0, 0, -8)); // apron
                        Box(pg, Color.white, new Vector3(0, 1.12f, 0), new Vector3(1.0f, 0.06f, 0.9f));          // apron string
                        var apronText = Text3D(pg, "味噌", Color.white, new Vector3(-0.575f, 0.72f, 0), 2.2f, new Vector3(0, 90, -8));
                        apronText.lineSpacing = -10;
                        // arms pushing forward in a charge
                        foreach (float z in new[] { -0.45f, 0.45f })
                        {
                            Part(pg, MeshKit.Capsule, pink, new Vector3(-0.45f, 0.95f, z), new Vector3(0.22f, 0.3f, 0.22f), new Vector3(0, 0, 70));
                            Ball(pg, pinkDk, new Vector3(-0.72f, 1.0f, z), Vector3.one * 0.2f);
                        }
                        // head
                        var hd = Node(pg, "Head", new Vector3(-0.32f, 1.45f, 0));
                        Ball(hd, pink, Vector3.zero, new Vector3(0.85f, 0.75f, 0.8f));
                        Cyl(hd, pinkDk, new Vector3(-0.38f, -0.05f, 0), 0.18f, 0.16f, new Vector3(0, 0, 90));   // snout
                        foreach (float z in new[] { -0.06f, 0.06f }) Ball(hd, H("#7A3A4A"), new Vector3(-0.47f, -0.05f, z), Vector3.one * 0.05f, default, true);
                        foreach (float z in new[] { -0.2f, 0.2f })
                        {
                            Ball(hd, H("#2A1E28"), new Vector3(-0.3f, 0.12f, z), new Vector3(0.07f, 0.1f, 0.07f), default, true);       // eyes
                            Box(hd, H("#2A1E28"), new Vector3(-0.3f, 0.23f, z), new Vector3(0.03f, 0.03f, 0.12f), new Vector3(z > 0 ? 25 : -25, 0, 0), true); // fierce brows
                            Part(hd, MeshKit.Prism(), pinkDk, new Vector3(0.05f, 0.33f, z * 1.4f), new Vector3(0.12f, 0.25f, 0.22f), new Vector3(0, 0, 15)); // ears
                        }
                        Box(hd, Color.white, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.08f, 0.82f));          // headband
                        Part(pg, MeshKit.Torus(0.12f, 0.035f, 12, 5, 300f), pinkDk, new Vector3(0.62f, 0.9f, 0), Vector3.one, new Vector3(0, 90, 0)); // curly tail
                    }
                    break;

                // ------------------------------------------------ overhead: slide under (bottom edge at y = 1.15)

                case ModelId.OverheadSign:
                    {
                        var g = Gantry(r, 1.9f, H("#3A3550"));
                        Box(g, H("#221C36"), new Vector3(0, 1.75f, 0), new Vector3(1.9f, 1.1f, 0.3f));
                        Box(g, H("#FF4F9A"), new Vector3(0, 1.75f, -0.16f), new Vector3(1.75f, 0.95f, 0.02f), default, true);
                        Box(g, H("#221C36"), new Vector3(0, 1.75f, -0.18f), new Vector3(1.6f, 0.8f, 0.02f), default, true);
                        Text3D(g, "茶餐廳", H("#FFD23F"), new Vector3(0, 1.75f, -0.2f), 5f);
                        Hangers(g, 1.9f, 2.3f, H("#3A3550"));
                    }
                    break;

                case ModelId.OverheadBanner:
                    {
                        var g = Gantry(r, 2.0f, H("#5D5A66"));
                        Box(g, red, new Vector3(0, 1.6f, 0), new Vector3(2.0f, 0.9f, 0.05f));
                        Box(g, gold, new Vector3(0, 2.06f, -0.03f), new Vector3(2.0f, 0.05f, 0.02f), default, true);
                        Box(g, gold, new Vector3(0, 1.17f, -0.03f), new Vector3(2.0f, 0.05f, 0.02f), default, true);
                        Text3D(g, "歡迎光臨", gold, new Vector3(0, 1.6f, -0.04f), 4.6f);
                        Hangers(g, 2.0f, 2.05f, H("#5D5A66"));
                    }
                    break;

                case ModelId.OverheadFirecracker:
                    {
                        var g = Gantry(r, 1.8f, H("#8B3A2B"));
                        for (int i = 0; i < 5; i++)
                        {
                            float x = -0.72f + i * 0.36f;
                            Cyl(g, H("#C8A15A"), new Vector3(x, 3.1f, 0), 0.015f, 1.6f);
                            for (int k = 0; k < 8; k++)
                            {
                                float y = 1.27f + k * 0.2f;
                                Cyl(g, k % 2 == 0 ? red : H("#B3182E"), new Vector3(x + (k % 2) * 0.05f, y, 0), 0.06f, 0.18f, new Vector3(0, 0, k % 2 == 0 ? 25 : -25));
                            }
                            Ball(g, gold, new Vector3(x, 2.9f, 0), Vector3.one * 0.12f);
                        }
                    }
                    break;

                case ModelId.OverheadNoren:
                    {
                        var g = Gantry(r, 2.0f, H("#6B4A3A"));
                        Cyl(g, H("#6B4A3A"), new Vector3(0, 2.75f, 0), 0.05f, 2.2f, new Vector3(0, 0, 90));
                        for (int i = 0; i < 3; i++)
                        {
                            float x = -0.66f + i * 0.66f;
                            Box(g, H("#24345E"), new Vector3(x, 1.95f, 0), new Vector3(0.62f, 1.6f, 0.03f));
                            Cyl(g, white, new Vector3(x, 2.15f, -0.02f), 0.17f, 0.01f, new Vector3(90, 0, 0), true);
                        }
                        Text3D(g, "名", H("#24345E"), new Vector3(0, 2.15f, -0.035f), 2.4f);
                    }
                    break;

                case ModelId.OverheadGarland:
                    {
                        var g = Gantry(r, 2.1f, white);
                        Box(g, H("#FF8FB1"), new Vector3(0, 1.55f, 0), new Vector3(2.1f, 0.8f, 0.05f));
                        Box(g, white, new Vector3(0, 1.55f, -0.03f), new Vector3(1.9f, 0.6f, 0.02f), default, true);
                        Text3D(g, "囍", red, new Vector3(0, 1.55f, -0.05f), 5f);
                        for (int i = 0; i <= 8; i++)
                        {
                            float x = -1.0f + i * 0.25f;
                            Ball(g, i % 2 == 0 ? H("#FF6F91") : H("#FFE3EC"), new Vector3(x, 2.0f + Mathf.Sin(i / 8f * Mathf.PI) * 0.15f, -0.05f), Vector3.one * 0.22f);
                        }
                        Hangers(g, 2.1f, 2.0f, white);
                    }
                    break;
            }
        }

        /// <summary>Two posts behind the lane and a beam reaching over it (overhead obstacles hang from it).</summary>
        static Transform Gantry(Transform r, float width, Color c)
        {
            var g = Node(r, "Gantry", Vector3.zero);
            float h = 3.6f;
            foreach (float x in new[] { -width / 2 - 0.15f, width / 2 + 0.15f })
            {
                Cyl(g, c, new Vector3(x, h / 2, 1.7f), 0.08f, h);
                Box(g, c, new Vector3(x, h, 0.75f), new Vector3(0.14f, 0.14f, 2.0f));
            }
            Box(g, c, new Vector3(0, h, -0.2f), new Vector3(width + 0.5f, 0.16f, 0.16f));
            return g;
        }

        static void Hangers(Transform g, float width, float top, Color c)
        {
            foreach (float x in new[] { -width * 0.4f, width * 0.4f })
                Cyl(g, c, new Vector3(x, (top + 3.6f) / 2, -0.2f), 0.02f, 3.6f - top);
        }

        static void Gifts(Transform r, int n)
        {
            Color[] box = { H("#F7A8C4"), H("#9ED9C3"), H("#C3B1E1"), H("#FFE3A3") };
            Color[] rib = { H("#E4577E"), Palette.Gold, H("#7E5BC2"), H("#E4577E") };
            float y = 0; float[] w = { 1.0f, 0.86f, 0.72f }; float[] h = { 0.82f, 0.78f, 0.74f };
            for (int i = 0; i < n; i++)
            {
                var b = Node(r, "Box" + i, new Vector3(i == 1 ? 0.04f : 0, y, 0), new Vector3(0, i == 1 ? -8 : (i == 2 ? 10 : 0), 0));
                Color bc = box[(i + n) % box.Length], rc = rib[(i + n) % rib.Length];
                Box(b, bc, new Vector3(0, h[i] / 2, 0), new Vector3(w[i], h[i], w[i] * 0.7f));
                Box(b, rc, new Vector3(0, h[i] / 2, 0), new Vector3(0.14f, h[i] + 0.01f, w[i] * 0.7f + 0.01f));
                Box(b, rc, new Vector3(0, h[i] / 2, 0), new Vector3(w[i] + 0.01f, h[i] + 0.01f, 0.14f));
                y += h[i];
                if (i == n - 1)
                {
                    Ball(b, rc, new Vector3(-0.15f, h[i] + 0.08f, 0), new Vector3(0.3f, 0.18f, 0.12f), new Vector3(0, 0, 25));
                    Ball(b, rc, new Vector3(0.15f, h[i] + 0.08f, 0), new Vector3(0.3f, 0.18f, 0.12f), new Vector3(0, 0, -25));
                    Ball(b, rc, new Vector3(0, h[i] + 0.06f, 0), Vector3.one * 0.12f);
                }
            }
        }

        // ------------------------------------------------------------------ characters

        public class Rig
        {
            public Transform root, body, hip, legL, legR, armL, armR, head, skirt, bouquet;
            public Transform kneeL, kneeR;   // groom only: so he can kneel on one knee for the proposal
            public bool isBride;
        }

        static readonly Color Skin = new Color(1f, 0.85f, 0.74f);

        static void Face(Transform head, float r, Color hair, bool bride)
        {
            // Eyes / blush on the front-left of the head so they read from the side-view camera.
            Color eye = H("#2A1E28");
            Ball(head, eye, new Vector3(r * 0.86f, 0.02f, -r * 0.34f), new Vector3(0.07f, 0.1f, 0.07f), default, true);
            Ball(head, eye, new Vector3(r * 0.9f, 0.02f, r * 0.18f), new Vector3(0.06f, 0.09f, 0.06f), default, true);
            Ball(head, H("#FF9AAE"), new Vector3(r * 0.72f, -0.09f, -r * 0.62f), new Vector3(0.1f, 0.05f, 0.06f), default, true);
            Box(head, H("#C2566E"), new Vector3(r * 0.95f, -0.11f, -r * 0.08f), new Vector3(0.02f, 0.025f, 0.08f), default, true);
            if (bride)
            {
                Box(head, eye, new Vector3(r * 0.86f, 0.09f, -r * 0.48f), new Vector3(0.02f, 0.02f, 0.06f), new Vector3(0, 0, 30), true);
            }
        }

        static Transform GroomLeg(Transform leg, Color pants, Color shoe, bool formal)
        {
            Part(leg, MeshKit.Capsule, pants, new Vector3(0, -0.18f, 0), new Vector3(0.21f, 0.2f, 0.21f));    // thigh
            var knee = Node(leg, "Knee", new Vector3(0, -0.36f, 0));
            Ball(knee, pants, Vector3.zero, Vector3.one * 0.205f);
            Part(knee, MeshKit.Capsule, pants, new Vector3(0, -0.17f, 0), new Vector3(0.2f, 0.2f, 0.2f));    // shin
            Box(knee, shoe, new Vector3(0.06f, -0.34f, 0), new Vector3(0.3f, 0.1f, 0.18f));
            if (!formal) Box(knee, H("#E5484D"), new Vector3(0.06f, -0.30f, -0.092f), new Vector3(0.2f, 0.03f, 0.01f), default, true);
            return knee;
        }

        /// <summary>Groom: casual clothes (formal=false) or tuxedo (formal=true).</summary>
        public static Rig BuildGroom(Transform parent, bool formal)
        {
            var rig = new Rig();
            Color top = formal ? H("#262636") : H("#7FB8F0");      // tux / t-shirt
            Color pants = formal ? H("#262636") : H("#3E5C8A");    // tux / jeans
            Color shoe = formal ? H("#15131A") : Color.white;
            Color shirt = Color.white, hair = H("#1E1A22");
            rig.root = Node(parent, formal ? "Groom_Formal" : "Groom_Casual", Vector3.zero);
            var facing = Node(rig.root, "Facing", Vector3.zero, new Vector3(0, 28, 0));
            rig.body = Node(facing, "Body", Vector3.zero);

            rig.hip = Node(rig.body, "Hip", new Vector3(0, 0.74f, 0));
            rig.legL = Node(rig.hip, "LegL", new Vector3(0, 0, -0.13f));
            rig.legR = Node(rig.hip, "LegR", new Vector3(0, 0, 0.13f));
            // thigh → knee → shin + shoe (looks like one straight leg until the knee bends)
            rig.kneeL = GroomLeg(rig.legL, pants, shoe, formal);
            rig.kneeR = GroomLeg(rig.legR, pants, shoe, formal);

            var torso = Node(rig.body, "Torso", new Vector3(0, 0.74f, 0));
            Box(torso, top, new Vector3(0, 0.3f, 0), new Vector3(0.42f, 0.62f, 0.5f));
            if (formal)
            {
                Box(torso, top, new Vector3(0.0f, 0.02f, 0), new Vector3(0.44f, 0.12f, 0.52f)); // jacket hem
                Part(torso, MeshKit.Prism(), shirt, new Vector3(0.212f, 0.36f, 0), new Vector3(0.02f, 0.5f, 0.2f), new Vector3(180, 0, 0));
                Box(torso, shirt, new Vector3(0.211f, 0.48f, 0), new Vector3(0.02f, 0.2f, 0.18f));
                Box(torso, black(), new Vector3(0.225f, 0.53f, -0.05f), new Vector3(0.04f, 0.08f, 0.1f), new Vector3(30, 0, 0));
                Box(torso, black(), new Vector3(0.225f, 0.53f, 0.05f), new Vector3(0.04f, 0.08f, 0.1f), new Vector3(-30, 0, 0));
                Ball(torso, H("#E8364F"), new Vector3(0.18f, 0.45f, -0.25f), Vector3.one * 0.11f);   // boutonniere
                Ball(torso, H("#4CAF6E"), new Vector3(0.16f, 0.4f, -0.26f), new Vector3(0.06f, 0.1f, 0.04f));
            }
            else
            {
                Box(torso, pants, new Vector3(0, 0.03f, 0), new Vector3(0.43f, 0.08f, 0.51f));           // jeans waist
                Ball(torso, Palette.Gold, new Vector3(0.0f, 0.34f, -0.255f), new Vector3(0.16f, 0.16f, 0.02f), default, true); // tee print
                Box(torso, H("#B07A4F"), new Vector3(-0.27f, 0.3f, 0), new Vector3(0.14f, 0.42f, 0.38f)); // backpack
                Box(torso, H("#8E5E3A"), new Vector3(-0.1f, 0.45f, -0.25f), new Vector3(0.05f, 0.35f, 0.03f)); // strap
            }

            Color sleeve = top;
            rig.armL = Node(torso, "ArmL", new Vector3(0, 0.55f, -0.3f));
            rig.armR = Node(torso, "ArmR", new Vector3(0, 0.55f, 0.3f));
            foreach (var arm in new[] { rig.armL, rig.armR })
            {
                if (formal)
                    Part(arm, MeshKit.Capsule, sleeve, new Vector3(0, -0.24f, 0), new Vector3(0.16f, 0.26f, 0.16f));
                else
                {
                    Part(arm, MeshKit.Capsule, Skin, new Vector3(0, -0.26f, 0), new Vector3(0.14f, 0.24f, 0.14f));
                    Part(arm, MeshKit.Capsule, sleeve, new Vector3(0, -0.09f, 0), new Vector3(0.18f, 0.12f, 0.18f)); // short sleeve
                }
                Ball(arm, Skin, new Vector3(0, -0.5f, 0), Vector3.one * 0.15f);
            }

            rig.head = Node(torso, "Head", new Vector3(0, 0.62f, 0));
            float hr = 0.33f;
            Ball(rig.head, Skin, new Vector3(0, hr, 0), Vector3.one * hr * 2);
            var face = Node(rig.head, "Face", new Vector3(0, hr, 0));
            Face(face, hr, hair, false);
            Ball(rig.head, hair, new Vector3(-0.04f, hr + 0.13f, 0), new Vector3(0.7f, 0.46f, 0.7f));
            Ball(rig.head, hair, new Vector3(0.16f, hr + 0.2f, -0.05f), new Vector3(0.36f, 0.18f, 0.5f), new Vector3(0, 0, formal ? -20 : -5));
            Ball(rig.head, hair, new Vector3(-0.12f, hr - 0.02f, 0), new Vector3(0.48f, 0.5f, 0.62f));
            Ball(rig.head, Skin, new Vector3(0.0f, hr - 0.02f, -0.32f), new Vector3(0.08f, 0.12f, 0.06f)); // ear
            return rig;
        }

        static Color black() => H("#1A1620");

        /// <summary>A simple wedding guest / night-market visitor. Returns the arm nodes for waving.</summary>
        public class Person { public Transform root, body, armL, armR, legL, legR; public bool elder; }

        public static Transform BuildGuest(Transform parent, int seed, out Transform armL, out Transform armR)
        {
            var pr = BuildPerson(parent, seed, false);
            armL = pr.armL; armR = pr.armR;
            return pr.root;
        }

        /// <summary>A simple townsperson with swingable legs and arms (optionally carrying a shopping bag).</summary>
        static Mesh elderRing, elderHook;
        const float StoopAngle = 8f;
        /// <summary>How far forward an elder holds the arm with the walking stick.</summary>
        public const float CaneArmAngle = 22f;

        /// <param name="elder">Kinmen villagers: white hair, a slight stoop, a walking stick, some in straw hats.</param>
        public static Person BuildPerson(Transform parent, int seed, bool withBag, bool elder = false)
        {
            var pr = new Person { elder = elder };
            var rnd = new System.Random(seed);
            Color Pick(params string[] hex) => H(hex[rnd.Next(hex.Length)]);
            bool woman = rnd.NextDouble() < 0.5;
            float s = (elder ? 1.05f : 0.82f) + (float)rnd.NextDouble() * (elder ? 0.1f : 0.12f);
            var root = Node(parent, "Guest", Vector3.zero);
            var body = Node(root, "Body", Vector3.zero);
            body.localScale = Vector3.one * s;
            Color skin = Pick("#FFDCC2", "#F5C9A8", "#E8B48E", "#FFE4D0");
            Color hair = Pick("#2A1E1A", "#3A2622", "#1A1620", "#5A3A28", "#8A6A4A");
            if (elder) { hair = Pick("#F2F0EC", "#E0DED8", "#C9C7C2", "#B0AEAA"); skin = Pick("#E8C4A0", "#DDB48E", "#D2A67E"); }
            Color cloth = elder ? (woman ? Pick("#7A5A78", "#5E7A8A", "#8A5A5A", "#6A7A5A", "#A07A9A") : Pick("#6A6A70", "#7A6A58", "#4E5E6E", "#8A8678"))
                        : woman ? Pick("#F7A8C4", "#9ED9C3", "#C3B1E1", "#FFE3A3", "#8FC8F0", "#FFB38A")
                                : Pick("#3A4A6E", "#5A5A66", "#6E4A3A", "#2E5E4E", "#7A6A9A");
            // legs (pivot at the hip so they can swing when walking)
            pr.legL = Node(body, "LegL", new Vector3(0, 0.62f, -0.1f));
            pr.legR = Node(body, "LegR", new Vector3(0, 0.62f, 0.1f));
            foreach (var leg in new[] { pr.legL, pr.legR })
            {
                Part(leg, MeshKit.Capsule, elder ? H("#4A4650") : woman ? skin : H("#2E2E3A"), new Vector3(0, -0.29f, 0), new Vector3(0.16f, 0.3f, 0.16f));
                Box(leg, H("#2A2230"), new Vector3(0.04f, -0.58f, 0), new Vector3(0.22f, 0.08f, 0.13f));
            }
            // elders stoop from the hips: the legs stay straight, everything above leans forward
            var upper = body;
            if (elder) upper = Node(Node(body, "Waist", new Vector3(0, 0.62f, 0), new Vector3(0, 0, -StoopAngle)), "Upper", new Vector3(0, -0.62f, 0));
            if (woman && !elder) Frus(body, cloth, new Vector3(0, 0.45f, 0), 0.38f, 0.2f, 0.62f, default, 12);
            else Box(upper, cloth, new Vector3(0, 0.92f, 0), new Vector3(0.38f, 0.56f, 0.44f));
            if (elder)
            {
                Cyl(upper, skin, new Vector3(0, 1.27f, 0), 0.09f, 0.18f);   // neck
                for (int b = 0; b < 3; b++) Ball(upper, Color.Lerp(cloth, Color.white, 0.6f), new Vector3(0.195f, 1.08f - b * 0.15f, 0), Vector3.one * 0.05f);
            }
            else if (woman) Frus(body, cloth, new Vector3(0, 1.0f, 0), 0.2f, 0.22f, 0.36f, default, 12);
            else { Box(body, Color.white, new Vector3(0.19f, 1.0f, 0), new Vector3(0.02f, 0.3f, 0.14f)); Box(body, Pick("#D7263D", "#2A2230", "#E4577E"), new Vector3(0.2f, 1.0f, 0), new Vector3(0.025f, 0.26f, 0.05f)); }
            var armL = Node(upper, "ArmL", new Vector3(0, 1.28f, -0.26f));
            var armR = Node(upper, "ArmR", new Vector3(0, 1.28f, 0.26f));
            foreach (var arm in new[] { armL, armR })
            {
                Part(arm, MeshKit.Capsule, woman && !elder ? skin : cloth, new Vector3(0, -0.22f, 0), new Vector3(0.13f, 0.23f, 0.13f));
                Ball(arm, skin, new Vector3(0, -0.45f, 0), Vector3.one * 0.12f);
            }
            float hr = 0.27f;
            var head = Node(upper, "Head", new Vector3(0, 1.35f, 0));
            Ball(head, skin, new Vector3(0, hr, 0), Vector3.one * hr * 2);
            Ball(head, H("#2A1E28"), new Vector3(hr * 0.86f, hr + 0.02f, -hr * 0.34f), new Vector3(0.06f, 0.08f, 0.06f), default, true);
            Ball(head, H("#2A1E28"), new Vector3(hr * 0.9f, hr + 0.02f, hr * 0.18f), new Vector3(0.05f, 0.07f, 0.05f), default, true);
            Ball(head, H("#FF9AAE"), new Vector3(hr * 0.72f, hr - 0.08f, -hr * 0.62f), new Vector3(0.08f, 0.04f, 0.05f), default, true);
            if (elder)
            {
                Ball(head, hair, new Vector3(-0.04f, hr + 0.1f, 0), new Vector3(0.58f, 0.4f, 0.58f));
                if (woman) Ball(head, hair, new Vector3(-0.3f, hr + 0.12f, 0), Vector3.one * 0.2f);                                     // bun
                if (rnd.NextDouble() < 0.4)
                {
                    // Kinmen straw hat
                    Frus(head, H("#D9BE7A"), new Vector3(0, hr + 0.2f, 0), 0.5f, 0.04f, 0.22f, default, 14);
                    Part(head, MeshKit.Cylinder, H("#B8964E"), new Vector3(0, hr + 0.21f, 0), new Vector3(0.5f, 0.012f, 0.5f));
                }
                else if (rnd.NextDouble() < 0.5)
                {
                    if (elderRing == null) elderRing = MeshKit.Torus(0.06f, 0.012f, 12, 4);
                    foreach (float z in new[] { -hr * 0.34f, hr * 0.18f })
                        Part(head, elderRing, H("#3A3030"), new Vector3(hr * 0.96f, hr + 0.02f, z), Vector3.one, new Vector3(0, 90, 0), true);
                }
                // walking stick: held in the hand of the forward arm, kept upright with its tip on the ground
                var cane = Node(armL, "Cane", new Vector3(0, -0.45f, 0), new Vector3(0, 0, StoopAngle - CaneArmAngle));
                Cyl(cane, H("#7A5230"), new Vector3(0, -0.41f, 0), 0.024f, 0.86f);
                if (elderHook == null) elderHook = MeshKit.Torus(0.06f, 0.024f, 10, 5, 180f);
                Part(cane, elderHook, H("#7A5230"), new Vector3(0.06f, 0.02f, 0), Vector3.one);
            }
            else
            {
                Ball(head, hair, new Vector3(-0.04f, hr + 0.1f, 0), new Vector3(0.58f, 0.4f, 0.58f));
                if (woman) Ball(head, hair, new Vector3(-0.13f, hr - 0.12f, 0), new Vector3(0.4f, 0.62f, 0.54f));
            }
            if (withBag && !elder && rnd.NextDouble() < 0.6)
            {
                var bag = Node(armR, "Bag", new Vector3(0, -0.6f, 0));
                Box(bag, Pick("#E4577E", "#F2C14E", "#8FC8F0", "#FFFFFF", "#9ED9C3"), new Vector3(0, -0.12f, 0), new Vector3(0.34f, 0.3f, 0.14f));
                Box(bag, H("#2A2230"), new Vector3(0, 0.07f, 0), new Vector3(0.2f, 0.08f, 0.02f));
            }
            pr.root = root; pr.body = body; pr.armL = armL; pr.armR = armR;
            return pr;
        }

        /// <summary>Bride: casual clothes (formal=false) or wedding dress (formal=true).</summary>
        public static Rig BuildBride(Transform parent, bool formal)
        {
            var rig = new Rig { isBride = true };
            Color hair = H("#3A2622");
            rig.root = Node(parent, formal ? "Bride_Formal" : "Bride_Casual", Vector3.zero);
            var facing = Node(rig.root, "Facing", Vector3.zero, new Vector3(0, 28, 0));
            rig.body = Node(facing, "Body", Vector3.zero);

            rig.hip = Node(rig.body, "Hip", new Vector3(0, 0.74f, 0));
            rig.legL = Node(rig.hip, "LegL", new Vector3(0.0f, 0, -0.1f));
            rig.legR = Node(rig.hip, "LegR", new Vector3(0.0f, 0, 0.1f));
            foreach (var leg in new[] { rig.legL, rig.legR })
            {
                if (!formal) Part(leg, MeshKit.Capsule, Skin, new Vector3(0, -0.36f, 0), new Vector3(0.14f, 0.34f, 0.14f));
                Box(leg, formal ? H("#F5E6EE") : Color.white, new Vector3(0.08f, -0.7f, 0), new Vector3(0.24f, 0.09f, 0.14f));
            }

            Color dressTop;
            rig.skirt = Node(rig.body, "Skirt", new Vector3(0, 0.84f, 0));
            if (formal)
            {
                Color dress = H("#FFFDFB"), lace = H("#FBE7EE");
                dressTop = dress;
                Frus(rig.skirt, dress, new Vector3(0, -0.8f, 0), 0.6f, 0.2f, 0.84f, new Vector3(0, 0, 0), 16);
                Frus(rig.skirt, lace, new Vector3(0, -0.8f, 0), 0.62f, 0.56f, 0.1f, default, 16);
                Part(rig.skirt, MeshKit.Torus(0.22f, 0.045f, 16, 6), H("#F7A8C4"), new Vector3(0, 0.0f, 0), Vector3.one, new Vector3(90, 0, 0));
            }
            else
            {
                // yellow pleated mini skirt + pink cardigan
                dressTop = H("#FF9EC4");
                Frus(rig.skirt, H("#FFD84D"), new Vector3(0, -0.4f, 0), 0.38f, 0.21f, 0.42f, default, 10);
            }

            var torso = Node(rig.body, "Torso", new Vector3(0, 0.84f, 0));
            Frus(torso, dressTop, Vector3.zero, formal ? 0.2f : 0.22f, formal ? 0.24f : 0.25f, 0.42f, default, 14);
            if (formal)
            {
                Box(torso, Skin, new Vector3(0, 0.45f, 0), new Vector3(0.22f, 0.1f, 0.34f));
                Ball(torso, Skin, new Vector3(0, 0.42f, 0), new Vector3(0.36f, 0.12f, 0.46f));
            }
            else
            {
                Box(torso, Color.white, new Vector3(0.2f, 0.26f, 0), new Vector3(0.06f, 0.32f, 0.14f));   // blouse under cardigan
                Box(torso, Skin, new Vector3(0, 0.44f, 0), new Vector3(0.2f, 0.08f, 0.3f));
            }

            rig.armL = Node(torso, "ArmL", new Vector3(0, 0.4f, -0.27f));
            rig.armR = Node(torso, "ArmR", new Vector3(0, 0.4f, 0.27f));
            foreach (var arm in new[] { rig.armL, rig.armR })
            {
                if (formal) Part(arm, MeshKit.Capsule, Skin, new Vector3(0, -0.22f, 0), new Vector3(0.12f, 0.24f, 0.12f));
                else
                {
                    Part(arm, MeshKit.Capsule, dressTop, new Vector3(0, -0.2f, 0), new Vector3(0.14f, 0.23f, 0.14f)); // cardigan sleeve
                    Ball(arm, Skin, new Vector3(0, -0.44f, 0), Vector3.one * 0.12f);
                }
            }
            rig.bouquet = Node(rig.armL, formal ? "Bouquet" : "BubbleTea", new Vector3(0.05f, -0.46f, 0));
            if (formal)
            {
                // a proper bridal bouquet: roses in a dome, leaves, ribbon and stems
                Color[] rose = { H("#FF4F7B"), H("#FFFFFF"), H("#FFB3C7"), H("#FF7FA0"), H("#FFF0F5"), H("#FF4F7B"), H("#FFD1DC") };
                Vector3[] at = { new Vector3(0, 0.1f, 0), new Vector3(0.11f, 0.05f, -0.08f), new Vector3(-0.11f, 0.05f, -0.08f), new Vector3(0.1f, 0.05f, 0.09f),
                                 new Vector3(-0.1f, 0.05f, 0.09f), new Vector3(0, 0.04f, -0.14f), new Vector3(0, 0.05f, 0.14f) };
                for (int i = 0; i < at.Length; i++)
                {
                    Ball(rig.bouquet, rose[i], at[i], Vector3.one * (i == 0 ? 0.17f : 0.14f));
                    Ball(rig.bouquet, Color.Lerp(rose[i], Color.black, 0.15f), at[i] + new Vector3(0.02f, 0.045f, -0.02f), Vector3.one * 0.06f); // rose centre
                }
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    Ball(rig.bouquet, H("#4E9A5B"), new Vector3(Mathf.Cos(a) * 0.17f, -0.02f, Mathf.Sin(a) * 0.17f), new Vector3(0.1f, 0.04f, 0.07f), new Vector3(0, -i * 60f, 20));
                }
                Frus(rig.bouquet, H("#5BAA6A"), new Vector3(0, -0.3f, 0), 0.035f, 0.07f, 0.28f, default, 8);
                Cyl(rig.bouquet, Color.white, new Vector3(0, -0.1f, 0), 0.075f, 0.07f);
                Box(rig.bouquet, Color.white, new Vector3(0.03f, -0.2f, -0.07f), new Vector3(0.04f, 0.16f, 0.02f), new Vector3(0, 0, 15));
                Box(rig.bouquet, Color.white, new Vector3(-0.03f, -0.2f, -0.07f), new Vector3(0.04f, 0.16f, 0.02f), new Vector3(0, 0, -15));
                rig.bouquet.localScale = Vector3.one * 1.25f;
            }
            else
            {
                // she carries a bubble tea
                Frus(rig.bouquet, H("#C99367"), new Vector3(0, -0.12f, 0), 0.07f, 0.09f, 0.24f, default, 10);
                Cyl(rig.bouquet, Color.white, new Vector3(0, 0.13f, 0), 0.095f, 0.02f);
                Cyl(rig.bouquet, H("#FF7FA8"), new Vector3(0.02f, 0.22f, 0), 0.015f, 0.18f, new Vector3(0, 0, -12));
            }

            rig.head = Node(torso, "Head", new Vector3(0, 0.48f, 0));
            float hr = 0.32f;
            if (formal)  // veil (semi-transparent) flowing behind
                Part(rig.head, MeshKit.Cube, new Color(1, 1, 1, 0.55f), new Vector3(-0.32f, hr - 0.1f, 0), new Vector3(0.05f, 0.95f, 0.62f), new Vector3(0, 0, -28), true);
            Ball(rig.head, Skin, new Vector3(0, hr, 0), Vector3.one * hr * 2);
            var face = Node(rig.head, "Face", new Vector3(0, hr, 0));
            Face(face, hr, hair, true);
            Ball(rig.head, hair, new Vector3(-0.05f, hr + 0.1f, 0), new Vector3(0.7f, 0.52f, 0.7f));
            Ball(rig.head, hair, new Vector3(0.14f, hr + 0.2f, -0.06f), new Vector3(0.34f, 0.16f, 0.5f), new Vector3(0, 0, -25));
            if (formal)
            {
                Ball(rig.head, hair, new Vector3(-0.15f, hr - 0.08f, 0), new Vector3(0.48f, 0.56f, 0.66f));
                Ball(rig.head, hair, new Vector3(-0.28f, hr + 0.2f, 0), Vector3.one * 0.3f);                  // bun
                Part(rig.head, MeshKit.Torus(0.17f, 0.03f, 16, 5, 180f), Palette.Gold, new Vector3(0.02f, hr + 0.3f, 0), Vector3.one, new Vector3(0, 90, 0));
                Ball(rig.head, H("#BFF3FF"), new Vector3(0.02f, hr + 0.47f, 0), Vector3.one * 0.07f, default, true);
                Ball(rig.head, H("#FF8FB1"), new Vector3(-0.2f, hr + 0.28f, -0.22f), Vector3.one * 0.1f);   // hair flower
            }
            else
            {
                // long hair down + a little hair clip
                Ball(rig.head, hair, new Vector3(-0.18f, hr - 0.2f, 0), new Vector3(0.46f, 0.8f, 0.66f));
                Box(rig.head, H("#FF8FB1"), new Vector3(0.1f, hr + 0.24f, -0.27f), new Vector3(0.12f, 0.04f, 0.04f), new Vector3(0, 0, -20));
            }
            return rig;
        }
    }
}
