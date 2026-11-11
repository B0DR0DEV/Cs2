using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Merges touch controls and keyboard/mouse. Look input is consumed every rendered
    /// frame (smooth aiming at any refresh rate); everything else is sampled at the
    /// physics tick, with button presses latched until the tick consumes them.
    /// </summary>
    public partial class InputRouter : Node
    {
        public TouchControls Touch;
        public bool MenuOpen;

        private Vector2 _mouseLook;
        private bool _fireLatched, _altLatched, _jumpLatched, _reloadLatched, _scopeLatched, _inspectLatched;
        private bool _nextLatched, _lastLatched;
        private int _slotLatched;
        private bool _crouchToggled;
        private bool _walkToggled;
        private float _accelSpeed;

        public bool TouchVisible
        {
            get { return Touch != null && Touch.Visible; }
            set
            {
                if (Touch == null) return;
                Touch.Visible = value;
                if (!value) Touch.ReleaseAll();
            }
        }

        public void SetMenuOpen(bool open)
        {
            MenuOpen = open;
            if (Touch != null)
            {
                Touch.Enabled = !open;
                if (open) Touch.ReleaseAll();
            }
            if (open) Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        private static bool IsMobile
        {
            get { return OS.HasFeature("mobile"); }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (MenuOpen) return;

            if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                _mouseLook += motion.Relative;
                return;
            }

            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (!IsMobile && Input.MouseMode != Input.MouseModeEnum.Captured && mb.ButtonIndex == MouseButton.Left)
                {
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                    return;
                }
                if (Input.MouseMode == Input.MouseModeEnum.Captured)
                {
                    if (mb.ButtonIndex == MouseButton.Left) _fireLatched = true;
                    if (mb.ButtonIndex == MouseButton.Right) { _scopeLatched = true; _altLatched = true; }
                    if (mb.ButtonIndex == MouseButton.WheelDown) _nextLatched = true;
                }
                return;
            }

            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                switch (key.Keycode)
                {
                    case Key.Space: _jumpLatched = true; break;
                    case Key.R: _reloadLatched = true; break;
                    case Key.F: _inspectLatched = true; break;
                    case Key.Key1: _slotLatched = 1; break;
                    case Key.Key2: _slotLatched = 2; break;
                    case Key.Key3: _slotLatched = 3; break;
                    case Key.Q: _lastLatched = true; break;
                }
            }
        }

        /// <summary>Look delta in degrees since the last call.</summary>
        public Vector2 ConsumeLook(float touchDegPerPixel, float mouseDegPerPixel, bool invertY, bool acceleration, float dt)
        {
            Vector2 result = Vector2.Zero;
            if (Touch != null && TouchVisible && !MenuOpen)
            {
                Vector2 px = Touch.ConsumeLook();
                float scale = touchDegPerPixel;
                if (acceleration && dt > 0f)
                {
                    float speed = px.Length() / dt / 1080f;
                    _accelSpeed = Mathf.Lerp(_accelSpeed, speed, 0.5f);
                    scale *= 1f + Mathf.Clamp(_accelSpeed - 0.5f, 0f, 3f) * 0.35f;
                }
                result += new Vector2(px.X, -px.Y) * scale;
            }
            else
            {
                Touch?.ConsumeLook();
            }
            result += new Vector2(_mouseLook.X, -_mouseLook.Y) * mouseDegPerPixel;
            _mouseLook = Vector2.Zero;
            if (invertY) result.Y = -result.Y;
            return result;
        }

        public PlayerCommand SampleTick(bool crouchToggle)
        {
            var cmd = new PlayerCommand();
            if (MenuOpen)
            {
                ClearLatches();
                return cmd;
            }

            if (Touch != null && TouchVisible)
            {
                Vector2 stick = Touch.StickValue;
                cmd.Forward = stick.Y;
                cmd.Side = stick.X;
                float m = stick.Length();
                if (Touch.Consume("walk")) _walkToggled = !_walkToggled;
                cmd.Walk = (m > 0.08f && m < TouchControls.WalkThreshold) || _walkToggled;
                cmd.FireHeld = Touch.Held("fireR") || Touch.Held("fireL");
                cmd.FirePressed = Touch.Consume("fireR") | Touch.Consume("fireL");
                cmd.JumpPressed = Touch.Consume("jump");
                cmd.ReloadPressed = Touch.Consume("reload");
                cmd.ScopePressed = Touch.Consume("scope");
                cmd.InspectPressed = Touch.Consume("inspect");
                if (Touch.Consume("slot1")) cmd.SelectSlot = 1;
                if (Touch.Consume("slot2")) cmd.SelectSlot = 2;
                if (Touch.Consume("slot3")) cmd.SelectSlot = 3;
                if (crouchToggle)
                {
                    if (Touch.Consume("crouch")) _crouchToggled = !_crouchToggled;
                    cmd.CrouchHeld = _crouchToggled;
                }
                else
                {
                    Touch.Consume("crouch");
                    cmd.CrouchHeld = Touch.Held("crouch");
                    _crouchToggled = false;
                }
            }

            // Keyboard (desktop / editor).
            float f = 0f, s = 0f;
            if (Input.IsKeyPressed(Key.W)) f += 1f;
            if (Input.IsKeyPressed(Key.S)) f -= 1f;
            if (Input.IsKeyPressed(Key.D)) s += 1f;
            if (Input.IsKeyPressed(Key.A)) s -= 1f;
            if (f != 0f || s != 0f)
            {
                cmd.Forward = f;
                cmd.Side = s;
                cmd.Walk |= Input.IsKeyPressed(Key.Shift);
            }
            if (Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.C)) cmd.CrouchHeld = true;
            if (Input.MouseMode == Input.MouseModeEnum.Captured && Input.IsMouseButtonPressed(MouseButton.Left)) cmd.FireHeld = true;

            cmd.FirePressed |= _fireLatched;
            cmd.AltFirePressed |= _altLatched || cmd.ScopePressed;
            cmd.JumpPressed |= _jumpLatched;
            cmd.ReloadPressed |= _reloadLatched;
            cmd.ScopePressed |= _scopeLatched;
            cmd.InspectPressed |= _inspectLatched;
            cmd.NextWeapon = _nextLatched;
            cmd.LastWeapon = _lastLatched;
            if (_slotLatched > 0) cmd.SelectSlot = _slotLatched;
            ClearLatches();

            if (GameContext.SmokeTest)
            {
                // Exercise movement, firing and reload paths in CI.
                ulong t = Engine.GetPhysicsFrames();
                cmd.Forward = 1f;
                cmd.Side = (t / 64) % 2 == 0 ? 1f : -1f;
                cmd.FireHeld = true;
                cmd.FirePressed = t % 16 == 0;
                cmd.JumpPressed = t % 90 == 0;
                cmd.ReloadPressed = t % 200 == 0;
            }
            return cmd;
        }

        private void ClearLatches()
        {
            _fireLatched = _altLatched = _jumpLatched = _reloadLatched = _scopeLatched = _inspectLatched = false;
            _nextLatched = _lastLatched = false;
            _slotLatched = 0;
        }
    }
}
