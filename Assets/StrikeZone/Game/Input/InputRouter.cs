using UnityEngine;
using UnityEngine.EventSystems;

namespace StrikeZone.Game
{
    /// <summary>
    /// Merges touch controls and keyboard/mouse into one PlayerCommand per frame.
    /// Touch controls are a fixed layout in phase 1; phase 4 replaces the layout with the
    /// user-editable HUD (drag, resize, opacity, saved presets) on top of the same widgets.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        public VirtualJoystick Joystick;
        public TouchLookArea LookArea;
        public TouchButton FireRight;
        public TouchButton FireLeft;
        public TouchButton Jump;
        public TouchButton Crouch;
        public TouchButton Reload;
        public TouchButton Scope;
        public TouchButton Inspect;
        public TouchButton Slot1;
        public TouchButton Slot2;
        public TouchButton Slot3;
        public TouchButton Walk;

        public GameObject TouchRoot;
        public bool MenuOpen;

        private bool _crouchToggled;
        private bool _walkToggled;
        private float _lastTouchLookSpeed;

        public bool TouchVisible
        {
            get { return TouchRoot != null && TouchRoot.activeSelf; }
            set { if (TouchRoot != null) TouchRoot.SetActive(value); }
        }

        public bool CursorLocked
        {
            get { return Cursor.lockState == CursorLockMode.Locked; }
        }

        /// <param name="touchDegreesPerPixel">Look conversion for swipes (already includes sensitivity and zoom).</param>
        /// <param name="mouseDegreesPerUnit">Look conversion for mouse.</param>
        public PlayerCommand Gather(float touchDegreesPerPixel, float mouseDegreesPerUnit, bool invertY, bool lookAcceleration, bool crouchToggle)
        {
            var cmd = new PlayerCommand { SelectSlot = 0 };
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

            // ---------------------------------------------------------------- touch
            if (TouchVisible && !MenuOpen)
            {
                Vector2 stick = Joystick != null ? Joystick.Value : Vector2.zero;
                cmd.Forward = stick.y;
                cmd.Side = stick.x;
                cmd.Walk = (Joystick != null && Joystick.IsWalking) || _walkToggled;

                Vector2 look = LookArea != null ? LookArea.Consume() : Vector2.zero;
                float scale = touchDegreesPerPixel;
                if (lookAcceleration)
                {
                    // Optional acceleration: faster swipes turn proportionally further.
                    float speed = look.magnitude / dt / Mathf.Max(Screen.height, 1);
                    _lastTouchLookSpeed = Mathf.Lerp(_lastTouchLookSpeed, speed, 0.5f);
                    scale *= 1f + Mathf.Clamp(_lastTouchLookSpeed - 0.5f, 0f, 3f) * 0.35f;
                }
                cmd.LookYaw += look.x * scale;
                cmd.LookPitch += look.y * scale * (invertY ? -1f : 1f);

                cmd.FireHeld = (FireRight != null && FireRight.Held) || (FireLeft != null && FireLeft.Held);
                bool fr = FireRight != null && FireRight.ConsumePress();
                bool fl = FireLeft != null && FireLeft.ConsumePress();
                cmd.FirePressed = fr || fl;
                cmd.JumpPressed = Jump != null && Jump.ConsumePress();
                cmd.ReloadPressed = Reload != null && Reload.ConsumePress();
                cmd.ScopePressed = Scope != null && Scope.ConsumePress();
                cmd.InspectPressed = Inspect != null && Inspect.ConsumePress();
                if (Slot1 != null && Slot1.ConsumePress()) cmd.SelectSlot = 1;
                if (Slot2 != null && Slot2.ConsumePress()) cmd.SelectSlot = 2;
                if (Slot3 != null && Slot3.ConsumePress()) cmd.SelectSlot = 3;
                if (Walk != null && Walk.ConsumePress()) _walkToggled = !_walkToggled;

                bool crouchHeld = Crouch != null && Crouch.Held;
                if (crouchToggle)
                {
                    if (Crouch != null && Crouch.ConsumePress()) _crouchToggled = !_crouchToggled;
                    cmd.CrouchHeld = _crouchToggled;
                }
                else
                {
                    if (Crouch != null) Crouch.ConsumePress();
                    cmd.CrouchHeld = crouchHeld;
                    _crouchToggled = false;
                }
            }
            else if (LookArea != null)
            {
                LookArea.Consume();
            }

            // ---------------------------------------------------------------- keyboard & mouse
            if (!MenuOpen)
            {
                float f = 0f, s = 0f;
                if (Input.GetKey(KeyCode.W)) f += 1f;
                if (Input.GetKey(KeyCode.S)) f -= 1f;
                if (Input.GetKey(KeyCode.D)) s += 1f;
                if (Input.GetKey(KeyCode.A)) s -= 1f;
                if (f != 0f || s != 0f)
                {
                    cmd.Forward = f;
                    cmd.Side = s;
                    cmd.Walk |= Input.GetKey(KeyCode.LeftShift);
                }

                if (Input.GetKeyDown(KeyCode.Space)) cmd.JumpPressed = true;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) cmd.CrouchHeld = true;
                if (Input.GetKeyDown(KeyCode.R)) cmd.ReloadPressed = true;
                if (Input.GetKeyDown(KeyCode.F)) cmd.InspectPressed = true;
                if (Input.GetKeyDown(KeyCode.Alpha1)) cmd.SelectSlot = 1;
                if (Input.GetKeyDown(KeyCode.Alpha2)) cmd.SelectSlot = 2;
                if (Input.GetKeyDown(KeyCode.Alpha3)) cmd.SelectSlot = 3;
                if (Input.GetKeyDown(KeyCode.Q)) cmd.LastWeapon = true;
                if (Input.mouseScrollDelta.y < -0.1f) cmd.NextWeapon = true;

                if (CursorLocked)
                {
                    cmd.FireHeld |= Input.GetMouseButton(0);
                    cmd.FirePressed |= Input.GetMouseButtonDown(0);
                    if (Input.GetMouseButtonDown(1))
                    {
                        cmd.ScopePressed = true;
                        cmd.AltFirePressed = true;
                    }
                    cmd.LookYaw += ReadAxis("Mouse X") * mouseDegreesPerUnit;
                    cmd.LookPitch += ReadAxis("Mouse Y") * mouseDegreesPerUnit * (invertY ? -1f : 1f);
                    if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
                }
                else if (!TouchVisible && Input.GetMouseButtonDown(0) &&
                         (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    SetCursorLocked(true);
                }
            }

            // Knife: the alternate attack is the scope button.
            if (cmd.ScopePressed) cmd.AltFirePressed = true;
            return cmd;
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static float ReadAxis(string axis)
        {
            try
            {
                return Input.GetAxisRaw(axis);
            }
            catch (System.ArgumentException)
            {
                return 0f;
            }
        }
    }
}
