using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>An Area3D that maps to one hit group of its target (ray queries collide with areas).</summary>
    public partial class HitboxPart : Area3D
    {
        public Damageable Target;
        public HitGroup Group;
    }
}
