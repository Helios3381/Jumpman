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
        public enum State { Idle, Run, Dead }
        public State state = State.Idle;

        public float Y { get; private set; }
        public float VY { get; private set; }
        public bool Grounded { get; private set; } = true;
        public bool Sliding { get; private set; }
        public bool HasShield { get; private set; }
        public bool BrideVisible { get; private set; } = true;

        public event Action Jumped, DoubleJumped, Landed, SlideStarted;

        ModelFactory.Rig groom, bride;
        ModelFactory.Rig[] groomRigs, brideRigs;   // [0] casual, [1] wedding attire
        Transform groomHolder, brideHolder;
        public bool Formal { get; private set; }

        /// <summary>Casual clothes for the journey; wedding attire from the wedding venue on.</summary>
        public void SetFormal(bool formal, bool animate)
        {
            Formal = formal;
            int i = formal ? 1 : 0;
            for (int k = 0; k < 2; k++)
            {
                groomRigs[k].root.gameObject.SetActive(k == i);
                brideRigs[k].root.gameObject.SetActive(k == i);
            }
            groom = groomRigs[i];
            bride = brideRigs[i];
            if (animate) changeSpin = 1f;
        }

        float changeSpin;
        Transform shieldBubble, dizzy, loveHeart;
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
            groomRigs = new[] { ModelFactory.BuildGroom(groomHolder, false), ModelFactory.BuildGroom(groomHolder, true) };
            brideRigs = new[] { ModelFactory.BuildBride(brideHolder, false), ModelFactory.BuildBride(brideHolder, true) };
            SetFormal(false, false);
            groomHolder.localPosition = new Vector3(GroomX, 0, 0);
            brideHolder.localPosition = new Vector3(GroomX - BrideGap, 0, BrideZ);

            shieldBubble = ModelFactory.Ball(groomHolder, new Color(1f, 0.85f, 0.4f, 0.28f), new Vector3(0, 0.95f, 0), new Vector3(2.0f, 2.3f, 2.0f));
            ModelFactory.Part(shieldBubble, MeshKit.Torus(0.5f, 0.02f, 32, 4), new Color(1f, 0.9f, 0.5f), Vector3.zero, Vector3.one, default, true);
            shieldBubble.gameObject.SetActive(false);

            // little spinning stars above the head after a bump
            dizzy = ModelFactory.Node(groomHolder, "Dizzy", new Vector3(0, 1.75f, 0));
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2 / 5;
                ModelFactory.Part(dizzy, MeshKit.Diamond(), Palette.Gold, new Vector3(Mathf.Cos(a) * 0.45f, 0, Mathf.Sin(a) * 0.45f), Vector3.one * 0.18f, default, true);
            }
            dizzy.gameObject.SetActive(false);

            loveHeart = ModelFactory.Part(transform, MeshKit.Heart(0.25f), new Color(1f, 0.36f, 0.52f), Vector3.zero, Vector3.one * 0.6f, default, true);
            loveHeart.gameObject.SetActive(false);
        }

        public void ResetRunner(bool brideVisible)
        {
            Y = 0; VY = 0; Grounded = true; airJumps = 1; coyote = 0; buffer = 0; holdTime = 0;
            flipTimer = 0; deadTimer = 0; travelled = 0; trail.Clear(); brideY = 0;
            Sliding = false; slideQueued = false; brideSliding = false;
            SetShield(false);
            SetBrideVisible(brideVisible, false);
            state = State.Idle;
            SetFormal(false, false);
            changeSpin = 0;
            groomHolder.localRotation = Quaternion.identity;
            brideHolder.localRotation = Quaternion.identity;
            foreach (var r in new[] { groomRigs[0], groomRigs[1], brideRigs[0], brideRigs[1] })
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
            if (shieldBubble) shieldBubble.gameObject.SetActive(on);
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
                coyote = Grounded ? CoyoteTime : coyote - dt;
                if (!jumpHeld) holdAllowed = false;

                // ---- slide / fast fall
                if (slidePressed)
                {
                    if (Grounded && !Sliding) StartSlide();
                    else if (!Grounded) { VY = Mathf.Min(VY, FastFallVelocity); holdAllowed = false; slideQueued = true; }
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

            // Bride replays the groom's height (and slide) at the point where she now stands.
            trail.Add(new Vector3(travelled, Y, Sliding ? 1 : 0));
            float target = travelled - BrideGap;
            while (trail.Count > 2 && trail[1].x <= target) trail.RemoveAt(0);
            brideYPrev = brideY;
            if (state != State.Dead && trail.Count >= 2 && trail[0].x <= target)
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
            // after a bump she walks up next to him
            float bx = state == State.Dead ? GroomX - 0.75f : GroomX - BrideGap;
            brideX = Mathf.MoveTowards(brideX, bx, dt * 2.5f);
            brideHolder.localPosition = new Vector3(brideX, brideY, BrideZ);
            if (brideScale < 1)
            {
                brideScale = Mathf.Min(1, brideScale + dt * 3f);
                brideHolder.localScale = Vector3.one * UIKit.EaseOutBack(brideScale);
            }

            if (shieldBubble && HasShield)
            {
                float s = 1 + Mathf.Sin(Time.time * 6) * 0.04f;
                shieldBubble.localScale = new Vector3(2.0f, Sliding ? 1.3f : 2.3f, 2.0f) * s;
                shieldBubble.localPosition = new Vector3(0, Sliding ? 0.55f : 0.95f, 0);
                shieldBubble.Rotate(0, 0, dt * 90f, Space.Self);
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
        }

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
                r.legL.localRotation = Quaternion.Slerp(r.legL.localRotation, Quaternion.Euler(0, 0, -45), dt * 25);
                r.legR.localRotation = Quaternion.Slerp(r.legR.localRotation, Quaternion.Euler(0, 0, -30), dt * 25);
                armL = r.isBride ? 60 : -60; armR = -80;
                var target = Quaternion.Euler(0, 0, r.isBride ? 50f : 62f);
                r.body.localRotation = Quaternion.Slerp(r.body.localRotation, target, dt * 25);
                r.body.localPosition = new Vector3(0.35f, 0.05f, 0);
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
