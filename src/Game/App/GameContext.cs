using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// Long-lived services shared by the menu and the match. Created once by Main.
    /// Match-scoped objects (player, HUD, effects) are set when a match starts and
    /// cleared when it ends.
    /// </summary>
    public static class GameContext
    {
        public static Main App;
        /// <summary>CI smoke test: drives the game automatically (see Main).</summary>
        public static bool SmokeTest;
        public static GameDatabase Database;
        public static GameSettings Settings;
        public static TrainingStats Lifetime;
        public static SoundBank Sounds;
        public static MaterialLibrary Materials;

        // Match scope.
        public static TrainingStats Session;
        public static AudioManager Audio;
        public static EffectsManager Effects;
        public static PlayerController Player;
        public static Hud Hud;
        public static bool SessionInfiniteAmmo;

        public static void ClearMatch()
        {
            Session = null;
            Audio = null;
            Effects = null;
            Player = null;
            Hud = null;
            SessionInfiniteAmmo = false;
        }
    }
}
