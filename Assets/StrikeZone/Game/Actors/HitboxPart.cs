using StrikeZone.Core;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>A collider that maps to a hit group of its owner.</summary>
    public sealed class HitboxPart : MonoBehaviour
    {
        public Damageable Owner;
        public HitGroup Group;
    }
}
