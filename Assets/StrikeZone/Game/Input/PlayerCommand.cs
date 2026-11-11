namespace StrikeZone.Game
{
    /// <summary>
    /// One frame of player intent, merged from touch, keyboard/mouse and (phase 4)
    /// gamepad. In the multiplayer phase this becomes the per-tick input packet, with
    /// fire/jump events carrying a sub-tick timestamp.
    /// </summary>
    public struct PlayerCommand
    {
        public float Forward;
        public float Side;
        public bool Walk;
        public float LookYaw;
        public float LookPitch;

        public bool FireHeld;
        public bool FirePressed;
        public bool AltFirePressed;
        public bool JumpPressed;
        public bool CrouchHeld;
        public bool ReloadPressed;
        public bool ScopePressed;
        public bool InspectPressed;
        public int SelectSlot;
        public bool NextWeapon;
        public bool LastWeapon;
    }
}
