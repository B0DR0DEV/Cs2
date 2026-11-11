using System;

namespace StrikeZone.Core
{
    /// <summary>Source-style movement constants. Units/s and units/s².</summary>
    [Serializable]
    public class MovementConfig
    {
        public float friction = 5.2f;
        public float stopSpeed = 80f;
        public float accelerate = 5.5f;
        public float airAccelerate = 12f;
        public float airWishSpeedCap = 30f;
        public float gravity = 800f;
        public float jumpImpulse = 301.993f;
        public float walkFraction = 0.52f;
        public float crouchFraction = 0.34f;
        public float crouchTransitionSpeed = 8f;
        /// <summary>Below this share of the weapon's max speed, moving costs no accuracy.</summary>
        public float accurateSpeedFraction = 0.34f;
        public float standHeight = 72f;
        public float crouchHeight = 54f;
        public float standEyeHeight = 64f;
        public float crouchEyeHeight = 46f;
        public float radius = 16f;
        public float stepHeight = 18f;
        public float ladderSpeed = 200f;
        public float fallDamageSafeSpeed = 580f;
        public float fallDamageFatalSpeed = 1024f;
        public float tagRecoveryTime = 0.4f;
        public float maxVelocity = 3500f;
    }

    [Serializable]
    public class SurfaceDef
    {
        public string id;
        public string displayName;
        /// <summary>Penetration budget consumed per unit of thickness.</summary>
        public float resistance = 1f;
        /// <summary>Extra damage multiplier for passing through this surface.</summary>
        public float damageFactor = 0.85f;
        public string footstep = "concrete";
        public string color = "#808080";
        public float smoothness = 0.2f;
        public float metallic;
    }

    [Serializable]
    public class DamageRules
    {
        public float headMultiplier = 4f;
        public float chestMultiplier = 1f;
        public float stomachMultiplier = 1.25f;
        public float armMultiplier = 1f;
        public float legMultiplier = 0.75f;
        /// <summary>Armor absorbs damage at this ratio (CS: 0.5).</summary>
        public float armorBonus = 0.5f;
        /// <summary>Range falloff step: damage *= rangeModifier ^ (distance / rangeStepUnits).</summary>
        public float rangeStepUnits = 500f;
        public int maxPenetrationSurfaces = 4;
        /// <summary>Weapon penetrationPower is multiplied by this to get the budget in resistance-units.</summary>
        public float penetrationBudgetScale = 10f;
        public float maxPenetrationThickness = 64f;
        public int maxHealth = 100;
        public int maxArmor = 100;
    }

    /// <summary>Root object of rules.json.</summary>
    [Serializable]
    public class GameRulesFile
    {
        public MovementConfig movement = new MovementConfig();
        public DamageRules damage = new DamageRules();
        public SurfaceDef[] surfaces = new SurfaceDef[0];
    }
}
