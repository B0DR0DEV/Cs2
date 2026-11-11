using System.Collections.Generic;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    public struct WorldImpact
    {
        public Vector3 Point;
        public Vector3 Normal;
        public SurfaceDef Surface;
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
    /// Hitscan with wall penetration. The ray is cast repeatedly, excluding everything
    /// already hit, so it walks through every collider on its path in order. For each world
    /// surface the exit point is found by casting back from beyond the wall, which gives the
    /// true thickness crossed; the core PenetrationModel decides whether the bullet survives
    /// and how much damage is left. Each actor is hit once (front-most hitbox), then the
    /// bullet continues with a "flesh" penetration cost — collaterals are possible but weak.
    /// </summary>
    public static class Ballistics
    {
        private static readonly SurfaceDef Flesh = new SurfaceDef { id = "flesh", resistance = 1f, damageFactor = 0.75f };
        private static readonly List<Damageable> HitTargets = new List<Damageable>(8);

        public static Vector3 Trace(PhysicsDirectSpaceState3D space, Vector3 origin, Vector3 dir, WeaponDef weapon, GameDatabase db,
            List<WorldImpact> impacts, List<ActorHit> actorHits)
        {
            impacts.Clear();
            actorHits.Clear();
            HitTargets.Clear();

            float maxDistance = weapon.range * Units.ToMeters;
            Vector3 farEnd = origin + dir * maxDistance;
            Vector3 end = origin + dir * Mathf.Min(maxDistance, 300f);
            float maxThickness = db.Damage.maxPenetrationThickness * Units.ToMeters;
            var pen = PenetrationModel.Begin(weapon, db.Damage);
            var exclude = new Godot.Collections.Array<Rid>();
            Vector3 from = origin;

            for (int iteration = 0; iteration < 16; iteration++)
            {
                var query = PhysicsRayQueryParameters3D.Create(from, farEnd, Layers.BulletMask, exclude);
                query.CollideWithAreas = true;
                query.CollideWithBodies = true;
                var hit = space.IntersectRay(query);
                if (hit.Count == 0) break;

                Vector3 point = hit["position"].AsVector3();
                Vector3 normal = hit["normal"].AsVector3();
                Rid rid = hit["rid"].AsRid();
                GodotObject collider = hit["collider"].AsGodotObject();
                exclude.Add(rid);

                var part = collider as HitboxPart;
                if (part != null)
                {
                    if (part.Target == null || !part.Target.IsAlive || HitTargets.Contains(part.Target)) continue;
                    HitTargets.Add(part.Target);
                    actorHits.Add(new ActorHit
                    {
                        Part = part,
                        Point = point,
                        DistanceMeters = origin.DistanceTo(point),
                        PenetrationScale = pen.DamageScale,
                        Penetrated = pen.Surfaces > 0
                    });
                    if (!PenetrationModel.TryPass(ref pen, 10f, Flesh, db.Damage))
                    {
                        end = point;
                        break;
                    }
                    from = point;
                    continue;
                }

                if (collider is Area3D) continue;

                SurfaceDef surface = db.GetSurface(SurfaceOf(collider));
                impacts.Add(new WorldImpact { Point = point, Normal = normal, Surface = surface, IsExit = false });

                Vector3 exitPoint, exitNormal;
                if (!FindExit(space, point, dir, rid, maxThickness, out exitPoint, out exitNormal))
                {
                    end = point;
                    break;
                }
                float thicknessUnits = point.DistanceTo(exitPoint) * Units.ToUnits;
                if (!PenetrationModel.TryPass(ref pen, thicknessUnits, surface, db.Damage))
                {
                    end = point;
                    break;
                }
                impacts.Add(new WorldImpact { Point = exitPoint, Normal = exitNormal, Surface = surface, IsExit = true });
                from = exitPoint + dir * 0.002f;
            }
            return end;
        }

        /// <summary>Casts back towards the entry point to find where the bullet leaves the collider <paramref name="rid"/>.</summary>
        private static bool FindExit(PhysicsDirectSpaceState3D space, Vector3 entry, Vector3 dir, Rid rid, float maxThickness,
            out Vector3 exitPoint, out Vector3 exitNormal)
        {
            exitPoint = entry;
            exitNormal = -dir;
            Vector3 far = entry + dir * (maxThickness + 0.01f);
            var skip = new Godot.Collections.Array<Rid>();
            for (int i = 0; i < 4; i++)
            {
                var q = PhysicsRayQueryParameters3D.Create(far, entry - dir * 0.001f, Layers.World, skip);
                q.CollideWithAreas = false;
                var hit = space.IntersectRay(q);
                if (hit.Count == 0) return false; // still inside: thicker than the maximum
                Rid hitRid = hit["rid"].AsRid();
                if (hitRid == rid)
                {
                    exitPoint = hit["position"].AsVector3();
                    exitNormal = hit["normal"].AsVector3();
                    return true;
                }
                skip.Add(hitRid);
            }
            return false;
        }

        public static string SurfaceOf(GodotObject collider)
        {
            var node = collider as Node;
            if (node != null && node.HasMeta("surface")) return node.GetMeta("surface").AsString();
            return null;
        }
    }
}
