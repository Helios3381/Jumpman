using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WeddingRun
{
    /// <summary>
    /// Wedding runner — main game loop. Everything (characters, city backgrounds, items and UI) is generated
    /// in code, so the scene only needs this component plus a camera and a light.
    /// </summary>
    public class WeddingGame : MonoBehaviour
    {
        [Header("Couple")]
        public string groomName = "凱銘";
        public string brideName = "詠萱";
        public string weddingDate = "2027.01.01";

        [Header("Assets (assigned by Tools > Wedding Run > Build Scene)")]
        public Material litMaterial;
        public Material unlitMaterial;
        public Material vertexColorMaterial;
        public Material fadeMaterial;
        public TMP_FontAsset fontBold;
        public TMP_FontAsset fontRegular;
        [Tooltip("Fallback for Japanese-only kanji that the Chinese font does not have")]
        public TMP_FontAsset fontJapanese;
        public AudioClip music;
        public AudioClip gameOverMusic;
        public AudioClip jumpSound;
        public AudioClip crashSound;

        [Header("Online")]
        public string leaderboardId = "highscores";

        [Header("Debug")]
        public bool autoPilot;
        public float debugStartDistance = 0;

        enum State { Title, Countdown, Playing, Paused, Dying, GameOver, Celebrating }
        State state = State.Title;

        class Hazard
        {
            public ItemDef def;
            public Transform t;
            public float x, y, bobAmp, bobPhase;
            public float width, vx;     // hitbox width (stretchable obstacles) and extra speed (charging obstacles)
            public bool done;
        }

        Camera cam;
        Light sun;
        WorldEnvironment env;
        CoupleRunner runner;
        GameUI ui;
        AudioSource musicSource, sfxSource;
        Transform hazardRoot;
        readonly List<Hazard> hazards = new List<Hazard>();

        // run data
        float distance, speed, nextPatternIn, comboTimer, shake, invulnerable, dyingTimer;
        int stage, lap, itemScore, plus, minus, combo, maxCombo, best;
        bool recordAnnounced;
        Coroutine countdownCo;
        string hitBy;

        public bool MusicOn { get; private set; } = true;
        public bool SfxOn { get; private set; } = true;

        const float SpawnX = 22f, DespawnX = -18f;
        const float TitleSpeed = 7f;

        int Score => Mathf.Max(0, Stages.DistancePoints(distance) + itemScore);
        float Multiplier => combo >= 10 ? 3f : combo >= 6 ? 2f : combo >= 3 ? 1.5f : 1f;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            Application.targetFrameRate = 60;
            OnlineRanking.LeaderboardId = leaderboardId;
            // Let the dynamic CJK font grow extra atlas pages instead of running out of space.
            foreach (var f in new[] { fontBold, fontRegular, fontJapanese })
                if (f != null) f.isMultiAtlasTexturesEnabled = true;
            if (fontJapanese != null)
                foreach (var f in new[] { fontBold, fontRegular })
                    if (f != null && f != fontJapanese)
                    {
                        if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new List<TMP_FontAsset>();
                        if (!f.fallbackFontAssetTable.Contains(fontJapanese))
                            f.fallbackFontAssetTable.Add(fontJapanese);
                    }
            Loc.Load();
            SfxBank.Build();
            ModelFactory.Init(litMaterial, unlitMaterial, vertexColorMaterial, fadeMaterial, fontBold);
            UIKit.Init(this, fontBold, fontRegular != null ? fontRegular : fontBold);
            UIKit.ClickSound = () => PlaySfx(SfxBank.Click, 0.8f);
            UIKit.HoverSound = () => PlaySfx(SfxBank.Hover, 0.5f);

            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.gameObject.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.cullingMask &= ~(1 << 31);

            sun = FindObjectOfType<Light>();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 230f;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true; musicSource.volume = 0.55f; musicSource.playOnAwake = false;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            MusicOn = PlayerPrefs.GetInt("wr_music", 1) == 1;
            SfxOn = PlayerPrefs.GetInt("wr_sfx", 1) == 1;
            musicSource.mute = !MusicOn; sfxSource.mute = !SfxOn;

            env = new GameObject("World").AddComponent<WorldEnvironment>();
            env.Build(cam, sun);
            env.SetStage(0, true);

            hazardRoot = new GameObject("Hazards").transform;
            runner = new GameObject("Couple").AddComponent<CoupleRunner>();
            runner.Build();
            runner.Jumped += () => PlaySfx(jumpSound, 0.55f);
            runner.DoubleJumped += () => { PlaySfx(SfxBank.Whoosh, 0.9f); PlaySfx(jumpSound, 0.4f, 1.25f); };
            runner.Kissed += OnKiss;
            runner.ProposalAccepted += OnProposalAccepted;
            runner.TeaServed += OnTeaServed;
            runner.Landed += () => env.Burst(new Vector3(CoupleRunner.GroomX, 0.1f, 0), new Color(1, 1, 1, 0.8f), 5, 0.4f);
            runner.state = CoupleRunner.State.Run;

            ui = gameObject.AddComponent<GameUI>();
            ui.Build(this);
            ApplyTitleLook();

            best = PlayerPrefs.GetInt("wr_best", 0);
            ui.SetBest(best);
            LoadOnlineBest();

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoshot") >= 0)
                gameObject.AddComponent<AutoShot>();
        }

        void Start()
        {
            // On the web page the music starts when the guest enters full screen (WebStartBGM).
            if (webAudioAllowed) PlayMusic(music, true);
            ui.ShowTitle();
            FitCamera();
        }

        async void LoadOnlineBest()
        {
            var online = await OnlineRanking.MyBest();
            if (online.HasValue && online.Value > best)
            {
                best = (int)online.Value;
                ui.SetBest(best);
            }
        }

        // ------------------------------------------------------------------ public API for UI

        public void StartRun()
        {
            if (state != State.Title && state != State.GameOver) return;
            ui.HideTitle();
            ui.HideGameOver();
            ResetRun();
            countdownCo = StartCoroutine(CountdownThenPlay());
        }

        public void Restart()
        {
            Time.timeScale = 1;
            ui.ShowPause(false);
            if (state == State.Title) return;
            state = State.GameOver;
            PlayMusic(music, true);
            StartRun();
        }

        public void GoHome()
        {
            Time.timeScale = 1;
            if (countdownCo != null) StopCoroutine(countdownCo);
            ui.ShowPause(false);
            ui.HideHUD();
            ui.HideGameOver();
            ResetRun();
            ApplyTitleLook();
            state = State.Title;
            PlayMusic(music, true);
            ui.SetBest(best);
            ui.ShowTitle();
        }

        /// <summary>True once the player has reached the wedding venue at least once (saved on this device).</summary>
        public static bool HasReachedWedding
        {
            get => PlayerPrefs.GetInt("wr_wedded", 0) == 1;
            set { PlayerPrefs.SetInt("wr_wedded", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Title screen: the groom alone at first; the couple in wedding clothes once the wedding was reached.</summary>
        void ApplyTitleLook()
        {
            bool wedded = HasReachedWedding;
            runner.SetBrideVisible(wedded, false);
            runner.SetFormal(wedded, false);
        }

        /// <summary>中文 → English → 日本語 → …  (title screen and pause menu)</summary>
        public void ChangeLanguage()
        {
            Loc.Set(Loc.Next);
            ui.Rebuild();
            ui.SetBest(best);
            ui.SetCombo(combo, Multiplier);
            ui.SetShield(runner.HasShield);
        }

        public void Pause()
        {
            if (state != State.Playing) return;
            state = State.Paused;
            Time.timeScale = 0;
            ui.ShowPause(true);
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            ui.ShowPause(false);
            Time.timeScale = 1;
            state = State.Playing;
        }

        public void ToggleMusic()
        {
            MusicOn = !MusicOn;
            musicSource.mute = !MusicOn;
            PlayerPrefs.SetInt("wr_music", MusicOn ? 1 : 0);
        }

        public void ToggleSfx()
        {
            SfxOn = !SfxOn;
            sfxSource.mute = !SfxOn;
            PlayerPrefs.SetInt("wr_sfx", SfxOn ? 1 : 0);
        }

        public Task<bool> SubmitScore(string playerName) => OnlineRanking.Submit(playerName, Score);

        public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || sfxSource == null) return;
            if (Mathf.Approximately(pitch, 1f)) { sfxSource.PlayOneShot(clip, volume); return; }
            var src = gameObject.AddComponent<AudioSource>();
            src.mute = !SfxOn; src.pitch = pitch; src.PlayOneShot(clip, volume);
            Destroy(src, clip.length / pitch + 0.1f);
        }

        void PlayMusic(AudioClip clip, bool loop)
        {
            if (clip == null) return;
            if (!webAudioAllowed && state == State.Title) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip; musicSource.loop = loop; musicSource.Play();
        }

        // ------------------------------------------------------------------ run flow

        void ResetRun()
        {
            foreach (var h in hazards) if (h.t) Destroy(h.t.gameObject);
            hazards.Clear();
            if (celebrateCo != null) { StopCoroutine(celebrateCo); celebrateCo = null; }
            foreach (var g in guests) if (g.t) Destroy(g.t.gameObject);
            guests.Clear();
            celebrated = false;
            proposed = false;
            if (proposeCo != null) { StopCoroutine(proposeCo); proposeCo = null; }
            engaged = false;
            if (engageCo != null) { StopCoroutine(engageCo); engageCo = null; }
            distance = debugStartDistance; itemScore = 0; plus = 0; minus = 0; combo = 0; maxCombo = 0;
            comboTimer = 0; invulnerable = 0; recordAnnounced = false; hitBy = null;
            nextPatternIn = 16f;
            stage = Stages.IndexAt(distance);
            lap = Stages.LapAt(distance);
            curTheme = Stages.ThemeAt(distance);
            env.SetStage(curTheme, false);
            env.Spectate = false;
            runner.ResetRunner(lap > 0 || stage >= 1);   // Hong Kong: he runs alone
            if (lap > 0 || Stages.List[stage].theme == 4) runner.SetFormal(true, false);
            else if (Stages.List[stage].theme == 6) runner.SetOutfit(ModelFactory.Outfit.Engagement, false);
            runner.state = CoupleRunner.State.Run;
            speed = TitleSpeed;
            ui.SetCombo(0, 1);
            ui.SetShield(false);
        }

        IEnumerator CountdownThenPlay()
        {
            state = State.Countdown;
            ui.ShowHUD();
            ui.SetScore(0, distance);
            yield return ui.Countdown();
            state = State.Playing;
            ui.StageBanner(Stages.List[stage], lap);
        }

        void Die(ItemDef by)
        {
            hitBy = by.Name;
            runner.Die();
            state = State.Dying;
            dyingTimer = 0;
            shake = 0.25f;
            PlaySfx(SfxBank.Bump, 0.9f);
            env.Burst(new Vector3(CoupleRunner.GroomX + 0.4f, runner.Y + 1.4f, -0.3f), Palette.Gold, 18, 0.7f);
            ui.Flash(Palette.Blush, 0.25f, 0.4f);
            musicSource.Stop();
        }

        void FinishRun()
        {
            state = State.GameOver;
            Time.timeScale = 1;
            int score = Score;
            bool newRecord = score > best;
            if (newRecord) { best = score; PlayerPrefs.SetInt("wr_best", best); PlayerPrefs.Save(); }
            PlayMusic(gameOverMusic, false);
            if (Platform.IsWebGL) Platform.SendScoreToPage(score);
            ui.ShowGameOver(new GameUI.RunResult
            {
                score = score, distance = Mathf.FloorToInt(distance), plus = plus, minus = minus,
                maxCombo = maxCombo, stage = stage, lap = lap, hitBy = hitBy, newRecord = newRecord,
            });
            if (newRecord) env.Confetti(new Vector3(0, 4, -2));
        }

        // ------------------------------------------------------------------ main loop

        void Update()
        {
            FitCamera();
            float dt = Time.deltaTime;

            bool pressed = false, held = false, slide = false, slideHeld = false;
            if (state == State.Playing)
            {
                ReadInput(out pressed, out held, out slide, out slideHeld);
                if (autoPilot) AutoPilot(ref pressed, ref held, ref slide, ref slideHeld);
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                if (state == State.Playing) Pause(); else if (state == State.Paused) Resume();
            }

            switch (state)
            {
                case State.Title:
                case State.Countdown:
                    speed = Mathf.MoveTowards(speed, state == State.Countdown ? Stages.SpeedAt(distance) * 0.8f : TitleSpeed, dt * 4f);
                    break;
                case State.Playing:
                    distance += speed * dt;
                    speed = Mathf.MoveTowards(speed, Stages.SpeedAt(distance), dt * 6f);
                    UpdateStage();
                    UpdateSpawning(dt);
                    if (lap == 0 && !proposed && distance >= ProposeAt) proposeCo = StartCoroutine(Proposal());
                    else if (lap == 0 && !engaged && distance >= EngageAt) engageCo = StartCoroutine(Engagement());
                    else if (lap == 0 && !celebrated && distance >= CelebrateAt) celebrateCo = StartCoroutine(WeddingFinale());
                    break;
                case State.Celebrating:
                    distance += speed * dt;     // the finale coroutine brings the speed down to 0
                    break;
                case State.Dying:
                    dyingTimer += Time.unscaledDeltaTime;
                    Time.timeScale = dyingTimer < 0.35f ? 0.5f : 1f;
                    speed = Mathf.MoveTowards(speed, 0, Time.unscaledDeltaTime * 45f);
                    if (dyingTimer > 1.6f) FinishRun();
                    break;
                case State.GameOver:
                    speed = 0;
                    break;
            }

            if (state != State.Paused)
            {
                runner.Tick(dt, speed, pressed, held, slide, slideHeld);
                if (runner.Sliding && state == State.Playing && Random.value < dt * 30f)
                    env.Burst(new Vector3(CoupleRunner.GroomX + 0.6f, 0.1f, -0.2f), new Color(1, 1, 1, 0.7f), 1, 0.3f);
                env.Tick(dt, speed);
                UpdateHazards(dt);
                UpdateGuests(dt);
            }

            if (state == State.Playing || state == State.Countdown || state == State.Celebrating)
            {
                if (state != State.Celebrating && comboTimer > 0 && (comboTimer -= dt) <= 0) { combo = 0; ui.SetCombo(0, 1); }
                ui.SetScore(Score, distance);
                ui.SetProgress(Stages.JourneyProgress(distance), stage, lap);
                if (!recordAnnounced && best > 0 && Score > best && state == State.Playing)
                {
                    recordAnnounced = true;
                    ui.Toast(Loc.T("新紀錄！", "NEW RECORD!", "新記録！"));
                    PlaySfx(SfxBank.Fanfare, 0.7f);
                }
            }
            if (invulnerable > 0) invulnerable -= dt;
        }

        /// <summary>
        /// Keyboard: Space/↑/W = jump (hold = higher), ↓/S = slide.
        /// Touch / mouse: touch the screen = jump (keep touching = higher, touch again in the air = double jump);
        /// swipe down = slide (keep the finger down = keep sliding). A swipe right after the touch turns the hop into a slide.
        /// </summary>
        void ReadInput(out bool pressed, out bool held, out bool slide, out bool slideHeld)
        {
            pressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            held = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            slide = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            slideHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);

            var es = EventSystem.current;
            bool began, down;
            Vector2 pos;
            int id;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                id = t.fingerId; pos = t.position;
                began = t.phase == TouchPhase.Began;
                down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }
            else
            {
                id = -1; pos = Input.mousePosition;
                began = Input.GetMouseButtonDown(0);
                down = Input.GetMouseButton(0);
            }

            if (began)
            {
                bool overUI = es != null && (id >= 0 ? es.IsPointerOverGameObject(id) : es.IsPointerOverGameObject());
                gesture = overUI ? Gesture.None : Gesture.Touch;
                gestureStart = pos;
                if (!overUI) pressed = true;           // touching the screen jumps right away
            }
            if (!down) { gesture = Gesture.None; return; }

            if (gesture == Gesture.Touch && pos.y - gestureStart.y < -Mathf.Max(30f, Screen.height * 0.045f))
            {
                gesture = Gesture.Down;
                slide = true;
            }
            if (gesture == Gesture.Touch) held = true;
            if (gesture == Gesture.Down) slideHeld = true;
        }

        enum Gesture { None, Touch, Down }
        Gesture gesture = Gesture.None;
        Vector2 gestureStart;

        int curTheme = -1;

        void UpdateStage()
        {
            int th = Stages.ThemeAt(distance);
            if (th != curTheme)
            {
                curTheme = th;
                env.SetStage(th, false);
            }
            int s = Stages.IndexAt(distance);
            int l = Stages.LapAt(distance);
            if (s == stage && l == lap) return;
            stage = s; lap = l;
            var def = Stages.List[s];
            ui.StageBanner(def, lap);
            PlaySfx(SfxBank.Fanfare, 0.6f);
            if (def.brideJoins && !runner.BrideVisible)
            {
                // Taipei: where they met — she joins the run.
                runner.SetBrideVisible(true, true);
                PlaySfx(SfxBank.Join, 0.8f);
                StartCoroutine(JoinHearts());
            }
            if (def.theme == 6 && runner.CurrentOutfit == ModelFactory.Outfit.Casual)
            {
                // arriving at her family home in Kinmen for the 文定: a red qipao for her, a suit for him
                runner.SetOutfit(ModelFactory.Outfit.Engagement, true);
                PlaySfx(SfxBank.Join, 0.8f);
                env.Burst(new Vector3(CoupleRunner.GroomX, 1f, 0), Palette.Gold, 40, 1.2f);
                env.Burst(runner.BridePosition + Vector3.up, new Color(0.9f, 0.12f, 0.18f), 40, 1.2f);
            }
            if (def.theme == 4)
            {
                HasReachedWedding = true;
                env.Confetti(new Vector3(0, 3, -1));
                if (!runner.Formal)
                {
                    // arriving at the wedding venue: change into the tuxedo and the wedding dress
                    runner.SetFormal(true, true);
                    PlaySfx(SfxBank.Join, 0.8f);
                    env.Burst(new Vector3(CoupleRunner.GroomX, 1f, 0), Palette.Gold, 40, 1.2f);
                    env.Burst(runner.BridePosition + Vector3.up, Palette.Rose, 40, 1.2f);
                }
            }
        }

        IEnumerator JoinHearts()
        {
            for (int i = 0; i < 4; i++)
            {
                env.Burst(runner.BridePosition + Vector3.up * 1.2f, Palette.Rose, 16, 0.8f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        void FitCamera()
        {
            // Keep about 25 world units visible horizontally on any aspect ratio (phones in portrait too).
            float aspect = cam.aspect;
            float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // Portrait phones get a slightly tighter view (still ~16 m of look-ahead) and the road sits lower.
            float halfWidth = Mathf.Lerp(9.5f, 12.3f, Mathf.InverseLerp(0.6f, 1.4f, aspect));
            float d = Mathf.Max(19f, halfWidth / (halfH * aspect));
            float centerY = Mathf.Max(3.9f, d * halfH * 0.4f);
            Vector3 basePos = new Vector3(1.5f, centerY + d * Mathf.Tan(6f * Mathf.Deg2Rad), -d);
            Vector3 offset = Vector3.zero;
            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - Time.unscaledDeltaTime * 2.2f);
                offset = Random.insideUnitSphere * shake * 0.5f;
            }
            cam.transform.SetPositionAndRotation(basePos + offset, Quaternion.Euler(6f, 0, 0));
        }

        // ------------------------------------------------------------------ wedding finale

        /// <summary>The proposal happens near the end of the Port of Nagoya stage (first lap only).</summary>
        public static float ProposeAt => Stages.StartOf(0, Stages.PortStage) + Stages.List[Stages.PortStage].length - 48f;

        bool proposed;
        Coroutine proposeCo;

        /// <summary>
        /// Port of Nagoya at night, first lap, nobody else around: they stop by the harbour, he kneels with the ring,
        /// she says yes — hearts and fireworks over the water — then on to the wedding venue.
        /// </summary>
        IEnumerator Proposal()
        {
            state = State.Celebrating;
            proposed = true;
            foreach (var h in hazards)
                if (!h.done && h.t && h.x > CoupleRunner.GroomX - 1f)
                {
                    env.Burst(new Vector3(h.x, h.y + 0.5f, 0), Palette.Rose, 12, 0.8f);
                    Destroy(h.t.gameObject);
                    h.done = true;
                }
            while (speed > 0.05f)
            {
                speed = Mathf.MoveTowards(speed, 0, Time.deltaTime * 15f);
                yield return null;
            }
            speed = 0;
            runner.BeginPropose();          // just the two of them by the harbour at night
            yield return new WaitForSeconds(4.8f);
            runner.EndPropose();
            nextPatternIn = 24f;
            state = State.Playing;
            proposeCo = null;
        }

        // ------------------------------------------------------------------ engagement (文定, first lap)

        public static float EngageAt => Stages.StartOf(0, Stages.EngagementStage) + Stages.List[Stages.EngagementStage].length - 40f;
        bool engaged;
        Coroutine engageCo;

        /// <summary>文定 at her family home in Kinmen: they stop, she serves him sweet tea, red-and-gold firecrackers, then on to the wedding.</summary>
        IEnumerator Engagement()
        {
            state = State.Celebrating;
            engaged = true;
            foreach (var h in hazards)
                if (!h.done && h.t && h.x > CoupleRunner.GroomX - 1f)
                {
                    env.Burst(new Vector3(h.x, h.y + 0.5f, 0), Palette.Gold, 12, 0.8f);
                    Destroy(h.t.gameObject);
                    h.done = true;
                }
            while (speed > 0.05f)
            {
                speed = Mathf.MoveTowards(speed, 0, Time.deltaTime * 15f);
                yield return null;
            }
            speed = 0;
            runner.BeginEngage();
            yield return new WaitForSeconds(4.4f);
            runner.EndEngage();
            nextPatternIn = 24f;
            state = State.Playing;
            engageCo = null;
        }

        void OnTeaServed()
        {
            PlaySfx(SfxBank.Chime, 0.9f);
            PlaySfx(SfxBank.Applause, 0.7f);
            var at = new Vector3(CoupleRunner.GroomX - 0.5f, 2.0f, -0.2f);
            env.Burst(at, Palette.Gold, 36, 1.0f);
            env.Burst(at, new Color(0.9f, 0.12f, 0.18f), 36, 1.2f);
            env.Confetti(new Vector3(CoupleRunner.GroomX - 0.5f, 2.5f, 0.5f));
            ui.Toast(Loc.T("文定之喜！", "Happy engagement!", "婚約おめでとう！"));
            StartCoroutine(Firecrackers());
        }

        /// <summary>Strings of firecrackers popping red and gold beside the courtyard.</summary>
        IEnumerator Firecrackers()
        {
            for (int i = 0; i < 12; i++)
            {
                var at = new Vector3(CoupleRunner.GroomX + Random.Range(1.5f, 5f), Random.Range(0.3f, 2f), Random.Range(-0.5f, 1f));
                env.Burst(at, i % 2 == 0 ? new Color(0.9f, 0.12f, 0.18f) : Palette.Gold, 12, 0.5f);
                PlaySfx(SfxBank.Bump, 0.3f, Random.Range(1.6f, 2.2f));
                yield return new WaitForSeconds(Random.Range(0.06f, 0.14f));
            }
        }

        void OnProposalAccepted()
        {
            PlaySfx(SfxBank.Chime, 0.9f);
            PlaySfx(SfxBank.Join, 0.8f);
            var at = new Vector3(CoupleRunner.GroomX - 0.5f, 2.0f, -0.2f);
            env.Burst(at, Palette.Rose, 40, 1.1f);
            env.Burst(new Vector3(CoupleRunner.GroomX - 0.62f, 1.2f, -0.2f), new Color(0.8f, 0.95f, 1f), 24, 0.6f);   // ring sparkle
            env.Confetti(new Vector3(CoupleRunner.GroomX - 0.5f, 2.5f, 0.5f));
            StartCoroutine(HarbourFireworks());
        }

        IEnumerator HarbourFireworks()
        {
            for (int i = 0; i < 6; i++)
            {
                env.Firework(cam.transform.position + new Vector3(Random.Range(-25f, 30f), Random.Range(14f, 24f), 70f));
                yield return new WaitForSeconds(0.35f);
            }
        }

        /// <summary>Where the first-lap finale starts (just before the loop goes back to Hong Kong).</summary>
        static float CelebrateAt => Stages.FirstLapLength - 28f;

        bool celebrated;
        Coroutine celebrateCo;

        class Guest
        {
            public Transform t, armL, armR;
            public float x, z, phase, appear;
        }
        readonly List<Guest> guests = new List<Guest>();

        /// <summary>
        /// End of the wedding venue: the couple stops, hugs and kisses while guests pop up around them and cheer,
        /// then everyone keeps running (the guests stay behind and scroll away).
        /// </summary>
        IEnumerator WeddingFinale()
        {
            state = State.Celebrating;
            celebrated = true;
            // clear anything still on the road ahead
            foreach (var h in hazards)
                if (!h.done && h.t && h.x > CoupleRunner.GroomX - 1f)
                {
                    env.Burst(new Vector3(h.x, h.y + 0.5f, 0), Palette.Rose, 12, 0.8f);
                    Destroy(h.t.gameObject);
                    h.done = true;
                }

            while (speed > 0.05f)
            {
                speed = Mathf.MoveTowards(speed, 0, Time.deltaTime * 15f);
                yield return null;
            }
            speed = 0;

            runner.BeginCelebrate();
            PlaySfx(SfxBank.Fanfare, 0.8f);
            SpawnGuests();
            yield return new WaitForSeconds(0.5f);
            PlaySfx(SfxBank.Applause, 0.8f);
            for (int i = 0; i < 6; i++)
            {
                env.Confetti(new Vector3(CoupleRunner.GroomX - 0.3f + Random.Range(-3f, 3f), 2.5f, Random.Range(-0.5f, 1.5f)));
                yield return new WaitForSeconds(0.7f);
            }
            runner.EndCelebrate();
            nextPatternIn = 30f;
            state = State.Playing;
            celebrateCo = null;
        }

        void OnKiss()
        {
            PlaySfx(SfxBank.Chime, 0.9f);
            PlaySfx(SfxBank.Applause, 0.7f, 1.1f);
            var at = new Vector3(CoupleRunner.GroomX - 0.3f, 2.2f, -0.2f);
            env.Burst(at, Palette.Rose, 40, 1.1f);
            env.Burst(at, Palette.Gold, 25, 1.4f);
            env.Firework(cam.transform.position + new Vector3(-6f, 18f, 60f));
            env.Firework(cam.transform.position + new Vector3(10f, 22f, 60f));
        }

        void SpawnGuests()
        {
            var root = hazardRoot;
            // (x, z): back row behind the couple, two at the sides in front
            Vector2[] spots =
            {
                new Vector2(-9.6f, 2.4f), new Vector2(-8.0f, 3.0f), new Vector2(-6.6f, 2.6f), new Vector2(-4.0f, 2.7f),
                new Vector2(-2.4f, 3.0f), new Vector2(-1.0f, 2.3f), new Vector2(-11.2f, -3.9f), new Vector2(1.2f, -3.9f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var g = new Guest { x = spots[i].x, z = spots[i].y, phase = Random.value * 6f, appear = -i * 0.12f };
                g.t = ModelFactory.BuildGuest(root, 1000 + i * 37 + Random.Range(0, 9999), out g.armL, out g.armR);
                float dx = CoupleRunner.GroomX - 0.3f - g.x, dz = 0.2f - g.z;
                g.t.localRotation = Quaternion.Euler(0, Mathf.Atan2(-dz, dx) * Mathf.Rad2Deg, 0);
                g.t.localScale = Vector3.zero;
                g.t.position = new Vector3(g.x, 0, g.z);
                guests.Add(g);
            }
        }

        void UpdateGuests(float dt)
        {
            bool cheering = state == State.Celebrating;
            for (int i = guests.Count - 1; i >= 0; i--)
            {
                var g = guests[i];
                g.x -= speed * dt;
                g.appear += dt;
                float a = Mathf.Clamp01(g.appear / 0.35f);
                float tt = Time.time * 7f + g.phase;
                float hop = cheering ? Mathf.Abs(Mathf.Sin(tt)) * 0.28f : 0f;
                if (g.t)
                {
                    g.t.localScale = Vector3.one * (g.appear <= 0 ? 0 : UIKit.EaseOutBack(a));
                    g.t.position = new Vector3(g.x, hop, g.z);
                    float wave = Mathf.Sin(tt * 1.3f) * 25f;
                    g.armL.localRotation = Quaternion.Euler(0, 0, cheering ? 150 + wave : 20);
                    g.armR.localRotation = Quaternion.Euler(0, 0, cheering ? 150 - wave : 150 + wave);  // keep waving goodbye
                }
                if (g.x < DespawnX - 4f)
                {
                    if (g.t) Destroy(g.t.gameObject);
                    guests.RemoveAt(i);
                }
            }
        }

        public void DebugForceSlide() => runner.DebugSlide();
        public void DebugGiveShield() { runner.SetShield(true); ui.SetShield(true); }
        public void DebugSpawnAt(ModelId id, float x)
        {
            Spawn(id, x, 0);
            if (id == ModelId.SumoPig) ui.Warn(new Vector3(0, 0.9f, 0));
        }
        [System.NonSerialized] public bool debugInvincible;   // AutoShot only

        // ------------------------------------------------------------------ spawning

        void UpdateSpawning(float dt)
        {
            nextPatternIn -= speed * dt;
            if (nextPatternIn > 0) return;
            if (lap == 0 && !celebrated && distance > CelebrateAt - 75f) return;   // keep the road clear for the finale
            if (lap == 0 && !proposed && distance > ProposeAt - 75f && distance < ProposeAt + 5f) return;   // and for the proposal
            if (lap == 0 && !engaged && distance > EngageAt - 75f && distance < EngageAt + 5f) return;     // and for the engagement
            var diff = Stages.DifficultyAt(distance);
            float length = SpawnPattern(Stages.List[stage], diff);
            float safeGap = speed * 0.6f + 2.5f;   // time to land / stand up and react again
            nextPatternIn = length + safeGap + Random.Range(diff.gapExtraMin, diff.gapExtraMax);
        }

        Hazard Spawn(ModelId id, float x, float y, float bob = 0)
        {
            var def = ItemDef.Get(id);
            var go = ModelFactory.Spawn(id, hazardRoot);
            var h = new Hazard { def = def, t = go.transform, x = x, y = y, bobAmp = bob, bobPhase = Random.value * 6f, width = def.size.x, vx = def.charge };
            go.transform.position = new Vector3(x, y, 0);
            if (def.kind == ItemKind.Obstacle && !def.overhead) go.transform.rotation = Quaternion.Euler(0, Random.Range(-12f, 12f), 0);
            if (def.charge > 0) go.transform.rotation = Quaternion.Euler(0, -28f, 0);   // faces the player while charging
            hazards.Add(h);
            return h;
        }

        /// <summary>Spawns one pattern at the right edge and returns its length in metres.</summary>
        float SpawnPattern(StageDef s, Difficulty df)
        {
            float x0 = SpawnX;
            float total = df.wObstacle + df.wGood + df.wBad;
            float r = Random.value * total;

            if (r < df.wObstacle)
            {
                // ---- Nagoya: the miso-katsu pig charges at you (jump!)
                if (s.theme == 3 && Random.value < 0.24f && ChargerFits())
                {
                    float px = SpawnX + 12f;
                    Spawn(ModelId.SumoPig, px, 0);
                    ui.Warn(new Vector3(0, 0.9f, 0));
                    PlaySfx(SfxBank.Beep, 0.6f, 0.7f);
                    return 14f;
                }
                // ---- slide under
                if (Random.value < df.slideChance)
                {
                    var o = ItemDef.Get(s.overhead);
                    Spawn(s.overhead, x0, 0);
                    float len = o.size.x;
                    if (Random.value < 0.4f)
                        for (int i = 0; i < 3; i++) Spawn(ModelId.Heart, x0 - 1.2f + i * 1.2f, 0.55f);   // low hearts: reward the slide
                    if (Random.value < df.mixChance)
                    {
                        // ...then jump right after standing up
                        var id2 = PickObstacle(s, df, false, 1.2f, 1.3f);
                        float x2 = x0 + o.size.x / 2 + speed * 0.45f + 1.8f;
                        Spawn(id2, x2, 0);
                        len = x2 - x0 + ItemDef.Get(id2).size.x;
                    }
                    return len;
                }

                // ---- jump over
                var id = PickObstacle(s, df, true);
                var d = ItemDef.Get(id);
                Spawn(id, x0, 0);
                float length = d.size.x;
                if (!d.tall && d.size.x <= 1.2f && Random.value < df.doubleChance)
                {
                    var id2 = PickObstacle(s, df, false, 1.2f, 1.3f);
                    var d2 = ItemDef.Get(id2);
                    float x2 = x0 + d.size.x / 2 + 1.3f + d2.size.x / 2;
                    Spawn(id2, x2, 0);
                    length = x2 - x0 + d2.size.x;
                }
                else if (!d.tall && Random.value < df.mixChance)
                {
                    // land, then immediately slide
                    float x2 = x0 + d.size.x / 2 + speed * 0.62f + 1.2f;
                    Spawn(s.overhead, x2, 0);
                    length = x2 - x0 + ItemDef.Get(s.overhead).size.x;
                }
                else if (Random.value < df.rewardArcChance)
                {
                    float top = d.size.y + 1.4f;
                    Spawn(ModelId.Heart, x0 - 1.7f, top - 0.5f);
                    Spawn(Random.value < 0.35f ? s.RandomFood : ModelId.Heart, x0, top);
                    Spawn(ModelId.Heart, x0 + 1.7f, top - 0.5f);
                }
                return length;
            }
            r -= df.wObstacle;

            if (r < df.wGood)
            {
                float roll = Random.value;
                if (roll < 0.10f && distance > 150 && !runner.HasShield)
                {
                    Spawn(ModelId.Shield, x0, 2.8f, 0.25f);
                    return 1;
                }
                if (roll < 0.16f)
                {
                    Spawn(ModelId.RedPacket, x0, Random.value < 0.5f ? 0.95f : 2.8f, 0.15f);
                    return 1;
                }
                if (roll < 0.29f)
                {
                    // Ring high above — needs a long press or double jump.
                    Spawn(ModelId.Heart, x0 - 3f, 1.0f);
                    Spawn(ModelId.Heart, x0 - 1.5f, 2.6f);
                    Spawn(ModelId.Ring, x0, 4.9f, 0.15f);
                    return 3;
                }
                if (roll < 0.55f)
                {
                    Spawn(s.RandomFood, x0, 2.8f, 0.2f);
                    return 1;
                }
                int n = Random.Range(3, 6);
                for (int i = 0; i < n; i++)
                    Spawn(i == n / 2 && Random.value < 0.5f ? s.RandomFood : ModelId.Heart, x0 + i * 1.5f, 0.95f);
                return (n - 1) * 1.5f + 1;
            }

            // bad things
            if (Random.value < 0.55f)
            {
                Spawn(Random.value < 0.5f ? ModelId.AlarmClock : ModelId.Briefcase, x0, 0.55f);
                return 1;
            }
            // rain cloud overhead with hearts underneath: stay on the ground!
            Spawn(ModelId.RainCloud, x0 + 1.5f, 3.1f, df.movingBad ? 0.45f : 0.12f);
            for (int i = 0; i < 3; i++) Spawn(ModelId.Heart, x0 + i * 1.5f, 0.95f);
            return 4;
        }

        /// <summary>A charger must not reach the player too soon after the previous obstacle.</summary>
        bool ChargerFits()
        {
            float arrive = (SpawnX + 12f - CoupleRunner.GroomX) / (speed + ItemDef.Get(ModelId.SumoPig).charge);
            foreach (var h in hazards)
            {
                if (h.done || h.def.kind != ItemKind.Obstacle || h.x < CoupleRunner.GroomX) continue;
                float t = (h.x - CoupleRunner.GroomX) / (speed + h.vx);
                if (Mathf.Abs(arrive - t) < 1.0f) return false;
            }
            return true;
        }

        ModelId PickObstacle(StageDef s, Difficulty df, bool allowTall, float maxWidth = 99f, float maxHeight = 99f)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var id = s.obstacles[Random.Range(0, s.obstacles.Length)];
                var d = ItemDef.Get(id);
                if (d.tall && (!allowTall || Random.value > df.tallChance)) continue;
                if (d.size.x > maxWidth || d.size.y > maxHeight) continue;
                return id;
            }
            return ModelId.Cone;
        }

        // ------------------------------------------------------------------ hazards & collisions

        void UpdateHazards(float dt)
        {
            Rect player = runner.Hitbox;
            bool canCollide = state == State.Playing;
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                var h = hazards[i];
                bool charging = h.vx > 0 && !h.done && state == State.Playing;
                h.x -= (speed + (charging ? h.vx : 0f)) * dt;
                float bob = h.bobAmp > 0 ? Mathf.Sin(Time.time * 2.6f + h.bobPhase) * h.bobAmp : 0;
                if (charging) bob = Mathf.Abs(Mathf.Sin(Time.time * 14f + h.bobPhase)) * 0.22f;   // stomping run
                if (charging && Random.value < dt * 25f && h.x < SpawnX)
                    env.Burst(new Vector3(h.x + 0.6f, 0.1f, 0), new Color(0.9f, 0.85f, 0.8f, 0.8f), 2, 0.4f);
                if (h.t)
                {
                    h.t.position = new Vector3(h.x, h.y + bob, 0);
                    if (h.def.kind != ItemKind.Obstacle && !h.done)
                        h.t.rotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 2f + h.bobPhase) * 35f, 0);
                }

                if (!h.done && canCollide && Hits(player, h, bob)) OnHit(h);

                if (h.x < DespawnX || (h.done && h.t == null))
                {
                    if (h.t) Destroy(h.t.gameObject);
                    hazards.RemoveAt(i);
                }
            }
        }

        static bool Hits(Rect p, Hazard h, float bob)
        {
            if (h.def.kind == ItemKind.Obstacle && h.def.overhead)
            {
                float w = h.def.size.x * 0.85f;
                return new Rect(h.x - w / 2, ItemDef.OverheadClearance, w, 100f).Overlaps(p);
            }
            if (h.def.kind == ItemKind.Obstacle)
            {
                float w = h.width * 0.8f, ht = h.def.size.y * 0.88f;
                return new Rect(h.x - w / 2, 0, w, ht).Overlaps(p);
            }
            float r = h.def.radius * (h.def.kind == ItemKind.Bad ? 0.85f : 1.1f);
            Vector2 c = new Vector2(h.x, h.y + bob);
            float cx = Mathf.Clamp(c.x, p.xMin, p.xMax), cy = Mathf.Clamp(c.y, p.yMin, p.yMax);
            return (new Vector2(cx, cy) - c).sqrMagnitude < r * r;
        }

        void OnHit(Hazard h)
        {
            var d = h.def;
            Vector3 pos = new Vector3(h.x, h.y + 0.6f, 0);
            switch (d.kind)
            {
                case ItemKind.Obstacle:
                    if (invulnerable > 0) return;
                    if (runner.HasShield || debugInvincible)
                    {
                        h.done = true;
                        if (!debugInvincible) runner.SetShield(false);
                        ui.SetShield(false);
                        invulnerable = 0.6f;
                        shake = 0.3f;
                        float hy = d.overhead ? 1.7f : d.size.y * 0.5f;
                        env.Burst(new Vector3(h.x, hy, 0), Palette.Gold, 30, 1.3f);
                        ui.Popup(new Vector3(h.x, hy + 1.2f, 0), Loc.T("護盾擋住了！", "Shield saved you!", "シールドで守った！"), Palette.GoldDark, 48);
                        ui.Flash(Palette.Gold, 0.3f);
                        PlaySfx(SfxBank.ShieldBreak);
                        Destroy(h.t.gameObject);
                        return;
                    }
                    h.done = true;
                    Die(d);
                    return;

                case ItemKind.Good:
                    h.done = true;
                    combo++;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    comboTimer = 3f;
                    int pts = Mathf.RoundToInt(d.points * Multiplier);
                    itemScore += pts; plus += pts;
                    ui.Popup(pos, $"+{pts}  {d.Popup}", d.points >= 50 ? Palette.GoldDark : Palette.Good, d.points >= 50 ? 52 : 42);
                    ui.SetCombo(combo, Multiplier);
                    ui.PunchScore();
                    env.Burst(new Vector3(h.x, h.y, 0), d.id == ModelId.Heart ? Palette.Rose : Palette.Gold, d.points >= 50 ? 28 : 14);
                    PlaySfx(d.points >= 50 ? SfxBank.Chime : SfxBank.Coin, 0.7f, 1f + Mathf.Min(combo, 12) * 0.03f);
                    Destroy(h.t.gameObject);
                    break;

                case ItemKind.Shield:
                    h.done = true;
                    runner.SetShield(true);
                    ui.SetShield(true);
                    ui.Popup(pos, d.Popup, Palette.GoldDark, 50);
                    env.Burst(new Vector3(h.x, h.y, 0), Palette.Gold, 26);
                    PlaySfx(SfxBank.Shield, 0.8f);
                    Destroy(h.t.gameObject);
                    break;

                case ItemKind.Bad:
                    h.done = true;
                    itemScore += d.points; minus += d.points;
                    combo = 0; comboTimer = 0;
                    ui.SetCombo(0, 1);
                    ui.Popup(pos, $"{d.points}  {d.Popup}", Palette.Bad, 46);
                    ui.Flash(Palette.Bad, 0.22f, 0.3f);
                    shake = 0.25f;
                    env.Burst(new Vector3(h.x, h.y, 0), new Color(0.5f, 0.5f, 0.6f), 14);
                    PlaySfx(SfxBank.Bad, 0.8f);
                    Destroy(h.t.gameObject);
                    break;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Simple bot used for demo / automated screenshots.</summary>
        void AutoPilot(ref bool pressed, ref bool held, ref bool slide, ref bool slideHeld)
        {
            foreach (var h in hazards)
            {
                if (h.done || h.def.kind != ItemKind.Obstacle) continue;
                float ahead = h.x - h.width / 2 - CoupleRunner.GroomX;
                if (h.x + h.width / 2 < CoupleRunner.GroomX - 0.4f) continue;
                float closing = speed + h.vx;
                if (h.vx > 0)
                {
                    if (ahead < 0 || ahead > closing * 0.3f + 1.2f) continue;
                    if (runner.Grounded) pressed = true;
                    held = true;
                    return;
                }
                if (h.def.overhead)
                {
                    if (ahead > speed * 0.2f + 0.8f) continue;
                    if (!runner.Sliding) slide = true;
                    slideHeld = true;
                    return;
                }
                if (ahead < 0 || ahead > speed * 0.3f + 1.2f) continue;
                if (runner.Grounded) pressed = true;
                held = true;
                return;
            }
        }

        void OnApplicationFocus(bool focus) { if (!focus && state == State.Playing && !autoPilot) Pause(); }

        // ------------------------------------------------------------------ web page bridge (WebGL)

        /// <summary>Called by the wedding web page when the guest enters full screen.</summary>
        [UnityEngine.Scripting.Preserve]   // called from JavaScript via SendMessage
        public void WebStartBGM()
        {
            webAudioAllowed = true;
            PlayMusic(state == State.GameOver ? gameOverMusic : music, state != State.GameOver);
        }

        /// <summary>Called by the wedding web page when the guest leaves full screen: stop music and pause.</summary>
        [UnityEngine.Scripting.Preserve]
        public void WebStopBGM()
        {
            musicSource.Stop();
            if (state == State.Playing) Pause();
        }

        bool webAudioAllowed = !Platform.IsWebGL;

        // debug hooks for AutoShot
        public void DebugJumpTo(float d) { distance = d; }
        /// <summary>AutoShot: lays out items in the air so the models can be checked in one picture.</summary>
        public void DebugLineup(ModelId[] ids, float x0, float y, float step)
        {
            for (int i = 0; i < ids.Length; i++) Spawn(ids[i], x0 + i * step, y);
        }
        public bool IsGameOver => state == State.GameOver;
        public void DebugKill() { if (state == State.Playing) Die(ItemDef.Get(ModelId.LiquorJar)); }
        public GameUI UI => ui;
    }
}
