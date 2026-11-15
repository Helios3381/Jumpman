using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static WeddingRun.UIKit;

namespace WeddingRun
{
    /// <summary>
    /// "Helios Studios" logo intro shown once per launch, between loading and the title screen.
    /// Everything (background, sun emblem, light sweep) is drawn in code so the WebGL download gains no image or video.
    /// Tap, click or any key skips it.
    /// </summary>
    public class IntroSplash : MonoBehaviour, IPointerDownHandler
    {
        const string Wordmark = "HELIOS";
        const string Subtitle = "STUDIOS";
        const int SortingOrder = 100;       // above GameUI (10)

        // timeline in seconds
        const float EmblemIn = 0.15f, EmblemDur = 0.75f;
        const float WordIn = 0.45f, WordDur = 0.7f;
        const float SubIn = 0.8f, SubDur = 0.5f;
        const float SweepIn = 1.05f, SweepDur = 0.75f;
        const float FadeOutAt = 2.2f, FadeOutDur = 0.5f, SkipFadeDur = 0.3f;
        // The first frames after the WebGL loader finishes can take very long; clamping keeps them from skipping the animation.
        const float MaxStep = 1f / 30f;

        static readonly Color BgCenter = Stages.C("#3A1E1A");
        static readonly Color BgEdge = Stages.C("#120A0C");
        static readonly Color Warm = Stages.C("#FFD9A0");
        static readonly Color SweepColor = Stages.C("#FFF6E0");

        CanvasGroup group;
        RectTransform emblem, rays, logo;
        Image glow;
        TextMeshProUGUI word, sub;
        Image line;
        Action done;
        float time;
        bool skipped, finished;

        /// <summary>Builds the intro on its own overlay canvas; <paramref name="onFinished"/> runs when it starts fading out.</summary>
        public static IntroSplash Play(Action onFinished)
        {
            var intro = new GameObject("IntroSplash", typeof(RectTransform)).AddComponent<IntroSplash>();
            intro.done = onFinished;
            intro.Build();
            return intro;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            scaler.matchWidthOrHeight = aspect >= 1.5f ? 1f : (aspect >= 1f ? 0.6f : 0f);
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();

            // Full-screen warm vignette; it also takes the skip tap so it never reaches the title buttons underneath.
            var bg = Stretch(root, "Bg");
            Img(bg, Sprite.Create(Vignette(64), new Rect(0, 0, 64, 64), Vector2.one * 0.5f), Color.white).raycastTarget = true;

            logo = Rect(root, "Logo", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1200, 600));

            var g = Rect(logo, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(560, 560));
            glow = Img(g, Sprite.Create(Glow(64), new Rect(0, 0, 64, 64), Vector2.one * 0.5f), new Color(Warm.r, Warm.g, Warm.b, 0));
            glow.raycastTarget = false;

            emblem = Rect(logo, "Emblem", new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(220, 220));
            var sun = Sprite.Create(SunTexture(256, false), new Rect(0, 0, 256, 256), Vector2.one * 0.5f);
            var ray = Sprite.Create(SunTexture(256, true), new Rect(0, 0, 256, 256), Vector2.one * 0.5f);
            rays = Rect(emblem, "Rays", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 220));
            Img(rays, ray, Palette.Gold).raycastTarget = false;
            Img(Rect(emblem, "Core", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 220)), sun, Warm).raycastTarget = false;

            word = Label(logo, Wordmark, 150, Palette.Cream, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1200, 180));
            word.enableWordWrapping = false; word.enableAutoSizing = false;
            word.characterSpacing = 18;

            var l = Rect(logo, "Line", new Vector2(0.5f, 0.5f), new Vector2(0, -148), new Vector2(520, 3));
            line = Img(l, White, Palette.Gold);
            line.raycastTarget = false;

            sub = Label(logo, Subtitle, 46, Palette.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(1200, 70), TextAlignmentOptions.Center, false);
            sub.enableWordWrapping = false; sub.enableAutoSizing = false;
            sub.characterSpacing = 60;

            var c = Label(root, GameUI.Copyright, 22, new Color(1, 1, 1, 0.45f), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1000, 30), TextAlignmentOptions.Center, false);
            c.enableWordWrapping = false;

            Apply(0);
        }

        public void OnPointerDown(PointerEventData e) => Skip();

        void Skip()
        {
            if (skipped || time >= FadeOutAt) return;
            skipped = true;
            Finish();
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            // Let clicks through right away so the title screen is usable while the intro fades.
            group.blocksRaycasts = false;
            done?.Invoke();
        }

        void Update()
        {
            // The scene's scripts already run while Unity's own splash is drawn on top, so hold the timeline until it is gone.
            if (!UnityEngine.Rendering.SplashScreen.isFinished) return;
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0)) Skip();   // mouse/touch go through OnPointerDown
            time += Mathf.Min(Time.unscaledDeltaTime, MaxStep);

            if (skipped)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, 0, Mathf.Min(Time.unscaledDeltaTime, MaxStep) / SkipFadeDur);
                if (group.alpha <= 0) Destroy(gameObject);
                return;
            }

            Apply(time);
            if (time >= FadeOutAt) Finish();
            if (time >= FadeOutAt + FadeOutDur) Destroy(gameObject);
        }

        static float Phase(float t, float start, float dur) => Mathf.Clamp01((t - start) / dur);

        void Apply(float t)
        {
            float e = Phase(t, EmblemIn, EmblemDur);
            emblem.localScale = Vector3.one * Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(e));
            SetAlpha(emblem, EaseOutCubic(Mathf.Clamp01(e * 2f)));
            rays.localRotation = Quaternion.Euler(0, 0, -t * 14f);
            glow.color = new Color(Warm.r, Warm.g, Warm.b, 0.38f * EaseOutCubic(e) + 0.06f * Mathf.Sin(t * 3f) * e);

            float w = Phase(t, WordIn, WordDur);
            word.alpha = EaseOutCubic(w);
            word.characterSpacing = Mathf.Lerp(48f, 18f, EaseOutCubic(w));
            word.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, EaseOutCubic(w));

            float s = Phase(t, SubIn, SubDur);
            sub.alpha = EaseOutCubic(s);
            line.rectTransform.localScale = new Vector3(EaseOutCubic(s), 1, 1);
            line.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.8f * s);

            Sweep(word, Phase(t, SweepIn, SweepDur), 1f);
            Sweep(sub, Phase(t, SweepIn + 0.12f, SweepDur), 0.7f);

            float o = Phase(t, FadeOutAt, FadeOutDur);
            group.alpha = 1 - EaseOutCubic(o);
            logo.localScale = Vector3.one * (1 + 0.04f * o);
        }

        static void SetAlpha(RectTransform rt, float a)
        {
            foreach (var img in rt.GetComponentsInChildren<Image>())
            {
                var c = img.color; c.a = a; img.color = c;
            }
        }

        /// <summary>
        /// Light sweep: brightens the glyph vertices near a band that travels left to right.
        /// Done on the text mesh colours so the light stays inside the letters without a mask or extra texture.
        /// </summary>
        static void Sweep(TextMeshProUGUI text, float k, float strength)
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            if (info.characterCount == 0) return;
            var bounds = text.textBounds;
            const float band = 0.18f;   // half-width of the band, as a share of the text width
            float centre = Mathf.Lerp(-band * 2f, 1f + band * 2f, k);
            bool active = k > 0 && k < 1;
            Color32 baseCol = text.color;
            byte a = (byte)Mathf.RoundToInt(text.alpha * 255);
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var cols = info.meshInfo[m].colors32;
                var verts = info.meshInfo[m].vertices;
                if (cols == null || verts == null) continue;
                for (int v = 0; v < info.meshInfo[m].vertexCount; v++)
                {
                    // slanted band: higher vertices count as further right
                    float x = Mathf.InverseLerp(bounds.min.x, bounds.max.x, verts[v].x + (verts[v].y - bounds.center.y) * 0.35f);
                    float lit = active ? Mathf.Clamp01(1 - Mathf.Abs(x - centre) / band) * strength : 0;
                    var c = Color32.Lerp(baseCol, SweepColor, lit);
                    c.a = a;
                    cols[v] = c;
                }
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        // ------------------------------------------------------------------ generated textures

        /// <summary>Soft round light (white, alpha falls off to the edge); tinted by the Image colour.</summary>
        static Texture2D Glow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float k = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    px[y * size + x] = new Color(1, 1, 1, k * k);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        static Texture2D Vignette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = ((y + 0.5f) / size - 0.55f) * 1.15f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.72f);
                    px[y * size + x] = Color.Lerp(BgCenter, BgEdge, d * d);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        /// <summary>Sun emblem: a ring with a filled core, or (rays = true) twelve tapered rays around it.</summary>
        static Texture2D SunTexture(int size, bool raysOnly)
        {
            const int RayCount = 12;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float aa = 1.5f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (raysOnly)
                    {
                        float ang = Mathf.Atan2(dy, dx) * RayCount / (2 * Mathf.PI);
                        float f = Mathf.Abs(ang - Mathf.Round(ang));                       // 0 at a ray's centre line
                        float along = Mathf.InverseLerp(0.30f, 0.49f, r);
                        float width = Mathf.Lerp(0.16f, 0.02f, along);
                        a = r < 0.30f || r > 0.49f ? 0 : Mathf.Clamp01((width - f) / 0.03f) * Mathf.Clamp01((0.49f - r) / aa);
                    }
                    else
                    {
                        float core = Mathf.Clamp01((0.16f - r) / aa);
                        float ring = Mathf.Clamp01((0.025f - Mathf.Abs(r - 0.235f)) / aa);
                        a = Mathf.Max(core, ring);
                    }
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }
    }
}
