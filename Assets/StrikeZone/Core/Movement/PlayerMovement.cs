using System;
using System.Numerics;

namespace StrikeZone.Core
{
    /// <summary>Per-tick movement input. Analog axes in [-1, 1].</summary>
    public struct MovementInput
    {
        public float Forward;
        public float Side;
        public float YawDeg;
        public float PitchDeg;
        /// <summary>True only on the tick the jump was pressed (no auto-hop).</summary>
        public bool Jump;
        public bool Crouch;
        public bool Walk;
        /// <summary>Max speed of the weapon in hand in its current mode (scoped or not).</summary>
        public float WeaponMaxSpeed;
        public bool OnLadder;
    }

    public struct MovementState
    {
        public Vector3 Velocity;
        public bool OnGround;
        /// <summary>0 = standing, 1 = fully crouched.</summary>
        public float CrouchAmount;
        /// <summary>Speed multiplier from being hit (1 = no slowdown).</summary>
        public float TagMultiplier;

        public bool IsCrouched
        {
            get { return CrouchAmount > 0.5f; }
        }

        public float HorizontalSpeed
        {
            get { return SzMath.HorizontalLength(Velocity); }
        }

        public static MovementState Create()
        {
            return new MovementState { TagMultiplier = 1f, OnGround = true };
        }
    }

    /// <summary>
    /// Source-engine style movement: friction then acceleration on the ground, capped
    /// air-acceleration in the air. Counter-strafing is not special-cased — it emerges
    /// from friction and acceleration both opposing the current velocity.
    /// Collision is resolved by the engine adapter, which writes the clipped velocity back.
    /// </summary>
    public static class PlayerMovement
    {
        public static float CurrentMaxSpeed(ref MovementState state, ref MovementInput input, MovementConfig cfg)
        {
            float speed = input.WeaponMaxSpeed > 0f ? input.WeaponMaxSpeed : 250f;
            if (state.IsCrouched) speed *= cfg.crouchFraction;
            else if (input.Walk) speed *= cfg.walkFraction;
            return speed * state.TagMultiplier;
        }

        public static void Simulate(ref MovementState state, ref MovementInput input, MovementConfig cfg, float dt)
        {
            if (dt <= 0f) return;

            state.CrouchAmount = SzMath.MoveTowards(state.CrouchAmount, input.Crouch ? 1f : 0f, cfg.crouchTransitionSpeed * dt);
            if (cfg.tagRecoveryTime > 0f)
                state.TagMultiplier = Math.Min(1f, state.TagMultiplier + dt / cfg.tagRecoveryTime);
            else
                state.TagMultiplier = 1f;

            if (input.OnLadder)
            {
                SimulateLadder(ref state, ref input, cfg);
                return;
            }

            float maxSpeed = CurrentMaxSpeed(ref state, ref input, cfg);
            Vector3 wish = SzMath.Forward(input.YawDeg) * input.Forward + SzMath.Right(input.YawDeg) * input.Side;
            float wishLen = wish.Length();
            Vector3 wishDir = wishLen > 1e-4f ? wish / wishLen : Vector3.Zero;
            float wishSpeed = maxSpeed * Math.Min(1f, wishLen);

            Vector3 v = state.Velocity;

            if (state.OnGround)
            {
                if (input.Jump)
                {
                    v.Y = cfg.jumpImpulse;
                    state.OnGround = false;
                    v = Accelerate(v, wishDir, Math.Min(wishSpeed, cfg.airWishSpeedCap), cfg.airAccelerate, wishSpeed, dt);
                }
                else
                {
                    v.Y = 0f;
                    v = ApplyFriction(v, cfg, dt);
                    v = Accelerate(v, wishDir, wishSpeed, cfg.accelerate, maxSpeed, dt);
                }
            }
            else
            {
                v = Accelerate(v, wishDir, Math.Min(wishSpeed, cfg.airWishSpeedCap), cfg.airAccelerate, wishSpeed, dt);
                v.Y -= cfg.gravity * dt;
            }

            // Safety clamp against numerical blow-ups.
            float len = v.Length();
            if (len > cfg.maxVelocity) v *= cfg.maxVelocity / len;

            state.Velocity = v;
        }

        private static void SimulateLadder(ref MovementState state, ref MovementInput input, MovementConfig cfg)
        {
            if (input.Jump)
            {
                // Hop off the ladder backwards.
                Vector3 back = -SzMath.Forward(input.YawDeg) * 150f;
                state.Velocity = new Vector3(back.X, 120f, back.Z);
                state.OnGround = false;
                return;
            }

            // Looking up while pressing forward climbs; looking down descends.
            float climbDir = input.PitchDeg > -20f ? 1f : -1f;
            float vertical = input.Forward * climbDir * cfg.ladderSpeed;
            Vector3 side = SzMath.Right(input.YawDeg) * (input.Side * cfg.ladderSpeed * 0.5f);
            // Push towards where the player looks: keeps them on the ladder and carries them
            // onto the ledge when they climb past the top of the ladder volume.
            Vector3 into = SzMath.Forward(input.YawDeg) * (input.Forward > 0.01f ? 80f : 0f);
            state.Velocity = new Vector3(side.X + into.X, vertical, side.Z + into.Z);
            state.OnGround = false;
        }

        /// <summary>Source PM_Friction: speed drops by max(speed, stopSpeed) × friction × dt.</summary>
        public static Vector3 ApplyFriction(Vector3 v, MovementConfig cfg, float dt)
        {
            float speed = SzMath.HorizontalLength(v);
            if (speed < 0.1f) return new Vector3(0f, v.Y, 0f);

            float control = speed < cfg.stopSpeed ? cfg.stopSpeed : speed;
            float drop = control * cfg.friction * dt;
            float newSpeed = speed - drop;
            if (newSpeed < 0f) newSpeed = 0f;
            float scale = newSpeed / speed;
            return new Vector3(v.X * scale, v.Y, v.Z * scale);
        }

        /// <summary>
        /// Source PM_Accelerate. <paramref name="wishSpeed"/> limits the projected speed
        /// along wishDir; <paramref name="accelScaleSpeed"/> sets how fast we get there.
        /// In the air wishSpeed is capped (30 u/s) but accelScaleSpeed is not — that is
        /// exactly what makes air-strafing possible.
        /// </summary>
        public static Vector3 Accelerate(Vector3 v, Vector3 wishDir, float wishSpeed, float accel, float accelScaleSpeed, float dt)
        {
            if (wishDir == Vector3.Zero) return v;
            float current = Vector3.Dot(new Vector3(v.X, 0f, v.Z), wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return v;
            float accelSpeed = accel * accelScaleSpeed * dt;
            if (accelSpeed > add) accelSpeed = add;
            return v + wishDir * accelSpeed;
        }

        /// <summary>Fall damage from vertical landing speed (units/s, positive = downward).</summary>
        public static int FallDamage(float fallSpeed, MovementConfig cfg)
        {
            if (fallSpeed <= cfg.fallDamageSafeSpeed) return 0;
            float span = cfg.fallDamageFatalSpeed - cfg.fallDamageSafeSpeed;
            if (span <= 0f) return 100;
            return (int)Math.Ceiling((fallSpeed - cfg.fallDamageSafeSpeed) * 100f / span);
        }

        /// <summary>Being hit slows the victim; repeated hits keep the strongest slowdown.</summary>
        public static void ApplyTag(ref MovementState state, float strength)
        {
            float target = 1f - SzMath.Clamp01(strength);
            if (target < state.TagMultiplier) state.TagMultiplier = target;
        }

        /// <summary>True when moving slowly enough that no movement inaccuracy applies.</summary>
        public static bool IsAccurateSpeed(float horizontalSpeed, float weaponMaxSpeed, MovementConfig cfg)
        {
            return horizontalSpeed <= weaponMaxSpeed * cfg.accurateSpeedFraction;
        }
    }
}
