using Godot;

namespace StrikeZone.Game
{
    /// <summary>Player preferences, persisted in user://settings.cfg.</summary>
    public sealed class GameSettings
    {
        private const string Path = "user://settings.cfg";
        private const string S = "settings";

        public string Nickname = "Игрок";
        public float TouchSensitivity = 1.0f;
        public float ScopedSensitivityMultiplier = 0.6f;
        public float MouseSensitivity = 0.12f;
        public bool InvertY;
        public bool LookAcceleration;
        public bool CrouchToggle = true;
        public int TargetFps = 60;
        public int GraphicsPreset = 1;
        public float MasterVolume = 0.9f;
        public bool Vibration = true;

        public bool TargetArmor = true;
        public bool TargetHelmet = true;
        public bool TargetsMove = true;
        public bool InfiniteAmmo;
        public bool ShowRecoilPattern;
        public bool ShowTracers = true;
        public bool ShowHitInfo = true;

        public string LoadoutPrimary = "kestrel";
        public string LoadoutSecondary = "viper9";

        public static GameSettings Load()
        {
            var s = new GameSettings();
            var cfg = new ConfigFile();
            if (cfg.Load(Path) != Error.Ok) return s;
            s.Nickname = cfg.GetValue(S, "nickname", s.Nickname).AsString();
            s.TouchSensitivity = cfg.GetValue(S, "touchSens", s.TouchSensitivity).AsSingle();
            s.ScopedSensitivityMultiplier = cfg.GetValue(S, "scopedSens", s.ScopedSensitivityMultiplier).AsSingle();
            s.MouseSensitivity = cfg.GetValue(S, "mouseSens", s.MouseSensitivity).AsSingle();
            s.InvertY = cfg.GetValue(S, "invertY", s.InvertY).AsBool();
            s.LookAcceleration = cfg.GetValue(S, "lookAccel", s.LookAcceleration).AsBool();
            s.CrouchToggle = cfg.GetValue(S, "crouchToggle", s.CrouchToggle).AsBool();
            s.TargetFps = cfg.GetValue(S, "fps", s.TargetFps).AsInt32();
            s.GraphicsPreset = cfg.GetValue(S, "gfx", s.GraphicsPreset).AsInt32();
            s.MasterVolume = cfg.GetValue(S, "volume", s.MasterVolume).AsSingle();
            s.Vibration = cfg.GetValue(S, "vibration", s.Vibration).AsBool();
            s.TargetArmor = cfg.GetValue(S, "tArmor", s.TargetArmor).AsBool();
            s.TargetHelmet = cfg.GetValue(S, "tHelmet", s.TargetHelmet).AsBool();
            s.TargetsMove = cfg.GetValue(S, "tMove", s.TargetsMove).AsBool();
            s.InfiniteAmmo = cfg.GetValue(S, "infAmmo", s.InfiniteAmmo).AsBool();
            s.ShowRecoilPattern = cfg.GetValue(S, "recoilOverlay", s.ShowRecoilPattern).AsBool();
            s.ShowTracers = cfg.GetValue(S, "tracers", s.ShowTracers).AsBool();
            s.ShowHitInfo = cfg.GetValue(S, "hitInfo", s.ShowHitInfo).AsBool();
            s.LoadoutPrimary = cfg.GetValue(S, "primary", s.LoadoutPrimary).AsString();
            s.LoadoutSecondary = cfg.GetValue(S, "secondary", s.LoadoutSecondary).AsString();
            return s;
        }

        public void Save()
        {
            var cfg = new ConfigFile();
            cfg.SetValue(S, "nickname", Nickname);
            cfg.SetValue(S, "touchSens", TouchSensitivity);
            cfg.SetValue(S, "scopedSens", ScopedSensitivityMultiplier);
            cfg.SetValue(S, "mouseSens", MouseSensitivity);
            cfg.SetValue(S, "invertY", InvertY);
            cfg.SetValue(S, "lookAccel", LookAcceleration);
            cfg.SetValue(S, "crouchToggle", CrouchToggle);
            cfg.SetValue(S, "fps", TargetFps);
            cfg.SetValue(S, "gfx", GraphicsPreset);
            cfg.SetValue(S, "volume", MasterVolume);
            cfg.SetValue(S, "vibration", Vibration);
            cfg.SetValue(S, "tArmor", TargetArmor);
            cfg.SetValue(S, "tHelmet", TargetHelmet);
            cfg.SetValue(S, "tMove", TargetsMove);
            cfg.SetValue(S, "infAmmo", InfiniteAmmo);
            cfg.SetValue(S, "recoilOverlay", ShowRecoilPattern);
            cfg.SetValue(S, "tracers", ShowTracers);
            cfg.SetValue(S, "hitInfo", ShowHitInfo);
            cfg.SetValue(S, "primary", LoadoutPrimary);
            cfg.SetValue(S, "secondary", LoadoutSecondary);
            cfg.Save(Path);
        }
    }

    /// <summary>Training statistics: per session (shown in the HUD) and lifetime (Statistics tab).</summary>
    public sealed class TrainingStats
    {
        private const string Path = "user://stats.cfg";
        private const string S = "stats";

        public int Shots;
        public int Hits;
        public int Headshots;
        public int Kills;
        public int Damage;
        public double Seconds;

        public float Accuracy
        {
            get { return Shots > 0 ? Hits / (float)Shots : 0f; }
        }

        public float HeadshotRate
        {
            get { return Hits > 0 ? Headshots / (float)Hits : 0f; }
        }

        public void Reset()
        {
            Shots = Hits = Headshots = Kills = Damage = 0;
            Seconds = 0;
        }

        public void AddTo(TrainingStats total)
        {
            total.Shots += Shots;
            total.Hits += Hits;
            total.Headshots += Headshots;
            total.Kills += Kills;
            total.Damage += Damage;
            total.Seconds += Seconds;
        }

        public static TrainingStats LoadLifetime()
        {
            var t = new TrainingStats();
            var cfg = new ConfigFile();
            if (cfg.Load(Path) != Error.Ok) return t;
            t.Shots = cfg.GetValue(S, "shots", 0).AsInt32();
            t.Hits = cfg.GetValue(S, "hits", 0).AsInt32();
            t.Headshots = cfg.GetValue(S, "hs", 0).AsInt32();
            t.Kills = cfg.GetValue(S, "kills", 0).AsInt32();
            t.Damage = cfg.GetValue(S, "damage", 0).AsInt32();
            t.Seconds = cfg.GetValue(S, "seconds", 0.0).AsDouble();
            return t;
        }

        public void SaveLifetime()
        {
            var cfg = new ConfigFile();
            cfg.SetValue(S, "shots", Shots);
            cfg.SetValue(S, "hits", Hits);
            cfg.SetValue(S, "hs", Headshots);
            cfg.SetValue(S, "kills", Kills);
            cfg.SetValue(S, "damage", Damage);
            cfg.SetValue(S, "seconds", Seconds);
            cfg.Save(Path);
        }
    }
}
