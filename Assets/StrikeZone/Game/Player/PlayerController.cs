using StrikeZone.Core;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// First-person player. Movement maths lives in StrikeZone.Core (identical on the
    /// server later); this class feeds it input, resolves collisions with Unity's
    /// CharacterController and writes the clipped velocity back into the core state.
    /// Runs after the EventSystem so touch input from this frame is already collected.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float BaseVerticalFov = 68f;

        private CharacterController _cc;
        private Transform _cameraPivot;
        private Camera _camera;
        private InputRouter _input;
        private WeaponController _weapons;
        private GameDatabase _db;
        private MovementConfig _cfg;

        private MovementState _move;
        private float _yaw;
        private float _pitch;
        private Vector3 _spawnPosition;
        private float _spawnYaw;
        private float _respawnTimer;
        private float _stepDistance;
        private float _landPunch;
        private float _fovCurrent = BaseVerticalFov;

        public int Health = 100;
        public int Armor = 100;
        public bool HasHelmet = true;

        public Camera Camera
        {
            get { return _camera; }
        }

        public Transform CameraTransform
        {
            get { return _cameraPivot; }
        }

        public WeaponController Weapons
        {
            get { return _weapons; }
        }

        public float Yaw
        {
            get { return _yaw; }
        }

        public float Pitch
        {
            get { return _pitch; }
        }

        public bool IsGrounded
        {
            get { return _move.OnGround; }
        }

        public bool IsCrouched
        {
            get { return _move.IsCrouched; }
        }

        /// <summary>Horizontal speed in game units per second (the number players compare with weapon speeds).</summary>
        public float HorizontalSpeed
        {
            get { return _move.HorizontalSpeed; }
        }

        public Vector3 VelocityMeters
        {
            get { return Units.ToUnity(_move.Velocity); }
        }

        public bool IsAlive
        {
            get { return Health > 0; }
        }

        public static PlayerController Create(Transform parent, GameDatabase db, Vector3 spawn, float yaw, InputRouter input)
        {
            var go = new GameObject("Player");
            go.layer = Layers.Player;
            go.transform.SetParent(parent, false);
            go.transform.position = spawn;
            var pc = go.AddComponent<PlayerController>();
            pc.Initialize(db, spawn, yaw, input);
            return pc;
        }

        private void Initialize(GameDatabase db, Vector3 spawn, float yaw, InputRouter input)
        {
            _db = db;
            _cfg = db.Movement;
            _input = input;
            _spawnPosition = spawn;
            _spawnYaw = yaw;
            _yaw = yaw;
            _move = MovementState.Create();

            _cc = gameObject.AddComponent<CharacterController>();
            _cc.radius = _cfg.radius * Units.ToMeters;
            _cc.height = _cfg.standHeight * Units.ToMeters;
            _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            _cc.stepOffset = _cfg.stepHeight * Units.ToMeters;
            _cc.slopeLimit = 45f;
            _cc.skinWidth = 0.03f;
            _cc.minMoveDistance = 0f;

            _cameraPivot = new GameObject("Eye").transform;
            _cameraPivot.SetParent(transform, false);
            _cameraPivot.localPosition = new Vector3(0f, _cfg.standEyeHeight * Units.ToMeters, 0f);
            _camera = _cameraPivot.gameObject.AddComponent<Camera>();
            _camera.gameObject.tag = "MainCamera";
            _camera.nearClipPlane = 0.02f;
            _camera.farClipPlane = 400f;
            _camera.fieldOfView = BaseVerticalFov;
            _cameraPivot.gameObject.AddComponent<AudioListener>();

            _weapons = gameObject.AddComponent<WeaponController>();
            _weapons.Initialize(db, this);
        }

        public void SetMaxHealthState()
        {
            Health = 100;
            Armor = 100;
            HasHelmet = true;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            if (!IsAlive)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f) Respawn();
                return;
            }

            var settings = GameContext.Settings;
            WeaponRuntime weapon = _weapons.Current;

            // ------------------------------------------------------------ look
            float zoom = weapon != null && weapon.IsScoped ? weapon.ScopeFov / BaseVerticalFov : 1f;
            float scopedMul = weapon != null && weapon.IsScoped ? settings.ScopedSensitivityMultiplier * zoom : 1f;
            float touchDegPerPixel = 120f / Mathf.Max(Screen.height, 1) * settings.TouchSensitivity * scopedMul;
            float mouseDeg = settings.MouseSensitivity * scopedMul;
            PlayerCommand cmd = _input.Gather(touchDegPerPixel, mouseDeg, settings.InvertY, settings.LookAcceleration, settings.CrouchToggle);

            _yaw = Mathf.Repeat(_yaw + cmd.LookYaw, 360f);
            _pitch = Mathf.Clamp(_pitch + cmd.LookPitch, -89f, 89f);

            // ------------------------------------------------------------ movement
            bool wantCrouch = cmd.CrouchHeld;
            if (!wantCrouch && _move.CrouchAmount > 0f && !HasHeadroomToStand()) wantCrouch = true;

            bool touchingLadder = TouchingLadder();
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
                WeaponMaxSpeed = weapon != null ? weapon.CurrentMaxSpeed : 250f,
                OnLadder = onLadder
            };

            bool wasGrounded = _move.OnGround;
            float oldHeight = _cc.height;
            PlayerMovement.Simulate(ref _move, ref input, _cfg, dt);
            if (cmd.JumpPressed && wasGrounded && !_move.OnGround && GameContext.Audio != null)
                GameContext.Audio.Play2D(GameContext.Audio.Clips.Get("jump"), 0.35f);

            UpdateHull(oldHeight, wasGrounded);

            Vector3 displacement = Units.ToUnity(_move.Velocity) * dt;
            if (_move.OnGround && !onLadder) displacement.y -= 4f * dt; // ground snap
            CollisionFlags flags = _cc.Move(displacement);

            bool grounded = _cc.isGrounded || (flags & CollisionFlags.Below) != 0;
            if (grounded && !onLadder)
            {
                if (!wasGrounded || _move.Velocity.Y < 0f)
                {
                    float fallSpeed = -_move.Velocity.Y;
                    if (!wasGrounded) OnLanded(fallSpeed);
                }
                if (_move.Velocity.Y <= 0f)
                {
                    _move.OnGround = true;
                    var v = _move.Velocity;
                    v.Y = 0f;
                    _move.Velocity = v;
                }
            }
            else if (!onLadder)
            {
                _move.OnGround = false;
            }

            if ((flags & CollisionFlags.Above) != 0 && _move.Velocity.Y > 0f)
            {
                var v = _move.Velocity;
                v.Y = 0f;
                _move.Velocity = v;
            }
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Keep only the velocity that actually happened, so we never "store" speed in a wall.
                Vector3 actual = _cc.velocity;
                var v = _move.Velocity;
                float ax = actual.x * Units.ToUnits;
                float az = actual.z * Units.ToUnits;
                if (Mathf.Abs(ax) < Mathf.Abs(v.X)) v.X = ax;
                if (Mathf.Abs(az) < Mathf.Abs(v.Z)) v.Z = az;
                _move.Velocity = v;
            }

            if (transform.position.y < -30f) Kill();

            // ------------------------------------------------------------ camera
            float eye = Mathf.Lerp(_cfg.standEyeHeight, _cfg.crouchEyeHeight, _move.CrouchAmount) * Units.ToMeters;
            _landPunch = Mathf.MoveTowards(_landPunch, 0f, dt * 0.4f);
            _cameraPivot.localPosition = new Vector3(0f, eye - _landPunch, 0f);

            float recoilPitch = 0f, recoilYaw = 0f;
            if (weapon != null)
            {
                weapon.GetRecoil(out recoilPitch, out recoilYaw);
                recoilPitch *= weapon.Def.cameraRecoilFollow;
                recoilYaw *= weapon.Def.cameraRecoilFollow;
            }
            _cameraPivot.rotation = Units.ViewRotation(_pitch + recoilPitch, _yaw + recoilYaw);

            float targetFov = weapon != null && weapon.IsScoped ? weapon.ScopeFov : BaseVerticalFov;
            _fovCurrent = Mathf.MoveTowards(_fovCurrent, targetFov, 600f * dt);
            _camera.fieldOfView = _fovCurrent;

            // ------------------------------------------------------------ weapons & sounds
            var accuracy = new AccuracyContext
            {
                HorizontalSpeed = _move.HorizontalSpeed,
                OnGround = _move.OnGround || onLadder,
                Crouched = _move.IsCrouched,
                WeaponMaxSpeed = weapon != null ? weapon.CurrentMaxSpeed : 250f,
                AccurateSpeedFraction = _cfg.accurateSpeedFraction
            };
            _weapons.Tick(ref cmd, ref accuracy, dt);

            UpdateFootsteps(dt, cmd.Walk, input.WeaponMaxSpeed);
        }

        private void UpdateHull(float oldHeight, bool wasGrounded)
        {
            float height = Mathf.Lerp(_cfg.standHeight, _cfg.crouchHeight, _move.CrouchAmount) * Units.ToMeters;
            if (Mathf.Abs(height - oldHeight) < 1e-5f) return;
            float delta = oldHeight - height;
            _cc.height = height;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);
            if (!wasGrounded)
            {
                // In the air the hull shrinks from the bottom (legs tuck in): crouch-jumping
                // gains ~9 units of clearance, exactly like in Source.
                _cc.Move(Vector3.up * delta * 0.5f);
            }
        }

        private bool HasHeadroomToStand()
        {
            float standH = _cfg.standHeight * Units.ToMeters;
            float r = _cc.radius * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * (r + 0.05f);
            Vector3 top = transform.position + Vector3.up * (standH - r);
            return !Physics.CheckCapsule(bottom, top, r, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        }

        private bool TouchingLadder()
        {
            float r = _cc.radius;
            Vector3 bottom = transform.position + Vector3.up * r;
            Vector3 top = transform.position + Vector3.up * (_cc.height - r);
            return Physics.CheckCapsule(bottom, top, r + 0.05f, Layers.LadderMask, QueryTriggerInteraction.Collide);
        }

        private void OnLanded(float fallSpeedUnits)
        {
            int damage = PlayerMovement.FallDamage(fallSpeedUnits, _cfg);
            if (fallSpeedUnits > 250f)
            {
                _landPunch = Mathf.Clamp(fallSpeedUnits / 1000f * 0.08f, 0f, 0.08f);
                if (GameContext.Audio != null)
                    GameContext.Audio.Play2D(GameContext.Audio.Clips.Get("land"), Mathf.Clamp01(fallSpeedUnits / 600f) * 0.8f);
            }
            if (damage > 0)
            {
                Health = Mathf.Max(0, Health - damage);
                if (GameContext.Audio != null) GameContext.Audio.Play2D(GameContext.Audio.Clips.Get("hurt"), 0.9f);
                if (GameContext.Hud != null) GameContext.Hud.OnPlayerDamaged(damage, "падение");
                if (GameContext.Settings != null && GameContext.Settings.Vibration) Haptics.Hit();
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
            // Walking and slow movement are silent; running is audible (that is the whole point of shift-walking).
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
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out hit, 0.6f, 1 << Layers.World, QueryTriggerInteraction.Ignore))
            {
                var tag = hit.collider.GetComponent<SurfaceTag>();
                if (tag != null) footstep = _db.GetSurface(tag.SurfaceId).footstep;
            }
            if (GameContext.Audio != null)
                GameContext.Audio.Play2D(GameContext.Audio.Clips.GetVariant("step_" + footstep), 0.32f, Random.Range(0.93f, 1.07f));
        }

        public void Kill()
        {
            if (!IsAlive && _respawnTimer > 0f) return;
            Health = 0;
            _respawnTimer = 2f;
            if (GameContext.Hud != null) GameContext.Hud.ShowMessage("Вы погибли — возрождение через 2 с", 2f);
        }

        public void Respawn()
        {
            _cc.enabled = false;
            transform.position = _spawnPosition;
            _cc.enabled = true;
            _yaw = _spawnYaw;
            _pitch = 0f;
            _move = MovementState.Create();
            SetMaxHealthState();
            _weapons.RefillAll();
        }
    }
}
