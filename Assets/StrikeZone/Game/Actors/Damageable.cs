using StrikeZone.Core;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>Everything known about one bullet (or swing) that hit an actor.</summary>
    public struct HitInfo
    {
        public WeaponDef Weapon;
        public HitGroup Group;
        public DamageResult Damage;
        public Vector3 Point;
        public Vector3 Direction;
        public float DistanceMeters;
        public bool Penetrated;
    }

    /// <summary>
    /// Base for anything with health and armor. In phase 1 the shooter computes damage
    /// locally; in phase 6 this call happens on the server only and the client just
    /// receives the result.
    /// </summary>
    public abstract class Damageable : MonoBehaviour
    {
        public int Health = 100;
        public int MaxHealth = 100;
        public int Armor = 100;
        public bool HasHelmet = true;

        public bool IsAlive
        {
            get { return Health > 0; }
        }

        /// <summary>Applies an already computed hit. Returns true if it killed.</summary>
        public bool ApplyHit(ref HitInfo hit)
        {
            if (!IsAlive) return false;
            Armor = Mathf.Max(0, Armor - hit.Damage.ArmorDamage);
            if (Armor == 0) HasHelmet = false;
            Health = Mathf.Max(0, Health - hit.Damage.HealthDamage);
            OnDamaged(ref hit);
            if (Health == 0)
            {
                OnKilled(ref hit);
                return true;
            }
            return false;
        }

        protected abstract void OnDamaged(ref HitInfo hit);
        protected abstract void OnKilled(ref HitInfo hit);
    }
}
