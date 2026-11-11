using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// Service locator for the offline sandbox. Kept deliberately small: systems receive
    /// what they need through Initialize() where practical, and this is only used by
    /// leaf objects created at runtime (targets, UI widgets).
    /// </summary>
    public static class GameContext
    {
        public static GameDatabase Database;
        public static MaterialLibrary Materials;
        public static AudioManager Audio;
        public static EffectsManager Effects;
        public static GameSettings Settings;
        public static PlayerController Player;
        public static Hud Hud;
        public static TrainingStats Stats;

        public static void Clear()
        {
            Database = null;
            Materials = null;
            Audio = null;
            Effects = null;
            Settings = null;
            Player = null;
            Hud = null;
            Stats = null;
        }
    }
}
