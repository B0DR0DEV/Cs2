using System;

namespace StrikeZone.Core
{
    public enum HitGroup
    {
        Generic,
        Head,
        Chest,
        Stomach,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    public struct DamageResult
    {
        public int HealthDamage;
        public int ArmorDamage;
        public float RawDamage;
        public bool Headshot;
        public bool ArmorAbsorbed;
    }

    /// <summary>
    /// Damage formula modelled on the classic tactical-shooter rules:
    /// base damage → range falloff → penetration loss → hit-group multiplier → armor split.
    /// Pure function: the server and the client get the same number for the same inputs.
    /// </summary>
    public static class DamageModel
    {
        public static float HitGroupMultiplier(HitGroup group, WeaponDef weapon, DamageRules rules)
        {
            switch (group)
            {
                case HitGroup.Head:
                    return weapon != null && weapon.headMultiplier > 0f ? weapon.headMultiplier : rules.headMultiplier;
                case HitGroup.Chest:
                    return rules.chestMultiplier;
                case HitGroup.Stomach:
                    return rules.stomachMultiplier;
                case HitGroup.LeftArm:
                case HitGroup.RightArm:
                    return rules.armMultiplier;
                case HitGroup.LeftLeg:
                case HitGroup.RightLeg:
                    return rules.legMultiplier;
                default:
                    return 1f;
            }
        }

        /// <summary>Legs are never covered; the head only with a helmet.</summary>
        public static bool IsArmorProtected(HitGroup group, bool hasHelmet)
        {
            switch (group)
            {
                case HitGroup.Head:
                    return hasHelmet;
                case HitGroup.LeftLeg:
                case HitGroup.RightLeg:
                    return false;
                default:
                    return true;
            }
        }

        public static float RangeFalloff(WeaponDef weapon, float distanceUnits, DamageRules rules)
        {
            if (weapon.IsMelee || distanceUnits <= 0f) return 1f;
            return (float)Math.Pow(weapon.rangeModifier, distanceUnits / rules.rangeStepUnits);
        }

        /// <param name="baseDamage">Damage before modifiers (normally weapon.damage; melee passes the swing damage).</param>
        /// <param name="penetrationScale">Damage multiplier left after passing through surfaces (1 = clean hit).</param>
        public static DamageResult Compute(
            WeaponDef weapon,
            float baseDamage,
            HitGroup group,
            float distanceUnits,
            float penetrationScale,
            int armor,
            bool hasHelmet,
            DamageRules rules)
        {
            var result = new DamageResult();
            result.Headshot = group == HitGroup.Head;

            float damage = baseDamage * RangeFalloff(weapon, distanceUnits, rules) * penetrationScale;
            damage *= HitGroupMultiplier(group, weapon, rules);
            result.RawDamage = damage;

            if (armor > 0 && IsArmorProtected(group, hasHelmet))
            {
                float armorRatio = weapon.armorRatio * 0.5f;
                float toHealth = damage * armorRatio;
                float toArmor = (damage - toHealth) * rules.armorBonus;

                if (toArmor > armor)
                {
                    // Not enough armor to absorb its share: the overflow goes to health.
                    toArmor = armor;
                    toHealth = damage - toArmor / rules.armorBonus;
                }

                result.HealthDamage = (int)toHealth;
                result.ArmorDamage = (int)toArmor;
                result.ArmorAbsorbed = true;
            }
            else
            {
                result.HealthDamage = (int)damage;
            }

            if (result.HealthDamage < 0) result.HealthDamage = 0;
            if (result.ArmorDamage < 0) result.ArmorDamage = 0;
            return result;
        }
    }
}
