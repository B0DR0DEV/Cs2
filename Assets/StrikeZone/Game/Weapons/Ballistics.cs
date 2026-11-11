using System;
using System.Collections.Generic;
using StrikeZone.Core;
using UnityEngine;

namespace StrikeZone.Game
{
    public struct WorldImpact
    {
        public Vector3 Point;
        public Vector3 Normal;
        public SurfaceDef Surface;
        public Transform Transform;
        public bool IsExit;
    }

    public struct ActorHit
    {
        public HitboxPart Part;
        public Vector3 Point;
        public float DistanceMeters;
        public float PenetrationScale;
        public bool Penetrated;
    }

    /// <summary>
    /// Hitscan with wall penetration. The ray collects every collider along its path,
    /// sorted by distance. For each world surface we find the exit point by casting
    /// backwards against that one collider, which gives the true thickness crossed, and
    /// ask the core PenetrationModel whether the bullet survives and how much damage remains.
    /// Actors are hit once each (the front-most hitbox), then the bullet continues with a
    /// "flesh" penetration cost so collaterals are possible but weak.
    /// </summary>
    public static class Ballistics
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[48];
        private static readonly HitDistanceComparer Comparer = new HitDistanceComparer();
        private static readonly List<Damageable> HitOwners = new List<Damageable>(8);
        private static readonly SurfaceDef Flesh = new SurfaceDef { id = "flesh", resistance = 1f, damageFactor = 0.75f };

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b)
            {
                return a.distance.CompareTo(b.distance);
            }
        }

        /// <returns>The point where the bullet stopped (or its max range), for drawing the tracer.</returns>
        public static Vector3 Trace(Vector3 origin, Vector3 direction, WeaponDef weapon, GameDatabase db,
            List<WorldImpact> impacts, List<ActorHit> actorHits)
        {
            impacts.Clear();
            actorHits.Clear();
            HitOwners.Clear();

            float maxDistance = weapon.range * Units.ToMeters;
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, maxDistance, Layers.BulletMask, QueryTriggerInteraction.Collide);
            Array.Sort(Hits, 0, count, Comparer);

            var pen = PenetrationModel.Begin(weapon, db.Damage);
            float maxThicknessMeters = db.Damage.maxPenetrationThickness * Units.ToMeters;
            Vector3 end = origin + direction * Mathf.Min(maxDistance, 300f);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                Collider col = hit.collider;
                if (col == null) continue;

                if (col.gameObject.layer == Layers.Hitbox)
                {
                    var part = col.GetComponent<HitboxPart>();
                    if (part == null || part.Owner == null || !part.Owner.IsAlive) continue;
                    if (HitOwners.Contains(part.Owner)) continue;
                    HitOwners.Add(part.Owner);

                    actorHits.Add(new ActorHit
                    {
                        Part = part,
                        Point = hit.point,
                        DistanceMeters = hit.distance,
                        PenetrationScale = pen.DamageScale,
                        Penetrated = pen.Surfaces > 0
                    });

                    if (!PenetrationModel.TryPass(ref pen, 10f, Flesh, db.Damage))
                    {
                        end = hit.point;
                        break;
                    }
                    continue;
                }

                if (col.isTrigger) continue;

                var tag = col.GetComponent<SurfaceTag>();
                SurfaceDef surface = db.GetSurface(tag != null ? tag.SurfaceId : null);
                impacts.Add(new WorldImpact { Point = hit.point, Normal = hit.normal, Surface = surface, Transform = col.transform, IsExit = false });

                // Find where the bullet leaves this collider.
                Vector3 far = hit.point + direction * (maxThicknessMeters + 0.01f);
                RaycastHit exit;
                float backLength = maxThicknessMeters + 0.01f;
                if (!col.Raycast(new Ray(far, -direction), out exit, backLength))
                {
                    end = hit.point;
                    break;
                }
                float thicknessMeters = Mathf.Max(0f, backLength - exit.distance);
                if (!PenetrationModel.TryPass(ref pen, thicknessMeters * Units.ToUnits, surface, db.Damage))
                {
                    end = hit.point;
                    break;
                }
                impacts.Add(new WorldImpact { Point = exit.point, Normal = exit.normal, Surface = surface, Transform = col.transform, IsExit = true });
            }

            return end;
        }
    }
}
