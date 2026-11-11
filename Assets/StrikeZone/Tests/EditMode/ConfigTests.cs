using NUnit.Framework;
using StrikeZone.Core;
using StrikeZone.Game;

namespace StrikeZone.Tests
{
    public class ConfigTests
    {
        [Test]
        public void ShippedConfig_LoadsWithoutErrors()
        {
            GameDatabase db = ConfigLoader.Load();
            Assert.IsEmpty(db.Errors, string.Join("\n", db.Errors));
            Assert.GreaterOrEqual(db.Weapons.Count, 20);
            Assert.IsNotNull(db.GetWeapon("knife"));
            Assert.IsNotNull(db.GetWeapon("kestrel"));
            Assert.IsNotNull(db.GetWeapon("hammer"));
        }

        [Test]
        public void ShippedConfig_HasCategoryCoverage()
        {
            GameDatabase db = ConfigLoader.Load();
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
            GameDatabase db = ConfigLoader.Load();
            var rules = db.Damage;

            // Bolt sniper one-shots an armored chest.
            var hammer = db.GetWeapon("hammer");
            var chest = DamageModel.Compute(hammer, hammer.damage, HitGroup.Chest, 500f, 1f, 100, true, rules);
            Assert.GreaterOrEqual(chest.HealthDamage, 100);

            // Attack rifle one-taps a helmeted head at 30 m.
            var kestrel = db.GetWeapon("kestrel");
            var head = DamageModel.Compute(kestrel, kestrel.damage, HitGroup.Head, 1200f, 1f, 100, true, rules);
            Assert.GreaterOrEqual(head.HealthDamage, 100);

            // Heavy pistol one-taps a helmeted head up close.
            var bull = db.GetWeapon("bull50");
            var bullHead = DamageModel.Compute(bull, bull.damage, HitGroup.Head, 200f, 1f, 100, true, rules);
            Assert.GreaterOrEqual(bullHead.HealthDamage, 100);

            // Starter pistol needs more than one body shot.
            var viper = db.GetWeapon("viper9");
            var body = DamageModel.Compute(viper, viper.damage, HitGroup.Chest, 200f, 1f, 0, false, rules);
            Assert.Less(body.HealthDamage, 100);

            // Every shipped weapon builds a spray pattern of the right length.
            foreach (var w in db.Weapons)
            {
                var rt = new WeaponRuntime(w);
                Assert.GreaterOrEqual(rt.PatternLength, 2, w.id);
            }
        }
    }
}
