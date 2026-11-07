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
                    Ball(r, H("#E59A2F"), new Vector3(0, -0.08f, 0), new Vector3(0.9f, 0.45f, 0.9f));
                    Ball(r, H("#F8C65E"), new Vector3(0, 0.0f, 0), new Vector3(0.84f, 0.42f, 0.84f));
                    for (int i = -1; i <= 1; i++)
                    {
                        Box(r, H("#D98A22"), new Vector3(i * 0.2f, 0.17f, -0.05f), new Vector3(0.03f, 0.06f, 0.7f), new Vector3(0, 45, 0));
                        Box(r, H("#D98A22"), new Vector3(i * 0.2f, 0.17f, -0.05f), new Vector3(0.03f, 0.06f, 0.7f), new Vector3(0, -45, 0));
                    }
                    Box(r, H("#FFF0A0"), new Vector3(0.05f, -0.05f, -0.3f), new Vector3(0.5f, 0.12f, 0.18f), new Vector3(0, 0, 8)); // butter: 菠蘿油
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

                case ModelId.Shield:
                    Cyl(r, gold, Vector3.zero, 0.42f, 0.14f, new Vector3(90, 0, 0));
                    Cyl(r, red, new Vector3(0, 0, -0.03f), 0.34f, 0.12f, new Vector3(90, 0, 0));
                    Text3D(r, "囍", gold, new Vector3(0, 0.01f, -0.1f), 3.0f);
                    Part(r, MeshKit.Torus(0.58f, 0.03f, 28, 5), H("#FFE9A8"), Vector3.zero, Vector3.one, default, true);
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
            Part(rig.legL, MeshKit.Capsule, pants, new Vector3(0, -0.34f, 0), new Vector3(0.21f, 0.37f, 0.21f));
            Box(rig.legL, shoe, new Vector3(0.06f, -0.7f, 0), new Vector3(0.3f, 0.1f, 0.18f));
            rig.legR = Node(rig.hip, "LegR", new Vector3(0, 0, 0.13f));
            Part(rig.legR, MeshKit.Capsule, pants, new Vector3(0, -0.34f, 0), new Vector3(0.21f, 0.37f, 0.21f));
            Box(rig.legR, shoe, new Vector3(0.06f, -0.7f, 0), new Vector3(0.3f, 0.1f, 0.18f));
            if (!formal)
            {
                Box(rig.legL, H("#E5484D"), new Vector3(0.06f, -0.66f, -0.092f), new Vector3(0.2f, 0.03f, 0.01f), default, true);
                Box(rig.legR, H("#E5484D"), new Vector3(0.06f, -0.66f, -0.092f), new Vector3(0.2f, 0.03f, 0.01f), default, true);
            }

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
                // pleated mini skirt + cardigan
                dressTop = H("#FFE08A");
                Frus(rig.skirt, H("#8FD3C2"), new Vector3(0, -0.4f, 0), 0.38f, 0.21f, 0.42f, default, 10);
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
                Ball(rig.bouquet, H("#FF6F91"), new Vector3(0, 0.05f, 0), Vector3.one * 0.15f);
                Ball(rig.bouquet, H("#FFFFFF"), new Vector3(0.08f, 0.02f, -0.06f), Vector3.one * 0.12f);
                Ball(rig.bouquet, H("#FFC94D"), new Vector3(-0.07f, 0.03f, -0.07f), Vector3.one * 0.11f);
                Ball(rig.bouquet, H("#FF9EB8"), new Vector3(0.0f, 0.08f, -0.1f), Vector3.one * 0.11f);
                Frus(rig.bouquet, H("#5BAA6A"), new Vector3(0, -0.22f, 0), 0.03f, 0.08f, 0.22f, default, 8);
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
