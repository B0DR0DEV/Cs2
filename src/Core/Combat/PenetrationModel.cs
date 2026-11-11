namespace StrikeZone.Core
{
    /// <summary>State of one bullet while it travels through surfaces.</summary>
    public struct PenetrationState
    {
        public float Budget;
        public float InitialBudget;
        public float DamageScale;
        public int Surfaces;
        public bool Stopped;
    }

    /// <summary>
    /// Wall-bang model. A bullet starts with a budget = penetrationPower × scale.
    /// Crossing a surface consumes thickness × resistance. Damage after the wall drops
    /// by the surface's own factor and by the share of budget spent, so a thin wooden
    /// door barely matters while 30 cm of concrete stops anything but the heaviest rounds.
    /// </summary>
    public static class PenetrationModel
    {
        public static PenetrationState Begin(WeaponDef weapon, DamageRules rules)
        {
            float budget = weapon.penetrationPower * rules.penetrationBudgetScale;
            return new PenetrationState
            {
                Budget = budget,
                InitialBudget = budget,
                DamageScale = 1f,
                Surfaces = 0,
                Stopped = budget <= 0f
            };
        }

        /// <summary>
        /// Tries to pass through a surface of the given thickness (game units).
        /// Returns false and marks the bullet stopped when it cannot get through.
        /// </summary>
        public static bool TryPass(ref PenetrationState state, float thicknessUnits, SurfaceDef surface, DamageRules rules)
        {
            if (state.Stopped) return false;
            if (state.Surfaces >= rules.maxPenetrationSurfaces || thicknessUnits > rules.maxPenetrationThickness)
            {
                state.Stopped = true;
                return false;
            }

            float resistance = surface != null ? surface.resistance : 1f;
            float cost = thicknessUnits * resistance;
            if (cost >= state.Budget)
            {
                state.Stopped = true;
                return false;
            }

            state.Budget -= cost;
            state.Surfaces++;
            float surfaceFactor = surface != null ? surface.damageFactor : 0.85f;
            float spentShare = state.InitialBudget > 0f ? cost / state.InitialBudget : 1f;
            state.DamageScale *= surfaceFactor * (1f - spentShare);
            if (state.DamageScale < 0.05f)
            {
                state.Stopped = true;
                return false;
            }
            return true;
        }
    }
}
