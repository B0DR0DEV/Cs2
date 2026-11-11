using System.IO;
using NUnit.Framework;
using StrikeZone.Core;

namespace StrikeZone.Tests
{
    public class ConfigTests
    {
        private static GameDatabase Load()
        {
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "config");
            string weapons = File.ReadAllText(Path.Combine(dir, "weapons.json"));
            string rules = File.ReadAllText(Path.Combine(dir, "rules.json"));
            return ConfigJson.Parse(weapons, rules);
        }

        [Test]
        public void ShippedConfig_LoadsWithoutErrors()
        {
            GameDatabase db = Load();
            Assert.IsEmpty(db.Errors, string.Join("\n", db.Errors));
            Assert.GreaterOrEqual(db.Weapons.Count, 20);
            Assert.IsNotNull(db.GetWeapon("knife"));
            Assert.IsNotNull(db.GetWeapon("kestrel"));
            Assert.IsNotNull(db.GetWeapon("hammer"));
            Assert.AreEqual(301.993f, db.Movement.jumpImpulse, 1e-3f);
            Assert.AreEqual(1.0f, db.GetSurface("wood").resistance, 1e-5f);
        }

        [Test]
        public void ShippedConfig_NestedRecoilIsRead()
        {
            GameDatabase db = Load();
            var kestrel = db.GetWeapon("kestrel");
            Assert.AreEqual(41, kestrel.recoil.seed);
            Assert.AreEqual(9, kestrel.recoil.climbShots);
            Assert.AreEqual(WeaponCategory.Rifle, kestrel.Category);
            Assert.AreEqual(TeamSide.Attack, kestrel.Side);
            var hammer = db.GetWeapon("hammer");
            Assert.AreEqual(2, hammer.scopeFov.Length);
        }

        [Test]
        public void ShippedConfig_HasCategoryCoverage()
        {
            GameDatabase db = Load();
            int pistols = 0, smgs = 0, shotguns = 0, rifles = 0, snipers = 0, mgs = 0;
            foreach (var w in db.Weapons)
            {
                switch (w.Category)
                {
                    case WeaponCategory.Pistol: pistols++; break;
                    case WeaponCategory.SMG: smgs++; break;
                    case WeaponCategory.Shotgun: shotguns++; break;
                    case WeaponCategory.Rifle: rifles++; break;
                    case WeaponCategory.Sniper: snipers++; break;
                    case WeaponCategory.MachineGun: mgs++; break;
                }
            }
            Assert.GreaterOrEqual(pistols, 5);
            Assert.GreaterOrEqual(smgs, 3);
            Assert.AreEqual(2, shotguns);
            Assert.GreaterOrEqual(rifles, 6);
            Assert.AreEqual(3, snipers);
            Assert.GreaterOrEqual(mgs, 1);
        }

        [Test]
        public void ShippedConfig_BalanceInvariants()
        {
            GameDatabase db = Load();
            var rules = db.Damage;

            var hammer = db.GetWeapon("hammer");
            var chest = DamageModel.Compute(hammer, hammer.damage, HitGroup.Chest, 500f, 1f, 100, true, rules);
            Assert.GreaterOrEqual(chest.HealthDamage, 100, "bolt sniper must one-shot an armored chest");

            var kestrel = db.GetWeapon("kestrel");
            var head = DamageModel.Compute(kestrel, kestrel.damage, HitGroup.Head, 1200f, 1f, 100, true, rules);
            Assert.GreaterOrEqual(head.HealthDamage, 100, "attack rifle must one-tap a helmet at 30 m");

            var bull = db.GetWeapon("bull50");
            var bullHead = DamageModel.Compute(bull, bull.damage, HitGroup.Head, 200f, 1f, 100, true, rules);
            Assert.GreaterOrEqual(bullHead.HealthDamage, 100);

            var viper = db.GetWeapon("viper9");
            var body = DamageModel.Compute(viper, viper.damage, HitGroup.Chest, 200f, 1f, 0, false, rules);
            Assert.Less(body.HealthDamage, 100);

            foreach (var w in db.Weapons)
            {
                var rt = new WeaponRuntime(w);
                Assert.GreaterOrEqual(rt.PatternLength, 2, w.id);
            }
        }
    }
}
