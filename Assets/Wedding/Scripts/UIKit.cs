using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeddingRun
{
    /// <summary>Tiny code-only UI kit: generated rounded sprites, builders and unscaled-time tweens.</summary>
    public static class UIKit
    {
        public static TMP_FontAsset Bold, Regular;
        public static Sprite Round16, Round32, Pill, Circle, Shadow, White;
        public static Action ClickSound, HoverSound;
        static MonoBehaviour runner;

        public static void Init(MonoBehaviour coroutineRunner, TMP_FontAsset bold, TMP_FontAsset regular)
        {
            runner = coroutineRunner; Bold = bold; Regular = regular;
            Round16 = RoundedSprite(16, 0, 1.5f);
            Round32 = RoundedSprite(32, 0, 1.5f);
            Pill = RoundedSprite(64, 0, 1.5f);
            Circle = RoundedSprite(64, 0, 1.5f, false);
            Shadow = RoundedSprite(40, 24, 24f);
            var tex = new Texture2D(4, 4); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; tex.SetPixels(px); tex.Apply();
            White = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        }

        /// <summary>SDF-style rounded rectangle, generated so it is crisp at any size (9-sliced).</summary>
        static Sprite RoundedSprite(int radius, int pad, float softness, bool sliced = true)
        {
            int size = (radius + pad) * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - c) - 1f, py = Mathf.Abs(y + 0.5f - c) - 1f; // 1px straight middle
                    float qx = Mathf.Max(px, 0), qy = Mathf.Max(py, 0);
                    float d = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                    float a = pad > 0 ? Mathf.Pow(Mathf.Clamp01(1 - (d + softness * 0.15f) / (pad)), 2.2f) : Mathf.Clamp01(0.5f - d / softness);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            float b = radius + pad + 0.5f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect,
                sliced ? new Vector4(b, b, b, b) : Vector4.zero);
        }

        // ------------------------------------------------------------------ builders

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name, float inset = 0)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Img(RectTransform rt, Sprite sprite, Color color, float ppuMultiplier = 1f)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
            return img;
        }

        /// <summary>Card with soft drop shadow.</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite shape = null, float shadowAlpha = 0.22f)
        {
            var holder = Rect(parent, name, anchor, pos, size);
            if (shadowAlpha > 0)
            {
                var sh = Rect(holder, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(0, -10), size + new Vector2(64, 64));
                var si = Img(sh, Shadow, new Color(0.25f, 0.05f, 0.15f, shadowAlpha));
                si.raycastTarget = false;
                sh.anchorMin = Vector2.zero; sh.anchorMax = Vector2.one; sh.sizeDelta = new Vector2(64, 64);
            }
            var bg = Stretch(holder, "Bg");
            Img(bg, shape ?? Round32, color);
            return holder;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var rt = Stretch(parent, "Label");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? Bold : Regular;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = true;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Min(size, 16);
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var rt = Rect(parent, "Label", anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? Bold : Regular;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = true;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Min(size, 16);
            return t;
        }

        public enum Style { Primary, Secondary, Gold, Ghost }

        public static Button Btn(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Style style, UnityAction onClick, float fontSize = 40)
        {
            Color bg, fg;
            switch (style)
            {
                case Style.Primary: bg = Palette.Rose; fg = Color.white; break;
                case Style.Gold: bg = Palette.Gold; fg = Palette.Wine; break;
                case Style.Ghost: bg = new Color(1, 1, 1, 0.22f); fg = Color.white; break;
                default: bg = Color.white; fg = Palette.Wine; break;
            }
            var rt = Card(parent, "Btn_" + text, anchor, pos, size, bg, Pill, style == Style.Ghost ? 0 : 0.25f);
            var bgImg = rt.Find("Bg").GetComponent<Image>();
            if (style == Style.Primary || style == Style.Gold)
            {
                // subtle darker lip at the bottom for a tactile look
                var lip = Stretch(rt, "Lip");
                lip.SetSiblingIndex(bgImg.transform.GetSiblingIndex());
                lip.offsetMin = new Vector2(0, -6); lip.offsetMax = new Vector2(0, -6);
                Img(lip, Pill, style == Style.Primary ? Palette.RoseDark : Palette.GoldDark).raycastTarget = false;
            }
            var label = Label(rt, text, fontSize, fg);
            label.enableWordWrapping = false;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bgImg;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => ClickSound?.Invoke());
            if (onClick != null) b.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<ButtonJuice>();
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            if (t) t.text = text;
        }

        public static CanvasGroup Group(RectTransform rt)
        {
            var g = rt.GetComponent<CanvasGroup>();
            return g ? g : rt.gameObject.AddComponent<CanvasGroup>();
        }

        // ------------------------------------------------------------------ tweens (unscaled time)

        public static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
        public static float EaseOutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);

        public static Coroutine Run(IEnumerator e) => runner.StartCoroutine(e);

        public static Coroutine Tween(float duration, Action<float> step, Action done = null, float delay = 0)
            => runner.StartCoroutine(TweenCo(duration, step, done, delay));

        static IEnumerator TweenCo(float duration, Action<float> step, Action done, float delay)
        {
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float t = 0;
            while (t < duration)
            {
                step(t / duration);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            step(1);
            done?.Invoke();
        }

        /// <summary>Fade + pop a screen in.</summary>
        public static void Show(RectTransform screen, float delay = 0)
        {
            screen.gameObject.SetActive(true);
            var g = Group(screen);
            g.alpha = 0; g.interactable = true; g.blocksRaycasts = true;
            screen.localScale = Vector3.one * 0.94f;
            Tween(0.32f, t =>
            {
                g.alpha = EaseOutCubic(t);
                screen.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, EaseOutBack(t));
            }, null, delay);
        }

        public static void Hide(RectTransform screen, float duration = 0.2f)
        {
            if (!screen.gameObject.activeSelf) return;
            var g = Group(screen);
            g.interactable = false; g.blocksRaycasts = false;
            float a0 = g.alpha;
            Tween(duration, t => g.alpha = Mathf.Lerp(a0, 0, t), () => screen.gameObject.SetActive(false));
        }

        public static void Punch(Transform t, float amount = 0.18f, float duration = 0.28f)
        {
            Tween(duration, k => t.localScale = Vector3.one * (1 + Mathf.Sin(k * Mathf.PI) * amount * (1 - k * 0.5f)), () => t.localScale = Vector3.one);
        }
    }

    /// <summary>Hover-grow / press-squish feedback for every button.</summary>
    public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        float target = 1, current = 1;
        public void OnPointerEnter(PointerEventData e) { target = 1.06f; UIKit.HoverSound?.Invoke(); }
        public void OnPointerExit(PointerEventData e) { target = 1f; }
        public void OnPointerDown(PointerEventData e) { target = 0.93f; }
        public void OnPointerUp(PointerEventData e) { target = 1.04f; }
        void OnDisable() { target = current = 1; transform.localScale = Vector3.one; }
        void Update()
        {
            current = Mathf.Lerp(current, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            transform.localScale = Vector3.one * current;
        }
    }
}
