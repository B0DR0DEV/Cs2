using System;
using NUnit.Framework;
using StrikeZone.Core;
using NVector3 = System.Numerics.Vector3;

namespace StrikeZone.Tests
{
    internal static class TestDefs
    {
        public static WeaponDef Rifle()
        {
            var w = new WeaponDef
            {
                id = "test_rifle",
                category = "Rifle",
                damage = 36f,
                armorRatio = 1.55f,
                rangeModifier = 0.98f,
                penetrationPower = 2f,
                cycleTime = 0.1f,
                fullAuto = true,
                magazineSize = 30,
                reserveAmmo = 90,
                reloadTime = 2.5f,
                deployTime = 1f,
                maxSpeed = 215f,
                inaccuracyStand = 0.38f,
                inaccuracyCrouch = 0.26f,
                inaccuracyMove = 7.5f,
                inaccuracyJump = 12f,
                inaccuracyFire = 0.55f,
                inaccuracyFireMax = 5f,
                recoveryTimeStand = 0.38f,
                recoveryTimeCrouch = 0.3f,
                recoilRecoveryRate = 14f,
                recoil = new RecoilPatternDef { seed = 41, climbShots = 9, climbDeg = 0.95f, swayDeg = 2.6f, swayPeriod = 12f }
            };
            w.Resolve();
            return w;
        }

        public static WeaponDef Sniper()
        {
            var w = new WeaponDef
            {
                id = "test_sniper",
                category = "Sniper",
                damage = 115f,
                armorRatio = 1.95f,
                rangeModifier = 0.99f,
                cycleTime = 1.455f,
                magazineSize = 5,
                reloadTime = 3.6f,
                maxSpeed = 200f,
                scopedMaxSpeed = 100f,
                scopeFov = new[] { 22f, 7f }
            };
            w.Resolve();
            return w;
        }

        public static AccuracyContext Standing(float maxSpeed)
        {
            return new AccuracyContext
            {
                HorizontalSpeed = 0f,
                OnGround = true,
                Crouched = false,
                WeaponMaxSpeed = maxSpeed,
                AccurateSpeedFraction = 0.34f
            };
        }
    }

    public class DamageModelTests
    {
        private readonly DamageRules _rules = new DamageRules();

        [Test]
        public void RifleHeadshot_KillsThroughHelmet()
        {
            var r = DamageModel.Compute(TestDefs.Rifle(), 36f, HitGroup.Head, 0f, 1f, 100, true, _rules);
            Assert.IsTrue(r.Headshot);
            Assert.AreEqual(111, r.HealthDamage);
            Assert.AreEqual(16, r.ArmorDamage);
            Assert.GreaterOrEqual(r.HealthDamage, 100);
        }

        [Test]
        public void Headshot_WithoutHelmet_IgnoresBodyArmor()
        {
            var r = DamageModel.Compute(TestDefs.Rifle(), 36f, HitGroup.Head, 0f, 1f, 100, false, _rules);
            Assert.AreEqual(144, r.HealthDamage);
            Assert.AreEqual(0, r.ArmorDamage);
        }

        [Test]
        public void Legs_AreNeverArmored()
        {
            var r = DamageModel.Compute(TestDefs.Rifle(), 36f, HitGroup.LeftLeg, 0f, 1f, 100, true, _rules);
            Assert.AreEqual(27, r.HealthDamage);
            Assert.AreEqual(0, r.ArmorDamage);
        }

        [Test]
        public void Stomach_Multiplier_Applies()
        {
            var r = DamageModel.Compute(TestDefs.Rifle(), 36f, HitGroup.Stomach, 0f, 1f, 0, false, _rules);
            Assert.AreEqual(45, r.HealthDamage);
        }

        [Test]
        public void ArmorOverflow_GoesToHealth()
        {
            var w = TestDefs.Rifle();
            w.armorRatio = 1f;
            var r = DamageModel.Compute(w, 100f, HitGroup.Chest, 0f, 1f, 5, false, _rules);
            Assert.AreEqual(5, r.ArmorDamage);
            Assert.AreEqual(90, r.HealthDamage);
        }

        [Test]
        public void BoltSniper_KillsArmoredChest()
        {
            var r = DamageModel.Compute(TestDefs.Sniper(), 115f, HitGroup.Chest, 0f, 1f, 100, true, _rules);
            Assert.GreaterOrEqual(r.HealthDamage, 100);
        }

        [Test]
        public void RangeFalloff_FollowsModifierPerStep()
        {
            var w = TestDefs.Rifle();
            Assert.AreEqual(0.98f, DamageModel.RangeFalloff(w, 500f, _rules), 1e-4f);
            Assert.AreEqual(0.98f * 0.98f, DamageModel.RangeFalloff(w, 1000f, _rules), 1e-4f);
            Assert.AreEqual(1f, DamageModel.RangeFalloff(w, 0f, _rules), 1e-6f);
        }
    }

    public class PenetrationTests
    {
        private readonly DamageRules _rules = new DamageRules();

        [Test]
        public void Rifle_PassesThinWood_WithModestLoss()
        {
            var wood = new SurfaceDef { id = "wood", resistance = 1f, damageFactor = 0.9f };
            var s = PenetrationModel.Begin(TestDefs.Rifle(), _rules);
            Assert.IsTrue(PenetrationModel.TryPass(ref s, 4f, wood, _rules));
            Assert.AreEqual(0.9f * (1f - 4f / 20f), s.DamageScale, 1e-4f);
        }

        [Test]
        public void Rifle_IsStoppedByThickConcrete()
        {
            var concrete = new SurfaceDef { id = "concrete", resistance = 4f, damageFactor = 0.75f };
            var s = PenetrationModel.Begin(TestDefs.Rifle(), _rules);
            Assert.IsFalse(PenetrationModel.TryPass(ref s, 8f, concrete, _rules));
            Assert.IsTrue(s.Stopped);
        }

        [Test]
        public void SurfaceLimit_StopsBullet()
        {
            var paper = new SurfaceDef { id = "paper", resistance = 0.01f, damageFactor = 1f };
            var s = PenetrationModel.Begin(TestDefs.Rifle(), _rules);
            for (int i = 0; i < _rules.maxPenetrationSurfaces; i++)
                Assert.IsTrue(PenetrationModel.TryPass(ref s, 1f, paper, _rules));
            Assert.IsFalse(PenetrationModel.TryPass(ref s, 1f, paper, _rules));
        }
    }

    public class MovementTests
    {
        private const float Dt = 1f / 64f;
        private readonly MovementConfig _cfg = new MovementConfig();

        private MovementInput Input(float forward, float side, float weaponSpeed = 250f)
        {
            return new MovementInput { Forward = forward, Side = side, YawDeg = 0f, WeaponMaxSpeed = weaponSpeed };
        }

        [Test]
        public void GroundAcceleration_ReachesButNeverExceedsWeaponSpeed()
        {
            var s = MovementState.Create();
            var input = Input(1f, 0f, 250f);
            for (int i = 0; i < 128; i++) PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
            Assert.LessOrEqual(s.HorizontalSpeed, 250.01f);
            Assert.GreaterOrEqual(s.HorizontalSpeed, 249f);
        }

        [Test]
        public void Walking_Uses52PercentSpeed()
        {
            var s = MovementState.Create();
            var input = Input(1f, 0f, 250f);
            input.Walk = true;
            for (int i = 0; i < 128; i++) PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
            Assert.AreEqual(130f, s.HorizontalSpeed, 1f);
        }

        [Test]
        public void CounterStrafe_ReachesAccurateSpeedQuickly()
        {
            var s = MovementState.Create();
            s.Velocity = new NVector3(250f, 0f, 0f);
            var input = Input(0f, -1f, 250f);
            float t = 0f;
            while (s.HorizontalSpeed > 250f * _cfg.accurateSpeedFraction && t < 1f)
            {
                PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
                t += Dt;
            }
            Assert.Less(t, 0.1f, "counter-strafe took " + t + " s");
        }

        [Test]
        public void ReleasingKeys_StopsSlowerThanCounterStrafe()
        {
            var s = MovementState.Create();
            s.Velocity = new NVector3(250f, 0f, 0f);
            var input = Input(0f, 0f, 250f);
            float t = 0f;
            while (s.HorizontalSpeed > 250f * _cfg.accurateSpeedFraction && t < 1f)
            {
                PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
                t += Dt;
            }
            Assert.Greater(t, 0.1f);
        }

        [Test]
        public void Jump_ApexMatchesImpulse()
        {
            var s = MovementState.Create();
            var input = Input(0f, 0f);
            input.Jump = true;
            PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
            input.Jump = false;
            Assert.IsFalse(s.OnGround);
            float height = s.Velocity.Y * Dt;
            for (int i = 0; i < 256 && s.Velocity.Y > 0f; i++)
            {
                PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
                height += s.Velocity.Y * Dt;
            }
            float expected = _cfg.jumpImpulse * _cfg.jumpImpulse / (2f * _cfg.gravity);
            Assert.AreEqual(expected, height, 5f);
        }

        [Test]
        public void AirAcceleration_CannotGainForwardSpeed()
        {
            var s = MovementState.Create();
            s.OnGround = false;
            s.Velocity = new NVector3(0f, 0f, 250f);
            var input = Input(1f, 0f);
            for (int i = 0; i < 32; i++) PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
            Assert.AreEqual(250f, s.HorizontalSpeed, 0.01f);
        }

        [Test]
        public void FallDamage_Thresholds()
        {
            Assert.AreEqual(0, PlayerMovement.FallDamage(580f, _cfg));
            Assert.AreEqual(100, PlayerMovement.FallDamage(1024f, _cfg));
            Assert.AreEqual(50, PlayerMovement.FallDamage(802f, _cfg));
        }

        [Test]
        public void Tagging_SlowsThenRecovers()
        {
            var s = MovementState.Create();
            PlayerMovement.ApplyTag(ref s, 0.5f);
            Assert.AreEqual(0.5f, s.TagMultiplier, 1e-5f);
            var input = Input(0f, 0f);
            for (int i = 0; i < 64; i++) PlayerMovement.Simulate(ref s, ref input, _cfg, Dt);
            Assert.AreEqual(1f, s.TagMultiplier, 1e-5f);
        }
    }

    public class WeaponTests
    {
        private readonly ShotOffset[] _offsets = new ShotOffset[16];

        [Test]
        public void SameSeed_GivesSameSpread()
        {
            var a = new WeaponRuntime(TestDefs.Rifle());
            var b = new WeaponRuntime(TestDefs.Rifle());
            var ctx = TestDefs.Standing(215f);
            var o1 = new ShotOffset[1];
            var o2 = new ShotOffset[1];
            a.Fire(5f, ref ctx, 12345UL, o1);
            b.Fire(5f, ref ctx, 12345UL, o2);
            Assert.AreEqual(o1[0].Pitch, o2[0].Pitch);
            Assert.AreEqual(o1[0].Yaw, o2[0].Yaw);
        }

        [Test]
        public void FirstShot_StaysWithinStandingInaccuracy()
        {
            var ctx = TestDefs.Standing(215f);
            for (ulong seed = 1; seed < 200; seed++)
            {
                var w = new WeaponRuntime(TestDefs.Rifle());
                Assert.AreEqual(1, w.Fire(5f, ref ctx, seed, _offsets));
                float r = (float)Math.Sqrt(_offsets[0].Pitch * _offsets[0].Pitch + _offsets[0].Yaw * _offsets[0].Yaw);
                Assert.LessOrEqual(r, 0.38f + 1e-4f);
            }
        }

        [Test]
        public void FullAuto_CadenceIsFrameRateIndependent()
        {
            var w = new WeaponRuntime(TestDefs.Rifle());
            var ctx = TestDefs.Standing(215f);
            int shots = 0;
            const float dt = 1f / 60f;
            for (int frame = 0; frame * dt < 0.999f; frame++)
            {
                float now = frame * dt;
                w.Tick(now, dt, false);
                while (w.CanFire(now) && w.Fire(now, ref ctx, (ulong)(shots + 1), _offsets) > 0) shots++;
            }
            Assert.AreEqual(10, shots);
        }

        [Test]
        public void Spray_ClimbsThenRecovers()
        {
            var w = new WeaponRuntime(TestDefs.Rifle());
            var ctx = TestDefs.Standing(215f);
            float now = 0f;
            for (int i = 0; i < 9; i++)
            {
                Assert.Greater(w.Fire(now, ref ctx, (ulong)(i + 1), _offsets), 0);
                now += 0.1f;
            }
            float pitch, yaw;
            w.GetRecoil(out pitch, out yaw);
            Assert.Greater(pitch, 4f);

            for (int i = 0; i < 64; i++)
            {
                now += 1f / 64f;
                w.Tick(now, 1f / 64f, false);
            }
            Assert.AreEqual(0f, w.RecoilIndex, 1e-5f);
            Assert.Less(w.AccumulatedInaccuracy, 0.01f);
        }

        [Test]
        public void Pattern_IsFixedAcrossInstances()
        {
            var a = new WeaponRuntime(TestDefs.Rifle());
            var b = new WeaponRuntime(TestDefs.Rifle());
            for (int i = 0; i < a.PatternLength; i++)
            {
                float pa, ya, pb, yb;
                a.GetPatternPoint(i, out pa, out ya);
                b.GetPatternPoint(i, out pb, out yb);
                Assert.AreEqual(pa, pb);
                Assert.AreEqual(ya, yb);
            }
        }

        [Test]
        public void Moving_IsMuchLessAccurate()
        {
            var w = new WeaponRuntime(TestDefs.Rifle());
            var still = TestDefs.Standing(215f);
            var running = TestDefs.Standing(215f);
            running.HorizontalSpeed = 215f;
            float a = w.CurrentInaccuracy(ref still);
            float b = w.CurrentInaccuracy(ref running);
            Assert.Greater(b, a + 7f);
            var slow = TestDefs.Standing(215f);
            slow.HorizontalSpeed = 60f;
            Assert.AreEqual(a, w.CurrentInaccuracy(ref slow), 1e-5f);
        }

        [Test]
        public void Reload_RefillsFromReserve()
        {
            var w = new WeaponRuntime(TestDefs.Rifle());
            var ctx = TestDefs.Standing(215f);
            float now = 0f;
            for (int i = 0; i < 5; i++)
            {
                w.Fire(now, ref ctx, (ulong)(i + 1), _offsets);
                now += 0.1f;
            }
            Assert.AreEqual(25, w.Ammo);
            Assert.IsTrue(w.StartReload(now));
            Assert.IsFalse(w.CanFire(now + 0.1f));
            w.Tick(now + 2.6f, 0.016f, false);
            Assert.AreEqual(30, w.Ammo);
            Assert.AreEqual(85, w.Reserve);
        }

        [Test]
        public void BoltSniper_UnscopesAfterShotAndResumes()
        {
            var def = TestDefs.Sniper();
            def.unscopeAfterShot = true;
            var w = new WeaponRuntime(def);
            var ctx = TestDefs.Standing(100f);
            Assert.AreEqual(1, w.CycleScope(0f));
            Assert.AreEqual(1, w.Fire(0f, ref ctx, 1UL, _offsets));
            Assert.IsFalse(w.IsScoped);
            w.Tick(1.5f, 0.016f, false);
            Assert.IsTrue(w.IsScoped);
        }
    }

    public class RandomTests
    {
        [Test]
        public void Random_IsDeterministicAndInRange()
        {
            var a = new SzRandom(42UL);
            var b = new SzRandom(42UL);
            for (int i = 0; i < 1000; i++)
            {
                float x = a.NextFloat();
                Assert.AreEqual(x, b.NextFloat());
                Assert.GreaterOrEqual(x, 0f);
                Assert.Less(x, 1f);
            }
        }

        [Test]
        public void Hash_DiffersForDifferentInputs()
        {
            Assert.AreNotEqual(SzRandom.Hash(1, 2, 3), SzRandom.Hash(1, 2, 4));
            Assert.AreEqual(SzRandom.Hash(7, 8, 9), SzRandom.Hash(7, 8, 9));
        }
    }

    public class MathTests
    {
        [Test]
        public void OffsetDirection_UpIsUp()
        {
            NVector3 d = SzMath.OffsetDirection(0f, 0f, 10f, 0f);
            Assert.Greater(d.Y, 0.15f);
            Assert.AreEqual(0f, d.X, 1e-5f);
            NVector3 r = SzMath.OffsetDirection(0f, 0f, 0f, 10f);
            Assert.Greater(r.X, 0.15f);
        }
    }
}
