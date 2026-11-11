using System;

namespace StrikeZone.Core
{
    /// <summary>
    /// Builds a fixed spray pattern: a vertical climb for the first shots, then a
    /// left-right sway with a slow residual climb. Values are cumulative offsets in
    /// degrees (pitch up, yaw right) for shot index i; shot 0 is always (0, 0) so the
    /// first bullet goes where the crosshair is.
    /// </summary>
    public static class RecoilPattern
    {
        public static void Build(RecoilPatternDef def, int length, float[] pitch, float[] yaw)
        {
            if (length < 1) return;
            var rng = new SzRandom((ulong)Math.Max(1, def.seed) * 2654435761UL + 17UL);
            pitch[0] = 0f;
            yaw[0] = 0f;
            float p = 0f;
            int climbShots = Math.Max(1, def.climbShots);
            float period = Math.Max(2f, def.swayPeriod);
            float dir = def.firstSwayDirection >= 0f ? 1f : -1f;

            for (int i = 1; i < length; i++)
            {
                float climb;
                float yawTarget;
                if (i <= climbShots)
                {
                    // Kick eases off slightly towards the end of the climb.
                    float t = (i - 1) / (float)climbShots;
                    climb = def.climbDeg * (1f - 0.35f * t);
                    // Slight drift opposite to the coming sway, so the spray "hooks".
                    yawTarget = -dir * def.swayDeg * 0.15f * t;
                }
                else
                {
                    climb = def.sustainClimbDeg;
                    float phase = (i - climbShots) / period * SzMath.TwoPi;
                    yawTarget = dir * def.swayDeg * (float)Math.Sin(phase);
                }

                p += climb + rng.NextSigned() * def.jitterDeg * 0.5f;
                pitch[i] = p;
                yaw[i] = yawTarget + rng.NextSigned() * def.jitterDeg;
            }
        }

        /// <summary>Samples the pattern at a fractional index (the index recovers smoothly between bursts).</summary>
        public static void Sample(float[] pitch, float[] yaw, float index, out float outPitch, out float outYaw)
        {
            int last = pitch.Length - 1;
            if (index <= 0f || last <= 0)
            {
                outPitch = 0f;
                outYaw = 0f;
                return;
            }
            if (index >= last)
            {
                outPitch = pitch[last];
                outYaw = yaw[last];
                return;
            }
            int i0 = (int)index;
            float t = index - i0;
            outPitch = SzMath.Lerp(pitch[i0], pitch[i0 + 1], t);
            outYaw = SzMath.Lerp(yaw[i0], yaw[i0 + 1], t);
        }
    }
}
