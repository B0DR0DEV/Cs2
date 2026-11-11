using System.Collections.Generic;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// The player's loadout (primary, secondary, knife). Drives WeaponRuntime from player
    /// commands at the physics tick, performs hitscan and spawns feedback. In the
    /// multiplayer phase the same flow runs on the server for authority.
    /// </summary>
    public partial class WeaponController : Node
    {
        private const int KnifeSlot = 3;

        private readonly WeaponRuntime[] _slots = new WeaponRuntime[4];
        private readonly ShotOffset[] _offsets = new ShotOffset[16];
        private readonly List<WorldImpact> _impacts = new List<WorldImpact>(16);
        private readonly List<ActorHit> _actorHits = new List<ActorHit>(8);

        private GameDatabase _db;
        private PlayerController _player;
        private ViewModel _viewModel;
        private int _current = KnifeSlot;
        private int _last = 2;
        private bool _dryFired;
        private int _reloadStage;
        private int _sessionSeed;

        public WeaponRuntime Current
        {
            get { return _slots[_current]; }
        }

        public int CurrentSlot
        {
            get { return _current; }
        }

        /// <summary>Total inaccuracy in degrees right now (drives the dynamic crosshair).</summary>
        public float CurrentSpread { get; private set; }

        public void Setup(GameDatabase db, PlayerController player, ViewModel viewModel)
        {
            _db = db;
            _player = player;
            _viewModel = viewModel;
            _sessionSeed = (int)(Time.GetUnixTimeFromSystem() % 100000);

            var s = GameContext.Settings;
            Give(db.GetWeapon("knife"), false);
            Give(db.GetWeapon(s.LoadoutSecondary) ?? db.GetWeapon("viper9"), false);
            Give(db.GetWeapon(s.LoadoutPrimary) ?? db.GetWeapon("kestrel"), true);
        }

        public WeaponRuntime GetSlot(int slot)
        {
            return slot >= 1 && slot <= 3 ? _slots[slot] : null;
        }

        public void Give(WeaponDef def, bool draw)
        {
            if (def == null) return;
            int slot = Mathf.Clamp(def.slot, 1, 3);
            _slots[slot] = new WeaponRuntime(def);
            if (draw || slot == _current) Select(slot, true);
        }

        public void RefillAll()
        {
            for (int i = 1; i <= 3; i++) _slots[i]?.Refill();
        }

        private float Now
        {
            get { return _player.MatchTime; }
        }

        public void Select(int slot, bool force = false)
        {
            if (slot < 1 || slot > 3 || _slots[slot] == null) return;
            if (slot == _current && !force) return;
            if (slot != _current)
            {
                _last = _current;
                _slots[_current]?.CancelReload();
            }
            _current = slot;
            var w = _slots[slot];
            w.Deploy(Now);
            _viewModel.SetWeapon(w.Def);
            _viewModel.OnDeploy(w.Def.deployTime);
            _reloadStage = 0;
            GameContext.Audio?.Play2D(GameContext.Sounds.Get("deploy"), 0.5f);
            GameContext.Hud?.OnWeaponChanged(w);
        }

        /// <summary>Called from the player's physics tick.</summary>
        public void Tick(ref PlayerCommand cmd, ref AccuracyContext accuracy, float dt)
        {
            float now = Now;
            var w = Current;
            if (w == null) return;
            w.InfiniteReserve = GameContext.Settings.InfiniteAmmo || GameContext.SessionInfiniteAmmo;
            w.Tick(now, dt, accuracy.Crouched);

            if (cmd.SelectSlot > 0) Select(cmd.SelectSlot);
            else if (cmd.LastWeapon) Select(_last);
            else if (cmd.NextWeapon) Select(NextSlot());
            w = Current;

            if (cmd.ReloadPressed && w.StartReload(now)) _reloadStage = 1;
            if (!w.Def.IsMelee && cmd.ScopePressed && w.Def.HasScope)
            {
                w.CycleScope(now);
                GameContext.Audio?.Play2D(GameContext.Sounds.Get("scope"), 0.6f);
            }
            if (cmd.InspectPressed) _viewModel.StartInspect();

            if (!cmd.FireHeld) _dryFired = false;
            accuracy.WeaponMaxSpeed = w.CurrentMaxSpeed;

            if (w.Def.IsMelee)
            {
                if (cmd.FireHeld) Swing(w, now, false);
                else if (cmd.AltFirePressed) Swing(w, now, true);
            }
            else
            {
                bool trigger = w.Def.fullAuto ? cmd.FireHeld : cmd.FirePressed;
                if (trigger)
                {
                    if (w.Ammo <= 0 && !w.IsReloading)
                    {
                        if (!_dryFired)
                        {
                            _dryFired = true;
                            GameContext.Audio?.Play2D(GameContext.Sounds.Get("dry"), 0.7f);
                            if (w.StartReload(now)) _reloadStage = 1;
                        }
                    }
                    else
                    {
                        int guard = 0;
                        while (w.CanFire(now) && guard++ < 4)
                        {
                            Shoot(w, now, ref accuracy);
                            if (!w.Def.fullAuto) break;
                        }
                    }
                }
            }

            UpdateReloadSounds(w, now);
            CurrentSpread = w.Def.IsMelee ? 0f : w.CurrentInaccuracy(ref accuracy);
        }

        /// <summary>Called every rendered frame for smooth view-model animation.</summary>
        public void AnimateViewModel(float dt, float lookYaw, float lookPitch)
        {
            var w = Current;
            if (w == null) return;
            float now = Now;
            float reloadProgress = w.IsReloading ? 1f - (w.ReloadEndTime - now) / Mathf.Max(0.01f, w.Def.reloadTime) : -1f;
            bool sniperScope = w.IsScoped && w.Def.Category == WeaponCategory.Sniper;
            _viewModel.Animate(dt, _player.VelocityMeters, _player.Grounded, lookYaw, lookPitch, reloadProgress, sniperScope, w.IsScoped && !sniperScope);
        }

        private int NextSlot()
        {
            for (int i = 1; i <= 3; i++)
            {
                int s = (_current - 1 + i) % 3 + 1;
                if (_slots[s] != null) return s;
            }
            return _current;
        }

        // ================================================================== guns

        private void Shoot(WeaponRuntime w, float now, ref AccuracyContext accuracy)
        {
            ulong seed = SzRandom.Hash(_sessionSeed, w.ShotCounter, (int)Engine.GetPhysicsFrames());
            int pellets = w.Fire(now, ref accuracy, seed, _offsets);
            if (pellets <= 0) return;

            var stats = GameContext.Session;
            if (stats != null) stats.Shots++;

            Vector3 origin = _player.EyePosition;
            Vector3 muzzle = _viewModel.Muzzle != null ? _viewModel.Muzzle.GlobalPosition : origin;
            var space = _player.GetWorld3D().DirectSpaceState;
            bool anyHit = false, anyHead = false;

            for (int p = 0; p < pellets; p++)
            {
                Vector3 dir = Units.Dir(SzMath.OffsetDirection(_player.Pitch, _player.Yaw, _offsets[p].Pitch, _offsets[p].Yaw));
                Vector3 end = Ballistics.Trace(space, origin, dir, w.Def, _db, _impacts, _actorHits);

                var fx = GameContext.Effects;
                if (fx != null)
                {
                    if (p == 0 || p % 3 == 0) fx.SpawnTracer(muzzle, end);
                    for (int i = 0; i < _impacts.Count; i++)
                    {
                        var imp = _impacts[i];
                        fx.SpawnDecal(imp.Point, imp.Normal);
                        fx.SpawnPuff(imp.Point, imp.Normal, PuffColor(imp.Surface), imp.IsExit ? 0.18f : 0.25f, 0.4f);
                        if (!imp.IsExit && i == 0 && p == 0)
                            GameContext.Audio?.PlayAt(GameContext.Sounds.Pick("impact_" + ImpactSound(imp.Surface)), imp.Point, 0.6f, (float)GD.RandRange(0.9, 1.1), 1.5f);
                    }
                }

                foreach (var hit in _actorHits)
                {
                    bool head;
                    ApplyHit(w.Def, w.Def.damage, hit, dir, out head);
                    anyHit = true;
                    anyHead |= head;
                }
            }

            if (stats != null && anyHit)
            {
                stats.Hits++;
                if (anyHead) stats.Headshots++;
            }

            GameContext.Audio?.Play2D(GameContext.Sounds.ShotSound(w.Def.soundProfile, w.Def.silenced), 0.9f, (float)GD.RandRange(0.97, 1.03));
            float kick = w.Def.Category == WeaponCategory.Sniper ? 2.2f : (w.Def.Category == WeaponCategory.Shotgun ? 1.8f : 1f);
            _viewModel.OnFire(kick, w.Def.silenced);

            if (GameContext.Effects != null && _viewModel.EjectPort != null && w.Def.Category != WeaponCategory.Shotgun)
            {
                Basis b = _player.ViewBasisNow;
                Vector3 ejectVel = b.X * (float)GD.RandRange(1.6, 2.4) + b.Y * (float)GD.RandRange(1.2, 2.0) + b.Z * 0.3f + _player.VelocityMeters;
                GameContext.Effects.EjectShell(_viewModel.EjectPort.GlobalPosition, ejectVel);
            }
            GameContext.Hud?.OnShot();
            if (w.Ammo == 0 && w.StartReload(now + w.Def.cycleTime)) _reloadStage = 1;
        }

        private bool ApplyHit(WeaponDef weapon, float baseDamage, ActorHit hit, Vector3 dir, out bool headshot)
        {
            Damageable target = hit.Part.Target;
            float distanceUnits = hit.DistanceMeters * Units.ToUnits;
            DamageResult result = DamageModel.Compute(weapon, baseDamage, hit.Part.Group, distanceUnits, hit.PenetrationScale,
                target.Armor, target.HasHelmet, _db.Damage);
            headshot = result.Headshot;

            var info = new HitInfo
            {
                Weapon = weapon,
                Group = hit.Part.Group,
                Damage = result,
                Point = hit.Point,
                Direction = dir,
                DistanceMeters = hit.DistanceMeters,
                Penetrated = hit.Penetrated
            };
            int before = target.Health;
            bool killed = target.ApplyHit(ref info);
            if (GameContext.Session != null) GameContext.Session.Damage += Mathf.Min(before, result.HealthDamage);

            GameContext.Effects?.SpawnPuff(hit.Point, -dir, new Color(0.65f, 0.08f, 0.06f, 0.9f), 0.22f, 0.35f);
            var audio = GameContext.Audio;
            if (audio != null)
            {
                if (result.Headshot)
                    audio.PlayAt(GameContext.Sounds.Get("headshot"), hit.Point, 1f, result.ArmorAbsorbed ? 1f : 0.85f, 12f);
                else
                    audio.PlayAt(GameContext.Sounds.Get("hit_body"), hit.Point, 0.9f, (float)GD.RandRange(0.95, 1.05), 8f);
                audio.Play2D(GameContext.Sounds.Get("hitmarker"), 0.35f);
            }
            if (killed && GameContext.Settings.Vibration) Input.VibrateHandheld(35, 0.6f);
            GameContext.Hud?.OnHit(ref info, killed);
            return killed;
        }

        // ================================================================== knife

        private void Swing(WeaponRuntime w, float now, bool heavy)
        {
            float baseDamage = w.Swing(now, heavy);
            if (baseDamage <= 0f) return;
            _viewModel.OnSwing(heavy);
            GameContext.Audio?.Play2D(GameContext.Sounds.Get("knife"), 0.7f, heavy ? 0.85f : 1.05f);

            Vector3 origin = _player.EyePosition;
            Basis view = _player.ViewBasisNow;
            Vector3 forward = -view.Z;
            float range = w.Def.meleeRange * Units.ToMeters;
            var space = _player.GetWorld3D().DirectSpaceState;

            // Five rays in a small cross approximate a swept sphere.
            Vector3[] offsets = { Vector3.Zero, view.X * 0.08f, -view.X * 0.08f, view.Y * 0.08f, -view.Y * 0.08f };
            foreach (var off in offsets)
            {
                var q = PhysicsRayQueryParameters3D.Create(origin + off * 0.2f, origin + forward * range + off, Layers.BulletMask);
                q.CollideWithAreas = true;
                var hit = space.IntersectRay(q);
                if (hit.Count == 0) continue;
                var collider = hit["collider"].AsGodotObject();
                Vector3 point = hit["position"].AsVector3();
                var part = collider as HitboxPart;
                if (part != null && part.Target != null && part.Target.IsAlive)
                {
                    var dummy = part.Target as TargetDummy;
                    if (dummy != null && dummy.IsBehind(forward)) baseDamage *= w.Def.backstabMultiplier;
                    var actorHit = new ActorHit { Part = part, Point = point, DistanceMeters = 0f, PenetrationScale = 1f };
                    bool head;
                    ApplyHit(w.Def, baseDamage, actorHit, forward, out head);
                    var stats = GameContext.Session;
                    if (stats != null)
                    {
                        stats.Shots++;
                        stats.Hits++;
                        if (head) stats.Headshots++;
                    }
                    return;
                }
                if (!(collider is Area3D))
                {
                    SurfaceDef surface = _db.GetSurface(Ballistics.SurfaceOf(collider));
                    Vector3 normal = hit["normal"].AsVector3();
                    GameContext.Effects?.SpawnPuff(point, normal, PuffColor(surface), 0.15f, 0.3f);
                    GameContext.Audio?.PlayAt(GameContext.Sounds.Pick("impact_" + ImpactSound(surface)), point, 0.5f, 1.2f, 1f);
                    return;
                }
            }
        }

        // ================================================================== helpers

        private void UpdateReloadSounds(WeaponRuntime w, float now)
        {
            if (!w.IsReloading || _reloadStage == 0)
            {
                if (!w.IsReloading) _reloadStage = 0;
                return;
            }
            float progress = 1f - (w.ReloadEndTime - now) / Mathf.Max(0.01f, w.Def.reloadTime);
            var audio = GameContext.Audio;
            if (audio == null) return;
            if (_reloadStage == 1 && progress > 0.12f)
            {
                _reloadStage = 2;
                audio.Play2D(GameContext.Sounds.Get("mag_out"), 0.7f);
            }
            else if (_reloadStage == 2 && progress > 0.58f)
            {
                _reloadStage = 3;
                audio.Play2D(GameContext.Sounds.Get("mag_in"), 0.8f);
            }
            else if (_reloadStage == 3 && progress > 0.85f)
            {
                _reloadStage = 4;
                audio.Play2D(GameContext.Sounds.Get("bolt"), 0.7f);
            }
        }

        public static string ImpactSound(SurfaceDef surface)
        {
            if (surface == null) return "concrete";
            switch (surface.footstep)
            {
                case "metal": return "metal";
                case "wood": return "wood";
                case "dirt": return "dirt";
                default: return "concrete";
            }
        }

        private static Color PuffColor(SurfaceDef surface)
        {
            switch (surface != null ? surface.id : "concrete")
            {
                case "metal": return new Color(1f, 0.75f, 0.35f, 1f);
                case "wood": return new Color(0.55f, 0.4f, 0.25f, 0.85f);
                case "dirt":
                case "sand": return new Color(0.6f, 0.5f, 0.35f, 0.85f);
                case "brick": return new Color(0.65f, 0.4f, 0.3f, 0.85f);
                default: return new Color(0.75f, 0.74f, 0.7f, 0.85f);
            }
        }
    }
}
