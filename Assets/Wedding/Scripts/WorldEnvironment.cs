using System.Collections.Generic;
using UnityEngine;
using static WeddingRun.ModelFactory;

namespace WeddingRun
{
    /// <summary>
    /// Sky, road, parallax city skylines and particles. Each stage of the journey has its own skyline
    /// (香港 → 金門 → 台北 → 名古屋 → 婚禮) that rises up while the previous one sinks away.
    /// </summary>
    public class WorldEnvironment : MonoBehaviour
    {
        class Strip
        {
            public Transform root;
            public float factor, width, baseY, y, targetY;
            public bool dying;
        }

        Camera cam;
        Light sun;
        Mesh skyMesh;
        Transform skyQuad, stars, sunDisc;
        Material roadMat, curbMat, farGroundMat, midGroundMat, lineMat, cloudMat, sunMat, starMat;

        /// <summary>A fog-free sphere for things that live far away in the sky.</summary>
        static Transform SkyPart(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = Part(parent, MeshKit.WithVertexColors(MeshKit.Sphere), Color.white, pos, scale, default, true);
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }
        readonly List<Strip> strips = new List<Strip>();
        Strip roadLines, clouds;
        ParticleSystem petals, sparkle, fireworks;
        Texture2D petalTex, dotTex;

        int stage = -1;
        StageDef from, to;
        float blend = 1;
        float fireworkTimer;

        public void Build(Camera camera, Light light)
        {
            cam = camera; sun = light;
            petalTex = MakeBlobTexture(32, 0.55f);
            dotTex = MakeBlobTexture(32, 1f);

            // ---- sky gradient (child of camera so it always fills the view)
            skyMesh = MeshKit.GradientQuad(Color.blue, Color.white);
            skyQuad = new GameObject("Sky").transform;
            skyQuad.SetParent(cam.transform, false);
            skyQuad.localPosition = new Vector3(0, 0, cam.farClipPlane * 0.9f);
            skyQuad.localScale = new Vector3(cam.farClipPlane * 4f, cam.farClipPlane * 2.2f, 1);
            skyQuad.gameObject.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var sr = skyQuad.gameObject.AddComponent<MeshRenderer>();
            sr.sharedMaterial = new Material(VertexMaterial) { renderQueue = 1000 };
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;

            stars = new GameObject("Stars").transform;
            stars.SetParent(cam.transform, false);
            stars.localPosition = new Vector3(0, 0, cam.farClipPlane * 0.85f);
            var rnd = new System.Random(7);
            starMat = new Material(VertexMaterial) { color = new Color(1, 0.96f, 0.85f), renderQueue = 1001 };
            sunMat = new Material(VertexMaterial) { color = Color.white, renderQueue = 1001 };
            for (int i = 0; i < 90; i++)
            {
                float x = (float)(rnd.NextDouble() - 0.5) * cam.farClipPlane * 2.6f;
                float y = (float)(rnd.NextDouble() * 0.5 + 0.05) * cam.farClipPlane * 0.8f;
                float s = (float)(rnd.NextDouble() * 2 + 1.2);
                SkyPart(stars, new Vector3(x, y, 0), Vector3.one * s, starMat);
            }
            MeshKit.Merge(stars, false);

            sunDisc = SkyPart(cam.transform, new Vector3(cam.farClipPlane * 0.25f, cam.farClipPlane * 0.22f, cam.farClipPlane * 0.8f), Vector3.one * 50f, sunMat);

            // ---- ground
            var groundRoot = new GameObject("Ground").transform;
            groundRoot.SetParent(transform, false);
            roadMat = new Material(Mat(Color.gray));
            curbMat = new Material(Mat(Color.white));
            farGroundMat = new Material(Mat(Color.green));
            midGroundMat = new Material(Mat(Color.gray));
            lineMat = new Material(Mat(Color.white, true));
            Box(groundRoot, Color.gray, new Vector3(0, -0.5f, -0.4f), new Vector3(400, 1, 6)).GetComponent<MeshRenderer>().sharedMaterial = roadMat;
            Box(groundRoot, Color.white, new Vector3(0, -0.38f, 3.8f), new Vector3(400, 1, 2.4f)).GetComponent<MeshRenderer>().sharedMaterial = curbMat;
            Box(groundRoot, Color.white, new Vector3(0, -0.45f, -23.4f), new Vector3(400, 1, 40f)).GetComponent<MeshRenderer>().sharedMaterial = curbMat;
            Box(groundRoot, Color.gray, new Vector3(0, -0.5f, 12.5f), new Vector3(600, 1, 15f)).GetComponent<MeshRenderer>().sharedMaterial = midGroundMat;
            Box(groundRoot, Color.green, new Vector3(0, -0.53f, 150f), new Vector3(1200, 1, 290)).GetComponent<MeshRenderer>().sharedMaterial = farGroundMat;

            // road dashes (scroll at full speed)
            roadLines = MakeStrip("RoadLines", 0f, 1f, 48f, (p, r, w) =>
            {
                for (float x = -w / 2; x < w / 2; x += 4f)
                    Box(p, Color.white, new Vector3(x + 1, 0.005f, -2.2f), new Vector3(2f, 0.02f, 0.18f), default, true);
                for (float x = -w / 2; x < w / 2; x += 3f)
                    Box(p, Color.white, new Vector3(x + 1, 0.11f, 2.66f), new Vector3(2.9f, 0.04f, 0.12f), default, true);
            }, false);
            foreach (var mr in roadLines.root.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = lineMat;

            // clouds (very slow, shared by all stages)
            cloudMat = new Material(VertexMaterial) { color = Color.white, renderQueue = 1002 };
            clouds = MakeStrip("Clouds", 160f, 0.04f, 360f, (p, r, w) =>
            {
                for (int i = 0; i < 9; i++)
                {
                    float x = -w / 2 + i * w / 9 + (float)r.NextDouble() * 10;
                    float y = 18 + (float)r.NextDouble() * 22;
                    float s = 6 + (float)r.NextDouble() * 6;
                    for (int k = 0; k < 4; k++)
                        SkyPart(p, new Vector3(x + (k - 1.5f) * s * 0.55f, y + (k % 2) * s * 0.2f, 0), new Vector3(s, s * 0.55f, s * 0.6f), cloudMat);
                }
            }, false);

            BuildParticles();
        }

        // ------------------------------------------------------------------ stage switching

        /// <summary>Switch to a visual theme (0 HK, 1 Kinmen, 2 Taipei, 3 Nagoya, 4 Wedding).</summary>
        public void SetStage(int index, bool instant)
        {
            if (index == stage) return;
            var def = Stages.ByTheme(index);
            from = stage >= 0 && !instant ? CurrentBlend() : def;
            to = def;
            blend = instant ? 1 : 0;
            stage = index;

            foreach (var s in strips)
                if (s != roadLines && s != clouds && !s.dying)
                {
                    s.dying = true;
                    s.targetY = s.baseY - (s.factor < 0.3f ? 45f : 14f);
                    if (instant) s.y = s.targetY;
                }

            var far = MakeStrip("Far_" + def.nameEn, 70f, 0.12f, 180f, FarBuilder(index), true);
            var mid = MakeStrip("Mid_" + def.nameEn, 11f, 0.55f, 72f, MidBuilder(index), true);
            foreach (var s in new[] { far, mid })
            {
                s.targetY = s.baseY;
                s.y = instant ? s.baseY : s.baseY - (s.factor < 0.3f ? 45f : 14f);
                Place(s);
            }

            var pm = petals.main;
            pm.startColor = new ParticleSystem.MinMaxGradient(def.petalA, def.petalB);
            if (instant) ApplyColors(def);
        }

        StageDef current; // blended snapshot used when a transition is interrupted

        StageDef CurrentBlend() => current ?? to;

        void ApplyColors(StageDef d)
        {
            current = d;
            MeshKit.GradientQuad(d.skyTop, d.skyBottom, skyMesh);
            cam.backgroundColor = d.skyBottom;
            RenderSettings.fogColor = d.fog;
            RenderSettings.ambientLight = d.ambient;
            roadMat.color = d.road;
            curbMat.color = d.curb;
            farGroundMat.color = d.farGround;
            midGroundMat.color = d.midGround;
            lineMat.color = d.roadLine;
            cloudMat.color = Color.Lerp(Color.white, d.skyBottom, 0.25f);
            sun.color = d.light;
            sun.intensity = d.lightIntensity;
            bool night = d.theme == 0 || d.theme == 4;
            sunMat.color = night ? new Color(1f, 0.97f, 0.88f) : new Color(1f, 0.93f, 0.7f);
            stars.gameObject.SetActive(night);
        }

        static StageDef Lerp(StageDef a, StageDef b, float t)
        {
            return new StageDef
            {
                theme = t < 0.5f ? a.theme : b.theme,
                skyTop = Color.Lerp(a.skyTop, b.skyTop, t),
                skyBottom = Color.Lerp(a.skyBottom, b.skyBottom, t),
                fog = Color.Lerp(a.fog, b.fog, t),
                ambient = Color.Lerp(a.ambient, b.ambient, t),
                road = Color.Lerp(a.road, b.road, t),
                curb = Color.Lerp(a.curb, b.curb, t),
                farGround = Color.Lerp(a.farGround, b.farGround, t),
                midGround = Color.Lerp(a.midGround, b.midGround, t),
                roadLine = Color.Lerp(a.roadLine, b.roadLine, t),
                light = Color.Lerp(a.light, b.light, t),
                lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t),
            };
        }

        // ------------------------------------------------------------------ per-frame

        public void Tick(float dt, float speed)
        {
            if (blend < 1)
            {
                blend = Mathf.Min(1, blend + dt / 2.2f);
                ApplyColors(Lerp(from, to, Mathf.SmoothStep(0, 1, blend)));
            }

            for (int i = strips.Count - 1; i >= 0; i--)
            {
                var s = strips[i];
                s.root.localPosition += Vector3.left * speed * s.factor * dt;
                if (s.root.localPosition.x <= -s.width) s.root.localPosition += Vector3.right * s.width;
                s.y = Mathf.MoveTowards(s.y, s.targetY, dt * (s.factor < 0.3f ? 30f : 10f));
                Place(s);
                if (s.dying && Mathf.Approximately(s.y, s.targetY))
                {
                    Destroy(s.root.gameObject);
                    strips.RemoveAt(i);
                }
            }

            // keep particle emitter in view
            var shape = petals.shape;
            shape.position = cam.transform.position + new Vector3(10, 9, 14);

            if (stage == 4)
            {
                fireworkTimer -= dt;
                if (fireworkTimer <= 0)
                {
                    fireworkTimer = Random.Range(0.6f, 1.6f);
                    Firework(cam.transform.position + new Vector3(Random.Range(-30f, 40f), Random.Range(14f, 26f), 70f));
                }
            }
        }

        static void Place(Strip s)
        {
            var p = s.root.localPosition;
            s.root.localPosition = new Vector3(p.x, s.y, p.z);
        }

        Strip MakeStrip(string name, float z, float factor, float width, System.Action<Transform, System.Random, float> build, bool merge)
        {
            var s = new Strip { factor = factor, width = width };
            s.root = new GameObject(name).transform;
            s.root.SetParent(transform, false);
            s.root.localPosition = new Vector3(0, 0, z);
            var a = new GameObject("A").transform;
            a.SetParent(s.root, false);
            build(a, new System.Random(name.GetHashCode()), width);
            if (merge) MeshKit.Merge(a, factor > 0.3f);
            var b = Instantiate(a.gameObject, s.root).transform;
            b.name = "B";
            b.localPosition = new Vector3(width, 0, 0);
            strips.Add(s);
            return s;
        }

        // ------------------------------------------------------------------ particles

        static Texture2D MakeBlobTexture(int size, float aspect)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = ((y + 0.5f) / size * 2 - 1) / aspect;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1 - d) * 4)));
                }
            tex.Apply();
            return tex;
        }

        ParticleSystem MakePS(string name, Texture2D tex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = new Material(VertexMaterial) { mainTexture = tex };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            return ps;
        }

        void BuildParticles()
        {
            petals = MakePS("Petals", petalTex);
            var m = petals.main;
            m.loop = true; m.startLifetime = 7f; m.startSpeed = 0f; m.maxParticles = 300;
            m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var em = petals.emission; em.rateOverTime = 14;
            var sh = petals.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(44, 1, 18);
            var vel = petals.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-5f, -2.5f); vel.y = new ParticleSystem.MinMaxCurve(-1.6f, -0.8f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var rot = petals.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var noise = petals.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.4f;
            petals.Play();

            sparkle = MakePS("Sparkle", dotTex);
            m = sparkle.main;
            m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f); m.maxParticles = 600; m.gravityModifier = 0.6f;
            em = sparkle.emission; em.rateOverTime = 0;
            sh = sparkle.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var sz = sparkle.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            sparkle.Play();

            fireworks = MakePS("Fireworks", dotTex);
            m = fireworks.main;
            m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(8f, 12f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.0f); m.maxParticles = 1500; m.gravityModifier = 0.35f;
            em = fireworks.emission; em.rateOverTime = 0;
            sh = fireworks.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            sz = fireworks.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var drag = fireworks.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.5f;
            fireworks.Play();
        }

        public void Burst(Vector3 pos, Color c, int count, float speedScale = 1f)
        {
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                ep.velocity = Random.onUnitSphere * Random.Range(3f, 7f) * speedScale + Vector3.up * 2f;
                ep.startColor = Color.Lerp(c, Color.white, Random.Range(0f, 0.5f));
                sparkle.Emit(ep, 1);
            }
        }

        public void Firework(Vector3 pos)
        {
            Color[] cols = { Palette.Gold, Palette.Rose, new Color(1f, 0.6f, 0.75f), new Color(0.7f, 0.85f, 1f), Color.white };
            Color c = cols[Random.Range(0, cols.Length)];
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < 70; i++)
            {
                ep.position = pos;
                ep.velocity = Random.onUnitSphere * Random.Range(9f, 12f);
                ep.startColor = Color.Lerp(c, Color.white, Random.Range(0f, 0.3f));
                fireworks.Emit(ep, 1);
            }
        }

        public void Confetti(Vector3 pos)
        {
            Color[] cols = { Palette.Gold, Palette.Rose, new Color(0.6f, 0.85f, 1f), new Color(0.65f, 0.9f, 0.7f), Color.white };
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < 90; i++)
            {
                ep.position = pos;
                ep.velocity = new Vector3(Random.Range(-6f, 6f), Random.Range(6f, 13f), Random.Range(-2f, 2f));
                ep.startColor = cols[Random.Range(0, cols.Length)];
                ep.startSize = Random.Range(0.2f, 0.35f);
                ep.startLifetime = Random.Range(1.2f, 2f);
                sparkle.Emit(ep, 1);
            }
        }

        // ------------------------------------------------------------------ skylines (far layer, z≈70)

        System.Action<Transform, System.Random, float> FarBuilder(int i)
        {
            switch (i)
            {
                case 0: return FarHongKong;
                case 1: return FarKinmen;
                case 2: return FarTaipei;
                case 3: return FarNagoya;
                default: return FarWedding;
            }
        }

        static float Rf(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static T Pick<T>(System.Random r, T[] arr) => arr[r.Next(arr.Length)];

        static void Windows(Transform p, System.Random r, Vector3 center, Vector3 size, float density, Color[] lit, float cell = 1.4f)
        {
            int cols = Mathf.Max(1, Mathf.FloorToInt(size.x / cell));
            int rows = Mathf.Max(1, Mathf.FloorToInt((size.y - 1.5f) / cell));
            float z = center.z - size.z / 2 - 0.02f;
            for (int cx = 0; cx < cols; cx++)
                for (int cy = 0; cy < rows; cy++)
                {
                    if (r.NextDouble() > density) continue;
                    float x = center.x - size.x / 2 + (cx + 0.5f) * size.x / cols;
                    float y = center.y - size.y / 2 + 1f + cy * cell + cell * 0.5f;
                    Box(p, Pick(r, lit), new Vector3(x, y, z), new Vector3(cell * 0.45f, cell * 0.55f, 0.05f), default, true);
                }
        }

        static void Hills(Transform p, System.Random r, float w, Color a, Color b, float zBack, float hMin, float hMax)
        {
            for (float x = -w / 2 - 10; x < w / 2 + 10; x += Rf(r, 14, 24))
            {
                float h = Rf(r, hMin, hMax);
                Ball(p, r.NextDouble() < 0.5 ? a : b, new Vector3(x, -2, zBack + Rf(r, -4, 4)), new Vector3(Rf(r, 28, 44), h * 2, 16));
            }
        }

        void FarHongKong(Transform p, System.Random r, float w)
        {
            Color[] body = { H("#3B3466"), H("#463C78"), H("#2E2A55"), H("#55488A"), H("#3A4573") };
            Color[] lit = { H("#FFD98A"), H("#FFB3D9"), H("#9FE8FF"), H("#FFF1C9") };
            Hills(p, r, w, H("#2B2D57"), H("#323464"), 22, 10, 17); // the Peak behind the city
            for (float x = -w / 2; x < w / 2;)
            {
                float bw = Rf(r, 3f, 6.5f), h = Rf(r, 7f, 20f), d = Rf(r, 3f, 6f), z = Rf(r, -3f, 6f);
                var c = new Vector3(x + bw / 2, h / 2, z);
                Box(p, Pick(r, body), c, new Vector3(bw, h, d));
                Windows(p, r, c, new Vector3(bw, h, d), 0.42f, lit);
                if (r.NextDouble() < 0.18) Box(p, Pick(r, lit), new Vector3(c.x, h + 0.5f, z), new Vector3(bw * 0.6f, 1f, 0.3f), default, true); // rooftop sign
                x += bw + Rf(r, 0.2f, 1.4f);
            }
            // IFC-like stepped tower
            float ix = 18;
            Box(p, H("#5C6AA0"), new Vector3(ix, 12, -5), new Vector3(5, 24, 5));
            Box(p, H("#6676AE"), new Vector3(ix, 25.5f, -5), new Vector3(4, 3, 4));
            Box(p, H("#7584BC"), new Vector3(ix, 27.6f, -5), new Vector3(3, 1.4f, 3));
            for (int k = 0; k < 4; k++) Box(p, H("#9FB0E6"), new Vector3(ix - 1.5f + k, 28.8f, -5), new Vector3(0.3f, 1.6f, 0.3f), default, true);
            Windows(p, r, new Vector3(ix, 12, -5), new Vector3(5, 24, 5), 0.5f, lit, 1.2f);
            // ICC-like slim tower
            Frus(p, H("#4E5A8E"), new Vector3(-40, 0, -4), 3.2f, 2.6f, 26f, new Vector3(0, 45, 0), 4);
            Windows(p, r, new Vector3(-40, 13, -6), new Vector3(3.6f, 26, 1), 0.5f, lit, 1.2f);
            // harbour lights strip
            for (float x = -w / 2; x < w / 2; x += 1.6f)
                Box(p, Pick(r, lit), new Vector3(x, 0.15f, -9), new Vector3(0.8f, 0.1f, 0.1f), default, true);
        }

        void FarKinmen(Transform p, System.Random r, float w)
        {
            Hills(p, r, w, H("#5FAE6E"), H("#6FBB78"), 10, 5, 9);
            // 莒光樓 inspired pavilion
            var t = Node(p, "Pavilion", new Vector3(0, 0, -6));
            Box(t, H("#E8DCC6"), new Vector3(0, 1.5f, 0), new Vector3(10, 3, 6));
            Box(t, H("#C9B79A"), new Vector3(0, 3.2f, 0), new Vector3(11, 0.4f, 7));
            for (int k = 0; k < 3; k++)
            {
                float y = 3.4f + k * 3.6f, s = 1 - k * 0.22f;
                Box(t, H("#F2E8D5"), new Vector3(0, y + 1.3f, 0), new Vector3(7 * s, 2.6f, 4.6f * s));
                for (int c = -2; c <= 2; c++) Box(t, H("#C0392B"), new Vector3(c * 1.5f * s, y + 1.3f, -2.35f * s), new Vector3(0.3f, 2.6f, 0.2f));
                Frus(t, H("#E07B39"), new Vector3(0, y + 2.6f, 0), 6.2f * s, 3.2f * s, 1.3f, new Vector3(0, 45, 0), 4);
            }
            Ball(t, Palette.Gold, new Vector3(0, 15.4f, 0), Vector3.one * 0.8f);
            // little houses and trees on the hills
            for (float x = -w / 2; x < w / 2; x += Rf(r, 6, 12))
            {
                if (Mathf.Abs(x) < 9) continue;
                float y = Rf(r, 0, 2.5f), z = Rf(r, -2, 6);
                Box(p, H("#C4573F"), new Vector3(x, y + 0.9f, z), new Vector3(3, 1.8f, 2));
                Part(p, MeshKit.Prism(), H("#9E3B2C"), new Vector3(x, y + 1.8f, z), new Vector3(3.4f, 1.1f, 2.4f));
                Ball(p, H("#3F8F55"), new Vector3(x + 3, y + 2, z + 1), new Vector3(2.4f, 3f, 2.4f));
            }
        }

        void FarTaipei(Transform p, System.Random r, float w)
        {
            Hills(p, r, w, H("#5B6F8A"), H("#6A7C96"), 24, 12, 18); // Yangmingshan ranges
            Color[] body = { H("#B9A9A2"), H("#A6979A"), H("#C8B6A6"), H("#8E8592"), H("#9F9AAE") };
            Color[] lit = { H("#FFE2A8"), H("#FFF4D6"), H("#FFC48A") };
            for (float x = -w / 2; x < w / 2;)
            {
                float bw = Rf(r, 3.5f, 7f), h = Rf(r, 4f, 13f), d = Rf(r, 3f, 6f), z = Rf(r, -2f, 6f);
                if (Mathf.Abs(x) < 7) { x += 2; continue; }
                var c = new Vector3(x + bw / 2, h / 2, z);
                Box(p, Pick(r, body), c, new Vector3(bw, h, d));
                Windows(p, r, c, new Vector3(bw, h, d), 0.3f, lit);
                x += bw + Rf(r, 0.4f, 2f);
            }
            // Taipei 101 inspired tower
            var t = Node(p, "Tower101", new Vector3(0, 0, -6));
            Color glass = H("#5E9897"), glassDark = H("#4C8584");
            Frus(t, glassDark, Vector3.zero, 4.2f, 3.0f, 5f, new Vector3(0, 45, 0), 4);
            for (int k = 0; k < 8; k++)
            {
                Frus(t, k % 2 == 0 ? glass : glassDark, new Vector3(0, 5 + k * 2.2f, 0), 2.3f, 3.0f, 2.2f, new Vector3(0, 45, 0), 4);
                Box(t, H("#FFE7A8"), new Vector3(0, 5 + k * 2.2f + 1.1f, -2.05f), new Vector3(1.8f, 0.12f, 0.05f), default, true);
            }
            Box(t, glassDark, new Vector3(0, 23.4f, 0), new Vector3(2.2f, 1.2f, 2.2f));
            Box(t, glass, new Vector3(0, 24.6f, 0), new Vector3(1.4f, 1.2f, 1.4f));
            Cyl(t, H("#C9D3D6"), new Vector3(0, 27.2f, 0), 0.18f, 4f);
        }

        void FarNagoya(Transform p, System.Random r, float w)
        {
            Hills(p, r, w, H("#9CC9A0"), H("#F4BFD0"), 18, 4, 7);
            Color[] body = { H("#E8E4EE"), H("#D9D4E2"), H("#F1EAE4"), H("#C9D3E0"), H("#E6DCD2") };
            for (float x = -w / 2; x < w / 2;)
            {
                float bw = Rf(r, 3.5f, 6.5f), h = Rf(r, 3.5f, 11f), d = Rf(r, 3f, 6f), z = Rf(r, 0f, 8f);
                if (Mathf.Abs(x) < 10) { x += 2; continue; }
                var c = new Vector3(x + bw / 2, h / 2, z);
                Box(p, Pick(r, body), c, new Vector3(bw, h, d));
                Windows(p, r, c, new Vector3(bw, h, d), 0.35f, new[] { H("#9DB7D6"), H("#B8CBE2") });
                x += bw + Rf(r, 0.5f, 2f);
            }
            // Nagoya Castle inspired keep
            var t = Node(p, "Castle", new Vector3(0, 0, -6));
            Frus(t, H("#A8A29A"), Vector3.zero, 7.5f, 6.2f, 3.5f, new Vector3(0, 45, 0), 4);
            float y = 3.5f; float[] sz = { 7.4f, 5.8f, 4.4f, 3.2f };
            for (int k = 0; k < sz.Length; k++)
            {
                Box(t, H("#FBFAF6"), new Vector3(0, y + 1.1f, 0), new Vector3(sz[k], 2.2f, sz[k] * 0.8f));
                for (int c = -1; c <= 1; c++) Box(t, H("#4A4A55"), new Vector3(c * sz[k] * 0.28f, y + 1.2f, -sz[k] * 0.4f - 0.02f), new Vector3(0.5f, 0.6f, 0.05f), default, true);
                Frus(t, H("#5E9E8A"), new Vector3(0, y + 2.2f, 0), sz[k] * 0.82f, sz[k] * 0.42f, 1.1f, new Vector3(0, 45, 0), 4);
                y += 3.3f;
            }
            Part(t, MeshKit.Prism(), H("#5E9E8A"), new Vector3(0, y - 1.1f, 0), new Vector3(2.4f, 1.4f, 1.8f));
            // golden shachihoko
            Part(t, MeshKit.Capsule, Palette.Gold, new Vector3(-1.1f, y + 0.6f, 0), new Vector3(0.35f, 0.5f, 0.35f), new Vector3(0, 0, -30));
            Part(t, MeshKit.Capsule, Palette.Gold, new Vector3(1.1f, y + 0.6f, 0), new Vector3(0.35f, 0.5f, 0.35f), new Vector3(0, 0, 30));
            // TV tower
            var tv = Node(p, "TVTower", new Vector3(-42, 0, -2));
            Frus(tv, H("#C97D4E"), Vector3.zero, 3.2f, 0.6f, 20f, new Vector3(0, 45, 0), 4);
            Box(tv, H("#8C8FA0"), new Vector3(0, 11f, 0), new Vector3(3.6f, 1.4f, 3.6f));
            Box(tv, H("#8C8FA0"), new Vector3(0, 15f, 0), new Vector3(2.2f, 0.8f, 2.2f));
            Cyl(tv, H("#E05A4A"), new Vector3(0, 22f, 0), 0.15f, 4f);
            // twin towers at the station
            var jr = Node(p, "Twin", new Vector3(40, 0, 0));
            Cyl(jr, H("#B8C6D6"), new Vector3(-2, 11, 0), 2.4f, 22f);
            Cyl(jr, H("#C9D4E0"), new Vector3(2.2f, 9.5f, 0), 2.1f, 19f);
            Box(jr, H("#A9B6C6"), new Vector3(0, 2.5f, 0), new Vector3(9, 5, 5));
        }

        void FarWedding(Transform p, System.Random r, float w)
        {
            Hills(p, r, w, H("#6FA87A"), H("#7DB585"), 12, 4, 8);
            // chapel
            var c = Node(p, "Chapel", new Vector3(0, 0, -6));
            Box(c, H("#FFF8F2"), new Vector3(0, 3, 0), new Vector3(9, 6, 6));
            Part(c, MeshKit.Prism(), H("#E88AA6"), new Vector3(0, 6, 0), new Vector3(9.6f, 3.2f, 6.6f));
            Box(c, H("#FFF8F2"), new Vector3(-6, 5, 0), new Vector3(3, 10, 3));
            Frus(c, H("#E88AA6"), new Vector3(-6, 10, 0), 2.4f, 0.01f, 3.4f, new Vector3(0, 45, 0), 4);
            Ball(c, Palette.Gold, new Vector3(-6, 8, -1.4f), Vector3.one * 1.1f);
            Part(c, MeshKit.Heart(0.3f), H("#FF5C8A"), new Vector3(-6, 14.3f, 0), Vector3.one * 1.6f);
            Cyl(c, H("#FFB8CB"), new Vector3(0, 4.2f, -3.02f), 1.1f, 0.05f, new Vector3(90, 0, 0), true);
            Box(c, H("#B97A57"), new Vector3(0, 1.3f, -3.02f), new Vector3(1.8f, 2.6f, 0.05f));
            for (int k = -1; k <= 1; k += 2) Box(c, H("#FFD3DF"), new Vector3(k * 3, 3.2f, -3.02f), new Vector3(1f, 2f, 0.05f), default, true);
            // heart hot-air balloons
            Color[] bc = { H("#FF6F91"), H("#FFC75F"), H("#C9A7FF"), H("#7FD1C7") };
            for (int k = 0; k < 5; k++)
            {
                float x = -w / 2 + 15 + k * (w / 5) + Rf(r, -5, 5);
                if (Mathf.Abs(x) < 10) x += 16;
                float y = Rf(r, 14, 22);
                Part(p, MeshKit.Heart(0.6f), bc[k % bc.Length], new Vector3(x, y, Rf(r, -4, 8)), Vector3.one * Rf(r, 3.5f, 5f), new Vector3(180, 0, 0));
                Box(p, H("#9C6B4E"), new Vector3(x, y - 3.4f, 0), new Vector3(0.9f, 0.7f, 0.9f));
            }
            // little houses
            for (float x = -w / 2; x < w / 2; x += Rf(r, 7, 13))
            {
                if (Mathf.Abs(x) < 12) continue;
                float z = Rf(r, 2, 8);
                Box(p, H("#FFF1E6"), new Vector3(x, 1.2f, z), new Vector3(3, 2.4f, 2.4f));
                Part(p, MeshKit.Prism(), H("#F2A7B8"), new Vector3(x, 2.4f, z), new Vector3(3.4f, 1.4f, 2.8f));
            }
        }

        // ------------------------------------------------------------------ street props (mid layer, z≈11)

        System.Action<Transform, System.Random, float> MidBuilder(int i)
        {
            switch (i)
            {
                case 0: return MidHongKong;
                case 1: return MidKinmen;
                case 2: return MidTaipei;
                case 3: return MidNagoya;
                default: return MidWedding;
            }
        }

        static void Lamp(Transform p, float x, Color pole, Color light)
        {
            Cyl(p, pole, new Vector3(x, 2.5f, 0), 0.1f, 5f);
            Box(p, pole, new Vector3(x + 0.5f, 4.95f, 0), new Vector3(1.1f, 0.12f, 0.12f));
            Ball(p, light, new Vector3(x + 0.95f, 4.75f, 0), new Vector3(0.45f, 0.3f, 0.45f), default, true);
        }

        static void Tree(Transform p, float x, float z, Color a, Color b, float s = 1f)
        {
            Cyl(p, H("#7A5236"), new Vector3(x, 1.2f * s, z), 0.22f * s, 2.4f * s);
            Ball(p, a, new Vector3(x, 3.0f * s, z), new Vector3(2.6f, 2.2f, 2.4f) * s);
            Ball(p, b, new Vector3(x - 0.8f * s, 2.6f * s, z - 0.4f), new Vector3(1.7f, 1.5f, 1.6f) * s);
            Ball(p, b, new Vector3(x + 0.9f * s, 2.7f * s, z - 0.3f), new Vector3(1.6f, 1.4f, 1.5f) * s);
        }

        void MidHongKong(Transform p, System.Random r, float w)
        {
            string[] signs = { "茶餐廳", "囍", "酒家", "金鋪", "涼茶", "大排檔" };
            Color[] neon = { H("#FF4F9A"), H("#4FE3FF"), H("#FFD23F"), H("#9B7BFF"), H("#FF6B4A") };
            int k = 0;
            for (float x = -w / 2; x < w / 2; x += 9f)
            {
                Lamp(p, x, H("#3A3550"), H("#FFE6A8"));
                if (k % 2 == 0)
                {
                    float sx = x + 4.5f;
                    Color nc = neon[(k / 2) % neon.Length];
                    string text = signs[(k / 2) % signs.Length];
                    Box(p, H("#2A2440"), new Vector3(sx, 6.2f, 0.3f), new Vector3(1.6f, text.Length * 1.15f + 0.6f, 0.4f));
                    Box(p, nc, new Vector3(sx, 6.2f, 0.08f), new Vector3(1.35f, text.Length * 1.15f + 0.35f, 0.05f), default, true);
                    Box(p, H("#1E1A30"), new Vector3(sx, 6.2f, 0.04f), new Vector3(1.15f, text.Length * 1.15f + 0.15f, 0.05f), default, true);
                    var t = Text3D(p, string.Join("\n", text.ToCharArray()), nc, new Vector3(sx, 6.2f, -0.02f), 7f);
                    t.lineSpacing = -20;
                }
                k++;
            }
            // red taxis parked behind the curb
            for (float x = -w / 2 + 6; x < w / 2; x += 24f)
            {
                var car = Node(p, "Taxi", new Vector3(x, 0, -2.5f));
                Box(car, H("#D9343A"), new Vector3(0, 0.75f, 0), new Vector3(3.6f, 0.8f, 1.6f));
                Box(car, H("#F2EEE8"), new Vector3(-0.2f, 1.4f, 0), new Vector3(2.0f, 0.65f, 1.5f));
                Box(car, H("#9FD4F0"), new Vector3(-0.2f, 1.42f, -0.76f), new Vector3(1.7f, 0.45f, 0.02f), default, true);
                Box(car, H("#FFE07A"), new Vector3(-0.2f, 1.85f, 0), new Vector3(0.5f, 0.2f, 0.3f), default, true);
                foreach (float wx in new[] { -1.2f, 1.2f }) Cyl(car, H("#1D1B22"), new Vector3(wx, 0.36f, -0.6f), 0.36f, 0.3f, new Vector3(90, 0, 0));
            }
        }

        void MidKinmen(Transform p, System.Random r, float w)
        {
            for (float x = -w / 2 + 4; x < w / 2; x += 18f)
            {
                // Minnan red-brick house with swallow-tail roof
                var h = Node(p, "MinnanHouse", new Vector3(x, 0, 1.5f));
                Box(h, H("#E9DFD0"), new Vector3(0, 0.4f, 0), new Vector3(7.4f, 0.8f, 3.4f));
                Box(h, H("#B9533F"), new Vector3(0, 2.0f, 0), new Vector3(7.2f, 2.6f, 3.2f));
                Box(h, H("#7A2E22"), new Vector3(0, 1.4f, -1.62f), new Vector3(1.4f, 2.2f, 0.05f));
                Box(h, Palette.Gold, new Vector3(0, 2.75f, -1.64f), new Vector3(1.0f, 0.3f, 0.02f), default, true);
                Box(h, H("#E9DFD0"), new Vector3(-2.4f, 2f, -1.62f), new Vector3(1.1f, 1f, 0.05f));
                Box(h, H("#E9DFD0"), new Vector3(2.4f, 2f, -1.62f), new Vector3(1.1f, 1f, 0.05f));
                Part(h, MeshKit.Prism(), H("#C8553A"), new Vector3(0, 3.3f, 0), new Vector3(7.6f, 1.5f, 3.8f));
                Box(h, H("#A23F2C"), new Vector3(0, 4.8f, 0), new Vector3(6.6f, 0.25f, 0.35f));
                Box(h, H("#A23F2C"), new Vector3(-3.7f, 5.15f, 0), new Vector3(1.4f, 0.22f, 0.3f), new Vector3(0, 0, -35));
                Box(h, H("#A23F2C"), new Vector3(3.7f, 5.15f, 0), new Vector3(1.4f, 0.22f, 0.3f), new Vector3(0, 0, 35));
                Box(h, H("#D7263D"), new Vector3(-0.9f, 2.0f, -1.65f), new Vector3(0.35f, 0.9f, 0.02f), default, true); // spring couplets
                Box(h, H("#D7263D"), new Vector3(0.9f, 2.0f, -1.65f), new Vector3(0.35f, 0.9f, 0.02f), default, true);

                // Wind Lion God guardian statue
                var lion = Node(p, "WindLion", new Vector3(x + 9, 0, -1.5f));
                Box(lion, H("#9C978F"), new Vector3(0, 0.3f, 0), new Vector3(1.2f, 0.6f, 1.2f));
                Part(lion, MeshKit.Capsule, H("#B7B1A8"), new Vector3(0, 1.35f, 0), new Vector3(0.75f, 0.65f, 0.6f));
                Ball(lion, H("#B7B1A8"), new Vector3(0.05f, 2.3f, 0), new Vector3(0.95f, 0.85f, 0.85f));
                Ball(lion, H("#2A2230"), new Vector3(0.3f, 2.4f, -0.36f), Vector3.one * 0.12f, default, true);
                Ball(lion, H("#2A2230"), new Vector3(-0.15f, 2.4f, -0.4f), Vector3.one * 0.12f, default, true);
                Box(lion, H("#D7263D"), new Vector3(0, 1.6f, -0.2f), new Vector3(0.95f, 0.7f, 0.35f), new Vector3(0, 0, 0));
                Tree(p, x + 13, 2, H("#4F9E5E"), H("#64B06F"), 1.1f);
            }
        }

        void MidTaipei(Transform p, System.Random r, float w)
        {
            Color[] awn = { H("#FF6B6B"), H("#FFD166"), H("#4ECDC4") };
            int k = 0;
            for (float x = -w / 2; x < w / 2; x += 12f, k++)
            {
                // night market stall
                var s = Node(p, "Stall", new Vector3(x, 0, 1));
                Box(s, H("#F4EDE4"), new Vector3(0, 0.6f, 0), new Vector3(3.6f, 1.2f, 1.6f));
                Box(s, H("#B98B5E"), new Vector3(0, 1.25f, 0), new Vector3(3.8f, 0.1f, 1.8f));
                Cyl(s, H("#5D5A66"), new Vector3(-1.7f, 1.9f, -0.7f), 0.06f, 1.4f);
                Cyl(s, H("#5D5A66"), new Vector3(1.7f, 1.9f, -0.7f), 0.06f, 1.4f);
                for (int i = 0; i < 6; i++)
                    Box(s, i % 2 == 0 ? awn[k % awn.Length] : Color.white, new Vector3(-1.6f + i * 0.64f, 2.75f, -0.3f), new Vector3(0.64f, 0.12f, 2.2f), new Vector3(-12, 0, 0));
                Box(s, H("#3A2E3A"), new Vector3(0, 3.4f, 0.4f), new Vector3(2.4f, 0.8f, 0.1f));
                string[] names = { "夜市", "雞排", "珍奶", "小籠包" };
                Text3D(s, names[k % names.Length], H("#FFE08A"), new Vector3(0, 3.4f, 0.32f), 5.5f);
                // lantern string
                for (int i = 0; i < 5; i++)
                {
                    float lx = x + 5 + i * 1.4f;
                    float ly = 5.2f - Mathf.Sin(i / 4f * Mathf.PI) * 0.6f;
                    Ball(p, H("#E63946"), new Vector3(lx, ly, 0.5f), new Vector3(0.6f, 0.75f, 0.6f), default, true);
                    Box(p, Palette.Gold, new Vector3(lx, ly + 0.42f, 0.5f), new Vector3(0.3f, 0.1f, 0.3f), default, true);
                }
                Cyl(p, H("#5D5A66"), new Vector3(x + 4.4f, 2.7f, 0.5f), 0.07f, 5.4f);
                Cyl(p, H("#5D5A66"), new Vector3(x + 11.2f, 2.7f, 0.5f), 0.07f, 5.4f);
                if (k % 2 == 1) Tree(p, x + 8, 3, H("#3F8F5A"), H("#58A66E"));
            }
        }

        void MidNagoya(Transform p, System.Random r, float w)
        {
            int k = 0;
            for (float x = -w / 2; x < w / 2; x += 8f, k++)
            {
                // sakura tree
                float z = Rf(r, 0, 2.5f);
                Cyl(p, H("#6E4A3A"), new Vector3(x, 1.4f, z), 0.25f, 2.8f);
                Box(p, H("#6E4A3A"), new Vector3(x - 0.6f, 2.8f, z), new Vector3(0.18f, 1.5f, 0.18f), new Vector3(0, 0, 35));
                Box(p, H("#6E4A3A"), new Vector3(x + 0.6f, 3.0f, z), new Vector3(0.18f, 1.5f, 0.18f), new Vector3(0, 0, -35));
                for (int i = 0; i < 6; i++)
                {
                    var pos = new Vector3(x + Rf(r, -1.6f, 1.6f), Rf(r, 3.2f, 4.6f), z + Rf(r, -1f, 0.6f));
                    Ball(p, i % 2 == 0 ? H("#FFB7CB") : H("#FFD3E0"), pos, Vector3.one * Rf(r, 1.4f, 2.1f));
                }
                if (k % 3 == 1)
                {
                    // stone lantern
                    var l = Node(p, "Toro", new Vector3(x + 4, 0, -1.2f));
                    Box(l, H("#A9A49C"), new Vector3(0, 0.15f, 0), new Vector3(0.9f, 0.3f, 0.9f));
                    Cyl(l, H("#B9B4AC"), new Vector3(0, 0.9f, 0), 0.18f, 1.2f);
                    Box(l, H("#B9B4AC"), new Vector3(0, 1.65f, 0), new Vector3(0.8f, 0.6f, 0.8f));
                    Box(l, H("#FFE7A8"), new Vector3(0, 1.65f, -0.41f), new Vector3(0.35f, 0.3f, 0.02f), default, true);
                    Frus(l, H("#9C978F"), new Vector3(0, 1.95f, 0), 0.75f, 0.1f, 0.5f, new Vector3(0, 45, 0), 4);
                }
                else if (k % 3 == 2)
                {
                    // vending machine
                    var v = Node(p, "Vending", new Vector3(x + 4, 0, -0.8f));
                    Box(v, H("#F4F6FA"), new Vector3(0, 1.0f, 0), new Vector3(1.1f, 2.0f, 0.8f));
                    Box(v, H("#BFE3FF"), new Vector3(0, 1.35f, -0.41f), new Vector3(0.9f, 0.9f, 0.02f), default, true);
                    Color[] can = { H("#E5484D"), H("#2FB37E"), H("#3D7DFF"), H("#FFB020") };
                    for (int i = 0; i < 8; i++)
                        Box(v, can[i % 4], new Vector3(-0.33f + (i % 4) * 0.22f, 1.15f + (i / 4) * 0.4f, -0.43f), new Vector3(0.12f, 0.25f, 0.02f), default, true);
                }
            }
        }

        void MidWedding(Transform p, System.Random r, float w)
        {
            Color[] balloons = { H("#FF6F91"), H("#FFC75F"), H("#FFFFFF"), H("#C9A7FF"), H("#FF9EB8") };
            int k = 0;
            for (float x = -w / 2; x < w / 2; x += 12f, k++)
            {
                // flower arch
                var a = Node(p, "Arch", new Vector3(x, 0, 1));
                Part(a, MeshKit.Torus(2.2f, 0.18f, 18, 6, 180f), Color.white, new Vector3(0, 2.4f, 0), Vector3.one);
                Cyl(a, Color.white, new Vector3(-2.2f, 1.2f, 0), 0.18f, 2.4f);
                Cyl(a, Color.white, new Vector3(2.2f, 1.2f, 0), 0.18f, 2.4f);
                for (int i = 0; i <= 8; i++)
                {
                    float ang = i / 8f * Mathf.PI;
                    Ball(a, i % 2 == 0 ? H("#FF8FB1") : H("#FFE3EC"), new Vector3(Mathf.Cos(ang) * 2.2f, 2.4f + Mathf.Sin(ang) * 2.2f, -0.2f), Vector3.one * 0.55f);
                }
                Ball(a, H("#7FBF7F"), new Vector3(-2.2f, 0.3f, 0), new Vector3(1, 0.6f, 1));
                Ball(a, H("#7FBF7F"), new Vector3(2.2f, 0.3f, 0), new Vector3(1, 0.6f, 1));
                // heart balloons
                for (int i = 0; i < 3; i++)
                {
                    float bx = x + 6 + i * 0.9f, by = 4.2f + (i % 2) * 0.8f;
                    Part(p, MeshKit.Heart(0.35f), balloons[(k + i) % balloons.Length], new Vector3(bx, by, 0.5f), Vector3.one * 1.1f, new Vector3(0, 0, (i - 1) * 12f));
                    Box(p, H("#D9D2DA"), new Vector3(x + 6.6f, by / 2 - 0.2f, 0.5f), new Vector3(0.03f, by - 0.6f, 0.03f), new Vector3(0, 0, (1 - i) * 8f), true);
                }
                // string lights
                for (int i = 0; i < 10; i++)
                {
                    float lx = x - 5 + i * 1.2f;
                    Ball(p, H("#FFF0B3"), new Vector3(lx, 6.2f - Mathf.Sin(i / 9f * Mathf.PI) * 0.7f, 2), Vector3.one * 0.25f, default, true);
                }
            }
        }
    }
}
