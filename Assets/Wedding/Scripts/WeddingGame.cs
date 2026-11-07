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
        public string groomName = "新郎";
        public string brideName = "新娘";
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

        enum State { Title, Countdown, Playing, Paused, Dying, GameOver }
        State state = State.Title;

        class Hazard
        {
            public ItemDef def;
            public Transform t;
            public float x, y, bobAmp, bobPhase;
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

        int Score => Mathf.Max(0, Mathf.FloorToInt(distance) + itemScore);
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
            distance = debugStartDistance; itemScore = 0; plus = 0; minus = 0; combo = 0; maxCombo = 0;
            comboTimer = 0; invulnerable = 0; recordAnnounced = false; hitBy = null;
            nextPatternIn = 16f;
            stage = Stages.IndexAt(distance);
            lap = Stages.LapAt(distance);
            env.SetStage(Stages.List[stage].theme, false);
            runner.ResetRunner(lap > 0 || stage >= 1);   // Hong Kong: he runs alone
            if (lap > 0 || Stages.List[stage].theme == 4) runner.SetFormal(true, false);
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
            }

            if (state == State.Playing || state == State.Countdown)
            {
                if (comboTimer > 0 && (comboTimer -= dt) <= 0) { combo = 0; ui.SetCombo(0, 1); }
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
        /// Touch / mouse: swipe up = jump (keep the finger down = higher), swipe down = slide (keep down = keep sliding).
        /// A quick tap also jumps, so guests who just tap the screen can still play.
        /// </summary>
        void ReadInput(out bool pressed, out bool held, out bool slide, out bool slideHeld)
        {
            pressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            held = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            slide = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            slideHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);

            var es = EventSystem.current;
            bool began, down, ended;
            Vector2 pos;
            int id;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                id = t.fingerId; pos = t.position;
                began = t.phase == TouchPhase.Began;
                ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                down = !ended;
            }
            else
            {
                id = -1; pos = Input.mousePosition;
                began = Input.GetMouseButtonDown(0);
                down = Input.GetMouseButton(0);
                ended = Input.GetMouseButtonUp(0);
            }

            if (began)
            {
                bool overUI = es != null && (id >= 0 ? es.IsPointerOverGameObject(id) : es.IsPointerOverGameObject());
                swipe = overUI ? Swipe.None : Swipe.Pending;
                swipeStart = pos;
                swipeTime = 0;
            }
            if (swipe == Swipe.None) return;
            swipeTime += Time.unscaledDeltaTime;

            float threshold = Mathf.Max(30f, Screen.height * 0.045f);
            float dy = pos.y - swipeStart.y;
            if (swipe == Swipe.Pending && down)
            {
                if (dy > threshold) { swipe = Swipe.Up; pressed = true; }
                else if (dy < -threshold) { swipe = Swipe.Down; slide = true; }
            }
            if (down)
            {
                if (swipe == Swipe.Up) held = true;
                if (swipe == Swipe.Down) slideHeld = true;
            }
            if (ended)
            {
                // quick tap without movement = jump
                if (swipe == Swipe.Pending && swipeTime < 0.3f && Mathf.Abs(dy) < threshold) pressed = true;
                swipe = Swipe.None;
            }
        }

        enum Swipe { None, Pending, Up, Down }
        Swipe swipe = Swipe.None;
        Vector2 swipeStart;
        float swipeTime;

        void UpdateStage()
        {
            int s = Stages.IndexAt(distance);
            int l = Stages.LapAt(distance);
            if (s == stage && l == lap) return;
            stage = s; lap = l;
            var def = Stages.List[s];
            env.SetStage(def.theme, false);
            ui.StageBanner(def, lap);
            PlaySfx(SfxBank.Fanfare, 0.6f);
            if (def.brideJoins && !runner.BrideVisible)
            {
                // Taipei: where they met — she joins the run.
                runner.SetBrideVisible(true, true);
                PlaySfx(SfxBank.Join, 0.8f);
                StartCoroutine(JoinHearts());
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

        // ------------------------------------------------------------------ spawning

        void UpdateSpawning(float dt)
        {
            nextPatternIn -= speed * dt;
            if (nextPatternIn > 0) return;
            var diff = Stages.DifficultyAt(distance);
            float length = SpawnPattern(Stages.List[stage], diff);
            float safeGap = speed * 0.6f + 2.5f;   // time to land / stand up and react again
            nextPatternIn = length + safeGap + Random.Range(diff.gapExtraMin, diff.gapExtraMax);
        }

        Hazard Spawn(ModelId id, float x, float y, float bob = 0)
        {
            var def = ItemDef.Get(id);
            var go = ModelFactory.Spawn(id, hazardRoot);
            var h = new Hazard { def = def, t = go.transform, x = x, y = y, bobAmp = bob, bobPhase = Random.value * 6f };
            go.transform.position = new Vector3(x, y, 0);
            if (def.kind == ItemKind.Obstacle && !def.overhead) go.transform.rotation = Quaternion.Euler(0, Random.Range(-12f, 12f), 0);
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
                if (roll < 0.05f && distance > 300 && !runner.HasShield)
                {
                    Spawn(ModelId.Shield, x0, 2.8f, 0.25f);
                    return 1;
                }
                if (roll < 0.12f)
                {
                    Spawn(ModelId.RedPacket, x0, Random.value < 0.5f ? 0.95f : 2.8f, 0.15f);
                    return 1;
                }
                if (roll < 0.26f)
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
                h.x -= speed * dt;
                float bob = h.bobAmp > 0 ? Mathf.Sin(Time.time * 2.6f + h.bobPhase) * h.bobAmp : 0;
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
                float w = h.def.size.x * 0.8f, ht = h.def.size.y * 0.88f;
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
                    if (runner.HasShield)
                    {
                        h.done = true;
                        runner.SetShield(false);
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
                float ahead = h.x - h.def.size.x / 2 - CoupleRunner.GroomX;
                if (h.x + h.def.size.x / 2 < CoupleRunner.GroomX - 0.4f) continue;
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
        public void WebStartBGM()
        {
            webAudioAllowed = true;
            PlayMusic(state == State.GameOver ? gameOverMusic : music, state != State.GameOver);
        }

        /// <summary>Called by the wedding web page when the guest leaves full screen: stop music and pause.</summary>
        public void WebStopBGM()
        {
            musicSource.Stop();
            if (state == State.Playing) Pause();
        }

        bool webAudioAllowed = !Platform.IsWebGL;

        // debug hooks for AutoShot
        public void DebugJumpTo(float d) { distance = d; }
        public bool IsGameOver => state == State.GameOver;
        public void DebugKill() { if (state == State.Playing) Die(ItemDef.Get(ModelId.LiquorJar)); }
        public GameUI UI => ui;
    }
}
