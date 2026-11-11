using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Player preferences persisted in PlayerPrefs. Phase 4 extends this with HUD layouts;
    /// the keys are versioned so a format change never breaks an existing install.
    /// </summary>
    public sealed class GameSettings
    {
        private const string Prefix = "sz.v1.";

        // Look sensitivity: degrees per screen-height of swipe (touch) / per mouse unit.
        public float TouchSensitivity = 1.0f;
        public float ScopedSensitivityMultiplier = 0.6f;
        public float MouseSensitivity = 2.0f;
        public bool InvertY;
        public bool LookAcceleration;

        public int TargetFps = 60;
        public int GraphicsPreset = 1; // 0 low, 1 medium, 2 high, 3 ultra

        // Training-range options.
        public bool TargetArmor = true;
        public bool TargetHelmet = true;
        public bool TargetsMove = true;
        public bool InfiniteAmmo;
        public bool ShowRecoilPattern;
        public bool ShowTracers = true;
        public bool ShowHitInfo = true;
        public bool CrouchToggle = true;
        public float MasterVolume = 1f;
        public bool Vibration = true;

        public static GameSettings Load()
        {
            var s = new GameSettings();
            s.TouchSensitivity = PlayerPrefs.GetFloat(Prefix + "touchSens", s.TouchSensitivity);
            s.ScopedSensitivityMultiplier = PlayerPrefs.GetFloat(Prefix + "scopedSens", s.ScopedSensitivityMultiplier);
            s.MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "mouseSens", s.MouseSensitivity);
            s.InvertY = GetBool("invertY", s.InvertY);
            s.LookAcceleration = GetBool("lookAccel", s.LookAcceleration);
            s.TargetFps = PlayerPrefs.GetInt(Prefix + "fps", s.TargetFps);
            s.GraphicsPreset = PlayerPrefs.GetInt(Prefix + "gfx", s.GraphicsPreset);
            s.TargetArmor = GetBool("tArmor", s.TargetArmor);
            s.TargetHelmet = GetBool("tHelmet", s.TargetHelmet);
            s.TargetsMove = GetBool("tMove", s.TargetsMove);
            s.InfiniteAmmo = GetBool("infAmmo", s.InfiniteAmmo);
            s.ShowRecoilPattern = GetBool("recoilOverlay", s.ShowRecoilPattern);
            s.ShowTracers = GetBool("tracers", s.ShowTracers);
            s.ShowHitInfo = GetBool("hitInfo", s.ShowHitInfo);
            s.CrouchToggle = GetBool("crouchToggle", s.CrouchToggle);
            s.MasterVolume = PlayerPrefs.GetFloat(Prefix + "volume", s.MasterVolume);
            s.Vibration = GetBool("vibration", s.Vibration);
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "touchSens", TouchSensitivity);
            PlayerPrefs.SetFloat(Prefix + "scopedSens", ScopedSensitivityMultiplier);
            PlayerPrefs.SetFloat(Prefix + "mouseSens", MouseSensitivity);
            SetBool("invertY", InvertY);
            SetBool("lookAccel", LookAcceleration);
            PlayerPrefs.SetInt(Prefix + "fps", TargetFps);
            PlayerPrefs.SetInt(Prefix + "gfx", GraphicsPreset);
            SetBool("tArmor", TargetArmor);
            SetBool("tHelmet", TargetHelmet);
            SetBool("tMove", TargetsMove);
            SetBool("infAmmo", InfiniteAmmo);
            SetBool("recoilOverlay", ShowRecoilPattern);
            SetBool("tracers", ShowTracers);
            SetBool("hitInfo", ShowHitInfo);
            SetBool("crouchToggle", CrouchToggle);
            PlayerPrefs.SetFloat(Prefix + "volume", MasterVolume);
            SetBool("vibration", Vibration);
            PlayerPrefs.Save();
        }

        private static bool GetBool(string key, bool fallback)
        {
            return PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
        }
    }
}
