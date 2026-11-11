using System;

namespace StrikeZone.Core
{
    /// <summary>Everything the accuracy model needs to know about the shooter this tick.</summary>
    public struct AccuracyContext
    {
        public float HorizontalSpeed;
        public bool OnGround;
        public bool Crouched;
        /// <summary>Max speed of the weapon in its current mode; movement inaccuracy scales against it.</summary>
        public float WeaponMaxSpeed;
        public float AccurateSpeedFraction;
    }

    /// <summary>Angular offset of one pellet relative to the view direction, degrees.</summary>
    public struct ShotOffset
    {
        public float Pitch;
        public float Yaw;
    }

    /// <summary>
    /// Runtime state of one weapon instance: ammo, timers, accumulated inaccuracy and
    /// spray index. Engine-independent so the server runs the exact same logic.
    /// Time is passed in explicitly (seconds); nothing reads a global clock.
    /// </summary>
    public sealed class WeaponRuntime
    {
        public readonly WeaponDef Def;
        private readonly float[] _patternPitch;
        private readonly float[] _patternYaw;

        public int Ammo;
        public int Reserve;
        public bool InfiniteAmmo;
        public bool InfiniteReserve;

        public float NextFireTime;
        public float ReloadEndTime;
        public float DeployEndTime;
        public float LastShotTime = -1000f;
        public float AccumulatedInaccuracy;
        public float RecoilIndex;
        public int ScopeLevel;
        public int ShotCounter;

        private int _resumeScopeLevel;

        public WeaponRuntime(WeaponDef def)
        {
            if (def == null) throw new ArgumentNullException("def");
            Def = def;
            Ammo = def.IsMelee ? 0 : def.magazineSize;
            Reserve = def.IsMelee ? 0 : def.reserveAmmo;
            int length = Math.Max(2, def.magazineSize + 1);
            _patternPitch = new float[length];
            _patternYaw = new float[length];
            RecoilPattern.Build(def.recoil, length, _patternPitch, _patternYaw);
        }

        public bool IsReloading
        {
            get { return ReloadEndTime > 0f; }
        }

        public bool IsScoped
        {
            get { return ScopeLevel > 0; }
        }

        public int PatternLength
        {
            get { return _patternPitch.Length; }
        }

        public float CurrentMaxSpeed
        {
            get { return IsScoped && Def.scopedMaxSpeed > 0f ? Def.scopedMaxSpeed : Def.maxSpeed; }
        }

        public void GetPatternPoint(int index, out float pitch, out float yaw)
        {
            int i = Math.Max(0, Math.Min(index, _patternPitch.Length - 1));
            pitch = _patternPitch[i];
            yaw = _patternYaw[i];
        }

        /// <summary>Called when the weapon is drawn. Cancels reload and zoom.</summary>
        public void Deploy(float now)
        {
            DeployEndTime = now + Def.deployTime;
            ReloadEndTime = 0f;
            ScopeLevel = 0;
            _resumeScopeLevel = 0;
            if (NextFireTime < DeployEndTime) NextFireTime = DeployEndTime;
        }

        public void Tick(float now, float dt, bool crouched)
        {
            if (ReloadEndTime > 0f && now >= ReloadEndTime) FinishReload();

            float recovery = crouched ? Def.recoveryTimeCrouch : Def.recoveryTimeStand;
            if (recovery > 0f)
            {
                // Exponential decay; ~95 % recovered after `recovery` seconds.
                AccumulatedInaccuracy *= (float)Math.Exp(-3f * dt / recovery);
                if (AccumulatedInaccuracy < 1e-4f) AccumulatedInaccuracy = 0f;
            }
            else
            {
                AccumulatedInaccuracy = 0f;
            }

            // Spray index recovers only once the trigger cadence is broken.
            if (now - LastShotTime > Def.cycleTime * 1.1f && RecoilIndex > 0f)
                RecoilIndex = Math.Max(0f, RecoilIndex - Def.recoilRecoveryRate * dt);

            if (_resumeScopeLevel > 0 && now >= NextFireTime && !IsReloading)
            {
                ScopeLevel = _resumeScopeLevel;
                _resumeScopeLevel = 0;
            }
        }

        public bool CanFire(float now)
        {
            if (now < NextFireTime || now < DeployEndTime || IsReloading) return false;
            return Def.IsMelee || Ammo > 0;
        }

        public float CurrentInaccuracy(ref AccuracyContext ctx)
        {
            float baseInaccuracy;
            if (IsScoped && Def.scopedInaccuracyStand > 0f)
                baseInaccuracy = ctx.Crouched ? Def.scopedInaccuracyCrouch : Def.scopedInaccuracyStand;
            else
                baseInaccuracy = ctx.Crouched ? Def.inaccuracyCrouch : Def.inaccuracyStand;

            float moving = 0f;
            float maxSpeed = ctx.WeaponMaxSpeed > 0f ? ctx.WeaponMaxSpeed : CurrentMaxSpeed;
            float accurateSpeed = maxSpeed * ctx.AccurateSpeedFraction;
            if (ctx.HorizontalSpeed > accurateSpeed && maxSpeed > accurateSpeed)
            {
                float t = SzMath.Clamp01((ctx.HorizontalSpeed - accurateSpeed) / (maxSpeed - accurateSpeed));
                moving = Def.inaccuracyMove * t;
            }

            float air = ctx.OnGround ? 0f : Def.inaccuracyJump;
            return baseInaccuracy + moving + air + AccumulatedInaccuracy;
        }

        /// <summary>Recoil offset at the current spray index — what the camera displays.</summary>
        public void GetRecoil(out float pitch, out float yaw)
        {
            RecoilPattern.Sample(_patternPitch, _patternYaw, RecoilIndex, out pitch, out yaw);
        }

        /// <summary>
        /// Fires one shot if possible. Writes one offset per pellet into <paramref name="offsets"/>
        /// (recoil + inaccuracy + pellet spread) and returns the pellet count, or 0 if it did not fire.
        /// <paramref name="seed"/> must be derived from shooter id, tick and shot counter so the
        /// server reproduces the exact same spread.
        /// </summary>
        public int Fire(float now, ref AccuracyContext ctx, ulong seed, ShotOffset[] offsets)
        {
            if (Def.IsMelee || !CanFire(now)) return 0;

            var rng = new SzRandom(seed);
            float inaccuracy = CurrentInaccuracy(ref ctx);
            float recoilPitch, recoilYaw;
            GetRecoil(out recoilPitch, out recoilYaw);

            // Radius uniform in [0, inaccuracy] → shots cluster towards the centre.
            float r = inaccuracy * rng.NextFloat();
            float theta = rng.NextFloat() * SzMath.TwoPi;
            float basePitch = recoilPitch + r * (float)Math.Sin(theta);
            float baseYaw = recoilYaw + r * (float)Math.Cos(theta);

            int pellets = Math.Min(Math.Max(1, Def.pellets), offsets.Length);
            for (int i = 0; i < pellets; i++)
            {
                float pr = Def.pelletSpread * (float)Math.Sqrt(rng.NextFloat());
                float pt = rng.NextFloat() * SzMath.TwoPi;
                offsets[i].Pitch = basePitch + pr * (float)Math.Sin(pt);
                offsets[i].Yaw = baseYaw + pr * (float)Math.Cos(pt);
            }

            if (!InfiniteAmmo) Ammo--;
            AccumulatedInaccuracy = Math.Min(AccumulatedInaccuracy + Def.inaccuracyFire, Math.Max(Def.inaccuracyFireMax, Def.inaccuracyFire));
            RecoilIndex = Math.Min(RecoilIndex + 1f, _patternPitch.Length - 1);
            LastShotTime = now;
            // Keep cadence independent of frame rate: schedule from the previous slot when firing continuously.
            NextFireTime = (now - NextFireTime) < Def.cycleTime ? NextFireTime + Def.cycleTime : now + Def.cycleTime;
            ShotCounter++;

            if (Def.unscopeAfterShot && ScopeLevel > 0)
            {
                _resumeScopeLevel = ScopeLevel;
                ScopeLevel = 0;
            }
            return pellets;
        }

        /// <summary>Melee swing. Returns the base damage of the swing, or 0 if not ready.</summary>
        public float Swing(float now, bool alternate)
        {
            if (!Def.IsMelee || !CanFire(now)) return 0f;
            float cycle = alternate && Def.meleeAltCycleTime > 0f ? Def.meleeAltCycleTime : Def.cycleTime;
            NextFireTime = now + cycle;
            LastShotTime = now;
            ShotCounter++;
            return alternate && Def.meleeAltDamage > 0f ? Def.meleeAltDamage : Def.damage;
        }

        public bool StartReload(float now)
        {
            if (Def.IsMelee || IsReloading || now < DeployEndTime) return false;
            if (Ammo >= Def.magazineSize) return false;
            if (Reserve <= 0 && !InfiniteReserve) return false;
            ReloadEndTime = now + Def.reloadTime;
            ScopeLevel = 0;
            _resumeScopeLevel = 0;
            return true;
        }

        public void CancelReload()
        {
            ReloadEndTime = 0f;
        }

        private void FinishReload()
        {
            ReloadEndTime = 0f;
            int needed = Def.magazineSize - Ammo;
            if (needed <= 0) return;
            int take = InfiniteReserve ? needed : Math.Min(needed, Reserve);
            Ammo += take;
            if (!InfiniteReserve) Reserve -= take;
        }

        /// <summary>Cycles zoom levels: 0 → 1 → … → 0. Returns the new level.</summary>
        public int CycleScope(float now)
        {
            if (!Def.HasScope || IsReloading || now < DeployEndTime) return ScopeLevel;
            _resumeScopeLevel = 0;
            ScopeLevel = ScopeLevel >= Def.scopeFov.Length ? 0 : ScopeLevel + 1;
            return ScopeLevel;
        }

        public float ScopeFov
        {
            get { return IsScoped ? Def.scopeFov[ScopeLevel - 1] : 0f; }
        }

        public void Refill()
        {
            if (Def.IsMelee) return;
            Ammo = Def.magazineSize;
            Reserve = Def.reserveAmmo;
            ReloadEndTime = 0f;
        }
    }
}
