using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static WeddingRun.UIKit;

namespace WeddingRun
{
    /// <summary>Every screen of the game, built in code (title, HUD, pause, game over, ranking, how-to).</summary>
    public class GameUI : MonoBehaviour
    {
        public class RunResult
        {
            public int score, distance, plus, minus, maxCombo, stage, lap;
            public string hitBy;
            public bool newRecord;
        }

        WeddingGame game;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform root, title, hud, pause, gameOver, ranking, howTo, overlay, popupLayer;
        TextMeshProUGUI bestLabel, scoreText, distanceText, comboText, countdownText, portraitHint;
        RectTransform lapPill, warnIcon;
        TextMeshProUGUI lapText;
        RectTransform comboPill, shieldPill, progressFill, progressMarker, bannerCard, toastCard;
        TextMeshProUGUI bannerTitle, bannerSub, toastText;
        Image flash;
        readonly List<Image> progressNodes = new List<Image>();
        readonly List<TextMeshProUGUI> nodeLabels = new List<TextMeshProUGUI>();
        Button musicBtn, sfxBtn, musicBtn2, sfxBtn2;

        // game over widgets
        TextMeshProUGUI goScore, goReason, goStage, goWeb;
        TextMeshProUGUI[] goStats = new TextMeshProUGUI[4];
        RectTransform goRecord, goSubmitRow;
        TMP_InputField nameInput;
        Button submitBtn;

        // ranking
        readonly List<TextMeshProUGUI[]> rankRows = new List<TextMeshProUGUI[]>();
        TextMeshProUGUI rankStatus;

        readonly Queue<TextMeshProUGUI> popupPool = new Queue<TextMeshProUGUI>();
        int lastScoreShown = -1;

        public bool AnyModalOpen => (pause && pause.gameObject.activeSelf) || ranking.gameObject.activeSelf || howTo.gameObject.activeSelf;

        public void Build(WeddingGame g)
        {
            game = g;
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            BuildCanvas();
        }

        /// <summary>Rebuild every screen in the current language, keeping what was visible.</summary>
        public void Rebuild()
        {
            bool titleOn = title.gameObject.activeSelf, hudOn = hud.gameObject.activeSelf, pauseOn = pause.gameObject.activeSelf;
            Destroy(root.gameObject);
            progressNodes.Clear(); nodeLabels.Clear(); rankRows.Clear(); popupPool.Clear();
            lastScoreShown = -1;
            BuildCanvas();
            title.gameObject.SetActive(titleOn);
            hud.gameObject.SetActive(hudOn);
            pause.gameObject.SetActive(pauseOn);
        }

        static string T(string zh, string en, string ja) => Loc.T(zh, en, ja);

        Button LangButton(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Style style)
            => Btn(parent, Loc.Label, anchor, pos, size, style, game.ChangeLanguage, 32);

        void BuildCanvas()
        {
            var cgo = new GameObject("UI", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;

            popupLayer = Stretch(root, "Popups");
            BuildHUD();
            BuildTitle();
            BuildPause();
            BuildGameOver();
            BuildRanking();
            BuildHowTo();

            overlay = Stretch(root, "Overlay");
            flash = Img(overlay, White, new Color(1, 1, 1, 0));
            flash.raycastTarget = false;
            countdownText = Label(root, "", 260, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(800, 400));
            countdownText.outlineWidth = 0.12f; countdownText.outlineColor = Palette.RoseDark;

            // toast (new record etc.)
            toastCard = Card(root, "Toast", new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(620, 96), Palette.Gold, Pill);
            toastText = Label(toastCard, "", 44, Palette.Wine);
            toastCard.gameObject.SetActive(false);

            // stage banner
            bannerCard = Card(root, "StageBanner", new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(980, 190), new Color(1, 1, 1, 0.94f), Round32);
            var stripe = Rect(bannerCard, "Stripe", new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(980 - 12, 10), new Vector2(0.5f, 1));
            Img(stripe, Pill, Palette.Rose);
            bannerTitle = Label(bannerCard, "", 64, Palette.Wine, new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(940, 90));
            bannerSub = Label(bannerCard, "", 38, Palette.Rose, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(940, 60), TextAlignmentOptions.Center, false);
            bannerCard.gameObject.SetActive(false);

            hud.gameObject.SetActive(false);
            pause.gameObject.SetActive(false);
            gameOver.gameObject.SetActive(false);
            ranking.gameObject.SetActive(false);
            howTo.gameObject.SetActive(false);
            RefreshSoundButtons();
        }

        void Update()
        {
            // Landscape: fit height. Portrait phones: fit width so nothing is cut off.
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            scaler.matchWidthOrHeight = aspect >= 1.5f ? 1f : (aspect >= 1f ? 0.6f : 0f);
            if (portraitHint) portraitHint.gameObject.SetActive(aspect < 1f);
        }

        // ------------------------------------------------------------------ title

        void BuildTitle()
        {
            title = Stretch(root, "Title");
            var shade = Stretch(title, "Shade");
            var shadeImg = Img(shade, White, new Color(0.18f, 0.05f, 0.14f, 0.28f));
            shadeImg.raycastTarget = false;

            // 囍 badge
            var ring = Rect(title, "BadgeRing", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(196, 196));
            Img(ring, Circle, Palette.Gold);
            var badge = Rect(ring, "Badge", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176, 176));
            Img(badge, Circle, H("#D7263D"));
            Label(badge, "囍", 120, Palette.Gold).rectTransform.anchoredPosition = new Vector2(0, 4);
            UIKit.Run(Spin(ring));

            var shadow = Label(title, "LOVE RUN", 168, new Color(0.45f, 0.08f, 0.25f, 0.45f), new Vector2(0.5f, 1), new Vector2(6, -350), new Vector2(1600, 190));
            shadow.characterSpacing = 6;
            var t = Label(title, "LOVE RUN", 168, Color.white, new Vector2(0.5f, 1), new Vector2(0, -344), new Vector2(1600, 190));
            t.characterSpacing = 6;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(Color.white, Color.white, H("#FFE3EA"), H("#FFD27A"));

            var chip = Card(title, "Names", new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(820, 84), Palette.Cream, Pill);
            Label(chip, $"{game.groomName}  &  {game.brideName}   ·   {game.weddingDate}", 38, Palette.Wine);

            var best = Card(title, "Best", new Vector2(0, 1), new Vector2(200, -70), new Vector2(320, 84), new Color(1, 1, 1, 0.92f), Pill);
            bestLabel = Label(best, "BEST  0", 36, Palette.Wine);

            LangButton(title, new Vector2(1, 1), new Vector2(-560, -70), new Vector2(220, 80), Style.Gold);
            musicBtn = Btn(title, "音樂 ON", new Vector2(1, 1), new Vector2(-330, -70), new Vector2(220, 80), Style.Secondary, () => { game.ToggleMusic(); RefreshSoundButtons(); }, 32);
            sfxBtn = Btn(title, "音效 ON", new Vector2(1, 1), new Vector2(-110, -70), new Vector2(200, 80), Style.Secondary, () => { game.ToggleSfx(); RefreshSoundButtons(); }, 32);

            var start = Btn(title, T("開始遊戲  START", "START", "スタート"), new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(620, 130), Style.Primary, game.StartRun, 54);
            UIKit.Run(Breathe(start.transform.Find("Bg")));
            Btn(title, T("玩法說明", "How to Play", "遊び方"), new Vector2(0.5f, 0), new Vector2(OnlineRanking.Available ? -170 : 0, 110), new Vector2(310, 96), Style.Secondary, OpenHowTo, 38);
            if (OnlineRanking.Available)
                Btn(title, T("排行榜", "Ranking", "ランキング"), new Vector2(0.5f, 0), new Vector2(170, 110), new Vector2(310, 96), Style.Gold, OpenRanking, 38);

            Label(title, T("點擊畫面 / 空白鍵：跳躍（按住更高、空中再點一次＝二段跳）　·　往下滑 / ↓：滑行", "Tap / Space: Jump (hold = higher, tap again in the air = double jump)  ·  Swipe down / ↓: Slide", "タップ / スペース：ジャンプ（長押しで高く・空中でもう一度＝2段ジャンプ）　·　下スワイプ / ↓：スライド"), 30, new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(1800, 50), TextAlignmentOptions.Center, false);
            portraitHint = Label(title, T("橫向螢幕遊玩體驗更佳", "Best played in landscape", "スマホを横にして遊んでね"), 34, Palette.Gold, new Vector2(0.5f, 0), new Vector2(0, 400), new Vector2(1600, 60));
        }

        System.Collections.IEnumerator Spin(Transform t)
        {
            while (t)
            {
                if (t.gameObject.activeInHierarchy)
                    t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 1.6f) * 6f);
                yield return null;
            }
        }

        System.Collections.IEnumerator Breathe(Transform t)
        {
            while (t)
            {
                if (t.gameObject.activeInHierarchy)
                {
                    float s = 1 + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.025f;
                    t.localScale = new Vector3(s, s, 1);
                }
                yield return null;
            }
        }

        public void SetBest(int best) { if (bestLabel) bestLabel.text = $"BEST  {best:N0}"; }

        public void ShowTitle()
        {
            Hide(hud, 0.15f);
            Hide(gameOver, 0.15f);
            Hide(pause, 0.15f);
            Show(title);
        }

        public void HideTitle() => Hide(title);

        void RefreshSoundButtons()
        {
            string m = T("音樂", "Music", "BGM"), e = T("音效", "Sound", "SE");
            foreach (var b in new[] { musicBtn, musicBtn2 }) if (b) SetButtonText(b, m + (game.MusicOn ? " ON" : " OFF"));
            foreach (var b in new[] { sfxBtn, sfxBtn2 }) if (b) SetButtonText(b, e + (game.SfxOn ? " ON" : " OFF"));
        }

        // ------------------------------------------------------------------ HUD

        void BuildHUD()
        {
            hud = Stretch(root, "HUD");

            var card = Card(hud, "ScoreCard", new Vector2(0, 1), new Vector2(230, -100), new Vector2(380, 140), new Color(1, 1, 1, 0.93f), Round32);
            Label(card, "SCORE", 26, Palette.Rose, new Vector2(0, 1), new Vector2(110, -30), new Vector2(180, 36), TextAlignmentOptions.Left);
            scoreText = Label(card, "0", 72, Palette.Wine, new Vector2(0, 0), new Vector2(190, 52), new Vector2(340, 90), TextAlignmentOptions.Left);
            distanceText = Label(card, "0 m", 28, Palette.Muted, new Vector2(1, 1), new Vector2(-90, -30), new Vector2(160, 36), TextAlignmentOptions.Right, false);

            comboPill = Card(hud, "Combo", new Vector2(0, 1), new Vector2(190, -212), new Vector2(300, 64), Palette.Gold, Pill);
            comboText = Label(comboPill, "", 32, Palette.Wine);
            comboPill.gameObject.SetActive(false);

            shieldPill = Card(hud, "Shield", new Vector2(0, 1), new Vector2(190, -290), new Vector2(300, 64), H("#D7263D"), Pill);
            Label(shieldPill, T("囍 護盾發動中", "囍 Shield ON", "囍 シールド中"), 30, Palette.Gold);
            shieldPill.gameObject.SetActive(false);

            // journey progress 香港 → 金門 → 台北 → 名古屋 → 囍
            var bar = Card(hud, "Journey", new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(900, 116), new Color(1, 1, 1, 0.9f), Round32, 0.15f);
            var track = Rect(bar, "Track", new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(760, 14));
            Img(track, Pill, H("#F3D9E2"));
            progressFill = Rect(track, "Fill", new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 14), new Vector2(0, 0.5f));
            Img(progressFill, Pill, Palette.Rose);
            for (int i = 0; i < Stages.List.Length; i++)
            {
                float x = -380 + i * 190;
                var node = Rect(bar, "Node" + i, new Vector2(0.5f, 0.5f), new Vector2(x, 14), new Vector2(40, 40));
                progressNodes.Add(Img(node, Circle, H("#F3D9E2")));
                nodeLabels.Add(Label(bar, Stages.List[i].ShortName, 26, Palette.Wine, new Vector2(0.5f, 0.5f), new Vector2(x, -30), new Vector2(180, 40)));
            }
            progressMarker = Rect(bar, "Marker", new Vector2(0.5f, 0.5f), new Vector2(-380, 14), new Vector2(56, 56));
            Img(progressMarker, Circle, Palette.Gold);
            var inner = Rect(progressMarker, "In", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            Img(inner, Circle, H("#D7263D"));
            Label(inner, "囍", 30, Palette.Gold);

            lapPill = Card(hud, "Lap", new Vector2(0.5f, 1), new Vector2(0, -172), new Vector2(300, 52), Palette.Gold, Pill, 0.15f);
            lapText = Label(lapPill, "", 28, Palette.Wine);
            lapPill.gameObject.SetActive(false);

            // control hints: left part of the screen slides, right part jumps
            var hl = Card(hud, "HintSlide", new Vector2(0, 0), new Vector2(190, 60), new Vector2(320, 64), new Color(0.43f, 0.16f, 0.27f, 0.45f), Pill, 0);
            Label(hl, T("↓ 往下滑：滑行", "↓ Swipe down: Slide", "↓ 下スワイプ：スライド"), 28, Color.white);
            var hr = Card(hud, "HintJump", new Vector2(1, 0), new Vector2(-190, 60), new Vector2(320, 64), new Color(0.43f, 0.16f, 0.27f, 0.45f), Pill, 0);
            Label(hr, T("點擊畫面：跳躍", "Tap: Jump", "タップ：ジャンプ"), 28, Color.white);
            foreach (var img in hl.GetComponentsInChildren<Image>()) img.raycastTarget = false;
            foreach (var img in hr.GetComponentsInChildren<Image>()) img.raycastTarget = false;
            foreach (var img in card.GetComponentsInChildren<Image>()) img.raycastTarget = false;

            // "!" warning on the right edge when something charges in from off-screen
            warnIcon = Rect(hud, "Warn", new Vector2(1, 0.5f), new Vector2(-80, 0), new Vector2(96, 96));
            Img(warnIcon, Circle, Color.white).raycastTarget = false;
            var wi = Rect(warnIcon, "In", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(82, 82));
            Img(wi, Circle, Palette.Bad).raycastTarget = false;
            Label(wi, "!", 66, Color.white);
            warnIcon.gameObject.SetActive(false);

            var pb = Btn(hud, "II", new Vector2(1, 1), new Vector2(-90, -90), new Vector2(104, 104), Style.Secondary, game.Pause, 44);
            pb.transform.Find("Bg").GetComponent<Image>().sprite = Circle;
        }

        public void ShowHUD()
        {
            Show(hud);
            lastScoreShown = -1;
        }

        public void HideHUD() => Hide(hud);

        /// <summary>Blinking "!" at the right edge, at the screen height of <paramref name="world"/>.</summary>
        public void Warn(Vector3 world)
        {
            var cam = Camera.main;
            if (!cam || !warnIcon) return;
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(cam.transform.position.x, world.y, 0));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, sp, null, out var local);
            warnIcon.anchoredPosition = new Vector2(-80, local.y);
            warnIcon.gameObject.SetActive(true);
            var g = Group(warnIcon);
            Tween(1.2f, k =>
            {
                g.alpha = Mathf.Repeat(k * 6f, 1f) < 0.65f ? 1f : 0.55f;
                warnIcon.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI * 6f) * 0.08f);
            }, () => warnIcon.gameObject.SetActive(false));
        }

        public void SetScore(int score, float distance)
        {
            if (score != lastScoreShown)
            {
                scoreText.text = score.ToString("N0");
                lastScoreShown = score;
            }
            distanceText.text = Mathf.FloorToInt(distance) + " m";
        }

        public void PunchScore() => Punch(scoreText.transform, 0.16f);

        public void SetCombo(int combo, float mult)
        {
            bool show = combo >= 2;
            if (show != comboPill.gameObject.activeSelf) comboPill.gameObject.SetActive(show);
            if (show)
            {
                string c = T($"連擊 {combo}", $"Combo {combo}", $"コンボ {combo}");
                comboText.text = mult > 1 ? $"{c}   ×{mult:0.#}" : c;
                Punch(comboPill, 0.1f, 0.2f);
            }
        }

        public void SetShield(bool on)
        {
            if (on && !shieldPill.gameObject.activeSelf) { shieldPill.gameObject.SetActive(true); Punch(shieldPill); }
            else if (!on) shieldPill.gameObject.SetActive(false);
        }

        public void SetProgress(float t, int stage, int lap)
        {
            bool showLap = lap > 0;
            if (lapPill.gameObject.activeSelf != showLap) lapPill.gameObject.SetActive(showLap);
            if (showLap) lapText.text = T($"第 {lap + 1} 輪", $"LAP {lap + 1}", $"{lap + 1} 周目");
            // later laps skip the wedding venue: the last node means "next lap"
            var last = nodeLabels[nodeLabels.Count - 1];
            string lastText = showLap ? T("再一輪", "Loop", "ループ") : Stages.List[Stages.List.Length - 1].ShortName;
            if (last.text != lastText) last.text = lastText;
            progressFill.sizeDelta = new Vector2(760 * Mathf.Clamp01(t), 14);
            float x = -380 + 760 * Mathf.Clamp01(t);
            progressMarker.anchoredPosition = new Vector2(x, 14 + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 6f);
            for (int i = 0; i < progressNodes.Count; i++) progressNodes[i].color = i <= stage ? Palette.Rose : H("#F3D9E2");
        }

        // ------------------------------------------------------------------ transient feedback

        public void StageBanner(StageDef s, int lap)
        {
            bannerTitle.text = Loc.Current == Lang.En ? $"STAGE {s.index + 1} · {s.nameEn}" : $"STAGE {s.index + 1} · {s.Name}  {s.nameEn}";
            bannerSub.text = lap > 0 ? T($"第 {lap + 1} 輪　越跑越快！", $"Lap {lap + 1} — faster and faster!", $"{lap + 1}周目　どんどん速くなる！") : "";
            bannerTitle.rectTransform.anchoredPosition = new Vector2(0, lap > 0 ? 26 : 0);
            bannerCard.gameObject.SetActive(true);
            var g = Group(bannerCard);
            Tween(2.8f, t =>
            {
                float a = t < 0.12f ? t / 0.12f : (t > 0.85f ? (1 - t) / 0.15f : 1);
                g.alpha = a;
                float y = t < 0.12f ? Mathf.Lerp(300, 220, EaseOutBack(t / 0.12f)) : 220;
                bannerCard.anchoredPosition = new Vector2(0, y);
            }, () => bannerCard.gameObject.SetActive(false));
        }

        public void Toast(string text)
        {
            toastText.text = text;
            toastCard.gameObject.SetActive(true);
            var g = Group(toastCard);
            Tween(2.2f, t =>
            {
                g.alpha = t < 0.1f ? t / 0.1f : (t > 0.85f ? (1 - t) / 0.15f : 1);
                toastCard.localScale = Vector3.one * (t < 0.15f ? Mathf.LerpUnclamped(0.6f, 1, EaseOutBack(t / 0.15f)) : 1);
            }, () => toastCard.gameObject.SetActive(false));
        }

        public void Flash(Color c, float alpha = 0.35f, float duration = 0.35f)
        {
            Tween(duration, t => flash.color = new Color(c.r, c.g, c.b, alpha * (1 - t)));
        }

        public void Popup(Vector3 world, string text, Color color, float size = 44)
        {
            var cam = Camera.main;
            if (!cam) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, sp, null, out var local);
            var t = popupPool.Count > 0 ? popupPool.Dequeue() : CreatePopup();
            t.gameObject.SetActive(true);
            t.text = text;
            t.fontSizeMax = size;
            t.fontSize = size;
            t.color = color;
            var rt = t.rectTransform;
            rt.anchoredPosition = local;
            Tween(0.9f, k =>
            {
                rt.anchoredPosition = local + new Vector2(0, 140 * EaseOutCubic(k));
                rt.localScale = Vector3.one * (k < 0.2f ? Mathf.LerpUnclamped(0.5f, 1.1f, EaseOutBack(k / 0.2f)) : 1.1f - (k - 0.2f) * 0.2f);
                Group(rt).alpha = k > 0.6f ? (1 - k) / 0.4f : 1;
            }, () => { t.gameObject.SetActive(false); popupPool.Enqueue(t); });
        }

        TextMeshProUGUI CreatePopup()
        {
            var t = Label(popupLayer, "", 44, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 80));
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(255, 255, 255, 255);
            t.fontStyle = FontStyles.Bold;
            t.enableWordWrapping = false;
            return t;
        }

        public System.Collections.IEnumerator Countdown()
        {
            string[] steps = { "3", "2", "1", "GO!" };
            for (int i = 0; i < steps.Length; i++)
            {
                countdownText.text = steps[i];
                countdownText.color = i == 3 ? Palette.Gold : Color.white;
                game.PlaySfx(i == 3 ? SfxBank.Go : SfxBank.Beep);
                var tr = countdownText.transform;
                Tween(0.6f, t =>
                {
                    tr.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, EaseOutBack(Mathf.Clamp01(t * 1.6f)));
                    var c = countdownText.color; c.a = t > 0.7f ? (1 - t) / 0.3f : 1; countdownText.color = c;
                });
                yield return new WaitForSecondsRealtime(i == 3 ? 0.5f : 0.62f);
            }
            countdownText.text = "";
        }

        // ------------------------------------------------------------------ pause

        void BuildPause()
        {
            pause = Stretch(root, "Pause");
            Img(Stretch(pause, "Dim"), White, new Color(0.15f, 0.04f, 0.12f, 0.55f));
            var card = Card(pause, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 680), Palette.Cream);
            Label(card, T("暫停中", "Paused", "一時停止"), 72, Palette.Wine, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(600, 100));
            Btn(card, T("繼續遊戲", "Resume", "つづける"), new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(480, 110), Style.Primary, game.Resume, 44);
            Btn(card, T("重新開始", "Restart", "やり直す"), new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(480, 100), Style.Secondary, game.Restart, 40);
            Btn(card, T("回主畫面", "Home", "タイトルへ"), new Vector2(0.5f, 1), new Vector2(0, -480), new Vector2(480, 100), Style.Secondary, game.GoHome, 40);
            LangButton(card, new Vector2(0.5f, 0), new Vector2(-220, 70), new Vector2(200, 80), Style.Gold);
            musicBtn2 = Btn(card, "音樂 ON", new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(200, 80), Style.Gold, () => { game.ToggleMusic(); RefreshSoundButtons(); }, 32);
            sfxBtn2 = Btn(card, "音效 ON", new Vector2(0.5f, 0), new Vector2(220, 70), new Vector2(200, 80), Style.Gold, () => { game.ToggleSfx(); RefreshSoundButtons(); }, 32);
        }

        public void ShowPause(bool on)
        {
            if (on) Show(pause); else Hide(pause, 0.12f);
        }

        // ------------------------------------------------------------------ game over

        void BuildGameOver()
        {
            gameOver = Stretch(root, "GameOver");
            Img(Stretch(gameOver, "Dim"), White, new Color(0.15f, 0.04f, 0.12f, 0.22f));
            var card = Card(gameOver, "Card", new Vector2(0.5f, 0.5f), new Vector2(250, -10), new Vector2(1060, 900), Palette.Cream);

            var head = Rect(card, "Head", new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(1060, 150), new Vector2(0.5f, 1));
            Img(head, Round32, Palette.Rose);
            var headFix = Rect(card, "HeadFix", new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1060, 40), new Vector2(0.5f, 1));
            Img(headFix, White, Palette.Rose);
            Label(head, T("休息一下～", "Take a break~", "ひと休み～"), 66, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1000, 100));

            goReason = Label(card, "", 34, Palette.Muted, new Vector2(0.5f, 1), new Vector2(0, -192), new Vector2(980, 50), TextAlignmentOptions.Center, false);
            Label(card, "SCORE", 30, Palette.Rose, new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(400, 40));
            goScore = Label(card, "0", 140, Palette.Wine, new Vector2(0.5f, 1), new Vector2(0, -335), new Vector2(900, 150));

            goRecord = Card(card, "Record", new Vector2(0.5f, 1), new Vector2(330, -268), new Vector2(300, 66), Palette.Gold, Pill, 0.15f);
            goRecord.localEulerAngles = new Vector3(0, 0, -8);
            Label(goRecord, T("新紀錄 NEW!", "NEW RECORD!", "ハイスコア！"), 32, Palette.Wine);

            string[] names = { T("距離", "Distance", "距離"), T("收集加分", "Bonus", "ボーナス"), T("扣分", "Penalty", "マイナス"), T("最高連擊", "Best Combo", "最大コンボ") };
            for (int i = 0; i < 4; i++)
            {
                var tile = Card(card, "Stat" + i, new Vector2(0.5f, 1), new Vector2(-375 + i * 250, -490), new Vector2(230, 120), Palette.Blush, Round16, 0);
                Label(tile, names[i], 28, Palette.Muted, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(220, 40), TextAlignmentOptions.Center, false);
                goStats[i] = Label(tile, "0", 44, Palette.Wine, new Vector2(0.5f, 0), new Vector2(0, 38), new Vector2(220, 60));
            }
            goStage = Label(card, "", 32, Palette.Wine, new Vector2(0.5f, 1), new Vector2(0, -585), new Vector2(980, 50), TextAlignmentOptions.Center, false);

            goSubmitRow = Rect(card, "SubmitRow", new Vector2(0.5f, 1), new Vector2(0, -665), new Vector2(980, 96));
            nameInput = InputField(goSubmitRow, new Vector2(-150, 0), new Vector2(560, 90), T("輸入你的名字", "Your name", "名前を入力"));
            submitBtn = Btn(goSubmitRow, T("上傳成績", "Submit", "スコア送信"), new Vector2(0.5f, 0.5f), new Vector2(285, 0), new Vector2(300, 90), Style.Gold, OnSubmit, 36);
            goWeb = Label(card, T("成績已送出！", "Score sent!", "スコアを送信しました！"), 34, Palette.Good, new Vector2(0.5f, 1), new Vector2(0, -665), new Vector2(900, 60));

            Btn(card, T("再玩一次", "Play Again", "もう一度"), new Vector2(0.5f, 0), new Vector2(-150, 80), new Vector2(380, 116), Style.Primary, game.Restart, 46);
            Btn(card, T("主畫面", "Home", "タイトルへ"), new Vector2(0.5f, 0), new Vector2(190, 80), new Vector2(260, 116), Style.Secondary, game.GoHome, 42);
        }

        TMP_InputField InputField(Transform parent, Vector2 pos, Vector2 size, string placeholder)
        {
            var rt = Card(parent, "Input", new Vector2(0.5f, 0.5f), pos, size, Color.white, Pill, 0.12f);
            var area = Stretch(rt, "TextArea");
            area.offsetMin = new Vector2(36, 8); area.offsetMax = new Vector2(-36, -8);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Label(area, placeholder, 36, new Color(Palette.Muted.r, Palette.Muted.g, Palette.Muted.b, 0.6f), TextAlignmentOptions.Left, false);
            var txt = Label(area, "", 36, Palette.Ink, TextAlignmentOptions.Left, false);
            txt.enableWordWrapping = false;
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = Regular;
            input.characterLimit = 16;
            input.targetGraphic = rt.Find("Bg").GetComponent<Image>();
            input.text = PlayerPrefs.GetString("wr_name", "");
            return input;
        }

        async void OnSubmit()
        {
            submitBtn.interactable = false;
            SetButtonText(submitBtn, T("上傳中…", "Sending…", "送信中…"));
            PlayerPrefs.SetString("wr_name", nameInput.text);
            bool ok = await game.SubmitScore(nameInput.text);
            if (!submitBtn) return;
            SetButtonText(submitBtn, ok ? T("已上傳", "Sent", "送信完了") : T("失敗，重試", "Failed, retry", "失敗・再試行"));
            submitBtn.interactable = !ok;
            if (ok) OpenRanking();
        }

        public void ShowGameOver(RunResult r)
        {
            HideHUD();
            goReason.text = r.hitBy != null ? T($"前面有「{r.hitBy}」，喘口氣再出發吧！", $"Bumped into the {r.hitBy}. Catch your breath and go again!", $"「{r.hitBy}」にぶつかっちゃった。ひと息ついて、また出発！") : "";
            goStats[0].text = r.distance + " m";
            goStats[1].text = "+" + r.plus;
            goStats[2].text = r.minus.ToString();
            goStats[3].text = r.maxCombo.ToString();
            var s = Stages.List[r.stage];
            string lapStr = r.lap > 0 ? T($"第 {r.lap + 1} 輪 · ", $"Lap {r.lap + 1} · ", $"{r.lap + 1}周目 · ") : "";
            goStage.text = T($"到達：{lapStr}{s.Name} {s.nameEn}", $"Reached: {lapStr}{s.nameEn}", $"到達：{lapStr}{s.Name}");
            goRecord.gameObject.SetActive(r.newRecord);
            goSubmitRow.gameObject.SetActive(OnlineRanking.Available);
            goWeb.gameObject.SetActive(Platform.IsWebGL);
            submitBtn.interactable = true;
            SetButtonText(submitBtn, T("上傳成績", "Submit", "スコア送信"));
            Show(gameOver);
            // count-up score
            goScore.text = "0";
            Tween(0.9f, t => goScore.text = Mathf.RoundToInt(r.score * EaseOutCubic(t)).ToString("N0"), () => { if (r.newRecord) Punch(goRecord, 0.25f, 0.4f); }, 0.25f);
        }

        public void HideGameOver() => Hide(gameOver, 0.15f);

        // ------------------------------------------------------------------ ranking

        void BuildRanking()
        {
            ranking = Stretch(root, "Ranking");
            Img(Stretch(ranking, "Dim"), White, new Color(0.15f, 0.04f, 0.12f, 0.6f));
            var card = Card(ranking, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 820), Palette.Cream);
            Label(card, T("排行榜 RANKING", "RANKING", "ランキング"), 60, Palette.Wine, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(800, 90));
            Color[] medal = { Palette.Gold, H("#C9CED8"), H("#E0A178"), Palette.Blush, Palette.Blush };
            for (int i = 0; i < 5; i++)
            {
                var row = Card(card, "Row" + i, new Vector2(0.5f, 1), new Vector2(0, -190 - i * 104), new Vector2(780, 88), Color.white, Pill, 0.08f);
                var c = Rect(row, "Rank", new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(64, 64));
                Img(c, Circle, medal[i]);
                Label(c, (i + 1).ToString(), 34, Palette.Wine);
                var n = Label(row, "-", 36, Palette.Ink, new Vector2(0, 0.5f), new Vector2(330, 0), new Vector2(440, 70), TextAlignmentOptions.Left, false);
                var s = Label(row, "-", 40, Palette.Rose, new Vector2(1, 0.5f), new Vector2(-140, 0), new Vector2(240, 70), TextAlignmentOptions.Right);
                rankRows.Add(new[] { n, s });
            }
            rankStatus = Label(card, "", 30, Palette.Muted, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(800, 50), TextAlignmentOptions.Center, false);
            Btn(card, T("返回", "Back", "とじる"), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(300, 100), Style.Primary, () => Hide(ranking), 40);
        }

        public async void OpenRanking()
        {
            Show(ranking);
            foreach (var r in rankRows) { r[0].text = "-"; r[1].text = "-"; }
            rankStatus.text = T("讀取中…", "Loading…", "よみこみ中…");
            var list = await OnlineRanking.Top(5);
            if (!rankStatus) return;
            if (list == null) { rankStatus.text = T("無法連線到排行榜", "Can't reach the ranking server", "ランキングにつながりません"); return; }
            rankStatus.text = list.Count == 0 ? T("還沒有紀錄，成為第一名吧！", "No scores yet — be the first!", "まだスコアがありません。1位をめざそう！") : "";
            for (int i = 0; i < list.Count && i < 5; i++)
            {
                rankRows[i][0].text = list[i].Key;
                rankRows[i][1].text = ((int)list[i].Value).ToString("N0");
            }
        }

        // ------------------------------------------------------------------ how to play

        void BuildHowTo()
        {
            howTo = Stretch(root, "HowTo");
            Img(Stretch(howTo, "Dim"), White, new Color(0.15f, 0.04f, 0.12f, 0.6f));
            var card = Card(howTo, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 960), Palette.Cream);
            Label(card, T("玩法說明", "How to Play", "遊び方"), 60, Palette.Wine, new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(800, 90));
            Label(card, T("跳躍：點擊畫面 / 空白鍵 / ↑（按住更高・空中再點一次＝二段跳）　·　滑行：往下滑 / ↓（空中＝急降）\n香港 → 台北 → 金門 → 名古屋 → 婚禮會場，之後四個城市無限循環、越跑越快！", "Jump: tap / Space / ↑ (hold = higher, tap again in the air = double jump)  ·  Slide: swipe down / ↓ (in the air = drop)\nHong Kong → Taipei → Kinmen → Nagoya → Wedding, then the 4 cities loop forever, faster and faster!", "ジャンプ：タップ / スペース / ↑（長押しで高く・空中でもう一度＝2段ジャンプ）　·　スライド：下スワイプ / ↓（空中＝急降下）\n香港 → 台北 → 金門 → 名古屋 → 結婚式場、そのあとは4都市を無限ループ、どんどん速くなる！"), 32, Palette.Ink,
                new Vector2(0.5f, 1), new Vector2(0, -165), new Vector2(1400, 100), TextAlignmentOptions.Center, false);

            ModelId[] good = { ModelId.Heart, ModelId.Ring, ModelId.RedPacket, ModelId.PineappleBun, ModelId.GongCandy, ModelId.BubbleTea, ModelId.EbiFry, ModelId.MisoKatsu, ModelId.WeddingCookie, ModelId.Shield };
            ModelId[] bad = { ModelId.AlarmClock, ModelId.Briefcase, ModelId.RainCloud };
            ModelId[] obs = { ModelId.Cone, ModelId.GiftStack2, ModelId.LiquorJar, ModelId.Suitcase, ModelId.ChampagneTower, ModelId.OverheadBanner, ModelId.OverheadFirecracker, ModelId.SumoPig };

            Column(card, -480, T("加分道具", "Bonus items", "プラスアイテム"), Palette.Good, good, true);
            Column(card, 0, T("扣分道具（不會結束）", "Minus items (keep running)", "マイナスアイテム（セーフ）"), Palette.Bad, bad, true);
            Column(card, 480, T("障礙物（撞到就 Game Over）", "Obstacles (Game Over)", "障害物（ぶつかるとアウト）"), Palette.Wine, obs, false);
            Label(card, T("連續收集加分道具可觸發連擊加倍：3連擊 ×1.5、6連擊 ×2、10連擊 ×3　·　囍字護盾可擋下一次障礙物", "Collect bonus items in a row for a combo: 3 = ×1.5, 6 = ×2, 10 = ×3  ·  The 囍 shield blocks one obstacle", "プラスアイテムを連続で取るとコンボ：3連 ×1.5、6連 ×2、10連 ×3　·　「囍」シールドは障害物を1回ガード"), 28, Palette.Muted,
                new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1400, 50), TextAlignmentOptions.Center, false);
            Btn(card, T("知道了！", "Got it!", "OK！"), new Vector2(0.5f, 0), new Vector2(0, 72), new Vector2(340, 96), Style.Primary, () => Hide(howTo), 40);
        }

        void Column(RectTransform card, float x, string header, Color color, ModelId[] ids, bool showPoints)
        {
            var hdr = Card(card, "Hdr" + header, new Vector2(0.5f, 1), new Vector2(x, -255), new Vector2(440, 60), color, Pill, 0);
            Label(hdr, header, 28, Color.white);
            float rowH = Mathf.Min(58, 450f / ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                var def = ItemDef.Get(ids[i]);
                var row = Rect(card, "Row", new Vector2(0.5f, 1), new Vector2(x, -315 - i * rowH), new Vector2(440, rowH));
                var icon = Rect(row, "Icon", new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(rowH, rowH));
                var raw = icon.gameObject.AddComponent<RawImage>();
                raw.texture = IconStudio.Get(ids[i]);
                raw.raycastTarget = false;
                Label(row, def.Name, 30, Palette.Ink, new Vector2(0, 0.5f), new Vector2(195, 0), new Vector2(210, rowH), TextAlignmentOptions.Left, false);
                string pts = def.kind == ItemKind.Shield ? T("擋1次", "Blocks 1", "1回ガード") : (showPoints ? (def.points > 0 ? "+" + def.points : def.points.ToString()) : (def.overhead ? T("要滑行", "Slide", "スライド") : def.charge > 0 ? T("會衝過來", "Charges!", "突進！") : def.tall ? T("要跳高", "Jump high", "高くジャンプ") : ""));
                Label(row, pts, 28, color, new Vector2(1, 0.5f), new Vector2(-65, 0), new Vector2(130, rowH), TextAlignmentOptions.Right);
            }
        }

        public void OpenHowTo() => Show(howTo);

        public void CloseModals()
        {
            Hide(howTo, 0.1f);
            Hide(ranking, 0.1f);
        }

        static Color H(string hex) => Stages.C(hex);
    }

    /// <summary>Renders each 3D model once into a small transparent texture for the UI legend.</summary>
    public static class IconStudio
    {
        static readonly Dictionary<ModelId, RenderTexture> cache = new Dictionary<ModelId, RenderTexture>();
        static Camera cam;
        const int Layer = 31;

        public static Texture Get(ModelId id)
        {
            if (cache.TryGetValue(id, out var rt) && rt != null) return rt;
            if (cam == null)
            {
                var go = new GameObject("IconCamera");
                Object.DontDestroyOnLoad(go);
                cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.cullingMask = 1 << Layer;
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
            }
            var holder = new GameObject("IconModel").transform;
            holder.position = new Vector3(0, -1000, 0);
            var model = ModelFactory.Spawn(id, holder);
            model.transform.localRotation = Quaternion.Euler(0, -20, 0);
            ModelFactory.SetLayer(holder.gameObject, Layer);

            var bounds = new Bounds(holder.position, Vector3.zero);
            bool first = true;
            foreach (var r in holder.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.12f + 0.02f;
            cam.transform.position = bounds.center + new Vector3(0, 0, -10);
            cam.transform.rotation = Quaternion.identity;

            rt = new RenderTexture(160, 160, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Icon_" + id };
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            cam.targetTexture = null;
            Object.DestroyImmediate(holder.gameObject);
            cache[id] = rt;
            return rt;
        }
    }
}
