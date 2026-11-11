using Godot;
using NVector3 = System.Numerics.Vector3;

namespace StrikeZone.Game
{
    /// <summary>
    /// Conversions between the simulation core and Godot.
    /// Core: game units (1 unit = 1 inch), X right, Y up, Z forward, yaw clockwise, pitch up.
    /// Godot: metres, X right, Y up, -Z forward (right-handed), rotation.y counter-clockwise.
    /// Mapping a core vector to Godot = scale and negate Z.
    /// </summary>
    public static class Units
    {
        public const float ToMeters = 0.0254f;
        public const float ToUnits = 1f / 0.0254f;

        public static Vector3 ToGodot(NVector3 v)
        {
            return new Vector3(v.X, v.Y, -v.Z) * ToMeters;
        }

        public static NVector3 ToCore(Vector3 v)
        {
            return new NVector3(v.X, v.Y, -v.Z) * ToUnits;
        }

        /// <summary>Converts a core direction (unitless) to a Godot direction.</summary>
        public static Vector3 Dir(NVector3 v)
        {
            return new Vector3(v.X, v.Y, -v.Z);
        }

        public static Basis ViewBasis(float pitchDeg, float yawDeg)
        {
            return Basis.FromEuler(new Vector3(Mathf.DegToRad(pitchDeg), Mathf.DegToRad(-yawDeg), 0f));
        }

        public static Basis YawBasis(float yawDeg)
        {
            return Basis.FromEuler(new Vector3(0f, Mathf.DegToRad(-yawDeg), 0f));
        }
    }

    /// <summary>Physics layer bits (layer N = bit N-1).</summary>
    public static class Layers
    {
        public const uint World = 1u << 0;
        public const uint Hitbox = 1u << 1;
        public const uint Player = 1u << 2;
        public const uint BodyBlocker = 1u << 3;
        public const uint Debris = 1u << 4;
        public const uint Ladder = 1u << 5;

        public const uint BulletMask = World | Hitbox;
        public const uint SolidMask = World | BodyBlocker;
    }
}
