using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// First-person player. Movement runs at the 64 Hz physics tick using the core's
    /// Source-style code (identical on the future server); Godot's CharacterBody3D resolves
    /// collisions and the slid velocity is written back into the core state. The camera is a
    /// separate node, interpolated between ticks and rotated every rendered frame, so aiming
    /// stays smooth at 90/120 Hz.
    /// </summary>
    public partial class PlayerController : CharacterBody3D
    {
        public const float BaseFov = 68f;

        private GameDatabase _db;
        private MovementConfig _cfg;
        private InputRouter _input;
        private WeaponController _weapons;
        private ViewModel _viewModel;
        private Camera3D _camera;
        private CollisionShape3D _shape;
        private CapsuleShape3D _capsule;
        private Area3D _ladderSensor;

        private MovementState _move;
        private float _yaw;
        private float _pitch;
        private Vector3 _spawn;
        private float _spawnYaw;
        private float _respawnTimer;
        private float _stepDistance;
        private float _landPunch;
        private float _fov = BaseFov;
        private Vector3 _prevTickPos;
        private Vector3 _currTickPos;

        public int Health = 100;
        public int Armor = 100;
        public bool HasHelmet = true;

        /// <summary>Simulation time in seconds (advances with physics ticks).</summary>
        public float MatchTime { get; private set; }

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public bool Grounded => _move.OnGround;
        public bool IsCrouched => _move.IsCrouched;
        public float HorizontalSpeed => _move.HorizontalSpeed;
        public Vector3 VelocityMeters => Units.ToGodot(_move.Velocity);
        public bool IsAlive => Health > 0;
        public Camera3D Camera => _camera;
        public WeaponController Weapons => _weapons;

        public Basis ViewBasisNow => Units.ViewBasis(_pitch, _yaw);

        /// <summary>Eye position at the current physics tick (where bullets start).</summary>
        public Vector3 EyePosition => GlobalPosition + new Vector3(0f, EyeHeight, 0f);

        private float EyeHeight => Mathf.Lerp(_cfg.standEyeHeight, _cfg.crouchEyeHeight, _move.CrouchAmount) * Units.ToMeters;

        public void Setup(GameDatabase db, InputRouter input, Vector3 spawn, float yaw)
        {
            _db = db;
            _cfg = db.Movement;
            _input = input;
            _spawn = spawn;
            _spawnYaw = yaw;
            _yaw = yaw;
            Position = spawn;
        }

        public override void _Ready()
        {
            _move = MovementState.Create();
            CollisionLayer = Layers.Player;
            CollisionMask = Layers.SolidMask;
            FloorSnapLength = 0.3f;
            FloorMaxAngle = Mathf.DegToRad(46f);
            FloorStopOnSlope = true;
            FloorConstantSpeed = true;
            SafeMargin = 0.01f;

            _capsule = new CapsuleShape3D { Radius = _cfg.radius * Units.ToMeters, Height = _cfg.standHeight * Units.ToMeters };
            _shape = new CollisionShape3D { Shape = _capsule, Position = new Vector3(0f, _capsule.Height * 0.5f, 0f) };
            AddChild(_shape);

            _ladderSensor = new Area3D { CollisionLayer = 0, CollisionMask = Layers.Ladder, Monitoring = true, Monitorable = false };
            _ladderSensor.AddChild(new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Radius = _capsule.Radius + 0.05f, Height = _capsule.Height },
                Position = new Vector3(0f, _capsule.Height * 0.5f, 0f)
            });
            AddChild(_ladderSensor);

            // Camera lives outside the body so it can be interpolated between physics ticks.
            _camera = new Camera3D { Fov = BaseFov, Near = 0.02f, Far = 400f, Current = true };
            GetParent().CallDeferred(Node.MethodName.AddChild, _camera);
            _viewModel = new ViewModel();
            _camera.AddChild(_viewModel);

            _weapons = new WeaponController { Name = "Weapons" };
            AddChild(_weapons);
            _prevTickPos = _currTickPos = GlobalPosition;
            // Deferred so the camera (and its view model) is in the tree first.
            Callable.From(InitWeapons).CallDeferred();
        }

        private void InitWeapons()
        {
            _weapons.Setup(_db, this, _viewModel);
            GameContext.Audio?.SetListener(_camera);
        }

        // ------------------------------------------------------------------ frame: look + camera

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var s = GameContext.Settings;
            var w = _weapons.Current;

            float zoom = w != null && w.IsScoped ? w.ScopeFov / BaseFov : 1f;
            float scopedMul = w != null && w.IsScoped ? s.ScopedSensitivityMultiplier * zoom : 1f;
            float touchDegPerPixel = 120f / 1080f * s.TouchSensitivity * scopedMul;
            Vector2 look = IsAlive ? _input.ConsumeLook(touchDegPerPixel, s.MouseSensitivity * scopedMul, s.InvertY, s.LookAcceleration, dt) : Vector2.Zero;
            _yaw = Mathf.PosMod(_yaw + look.X, 360f);
            _pitch = Mathf.Clamp(_pitch + look.Y, -89f, 89f);

            // Interpolated body position between the last two ticks.
            float alpha = (float)Engine.GetPhysicsInterpolationFraction();
            Vector3 body = _prevTickPos.Lerp(_currTickPos, alpha);
            _landPunch = Mathf.MoveToward(_landPunch, 0f, dt * 0.4f);
            if (_camera.IsInsideTree())
            {
                float recoilPitch = 0f, recoilYaw = 0f;
                if (w != null)
                {
                    w.GetRecoil(out recoilPitch, out recoilYaw);
                    recoilPitch *= w.Def.cameraRecoilFollow;
                    recoilYaw *= w.Def.cameraRecoilFollow;
                }
                _camera.GlobalTransform = new Transform3D(Units.ViewBasis(_pitch + recoilPitch, _yaw + recoilYaw),
                    body + new Vector3(0f, EyeHeight - _landPunch, 0f));
                float targetFov = w != null && w.IsScoped ? w.ScopeFov : BaseFov;
                _fov = Mathf.MoveToward(_fov, targetFov, 600f * dt);
                _camera.Fov = _fov;
            }

            _weapons.AnimateViewModel(dt, look.X, look.Y);
        }

        // ------------------------------------------------------------------ tick: movement + weapons

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            MatchTime += dt;
            if (GameContext.Session != null) GameContext.Session.Seconds += dt;
            _prevTickPos = _currTickPos;

            if (!IsAlive)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f) Respawn();
                _currTickPos = GlobalPosition;
                return;
            }

            var s = GameContext.Settings;
            PlayerCommand cmd = _input.SampleTick(s.CrouchToggle);
            var w = _weapons.Current;

            bool wantCrouch = cmd.CrouchHeld;
            if (!wantCrouch && _move.CrouchAmount > 0f && !HasHeadroom()) wantCrouch = true;
            bool touchingLadder = _ladderSensor.HasOverlappingAreas();
            bool onLadder = touchingLadder && !(_move.OnGround && cmd.Forward <= 0.01f);

            var input = new MovementInput
            {
                Forward = cmd.Forward,
                Side = cmd.Side,
                YawDeg = _yaw,
                PitchDeg = _pitch,
                Jump = cmd.JumpPressed,
                Crouch = wantCrouch,
                Walk = cmd.Walk,
                WeaponMaxSpeed = w != null ? w.CurrentMaxSpeed : 250f,
                OnLadder = onLadder
            };

            bool wasGrounded = _move.OnGround;
            float oldHeight = _capsule.Height;
            PlayerMovement.Simulate(ref _move, ref input, _cfg, dt);
            if (cmd.JumpPressed && wasGrounded && !_move.OnGround)
                GameContext.Audio?.Play2D(GameContext.Sounds.Get("jump"), 0.35f);
            UpdateHull(oldHeight, wasGrounded);

            float fallSpeedBefore = -_move.Velocity.Y;
            Velocity = Units.ToGodot(_move.Velocity);
            FloorSnapLength = _move.OnGround && !onLadder ? 0.3f : 0f;
            MoveAndSlide();

            // Write the collision-resolved velocity back into the core state.
            Vector3 slid = Velocity;
            var core = Units.ToCore(slid);
            bool grounded = IsOnFloor() && !onLadder;
            if (grounded)
            {
                if (!wasGrounded) OnLanded(fallSpeedBefore);
                core.Y = 0f;
                _move.OnGround = true;
            }
            else if (!onLadder)
            {
                _move.OnGround = false;
            }
            if (IsOnCeiling() && core.Y > 0f) core.Y = 0f;
            _move.Velocity = core;

            if (GlobalPosition.Y < -30f) Kill();
            _currTickPos = GlobalPosition;

            var accuracy = new AccuracyContext
            {
                HorizontalSpeed = _move.HorizontalSpeed,
                OnGround = _move.OnGround || onLadder,
                Crouched = _move.IsCrouched,
                WeaponMaxSpeed = input.WeaponMaxSpeed,
                AccurateSpeedFraction = _cfg.accurateSpeedFraction
            };
            _weapons.Tick(ref cmd, ref accuracy, dt);
            UpdateFootsteps(dt, cmd.Walk, input.WeaponMaxSpeed);
        }

        private void UpdateHull(float oldHeight, bool wasGrounded)
        {
            float height = Mathf.Lerp(_cfg.standHeight, _cfg.crouchHeight, _move.CrouchAmount) * Units.ToMeters;
            if (Mathf.Abs(height - oldHeight) < 1e-5f) return;
            _capsule.Height = height;
            _shape.Position = new Vector3(0f, height * 0.5f, 0f);
            if (!wasGrounded)
            {
                // In the air the hull shrinks from the bottom (legs tuck in): crouch-jumping
                // gains about 9 units of clearance, as in Source.
                MoveAndCollide(Vector3.Up * (oldHeight - height) * 0.5f);
            }
        }

        private bool HasHeadroom()
        {
            float standH = _cfg.standHeight * Units.ToMeters;
            var shape = new CapsuleShape3D { Radius = _capsule.Radius * 0.95f, Height = standH - 0.06f };
            var q = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, GlobalPosition + new Vector3(0f, standH * 0.5f + 0.04f, 0f)),
                CollisionMask = Layers.SolidMask,
                CollideWithAreas = false
            };
            return GetWorld3D().DirectSpaceState.IntersectShape(q, 1).Count == 0;
        }

        private void OnLanded(float fallSpeedUnits)
        {
            int damage = PlayerMovement.FallDamage(fallSpeedUnits, _cfg);
            if (fallSpeedUnits > 250f)
            {
                _landPunch = Mathf.Clamp(fallSpeedUnits / 1000f * 0.08f, 0f, 0.08f);
                GameContext.Audio?.Play2D(GameContext.Sounds.Get("land"), Mathf.Clamp(fallSpeedUnits / 600f, 0f, 1f) * 0.8f);
            }
            if (damage > 0)
            {
                Health = Mathf.Max(0, Health - damage);
                GameContext.Audio?.Play2D(GameContext.Sounds.Get("hurt"), 0.9f);
                GameContext.Hud?.OnPlayerDamaged(damage, "падение");
                if (GameContext.Settings.Vibration) Input.VibrateHandheld(60, 0.8f);
                if (Health <= 0) Kill();
            }
        }

        private void UpdateFootsteps(float dt, bool walking, float weaponMaxSpeed)
        {
            if (!_move.OnGround)
            {
                _stepDistance = 0f;
                return;
            }
            float speed = _move.HorizontalSpeed;
            // Walking, crouching and slow movement are silent — that is the point of shift-walking.
            bool audible = !walking && !_move.IsCrouched && speed > weaponMaxSpeed * 0.55f;
            if (!audible)
            {
                _stepDistance = 0f;
                return;
            }
            _stepDistance += speed * Units.ToMeters * dt;
            if (_stepDistance < 1.55f) return;
            _stepDistance = 0f;

            string footstep = "concrete";
            var q = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * 0.2f, GlobalPosition + Vector3.Down * 0.4f, Layers.World);
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
            if (hit.Count > 0)
            {
                string surface = Ballistics.SurfaceOf(hit["collider"].AsGodotObject());
                footstep = _db.GetSurface(surface).footstep;
            }
            GameContext.Audio?.Play2D(GameContext.Sounds.Pick("step_" + footstep), 0.32f, (float)GD.RandRange(0.93, 1.07));
        }

        public void Kill()
        {
            if (!IsAlive && _respawnTimer > 0f) return;
            Health = 0;
            _respawnTimer = 2f;
            GameContext.Hud?.ShowMessage("Вы погибли — возрождение через 2 с", 2f);
        }

        public void Respawn()
        {
            GlobalPosition = _spawn;
            Velocity = Vector3.Zero;
            _prevTickPos = _currTickPos = _spawn;
            _yaw = _spawnYaw;
            _pitch = 0f;
            _move = MovementState.Create();
            Health = 100;
            Armor = 100;
            HasHelmet = true;
            _weapons.RefillAll();
        }

        public override void _ExitTree()
        {
            if (_camera != null && IsInstanceValid(_camera)) _camera.QueueFree();
        }
    }
}
