using System.Collections.Generic;
using StrikeZone.Core;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Owns the player's loadout (primary, secondary, knife), drives WeaponRuntime from
    /// player commands, performs the hitscan and spawns feedback (sound, tracers, decals,
    /// shells, hit markers). In phase 6 the same flow runs on the server for authority,
    /// while the client keeps the effects.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        private const int PrimarySlot = 1;
        private const int SecondarySlot = 2;
        private const int KnifeSlot = 3;

        private readonly WeaponRuntime[] _slots = new WeaponRuntime[4];
        private readonly ShotOffset[] _offsets = new ShotOffset[16];
        private readonly List<WorldImpact> _impacts = new List<WorldImpact>(16);
        private readonly List<ActorHit> _actorHits = new List<ActorHit>(8);

        private GameDatabase _db;
        private PlayerController _player;
        private ViewModel _viewModel;
        private int _currentSlot = KnifeSlot;
        private int _lastSlot = SecondarySlot;
        private bool _dryFired;
        private float _reloadSoundStage;
        private ulong _sessionSeed;

        public WeaponRuntime Current
        {
            get { return _slots[_currentSlot]; }
        }

        public int CurrentSlot
        {
            get { return _currentSlot; }
        }

        public WeaponRuntime GetSlot(int slot)
        {
            return slot >= 1 && slot <= 3 ? _slots[slot] : null;
        }

        /// <summary>Current total inaccuracy in degrees (for the dynamic crosshair).</summary>
        public float CurrentSpread { get; private set; }

        public void Initialize(GameDatabase db, PlayerController player)
        {
            _db = db;
            _player = player;
            _sessionSeed = (ulong)System.DateTime.UtcNow.Ticks;

            var vmGo = new GameObject("ViewModel");
            vmGo.transform.SetParent(player.CameraTransform, false);
            _viewModel = vmGo.AddComponent<ViewModel>();
            _viewModel.Initialize(GameContext.Materials);

            Give(db.GetWeapon("knife"), false);
            Give(db.GetWeapon("viper9"), false);
            Give(db.GetWeapon("kestrel"), true);
        }

        /// <summary>Puts a weapon into its slot (replacing what was there) and optionally draws it.</summary>
        public void Give(WeaponDef def, bool draw)
        {
            if (def == null) return;
            int slot = Mathf.Clamp(def.slot, 1, 3);
            _slots[slot] = new WeaponRuntime(def);
            ApplyAmmoSettings(_slots[slot]);
            if (draw || slot == _currentSlot) Select(slot, true);
        }

        public void RefillAll()
        {
            for (int i = 1; i <= 3; i++)
                if (_slots[i] != null) _slots[i].Refill();
        }

        private void ApplyAmmoSettings(WeaponRuntime w)
        {
            if (w == null) return;
            var s = GameContext.Settings;
            w.InfiniteReserve = s != null && s.InfiniteAmmo;
        }

        public void Select(int slot, bool force = false)
        {
            if (slot < 1 || slot > 3 || _slots[slot] == null) return;
            if (slot == _currentSlot && !force) return;
            if (slot != _currentSlot)
            {
                _lastSlot = _currentSlot;
                if (_slots[_currentSlot] != null) _slots[_currentSlot].CancelReload();
            }
            _currentSlot = slot;
            WeaponRuntime w = _slots[slot];
            w.Deploy(Time.time);
            _viewModel.SetWeapon(w.Def, GameContext.Materials);
            _viewModel.OnDeploy(w.Def.deployTime);
            _reloadSoundStage = 0f;
            var audio = GameContext.Audio;
            if (audio != null) audio.Play2D(audio.Clips.Get("deploy"), 0.5f);
            if (GameContext.Hud != null) GameContext.Hud.OnWeaponChanged(w);
        }

        public void Tick(ref PlayerCommand cmd, ref AccuracyContext accuracy, float dt)
        {
            float now = Time.time;
            WeaponRuntime w = Current;
            if (w == null) return;

            ApplyAmmoSettings(w);
            w.Tick(now, dt, accuracy.Crouched);

            // ---------------------------------------------------------------- switching
            if (cmd.SelectSlot > 0) Select(cmd.SelectSlot);
            else if (cmd.LastWeapon) Select(_lastSlot);
            else if (cmd.NextWeapon) Select(NextSlot());
            w = Current;

            // ---------------------------------------------------------------- reload / scope / inspect
            if (cmd.ReloadPressed && w.StartReload(now)) BeginReloadSounds();
            if (!w.Def.IsMelee && cmd.ScopePressed && w.Def.HasScope)
            {
                w.CycleScope(now);
                var audio = GameContext.Audio;
                if (audio != null) audio.Play2D(audio.Clips.Get("scope"), 0.6f);
            }
            if (cmd.InspectPressed) _viewModel.StartInspect();

            // ---------------------------------------------------------------- firing
            if (!cmd.FireHeld) _dryFired = false;
            accuracy.WeaponMaxSpeed = w.CurrentMaxSpeed;

            if (w.Def.IsMelee)
            {
                if (cmd.FireHeld) TrySwing(w, now, false);
                else if (cmd.AltFirePressed) TrySwing(w, now, true);
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
                            var audio = GameContext.Audio;
                            if (audio != null) audio.Play2D(audio.Clips.Get("dry"), 0.7f);
                            if (w.StartReload(now)) BeginReloadSounds();
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

            // ---------------------------------------------------------------- view model
            float reloadProgress = -1f;
            if (w.IsReloading) reloadProgress = 1f - (w.ReloadEndTime - now) / Mathf.Max(0.01f, w.Def.reloadTime);
            bool sniperScope = w.IsScoped && w.Def.Category == WeaponCategory.Sniper;
            _viewModel.Animate(dt, _player.VelocityMeters, _player.IsGrounded, cmd.LookYaw, cmd.LookPitch,
                reloadProgress, sniperScope, w.IsScoped && !sniperScope);
        }

        private int NextSlot()
        {
            for (int i = 1; i <= 3; i++)
            {
                int s = (_currentSlot - 1 + i) % 3 + 1;
                if (_slots[s] != null) return s;
            }
            return _currentSlot;
        }

        // ==================================================================== guns

        private void Shoot(WeaponRuntime w, float now, ref AccuracyContext accuracy)
        {
            ulong seed = SzRandom.Hash((int)(_sessionSeed & 0x7fffffff), w.ShotCounter, Time.frameCount);
            int pellets = w.Fire(now, ref accuracy, seed, _offsets);
            if (pellets <= 0) return;

            var stats = GameContext.Stats;
            if (stats != null) stats.Shots++;

            Transform eye = _player.CameraTransform;
            Vector3 origin = eye.position;
            Vector3 muzzle = _viewModel.Muzzle != null ? _viewModel.Muzzle.position : origin;
            bool anyHit = false;
            bool anyHead = false;

            for (int p = 0; p < pellets; p++)
            {
                Vector3 dir = Units.Direction(SzMath.OffsetDirection(_player.Pitch, _player.Yaw, _offsets[p].Pitch, _offsets[p].Yaw));
                Vector3 end = Ballistics.Trace(origin, dir, w.Def, _db, _impacts, _actorHits);

                var effects = GameContext.Effects;
                if (effects != null)
                {
                    if (p == 0 || p % 3 == 0) effects.SpawnTracer(muzzle, end);
                    for (int i = 0; i < _impacts.Count; i++)
                    {
                        WorldImpact imp = _impacts[i];
                        effects.SpawnDecal(imp.Point, imp.Normal, imp.Transform);
                        effects.SpawnPuff(imp.Point, imp.Normal, PuffColor(imp.Surface), imp.IsExit ? 0.18f : 0.25f, 0.4f);
                        if (!imp.IsExit && i == 0 && p == 0)
                        {
                            var audio = GameContext.Audio;
                            if (audio != null) audio.PlayAt(audio.Clips.GetVariant("impact_" + ImpactSound(imp.Surface)), imp.Point, 0.6f, Random.Range(0.9f, 1.1f), 1.5f);
                        }
                    }
                }

                for (int i = 0; i < _actorHits.Count; i++)
                {
                    bool head;
                    ApplyActorHit(w.Def, w.Def.damage, _actorHits[i], dir, out head);
                    anyHit = true;
                    anyHead |= head;
                }
            }

            if (stats != null && anyHit)
            {
                stats.Hits++;
                if (anyHead) stats.Headshots++;
            }

            // Feedback.
            var a = GameContext.Audio;
            if (a != null) a.Play2D(a.Clips.Shot(w.Def.soundProfile, w.Def.silenced), 0.9f, Random.Range(0.97f, 1.03f));
            float kick = w.Def.Category == WeaponCategory.Sniper ? 2.2f : (w.Def.Category == WeaponCategory.Shotgun ? 1.8f : 1f);
            _viewModel.OnFire(kick, w.Def.silenced);
            if (GameContext.Effects != null && _viewModel.EjectPort != null && w.Def.Category != WeaponCategory.Shotgun)
            {
                Vector3 ejectVel = eye.right * Random.Range(1.6f, 2.4f) + eye.up * Random.Range(1.2f, 2f) - eye.forward * 0.3f + _player.VelocityMeters;
                GameContext.Effects.EjectShell(_viewModel.EjectPort.position, ejectVel);
            }
            if (GameContext.Hud != null) GameContext.Hud.OnShot();
            if (w.Ammo == 0 && !w.InfiniteAmmo && w.StartReload(now + w.Def.cycleTime)) BeginReloadSounds();
        }

        /// <returns>True when the hit killed.</returns>
        private bool ApplyActorHit(WeaponDef weapon, float baseDamage, ActorHit hit, Vector3 dir, out bool headshot)
        {
            Damageable target = hit.Part.Owner;
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
            int healthBefore = target.Health;
            bool killed = target.ApplyHit(ref info);

            var stats = GameContext.Stats;
            if (stats != null) stats.Damage += Mathf.Min(healthBefore, result.HealthDamage);

            var effects = GameContext.Effects;
            if (effects != null) effects.SpawnPuff(hit.Point, -dir, new Color(0.65f, 0.08f, 0.06f, 0.9f), 0.22f, 0.35f);

            var audio = GameContext.Audio;
            if (audio != null)
            {
                if (result.Headshot)
                    audio.PlayAt(audio.Clips.Get("headshot"), hit.Point, result.ArmorAbsorbed ? 1f : 0.85f, result.ArmorAbsorbed ? 1f : 0.85f, 25f);
                else
                    audio.PlayAt(audio.Clips.Get("hit_body"), hit.Point, 0.9f, Random.Range(0.95f, 1.05f), 15f);
                audio.Play2D(audio.Clips.Get("hitmarker"), 0.35f);
            }
            if (GameContext.Settings != null && GameContext.Settings.Vibration && killed) Haptics.Hit();
            if (GameContext.Hud != null) GameContext.Hud.OnHit(ref info, killed);
            return killed;
        }

        // ==================================================================== knife

        private void TrySwing(WeaponRuntime w, float now, bool heavy)
        {
            float baseDamage = w.Swing(now, heavy);
            if (baseDamage <= 0f) return;
            _viewModel.OnSwing(heavy);
            var audio = GameContext.Audio;
            if (audio != null) audio.Play2D(audio.Clips.Get("knife"), 0.7f, heavy ? 0.85f : 1.05f);

            Transform eye = _player.CameraTransform;
            float range = w.Def.meleeRange * Units.ToMeters;
            RaycastHit hit;
            if (!Physics.SphereCast(eye.position, 0.08f, eye.forward, out hit, range, Layers.BulletMask, QueryTriggerInteraction.Collide))
                return;

            var part = hit.collider.GetComponent<HitboxPart>();
            if (part != null && part.Owner != null && part.Owner.IsAlive)
            {
                var dummy = part.Owner as TargetDummy;
                if (dummy != null && dummy.IsBehind(eye.forward)) baseDamage *= w.Def.backstabMultiplier;
                var actorHit = new ActorHit { Part = part, Point = hit.point, DistanceMeters = 0f, PenetrationScale = 1f, Penetrated = false };
                bool head;
                ApplyActorHit(w.Def, baseDamage, actorHit, eye.forward, out head);
                var stats = GameContext.Stats;
                if (stats != null)
                {
                    stats.Shots++;
                    stats.Hits++;
                    if (head) stats.Headshots++;
                }
            }
            else if (!hit.collider.isTrigger)
            {
                var tag = hit.collider.GetComponent<SurfaceTag>();
                SurfaceDef surface = _db.GetSurface(tag != null ? tag.SurfaceId : null);
                if (GameContext.Effects != null) GameContext.Effects.SpawnPuff(hit.point, hit.normal, PuffColor(surface), 0.15f, 0.3f);
                if (audio != null) audio.PlayAt(audio.Clips.GetVariant("impact_" + ImpactSound(surface)), hit.point, 0.5f, 1.2f, 1f);
            }
        }

        // ==================================================================== helpers

        private void BeginReloadSounds()
        {
            _reloadSoundStage = 0.0001f;
            if (GameContext.Hud != null) GameContext.Hud.OnReload();
        }

        private void UpdateReloadSounds(WeaponRuntime w, float now)
        {
            if (!w.IsReloading || _reloadSoundStage <= 0f) return;
            float progress = 1f - (w.ReloadEndTime - now) / Mathf.Max(0.01f, w.Def.reloadTime);
            var audio = GameContext.Audio;
            if (audio == null) return;
            if (_reloadSoundStage < 1f && progress > 0.12f)
            {
                _reloadSoundStage = 1f;
                audio.Play2D(audio.Clips.Get("mag_out"), 0.7f);
            }
            else if (_reloadSoundStage < 2f && progress > 0.58f)
            {
                _reloadSoundStage = 2f;
                audio.Play2D(audio.Clips.Get("mag_in"), 0.8f);
            }
            else if (_reloadSoundStage < 3f && progress > 0.85f)
            {
                _reloadSoundStage = 3f;
                audio.Play2D(audio.Clips.Get("bolt"), 0.7f);
            }
        }

        private static string ImpactSound(SurfaceDef surface)
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
            string id = surface != null ? surface.id : "concrete";
            switch (id)
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
