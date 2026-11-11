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
        Material haloMat;
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
            skyMesh = new Mesh { name = "Sky" };
            SetSky(Color.blue, Color.white, Color.white);
            skyQuad = new GameObject("Sky").transform;
            skyQuad.SetParent(cam.transform, false);
            skyQuad.localPosition = new Vector3(0, 0, cam.farClipPlane * 0.9f);
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
            haloMat = new Material(VertexMaterial) { color = new Color(1, 1, 1, 0.2f), renderQueue = 1001 };
            SkyPart(sunDisc, new Vector3(0, 0, 0.6f), Vector3.one * 2.6f, haloMat);

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
            int previous = stage;
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
            var front = MakeStrip("Front_" + def.nameEn, -4.3f, 1f, 48f, FrontBuilder(index), true);
            foreach (var s in new[] { far, mid, front })
            {
                s.targetY = s.baseY;
                s.y = instant ? s.baseY : s.baseY - (s.factor < 0.3f ? 45f : 14f);
                Place(s);
            }

            // nobody at the night-time port, and only Kinmen has elderly villagers: clear whoever is still off-screen ahead
            if (index == 5 || index == KinmenTheme || previous == KinmenTheme)
                for (int i = walkers.Count - 1; i >= 0; i--)
                    if (walkers[i].x > 12f || instant) { Destroy(walkers[i].p.root.gameObject); walkers.RemoveAt(i); }

            var pm = petals.main;
            pm.startColor = new ParticleSystem.MinMaxGradient(def.petalA, def.petalB);
            if (instant) ApplyColors(def);
        }

        StageDef current; // blended snapshot used when a transition is interrupted

        StageDef CurrentBlend() => current ?? to;

        void ApplyColors(StageDef d)
        {
            current = d;
            Color Sky(Color c) => Color.Lerp(c, c.linear, d.trueSky);   // vertex colours skip the gamma→linear conversion
            SetSky(Sky(d.skyTop), Sky(d.skyBottom), Sky(Color.Lerp(d.skyBottom, d.light, d.glow)));
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
            bool night = d.theme == 0 || d.theme == 2 || d.theme == 4 || d.theme == 5;
            sunMat.color = night ? new Color(1f, 0.97f, 0.88f) : new Color(1f, 0.93f, 0.7f);
            haloMat.color = night ? new Color(1f, 0.95f, 0.8f, 0.16f) : new Color(1f, 0.9f, 0.6f, 0.28f);
            cloudMat.color = Color.Lerp(Color.white, d.skyBottom, night ? 0.55f : 0.25f);
            stars.gameObject.SetActive(night);
        }

        /// <summary>Sky gradient in camera space: glow at the horizon → horizon colour → zenith colour.</summary>
        void SetSky(Color top, Color bottom, Color glow)
        {
            float D = cam.farClipPlane * 0.9f, W = D * 3f;
            float yLow = -D, y0 = D * Mathf.Tan(4f * Mathf.Deg2Rad), y1 = D * Mathf.Tan(14f * Mathf.Deg2Rad), y2 = D * Mathf.Tan(27f * Mathf.Deg2Rad);
            float[] ys = { yLow, y0, y1, y2 };
            Color[] cs = { glow, glow, bottom, top };
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color>();
            var t = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 4; i++) { v.Add(new Vector3(-W, ys[i])); v.Add(new Vector3(W, ys[i])); c.Add(cs[i]); c.Add(cs[i]); }
            for (int i = 0; i < 3; i++) { int a = i * 2; t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            skyMesh.Clear();
            skyMesh.SetVertices(v); skyMesh.SetColors(c); skyMesh.SetTriangles(t, 0);
            skyMesh.bounds = new Bounds(Vector3.zero, new Vector3(W * 2, D * 2, 10));
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
                glow = Mathf.Lerp(a.glow, b.glow, t),
                trueSky = Mathf.Lerp(a.trueSky, b.trueSky, t),
            };
        }

        // ------------------------------------------------------------------ per-frame

        // ------------------------------------------------------------------ passers-by

        const int KinmenTheme = 1;
        class Walker { public ModelFactory.Person p; public float x, y, z, dir, pace, phase; public bool watcher; }
        readonly List<Walker> walkers = new List<Walker>();
        /// <summary>While true (the proposal), passers-by stop and watch the couple.</summary>
        public bool Spectate;
        float walkerTimer = 0.5f;
        int walkerSeed;

        /// <summary>A few townspeople strolling along the pavements (behind the road, sometimes in front).</summary>
        void UpdateWalkers(float dt, float speed)
        {
            walkerTimer -= dt;
            bool wedding = stage == 4 || Spectate;
            if (walkerTimer <= 0)
            {
                // the night-time port is just the two of them
                if (stage != 5 && walkers.Count < (wedding ? 8 : 5)) SpawnWalker(speed);
                walkerTimer = wedding ? Random.Range(0.8f, 1.8f) : Random.Range(1.4f, 3.4f);
            }
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                if (wedding && !w.watcher && w.z > 0) w.watcher = true;   // people on the far pavement stop to watch the couple
                if (w.watcher)
                {
                    // stand still, turn towards the bride & groom, clap — and wave and hop as they pass
                    w.x -= speed * dt;
                    float dx = CoupleRunner.GroomX - 0.5f - w.x, dz = -w.z;
                    var look = Quaternion.Euler(0, Mathf.Atan2(-dz, dx) * Mathf.Rad2Deg, 0);
                    w.p.root.localRotation = Quaternion.Slerp(w.p.root.localRotation, look, dt * 6f);
                    w.phase += dt;
                    bool near = Mathf.Abs(dx) < 5f;
                    float clap = Mathf.Sin(w.phase * 14f) * 14f;
                    w.p.legL.localRotation = Quaternion.identity;
                    w.p.legR.localRotation = Quaternion.identity;
                    w.p.armL.localRotation = Quaternion.Slerp(w.p.armL.localRotation, Quaternion.Euler(0, 0, near ? 150f + clap : 75f + clap), dt * 10f);
                    w.p.armR.localRotation = Quaternion.Slerp(w.p.armR.localRotation, Quaternion.Euler(0, 0, near ? 150f - clap : 75f - clap), dt * 10f);
                    w.p.body.localPosition = new Vector3(0, near ? Mathf.Abs(Mathf.Sin(w.phase * 7f)) * 0.2f : 0f, 0);
                    w.p.root.localPosition = new Vector3(w.x, w.y, w.z);
                    if (w.x < -26f) { Destroy(w.p.root.gameObject); walkers.RemoveAt(i); }
                    continue;
                }
                if (w.dir > 0 && speed > w.pace)
                    w.dir = -1f;   // the run started again: turn round instead of drifting backwards
                w.p.root.localRotation = Quaternion.Slerp(w.p.root.localRotation, Quaternion.Euler(0, w.dir > 0 ? 20f : 160f, 0), dt * 8f);
                w.x += (w.dir * w.pace - speed) * dt;
                w.phase += dt * w.pace * 5.5f;
                float sw = Mathf.Sin(w.phase);
                float stride = w.p.elder ? 18f : 28f, armSwing = w.p.elder ? 10f : 22f;
                w.p.legL.localRotation = Quaternion.Euler(0, 0, sw * stride);
                w.p.legR.localRotation = Quaternion.Euler(0, 0, -sw * stride);
                w.p.armL.localRotation = Quaternion.Euler(0, 0, w.p.elder ? ModelFactory.CaneArmAngle - sw * 3f : -sw * armSwing);
                w.p.armR.localRotation = Quaternion.Euler(0, 0, w.p.armR.childCount > 2 ? sw * 8f : sw * armSwing);
                w.p.body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Cos(w.phase)) * (w.p.elder ? 0.015f : 0.04f), 0);
                w.p.root.localPosition = new Vector3(w.x, w.y, w.z);
                if (w.x < -26f || w.x > 32f)
                {
                    Destroy(w.p.root.gameObject);
                    walkers.RemoveAt(i);
                }
            }
        }

        void SpawnWalker(float speed)
        {
            bool elder = stage == KinmenTheme;
            // While the world scrolls, someone walking the same way as the couple drifts backwards on screen and looks
            // like they are moonwalking, so everyone comes towards the couple unless the world has stopped.
            var w = new Walker { dir = speed > 0.5f || Random.value < 0.5f ? -1f : 1f, pace = elder ? Random.Range(0.8f, 1.1f) : Random.Range(1.1f, 1.7f), phase = Random.value * 6f };
            bool front = stage != 4 && !Spectate && Random.value < 0.25f;   // wedding watchers line the far pavement only
            w.z = front ? Random.Range(-6.2f, -5.4f) : Random.Range(3.3f, 4.5f);
            w.y = front ? 0.05f : 0.12f;
            w.x = speed > 2f ? 27f : (w.dir > 0 ? -24f : 27f);
            if (stage == 4 || Spectate) { w.watcher = true; w.x = speed > 2f ? 27f : Random.Range(-14f, 14f); }
            w.p = ModelFactory.BuildPerson(transform, 7000 + walkerSeed++ * 13, true, elder);
            if (elder && front) w.p.body.localScale *= 0.82f;   // the near pavement is closer to the camera
            w.p.root.localRotation = Quaternion.Euler(0, w.dir > 0 ? 20f : 160f, 0);
            w.p.root.localPosition = new Vector3(w.x, w.y, w.z);
            walkers.Add(w);
        }

        public void Tick(float dt, float speed)
        {
            UpdateWalkers(dt, speed);
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
                case 5: return FarNagoyaPort;
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
            float ix = 29;
            Box(p, H("#5C6AA0"), new Vector3(ix, 12, -5), new Vector3(5, 24, 5));
            Box(p, H("#6676AE"), new Vector3(ix, 25.5f, -5), new Vector3(4, 3, 4));
            Box(p, H("#7584BC"), new Vector3(ix, 27.6f, -5), new Vector3(3, 1.4f, 3));
            for (int k = 0; k < 4; k++) Box(p, H("#9FB0E6"), new Vector3(ix - 1.5f + k, 28.8f, -5), new Vector3(0.3f, 1.6f, 0.3f), default, true);
            Windows(p, r, new Vector3(ix, 12, -5), new Vector3(5, 24, 5), 0.5f, lit, 1.2f);
            VictoriaHarbour(p, r, w, lit);
            // harbour lights strip
            for (float x = -w / 2; x < w / 2; x += 1.6f)
                Box(p, Pick(r, lit), new Vector3(x, 0.15f, -9), new Vector3(0.8f, 0.1f, 0.1f), default, true);
            // Bank of China–style tower: angular glass with white X bracing
            var boc = Node(p, "BOC", new Vector3(14, 0, -4));
            Box(boc, H("#5B6E9E"), new Vector3(0, 10, 0), new Vector3(3.8f, 20, 3.8f));
            Part(boc, MeshKit.Prism(), H("#6A7EB0"), new Vector3(0, 20f, 0), new Vector3(3.8f, 3.5f, 3.8f), new Vector3(0, 90, 0));
            for (int k = 0; k < 4; k++)
            {
                Box(boc, H("#E8ECF8"), new Vector3(0, 2.5f + k * 4.6f, -1.92f), new Vector3(0.12f, 6.2f, 0.04f), new Vector3(0, 0, 38), true);
                Box(boc, H("#E8ECF8"), new Vector3(0, 2.5f + k * 4.6f, -1.92f), new Vector3(0.12f, 6.2f, 0.04f), new Vector3(0, 0, -38), true);
            }
            Cyl(boc, H("#C9D3D6"), new Vector3(-0.8f, 25.5f, 0), 0.08f, 3.5f);
            Cyl(boc, H("#C9D3D6"), new Vector3(0.8f, 25f, 0), 0.08f, 2.6f);
            // neon reflections shimmering on Victoria Harbour
            for (float x = -w / 2; x < w / 2; x += Rf(r, 0.8f, 2.2f))
            {
                Color c = Color.Lerp(Pick(r, lit), H("#2B3E6E"), 0.35f);
                Box(p, c, new Vector3(x, 0.04f, Rf(r, -40f, -14f)), new Vector3(Rf(r, 0.12f, 0.4f), 0.03f, Rf(r, 1.5f, 5f)), default, true);
            }
            // red-sail junk boat
            var junk = Node(p, "Junk", new Vector3(-4, 0, -33));
            Box(junk, H("#5A3A28"), new Vector3(0, 0.5f, 0), new Vector3(6, 1f, 1.6f));
            Box(junk, H("#5A3A28"), new Vector3(3.2f, 0.75f, 0), new Vector3(1.4f, 0.8f, 1.5f), new Vector3(0, 0, -28));
            for (int k = 0; k < 3; k++)
            {
                float sx = -1.8f + k * 1.9f, sh = k == 1 ? 5f : 4f;
                Cyl(junk, H("#3A2A20"), new Vector3(sx, 1f + sh / 2, 0), 0.06f, sh);
                Box(junk, H("#C8102E"), new Vector3(sx + 0.7f, 1.6f + sh * 0.45f, 0), new Vector3(1.4f, sh * 0.8f, 0.05f));
                for (int b = 0; b < 4; b++) Box(junk, H("#8A0A1E"), new Vector3(sx + 0.7f, 1.8f + b * sh * 0.2f, -0.03f), new Vector3(1.45f, 0.05f, 0.02f), default, true);
            }

            // ---- sightseeing: Victoria Peak with the Peak Tower
            Ball(p, H("#2A3156"), new Vector3(55, -2, 26), new Vector3(46, 30, 20));
            var peak = Node(p, "PeakTower", new Vector3(55, 12.3f, 24));
            Box(peak, H("#8C93B0"), new Vector3(0, 1.0f, 0), new Vector3(2.4f, 2f, 2.4f));
            Frus(peak, H("#C9CFE6"), new Vector3(0, 2f, 0), 1.6f, 3.4f, 1.4f, default, 16);   // the "wok" on top
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16;
                Ball(peak, H("#FFE6A8"), new Vector3(Mathf.Cos(a) * 3.3f, 3.35f, Mathf.Sin(a) * 3.3f), Vector3.one * 0.25f, default, true);
            }
            // ---- the Star Ferry crossing the harbour
            var ferry = Node(p, "StarFerry", new Vector3(-26, 0, -26));
            Box(ferry, H("#2E7D4F"), new Vector3(0, 0.45f, 0), new Vector3(6.5f, 0.9f, 1.8f));
            Box(ferry, Color.white, new Vector3(0, 1.25f, 0), new Vector3(6f, 0.7f, 1.7f));
            for (int i = 0; i < 8; i++) Box(ferry, H("#FFE6A8"), new Vector3(-2.6f + i * 0.75f, 1.3f, -0.86f), new Vector3(0.45f, 0.35f, 0.02f), default, true);
            Box(ferry, H("#2E7D4F"), new Vector3(0, 1.67f, 0), new Vector3(6.2f, 0.14f, 1.8f));
            Box(ferry, Color.white, new Vector3(0, 2.05f, 0), new Vector3(4.4f, 0.6f, 1.5f));
            Box(ferry, H("#2E7D4F"), new Vector3(0, 2.42f, 0), new Vector3(4.6f, 0.12f, 1.6f));
            Box(ferry, Color.white, new Vector3(0, 0.95f, -0.91f), new Vector3(6.4f, 0.08f, 0.02f), default, true);
        }

        /// <summary>
        /// Victoria Harbour as seen from the Tsim Sha Tsui promenade (Wan Chai on the left, Central on the right):
        /// the Convention Centre's bird-like roof on the water, triangular Central Plaza with its neon clock bars,
        /// round Hopewell Centre, The Center's colour bands, the Observation Wheel in front of IFC,
        /// light reflections on the water, and the Symphony of Lights searchlights and lasers.
        /// </summary>
        void VictoriaHarbour(Transform p, System.Random r, float w, Color[] lit)
        {
            // HKCEC: glass halls under a sweeping bird/turtle-shell roof, on the water
            var cec = Node(p, "HKCEC", new Vector3(-37, 0, -13));
            Box(cec, H("#7E9CC8"), new Vector3(0, 0.9f, 0), new Vector3(9f, 1.8f, 4.5f));
            for (int i = 0; i < 9; i++) Box(cec, H("#FFE6A8"), new Vector3(-4f + i, 0.9f, -2.27f), new Vector3(0.6f, 1.2f, 0.03f), default, true);
            Ball(cec, H("#DDE3EE"), new Vector3(0, 1.9f, 0), new Vector3(10.5f, 1.6f, 5.4f));
            Ball(cec, H("#E9EEF6"), new Vector3(-3.4f, 2.1f, -0.3f), new Vector3(4.2f, 1.1f, 4f), new Vector3(0, 0, 12));
            Ball(cec, H("#E9EEF6"), new Vector3(3.4f, 2.1f, -0.3f), new Vector3(4.2f, 1.1f, 4f), new Vector3(0, 0, -12));
            // Central Plaza: triangular plan (truncated corners), pyramid top with a mast, four-bar neon clock
            var cp = Node(p, "CentralPlaza", new Vector3(-27, 0, -5));
            Frus(cp, H("#C9A85A"), Vector3.zero, 3.2f, 3.0f, 23f, new Vector3(0, 30, 0), 3);
            for (float y = 1.5f; y < 23f; y += 1.3f) Box(cp, H("#FFE7A0"), new Vector3(0, y, -1.45f), new Vector3(2.6f, 0.14f, 0.05f), default, true);
            Frus(cp, H("#E8C870"), new Vector3(0, 23f, 0), 3.0f, 0.3f, 3f, new Vector3(0, 30, 0), 3);
            Color[] clock = { H("#FF4F5E"), H("#FFD23F"), H("#4FE3FF"), H("#7CFF8A") };
            for (int i = 0; i < 4; i++) Box(cp, clock[i], new Vector3(0, 23.4f + i * 0.5f, -1.2f + i * 0.3f), new Vector3(2.0f - i * 0.4f, 0.18f, 0.05f), default, true);
            Cyl(cp, H("#D9DEE8"), new Vector3(0, 28.5f, 0), 0.1f, 5f);
            // Hopewell Centre: round tower with a revolving restaurant top
            var hw = Node(p, "Hopewell", new Vector3(-16, 0, -1));
            Cyl(hw, H("#8A92B0"), new Vector3(0, 9f, 0), 1.8f, 18f);
            for (float y = 1f; y < 18f; y += 1f) Cyl(hw, Pick(r, lit), new Vector3(0, y, 0), 1.82f, 0.12f, default, true);
            Cyl(hw, H("#B6BDD4"), new Vector3(0, 18.4f, 0), 2.1f, 0.8f);
            // The Center: stacked colour bands up the whole tower
            var tc = Node(p, "TheCenter", new Vector3(4, 0, -4));
            Box(tc, H("#3E4468"), new Vector3(0, 11f, 0), new Vector3(3.4f, 22f, 3.4f));
            Color[] rainbow = { H("#FF4F5E"), H("#FF9A3D"), H("#FFD23F"), H("#7CFF8A"), H("#4FE3FF"), H("#9B7BFF") };
            for (int i = 0; i < 18; i++) Box(tc, rainbow[i % rainbow.Length], new Vector3(0, 1.2f + i * 1.2f, -1.72f), new Vector3(3.3f, 0.32f, 0.04f), default, true);
            Frus(tc, H("#5A6290"), new Vector3(0, 22f, 0), 2.3f, 0.6f, 2f, new Vector3(0, 45, 0), 4);
            Cyl(tc, H("#D9DEE8"), new Vector3(0, 25f, 0), 0.08f, 2.4f);
            // Hong Kong Observation Wheel (60 m, 42 gondolas, red lights) on the Central harbourfront
            var hw2 = Node(p, "ObservationWheel", new Vector3(21.5f, 0, -13));
            float wr = 3.3f;
            Box(hw2, H("#CFD5DE"), new Vector3(0, 0.4f, 0), new Vector3(3f, 0.8f, 2f));
            Beam(hw2, new Vector3(-1.4f, 0.8f, 0), new Vector3(0, wr + 0.9f, 0), 0.14f, H("#E4E8EE"));
            Beam(hw2, new Vector3(1.4f, 0.8f, 0), new Vector3(0, wr + 0.9f, 0), 0.14f, H("#E4E8EE"));
            var wheel = Node(hw2, "Ring", new Vector3(0, wr + 0.9f, -0.2f));
            Part(wheel, MeshKit.Torus(wr, 0.09f, 48, 5), H("#FF3B4E"), Vector3.zero, Vector3.one, default, true);
            Part(wheel, MeshKit.Torus(wr * 0.92f, 0.04f, 48, 4), H("#FFD0D6"), Vector3.zero, Vector3.one, default, true);
            for (int i = 0; i < 14; i++)
            {
                float a = i * 360f / 14;
                Box(wheel, H("#F2F2F2"), new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * wr / 2, Mathf.Sin(a * Mathf.Deg2Rad) * wr / 2, 0), new Vector3(wr, 0.04f, 0.04f), new Vector3(0, 0, a));
            }
            for (int i = 0; i < 42; i++)
            {
                float a = i * Mathf.PI * 2 / 42;
                Box(wheel, H("#FFFFFF"), new Vector3(Mathf.Cos(a) * wr, Mathf.Sin(a) * wr - 0.2f, -0.1f), new Vector3(0.22f, 0.24f, 0.22f));
            }
            // reflections of the landmarks shimmering on the harbour
            float[] lx = { -37f, -27f, -16f, 4f, 14f, 21.5f, 29f };
            Color[] lc = { H("#FFE6A8"), H("#FFD86B"), H("#FFB3D9"), H("#9B7BFF"), H("#E8ECF8"), H("#FF3B4E"), H("#9FB0E6") };
            for (int i = 0; i < lx.Length; i++)
                for (int k = 0; k < 6; k++)
                    Box(p, Color.Lerp(lc[i], H("#2B3E6E"), 0.25f + k * 0.08f), new Vector3(lx[i] + Rf(r, -0.8f, 0.8f), 0.05f, -16f - k * 3.2f), new Vector3(Rf(r, 0.25f, 0.6f), 0.03f, Rf(r, 1.6f, 2.8f)), default, true);
            // a harbour tour boat
            var tour = Node(p, "TourBoat", new Vector3(10, 0, -24));
            Box(tour, Color.white, new Vector3(0, 0.4f, 0), new Vector3(4.5f, 0.8f, 1.4f));
            Box(tour, H("#2E5E9E"), new Vector3(0, 0.1f, 0), new Vector3(4.6f, 0.2f, 1.45f));
            for (int i = 0; i < 6; i++) Box(tour, H("#FFE6A8"), new Vector3(-1.8f + i * 0.7f, 0.5f, -0.71f), new Vector3(0.4f, 0.25f, 0.02f), default, true);
            Box(tour, Color.white, new Vector3(-0.4f, 1.05f, 0), new Vector3(2.4f, 0.5f, 1.2f));

            // ---- A Symphony of Lights: sweeping searchlights and lasers (animated, never merged)
            var show = Node(p, "SymphonyOfLights", Vector3.zero);
            show.gameObject.AddComponent<NoMerge>();
            // searchlights fired from the waterfront so they sweep through the visible sky in front of the towers
            Vector3[] bases = { new Vector3(-37, 2.6f, -13), new Vector3(-22, 0.5f, -11), new Vector3(-6, 0.5f, -11), new Vector3(9, 0.5f, -11), new Vector3(21.5f, 1f, -13), new Vector3(34, 0.5f, -11) };
            for (int i = 0; i < bases.Length; i++)
                for (int k = 0; k < 2; k++)
                {
                    var pivot = Node(show, "Searchlight", bases[i], new Vector3(0, 0, k == 0 ? 16 : -16));
                    var sw = pivot.gameObject.AddComponent<Sweeper>();
                    sw.amplitude = 24f; sw.speed = 0.45f + i * 0.11f + k * 0.19f; sw.phase = i * 1.3f + k * 2f;
                    Glow(pivot, MeshKit.Frustum(0.1f, 1f), new Color(0.88f, 0.93f, 1f, 0.2f), Vector3.zero, new Vector3(1.6f, 34f, 1.6f));
                }
            // lasers from the IFC rooftop sweeping across the harbour
            Color[] laser = { new Color(0.3f, 1f, 0.5f, 0.6f), new Color(0.35f, 0.75f, 1f, 0.6f), new Color(1f, 0.35f, 0.75f, 0.55f) };
            for (int k = 0; k < 3; k++)
            {
                var pivot = Node(show, "Laser", new Vector3(29, 29.5f, -5), new Vector3(0, 0, 108 + k * 14));
                var sw = pivot.gameObject.AddComponent<Sweeper>();
                sw.amplitude = 35f; sw.speed = 0.7f + k * 0.25f; sw.phase = k * 1.7f;
                Glow(pivot, MeshKit.Cube, laser[k], new Vector3(0, 22f, 0), new Vector3(0.07f, 44f, 0.07f));
            }
        }

        static readonly Dictionary<Color, Material> glowMats = new Dictionary<Color, Material>();

        /// <summary>Unlit, fog-free, translucent part (searchlight beams, lasers).</summary>
        static Transform Glow(Transform parent, Mesh mesh, Color c, Vector3 pos, Vector3 scale)
        {
            var t = Part(parent, MeshKit.WithVertexColors(mesh), Color.white, pos, scale, default, true);
            if (!glowMats.TryGetValue(c, out var m) || m == null) glowMats[c] = m = new Material(VertexMaterial) { color = c };
            t.GetComponent<MeshRenderer>().sharedMaterial = m;
            return t;
        }

        void FarKinmen(Transform p, System.Random r, float w)
        {
            Hills(p, r, w, H("#5FAE6E"), H("#6FBB78"), 10, 5, 9);
            // 太武山: a granite massif with boulders and the red 毋忘在莒 inscription carved into its rock
            var tw = Node(p, "Taiwu", new Vector3(-55f, 0, 14f));
            Ball(tw, H("#8E9E86"), new Vector3(0, -3f, 0), new Vector3(52f, 30f, 20f));
            for (int i = 0; i < 9; i++)
                Ball(tw, i % 2 == 0 ? H("#BDB8AC") : H("#A8A397"), new Vector3(Rf(r, -14f, 14f), Rf(r, 5f, 10.5f), Rf(r, -9f, -6f)), new Vector3(Rf(r, 2.5f, 5f), Rf(r, 1.8f, 3.2f), 2.5f));
            Box(tw, H("#D3CEC2"), new Vector3(3f, 8.2f, -10.4f), new Vector3(10f, 3.6f, 1f));
            Text3D(tw, "毋忘在莒", H("#C8102E"), new Vector3(3f, 8.2f, -10.95f), 13f);

            // the beach: sand, and the old anti-landing rail spikes (軌條砦) facing the sea
            Box(p, H("#EADBB8"), new Vector3(0, 0.05f, -13f), new Vector3(w + 2f, 0.08f, 3.5f));
            int n = 0;
            for (float x = -w / 2; x < w / 2; x += 1.6f, n++)
                Box(p, H("#5A4038"), new Vector3(x, 0.7f, -15.2f), new Vector3(0.16f, 1.6f, 0.16f), new Vector3(-35f, 0, n % 2 == 0 ? 10f : -10f));
            // sparkling sea + a couple of fishing boats
            for (float x = -w / 2; x < w / 2; x += Rf(r, 1f, 3f))
                Box(p, H("#E8FBFF"), new Vector3(x, 0.04f, Rf(r, -45f, -17f)), new Vector3(Rf(r, 0.6f, 1.6f), 0.03f, 0.12f), default, true);
            foreach (float bx in new[] { -50f, 22f, 60f })
            {
                var b = Node(p, "Boat", new Vector3(bx, 0, Rf(r, -36f, -24f)));
                Box(b, H("#F4F1EA"), new Vector3(0, 0.35f, 0), new Vector3(3.2f, 0.7f, 1.1f));
                Box(b, H("#2E7DB8"), new Vector3(0, 0.1f, 0), new Vector3(3.3f, 0.2f, 1.15f));
                Box(b, H("#F4F1EA"), new Vector3(-0.6f, 1.0f, 0), new Vector3(1.2f, 0.7f, 0.9f));
                Cyl(b, H("#5A4A3A"), new Vector3(0.6f, 1.6f, 0), 0.04f, 2f);
                Box(b, H("#D7263D"), new Vector3(0.9f, 2.3f, 0), new Vector3(0.6f, 0.35f, 0.02f), default, true);
            }

            JuguangTower(Node(p, "JuguangTower", new Vector3(0, 0, -6)));

            // villages on the hills: red roofs with little swallow tails
            for (float x = -w / 2; x < w / 2; x += Rf(r, 6, 12))
            {
                if (Mathf.Abs(x) < 10 || Mathf.Abs(x + 55f) < 16f) continue;
                float y = Rf(r, 0, 2.5f), z = Rf(r, -2, 6);
                Box(p, KBrick, new Vector3(x, y + 0.9f, z), new Vector3(3, 1.8f, 2));
                Part(p, MeshKit.Prism(), KTile, new Vector3(x, y + 1.8f, z), new Vector3(3.4f, 1.1f, 2.4f));
                foreach (int sg in new[] { -1, 1 })
                    Box(p, KRidge, new Vector3(x + sg * 1.75f, y + 3.05f, z), new Vector3(0.7f, 0.14f, 0.2f), new Vector3(0, 0, sg * 35f));
                Ball(p, H("#3F8F55"), new Vector3(x + 3, y + 2, z + 1), new Vector3(2.4f, 3f, 2.4f));
            }

            // ---- sightseeing: the Kinmen Bridge reaching across the sea to Lieyu
            float bz = 6f, deckY = 2.6f;
            Color bw = H("#E6E8EC");
            Box(p, bw, new Vector3(0, deckY, bz), new Vector3(w + 2f, 0.45f, 1.6f));
            Box(p, H("#B9BDC6"), new Vector3(0, deckY + 0.4f, bz - 0.8f), new Vector3(w + 2f, 0.12f, 0.06f));
            for (float x = -w / 2; x < w / 2; x += 12f) Cyl(p, H("#C9CCD2"), new Vector3(x, deckY / 2, bz), 0.35f, deckY);
            foreach (float px in new[] { -30f, 30f })
            {
                Box(p, bw, new Vector3(px - 0.7f, 8f, bz), new Vector3(0.5f, 16f, 0.6f));
                Box(p, bw, new Vector3(px + 0.7f, 8f, bz), new Vector3(0.5f, 16f, 0.6f));
                Box(p, bw, new Vector3(px, 10f, bz), new Vector3(1.9f, 0.4f, 0.6f));
                Box(p, bw, new Vector3(px, 15.6f, bz), new Vector3(1.9f, 0.4f, 0.6f));
                for (int k = 1; k <= 5; k++)
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector3 a = new Vector3(px, 15.2f, bz - 0.35f), b = new Vector3(px + side * k * 4f, deckY + 0.25f, bz - 0.35f);
                        Vector3 m = (a + b) / 2, d = b - a;
                        Box(p, H("#F4F6F8"), m, new Vector3(d.magnitude, 0.05f, 0.05f), new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), true);
                    }
            }
        }

        /// <summary>
        /// 莒光樓: a three-storey palace-style tower (1953) on a raised terrace by a pond, red columns, glazed roofs with
        /// upturned eaves on every floor, and two cannons in front.
        /// </summary>
        static void JuguangTower(Transform t)
        {
            Color wall = H("#F2E8D5"), col = H("#C0392B"), roof = H("#E39A3B"), roofDk = H("#B8742A"), stone = H("#D8CFC0");
            Box(t, H("#6FB7D8"), new Vector3(0, 0.05f, -7.5f), new Vector3(18f, 0.06f, 5f));                 // 莒光湖
            Box(t, stone, new Vector3(0, 1f, 0), new Vector3(14f, 2f, 8f));                                     // terrace
            for (int k = 0; k < 4; k++) Box(t, stone, new Vector3(0, 0.25f + k * 0.5f, -4.2f - (3 - k) * 0.5f), new Vector3(4f, 0.5f, 1f));
            foreach (int sg in new[] { -1, 1 })
            {
                Cyl(t, H("#4A4E55"), new Vector3(sg * 5.2f, 2.45f, -3.2f), 0.22f, 1.8f, new Vector3(70, 0, 0)); // cannons
                Box(t, H("#5A5E66"), new Vector3(sg * 5.2f, 2.2f, -2.9f), new Vector3(0.7f, 0.4f, 0.8f));
            }
            float[] wW = { 10f, 7.6f, 5.2f }, wH = { 3.2f, 2.8f, 2.4f }, wD = { 6f, 4.6f, 3.4f };
            float y = 2f;
            for (int f = 0; f < 3; f++)
            {
                Box(t, wall, new Vector3(0, y + wH[f] / 2, 0), new Vector3(wW[f], wH[f], wD[f]));
                int cols = f == 0 ? 6 : 4;
                for (int c = 0; c < cols; c++)
                    Box(t, col, new Vector3(-wW[f] / 2 + 0.4f + c * (wW[f] - 0.8f) / (cols - 1), y + wH[f] / 2, -wD[f] / 2 - 0.12f), new Vector3(0.32f, wH[f], 0.24f));
                Box(t, H("#7A2E22"), new Vector3(0, y + 1.0f, -wD[f] / 2 - 0.05f), new Vector3(1.4f, 2.0f, 0.05f));     // doors / windows
                y += wH[f];
                // eave: a wide glazed roof whose corners curl up
                float ew = wW[f] + 2.4f, ed = wD[f] + 2.2f;
                Box(t, roofDk, new Vector3(0, y + 0.1f, 0), new Vector3(ew, 0.2f, ed));
                Part(t, MeshKit.Prism(), roof, new Vector3(0, y + 0.2f, 0), new Vector3(ew - 0.4f, f == 2 ? 1.8f : 0.9f, ed - 0.4f));
                foreach (int sg in new[] { -1, 1 })
                    Box(t, roofDk, new Vector3(sg * (ew / 2 + 0.25f), y + 0.45f, -ed / 2 + 0.2f), new Vector3(0.9f, 0.18f, 0.3f), new Vector3(0, 0, sg * 30f));
                y += f == 2 ? 1.8f : 0.6f;
            }
            Box(t, roofDk, new Vector3(0, y + 0.15f, 0), new Vector3(4.2f, 0.3f, 0.35f));                      // top ridge
            foreach (int sg in new[] { -1, 1 }) SwallowTail(t, new Vector3(sg * 2.1f, y + 0.15f, 0), sg, 0.8f, roofDk);
        }

        void FarTaipei(Transform p, System.Random r, float w)
        {
            // Taipei at night: dark mountains, lit windows, Taipei 101 with glowing bands
            Hills(p, r, w, H("#232444"), H("#2B2B50"), 24, 12, 18);
            Color[] body = { H("#2F2B46"), H("#3A3452"), H("#28263E"), H("#40385A"), H("#33304C") };
            Color[] lit = { H("#FFE2A8"), H("#FFF4D6"), H("#FFC48A"), H("#FFD27A") };
            for (float x = -w / 2; x < w / 2;)
            {
                float bw = Rf(r, 3.5f, 7f), h = Rf(r, 4f, 14f), d = Rf(r, 3f, 6f), z = Rf(r, -2f, 6f);
                if (Mathf.Abs(x) < 7 || (x > -52 && x < -28)) { x += 2; continue; }   // gaps for Taipei 101 and the Grand Hotel
                var c = new Vector3(x + bw / 2, h / 2, z);
                Box(p, Pick(r, body), c, new Vector3(bw, h, d));
                Windows(p, r, c, new Vector3(bw, h, d), 0.55f, lit);
                if (r.NextDouble() < 0.25) Box(p, Pick(r, new[] { H("#FF5C8A"), H("#4FE3FF"), H("#FFD23F") }), new Vector3(c.x, h + 0.45f, z - d / 2), new Vector3(bw * 0.7f, 0.8f, 0.2f), default, true);
                // Taipei apartment roofs: stainless water tanks and tin add-on rooms (頂樓加蓋); iron window grilles (鐵窗)
                if (r.NextDouble() < 0.5) Cyl(p, H("#B9BFCB"), new Vector3(c.x + Rf(r, -bw * 0.3f, bw * 0.3f), h + 0.55f, z + 0.5f), 0.42f, 1.1f);
                if (r.NextDouble() < 0.35) Box(p, Pick(r, new[] { H("#4E6C8A"), H("#5E5A6E"), H("#3F6A6A") }), new Vector3(c.x - bw * 0.15f, h + 0.45f, z + 0.3f), new Vector3(bw * 0.5f, 0.9f, d * 0.6f));
                if (r.NextDouble() < 0.4)
                    for (float gx = c.x - bw / 2 + 0.3f; gx < c.x + bw / 2 - 0.1f; gx += 0.45f)
                        Box(p, H("#1E1C2A"), new Vector3(gx, h * 0.32f, z - d / 2 - 0.08f), new Vector3(0.05f, h * 0.5f, 0.04f));
                x += bw + Rf(r, 0.4f, 2f);
            }
            // Taipei 101 inspired tower, lit up at night
            var t = Node(p, "Tower101", new Vector3(0, 0, -6));
            Color glass = H("#2F5E66"), glassDark = H("#244B52"), band = H("#7FF0D8");
            Frus(t, glassDark, Vector3.zero, 4.2f, 3.0f, 5f, new Vector3(0, 45, 0), 4);
            for (int k = 0; k < 8; k++)
            {
                Frus(t, k % 2 == 0 ? glass : glassDark, new Vector3(0, 5 + k * 2.2f, 0), 2.3f, 3.0f, 2.2f, new Vector3(0, 45, 0), 4);
                Box(t, band, new Vector3(0, 5 + k * 2.2f + 2.1f, -2.08f), new Vector3(4.1f, 0.14f, 0.05f), default, true);
                Box(t, H("#FFE7A8"), new Vector3(0, 5 + k * 2.2f + 1.1f, -1.85f), new Vector3(2.2f, 0.12f, 0.05f), default, true);
            }
            // the giant ancient-coin ornament on the base section
            Cyl(t, H("#D9B44A"), new Vector3(0, 3.3f, -3.25f), 0.85f, 0.12f, new Vector3(90, 0, 0));
            Box(t, glassDark, new Vector3(0, 3.3f, -3.33f), new Vector3(0.55f, 0.55f, 0.04f));
            Box(t, glassDark, new Vector3(0, 23.4f, 0), new Vector3(2.2f, 1.2f, 2.2f));
            Box(t, glass, new Vector3(0, 24.6f, 0), new Vector3(1.4f, 1.2f, 1.4f));
            Cyl(t, H("#C9D3D6"), new Vector3(0, 27.2f, 0), 0.18f, 4f);
            Ball(t, H("#FF4F5E"), new Vector3(0, 29.3f, 0), Vector3.one * 0.5f, default, true);
            // city light reflections on the ground haze
            for (float x = -w / 2; x < w / 2; x += Rf(r, 1.5f, 3f))
                Box(p, Pick(r, lit), new Vector3(x, 0.05f, Rf(r, -16f, -9f)), new Vector3(0.12f, 0.04f, Rf(r, 0.6f, 1.6f)), default, true);

            // ---- sightseeing: the Grand Hotel (red palace on its hill)
            Ball(p, H("#1E2140"), new Vector3(-40, -1, 2), new Vector3(30, 12, 14));
            var gh = Node(p, "GrandHotel", new Vector3(-40, 4.0f, -2));
            Color ghRed = H("#C8102E"), ghRoof = H("#E89A2C"), ghWin = H("#FFD98A");
            Box(gh, ghRed, new Vector3(0, 1.4f, 0), new Vector3(13f, 2.8f, 3f));
            for (int i = 0; i < 12; i++) Box(gh, ghWin, new Vector3(-5.5f + i, 1.5f, -1.52f), new Vector3(0.5f, 1.2f, 0.04f), default, true);
            var rf1 = Node(gh, "Roof1", new Vector3(0, 2.8f, 0)); rf1.localScale = new Vector3(1, 1, 0.33f);
            Frus(rf1, ghRoof, Vector3.zero, 10f, 7.4f, 0.9f, new Vector3(0, 45, 0), 4);
            Box(gh, ghRed, new Vector3(0, 4.3f, 0), new Vector3(6f, 1.6f, 2.6f));
            for (int i = 0; i < 5; i++) Box(gh, ghWin, new Vector3(-2f + i, 4.3f, -1.32f), new Vector3(0.45f, 0.9f, 0.04f), default, true);
            var rf2 = Node(gh, "Roof2", new Vector3(0, 5.1f, 0)); rf2.localScale = new Vector3(1, 1, 0.5f);
            Frus(rf2, ghRoof, Vector3.zero, 5.4f, 3.6f, 0.8f, new Vector3(0, 45, 0), 4);
            Part(gh, MeshKit.Prism(), ghRoof, new Vector3(0, 5.85f, 0), new Vector3(3.6f, 1.1f, 1.8f));
            Box(gh, Palette.Gold, new Vector3(0, 6.97f, 0), new Vector3(3.4f, 0.12f, 0.15f), default, true);
            // ---- sightseeing: Maokong Gondola over the hills
            Vector3 c0 = new Vector3(28, 5, 12), c1 = new Vector3(88, 14, 12);
            Vector3 cd = c1 - c0;
            Box(p, H("#8A8FA3"), (c0 + c1) / 2, new Vector3(cd.magnitude, 0.06f, 0.06f), new Vector3(0, 0, Mathf.Atan2(cd.y, cd.x) * Mathf.Rad2Deg), true);
            for (int i = 0; i <= 3; i++) { var pp = Vector3.Lerp(c0, c1, i / 3f); Cyl(p, H("#6E7388"), new Vector3(pp.x, pp.y / 2, pp.z + 0.3f), 0.18f, pp.y); }
            Color[] cab = { H("#5CE1FF"), H("#FFD23F"), H("#FF5C8A"), H("#7CFF8A") };
            for (int i = 1; i < 9; i++)
            {
                var cp = Vector3.Lerp(c0, c1, i / 9f);
                Box(p, H("#8A8FA3"), cp + new Vector3(0, -0.35f, 0), new Vector3(0.05f, 0.7f, 0.05f), default, true);
                Box(p, cab[i % cab.Length], cp + new Vector3(0, -1.05f, 0), new Vector3(0.9f, 0.8f, 0.9f));
                Box(p, H("#FFF4D6"), cp + new Vector3(0, -1.0f, -0.46f), new Vector3(0.6f, 0.35f, 0.02f), default, true);
            }
            // ---- sightseeing: Lungshan-style temple with ornate ridges and lanterns
            var tp = Node(p, "Temple", new Vector3(24, 0, -10));
            Color tRed = H("#B22222"), tRoof = H("#4A4F63"), tGold = H("#F0B850"), tGreen = H("#3FA86A");
            Box(tp, H("#8C8A86"), new Vector3(0, 0.3f, 0), new Vector3(10.5f, 0.6f, 4.2f));
            Box(tp, tRed, new Vector3(0, 2.0f, 0), new Vector3(8.5f, 2.8f, 3f));
            for (int i = 0; i < 6; i++) Cyl(tp, tGold, new Vector3(-3.75f + i * 1.5f, 2.0f, -1.6f), 0.14f, 2.8f);
            for (int i = 0; i < 3; i++) Box(tp, H("#FFD98A"), new Vector3(-2.5f + i * 2.5f, 1.9f, -1.52f), new Vector3(1.2f, 1.8f, 0.04f), default, true);
            Part(tp, MeshKit.Prism(), tRoof, new Vector3(0, 3.4f, 0), new Vector3(10f, 1.6f, 4.6f));
            Box(tp, tGold, new Vector3(0, 5.0f, 0), new Vector3(8.6f, 0.25f, 0.3f));
            Box(tp, tGreen, new Vector3(0, 5.2f, 0), new Vector3(7.6f, 0.15f, 0.25f));
            foreach (int sg in new[] { -1, 1 }) Box(tp, tGold, new Vector3(sg * 4.6f, 5.35f, 0), new Vector3(1.4f, 0.22f, 0.3f), new Vector3(0, 0, sg * 35));
            Box(tp, tRed, new Vector3(0, 5.8f, 0), new Vector3(4f, 1.2f, 2f));
            Part(tp, MeshKit.Prism(), tRoof, new Vector3(0, 6.4f, 0), new Vector3(5.2f, 1.2f, 2.6f));
            Box(tp, tGold, new Vector3(0, 7.62f, 0), new Vector3(4.2f, 0.2f, 0.25f));
            foreach (int sg in new[] { -1, 1 })
            {
                Box(tp, tGold, new Vector3(sg * 2.5f, 7.9f, 0), new Vector3(1.0f, 0.2f, 0.25f), new Vector3(0, 0, sg * 40));
                Ball(tp, tGreen, new Vector3(sg * 1.2f, 7.95f, 0), new Vector3(0.9f, 0.35f, 0.25f));   // dragon figures on the ridge
            }
            Ball(tp, H("#FF4F5E"), new Vector3(0, 8.0f, 0), Vector3.one * 0.5f, default, true);         // pearl
            for (int i = 0; i < 7; i++) Ball(tp, H("#FF3B3B"), new Vector3(-3.6f + i * 1.2f, 3.0f, -2.0f), new Vector3(0.45f, 0.55f, 0.45f), default, true);
        }

        void FarNagoya(Transform p, System.Random r, float w)
        {
            Hills(p, r, w, H("#9CC9A0"), H("#F4BFD0"), 18, 4, 7);
            Color[] body = { H("#E8E4EE"), H("#D9D4E2"), H("#F1EAE4"), H("#C9D3E0"), H("#E6DCD2") };
            for (float x = -w / 2; x < w / 2;)
            {
                float bw = Rf(r, 3.5f, 6.5f), h = Rf(r, 3.5f, 11f), d = Rf(r, 3f, 6f), z = Rf(r, 0f, 8f);
                if (Mathf.Abs(x) < 15) { x += 2; continue; }
                var c = new Vector3(x + bw / 2, h / 2, z);
                Box(p, Pick(r, body), c, new Vector3(bw, h, d));
                Windows(p, r, c, new Vector3(bw, h, d), 0.35f, new[] { H("#9DB7D6"), H("#B8CBE2") });
                x += bw + Rf(r, 0.5f, 2f);
            }
            var castle = Node(p, "Castle", new Vector3(0, 0, -6));
            castle.localScale = Vector3.one * 0.8f;
            NagoyaCastle(castle);
            // moat in front of the castle + pine trees
            Box(p, H("#6FA9C8"), new Vector3(0, 0.04f, -15f), new Vector3(36, 0.06f, 3.2f), default, true);
            Box(p, H("#9C978F"), new Vector3(0, 0.25f, -13.2f), new Vector3(36, 0.5f, 0.6f));
            foreach (float x in new[] { -15f, -12.5f, 12.5f, 15.5f, -18f, 18.5f })
            {
                Cyl(p, H("#6E4A3A"), new Vector3(x, 1.2f, -10f), 0.25f, 2.4f);
                Ball(p, H("#3E7A55"), new Vector3(x, 3f, -10f), new Vector3(3.2f, 1.6f, 2.6f));
                Ball(p, H("#4B8A60"), new Vector3(x + 0.6f, 4f, -10f), new Vector3(2.2f, 1.2f, 2f));
            }
            // ---- Sakae: MIRAI TOWER with Oasis 21 right beside it
            MiraiTower(Node(p, "MiraiTower", new Vector3(20, 0, -2)));
            var oasis = Node(p, "Oasis21", new Vector3(30.5f, 0, -5));
            oasis.localScale = Vector3.one * 1.3f;
            Oasis21(oasis);
            // ---- Nagoya Station towers (further back, west side)
            var st = Node(p, "NagoyaStation", new Vector3(-27, 0, 14));
            st.localScale = Vector3.one * 0.8f;
            NagoyaStation(st);
            var sp = Node(p, "SpiralTowers", new Vector3(-13.5f, 0, 12f));
            sp.localScale = Vector3.one * 0.8f;
            SpiralTower(sp);

            // ---- sightseeing: Sunshine Sakae with the Ferris wheel built into the building
            var ss = Node(p, "SunshineSakae", new Vector3(41, 0, 0));
            Box(ss, H("#D9DEE8"), new Vector3(0, 4f, 0), new Vector3(7f, 8f, 5f));
            Windows(ss, r, new Vector3(0, 4f, 0), new Vector3(7f, 8f, 5f), 0.4f, new[] { H("#9DB7D6"), H("#B8CBE2") });
            var wheel = Node(ss, "Wheel", new Vector3(2.6f, 7.6f, -2.9f));
            Part(wheel, MeshKit.Torus(3.2f, 0.12f, 40, 6), H("#E8364F"), Vector3.zero, Vector3.one);
            Part(wheel, MeshKit.Torus(2.3f, 0.05f, 32, 4), H("#E8364F"), Vector3.zero, Vector3.one);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                Box(wheel, H("#F2F2F2"), new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 1.6f, Mathf.Sin(a * Mathf.Deg2Rad) * 1.6f, 0), new Vector3(3.2f, 0.06f, 0.06f), new Vector3(0, 0, a));
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16;
                Box(wheel, i % 2 == 0 ? H("#FFFFFF") : H("#FFD3E0"), new Vector3(Mathf.Cos(a) * 3.2f, Mathf.Sin(a) * 3.2f - 0.28f, -0.12f), new Vector3(0.42f, 0.45f, 0.42f));
            }
            Cyl(wheel, Color.white, Vector3.zero, 0.32f, 0.3f, new Vector3(90, 0, 0));
        }

        /// <summary>
        /// Towers around Nagoya Station: JR Central Towers (office tower 245 m, slightly taller than the round hotel
        /// tower 226 m, both standing on the station building), Midland Square (247 m) nearby, the slim curved
        /// Lucent Tower (180 m) and the Dai Nagoya Building with its famous name sign.
        /// </summary>
        /// <summary>Mode Gakuen Spiral Towers (170 m, near Nagoya Station): glass wings that twist round a core as the tower rises and tapers.</summary>
        static void SpiralTower(Transform t)
        {
            float u = 22f / 180f, top = 170f * u;
            Color glass = H("#A9BCD0"), glassDk = H("#8EA3BA");
            Cyl(t, H("#C9D3DE"), new Vector3(0, top * 0.45f, 0), 0.5f, top * 0.9f);
            const int layers = 20;
            for (int i = 0; i < layers; i++)
            {
                float k = i / (float)(layers - 1), y = 1f + k * (top - 3f);
                float sc = Mathf.Lerp(1f, 0.25f, k * k);
                var layer = Node(t, "Layer", new Vector3(0, y, 0), new Vector3(0, i * 9f, 0));
                for (int wg = 0; wg < 3; wg++)
                {
                    var wing = Node(layer, "Wing", Vector3.zero, new Vector3(0, wg * 120f, 0));
                    Ball(wing, i % 2 == 0 ? glass : glassDk, new Vector3(1.0f * sc, 0, 0), new Vector3(2.6f * sc, (top - 3f) / layers * 1.15f, 1.3f * sc));
                }
            }
            Cyl(t, H("#E9EEF4"), new Vector3(0, top, 0), 0.08f, 2.4f);
        }

        static void NagoyaStation(Transform t)
        {
            float u = 22f / 180f;   // same scale as the MIRAI TOWER
            Color podium = H("#DCD6CB"), glassA = H("#AFC0D2"), glassB = H("#C3CDD8"), band = H("#E9EEF4");
            // station building (podium) with horizontal glass bands
            Box(t, podium, new Vector3(0, 3.6f, 0), new Vector3(17f, 7.2f, 7f));
            for (int i = 1; i < 6; i++) Box(t, H("#9FB4C8"), new Vector3(0, i * 1.2f, -3.52f), new Vector3(16.6f, 0.35f, 0.04f), default, true);
            Box(t, H("#5E6E84"), new Vector3(0, 0.9f, -3.53f), new Vector3(6f, 1.6f, 0.04f));               // entrance
            // JR Central Towers: office tower (chamfered square, taller) and hotel tower (round)
            float officeTop = 245f * u, hotelTop = 226f * u;
            Frus(t, glassA, new Vector3(3.6f, 7.2f, 0), 2.7f, 2.6f, officeTop - 7.2f, new Vector3(0, 22.5f, 0), 8);
            for (float y = 9f; y < officeTop - 1; y += 2.2f) Box(t, band, new Vector3(3.6f, y, -2.48f), new Vector3(3.6f, 0.1f, 0.04f), default, true);
            Box(t, H("#8E9BAE"), new Vector3(3.6f, officeTop + 0.3f, 0), new Vector3(3f, 0.6f, 3f));
            Cyl(t, glassB, new Vector3(-3.6f, 7.2f + (hotelTop - 7.2f) / 2, 0), 2.4f, hotelTop - 7.2f);
            for (float y = 9f; y < hotelTop - 1; y += 2.2f) Box(t, band, new Vector3(-3.6f, y, -2.4f), new Vector3(3.2f, 0.1f, 0.04f), default, true);
            Cyl(t, H("#8E9BAE"), new Vector3(-3.6f, hotelTop + 0.25f, 0), 2.1f, 0.5f);
            // Midland Square: square tower with a sloping crown (open-air observation deck at the top)
            var ms = Node(t, "MidlandSquare", new Vector3(13.5f, 0, 2.5f));
            float msTop = 247f * u;
            Frus(ms, H("#8F9DB3"), Vector3.zero, 3.4f, 3.1f, msTop - 2.6f, new Vector3(0, 45, 0), 4);
            for (int i = -1; i <= 1; i++) Box(ms, H("#C9D3E0"), new Vector3(i * 0.8f, (msTop - 2.6f) / 2, -2.25f), new Vector3(0.08f, msTop - 3f, 0.04f), default, true);
            Part(ms, MeshKit.Prism(), H("#B6C2D2"), new Vector3(0, msTop - 2.6f, 0), new Vector3(4.2f, 2.6f, 4.2f), new Vector3(0, 0, 0));
            // Lucent Tower: slim, curved lens-shaped plan
            var lu = Node(t, "LucentTower", new Vector3(-13.5f, 0, 3f));
            var luBody = Cyl(lu, H("#B9D2CE"), new Vector3(0, 180f * u / 2, 0), 1.6f, 180f * u);
            luBody.localScale = new Vector3(1.6f, luBody.localScale.y, 4.6f);
            Ball(lu, H("#CFE2DE"), new Vector3(0, 180f * u, 0), new Vector3(1.6f, 1.2f, 4.6f));
            // Dai Nagoya Building with its name sign, in front
            var dn = Node(t, "DaiNagoya", new Vector3(9f, 0, -6f));
            Box(dn, H("#9AAFC6"), new Vector3(0, 175f * u / 2, 0), new Vector3(5.5f, 175f * u, 4f));
            for (float y = 1.5f; y < 175f * u; y += 1.6f) Box(dn, H("#C8D6E6"), new Vector3(0, y, -2.02f), new Vector3(5.3f, 0.08f, 0.04f), default, true);
            Box(dn, H("#EDE7DA"), new Vector3(0, 2.2f, -2.1f), new Vector3(5.7f, 4.4f, 0.2f));
            Text3D(dn, "大名古屋ビルヂング", H("#2A2A33"), new Vector3(0, 3.9f, -2.22f), 3.2f);
        }

        /// <summary>
        /// Oasis 21: the "Water Spaceship" — an oval glass roof filled with water, ringed by a walkway,
        /// floating above the sunken Galaxy Platform plaza in a green park, right beside the TV tower.
        /// </summary>
        static void Oasis21(Transform t)
        {
            // park slopes and the sunken plaza
            Ball(t, H("#8CC79A"), new Vector3(0, -0.4f, 0), new Vector3(15f, 1.4f, 9f));
            var pit = Cyl(t, H("#4A5068"), new Vector3(0, 0.32f, 0), 4.2f, 0.06f, default, true);
            pit.localScale = new Vector3(pit.localScale.x, pit.localScale.y, pit.localScale.z * 0.62f);
            // slanted supports and the glass lift tube
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Vector3 bottom = new Vector3(Mathf.Cos(a) * 2.6f, 0.3f, Mathf.Sin(a) * 1.6f);
                Vector3 topP = new Vector3(Mathf.Cos(a) * 4.2f, 2.0f, Mathf.Sin(a) * 2.6f);
                Beam(t, bottom, topP, 0.16f, H("#E4E8EE"));
            }
            Cyl(t, new Color(0.8f, 0.92f, 1f, 0.5f), new Vector3(1.5f, 1.2f, 0), 0.35f, 1.8f);
            // the oval glass roof, white rim walkway with lights, and the water on top
            var roof = Node(t, "WaterSpaceship", new Vector3(0, 2.0f, 0));
            roof.localScale = new Vector3(1f, 1f, 0.62f);
            Cyl(roof, H("#F1F4F8"), Vector3.zero, 5.6f, 0.18f);                                         // white frame
            Cyl(roof, new Color(0.62f, 0.84f, 1f, 0.55f), new Vector3(0, 0.22f, 0), 5.4f, 0.3f);       // glass body
            Cyl(roof, H("#F1F4F8"), new Vector3(0, 0.4f, 0), 5.6f, 0.06f);                              // walkway
            Cyl(roof, H("#8FD6F2"), new Vector3(0, 0.45f, 0), 4.5f, 0.05f, default, true);              // water
            for (int i = 1; i <= 3; i++) Part(roof, MeshKit.Torus(i * 1.2f, 0.025f, 32, 4), H("#D6F3FF"), new Vector3(0, 0.49f, 0), Vector3.one, new Vector3(90, 0, 0), true); // ripples
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI * 2 / 20;
                Ball(roof, H("#FFF2C0"), new Vector3(Mathf.Cos(a) * 5.05f, 0.5f, Mathf.Sin(a) * 5.05f), Vector3.one * 0.12f, default, true);
            }
        }

        static void Beam(Transform p, Vector3 a, Vector3 b, float th, Color c, bool unlit = false)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var tr = Part(p, MeshKit.Cube, c, (a + b) / 2, new Vector3(th, d.magnitude, th), default, unlit);
            tr.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        }

        static Vector3 Corner(float half, float y, int i) =>
            new Vector3(i == 0 || i == 3 ? -half : half, y, i < 2 ? -half : half);

        /// <summary>Square lattice section: corner posts, rings and X-bracing on every face.</summary>
        static void LatticeShaft(Transform t, float y0, float y1, float h0, float h1, int segments, float th, Color post, Color brace)
        {
            for (int k = 0; k < segments; k++)
            {
                float ya = Mathf.Lerp(y0, y1, k / (float)segments), yb = Mathf.Lerp(y0, y1, (k + 1) / (float)segments);
                float ha = Mathf.Lerp(h0, h1, k / (float)segments), hb = Mathf.Lerp(h0, h1, (k + 1) / (float)segments);
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    Beam(t, Corner(ha, ya, i), Corner(hb, yb, i), th, post);
                    Beam(t, Corner(hb, yb, i), Corner(hb, yb, j), th * 0.7f, brace);
                    Beam(t, Corner(ha, ya, i), Corner(hb, yb, j), th * 0.5f, brace);
                    Beam(t, Corner(ha, ya, j), Corner(hb, yb, i), th * 0.5f, brace);
                }
            }
        }

        /// <summary>
        /// Chubu Electric Power MIRAI TOWER (Nagoya TV Tower, 1954): silver steel lattice in the Eiffel style,
        /// four legs tied by concrete arches, a level at 30 m, enclosed observation deck at 90 m,
        /// open wire-mesh balcony at 100 m and antennas up to 180 m.
        /// </summary>
        static void MiraiTower(Transform t)
        {
            Color steel = H("#CDD2DB"), brace = H("#AEB4BF"), concrete = H("#DCD8D0"), glass = H("#53647E");
            const float top = 22f; float u = top / 180f;
            float yJoin = 30 * u, yDeck = 90 * u, yBal = 100 * u, yLat = 140 * u;
            float hb = 3.4f, hj = 1.15f, hd = 0.72f, ht = 0.34f;
            // four legs curving in from the wide base to the 30 m level
            for (int i = 0; i < 4; i++)
            {
                Vector3 prev = Corner(hb, 0, i);
                for (int k = 1; k <= 5; k++)
                {
                    float q = k / 5f;
                    Vector3 cur = Corner(Mathf.Lerp(hb, hj, 1 - (1 - q) * (1 - q)), yJoin * q, i);
                    Beam(t, prev, cur, 0.24f, steel);
                    prev = cur;
                }
                Box(t, concrete, Corner(hb, 0.15f, i), new Vector3(0.7f, 0.3f, 0.7f));
            }
            // concrete arches tying the legs together on all four sides
            for (int f = 0; f < 4; f++)
            {
                var arch = Node(t, "Arch", Vector3.zero, new Vector3(0, f * 90f, 0));
                Part(arch, MeshKit.Torus(1f, 0.07f, 20, 5, 180f), concrete, new Vector3(0, 0, -hb + 0.05f), new Vector3(hb * 0.9f, 1.5f, 1f));
            }
            // 30 m level (restaurant / elevator hall)
            Box(t, steel, new Vector3(0, yJoin, 0), new Vector3(hj * 2 + 0.7f, 0.3f, hj * 2 + 0.7f));
            Box(t, glass, new Vector3(0, yJoin + 0.35f, 0), new Vector3(hj * 2 + 0.4f, 0.4f, hj * 2 + 0.4f));
            // lattice up to the observation deck
            LatticeShaft(t, yJoin + 0.55f, yDeck - 0.45f, hj, hd, 6, 0.16f, steel, brace);
            // enclosed observation deck at 90 m (glass band) and the open mesh balcony at 100 m
            float dw = hd * 2 + 1.5f;
            Box(t, steel, new Vector3(0, yDeck - 0.5f, 0), new Vector3(dw + 0.2f, 0.14f, dw + 0.2f));
            Box(t, glass, new Vector3(0, yDeck, 0), new Vector3(dw, 0.85f, dw));
            for (int i = 0; i < 6; i++) Box(t, H("#E8ECF2"), new Vector3(-dw / 2 + (i + 0.5f) * dw / 6, yDeck, -dw / 2 - 0.02f), new Vector3(0.05f, 0.85f, 0.03f), default, true);
            Box(t, steel, new Vector3(0, yDeck + 0.5f, 0), new Vector3(dw + 0.2f, 0.14f, dw + 0.2f));
            LatticeShaft(t, yDeck + 0.57f, yBal - 0.1f, hd, hd * 0.95f, 1, 0.14f, steel, brace);
            Box(t, steel, new Vector3(0, yBal, 0), new Vector3(dw - 0.2f, 0.1f, dw - 0.2f));
            for (int i = 0; i < 4; i++)
            {
                var rail = Node(t, "Rail", new Vector3(0, yBal + 0.35f, 0), new Vector3(0, i * 90f, 0));
                Box(rail, H("#E3E7EE"), new Vector3(0, 0, -(dw - 0.2f) / 2), new Vector3(dw - 0.2f, 0.6f, 0.03f));
            }
            // lattice narrowing to the antenna section
            LatticeShaft(t, yBal + 0.1f, yLat, hd * 0.95f, ht, 5, 0.12f, steel, brace);
            Box(t, steel, new Vector3(0, yLat + 0.9f, 0), new Vector3(0.36f, 1.8f, 0.36f));   // square antenna prism
            Cyl(t, steel, new Vector3(0, (yLat + 1.8f + top) / 2, 0), 0.08f, top - yLat - 1.8f);
            Ball(t, H("#FF4F5E"), new Vector3(0, top, 0), Vector3.one * 0.32f, default, true);
        }

        /// <summary>
        /// Nagoya Castle, after the real proportions: a broad sloping stone base, five white storeys with
        /// flared green copper roofs and triangular gables, golden shachihoko on the ridge, and the smaller keep beside it.
        /// </summary>
        static void NagoyaCastle(Transform t)
        {
            Color stone = H("#A49C92"), stoneDark = H("#8E867C"), wall = H("#FBFAF6"), roof = H("#5E9E8A"), roofEdge = H("#4C8774"), win = H("#3E3E48");
            const float D = 0.82f;  // depth ratio
            // sloping stone base (石垣), two sections for a gentle curve
            Frus(t, stone, Vector3.zero, 11.6f, 10.2f, 2.6f, new Vector3(0, 45, 0), 4);
            Frus(t, stone, new Vector3(0, 2.6f, 0), 10.2f, 9.7f, 2.4f, new Vector3(0, 45, 0), 4);
            for (int i = 1; i < 5; i++) Box(t, stoneDark, new Vector3(0, i * 1.0f, -7.2f + i * 0.12f), new Vector3(15.5f - i * 0.6f, 0.05f, 0.05f), default, true);

            float y = 5f;
            float[] widths = { 12.6f, 11f, 9.4f, 7.6f, 5.8f };
            for (int k = 0; k < widths.Length; k++)
            {
                float wd = widths[k], h = 1.9f;
                Box(t, wall, new Vector3(0, y + h / 2, 0), new Vector3(wd, h, wd * D));
                int nw = Mathf.Max(2, Mathf.RoundToInt(wd / 1.6f));
                for (int i = 0; i < nw; i++)
                    Box(t, win, new Vector3(-wd / 2 + (i + 0.5f) * wd / nw, y + h * 0.55f, -wd * D / 2 - 0.02f), new Vector3(0.45f, 0.55f, 0.04f), default, true);
                // flared roof
                float rw = (wd + 1.6f) * 0.7071f;
                Frus(t, roofEdge, new Vector3(0, y + h, 0), rw, rw * 0.94f, 0.18f, new Vector3(0, 45, 0), 4);
                Frus(t, roof, new Vector3(0, y + h + 0.18f, 0), rw * 0.94f, (wd * 0.72f) * 0.7071f, 0.8f, new Vector3(0, 45, 0), 4);
                // triangular gables (千鳥破風) on the lower roofs
                if (k < 3)
                {
                    float gw = wd * 0.34f;
                    Part(t, MeshKit.Prism(), roof, new Vector3(0, y + h + 0.3f, -wd * D / 2 - 0.25f), new Vector3(1.0f, 1.2f, gw + 0.4f), new Vector3(0, 90, 0));
                    Part(t, MeshKit.Prism(), wall, new Vector3(0, y + h + 0.34f, -wd * D / 2 - 0.76f), new Vector3(0.05f, 0.9f, gw * 0.8f), new Vector3(0, 90, 0));
                }
                y += h + 0.98f;
            }
            // top hip-and-gable roof with golden shachihoko
            Part(t, MeshKit.Prism(), roof, new Vector3(0, y - 0.1f, 0), new Vector3(5.6f, 1.7f, 4.4f));
            Box(t, roofEdge, new Vector3(0, y + 1.62f, 0), new Vector3(5.2f, 0.16f, 0.25f));
            foreach (int sgn in new[] { -1, 1 })
            {
                var sh = Node(t, "Shachi", new Vector3(sgn * 2.5f, y + 2.1f, 0), new Vector3(0, 0, sgn * 25));
                Part(sh, MeshKit.Capsule, Palette.Gold, Vector3.zero, new Vector3(0.45f, 0.55f, 0.4f), default, false, 0.8f);
                Box(sh, Palette.Gold, new Vector3(sgn * 0.12f, 0.55f, 0), new Vector3(0.45f, 0.18f, 0.08f), new Vector3(0, 0, sgn * 35));
            }
            // small keep (小天守) to the left, joined by a corridor
            var s2 = Node(t, "SmallKeep", new Vector3(-12.5f, 0, 2f));
            Frus(s2, stone, Vector3.zero, 6.4f, 5.6f, 3.6f, new Vector3(0, 45, 0), 4);
            float y2 = 3.6f;
            foreach (float wd in new[] { 6.8f, 5.2f })
            {
                Box(s2, wall, new Vector3(0, y2 + 0.85f, 0), new Vector3(wd, 1.7f, wd * D));
                for (int i = 0; i < 3; i++) Box(s2, win, new Vector3(-wd / 3 + i * wd / 3, y2 + 0.95f, -wd * D / 2 - 0.02f), new Vector3(0.4f, 0.5f, 0.04f), default, true);
                float rw = (wd + 1.2f) * 0.7071f;
                Frus(s2, roof, new Vector3(0, y2 + 1.7f, 0), rw, wd * 0.5f, 0.75f, new Vector3(0, 45, 0), 4);
                y2 += 2.45f;
            }
            Part(s2, MeshKit.Prism(), roof, new Vector3(0, y2 - 0.1f, 0), new Vector3(3.6f, 1.2f, 2.8f));
            Box(t, stone, new Vector3(-6.8f, 2.2f, 1.5f), new Vector3(4f, 4.4f, 3f));
            Box(t, wall, new Vector3(-6.8f, 5f, 1.5f), new Vector3(3.6f, 1.2f, 2.4f));
        }

        void FarWedding(Transform p, System.Random r, float w)
        {
            Hills(p, r, w, H("#6FA87A"), H("#7DB585"), 12, 4, 8);
            // glowing sky lanterns drifting up in the twilight
            for (int i = 0; i < 26; i++)
            {
                var pos = new Vector3(Rf(r, -w / 2, w / 2), Rf(r, 9f, 27f), Rf(r, -6f, 10f));
                Frus(p, Pick(r, new[] { H("#FFC46B"), H("#FFD98A"), H("#FFB07A") }), pos, 0.35f, 0.5f, 0.8f, default, 6, true);
            }
            // flower lawn: dots of pink and white
            for (int i = 0; i < 70; i++)
                Ball(p, Pick(r, new[] { H("#FF8FB1"), Color.white, H("#FFD36E") }), new Vector3(Rf(r, -w / 2, w / 2), 0.15f, Rf(r, -18f, -8f)), new Vector3(0.6f, 0.25f, 0.6f), default, true);
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
                case 5: return MidNagoyaPort;
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
            // A real Hong Kong street: two-storey shops you can see into, neon signs, the Clock Tower and a Ding Ding tram
            // two rows of shops with an open harbour-front promenade between them (so the skyline stays visible)
            // just a few shops: 茶餐廳 + 金鋪 together, and a 餅家 further along; the rest is harbour-front promenade
            float[] xs = { -31f, -24.5f, 28f };
            int[] kinds = { 0, 1, 2 };
            string[] neonText = { "酒家", "冰室", "麻雀", "囍" };
            Color[] neon = { H("#FF4F9A"), H("#4FE3FF"), H("#FFD23F"), H("#FF6B4A") };
            for (int i = 0; i < xs.Length; i++)
            {
                HKShop(p, xs[i], 6f, kinds[i], i);
                if (i % 2 == 1)
                {
                    // vertical neon sign sticking out over the street
                    string text = neonText[(i / 2) % neonText.Length];
                    Color nc = neon[(i / 2) % neon.Length];
                    float sx = xs[i] + 2.4f, sy = 6.6f, hgt = text.Length * 1.05f + 0.5f;
                    Box(p, H("#3A3550"), new Vector3(sx, sy + hgt / 2 + 0.1f, -0.4f), new Vector3(0.1f, 0.1f, 1.8f));
                    Box(p, H("#2A2440"), new Vector3(sx, sy, -1.2f), new Vector3(1.3f, hgt, 0.3f));
                    Box(p, nc, new Vector3(sx, sy, -1.36f), new Vector3(1.12f, hgt - 0.2f, 0.04f), default, true);
                    Box(p, H("#1E1A30"), new Vector3(sx, sy, -1.39f), new Vector3(0.96f, hgt - 0.36f, 0.04f), default, true);
                    var t = Text3D(p, string.Join("\n", text.ToCharArray()), nc, new Vector3(sx, sy, -1.42f), 6.4f);
                    t.lineSpacing = -22;
                }
            }
            PawnSign(p, new Vector3(24.4f, 5.6f, -1.6f));
            ClockTower(Node(p, "ClockTower", new Vector3(-3f, 0, 1.2f)));
            foreach (float lx in new[] { -18f, -9f, 3.5f, 11f, 18f, 23f, 34f }) Lamp(p, lx, H("#3A3550"), H("#FFE6A8"));
            for (float bx = -16f; bx < 24f; bx += 4.5f)
            {
                if (bx > -6f && bx < 0f) continue;   // clock tower plaza
                // harbour-front benches
                Box(p, H("#8E6A4E"), new Vector3(bx, 0.45f, 1.6f), new Vector3(1.6f, 0.1f, 0.45f));
                Box(p, H("#8E6A4E"), new Vector3(bx, 0.75f, 1.85f), new Vector3(1.6f, 0.5f, 0.08f));
            }
            // harbour railing along the promenade
            Box(p, H("#C9C2D6"), new Vector3(3f, 0.9f, 2.6f), new Vector3(42f, 0.08f, 0.08f));
            for (float rx = -18f; rx <= 24f; rx += 2f) Cyl(p, H("#C9C2D6"), new Vector3(rx, 0.45f, 2.6f), 0.04f, 0.9f);
            DingDingTram(Node(p, "Tram", new Vector3(8f, 0, -2.7f)));
            // a red taxi
            var car = Node(p, "Taxi", new Vector3(-27f, 0, -2.6f));
            Box(car, H("#D9343A"), new Vector3(0, 0.75f, 0), new Vector3(3.6f, 0.8f, 1.6f));
            Box(car, H("#F2EEE8"), new Vector3(-0.2f, 1.4f, 0), new Vector3(2.0f, 0.65f, 1.5f));
            Box(car, H("#9FD4F0"), new Vector3(-0.2f, 1.42f, -0.76f), new Vector3(1.7f, 0.45f, 0.02f), default, true);
            Box(car, H("#FFE07A"), new Vector3(-0.2f, 1.85f, 0), new Vector3(0.5f, 0.2f, 0.3f), default, true);
            foreach (float wx in new[] { -1.2f, 1.2f }) Cyl(car, H("#1D1B22"), new Vector3(wx, 0.36f, -0.6f), 0.36f, 0.3f, new Vector3(90, 0, 0));
        }

        /// <summary>
        /// Hong Kong pawnshop sign (蝠鼠吊金錢): a bat (蝠 = 福, luck) on top holding a gourd-shaped board with 押,
        /// outlined in neon and hung high over the street.
        /// </summary>
        static void PawnSign(Transform p, Vector3 at)
        {
            var s = Node(p, "PawnSign", at);
            Color wood = H("#5A1E1E"), neon = H("#FF4F6A");
            Box(s, H("#3A3550"), new Vector3(0, 0.9f, 0.9f), new Vector3(0.08f, 0.08f, 1.8f));      // bracket to the wall
            Ball(s, wood, new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.4f, 0.2f));                    // the bat
            foreach (int sg in new[] { -1, 1 })
            {
                Box(s, wood, new Vector3(sg * 0.55f, 0.82f, 0), new Vector3(0.7f, 0.22f, 0.12f), new Vector3(0, 0, sg * 22f));
                Box(s, wood, new Vector3(sg * 0.98f, 1.02f, 0), new Vector3(0.3f, 0.14f, 0.12f), new Vector3(0, 0, sg * 50f));
                Box(s, neon, new Vector3(sg * 0.55f, 0.95f, -0.07f), new Vector3(0.7f, 0.035f, 0.02f), new Vector3(0, 0, sg * 22f), true);
            }
            Cyl(s, wood, new Vector3(0, 0.3f, 0), 0.06f, 0.5f);
            Cyl(s, wood, new Vector3(0, -0.1f, 0), 0.32f, 0.12f, new Vector3(90, 0, 0));                // gourd: small bulb
            Cyl(s, wood, new Vector3(0, -0.9f, 0), 0.62f, 0.12f, new Vector3(90, 0, 0));                // gourd: big bulb
            Part(s, MeshKit.Torus(0.62f, 0.035f, 24, 4), neon, new Vector3(0, -0.9f, -0.08f), Vector3.one, default, true);
            Part(s, MeshKit.Torus(0.32f, 0.03f, 18, 4), neon, new Vector3(0, -0.1f, -0.08f), Vector3.one, default, true);
            Text3D(s, "押", Palette.Gold, new Vector3(0, -0.92f, -0.1f), 8f);
        }

        /// <summary>
        /// Hong Kong shop with an open shopfront (interior visible): 0 茶餐廳, 1 金鋪, 2 餅家, 3 涼茶, 4 藥房.
        /// Ground floor shop + one residential floor with windows and air-conditioners.
        /// </summary>
        static void HKShop(Transform p, float x, float w, int kind, int seed)
        {
            var r = new System.Random(seed * 31 + kind);
            var s = Node(p, "Shop", new Vector3(x, 0, 0.6f));
            Color facade, inside, signBg, signFg, awn; string name; float nameSize = 5f;
            switch (kind)
            {
                case 0: facade = H("#3F7D5E"); inside = H("#FFE9C2"); signBg = H("#FFF4DC"); signFg = H("#C0392B"); awn = H("#2F6B4E"); name = "茶餐廳"; break;
                case 1: facade = H("#A8182C"); inside = H("#FFE7A0"); signBg = H("#7A0F1E"); signFg = Palette.Gold; awn = H("#8A1222"); name = "金鋪"; break;
                case 2: facade = H("#E9B44C"); inside = H("#FFF1D0"); signBg = H("#FFF8E8"); signFg = H("#8A4B2F"); awn = H("#D9534F"); name = "餅家"; break;
                case 3: facade = H("#5A3A28"); inside = H("#FFD9A0"); signBg = H("#2A1A12"); signFg = H("#FFE9B0"); awn = H("#3A2418"); name = "涼茶"; break;
                default: facade = H("#E8E6E0"); inside = H("#F2FFF6"); signBg = H("#1E7A4A"); signFg = Color.white; awn = H("#1E7A4A"); name = "藥房"; break;
            }
            // ---- ground floor: open shopfront
            Box(s, inside, new Vector3(0, 1.55f, 0.35f), new Vector3(w - 0.4f, 2.9f, 0.1f), default, true);   // lit back wall
            Box(s, H("#7A6A5C"), new Vector3(0, 0.04f, -0.5f), new Vector3(w - 0.4f, 0.08f, 1.7f));           // floor
            Box(s, facade, new Vector3(-w / 2 + 0.22f, 1.7f, -0.5f), new Vector3(0.44f, 3.4f, 1.7f));         // side pillars
            Box(s, facade, new Vector3(w / 2 - 0.22f, 1.7f, -0.5f), new Vector3(0.44f, 3.4f, 1.7f));
            Box(s, facade, new Vector3(0, 1.7f, 0.85f), new Vector3(w, 3.4f, 0.9f));                          // body behind
            Box(s, facade, new Vector3(0, 3.15f, -0.5f), new Vector3(w, 0.5f, 1.7f));                         // lintel
            Box(s, signBg, new Vector3(0, 3.15f, -1.38f), new Vector3(w - 0.5f, 0.78f, 0.06f), default, true); // sign board (lit)
            Text3D(s, name, signFg, new Vector3(0, 3.15f, -1.43f), nameSize);
            Box(s, awn, new Vector3(0, 2.62f, -1.75f), new Vector3(w - 0.4f, 0.07f, 0.9f), new Vector3(-16, 0, 0));
            Box(s, H("#FFF2C0"), new Vector3(0, 2.7f, -1.3f), new Vector3(w - 0.9f, 0.05f, 0.05f), default, true); // strip light

            switch (kind)
            {
                case 0: // 茶餐廳: booth seats, tables, menu boards
                    for (int i = 0; i < 3; i++)
                    {
                        float tx = -1.8f + i * 1.8f;
                        Box(s, H("#2E8B57"), new Vector3(tx - 0.5f, 0.45f, -0.4f), new Vector3(0.3f, 0.9f, 0.9f));
                        Box(s, H("#2E8B57"), new Vector3(tx + 0.5f, 0.45f, -0.4f), new Vector3(0.3f, 0.9f, 0.9f));
                        Box(s, H("#F5F0E6"), new Vector3(tx, 0.78f, -0.4f), new Vector3(0.65f, 0.06f, 0.75f));
                        Cyl(s, H("#9A9AA2"), new Vector3(tx, 0.4f, -0.4f), 0.04f, 0.75f);
                        Cyl(s, Color.white, new Vector3(tx - 0.12f, 0.86f, -0.5f), 0.06f, 0.1f);
                    }
                    Box(s, H("#C0392B"), new Vector3(-1.4f, 2.2f, 0.28f), new Vector3(1.6f, 0.6f, 0.04f), default, true);
                    Box(s, H("#F2C14E"), new Vector3(1.4f, 2.2f, 0.28f), new Vector3(1.6f, 0.6f, 0.04f), default, true);
                    Text3D(s, "奶茶", Color.white, new Vector3(-1.4f, 2.2f, 0.25f), 3f);
                    Text3D(s, "菠蘿油", H("#8A1F11"), new Vector3(1.4f, 2.2f, 0.25f), 3f);
                    break;
                case 1: // 金鋪: glowing glass counters full of gold
                    for (int i = 0; i < 3; i++)
                    {
                        float tx = -1.7f + i * 1.7f;
                        Box(s, H("#5A0A14"), new Vector3(tx, 0.45f, -0.55f), new Vector3(1.4f, 0.9f, 0.7f));
                        Box(s, H("#FFF6D8"), new Vector3(tx, 1.0f, -0.55f), new Vector3(1.4f, 0.22f, 0.7f), default, true);
                        for (int g = 0; g < 4; g++) Ball(s, Palette.Gold, new Vector3(tx - 0.45f + g * 0.3f, 1.16f, -0.75f), new Vector3(0.18f, 0.1f, 0.18f), default, true);
                    }
                    Cyl(s, Palette.Gold, new Vector3(0, 2.15f, 0.27f), 0.42f, 0.05f, new Vector3(90, 0, 0), true);
                    Text3D(s, "金", H("#A8182C"), new Vector3(0, 2.15f, 0.22f), 5f);
                    break;
                case 2: // 餅家: display case with egg tarts and pineapple buns
                    Box(s, H("#C9A27A"), new Vector3(0, 0.5f, -1.0f), new Vector3(w - 1.2f, 1.0f, 0.6f));
                    Box(s, H("#EAF6FF"), new Vector3(0, 1.1f, -1.0f), new Vector3(w - 1.2f, 0.2f, 0.6f), default, true);
                    for (int i = 0; i < 8; i++)
                    {
                        float tx = -2.0f + i * 0.57f;
                        Cyl(s, H("#D99A4E"), new Vector3(tx, 1.25f, -1.05f), 0.13f, 0.08f);
                        Cyl(s, H("#FFD84D"), new Vector3(tx, 1.29f, -1.05f), 0.1f, 0.02f, default, true);   // egg tart
                    }
                    for (int i = 0; i < 5; i++) Ball(s, H("#F2B640"), new Vector3(-1.6f + i * 0.8f, 1.9f, 0.1f), new Vector3(0.35f, 0.2f, 0.35f));
                    Box(s, H("#B98B5E"), new Vector3(0, 1.78f, 0.15f), new Vector3(w - 1.0f, 0.06f, 0.4f));
                    break;
                case 3: // 涼茶: bronze urns on the counter
                    Box(s, H("#3A2418"), new Vector3(0, 0.5f, -0.9f), new Vector3(w - 1.2f, 1.0f, 0.6f));
                    for (int i = 0; i < 3; i++)
                    {
                        float tx = -1.5f + i * 1.5f;
                        Frus(s, H("#B8803A"), new Vector3(tx, 1.0f, -0.85f), 0.3f, 0.36f, 0.75f, default, 12);
                        Ball(s, H("#C9923F"), new Vector3(tx, 1.78f, -0.85f), new Vector3(0.6f, 0.2f, 0.6f));
                        Cyl(s, H("#2A1A12"), new Vector3(tx, 1.06f, -1.0f), 0.08f, 0.1f);
                    }
                    Box(s, H("#C0392B"), new Vector3(0, 2.3f, 0.27f), new Vector3(2.2f, 0.5f, 0.04f), default, true);
                    Text3D(s, "廿四味", H("#FFE9B0"), new Vector3(0, 2.3f, 0.24f), 3f);
                    break;
                default: // 藥房: shelves of colourful boxes + green cross
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < 9; i++)
                            Box(s, Pick(r, new[] { H("#E5484D"), H("#3D7DFF"), H("#2FB37E"), H("#FFB020"), Color.white }), new Vector3(-2.2f + i * 0.55f, 0.9f + row * 0.6f, 0.2f), new Vector3(0.4f, 0.42f, 0.2f));
                    Box(s, H("#D9D9D9"), new Vector3(0, 0.5f, -0.9f), new Vector3(w - 1.4f, 1.0f, 0.55f));
                    Box(s, H("#3BE37A"), new Vector3(w / 2 - 0.2f, 4.6f, -1.6f), new Vector3(0.25f, 0.8f, 0.1f), default, true);
                    Box(s, H("#3BE37A"), new Vector3(w / 2 - 0.2f, 4.6f, -1.6f), new Vector3(0.8f, 0.25f, 0.1f), default, true);
                    break;
            }

            // ---- residential floor above: windows, air-conditioners, laundry
            Color[] upper = { H("#E8D5B5"), H("#C9D6C3"), H("#E3C4C4"), H("#CFD3DD"), H("#EAD9C6") };
            Color uc = upper[(seed + kind) % upper.Length];
            Box(s, uc, new Vector3(0, 4.7f, 0.15f), new Vector3(w, 2.6f, 2.3f));
            Box(s, Color.Lerp(uc, Color.black, 0.15f), new Vector3(0, 6.1f, 0.15f), new Vector3(w + 0.1f, 0.25f, 2.4f));
            for (int i = 0; i < 3; i++)
            {
                float wx = -w / 2 + (i + 0.5f) * w / 3;
                bool lit = r.NextDouble() < 0.55;
                Box(s, Color.Lerp(uc, Color.black, 0.3f), new Vector3(wx, 4.9f, -1.0f), new Vector3(1.15f, 1.15f, 0.05f));
                Box(s, lit ? H("#FFE3A3") : H("#5D6E8C"), new Vector3(wx, 4.9f, -1.03f), new Vector3(1.0f, 1.0f, 0.04f), default, true);
                Box(s, Color.Lerp(uc, Color.black, 0.3f), new Vector3(wx, 4.9f, -1.06f), new Vector3(0.05f, 1.0f, 0.03f));
                if (r.NextDouble() < 0.6) Box(s, H("#C9CCD2"), new Vector3(wx + 0.2f, 4.05f, -1.22f), new Vector3(0.7f, 0.42f, 0.42f));   // AC unit
            }
            if (r.NextDouble() < 0.5)
            {
                Cyl(s, H("#9A9AA2"), new Vector3(0, 5.6f, -1.5f), 0.025f, w * 0.8f, new Vector3(0, 0, 90));
                for (int i = 0; i < 4; i++) Box(s, Pick(r, new[] { H("#FF8FB1"), H("#8FC8F0"), Color.white, H("#FFD36E") }), new Vector3(-1.2f + i * 0.8f, 5.3f, -1.5f), new Vector3(0.45f, 0.55f, 0.02f));
            }
        }

        /// <summary>Tsim Sha Tsui Clock Tower: red brick with white bands, clocks and a small cupola.</summary>
        static void ClockTower(Transform t)
        {
            Color brick = H("#B5523B"), band = H("#F2EDE4");
            Box(t, H("#C9C2B8"), new Vector3(0, 0.15f, 0), new Vector3(3.0f, 0.3f, 3.0f));
            Box(t, brick, new Vector3(0, 4.3f, 0), new Vector3(2.2f, 8f, 2.2f));
            for (int i = 1; i <= 3; i++) Box(t, band, new Vector3(0, i * 2.2f, 0), new Vector3(2.3f, 0.18f, 2.3f));
            Box(t, H("#3A2A2A"), new Vector3(0, 1.1f, -1.11f), new Vector3(0.8f, 1.6f, 0.05f));
            Box(t, band, new Vector3(0, 8.5f, 0), new Vector3(2.5f, 0.4f, 2.5f));
            Cyl(t, Color.white, new Vector3(0, 7.4f, -1.12f), 0.62f, 0.05f, new Vector3(90, 0, 0), true);
            Box(t, H("#2A2230"), new Vector3(0, 7.55f, -1.16f), new Vector3(0.06f, 0.4f, 0.02f), default, true);
            Box(t, H("#2A2230"), new Vector3(0.14f, 7.4f, -1.16f), new Vector3(0.3f, 0.05f, 0.02f), default, true);
            Frus(t, brick, new Vector3(0, 8.7f, 0), 0.9f, 0.8f, 1.2f, default, 8);
            Ball(t, H("#5E9E8A"), new Vector3(0, 10.0f, 0), new Vector3(1.6f, 1.3f, 1.6f));
            Cyl(t, H("#4A4A55"), new Vector3(0, 11.0f, 0), 0.04f, 1.2f);
        }

        /// <summary>Hong Kong Ding Ding: a narrow green double-decker tram.</summary>
        static void DingDingTram(Transform t)
        {
            Color green = H("#2E7D4F"), cream = H("#F2EAD3");
            Box(t, green, new Vector3(0, 1.1f, 0), new Vector3(5.6f, 1.6f, 1.7f));
            Box(t, cream, new Vector3(0, 2.0f, 0), new Vector3(5.6f, 0.2f, 1.72f));
            Box(t, green, new Vector3(0, 2.85f, 0), new Vector3(5.6f, 1.5f, 1.7f));
            Box(t, H("#1F5A39"), new Vector3(0, 3.65f, 0), new Vector3(5.4f, 0.15f, 1.6f));
            for (int i = 0; i < 6; i++)
            {
                Box(t, H("#FFE6A8"), new Vector3(-2.3f + i * 0.92f, 1.35f, -0.86f), new Vector3(0.7f, 0.6f, 0.02f), default, true);
                Box(t, H("#FFE6A8"), new Vector3(-2.3f + i * 0.92f, 3.0f, -0.86f), new Vector3(0.7f, 0.55f, 0.02f), default, true);
            }
            Box(t, H("#FF8FB1"), new Vector3(0.6f, 2.45f, -0.87f), new Vector3(2.6f, 0.45f, 0.02f), default, true);   // advert
            Text3D(t, "囍", H("#C0392B"), new Vector3(0.6f, 2.45f, -0.89f), 2.4f);
            Cyl(t, H("#3A3A44"), new Vector3(0.6f, 4.4f, 0), 0.03f, 1.6f, new Vector3(0, 0, -35));
            foreach (float wx in new[] { -1.9f, 1.9f }) Cyl(t, H("#1D1B22"), new Vector3(wx, 0.25f, -0.6f), 0.25f, 0.2f, new Vector3(90, 0, 0));
        }

        void MidKinmen(Transform p, System.Random r, float w)
        {
            // A Kinmen village street like 水頭 / 珠山 / 山后: Minnan houses with red-tile roofs, granite bases and red-brick
            // walls (swallow-tail 燕尾 or horseback 馬背 ridges), a Nanyang-style Western mansion with its gun tower,
            // a granite memorial archway, red-brick walls with tile flowers, wind lion guardians and village trees.
            MinnanHouse(Node(p, "MinnanHouse", new Vector3(-28f, 0, 1.6f)), true, "穎川衍派");
            WindLionGod(Node(p, "WindLionGod", new Vector3(-19.6f, 0, -1.5f)));
            Casuarina(p, -17.4f, 0.6f);
            GunTower(Node(p, "GunTower", new Vector3(-14.4f, 0, 2.4f)), H("#EDE6DA"));
            NanyangMansion(Node(p, "NanyangMansion", new Vector3(-8.6f, 0, 2.2f)), H("#F2CFC8"), H("#FFF8EE"));
            TileFlowerWall(p, -3f, 3.2f, 0.4f);
            MemorialArch(Node(p, "MemorialArch", new Vector3(3.2f, 0, 0.4f)));
            MinnanHouse(Node(p, "MinnanHouse", new Vector3(20.2f, 0, 1.6f)), false, "西河衍派");
            WindLionGod(Node(p, "WindLionGod", new Vector3(29.2f, 0, -1.5f)));
            Banyan(p, 32.6f, 1.4f);
            // bougainvillea spilling over the walls
            foreach (float bx in new[] { -21.8f, -4.2f, 26.2f })
            {
                Ball(p, H("#D63A8E"), new Vector3(bx, 2.3f, 0.1f), new Vector3(1.6f, 1.1f, 1.1f));
                Ball(p, H("#E85AA6"), new Vector3(bx + 0.6f, 1.7f, -0.2f), new Vector3(1.1f, 0.9f, 0.9f));
            }

            // ---- sightseeing: the giant Kinmen Kaoliang liquor bottle
            var bt = Node(p, "KaoliangBottle", new Vector3(9.5f, 0, 2.2f));
            Color porcelain = H("#F4F2EE");
            Box(bt, H("#B9B3A8"), new Vector3(0, 0.4f, 0), new Vector3(2.4f, 0.8f, 2.4f));
            Frus(bt, porcelain, new Vector3(0, 0.8f, 0), 0.85f, 0.95f, 3.2f, default, 16);
            Frus(bt, porcelain, new Vector3(0, 4.0f, 0), 0.95f, 0.3f, 1.0f, default, 16);
            Cyl(bt, porcelain, new Vector3(0, 5.4f, 0), 0.28f, 0.8f);
            Cyl(bt, H("#C8102E"), new Vector3(0, 5.95f, 0), 0.33f, 0.32f);
            Box(bt, H("#C8102E"), new Vector3(0, 2.5f, -0.94f), new Vector3(1.0f, 1.8f, 0.04f));
            var lbl = Text3D(bt, "金門\n高粱", Palette.Gold, new Vector3(0, 2.5f, -0.97f), 3.4f);
            lbl.lineSpacing = -15;
        }

        // ------------------------------------------------------------------ Kinmen building kit
        static readonly Color KGranite = H("#E6E0D4"), KGraniteDk = H("#C9C1B2"), KBrick = H("#B8452F"), KBrickDk = H("#9C3524"),
                              KTile = H("#C9573B"), KTileDk = H("#A8432D"), KRidge = H("#9E3B28");
        static Mesh tileFlower;

        /// <summary>Minnan house (閩南古厝): granite base, red-brick walls, recessed entrance with door gods and family plaque, stone windows, side wings.</summary>
        static void MinnanHouse(Transform h, bool swallowTail, string plaque)
        {
            foreach (int sg in new[] { -1, 1 })
            {
                // side wings (櫸頭): lower, a little forward, with horseback gable ends
                var wing = Node(h, "Wing", new Vector3(sg * 5.4f, 0, -0.5f));
                Box(wing, KGranite, new Vector3(0, 0.45f, 0), new Vector3(3.0f, 0.9f, 2.6f));
                Box(wing, KBrick, new Vector3(0, 1.85f, 0), new Vector3(3.0f, 1.9f, 2.6f));
                for (float y = 1.2f; y < 2.8f; y += 0.3f) Box(wing, KBrickDk, new Vector3(0, y, -1.305f), new Vector3(3.0f, 0.03f, 0.01f));
                StoneWindow(wing, new Vector3(0, 1.9f, -1.33f), 0.9f, 0.8f);
                TiledRoof(wing, new Vector3(0, 2.8f, 0), 3.3f, 0.8f, 2.9f, 0.38f);
                Ball(wing, KRidge, new Vector3(sg * 1.55f, 3.45f, 0), new Vector3(0.55f, 0.55f, 0.35f));
            }
            // main hall
            Box(h, KGranite, new Vector3(0, 0.5f, 0), new Vector3(7.6f, 1.0f, 3.4f));
            Box(h, KGraniteDk, new Vector3(0, 1.0f, -1.71f), new Vector3(7.6f, 0.06f, 0.02f));
            Box(h, KGraniteDk, new Vector3(0, 0.5f, -1.71f), new Vector3(7.6f, 0.04f, 0.02f));
            Box(h, KBrick, new Vector3(0, 2.3f, 0), new Vector3(7.6f, 2.6f, 3.4f));
            for (float y = 1.3f; y < 3.55f; y += 0.3f) Box(h, KBrickDk, new Vector3(0, y, -1.705f), new Vector3(7.6f, 0.03f, 0.01f));
            // recessed entrance (凹壽): granite door frame, wooden doors with door gods, couplets, family plaque, lanterns
            Box(h, H("#5B2A22"), new Vector3(0, 1.95f, -1.708f), new Vector3(2.7f, 2.9f, 0.02f));
            foreach (int sg in new[] { -1, 1 }) Box(h, KGranite, new Vector3(sg * 0.79f, 1.7f, -1.73f), new Vector3(0.22f, 2.4f, 0.06f));
            Box(h, KGranite, new Vector3(0, 2.98f, -1.73f), new Vector3(1.8f, 0.22f, 0.06f));
            Box(h, H("#3E1F16"), new Vector3(0, 1.7f, -1.72f), new Vector3(1.36f, 2.4f, 0.02f));
            foreach (int sg in new[] { -1, 1 })
            {
                Box(h, H("#C8202E"), new Vector3(sg * 0.34f, 1.75f, -1.74f), new Vector3(0.52f, 1.6f, 0.01f), default, true);
                Cyl(h, Palette.Gold, new Vector3(sg * 0.34f, 2.1f, -1.75f), 0.11f, 0.01f, new Vector3(90, 0, 0), true);
                Box(h, H("#D7263D"), new Vector3(sg * 1.1f, 1.9f, -1.73f), new Vector3(0.26f, 1.7f, 0.01f), default, true);
                StoneWindow(h, new Vector3(sg * 2.65f, 2.25f, -1.72f), 1.1f, 1.0f);
                Ball(h, H("#E0303C"), new Vector3(sg * 1.32f, 2.95f, -1.98f), new Vector3(0.34f, 0.42f, 0.34f), default, true);
                Cyl(h, Palette.Gold, new Vector3(sg * 1.32f, 3.19f, -1.98f), 0.08f, 0.06f);
            }
            Box(h, H("#2A1E1A"), new Vector3(0, 3.36f, -1.74f), new Vector3(1.9f, 0.46f, 0.03f));
            Text3D(h, plaque, Palette.Gold, new Vector3(0, 3.36f, -1.77f), 2.2f);
            // roof: red tiles in rows, main ridge with cut-porcelain decoration and swallow-tail or horseback ends
            TiledRoof(h, new Vector3(0, 3.6f, 0), 8.2f, 1.6f, 4.0f, 0.36f);
            Box(h, KRidge, new Vector3(0, 5.3f, 0), new Vector3(6.6f, 0.3f, 0.36f));
            Box(h, H("#3FA37A"), new Vector3(-0.38f, 5.52f, 0), new Vector3(0.4f, 0.16f, 0.2f));
            Box(h, H("#F2C14E"), new Vector3(0, 5.56f, 0), new Vector3(0.3f, 0.22f, 0.2f));
            Box(h, H("#3D7DFF"), new Vector3(0.38f, 5.52f, 0), new Vector3(0.4f, 0.16f, 0.2f));
            foreach (int sg in new[] { -1, 1 })
            {
                if (swallowTail) SwallowTail(h, new Vector3(sg * 3.25f, 5.3f, 0), sg, 1f, KRidge);
                else Ball(h, KRidge, new Vector3(sg * 3.55f, 5.15f, 0), new Vector3(1.0f, 0.9f, 0.4f));
            }
        }

        /// <summary>Gable roof whose front slope faces the camera, with rows of tiles running down the slope.</summary>
        static void TiledRoof(Transform h, Vector3 basePos, float len, float height, float depth, float spacing)
        {
            Part(h, MeshKit.Prism(), KTile, basePos, new Vector3(len, height, depth));
            float half = depth / 2, slope = Mathf.Sqrt(height * height + half * half);
            float ang = Mathf.Atan2(height, half) * Mathf.Rad2Deg;
            Vector3 normal = new Vector3(0, half, -height) / slope;
            Vector3 mid = basePos + new Vector3(0, height / 2, -half / 2) + normal * 0.03f;
            for (float x = -len / 2 + spacing / 2; x < len / 2; x += spacing)
                Box(h, KTileDk, mid + new Vector3(x, 0, 0), new Vector3(0.07f, 0.03f, slope), new Vector3(-ang, 0, 0));
            Box(h, KTileDk, basePos + new Vector3(0, 0.05f, -half), new Vector3(len, 0.1f, 0.12f));
        }

        /// <summary>Swallow-tail ridge end (燕尾): the ridge sweeps upwards and splits into two tips.</summary>
        static void SwallowTail(Transform h, Vector3 start, int sg, float s, Color c)
        {
            Vector3 at = start;
            foreach (float a in new[] { 8f, 20f, 34f })
            {
                var d = new Vector3(sg * Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
                float len = 0.55f * s;
                Box(h, c, at + d * (len / 2), new Vector3(len + 0.06f * s, 0.26f * s, 0.32f * s), new Vector3(0, 0, sg * a));
                at += d * len;
            }
            foreach (float a in new[] { 60f, 36f })
            {
                var d = new Vector3(sg * Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0);
                float len = 0.7f * s;
                Box(h, c, at + d * (len / 2), new Vector3(len, 0.09f * s, 0.26f * s), new Vector3(0, 0, sg * a));
            }
        }

        /// <summary>Granite window with stone bars (石窗).</summary>
        static void StoneWindow(Transform h, Vector3 c, float w, float hgt)
        {
            Box(h, KGranite, c, new Vector3(w, hgt, 0.06f));
            Box(h, H("#3A2620"), c + new Vector3(0, 0, -0.02f), new Vector3(w * 0.76f, hgt * 0.74f, 0.04f));
            for (int k = 0; k < 5; k++)
                Box(h, KGranite, c + new Vector3(-w * 0.3f + k * w * 0.15f, 0, -0.05f), new Vector3(0.06f, hgt * 0.74f, 0.03f));
        }

        /// <summary>Nanyang-style Western mansion (洋樓) built by merchants back from Southeast Asia: two storeys of arcades, green shutters, a parapet with a pediment.</summary>
        static void NanyangMansion(Transform m, Color wall, Color trim)
        {
            Box(m, KGranite, new Vector3(0, 0.25f, 0), new Vector3(8.6f, 0.5f, 4.4f));
            Box(m, Color.Lerp(wall, Color.black, 0.18f), new Vector3(0, 3.6f, 0.6f), new Vector3(8f, 6.2f, 3.0f));   // back wall, in the arcade's shade
            foreach (int sg in new[] { -1, 1 }) Box(m, wall, new Vector3(sg * 4.15f, 3.6f, -0.6f), new Vector3(0.3f, 6.2f, 2.6f));
            if (tileFlower == null) tileFlower = MeshKit.Torus(0.14f, 0.035f, 12, 4);
            var arch = MeshKit.Torus(0.74f, 0.1f, 16, 4, 180f);
            foreach (float fy in new[] { 0.5f, 3.6f })
            {
                for (int b = 0; b < 4; b++)
                {
                    float xc = -2.85f + b * 1.9f;
                    Box(m, H("#3E8C7E"), new Vector3(xc, fy + 1.15f, -0.92f), new Vector3(0.62f, 1.5f, 0.04f));      // green shutters
                    Box(m, Color.white, new Vector3(xc, fy + 1.15f, -0.95f), new Vector3(0.04f, 1.5f, 0.02f));
                    Part(m, arch, trim, new Vector3(xc, fy + 1.86f, -1.85f), Vector3.one);
                    foreach (int sg in new[] { -1, 1 }) Box(m, wall, new Vector3(xc + sg * 0.6f, fy + 2.48f, -1.85f), new Vector3(0.38f, 0.45f, 0.3f));
                }
                for (int c = 0; c < 5; c++)
                    Box(m, trim, new Vector3(-3.8f + c * 1.9f, fy + 1.35f, -1.85f), new Vector3(0.42f, 2.7f, 0.42f));
                Box(m, wall, new Vector3(0, fy + 2.85f, -1.75f), new Vector3(8.4f, 0.4f, 0.6f));
            }
            // balustrade on the upper veranda
            Box(m, trim, new Vector3(0, 4.45f, -1.9f), new Vector3(8f, 0.1f, 0.3f));
            for (float x = -3.6f; x <= 3.6f; x += 0.3f)
                if (Mathf.Abs(Mathf.Repeat(x + 3.8f, 1.9f)) > 0.3f) Cyl(m, trim, new Vector3(x, 4.05f, -1.9f), 0.045f, 0.75f);
            // parapet and pediment
            Box(m, trim, new Vector3(0, 6.75f, -1.7f), new Vector3(8.8f, 0.12f, 0.7f));
            Box(m, wall, new Vector3(0, 7.15f, -1.6f), new Vector3(8.6f, 0.7f, 0.5f));
            Part(m, MeshKit.Prism(), wall, new Vector3(0, 7.5f, -1.6f), new Vector3(3.0f, 1.0f, 0.5f));
            Cyl(m, H("#D7263D"), new Vector3(0, 7.8f, -1.87f), 0.25f, 0.04f, new Vector3(90, 0, 0));
        }

        /// <summary>Gun tower (銃樓) that guarded the mansions against pirates: tall, small gun slits, a crenellated top.</summary>
        static void GunTower(Transform t, Color wall)
        {
            Box(t, KGranite, new Vector3(0, 0.9f, 0), new Vector3(2.6f, 1.8f, 2.6f));
            Box(t, wall, new Vector3(0, 4.9f, 0), new Vector3(2.4f, 6.2f, 2.4f));
            for (int f = 0; f < 3; f++)
                foreach (float x in new[] { -0.55f, 0.55f })
                    Box(t, H("#2A2020"), new Vector3(x, 2.6f + f * 1.7f, -1.21f), new Vector3(0.14f, 0.45f, 0.02f));
            Box(t, H("#3E8C7E"), new Vector3(0, 7.25f, -1.21f), new Vector3(0.6f, 0.8f, 0.02f));
            Box(t, KGraniteDk, new Vector3(0, 8.1f, 0), new Vector3(2.8f, 0.2f, 2.8f));
            Box(t, wall, new Vector3(0, 8.3f, 0), new Vector3(2.6f, 0.3f, 2.6f));
            for (int k = 0; k < 4; k++) Box(t, wall, new Vector3(-1.05f + k * 0.7f, 8.6f, -1.15f), new Vector3(0.4f, 0.5f, 0.3f));
        }

        /// <summary>Granite memorial archway (牌坊): four pillars and three bays, carved panels, a plaque and tiered roofs.</summary>
        static void MemorialArch(Transform a)
        {
            Color st = H("#CFC8BB"), dk = H("#A9A196"), roof = H("#B8B0A3");
            foreach (float x in new[] { -2.7f, -0.95f, 0.95f, 2.7f })
            {
                float hgt = Mathf.Abs(x) > 2f ? 3.75f : 4.8f;
                Box(a, st, new Vector3(x, hgt / 2, 0), new Vector3(0.5f, hgt, 0.5f));
                Box(a, dk, new Vector3(x, 0.35f, 0), new Vector3(0.8f, 0.7f, 0.9f));
            }
            foreach (int sg in new[] { -1, 1 })
            {
                float cx = sg * 1.825f;
                Box(a, st, new Vector3(cx, 2.9f, 0), new Vector3(1.9f, 0.3f, 0.4f));
                Box(a, H("#E2DCD0"), new Vector3(cx, 3.25f, -0.05f), new Vector3(1.5f, 0.4f, 0.3f));
                Box(a, st, new Vector3(cx, 3.6f, 0), new Vector3(1.9f, 0.25f, 0.4f));
                Part(a, MeshKit.Prism(), roof, new Vector3(cx, 3.75f, 0), new Vector3(2.3f, 0.55f, 1.0f));
                SwallowTail(a, new Vector3(cx + sg * 0.9f, 4.25f, 0), sg, 0.35f, dk);
            }
            Box(a, st, new Vector3(0, 3.3f, 0), new Vector3(2.0f, 0.3f, 0.4f));
            Box(a, H("#7F786C"), new Vector3(0, 3.85f, -0.1f), new Vector3(1.5f, 0.7f, 0.2f));
            Text3D(a, "節孝", Palette.Gold, new Vector3(0, 3.85f, -0.22f), 3.2f);
            Box(a, st, new Vector3(0, 4.35f, 0), new Vector3(2.0f, 0.3f, 0.4f));
            Part(a, MeshKit.Prism(), roof, new Vector3(0, 4.8f, 0), new Vector3(2.8f, 0.7f, 1.1f));
            Box(a, dk, new Vector3(0, 5.52f, 0), new Vector3(2.0f, 0.12f, 0.15f));
            foreach (int sg in new[] { -1, 1 }) SwallowTail(a, new Vector3(sg * 1.0f, 5.52f, 0), sg, 0.45f, dk);
        }

        /// <summary>Red-brick courtyard wall on a granite base with red-tile flower openings (瓦花).</summary>
        static void TileFlowerWall(Transform p, float xc, float len, float z)
        {
            if (tileFlower == null) tileFlower = MeshKit.Torus(0.14f, 0.035f, 12, 4);
            Box(p, KGranite, new Vector3(xc, 0.3f, z), new Vector3(len, 0.6f, 0.4f));
            Box(p, KBrick, new Vector3(xc, 1.3f, z), new Vector3(len, 1.4f, 0.36f));
            Box(p, KTileDk, new Vector3(xc, 2.05f, z), new Vector3(len + 0.2f, 0.12f, 0.5f));
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.1f));
            for (int i = 0; i < n; i++)
            {
                float x = xc - len / 2 + (i + 0.5f) * len / n;
                Box(p, H("#5B2A22"), new Vector3(x, 1.45f, z - 0.19f), new Vector3(0.62f, 0.62f, 0.02f));
                for (int k = 0; k < 4; k++)
                    Part(p, tileFlower, KTile, new Vector3(x + (k % 2 == 0 ? -0.14f : 0.14f), 1.45f + (k < 2 ? 0.14f : -0.14f), z - 0.21f), Vector3.one);
            }
        }

        /// <summary>Casuarina (木麻黃), the tall wind-break tree along Kinmen's roads.</summary>
        static void Casuarina(Transform p, float x, float z, float s = 1f)
        {
            Cyl(p, H("#6E5440"), new Vector3(x, 2.6f * s, z), 0.14f * s, 5.2f * s);
            Color a = H("#3E6E4A"), b = H("#4E8059");
            Ball(p, a, new Vector3(x, 3.4f * s, z), new Vector3(1.9f, 1.5f, 1.9f) * s);
            Ball(p, b, new Vector3(x + 0.2f * s, 4.5f * s, z), new Vector3(1.6f, 1.5f, 1.6f) * s);
            Ball(p, a, new Vector3(x - 0.1f * s, 5.5f * s, z), new Vector3(1.2f, 1.4f, 1.2f) * s);
            Ball(p, b, new Vector3(x, 6.3f * s, z), new Vector3(0.7f, 1.0f, 0.7f) * s);
        }

        /// <summary>The big banyan tree (榕樹) of the village square, with hanging aerial roots.</summary>
        static void Banyan(Transform p, float x, float z)
        {
            Color bark = H("#7A6656");
            Cyl(p, bark, new Vector3(x, 1.3f, z), 0.5f, 2.6f);
            foreach (int sg in new[] { -1, 1 }) Cyl(p, bark, new Vector3(x + sg * 0.6f, 2.7f, z), 0.2f, 1.6f, new Vector3(0, 0, -sg * 35f));
            Color a = H("#3F7F4E"), b = H("#4F9159");
            Ball(p, a, new Vector3(x, 3.9f, z), new Vector3(5.6f, 2.6f, 3.6f));
            Ball(p, b, new Vector3(x - 1.9f, 3.5f, z - 0.4f), new Vector3(3.0f, 2.0f, 2.6f));
            Ball(p, b, new Vector3(x + 2.0f, 3.6f, z - 0.3f), new Vector3(3.2f, 2.0f, 2.6f));
            Ball(p, a, new Vector3(x + 0.4f, 4.8f, z), new Vector3(3.4f, 1.8f, 2.8f));
            for (int i = 0; i < 7; i++) Cyl(p, H("#8A7460"), new Vector3(x - 2.4f + i * 0.8f, 2.0f, z - 0.9f), 0.03f, 1.6f);
        }

        /// <summary>
        /// Kinmen Wind Lion God (風獅爺): a standing stone lion guardian on a pedestal — big head with a curly mane,
        /// bulging eyes and a toothy grin, a red cape tied at the neck, a bell on the chest and a 令 flag in its paw.
        /// </summary>
        static void WindLionGod(Transform t)
        {
            Color stone = H("#B2ADA4"), stoneDk = H("#8F8A82"), stoneLt = H("#C9C4BB"), red = H("#D7263D"), dark = H("#2A2230");
            Box(t, stoneDk, new Vector3(0, 0.2f, 0), new Vector3(1.5f, 0.4f, 1.4f));
            Box(t, stone, new Vector3(0, 0.55f, 0), new Vector3(1.2f, 0.3f, 1.1f));
            Frus(t, H("#9C6B2E"), new Vector3(0, 0, -1.0f), 0.18f, 0.24f, 0.3f, default, 10);              // incense burner
            for (int i = -1; i <= 1; i++)
            {
                Box(t, H("#8A3A2A"), new Vector3(i * 0.06f, 0.45f, -1.0f), new Vector3(0.02f, 0.35f, 0.02f));
                Ball(t, H("#FF8A3D"), new Vector3(i * 0.06f, 0.63f, -1.0f), Vector3.one * 0.04f, default, true);
            }

            var b = Node(t, "Lion", new Vector3(0, 0.7f, 0));
            foreach (int sg in new[] { -1, 1 })
            {
                Part(b, MeshKit.Capsule, stone, new Vector3(sg * 0.2f, 0.3f, 0), new Vector3(0.32f, 0.3f, 0.32f));
                Ball(b, stoneLt, new Vector3(sg * 0.22f, 0.06f, -0.12f), new Vector3(0.32f, 0.16f, 0.4f));
            }
            Part(b, MeshKit.Capsule, stone, new Vector3(0, 1.0f, 0), new Vector3(0.82f, 0.55f, 0.66f));      // upright body
            Ball(b, stoneLt, new Vector3(0, 0.92f, -0.24f), new Vector3(0.55f, 0.62f, 0.3f));                // belly
            Box(b, red, new Vector3(0, 1.02f, 0.34f), new Vector3(0.88f, 1.05f, 0.06f), new Vector3(-8, 0, 0)); // red cape
            Ball(b, red, new Vector3(0, 1.47f, -0.27f), new Vector3(0.3f, 0.2f, 0.2f));
            foreach (int sg in new[] { -1, 1 }) Box(b, red, new Vector3(sg * 0.1f, 1.32f, -0.33f), new Vector3(0.08f, 0.26f, 0.03f), new Vector3(0, 0, sg * 20));
            Ball(b, Palette.Gold, new Vector3(0, 1.25f, -0.37f), Vector3.one * 0.13f);                    // bell
            // left paw on the chest, right paw holding a 令 flag
            Part(b, MeshKit.Capsule, stone, new Vector3(-0.33f, 1.1f, -0.2f), new Vector3(0.2f, 0.25f, 0.2f), new Vector3(40, 0, -30));
            Ball(b, stoneLt, new Vector3(-0.2f, 1.0f, -0.38f), Vector3.one * 0.17f);
            Part(b, MeshKit.Capsule, stone, new Vector3(0.42f, 1.2f, -0.16f), new Vector3(0.2f, 0.27f, 0.2f), new Vector3(20, 0, 50));
            Ball(b, stoneLt, new Vector3(0.62f, 1.36f, -0.28f), Vector3.one * 0.17f);
            Cyl(b, H("#6E4A3A"), new Vector3(0.64f, 1.75f, -0.28f), 0.025f, 1.3f);
            Box(b, red, new Vector3(0.88f, 2.15f, -0.28f), new Vector3(0.44f, 0.5f, 0.02f));                 // 令 flag held out to the side
            Text3D(b, "令", Palette.Gold, new Vector3(0.88f, 2.15f, -0.3f), 2.6f);
            Part(b, MeshKit.Torus(0.18f, 0.06f, 12, 5, 270f), stone, new Vector3(0, 0.7f, 0.42f), Vector3.one, new Vector3(0, 90, 0)); // tail

            // big head with curly mane and a fierce, friendly grin
            var h = Node(b, "Head", new Vector3(0, 1.92f, -0.02f));
            for (int i = 0; i < 12; i++)
            {
                float a = (15 + i * 30) * Mathf.Deg2Rad;
                Ball(h, stoneDk, new Vector3(Mathf.Cos(a) * 0.52f, Mathf.Sin(a) * 0.44f + 0.02f, 0.12f), Vector3.one * 0.28f);
            }
            Ball(h, stone, Vector3.zero, new Vector3(1.0f, 0.86f, 0.85f));
            foreach (int sg in new[] { -1, 1 })
            {
                Ball(h, stoneDk, new Vector3(sg * 0.38f, 0.34f, 0.04f), new Vector3(0.18f, 0.16f, 0.1f));            // ears
                Box(h, stoneDk, new Vector3(sg * 0.18f, 0.21f, -0.37f), new Vector3(0.27f, 0.07f, 0.08f), new Vector3(0, 0, sg * -14));   // brows
                Ball(h, Color.white, new Vector3(sg * 0.18f, 0.08f, -0.37f), new Vector3(0.22f, 0.2f, 0.12f), default, true);            // bulging eyes
                Ball(h, dark, new Vector3(sg * 0.18f, 0.08f, -0.44f), new Vector3(0.1f, 0.1f, 0.05f), default, true);
                Box(h, Color.white, new Vector3(sg * 0.21f, -0.23f, -0.44f), new Vector3(0.05f, 0.1f, 0.03f), default, true);          // fangs
            }
            Ball(h, stoneLt, new Vector3(0, -0.03f, -0.43f), new Vector3(0.25f, 0.18f, 0.17f));                // nose
            Box(h, H("#7A2030"), new Vector3(0, -0.2f, -0.4f), new Vector3(0.52f, 0.16f, 0.06f));               // mouth
            for (int i = 0; i < 6; i++) Box(h, Color.white, new Vector3(-0.2f + i * 0.08f, -0.14f, -0.44f), new Vector3(0.06f, 0.06f, 0.03f), default, true);
            for (int i = 0; i < 3; i++) Ball(h, stoneDk, new Vector3(-0.2f + i * 0.2f, 0.42f, -0.05f), Vector3.one * 0.24f);   // top curls
        }

        void MidTaipei(Transform p, System.Random r, float w)
        {
            // Taipei night market: glowing stall signs, bulbs, lanterns overhead, steam, visitors and an entrance gate
            Color[] awn = { H("#FF6B6B"), H("#FFD166"), H("#4ECDC4"), H("#FF8FB1") };
            Color[] sign = { H("#FFE14D"), H("#FF5C8A"), H("#5CE1FF"), H("#FFFFFF"), H("#FF9A3D") };
            string[] names = { "雞排", "珍珠奶茶", "臭豆腐", "蚵仔煎", "小籠包", "滷味", "大腸包小腸", "刈包" };
            int k = 0;
            for (float x = -w / 2 + 2; x < w / 2 - 6; x += 7.2f, k++)
            {
                if (k == 4) { NightMarketGate(p, x + 1.5f); x += 3f; continue; }
                var st = Node(p, "Stall", new Vector3(x, 0, 0.8f));
                Box(st, H("#F4EDE4"), new Vector3(0, 0.55f, 0), new Vector3(3.4f, 1.1f, 1.5f));
                Box(st, H("#B98B5E"), new Vector3(0, 1.15f, 0), new Vector3(3.6f, 0.1f, 1.7f));
                // colourful food on the counter
                for (int i = 0; i < 6; i++) Box(st, Pick(r, new[] { H("#E07A3A"), H("#F2C14E"), H("#8BC34A"), H("#C0392B") }), new Vector3(-1.3f + i * 0.52f, 1.26f, -0.3f), new Vector3(0.4f, 0.12f, 0.35f));
                Cyl(st, H("#4A4552"), new Vector3(-1.65f, 1.9f, -0.7f), 0.05f, 1.6f);
                Cyl(st, H("#4A4552"), new Vector3(1.65f, 1.9f, -0.7f), 0.05f, 1.6f);
                Color ac = awn[k % awn.Length];
                for (int i = 0; i < 6; i++)
                    Box(st, i % 2 == 0 ? ac : Color.white, new Vector3(-1.55f + i * 0.62f, 2.75f, -0.35f), new Vector3(0.62f, 0.1f, 2.1f), new Vector3(-12, 0, 0));
                // bulbs along the awning edge
                for (int i = 0; i < 8; i++) Ball(st, H("#FFF2B0"), new Vector3(-1.6f + i * 0.46f, 2.45f, -1.35f), Vector3.one * 0.14f, default, true);
                // glowing sign
                Color sc = sign[k % sign.Length];
                Box(st, H("#1E1A26"), new Vector3(0, 3.45f, 0.35f), new Vector3(2.9f, 0.85f, 0.12f));
                Box(st, sc, new Vector3(0, 3.45f, 0.27f), new Vector3(2.7f, 0.7f, 0.03f), default, true);
                Text3D(st, names[k % names.Length], H("#2A1E26"), new Vector3(0, 3.45f, 0.24f), names[k % names.Length].Length > 3 ? 4f : 5.2f);
                // steam rising from the food
                for (int i = 0; i < 3; i++) Ball(st, new Color(1, 1, 1, 0.35f), new Vector3(-0.6f + i * 0.6f, 1.7f + i * 0.25f, -0.2f), new Vector3(0.45f, 0.35f, 0.35f));
                // visitors in front of the stall
                if (k % 2 == 0)
                {
                    var g1 = ModelFactory.BuildGuest(p, 500 + k * 7, out _, out _);
                    g1.localPosition = new Vector3(x - 0.7f, 0, -0.9f); g1.localEulerAngles = new Vector3(0, -80, 0);
                    var g2 = ModelFactory.BuildGuest(p, 900 + k * 13, out _, out _);
                    g2.localPosition = new Vector3(x + 0.9f, 0, -1.1f); g2.localEulerAngles = new Vector3(0, -100, 0);
                }
            }
            // red lanterns strung across the whole street
            for (float x = -w / 2; x < w / 2; x += 1.6f)
            {
                float ly = 5.6f - Mathf.Abs(Mathf.Sin((x + w / 2) / 9.6f * Mathf.PI)) * 0.5f;
                Ball(p, H("#FF3B3B"), new Vector3(x, ly, -0.6f), new Vector3(0.6f, 0.72f, 0.6f), default, true);
                Box(p, Palette.Gold, new Vector3(x, ly + 0.42f, -0.6f), new Vector3(0.28f, 0.1f, 0.28f), default, true);
                Box(p, Palette.Gold, new Vector3(x, ly - 0.42f, -0.6f), new Vector3(0.2f, 0.1f, 0.2f), default, true);
            }
            for (float x = -w / 2; x < w / 2; x += 9.6f) Cyl(p, H("#4A4552"), new Vector3(x, 2.9f, -0.6f), 0.07f, 5.8f);
            // warm string lights behind
            for (float x = -w / 2; x < w / 2; x += 0.8f)
                Ball(p, H("#FFE7A0"), new Vector3(x, 4.4f + Mathf.Sin(x * 0.7f) * 0.15f, 1.9f), Vector3.one * 0.13f, default, true);
        }

        static void NightMarketGate(Transform p, float x)
        {
            var g = Node(p, "Gate", new Vector3(x, 0, 0.4f));
            Color red = H("#C8102E");
            Cyl(g, red, new Vector3(-2.6f, 2.8f, 0), 0.3f, 5.6f);
            Cyl(g, red, new Vector3(2.6f, 2.8f, 0), 0.3f, 5.6f);
            Box(g, red, new Vector3(0, 5.8f, 0), new Vector3(6.6f, 1.3f, 0.5f));
            Box(g, Palette.Gold, new Vector3(0, 5.8f, -0.26f), new Vector3(6.2f, 1.0f, 0.03f), default, true);
            Box(g, H("#8A0A1E"), new Vector3(0, 6.6f, 0), new Vector3(7.4f, 0.3f, 0.8f));
            Text3D(g, "夜市", red, new Vector3(0, 5.8f, -0.29f), 9f);
            for (int i = 0; i < 14; i++) Ball(g, H("#FFF2B0"), new Vector3(-3.25f + i * 0.5f, 6.95f, -0.2f), Vector3.one * 0.16f, default, true);
            for (int i = 0; i < 10; i++)
            {
                Ball(g, H("#FFF2B0"), new Vector3(-2.6f, 0.5f + i * 0.5f, -0.32f), Vector3.one * 0.12f, default, true);
                Ball(g, H("#FFF2B0"), new Vector3(2.6f, 0.5f + i * 0.5f, -0.32f), Vector3.one * 0.12f, default, true);
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

            // ---- sightseeing: a big vermilion torii (shrine gate)
            var to = Node(p, "Torii", new Vector3(2.5f, 0, -0.6f));
            Color verm = H("#E0452B"), blk = H("#2A2230");
            Cyl(to, verm, new Vector3(-2.1f, 2.9f, 0), 0.26f, 5.8f);
            Cyl(to, verm, new Vector3(2.1f, 2.9f, 0), 0.26f, 5.8f);
            Box(to, blk, new Vector3(-2.1f, 0.2f, 0), new Vector3(0.7f, 0.4f, 0.7f));
            Box(to, blk, new Vector3(2.1f, 0.2f, 0), new Vector3(0.7f, 0.4f, 0.7f));
            Box(to, verm, new Vector3(0, 4.7f, 0), new Vector3(5.4f, 0.32f, 0.4f));
            Box(to, verm, new Vector3(0, 5.75f, 0), new Vector3(6.4f, 0.4f, 0.55f));
            Box(to, blk, new Vector3(0, 6.05f, 0), new Vector3(6.9f, 0.22f, 0.65f));
            Box(to, blk, new Vector3(-3.45f, 6.15f, 0), new Vector3(0.6f, 0.2f, 0.65f), new Vector3(0, 0, 12));
            Box(to, blk, new Vector3(3.45f, 6.15f, 0), new Vector3(0.6f, 0.2f, 0.65f), new Vector3(0, 0, -12));
            Box(to, verm, new Vector3(0, 5.2f, 0), new Vector3(0.35f, 0.8f, 0.3f));
            Box(to, blk, new Vector3(0, 5.2f, -0.17f), new Vector3(0.6f, 0.7f, 0.04f));
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

        // ------------------------------------------------------------------ near sidewalk (front layer, z≈-4.3)
        // Kept under ~1 m so it never hides the lane the couple runs on.

        System.Action<Transform, System.Random, float> FrontBuilder(int i)
        {
            switch (i)
            {
                case 0: return FrontHongKong;
                case 1: return FrontKinmen;
                case 2: return FrontTaipei;
                case 3: return FrontNagoya;
                case 5: return FrontNagoyaPort;
                default: return FrontWedding;
            }
        }

        static void PavingLines(Transform p, float w, Color c)
        {
            for (float x = -w / 2; x < w / 2; x += 1.5f)
                Box(p, c, new Vector3(x, 0.06f, -1.2f), new Vector3(0.04f, 0.01f, 3.2f), default, true);
        }

        void FrontHongKong(Transform p, System.Random r, float w)
        {
            // Hong Kong's green pedestrian railings
            Color green = H("#2F7A55");
            PavingLines(p, w, H("#B7B0C6"));
            for (float x = -w / 2; x < w / 2; x += 2.4f)
            {
                Cyl(p, green, new Vector3(x, 0.45f, 0.4f), 0.05f, 0.9f);
                Box(p, green, new Vector3(x + 1.2f, 0.88f, 0.4f), new Vector3(2.4f, 0.06f, 0.06f));
                Box(p, green, new Vector3(x + 1.2f, 0.2f, 0.4f), new Vector3(2.4f, 0.06f, 0.06f));
                for (int i = 1; i < 6; i++) Box(p, green, new Vector3(x + i * 0.4f, 0.54f, 0.4f), new Vector3(0.035f, 0.68f, 0.035f));
            }
            for (float x = -w / 2 + 5; x < w / 2; x += 16f)
            {
                Frus(p, H("#8C6A4E"), new Vector3(x, 0.05f, -0.6f), 0.35f, 0.45f, 0.5f, default, 10);
                Ball(p, H("#4E9A5B"), new Vector3(x, 0.8f, -0.6f), new Vector3(0.9f, 0.7f, 0.9f));
            }
        }

        void FrontKinmen(Transform p, System.Random r, float w)
        {
            PavingLines(p, w, H("#8E5040"));
            for (float x = -w / 2; x < w / 2; x += 6f)
            {
                // low granite wall segment
                Box(p, KGraniteDk, new Vector3(x + 1.5f, 0.32f, 0.2f), new Vector3(3f, 0.55f, 0.5f));
                Box(p, H("#A49E94"), new Vector3(x + 1.5f, 0.62f, 0.2f), new Vector3(3.1f, 0.08f, 0.6f));
                // a patch of sorghum (高粱), the crop behind Kinmen's famous liquor
                for (int k = 0; k < 7; k++)
                {
                    float sx = x + 3.5f + k * 0.32f + Rf(r, -0.06f, 0.06f), sz = -1.0f + Rf(r, -0.25f, 0.25f), sh = Rf(r, 1.0f, 1.25f);
                    Cyl(p, H("#8DA65A"), new Vector3(sx, sh / 2, sz), 0.025f, sh);
                    Box(p, H("#7FA04E"), new Vector3(sx + 0.16f, sh * 0.55f, sz), new Vector3(0.42f, 0.04f, 0.08f), new Vector3(0, 0, 30f));
                    Box(p, H("#7FA04E"), new Vector3(sx - 0.16f, sh * 0.4f, sz), new Vector3(0.42f, 0.04f, 0.08f), new Vector3(0, 0, -30f));
                    Ball(p, k % 2 == 0 ? H("#A8442A") : H("#B5532F"), new Vector3(sx, sh + 0.1f, sz), new Vector3(0.13f, 0.3f, 0.13f));
                }
                for (int i = 0; i < 3; i++) Ball(p, H("#7CC46B"), new Vector3(x + Rf(r, 0, 3), 0.12f, Rf(r, -1.2f, -0.4f)), new Vector3(0.35f, 0.2f, 0.35f));
            }
        }

        void FrontTaipei(Transform p, System.Random r, float w)
        {
            PavingLines(p, w, H("#8A8094"));
            Color[] col = { H("#E5484D"), H("#F4F1EA"), H("#58C4B0"), H("#3D7DFF"), H("#FFB020") };
            for (float x = -w / 2 + 1; x < w / 2; x += 1.1f)
            {
                if (r.NextDouble() < 0.35) continue;
                // parked scooters, seen from the side, nose towards the road
                var sc = Node(p, "Scooter", new Vector3(x, 0, -0.2f), new Vector3(0, -90, 0));
                Color c = Pick(r, col);
                Cyl(sc, H("#1D1B22"), new Vector3(-0.45f, 0.2f, 0), 0.2f, 0.1f, new Vector3(90, 0, 0));
                Cyl(sc, H("#1D1B22"), new Vector3(0.45f, 0.2f, 0), 0.2f, 0.1f, new Vector3(90, 0, 0));
                Box(sc, c, new Vector3(-0.15f, 0.42f, 0), new Vector3(0.75f, 0.35f, 0.32f));
                Box(sc, H("#2B2B33"), new Vector3(-0.2f, 0.63f, 0), new Vector3(0.55f, 0.08f, 0.26f));
                Box(sc, c, new Vector3(0.38f, 0.55f, 0), new Vector3(0.14f, 0.6f, 0.28f), new Vector3(0, 0, -12));
                Ball(sc, H("#FFF3B0"), new Vector3(0.48f, 0.78f, 0), new Vector3(0.1f, 0.12f, 0.14f), default, true);
            }
        }

        void FrontNagoya(Transform p, System.Random r, float w)
        {
            PavingLines(p, w, H("#D6CFDA"));
            for (float x = -w / 2; x < w / 2; x += 8f)
            {
                // flower bed
                Box(p, H("#9C978F"), new Vector3(x + 2f, 0.18f, 0f), new Vector3(3.6f, 0.3f, 1.0f));
                Box(p, H("#6E4A3A"), new Vector3(x + 2f, 0.3f, 0f), new Vector3(3.4f, 0.1f, 0.85f));
                for (int i = 0; i < 12; i++)
                    Ball(p, Pick(r, new[] { H("#FF8FB1"), Color.white, H("#FFD36E"), H("#C9A7FF") }), new Vector3(x + 0.5f + (i % 6) * 0.6f, 0.45f, -0.25f + (i / 6) * 0.45f), Vector3.one * 0.28f);
            }
            // sakura petals on the ground
            for (int i = 0; i < 40; i++)
                Cyl(p, Pick(r, new[] { H("#FFC0D3"), H("#FFE4EE") }), new Vector3(Rf(r, -w / 2, w / 2), 0.065f, Rf(r, -2.5f, 0.6f)), 0.08f, 0.01f, default, true);
        }

        void FrontWedding(Transform p, System.Random r, float w)
        {
            // the aisle: white flower stands, candles and rose petals
            for (float x = -w / 2; x < w / 2; x += 4f)
            {
                Cyl(p, Color.white, new Vector3(x, 0.4f, 0.2f), 0.06f, 0.8f);
                Frus(p, Color.white, new Vector3(x, 0.05f, 0.2f), 0.2f, 0.08f, 0.15f, default, 10);
                Ball(p, H("#FF8FB1"), new Vector3(x, 0.92f, 0.2f), Vector3.one * 0.38f);
                Ball(p, Color.white, new Vector3(x + 0.12f, 0.98f, 0.08f), Vector3.one * 0.2f);
                Ball(p, H("#7FBF7F"), new Vector3(x, 0.78f, 0.2f), new Vector3(0.45f, 0.12f, 0.45f));
                Cyl(p, H("#FFF8E8"), new Vector3(x + 2f, 0.15f, 0f), 0.06f, 0.3f);
                Ball(p, H("#FFD36E"), new Vector3(x + 2f, 0.36f, 0f), new Vector3(0.07f, 0.12f, 0.07f), default, true);
            }
            for (int i = 0; i < 60; i++)
                Cyl(p, Pick(r, new[] { H("#FF4F7B"), H("#FF8FB1"), Color.white }), new Vector3(Rf(r, -w / 2, w / 2), 0.065f, Rf(r, -2.5f, 0.6f)), 0.09f, 0.01f, default, true);
        }

        // ------------------------------------------------------------------ Port of Nagoya (Garden Pier) — where he proposed

        /// <summary>
        /// Garden Pier at sunset: the white sail-ship Port Building (63 m, observation deck at 53 m), the Antarctic
        /// ship Fuji moored at the pier, the aquarium with its titanium dome, Sea Train Land's 85 m Ferris wheel and,
        /// far across the harbour, the three cable-stayed Meikō Triton bridges with A-shaped towers.
        /// </summary>
        void FarNagoyaPort(Transform p, System.Random r, float w)
        {
            float u = 0.2f;   // metres → units for this closer view
            // night lights shimmering on the water
            for (float x = -w / 2; x < w / 2; x += Rf(r, 0.8f, 2f))
                Box(p, Pick(r, new[] { H("#FFD8A0"), H("#FF9EC8"), H("#9FE0FF"), H("#FFE8C8") }), new Vector3(x, 0.04f, Rf(r, -42f, -14f)), new Vector3(Rf(r, 0.15f, 0.4f), 0.03f, Rf(r, 1.2f, 3.5f)), default, true);

            // Meikō Triton: west (red), centre (white, tallest), east (blue) cable-stayed bridges far across the harbour
            float tz = 34f, deck = 4.2f;
            Box(p, H("#E8E4EE"), new Vector3(0, deck, tz), new Vector3(w + 2f, 0.5f, 1.4f));
            (float x, float h, Color c)[] towers = { (-68f, 13f, H("#D7263D")), (-50f, 13f, H("#D7263D")), (-18f, 19f, H("#F2F2F4")), (6f, 19f, H("#F2F2F4")), (38f, 13.5f, H("#2E6FD8")), (56f, 13.5f, H("#2E6FD8")) };
            foreach (var t in towers)
            {
                Beam(p, new Vector3(t.x - 1.6f, 0, tz), new Vector3(t.x, t.h, tz), 0.4f, t.c);     // A-shaped tower
                Beam(p, new Vector3(t.x + 1.6f, 0, tz), new Vector3(t.x, t.h, tz), 0.4f, t.c);
                Box(p, t.c, new Vector3(t.x, deck - 0.5f, tz), new Vector3(2.6f, 0.3f, 0.5f));
                for (int k = 1; k <= 4; k++)
                    foreach (int sg in new[] { -1, 1 })
                        Beam(p, new Vector3(t.x, t.h - 0.6f, tz - 0.2f), new Vector3(t.x + sg * k * 2.6f, deck + 0.2f, tz - 0.2f), 0.06f, H("#FFF4D8"), true);   // lit cables
                Ball(p, H("#FF3B3B"), new Vector3(t.x, t.h + 0.3f, tz), Vector3.one * 0.45f, default, true);                               // aviation light
            }
            for (float x = -w / 2; x < w / 2; x += 2.4f)
                Box(p, H("#FFE7B0"), new Vector3(x, deck + 0.35f, tz - 0.75f), new Vector3(0.5f, 0.14f, 0.05f), default, true);           // road lights along the deck

            // aquarium: north building with the titanium dome, south building with glass
            var aq = Node(p, "Aquarium", new Vector3(-46, 0, -6));
            Box(aq, H("#E8EEF4"), new Vector3(0, 1.6f, 0), new Vector3(16f, 3.2f, 7f));
            Part(aq, MeshKit.Sphere, H("#C9CED8"), new Vector3(-2f, 3.2f, 0), new Vector3(9f, 4.2f, 6.5f), default, false, 0.8f);   // titanium dome
            Ball(aq, H("#DDE6F0"), new Vector3(5f, 3.0f, 0.5f), new Vector3(6f, 2.0f, 5f));                           // wave roof
            for (int i = 0; i < 8; i++) Box(aq, H("#8FD0FF"), new Vector3(-7f + i * 2f, 1.4f, -3.52f), new Vector3(1.4f, 1.6f, 0.04f), default, true);
            Box(aq, H("#4AA3D9"), new Vector3(6.5f, 1.5f, -3.55f), new Vector3(3f, 0.6f, 0.04f), default, true);

            // Sea Train Land Ferris wheel (85 m, 48 gondolas, colourful lights)
            var fw = Node(p, "SeaTrainWheel", new Vector3(-28, 0, 6));
            float wr = 85f * u / 2f;
            Beam(fw, new Vector3(-3f, 0, 0), new Vector3(0, wr + 1.2f, 0), 0.35f, H("#E4E8EE"));
            Beam(fw, new Vector3(3f, 0, 0), new Vector3(0, wr + 1.2f, 0), 0.35f, H("#E4E8EE"));
            var ring = Node(fw, "Ring", new Vector3(0, wr + 1.2f, -0.3f));
            Part(ring, MeshKit.Torus(wr, 0.14f, 64, 5), H("#F4F4F8"), Vector3.zero, Vector3.one);
            Color[] led = { H("#FF5C8A"), H("#FFD23F"), H("#4FE3FF"), H("#7CFF8A"), H("#9B7BFF") };
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                Ball(ring, led[i % led.Length], new Vector3(Mathf.Cos(a) * wr, Mathf.Sin(a) * wr, -0.12f), Vector3.one * 0.32f, default, true);
                Box(ring, i % 2 == 0 ? Color.white : H("#FFE3EA"), new Vector3(Mathf.Cos(a) * wr, Mathf.Sin(a) * wr - 0.45f, -0.2f), new Vector3(0.5f, 0.55f, 0.5f));
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i * 360f / 16;
                Box(ring, H("#E4E8EE"), new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * wr / 2, Mathf.Sin(a * Mathf.Deg2Rad) * wr / 2, 0), new Vector3(wr, 0.08f, 0.08f), new Vector3(0, 0, a));
            }

            // Port Building: a white "sailing ship" with the observation deck near the top
            var pb = Node(p, "PortBuilding", new Vector3(2, 0, -8));
            Color white = H("#F6F6F8"), glass = H("#5E7EA8");
            Box(pb, white, new Vector3(0, 1.4f, 0), new Vector3(11f, 2.8f, 6f));                       // ship-like base
            Part(pb, MeshKit.Prism(), white, new Vector3(6.2f, 0.7f, 0), new Vector3(1.6f, 1.4f, 6f), new Vector3(0, 0, -90));   // bow
            for (int i = 0; i < 8; i++) Box(pb, H("#FFE3A3"), new Vector3(-4.5f + i * 1.2f, 1.6f, -3.02f), new Vector3(0.8f, 0.9f, 0.04f), default, true);
            Box(pb, white, new Vector3(-1f, 6.6f, 0), new Vector3(3.2f, 7.8f, 3.4f));                   // tower
            Box(pb, H("#9FD4FF"), new Vector3(-1f, 6.6f, -1.72f), new Vector3(0.9f, 7.2f, 0.04f), default, true);
            Box(pb, H("#3E5A80"), new Vector3(-1f, 53f * u, 0), new Vector3(4.4f, 1.4f, 4.2f));        // observation deck (53 m)
            Box(pb, H("#FFE8B0"), new Vector3(-1f, 53f * u, -2.12f), new Vector3(4.2f, 0.8f, 0.04f), default, true);   // lit deck windows
            Box(pb, white, new Vector3(-1f, 53f * u + 0.85f, 0), new Vector3(4.6f, 0.3f, 4.4f));
            Part(pb, MeshKit.Prism(), white, new Vector3(1.3f, 3f, 0), new Vector3(0.25f, 10f, 4.2f), new Vector3(0, 90, 0));  // sail
            Cyl(pb, white, new Vector3(-1f, 63f * u + 0.8f, 0), 0.08f, 2.4f);                           // mast (63 m)
            Box(pb, H("#D7263D"), new Vector3(-0.6f, 63f * u + 1.6f, 0), new Vector3(0.7f, 0.4f, 0.02f), default, true);   // flag

            // Antarctic research ship Fuji, moored at the pier (orange hull, white superstructure)
            var fj = Node(p, "Fuji", new Vector3(-12, 0, -20));
            Color hull = H("#E8642A");
            Box(fj, hull, new Vector3(0, 1.3f, 0), new Vector3(16f, 2.6f, 3.4f));
            Part(fj, MeshKit.Prism(), hull, new Vector3(8.6f, 1.3f, 0), new Vector3(2.6f, 1.2f, 3.4f), new Vector3(0, 0, -90));
            Box(fj, H("#2A2230"), new Vector3(0, 0.15f, 0), new Vector3(16.6f, 0.3f, 3.45f));
            Box(fj, white, new Vector3(1.5f, 3.4f, 0), new Vector3(6f, 1.6f, 2.8f));
            Box(fj, white, new Vector3(2.4f, 4.7f, 0), new Vector3(3.2f, 1.0f, 2.4f));
            for (int i = 0; i < 6; i++) Box(fj, H("#3E4E6A"), new Vector3(-0.8f + i * 0.9f, 3.6f, -1.42f), new Vector3(0.5f, 0.4f, 0.03f), default, true);
            Cyl(fj, white, new Vector3(0.2f, 4.9f, 0), 0.45f, 1.6f);                                    // funnel
            Box(fj, H("#E8642A"), new Vector3(0.2f, 5.5f, 0), new Vector3(0.95f, 0.25f, 0.95f));
            Cyl(fj, H("#C9CCD2"), new Vector3(3.2f, 6.8f, 0), 0.06f, 3.4f);                             // mast
            Box(fj, H("#F2C14E"), new Vector3(5.6f, 3.2f, 0), new Vector3(0.12f, 2.2f, 0.12f), new Vector3(0, 0, -30));   // cargo cranes
            Box(fj, H("#F2C14E"), new Vector3(-4.6f, 3.2f, 0), new Vector3(0.12f, 2.2f, 0.12f), new Vector3(0, 0, 30));
            Box(fj, H("#C9CCD2"), new Vector3(-6.4f, 2.7f, 0), new Vector3(3f, 0.1f, 3f));             // helicopter deck at the stern

            // container gantry cranes on the far right
            foreach (float cx in new[] { 50f, 60f, 70f })
            {
                var cr = Node(p, "Crane", new Vector3(cx, 0, 4));
                Color red = H("#D7263D");
                Beam(cr, new Vector3(-1.5f, 0, 0), new Vector3(-1.5f, 9f, 0), 0.3f, red);
                Beam(cr, new Vector3(1.5f, 0, 0), new Vector3(1.5f, 9f, 0), 0.3f, red);
                Box(cr, Color.white, new Vector3(-1.5f, 9.3f, 0), new Vector3(9f, 0.5f, 0.6f));
                Box(cr, red, new Vector3(0.5f, 8.2f, 0), new Vector3(1.4f, 1.2f, 1.2f));
            }
            // quay wall in front of the water
            Box(p, H("#B8B0A6"), new Vector3(0, 0.3f, -12.5f), new Vector3(w + 2f, 0.6f, 1.2f));
        }

        void MidNagoyaPort(Transform p, System.Random r, float w)
        {
            // harbour-front promenade: railing, bollards, lamps, benches, flags and a heart photo frame
            Box(p, H("#F2F2F4"), new Vector3(0, 1.0f, 2.4f), new Vector3(w + 1f, 0.08f, 0.08f));
            Box(p, H("#F2F2F4"), new Vector3(0, 0.55f, 2.4f), new Vector3(w + 1f, 0.06f, 0.06f));
            for (float x = -w / 2; x < w / 2; x += 2f) Cyl(p, H("#F2F2F4"), new Vector3(x, 0.5f, 2.4f), 0.05f, 1.0f);
            for (float x = -w / 2 + 3; x < w / 2; x += 6f)
            {
                Cyl(p, H("#2A2A33"), new Vector3(x, 0.3f, 1.4f), 0.22f, 0.6f);                            // bollard
                Ball(p, H("#2A2A33"), new Vector3(x, 0.62f, 1.4f), new Vector3(0.55f, 0.25f, 0.55f));
            }
            int k = 0;
            for (float x = -w / 2; x < w / 2; x += 9f, k++)
            {
                Lamp(p, x, H("#3A4A6E"), H("#FFE6A8"));
                Box(p, H("#8E6A4E"), new Vector3(x + 4.5f, 0.45f, 0.8f), new Vector3(1.6f, 0.1f, 0.45f));
                Box(p, H("#8E6A4E"), new Vector3(x + 4.5f, 0.75f, 1.05f), new Vector3(1.6f, 0.5f, 0.08f));
                if (k % 2 == 0)
                {
                    Cyl(p, Color.white, new Vector3(x + 2.2f, 3f, 1.8f), 0.05f, 6f);                      // flag pole
                    Box(p, k % 4 == 0 ? H("#2E6FD8") : H("#D7263D"), new Vector3(x + 2.75f, 5.6f, 1.8f), new Vector3(1.0f, 0.65f, 0.02f));
                }
            }
            // heart-shaped photo frame on the pier (the proposal spot)
            var hf = Node(p, "HeartFrame", new Vector3(10f, 0, 1.2f));
            Box(hf, H("#B8B0A6"), new Vector3(0, 0.15f, 0), new Vector3(2.4f, 0.3f, 1.2f));
            Part(hf, MeshKit.Heart(0.18f), H("#FF6F91"), new Vector3(0, 2.4f, 0), Vector3.one * 3.2f);
            Part(hf, MeshKit.Heart(0.2f), H("#CFC7BC"), new Vector3(0, 2.42f, -0.02f), Vector3.one * 2.3f);   // hollow look
            Cyl(hf, H("#FF6F91"), new Vector3(0, 0.75f, 0), 0.08f, 1.0f);
            // trees
            Tree(p, -20f, 1.5f, H("#4F9E5E"), H("#64B06F"));
            Tree(p, 26f, 1.5f, H("#4F9E5E"), H("#64B06F"));
            // anchor monument
            var an = Node(p, "Anchor", new Vector3(-6f, 0, 1.2f));
            Box(an, H("#B8B0A6"), new Vector3(0, 0.3f, 0), new Vector3(1.4f, 0.6f, 1.2f));
            Box(an, H("#3A3A44"), new Vector3(0, 1.7f, 0), new Vector3(0.16f, 2.2f, 0.16f));
            Part(an, MeshKit.Torus(0.6f, 0.08f, 20, 5, 180f), H("#3A3A44"), new Vector3(0, 1.1f, 0), Vector3.one, new Vector3(0, 0, 180));
            Box(an, H("#3A3A44"), new Vector3(0, 2.6f, 0), new Vector3(0.9f, 0.14f, 0.14f));
            Part(an, MeshKit.Torus(0.2f, 0.06f, 12, 5), H("#3A3A44"), new Vector3(0, 3.0f, 0), Vector3.one);
        }

        void FrontNagoyaPort(Transform p, System.Random r, float w)
        {
            // wooden boardwalk with rope posts
            for (float x = -w / 2; x < w / 2; x += 0.9f)
                Box(p, H("#A9805A"), new Vector3(x, 0.06f, -1.2f), new Vector3(0.05f, 0.01f, 3.2f), default, true);
            for (float x = -w / 2; x < w / 2; x += 3f)
            {
                Cyl(p, H("#6E4A3A"), new Vector3(x, 0.4f, 0.4f), 0.09f, 0.8f);
                Ball(p, H("#6E4A3A"), new Vector3(x, 0.82f, 0.4f), Vector3.one * 0.2f);
                Box(p, H("#D8C49A"), new Vector3(x + 1.5f, 0.6f, 0.4f), new Vector3(3f, 0.05f, 0.05f));
            }
            for (float x = -w / 2 + 5; x < w / 2; x += 16f)
            {
                Frus(p, H("#8C6A4E"), new Vector3(x, 0.05f, -0.8f), 0.35f, 0.45f, 0.5f, default, 10);
                Ball(p, H("#FF8FB1"), new Vector3(x, 0.75f, -0.8f), new Vector3(0.8f, 0.5f, 0.8f));
            }
        }
    }

    /// <summary>Marks a branch that MeshKit.Merge must leave alone (animated parts).</summary>
    public class NoMerge : MonoBehaviour { }

    /// <summary>Sways a transform around its local Z axis (searchlights, lasers).</summary>
    public class Sweeper : MonoBehaviour
    {
        public float amplitude = 25f, speed = 0.6f, phase;
        Quaternion baseRot;
        void Awake() { baseRot = transform.localRotation; }
        void Update() { transform.localRotation = baseRot * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * speed + phase) * amplitude); }
    }
}
