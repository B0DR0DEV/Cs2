namespace StrikeZone.Game
{
    /// <summary>
    /// One physics tick of player intent, merged from touch and keyboard/mouse.
    /// In the multiplayer phase this becomes the input packet (with sub-tick timestamps).
    /// Look deltas are applied every rendered frame and are not part of the tick command.
    /// </summary>
    public struct PlayerCommand
    {
        public float Forward;
        public float Side;
        public bool Walk;
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
