using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace StrikeZone.Game
{
    /// <summary>
    /// The simulation works in game units (1 unit = 1 inch), Unity works in metres.
    /// All conversions go through here so nothing mixes the two by accident.
    /// </summary>
    public static class Units
    {
        public const float ToMeters = 0.0254f;
        public const float ToUnits = 1f / 0.0254f;

        public static Vector3 ToUnity(NVector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z) * ToMeters;
        }

        public static NVector3 ToCore(Vector3 v)
        {
            return new NVector3(v.x, v.y, v.z) * ToUnits;
        }

        public static Vector3 Direction(NVector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        /// <summary>Core pitch is positive when looking up; Unity Euler X is positive when looking down.</summary>
        public static Quaternion ViewRotation(float pitchDeg, float yawDeg)
        {
            return Quaternion.Euler(-pitchDeg, yawDeg, 0f);
        }
    }

    /// <summary>Physics layers. User layers 8+ are used by index so no TagManager edits are needed.</summary>
    public static class Layers
    {
        public const int World = 0;
        public const int IgnoreRaycast = 2;
        public const int Hitbox = 8;
        public const int Player = 9;
        public const int BodyBlocker = 10;
        public const int Debris = 11;
        public const int Ladder = 12;

        /// <summary>What bullets can hit.</summary>
        public const int BulletMask = (1 << World) | (1 << Hitbox);
        /// <summary>What blocks a player's capsule (used for head-room checks).</summary>
        public const int SolidMask = (1 << World) | (1 << BodyBlocker);
        public const int LadderMask = 1 << Ladder;

        private static bool _configured;

        public static void ConfigureCollisionMatrix()
        {
            if (_configured) return;
            _configured = true;
            Physics.IgnoreLayerCollision(Debris, Player, true);
            Physics.IgnoreLayerCollision(Debris, Hitbox, true);
            Physics.IgnoreLayerCollision(Debris, BodyBlocker, true);
            Physics.IgnoreLayerCollision(Debris, Debris, true);
            Physics.IgnoreLayerCollision(Hitbox, Player, true);
            Physics.IgnoreLayerCollision(Hitbox, BodyBlocker, true);
            Physics.IgnoreLayerCollision(Hitbox, Hitbox, true);
            Physics.IgnoreLayerCollision(Ladder, Debris, true);
            Physics.IgnoreLayerCollision(Ladder, Hitbox, true);
        }
    }
}
