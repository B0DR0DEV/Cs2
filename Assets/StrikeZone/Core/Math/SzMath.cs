using System;
using System.Numerics;

namespace StrikeZone.Core
{
    /// <summary>
    /// Math helpers for the simulation. Coordinate convention (same axes as Unity, so the
    /// adapter layer only scales units): X = right, Y = up, Z = forward.
    /// Angles are degrees. Yaw rotates clockwise around +Y starting at +Z.
    /// Pitch is positive when looking UP (Unity's Euler X is the opposite sign).
    /// Distances are game units (1 unit = 1 inch = 0.0254 m).
    /// </summary>
    public static class SzMath
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public const float TwoPi = (float)(Math.PI * 2.0);

        public static float Clamp(float v, float min, float max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        public static float Clamp01(float v)
        {
            return Clamp(v, 0f, 1f);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        public static Vector3 Forward(float yawDeg)
        {
            float y = yawDeg * Deg2Rad;
            return new Vector3((float)Math.Sin(y), 0f, (float)Math.Cos(y));
        }

        public static Vector3 Right(float yawDeg)
        {
            float y = yawDeg * Deg2Rad;
            return new Vector3((float)Math.Cos(y), 0f, -(float)Math.Sin(y));
        }

        public static Vector3 Direction(float pitchDeg, float yawDeg)
        {
            float p = pitchDeg * Deg2Rad;
            float y = yawDeg * Deg2Rad;
            float cp = (float)Math.Cos(p);
            return new Vector3(cp * (float)Math.Sin(y), (float)Math.Sin(p), cp * (float)Math.Cos(y));
        }

        /// <summary>
        /// Applies an angular offset in view space: the offset is measured along the view's
        /// own right/up axes, so spread stays circular even when looking straight up or down.
        /// </summary>
        public static Vector3 OffsetDirection(float pitchDeg, float yawDeg, float offsetPitchDeg, float offsetYawDeg)
        {
            Vector3 forward = Direction(pitchDeg, yawDeg);
            Vector3 right = Right(yawDeg);
            // Component-wise: Cross((0,0,1), (1,0,0)) = (0,1,0), i.e. up for a level view.
            Vector3 up = Vector3.Cross(forward, right);
            float tx = (float)Math.Tan(offsetYawDeg * Deg2Rad);
            float ty = (float)Math.Tan(offsetPitchDeg * Deg2Rad);
            Vector3 dir = forward + right * tx + up * ty;
            return Vector3.Normalize(dir);
        }

        public static float HorizontalLength(Vector3 v)
        {
            return (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);
        }
    }
}
