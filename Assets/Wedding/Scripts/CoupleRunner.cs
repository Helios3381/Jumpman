using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeddingRun
{
    /// <summary>
    /// The groom (player) and — from Taipei on — the bride, who follows exactly in his footsteps.
    /// Custom arcade physics: variable jump height (hold), double jump, slide, fast-fall,
    /// coyote time and input buffering.
    /// </summary>
    public class CoupleRunner : MonoBehaviour
    {
        // ---- tuning ----
        public const float Gravity = 58f;
        public const float JumpVelocity = 16.5f;
        public const float DoubleJumpVelocity = 15f;
        public const float HoldGravityScale = 0.5f;   // lighter gravity while the button is held on the way up
        public const float MaxHoldTime = 0.22f;
        public const float FallMultiplier = 1.45f;    // snappier fall
        public const float FastFallVelocity = -26f;   // slide pressed in the air
        public const float CoyoteTime = 0.09f;
        public const float JumpBuffer = 0.14f;
        public const float SlideMin = 0.45f, SlideMax = 1.1f;

        public const float GroomX = -5f;
        public const float BrideGap = 1.2f;
        public const float BrideZ = 0.8f;
        public const float HalfWidth = 0.32f;
        public const float Height = 1.7f;
        public const float SlideHeight = 0.8f;

        /// <summary>Dead = bumped into something: the groom sits down dizzy, the bride comes over (no "death").</summary>
        /// <summary>Celebrate = wedding finale: they stop, hug and kiss.</summary>
        public enum State { Idle, Run, Dead, Celebrate, Propose, Engage }
        public State state = State.Idle;

        public float Y { get; private set; }
        public float VY { get; private set; }
        public bool Grounded { get; private set; } = true;
        public bool Sliding { get; private set; }
        public bool HasShield { get; private set; }
        public bool BrideVisible { get; private set; } = true;

        public event Action Jumped, DoubleJumped, Landed, SlideStarted, Kissed, ProposalAccepted, TeaServed;

        float proposeTimer;
        bool accepted;
        Transform ringBox, ringGem;
        Transform[] ringGlints;

        /// <summary>Proposal at the Port of Nagoya: he kneels with a ring, she says yes, they hug.</summary>
        public void BeginPropose()
        {
            state = State.Propose;
            proposeTimer = 0; accepted = false;
            Sliding = false; slideQueued = false; buffer = 0;
            Y = 0; VY = 0; Grounded = true;
        }

        float engageTimer;
        bool served;
        Transform teaTray, teaCupOnTray, teaCup;

        /// <summary>文定: she serves him sweet tea with both hands, he bows and drinks, everyone cheers.</summary>
        public void BeginEngage()
        {
            state = State.Engage;
            engageTimer = 0; served = false;
            Sliding = false; slideQueued = false; buffer = 0;
            Y = 0; VY = 0; Grounded = true;
        }

        public void EndEngage()
        {
            state = State.Run;
            teaTray.gameObject.SetActive(false);
            teaCup.gameObject.SetActive(false);
        }

        public void EndPropose()
        {
            state = State.Run;
            ringBox.gameObject.SetActive(false);
            loveHeart.gameObject.SetActive(false);
        }

        float celebrateTimer, sinceJump = 9f, brideZ = BrideZ;
        bool kissed;

        /// <summary>Wedding finale: the bride walks up, they face each other, hug and kiss.</summary>
        public void BeginCelebrate()
        {
            state = State.Celebrate;
            celebrateTimer = 0; kissed = false;
            Sliding = false; slideQueued = false; buffer = 0;
            Y = 0; VY = 0; Grounded = true;
        }

        public void EndCelebrate()
        {
            state = State.Run;
            loveHeart.gameObject.SetActive(false);
            kissHeart.gameObject.SetActive(false);
        }

        ModelFactory.Rig groom, bride;
        ModelFactory.Rig[] groomRigs, brideRigs;   // indexed by ModelFactory.Outfit: casual, wedding attire, engagement
        Transform groomHolder, brideHolder;
        public bool Formal { get; private set; }
        public ModelFactory.Outfit CurrentOutfit { get; private set; }

        /// <summary>Casual clothes for the journey; wedding attire from the wedding venue on.</summary>
        public void SetFormal(bool formal, bool animate) => SetOutfit(formal ? ModelFactory.Outfit.Wedding : ModelFactory.Outfit.Casual, animate);

        public void SetOutfit(ModelFactory.Outfit outfit, bool animate)
        {
            CurrentOutfit = outfit;
            Formal = outfit == ModelFactory.Outfit.Wedding;
            int i = (int)outfit;
            for (int k = 0; k < groomRigs.Length; k++)
            {
                groomRigs[k].root.gameObject.SetActive(k == i);
                brideRigs[k].root.gameObject.SetActive(k == i);
            }
            groom = groomRigs[i];
            bride = brideRigs[i];
            if (animate) changeSpin = 1f;
        }

        float changeSpin;
        Transform shieldFx, groundRing, dizzy, loveHeart, kissHeart;
        readonly Transform[] orbiters = new Transform[3];
        float shieldAppear;
        int airJumps = 1;
        float coyote, buffer, holdTime, phase, flipTimer, deadTimer, landSquash, brideLandSquash, slideTimer, brideScale = 1;
        bool holdAllowed, slideQueued;
        float travelled;   // used for the bride's trail
        readonly List<Vector3> trail = new List<Vector3>(); // (distance, y, sliding)
        float brideY, brideYPrev, brideX;
        bool brideSliding;

        public void Build()
        {
            groomHolder = ModelFactory.Node(transform, "GroomHolder", Vector3.zero);
            brideHolder = ModelFactory.Node(transform, "BrideHolder", Vector3.zero);
            groomRigs = new[] { ModelFactory.BuildGroom(groomHolder, false), ModelFactory.BuildGroom(groomHolder, true), ModelFactory.BuildGroom(groomHolder, true, true) };
            brideRigs = new[] { ModelFactory.BuildBride(brideHolder, false), ModelFactory.BuildBride(brideHolder, true), ModelFactory.BuildBride(brideHolder, false, true) };
            SetFormal(false, false);
            groomHolder.localPosition = new Vector3(GroomX, 0, 0);
            brideHolder.localPosition = new Vector3(GroomX - BrideGap, 0, BrideZ);

            // Shield active: three little golden shields orbit him + a soft golden ring under his feet
            shieldFx = ModelFactory.Node(groomHolder, "ShieldFx", Vector3.zero);
            groundRing = ModelFactory.Node(shieldFx, "Ring", new Vector3(0, 0.04f, 0), new Vector3(90, 0, 0));
            ModelFactory.Part(groundRing, MeshKit.Torus(0.72f, 0.035f, 40, 5), new Color(1f, 0.86f, 0.42f), Vector3.zero, Vector3.one, default, true);
            ModelFactory.Part(groundRing, MeshKit.Torus(0.6f, 0.012f, 40, 4), new Color(1f, 0.95f, 0.75f), Vector3.zero, Vector3.one, default, true);
            for (int i = 0; i < orbiters.Length; i++)
            {
                var o = ModelFactory.Node(shieldFx, "Orbiter" + i, Vector3.zero);
                ModelFactory.Part(o, MeshKit.ShieldShape(0.08f), Palette.Gold, Vector3.zero, Vector3.one * 0.32f, default, false, 0.85f);
                ModelFactory.Part(o, MeshKit.ShieldShape(0.05f), new Color(0.84f, 0.15f, 0.24f), new Vector3(0, 0.01f, -0.03f), Vector3.one * 0.23f, default, false, 0.5f);
                ModelFactory.Part(o, MeshKit.Diamond(), new Color(1f, 0.96f, 0.75f), new Vector3(0, 0.02f, -0.06f), new Vector3(0.07f, 0.09f, 0.03f), default, true);
                orbiters[i] = o;
            }
            shieldFx.gameObject.SetActive(false);

            // little spinning stars above the head after a bump
            dizzy = ModelFactory.Node(groomHolder, "Dizzy", new Vector3(0, 1.75f, 0));
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2 / 5;
                ModelFactory.Part(dizzy, MeshKit.Diamond(), Palette.Gold, new Vector3(Mathf.Cos(a) * 0.45f, 0, Mathf.Sin(a) * 0.45f), Vector3.one * 0.18f, default, true);
            }
            dizzy.gameObject.SetActive(false);

            ringBox = ModelFactory.Node(groomHolder, "RingBox", Vector3.zero);
            ModelFactory.Box(ringBox, new Color(0.55f, 0.08f, 0.2f), new Vector3(0, 0, 0), new Vector3(0.24f, 0.16f, 0.22f));
            ModelFactory.Box(ringBox, new Color(0.55f, 0.08f, 0.2f), new Vector3(0.02f, 0.17f, 0.1f), new Vector3(0.24f, 0.04f, 0.22f), new Vector3(-70, 0, 0));
            ModelFactory.Box(ringBox, Color.white, new Vector3(0, 0.085f, 0), new Vector3(0.2f, 0.02f, 0.18f));
            ModelFactory.Part(ringBox, MeshKit.Torus(0.085f, 0.02f, 18, 6), Palette.Gold, new Vector3(0, 0.16f, 0), Vector3.one, default, false, 0.9f);
            ModelFactory.Box(ringBox, Palette.Gold, new Vector3(0, 0.255f, 0), new Vector3(0.06f, 0.04f, 0.06f));   // the setting
            // a big diamond that catches the light: bright facets and twinkling star glints
            ringGem = ModelFactory.Node(ringBox, "Diamond", new Vector3(0, 0.35f, 0));
            ModelFactory.Part(ringGem, MeshKit.Diamond(), new Color(0.82f, 0.97f, 1f), Vector3.zero, Vector3.one * 0.23f, default, true);
            ModelFactory.Part(ringGem, MeshKit.Diamond(), Color.white, new Vector3(-0.03f, 0.03f, -0.07f), Vector3.one * 0.1f, default, true);
            Vector3[] glintAt = { new Vector3(-0.19f, 0.52f, -0.1f), new Vector3(0.17f, 0.47f, -0.1f), new Vector3(0.03f, 0.62f, -0.1f), new Vector3(-0.13f, 0.27f, -0.1f), new Vector3(0.2f, 0.3f, -0.1f), new Vector3(-0.24f, 0.4f, -0.1f) };
            ringGlints = new Transform[glintAt.Length];
            for (int i = 0; i < glintAt.Length; i++)
            {
                var g = ModelFactory.Node(ringBox, "Glint", glintAt[i]);
                ModelFactory.Box(g, Color.white, Vector3.zero, new Vector3(0.26f, 0.026f, 0.01f), default, true);
                ModelFactory.Box(g, Color.white, Vector3.zero, new Vector3(0.026f, 0.26f, 0.01f), default, true);
                ModelFactory.Box(g, new Color(1f, 0.95f, 0.7f), Vector3.zero, new Vector3(0.09f, 0.016f, 0.01f), new Vector3(0, 0, 45), true);
                ModelFactory.Box(g, new Color(1f, 0.95f, 0.7f), Vector3.zero, new Vector3(0.016f, 0.09f, 0.01f), new Vector3(0, 0, 45), true);
                ringGlints[i] = g;
            }
            ringBox.gameObject.SetActive(false);
            // 文定: her red tea tray with two cups, and the cup he drinks from
            teaTray = ModelFactory.Node(brideHolder, "TeaTray", Vector3.zero);
            ModelFactory.Cyl(teaTray, new Color(0.72f, 0.1f, 0.15f), Vector3.zero, 0.2f, 0.03f);
            ModelFactory.Part(teaTray, MeshKit.Torus(0.2f, 0.014f, 20, 4), Palette.Gold, new Vector3(0, 0.015f, 0), Vector3.one, new Vector3(90, 0, 0));
            teaCupOnTray = ModelFactory.Node(teaTray, "Cup", new Vector3(0.07f, 0.015f, -0.05f));
            ModelFactory.TeaCup(teaCupOnTray);
            ModelFactory.TeaCup(ModelFactory.Node(teaTray, "Cup2", new Vector3(-0.08f, 0.015f, 0.04f)));
            teaTray.gameObject.SetActive(false);
            teaCup = ModelFactory.Node(transform, "TeaCup", Vector3.zero);
            teaCup.localScale = Vector3.one * 1.3f;
            ModelFactory.TeaCup(teaCup);
            teaCup.gameObject.SetActive(false);
            kissHeart = ModelFactory.Part(transform, MeshKit.Heart(0.2f), new Color(1f, 0.3f, 0.45f), Vector3.zero, Vector3.one * 0.25f, default, true);
            kissHeart.gameObject.SetActive(false);
            loveHeart = ModelFactory.Part(transform, MeshKit.Heart(0.25f), new Color(1f, 0.36f, 0.52f), Vector3.zero, Vector3.one * 0.6f, default, true);
            loveHeart.gameObject.SetActive(false);
            kissHeart.gameObject.SetActive(false);
        }

        public void ResetRunner(bool brideVisible)
        {
            Y = 0; VY = 0; Grounded = true; airJumps = 1; coyote = 0; buffer = 0; holdTime = 0;
            flipTimer = 0; deadTimer = 0; travelled = 0; trail.Clear(); brideY = 0;
            celebrateTimer = 0; sinceJump = 9f; brideZ = BrideZ; kissed = false;
            proposeTimer = 0; accepted = false;
            engageTimer = 0; served = false;
            if (ringBox) ringBox.gameObject.SetActive(false);
            if (teaTray) teaTray.gameObject.SetActive(false);
            if (teaCup) teaCup.gameObject.SetActive(false);
            foreach (var rr in groomRigs) rr.body.parent.localRotation = Quaternion.Euler(0, 28, 0);
            foreach (var rr in brideRigs) rr.body.parent.localRotation = Quaternion.Euler(0, 28, 0);
            Sliding = false; slideQueued = false; brideSliding = false;
            SetShield(false);
            SetBrideVisible(brideVisible, false);
            state = State.Idle;
            SetFormal(false, false);
            changeSpin = 0;
            groomHolder.localRotation = Quaternion.identity;
            brideHolder.localRotation = Quaternion.identity;
            var allRigs = new List<ModelFactory.Rig>(groomRigs);
            allRigs.AddRange(brideRigs);
            foreach (var r in allRigs)
            {
                r.body.localPosition = Vector3.zero;
                r.body.localRotation = Quaternion.identity;
                r.body.localScale = Vector3.one;
                if (r.head) r.head.localRotation = Quaternion.identity;
            }
            dizzy.gameObject.SetActive(false);
            loveHeart.gameObject.SetActive(false);
            brideX = GroomX - BrideGap;
            groomHolder.localPosition = new Vector3(GroomX, 0, 0);
            brideHolder.localPosition = new Vector3(brideX, 0, BrideZ);
        }

        /// <summary>In Hong Kong he runs alone; she joins in Taipei (where they met).</summary>
        public void SetBrideVisible(bool on, bool animate)
        {
            BrideVisible = on;
            brideHolder.gameObject.SetActive(on);
            brideScale = on && animate ? 0.01f : 1f;
            brideHolder.localScale = Vector3.one * brideScale;
        }

        public Vector3 BridePosition => brideHolder.position;

        public void SetShield(bool on)
        {
            HasShield = on;
            if (shieldFx) shieldFx.gameObject.SetActive(on);
            shieldAppear = 0;
        }

        public void Die()
        {
            state = State.Dead;
            deadTimer = 0;
            Sliding = false;
        }

        public Rect Hitbox => new Rect(GroomX - HalfWidth, Y + 0.05f, HalfWidth * 2, Sliding ? SlideHeight : Height);

        /// <summary>Called by the game every frame.</summary>
        public void Tick(float dt, float speed, bool jumpPressed, bool jumpHeld, bool slidePressed, bool slideHeld)
        {
            if (state == State.Run)
            {
                if (jumpPressed) buffer = JumpBuffer; else buffer -= dt;
                sinceJump += dt;
                coyote = Grounded ? CoyoteTime : coyote - dt;
                if (!jumpHeld) holdAllowed = false;

                // ---- slide / fast fall
                if (slidePressed)
                {
                    if (Grounded && !Sliding) StartSlide();
                    else if (!Grounded)
                    {
                        // a down-swipe right after a tap turns the little hop into a slide almost instantly
                        VY = Mathf.Min(VY, sinceJump < 0.25f ? -45f : FastFallVelocity);
                        holdAllowed = false; slideQueued = true;
                    }
                }
                if (Sliding)
                {
                    slideTimer += dt;
                    if ((!slideHeld && slideTimer >= SlideMin) || slideTimer >= SlideMax) Sliding = false;
                }

                // ---- jump (also cancels a slide)
                if (buffer > 0)
                {
                    if (Grounded || coyote > 0)
                    {
                        Sliding = false; slideQueued = false;
                        VY = JumpVelocity; Grounded = false; coyote = 0; buffer = 0; holdTime = 0; holdAllowed = true;
                        sinceJump = 0;
                        Jumped?.Invoke();
                    }
                    else if (airJumps > 0)
                    {
                        slideQueued = false;
                        VY = DoubleJumpVelocity; airJumps--; buffer = 0; holdTime = 0; holdAllowed = true; flipTimer = 0.4f;
                        DoubleJumped?.Invoke();
                    }
                }

                if (!Grounded)
                {
                    float g = Gravity;
                    if (VY > 0 && holdAllowed && holdTime < MaxHoldTime) { g *= HoldGravityScale; holdTime += dt; }
                    else if (VY < 0) g *= FallMultiplier;
                    VY -= g * dt;
                    Y += VY * dt;
                    if (Y <= 0)
                    {
                        Y = 0; VY = 0; Grounded = true; airJumps = 1; landSquash = 1;
                        Landed?.Invoke();
                        if (slideQueued || slideHeld) StartSlide();
                        slideQueued = false;
                    }
                }
                travelled += speed * dt;
            }
            else if (state == State.Idle)
            {
                travelled += speed * dt;
            }
            else if (state == State.Celebrate)
            {
                celebrateTimer += dt;
                if (!kissed && celebrateTimer > 1.5f) { kissed = true; Kissed?.Invoke(); }
            }
            else if (state == State.Propose)
            {
                proposeTimer += dt;
                if (!accepted && proposeTimer > 2.3f) { accepted = true; ProposalAccepted?.Invoke(); }
            }
            else if (state == State.Engage)
            {
                engageTimer += dt;
                if (!served && engageTimer > 2.2f) { served = true; TeaServed?.Invoke(); }
            }

            // Bride replays the groom's height (and slide) at the point where she now stands.
            trail.Add(new Vector3(travelled, Y, Sliding ? 1 : 0));
            float target = travelled - BrideGap;
            while (trail.Count > 2 && trail[1].x <= target) trail.RemoveAt(0);
            brideYPrev = brideY;
            if (state != State.Dead && state != State.Celebrate && state != State.Propose && state != State.Engage && trail.Count >= 2 && trail[0].x <= target)
            {
                var a = trail[0]; var b = trail[1];
                float t = Mathf.Approximately(b.x, a.x) ? 1 : Mathf.Clamp01((target - a.x) / (b.x - a.x));
                brideY = Mathf.Lerp(a.y, b.y, t);
                brideSliding = (t < 0.5f ? a.z : b.z) > 0.5f;
            }
            else
            {
                brideY = Mathf.MoveTowards(brideY, 0, dt * 10);
                brideSliding = false;
            }
            if (brideYPrev > 0.01f && brideY <= 0.01f) brideLandSquash = 1;

            if (state == State.Dead) deadTimer += dt;
            phase += dt * Mathf.Max(speed, 4f) * 1.15f;
            Animate(groom, Y, Grounded, Sliding, dt, 0f);
            if (BrideVisible) Animate(bride, brideY, brideY <= 0.01f, brideSliding, dt, 0.6f);

            groomHolder.localPosition = new Vector3(GroomX, Y, 0);
            // after a bump she walks up next to him; at the wedding finale she walks right up to him
            bool together = state == State.Celebrate || state == State.Propose || state == State.Engage;
            float bx = state == State.Dead ? GroomX - 0.75f
                     : state == State.Celebrate ? GroomX - 0.66f
                     : state == State.Engage ? GroomX - 1.4f
                     : state == State.Propose ? (proposeTimer > 2.9f ? GroomX - 0.66f : GroomX - 1.05f)
                     : GroomX - BrideGap;
            brideX = Mathf.MoveTowards(brideX, bx, dt * (together ? 1.6f : 2.5f));
            brideZ = Mathf.MoveTowards(brideZ, together ? -0.22f : BrideZ, dt * 1.5f);
            float tiptoe = state == State.Celebrate && celebrateTimer > 1.3f ? Mathf.Min(0.07f, (celebrateTimer - 1.3f) * 0.3f) : 0f;
            if (state == State.Propose && proposeTimer > 2.2f && proposeTimer < 2.9f)
                tiptoe = Mathf.Abs(Mathf.Sin((proposeTimer - 2.2f) * 13f)) * 0.22f;        // she hops: "yes!"
            if (state == State.Propose)
            {
                // ring box held out towards her while he kneels
                bool showRing = proposeTimer > 0.8f && proposeTimer < 3.0f;
                if (ringBox.gameObject.activeSelf != showRing) ringBox.gameObject.SetActive(showRing);
                float pop = Mathf.Clamp01((proposeTimer - 0.8f) * 4f);
                ringBox.localPosition = new Vector3(-0.62f, 0.99f, -0.18f);
                ringBox.localRotation = Quaternion.Euler(0, -20, 0);
                ringBox.localScale = Vector3.one * UIKit.EaseOutBack(pop) * 1.4f;
                if (showRing)
                {
                    float tw = Time.time;
                    ringGem.localRotation = Quaternion.Euler(0, tw * 70f, 0);
                    for (int i = 0; i < ringGlints.Length; i++)
                    {
                        float k = Mathf.Max(0f, Mathf.Sin(tw * 6.5f + i * 1.9f));
                        ringGlints[i].localScale = Vector3.one * k * k * 1.3f;
                        ringGlints[i].localRotation = Quaternion.Euler(0, 0, tw * 80f + i * 25f);
                    }
                }
            }
            if (teaTray)
            {
                // the tray in both her hands, held out to him; he takes one cup and drinks
                bool offering = state == State.Engage && engageTimer > 0.5f && engageTimer < 2.6f;
                if (teaTray.gameObject.activeSelf != offering) teaTray.gameObject.SetActive(offering);
                if (offering)
                {
                    teaTray.localPosition = new Vector3(0.5f, 1.08f, -0.08f);
                    teaTray.localScale = Vector3.one * UIKit.EaseOutBack(Mathf.Clamp01((engageTimer - 0.5f) * 4f)) * 1.3f;
                }
                bool drinking = state == State.Engage && engageTimer > 1.5f && engageTimer < 2.6f;
                teaCupOnTray.gameObject.SetActive(!drinking);
                if (teaCup.gameObject.activeSelf != drinking) teaCup.gameObject.SetActive(drinking);
                if (drinking) teaCup.position = groom.armR.TransformPoint(new Vector3(0, -0.52f, 0));
            }
            brideHolder.localPosition = new Vector3(brideX, brideY + tiptoe, brideZ);
            // the groom turns around to face her
            // (fully side-on during the kiss so both faces read clearly from the camera)
            bool facingEachOther = (state == State.Celebrate && celebrateTimer > 0.15f) || (state == State.Propose && proposeTimer > 0.15f)
                                   || (state == State.Engage && engageTimer > 0.15f);
            var facing = groom.body.parent;
            facing.localRotation = Quaternion.Slerp(facing.localRotation, Quaternion.Euler(0, facingEachOther ? 163f : 28f, 0), dt * 6f);
            var bFacing = bride.body.parent;
            bool brideTurned = facingEachOther && ((state == State.Celebrate && celebrateTimer > 0.9f) || (state == State.Propose && proposeTimer > 0.6f)
                                                   || (state == State.Engage && engageTimer > 0.5f));
            bFacing.localRotation = Quaternion.Slerp(bFacing.localRotation, Quaternion.Euler(0, brideTurned ? -14f : 28f, 0), dt * 6f);
            if (brideScale < 1)
            {
                brideScale = Mathf.Min(1, brideScale + dt * 3f);
                brideHolder.localScale = Vector3.one * UIKit.EaseOutBack(brideScale);
            }

            bool showShield = HasShield && state != State.Propose && state != State.Celebrate && state != State.Engage;
            if (shieldFx && shieldFx.gameObject.activeSelf != showShield)
            {
                shieldFx.gameObject.SetActive(showShield);
                if (showShield) shieldAppear = 0;
            }
            if (shieldFx && showShield)
            {
                shieldAppear = Mathf.Min(1, shieldAppear + dt * 3f);
                float grow = UIKit.EaseOutBack(shieldAppear);
                float baseY = Sliding ? 0.45f : 1.0f;
                for (int i = 0; i < orbiters.Length; i++)
                {
                    float a = Time.time * 2.4f + i * Mathf.PI * 2f / orbiters.Length;
                    var o = orbiters[i];
                    o.localPosition = new Vector3(Mathf.Cos(a) * 0.95f * grow, baseY + Mathf.Sin(Time.time * 3f + i * 2f) * 0.1f, Mathf.Sin(a) * 0.55f * grow);
                    // always face the camera (undo the holder's facing) and wobble a little
                    o.rotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 4f + i) * 12f);
                    o.localScale = Vector3.one * grow;
                }
                float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.06f;
                groundRing.localScale = new Vector3(pulse, pulse, 1f) * grow;
                groundRing.localPosition = new Vector3(0, 0.04f - Y, 0);   // stays on the road while jumping
            }

            if (changeSpin > 0)
            {
                changeSpin = Mathf.Max(0, changeSpin - dt * 1.6f);
                float k = 1 - changeSpin;
                var q = Quaternion.Euler(0, 720f * UIKit.EaseOutCubic(k), 0);
                groomHolder.localRotation = q;
                brideHolder.localRotation = q;
            }

            UpdateBumpEffects(dt);
            if (state == State.Celebrate && celebrateTimer > 1.5f)
            {
                // a big heart rises above the kiss
                if (!loveHeart.gameObject.activeSelf) loveHeart.gameObject.SetActive(true);
                float k = Mathf.Clamp01((celebrateTimer - 1.5f) / 1.2f);
                loveHeart.localPosition = new Vector3(GroomX - 0.3f, 2.3f + UIKit.EaseOutCubic(k) * 0.9f, -0.2f);
                loveHeart.localScale = Vector3.one * (0.3f + UIKit.EaseOutBack(k) * 0.7f + Mathf.Sin(celebrateTimer * 6f) * 0.04f);
                loveHeart.localRotation = Quaternion.identity;
            }
            if (state == State.Propose && proposeTimer > 2.3f)
            {
                if (!loveHeart.gameObject.activeSelf) loveHeart.gameObject.SetActive(true);
                float k = Mathf.Clamp01((proposeTimer - 2.3f) / 1.0f);
                loveHeart.localPosition = new Vector3(GroomX - 0.5f, 2.2f + UIKit.EaseOutCubic(k) * 1.0f, -0.2f);
                loveHeart.localScale = Vector3.one * (0.3f + UIKit.EaseOutBack(k) * 0.7f + Mathf.Sin(proposeTimer * 6f) * 0.04f);
                loveHeart.localRotation = Quaternion.identity;
            }
            // a little heart right at the kiss
            bool kissing = state == State.Celebrate && celebrateTimer > 1.4f;
            if (kissHeart.gameObject.activeSelf != kissing) kissHeart.gameObject.SetActive(kissing);
            if (kissing)
            {
                kissHeart.localPosition = new Vector3(GroomX - 0.36f, 1.78f + Mathf.Sin(celebrateTimer * 3f) * 0.03f, -0.55f);
                kissHeart.localScale = Vector3.one * (0.22f + Mathf.Abs(Mathf.Sin(celebrateTimer * 5f)) * 0.06f) * Mathf.Clamp01((celebrateTimer - 1.4f) * 4f);
            }
        }

        public void DebugSlide() { if (Grounded) { StartSlide(); slideTimer = -0.6f; } }

        void StartSlide()
        {
            Sliding = true;
            slideTimer = 0;
            SlideStarted?.Invoke();
        }

        void UpdateBumpEffects(float dt)
        {
            bool bumped = state == State.Dead;
            if (dizzy.gameObject.activeSelf != bumped) dizzy.gameObject.SetActive(bumped);
            if (bumped)
            {
                dizzy.localPosition = new Vector3(-0.1f, 1.25f, 0);
                dizzy.Rotate(0, dt * 240f, 0, Space.Self);
                // a small heart floats up between them once she reaches him
                if (BrideVisible && deadTimer > 0.6f)
                {
                    if (!loveHeart.gameObject.activeSelf) loveHeart.gameObject.SetActive(true);
                    float k = Mathf.Repeat(deadTimer - 0.6f, 1.8f) / 1.8f;
                    loveHeart.localPosition = new Vector3(GroomX - 0.45f, 1.7f + k * 1.2f, -0.2f);
                    loveHeart.localScale = Vector3.one * (0.45f + Mathf.Sin(k * Mathf.PI) * 0.25f);
                }
            }
        }

        void Animate(ModelFactory.Rig r, float y, bool grounded, bool sliding, float dt, float phaseOffset)
        {
            float p = phase - phaseOffset;
            float s = Mathf.Sin(p);
            float legSwing, armSwing, bob = 0, lean = 0;
            float armL = 0, armR = 0;
            if (r.kneeL && state != State.Propose)
            {
                r.kneeL.localRotation = Quaternion.Slerp(r.kneeL.localRotation, Quaternion.identity, dt * 12);
                r.kneeR.localRotation = Quaternion.Slerp(r.kneeR.localRotation, Quaternion.identity, dt * 12);
            }

            if (state == State.Dead)
            {
                float k = Mathf.Clamp01(deadTimer / 0.35f);
                float e = 1 - (1 - k) * (1 - k);
                if (!r.isBride)
                {
                    // plop down on the ground, a little dazed
                    float hop = Mathf.Sin(Mathf.Clamp01(deadTimer / 0.3f) * Mathf.PI) * 0.35f;
                    r.body.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, 12f, e));
                    r.body.localPosition = new Vector3(-0.25f * e, Mathf.Lerp(0, -0.5f, e) + hop, 0);
                    r.legL.localRotation = Quaternion.Euler(0, 0, 80 * e);
                    r.legR.localRotation = Quaternion.Euler(0, 0, 70 * e);
                    r.armL.localRotation = Quaternion.Euler(0, 0, -35 * e);
                    r.armR.localRotation = Quaternion.Euler(0, 0, -35 * e);
                    if (r.head) r.head.localRotation = Quaternion.Euler(Mathf.Sin(deadTimer * 5f) * 8f * e, 0, 6f * e);
                }
                else
                {
                    // she bends towards him and offers her hand
                    r.body.localRotation = Quaternion.Slerp(r.body.localRotation, Quaternion.Euler(0, 0, -12f), dt * 6);
                    r.body.localPosition = Vector3.zero;
                    r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, 20), dt * 6);
                    r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, 75), dt * 6);
                    r.legL.localRotation = Quaternion.identity;
                    r.legR.localRotation = Quaternion.identity;
                    if (r.head) r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(0, 0, -10), dt * 6);
                    if (r.skirt) r.skirt.localRotation = Quaternion.identity;
                }
                r.body.localScale = Vector3.one;
                return;
            }

            if (state == State.Propose)
            {
                float t = proposeTimer;
                bool hugging = t > 2.9f;
                if (!r.isBride)
                {
                    // kneel on one knee, hold the ring box out; stand up for the hug
                    float kneel = Mathf.Clamp01((t - 0.5f) / 0.5f) * (1f - Mathf.Clamp01((t - 2.9f) / 0.4f));
                    float e = kneel * kneel * (3 - 2 * kneel);
                    // far leg: thigh forward, shin straight down, foot flat on the ground
                    // near leg: knee on the ground, shin back along the road, toes tucked
                    r.body.localPosition = Vector3.Lerp(r.body.localPosition, new Vector3(0, -0.29f * e, 0), dt * 12);
                    r.body.localRotation = Quaternion.Slerp(r.body.localRotation, Quaternion.Euler(0, 0, hugging ? -6f : 0f), dt * 8);
                    r.legL.localRotation = Quaternion.Euler(0, 0, 80f * e);
                    if (r.kneeL) r.kneeL.localRotation = Quaternion.Euler(0, 0, -80f * e);
                    r.legR.localRotation = Quaternion.Euler(0, 0, -5f * e);
                    if (r.kneeR) r.kneeR.localRotation = Quaternion.Euler(0, 0, -95f * e);
                    r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, hugging ? 80f : 75f * e), dt * 10);
                    r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, hugging ? 95f : 35f * e), dt * 10);
                    if (r.head) r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(0, 0, hugging ? -12f : 8f * e), dt * 8);
                }
                else
                {
                    bool walking = t < 0.6f;
                    float w = Mathf.Sin(Time.time * 9f);
                    r.legL.localRotation = walking ? Quaternion.Euler(0, 0, w * 25f) : Quaternion.Slerp(r.legL.localRotation, Quaternion.identity, dt * 8);
                    r.legR.localRotation = walking ? Quaternion.Euler(0, 0, -w * 25f) : Quaternion.Slerp(r.legR.localRotation, Quaternion.identity, dt * 8);
                    // surprised: hands to her mouth → nods "yes" → hug
                    bool surprised = t > 0.8f && t < 2.2f;
                    float aL = hugging ? 95f : surprised ? 150f : 40f, aR = hugging ? 70f : surprised ? 145f : 10f;
                    r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, aL), dt * 10);
                    r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, aR), dt * 10);
                    r.body.localRotation = Quaternion.Slerp(r.body.localRotation, Quaternion.Euler(0, 0, surprised ? 7f : hugging ? -6f : 0f), dt * 8);
                    r.body.localPosition = Vector3.Lerp(r.body.localPosition, Vector3.zero, dt * 8);
                    float nod = t > 2.2f && t < 2.9f ? Mathf.Sin((t - 2.2f) * 18f) * 12f : 0f;
                    if (r.head) r.head.localRotation = Quaternion.Euler(0, 0, nod + (hugging ? -14f : 0f));
                    if (r.skirt) r.skirt.localRotation = Quaternion.Euler(0, 0, walking ? w * 4f : 0f);
                }
                r.body.localScale = Vector3.one;
                return;
            }

            if (state == State.Engage)
            {
                float t = engageTimer;
                bool served = t > 2.2f, drinking = t > 1.5f && t < 2.6f;
                if (!r.isBride)
                {
                    // he bows to her, then drinks the tea; afterwards he cheers
                    float bow = t > 0.7f && t < 1.45f ? Mathf.Sin((t - 0.7f) / 0.75f * Mathf.PI) : 0f;
                    float cheer = served && !drinking ? Mathf.Sin(Time.time * 8f) * 15f : 0f;
                    r.legL.localRotation = Quaternion.Slerp(r.legL.localRotation, Quaternion.identity, dt * 8);
                    r.legR.localRotation = Quaternion.Slerp(r.legR.localRotation, Quaternion.identity, dt * 8);
                    r.body.localRotation = Quaternion.Slerp(r.body.localRotation, Quaternion.Euler(0, 0, -12f * bow), dt * 10);
                    r.body.localPosition = Vector3.Lerp(r.body.localPosition, Vector3.zero, dt * 8);
                    float aL = drinking ? 60f : served ? 150f + cheer : 0f, aR = drinking ? 120f : served ? 150f - cheer : 0f;
                    r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, aL), dt * 10);
                    r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, aR), dt * 10);
                    if (r.head) r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(0, 0, drinking ? 14f : 0f), dt * 8);
                }
                else
                {
                    bool walking = t < 0.6f;
                    float w = Mathf.Sin(Time.time * 9f);
                    r.legL.localRotation = walking ? Quaternion.Euler(0, 0, w * 25f) : Quaternion.Slerp(r.legL.localRotation, Quaternion.identity, dt * 8);
                    r.legR.localRotation = walking ? Quaternion.Euler(0, 0, -w * 25f) : Quaternion.Slerp(r.legR.localRotation, Quaternion.identity, dt * 8);
                    // both hands hold the tray out to him; once he has drunk she claps
                    bool offering = t > 0.5f && t < 2.6f;
                    float clap = served && !offering ? Mathf.Sin(Time.time * 14f) * 14f : 0f;
                    float a = offering ? 75f : served ? 120f : 20f;
                    r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, a + clap), dt * 10);
                    r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, a - clap), dt * 10);
                    r.body.localRotation = Quaternion.Slerp(r.body.localRotation, Quaternion.Euler(0, 0, offering && t > 0.8f && t < 1.4f ? -10f : 0f), dt * 8);
                    r.body.localPosition = Vector3.Lerp(r.body.localPosition, Vector3.zero, dt * 8);
                    float nod = served && t < 2.9f ? Mathf.Sin((t - 2.2f) * 18f) * 10f : 0f;
                    if (r.head) r.head.localRotation = Quaternion.Euler(0, 0, nod);
                    if (r.skirt) r.skirt.localRotation = Quaternion.Euler(0, 0, walking ? w * 4f : 0f);
                }
                r.body.localScale = Vector3.one;
                return;
            }

            if (state == State.Celebrate)
            {
                float t = celebrateTimer;
                bool walking = r.isBride && t < 1.1f;
                float hug = Mathf.Clamp01((t - 0.9f) / 0.4f);
                float kiss = Mathf.Clamp01((t - 1.3f) / 0.3f);
                if (walking)
                {
                    float w = Mathf.Sin(Time.time * 9f);
                    r.legL.localRotation = Quaternion.Euler(0, 0, w * 25f);
                    r.legR.localRotation = Quaternion.Euler(0, 0, -w * 25f);
                }
                else
                {
                    r.legL.localRotation = Quaternion.Slerp(r.legL.localRotation, Quaternion.identity, dt * 8);
                    r.legR.localRotation = Quaternion.Slerp(r.legR.localRotation, Quaternion.Euler(0, 0, r.isBride ? -18f * kiss : 0), dt * 8);
                }
                // arms wrap around each other, bodies lean in, heads tilt for the kiss
                float armA = Mathf.Lerp(r.isBride ? 40f : 0f, r.isBride ? 95f : 80f, hug);
                float armB = Mathf.Lerp(0f, r.isBride ? 70f : 95f, hug);
                r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, armA), dt * 10);
                r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, armB), dt * 10);
                r.body.localRotation = Quaternion.Slerp(r.body.localRotation, Quaternion.Euler(0, 0, -6f * hug), dt * 8);
                r.body.localPosition = Vector3.Lerp(r.body.localPosition, Vector3.zero, dt * 8);
                r.body.localScale = Vector3.one;
                if (r.head) r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(0, 0, -14f * kiss), dt * 8);
                if (r.skirt) r.skirt.localRotation = Quaternion.Euler(0, 0, walking ? Mathf.Sin(Time.time * 9f) * 4f : 0f);
                return;
            }

            if (state == State.Idle)
            {
                bob = Mathf.Sin(Time.time * 2.2f + phaseOffset) * 0.025f;
                armL = -6 + Mathf.Sin(Time.time * 2.2f) * 4;
                armR = r.isBride ? 8 : 150 + Mathf.Sin(Time.time * 7f) * 22f; // groom waves
                if (r.isBride) armL = 40;
                r.legL.localRotation = Quaternion.identity;
                r.legR.localRotation = Quaternion.identity;
            }
            else if (sliding && grounded)
            {
                // baseball-style slide: lean back, legs forward
                // body leans back 70°, legs stretch forward along the ground (kept above the road so they stay visible)
                float lean2 = r.isBride ? 52f : 70f;
                r.legL.localRotation = Quaternion.Slerp(r.legL.localRotation, Quaternion.Euler(0, 0, 90f - lean2), dt * 25);
                r.legR.localRotation = Quaternion.Slerp(r.legR.localRotation, Quaternion.Euler(0, 0, 76f - lean2), dt * 25);
                armL = r.isBride ? 60 : -50; armR = -75;
                var target = Quaternion.Euler(0, 0, lean2);
                r.body.localRotation = Quaternion.Slerp(r.body.localRotation, target, dt * 25);
                r.body.localPosition = new Vector3(r.isBride ? 0.35f : 0.45f, r.isBride ? 0.02f : 0.04f, 0);
                r.body.localScale = Vector3.one;
                r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, armL), dt * 16);
                r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, armR), dt * 16);
                if (r.skirt) r.skirt.localRotation = Quaternion.Euler(0, 0, -20);
                return;
            }
            else if (grounded)
            {
                legSwing = s * 42f;
                armSwing = -s * 38f;
                bob = Mathf.Abs(Mathf.Cos(p)) * 0.08f;
                lean = -7f;
                r.legL.localRotation = Quaternion.Euler(0, 0, legSwing);
                r.legR.localRotation = Quaternion.Euler(0, 0, -legSwing);
                armL = armSwing; armR = -armSwing;
                if (r.isBride) armL = 45 + s * 8;
            }
            else
            {
                r.legL.localRotation = Quaternion.Slerp(r.legL.localRotation, Quaternion.Euler(0, 0, 40), dt * 18);
                r.legR.localRotation = Quaternion.Slerp(r.legR.localRotation, Quaternion.Euler(0, 0, -25), dt * 18);
                armL = r.isBride ? 70 : 155; armR = 140;
                lean = -4f;
            }

            r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(0, 0, armL), dt * 16);
            r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(0, 0, armR), dt * 16);

            // double-jump flip (groom only) + land squash
            float flip = 0;
            if (!r.isBride && flipTimer > 0)
            {
                flipTimer -= dt;
                flip = -360f * (1 - Mathf.Clamp01(flipTimer / 0.4f));
            }
            ref float squash = ref r.isBride ? ref brideLandSquash : ref landSquash;
            squash = Mathf.MoveTowards(squash, 0, dt * 6f);
            float sq = Mathf.Sin(squash * Mathf.PI) * 0.18f;
            r.body.localScale = new Vector3(1 + sq * 0.6f, 1 - sq, 1 + sq * 0.6f);
            if (Mathf.Abs(flip) > 0.01f)
            {
                // rotate around the body centre rather than the feet
                Vector3 pivot = new Vector3(0, 0.9f, 0);
                var q = Quaternion.Euler(0, 0, flip + lean);
                r.body.localRotation = q;
                r.body.localPosition = pivot - q * pivot + new Vector3(0, bob, 0);
            }
            else
            {
                r.body.localRotation = Quaternion.Slerp(r.body.localRotation, Quaternion.Euler(0, 0, lean), dt * 20);
                r.body.localPosition = Vector3.Lerp(r.body.localPosition, new Vector3(0, bob, 0), dt * 20);
            }
            if (r.head) r.head.localRotation = Quaternion.identity;
            if (r.skirt) r.skirt.localRotation = Quaternion.Euler(0, 0, grounded ? s * 4f : 8f);
        }
    }
}
