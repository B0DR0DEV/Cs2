using System;
using System.Text.Json.Serialization;

namespace StrikeZone.Core
{
    public enum WeaponCategory
    {
        Knife,
        Pistol,
        SMG,
        Shotgun,
        Rifle,
        Sniper,
        MachineGun
    }

    public enum TeamSide
    {
        Any,
        Attack,
        Defense
    }

    /// <summary>
    /// Parameters of a fixed, learnable spray pattern. The pattern is generated once from
    /// these numbers with a seeded RNG, so it is identical every time (and on the server).
    /// </summary>
    [Serializable]
    public class RecoilPatternDef
    {
        public int seed = 1;
        /// <summary>Number of shots in the initial vertical climb.</summary>
        public int climbShots = 8;
        /// <summary>Vertical kick per shot at the start of the climb, degrees.</summary>
        public float climbDeg = 0.8f;
        /// <summary>Vertical kick per shot after the climb, degrees.</summary>
        public float sustainClimbDeg = 0.08f;
        /// <summary>Horizontal sway amplitude after the climb, degrees.</summary>
        public float swayDeg = 1.5f;
        /// <summary>Shots per full left-right sway cycle.</summary>
        public float swayPeriod = 12f;
        /// <summary>Fixed per-shot irregularity baked into the pattern, degrees.</summary>
        public float jitterDeg = 0.08f;
        /// <summary>+1: the spray first drifts right, -1: left.</summary>
        public float firstSwayDirection = -1f;
    }

    /// <summary>
    /// Complete balance description of a weapon. Loaded from JSON; every number that
    /// affects feel lives here so designers can tune without recompiling.
    /// Angles in degrees, distances in game units, times in seconds, speeds in units/s.
    /// </summary>
    [Serializable]
    public class WeaponDef
    {
        public string id;
        public string displayName;
        public string category;
        public string side = "Any";
        public int slot = 1;
        public int price;
        public int killReward = 300;

        // --- Damage ---
        public float damage = 30f;
        /// <summary>CS-style armor ratio: share of damage that reaches health through armor = armorRatio * 0.5.</summary>
        public float armorRatio = 1f;
        public float headMultiplier = 4f;
        /// <summary>Damage multiplier per 500 units of distance.</summary>
        public float rangeModifier = 0.98f;
        public float range = 8192f;
        public float penetrationPower = 1f;
        public int pellets = 1;
        public float pelletSpread;

        // --- Handling ---
        public float cycleTime = 0.1f;
        public bool fullAuto;
        public int magazineSize = 30;
        public int reserveAmmo = 90;
        public float reloadTime = 2.5f;
        public float deployTime = 1f;
        public float maxSpeed = 250f;
        public float scopedMaxSpeed;
        /// <summary>Vertical field of view per zoom level; empty = no scope.</summary>
        public float[] scopeFov = new float[0];
        public bool unscopeAfterShot;
        public bool silenced;
        /// <summary>How strongly a hit from this weapon slows the victim (0..1).</summary>
        public float taggingStrength = 0.5f;

        // --- Accuracy (degrees) ---
        public float inaccuracyStand = 0.3f;
        public float inaccuracyCrouch = 0.2f;
        public float inaccuracyMove = 6f;
        public float inaccuracyJump = 10f;
        public float inaccuracyFire = 0.5f;
        public float inaccuracyFireMax = 6f;
        public float scopedInaccuracyStand;
        public float scopedInaccuracyCrouch;
        public float recoveryTimeStand = 0.35f;
        public float recoveryTimeCrouch = 0.3f;

        // --- Recoil ---
        /// <summary>How many pattern steps per second the spray index recovers after you stop firing.</summary>
        public float recoilRecoveryRate = 14f;
        /// <summary>Share of recoil applied to the camera (the rest only moves the bullets).</summary>
        public float cameraRecoilFollow = 0.5f;
        public RecoilPatternDef recoil = new RecoilPatternDef();

        // --- Melee ---
        public float meleeRange = 48f;
        public float meleeAltDamage;
        public float meleeAltCycleTime = 1f;
        public float backstabMultiplier = 1f;

        // --- Presentation ---
        public string soundProfile = "rifle";
        public string modelProfile = "rifle";

        [NonSerialized, JsonIgnore] public WeaponCategory Category;
        [NonSerialized, JsonIgnore] public TeamSide Side;

        public bool IsMelee
        {
            get { return Category == WeaponCategory.Knife; }
        }

        public bool HasScope
        {
            get { return scopeFov != null && scopeFov.Length > 0; }
        }

        /// <summary>Parses string fields into enums. Call once after deserialisation.</summary>
        public void Resolve()
        {
            WeaponCategory cat;
            Category = Enum.TryParse(category, true, out cat) ? cat : WeaponCategory.Rifle;
            TeamSide s;
            Side = Enum.TryParse(side, true, out s) ? s : TeamSide.Any;
            if (scopeFov == null) scopeFov = new float[0];
            if (recoil == null) recoil = new RecoilPatternDef();
        }

        /// <summary>Returns null when the definition is valid, otherwise a description of the first problem.</summary>
        public string Validate()
        {
            if (string.IsNullOrEmpty(id)) return "weapon without id";
            WeaponCategory cat;
            if (!Enum.TryParse(category, true, out cat)) return id + ": unknown category '" + category + "'";
            if (damage <= 0f) return id + ": damage must be > 0";
            if (cycleTime <= 0f) return id + ": cycleTime must be > 0";
            if (maxSpeed <= 0f) return id + ": maxSpeed must be > 0";
            if (cat != WeaponCategory.Knife)
            {
                if (magazineSize <= 0) return id + ": magazineSize must be > 0";
                if (reloadTime <= 0f) return id + ": reloadTime must be > 0";
                if (pellets <= 0) return id + ": pellets must be > 0";
                if (armorRatio <= 0f || armorRatio > 2f) return id + ": armorRatio must be in (0, 2]";
                if (rangeModifier <= 0f || rangeModifier > 1f) return id + ": rangeModifier must be in (0, 1]";
            }
            if (scopeFov != null && scopeFov.Length > 0 && scopedMaxSpeed <= 0f) return id + ": scoped weapon needs scopedMaxSpeed";
            return null;
        }
    }

    [Serializable]
    public class WeaponDatabaseDef
    {
        public WeaponDef[] weapons = new WeaponDef[0];
    }
}
