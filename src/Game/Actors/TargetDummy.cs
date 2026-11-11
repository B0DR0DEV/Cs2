using System.Collections.Generic;
using Godot;
using StrikeZone.Core;
using NVector3 = System.Numerics.Vector3;

namespace StrikeZone.Game
{
    public enum TargetMode
    {
        Static,
        Crouching,
        Strafing
    }

    /// <summary>
    /// Practice target. Every visible body part IS its hitbox (same box, same size).
    /// Strafing targets move with the same Source-style movement code as the player,
    /// including counter-strafing when they change direction.
    /// </summary>
    public partial class TargetDummy : Damageable
    {
        private static readonly System.Random Rng = new System.Random();

        private TargetMode _mode;
        private Node3D _body;
        private List<BodyPart> _parts;
        private readonly List<HitboxPart> _hitboxes = new List<HitboxPart>();
        private MeshInstance3D _helmet;
        private MeshInstance3D _vest;
        private MeshInstance3D _healthBar;
        private StandardMaterial3D _healthMat;
        private StandardMaterial3D _flashMat;
        private AnimatableBody3D _blocker;

        private Vector3 _spawn;
        private Vector3 _strafeA;
        private Vector3 _strafeB;
        private float _strafeYaw;
        private MovementState _move;
        private int _dir = 1;
        private int _phase;
        private float _phaseTimer;
        private float _flashTimer;
        private BodyPart _flashed;
        private Material _flashedOriginal;
        private float _deathTimer;
        private float _topple;

        public static TargetDummy Create(TargetSpawn spawn)
        {
            var t = new TargetDummy();
            t._mode = spawn.Mode;
            t._spawn = spawn.Position;
            t._strafeA = spawn.StrafeA;
            t._strafeB = spawn.StrafeB;
            t.Position = spawn.Position;
            // Model front is -Z; targets face the firing line at +Z.
            t.Rotation = new Vector3(0f, Mathf.Pi, 0f);
            t.Name = "Target_" + spawn.Mode;
            return t;
        }

        public override void _Ready()
        {
            var materials = GameContext.Materials;
            _move = MovementState.Create();
            Vector3 axis = _strafeB - _strafeA;
            _strafeYaw = Mathf.RadToDeg(Mathf.Atan2(axis.X, -axis.Z));

            _body = new Node3D { Name = "Body" };
            AddChild(_body);
            bool crouched = _mode == TargetMode.Crouching;
            _parts = HumanoidModel.Build(_body, Outfit.Target, materials, crouched);
            foreach (var p in _parts)
            {
                var hb = new HitboxPart
                {
                    Name = "Hitbox",
                    Target = this,
                    Group = p.Group,
                    CollisionLayer = Layers.Hitbox,
                    CollisionMask = 0,
                    Monitoring = false,
                    Monitorable = true
                };
                hb.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = p.Size } });
                p.Node.AddChild(hb);
                _hitboxes.Add(hb);
            }

            var gear = new Color(0.2f, 0.23f, 0.18f);
            _helmet = HumanoidModel.AddGear(_parts[0].Node, "Helmet", new Vector3(0.235f, 0.1f, 0.255f), new Vector3(0f, 0.09f, 0f), gear, materials);
            _vest = HumanoidModel.AddGear(_parts[1].Node, "Vest", new Vector3(0.45f, 0.58f, 0.27f), new Vector3(0f, -0.13f, 0f), gear, materials);

            float height = crouched ? 1.37f : 1.83f;
            _blocker = new AnimatableBody3D { CollisionLayer = Layers.BodyBlocker, CollisionMask = 0 };
            _blocker.AddChild(new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Radius = 0.3f, Height = height },
                Position = new Vector3(0f, height * 0.5f, 0f)
            });
            AddChild(_blocker);

            _healthMat = MaterialLibrary.NewUnlit(new Color(0.3f, 0.95f, 0.4f), null, true, false);
            _healthBar = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(0.5f, 0.05f) },
                MaterialOverride = _healthMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0f, crouched ? 1.5f : 1.95f, 0f)
            };
            AddChild(_healthBar);
            _flashMat = materials.Lit(new Color(1f, 0.25f, 0.2f), 0.6f);

            Respawn();
        }

        public void Respawn()
        {
            var s = GameContext.Settings;
            Health = MaxHealth;
            Armor = s == null || s.TargetArmor ? 100 : 0;
            HasHelmet = Armor > 0 && (s == null || s.TargetHelmet);
            _helmet.Visible = HasHelmet;
            _vest.Visible = Armor > 0;
            _topple = 0f;
            _body.Rotation = Vector3.Zero;
            _deathTimer = 0f;
            _move = MovementState.Create();
            _phase = 0;
            foreach (var hb in _hitboxes) hb.CollisionLayer = Layers.Hitbox;
            _blocker.CollisionLayer = Layers.BodyBlocker;
            _healthBar.Visible = true;
            if (_mode != TargetMode.Strafing) Position = _spawn;
            UpdateHealthBar();
        }

        protected override void OnDamaged(ref HitInfo hit)
        {
            PlayerMovement.ApplyTag(ref _move, hit.Weapon != null ? hit.Weapon.taggingStrength : 0.5f);
            _vest.Visible = Armor > 0;
            _helmet.Visible = HasHelmet;
            foreach (var p in _parts)
            {
                if (p.Group != hit.Group) continue;
                ClearFlash();
                _flashed = p;
                _flashedOriginal = p.Mesh.MaterialOverride;
                p.Mesh.MaterialOverride = _flashMat;
                _flashTimer = 0.09f;
                break;
            }
            UpdateHealthBar();
        }

        protected override void OnKilled(ref HitInfo hit)
        {
            // Off the hitbox layer so bullets pass through the falling body.
            foreach (var hb in _hitboxes) hb.CollisionLayer = 0;
            _blocker.CollisionLayer = 0;
            _healthBar.Visible = false;
            _deathTimer = 1.8f;
            if (GameContext.Session != null) GameContext.Session.Kills++;
        }

        /// <summary>True when the attacker is behind the target (knife backstab).</summary>
        public bool IsBehind(Vector3 attackerForward)
        {
            Vector3 f = -GlobalTransform.Basis.Z;
            f.Y = 0f;
            attackerForward.Y = 0f;
            if (f.LengthSquared() < 1e-6f || attackerForward.LengthSquared() < 1e-6f) return false;
            return f.Normalized().Dot(attackerForward.Normalized()) > 0.5f;
        }

        private void ClearFlash()
        {
            if (_flashed != null) _flashed.Mesh.MaterialOverride = _flashedOriginal;
            _flashed = null;
        }

        private void UpdateHealthBar()
        {
            float k = Mathf.Clamp(Health / (float)MaxHealth, 0f, 1f);
            _healthBar.Scale = new Vector3(Mathf.Max(k, 0.02f), 1f, 1f);
            _healthMat.AlbedoColor = new Color(0.95f, 0.2f, 0.15f).Lerp(new Color(0.3f, 0.95f, 0.4f), k);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0f) ClearFlash();
            }

            if (!IsAlive)
            {
                _topple = Mathf.MoveToward(_topple, Mathf.DegToRad(88f), Mathf.DegToRad(300f) * dt);
                _body.Rotation = new Vector3(_topple, 0f, 0f);
                _deathTimer -= dt;
                if (_deathTimer <= 0f) Respawn();
                return;
            }

            if (_mode == TargetMode.Strafing) UpdateStrafe(dt);
        }

        private void UpdateStrafe(float dt)
        {
            var settings = GameContext.Settings;
            if (settings != null && !settings.TargetsMove)
            {
                _move.Velocity = NVector3.Zero;
                return;
            }

            var cfg = GameContext.Database.Movement;
            var input = new MovementInput { YawDeg = _strafeYaw, WeaponMaxSpeed = 225f };
            Vector3 axis = _strafeB - _strafeA;
            float length = axis.Length();
            Vector3 dir = axis / Mathf.Max(length, 0.01f);
            float along = (Position - _strafeA).Dot(dir);

            switch (_phase)
            {
                case 0:
                    input.Forward = _dir;
                    bool pastEnd = (_dir > 0 && along >= length) || (_dir < 0 && along <= 0f);
                    bool randomStop = Rng.NextDouble() < 0.35 * dt;
                    if (pastEnd || randomStop)
                    {
                        _phase = 1;
                        if (pastEnd) _dir = -_dir;
                    }
                    break;
                case 1:
                    // Counter-strafe to stop quickly, as a player would.
                    Vector3 vel = Units.ToGodot(_move.Velocity);
                    input.Forward = _move.HorizontalSpeed > 30f ? -Mathf.Sign(vel.Dot(dir)) : 0f;
                    if (_move.HorizontalSpeed < 15f)
                    {
                        _phase = 2;
                        _phaseTimer = 0.15f + (float)Rng.NextDouble() * 0.75f;
                    }
                    break;
                default:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        _phase = 0;
                        if (Rng.NextDouble() < 0.3) _dir = -_dir;
                    }
                    break;
            }

            _move.OnGround = true;
            PlayerMovement.Simulate(ref _move, ref input, cfg, dt);
            Vector3 v = Units.ToGodot(_move.Velocity);
            v.Y = 0f;
            Vector3 next = Position + v * dt;
            float nextAlong = Mathf.Clamp((next - _strafeA).Dot(dir), -0.3f, length + 0.3f);
            Position = _strafeA + dir * nextAlong;
        }
    }
}
