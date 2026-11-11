using System.Collections.Generic;
using Godot;

namespace StrikeZone.Game
{
    public enum TrainingMode
    {
        Free,
        Targets
    }

    public sealed class TrainingOptions
    {
        public TrainingMode Mode = TrainingMode.Free;
        public string ModeTitle = "СВОБОДНАЯ ТРЕНИРОВКА";
        /// <summary>Free practice: unlimited ammo regardless of the saved setting.</summary>
        public bool InfiniteAmmo = true;
    }

    /// <summary>
    /// One offline session on the training range: environment, map, targets, player,
    /// effects, audio, HUD, touch controls and the pause menu.
    /// </summary>
    public partial class TrainingMatch : Node3D
    {
        private readonly TrainingOptions _options;
        private DirectionalLight3D _sun;
        private readonly List<TargetDummy> _targets = new List<TargetDummy>();
        private PlayerController _player;

        public TrainingMatch(TrainingOptions options)
        {
            _options = options ?? new TrainingOptions();
            Name = "TrainingMatch";
        }

        // Godot needs a parameterless constructor for scripts it may instantiate itself.
        public TrainingMatch() : this(null)
        {
        }

        public override void _Ready()
        {
            var db = GameContext.Database;
            GameContext.Session = new TrainingStats();
            GameContext.SessionInfiniteAmmo = _options.InfiniteAmmo;

            BuildEnvironment();
            var layout = new TrainingRangeBuilder(GameContext.Materials).Build(this);

            var audio = new AudioManager { Name = "Audio" };
            AddChild(audio);
            GameContext.Audio = audio;
            var effects = new EffectsManager { Name = "Effects", ShowTracers = GameContext.Settings.ShowTracers };
            AddChild(effects);
            GameContext.Effects = effects;

            foreach (var spawn in layout.Targets)
            {
                var t = TargetDummy.Create(spawn);
                AddChild(t);
                _targets.Add(t);
            }

            // UI layer.
            var ui = new CanvasLayer { Name = "UI", Layer = 10 };
            AddChild(ui);
            var hud = new Hud { Name = "Hud" };
            var touch = new TouchControls { Name = "Touch" };
            var router = new InputRouter { Name = "Input", Touch = touch };
            AddChild(router);

            _player = new PlayerController { Name = "Player" };
            _player.Setup(db, router, layout.PlayerSpawn, layout.PlayerYaw);
            AddChild(_player);
            GameContext.Player = _player;
            hud.Setup(_player);
            GameContext.Hud = hud;

            ui.AddChild(hud);
            ui.AddChild(touch);
            touch.Visible = OS.HasFeature("mobile");
            var menu = new GameMenu
            {
                Name = "Menu",
                Router = router,
                OnWeaponPicked = def => _player.Weapons.Give(def, true),
                OnResetStats = ResetStats,
                OnExit = Exit,
                OnSettingsChanged = ApplySettings
            };
            ui.AddChild(menu);

            ApplySettings();
            hud.ShowMessage("ПОЛИГОН — стрельба, отдача, прострелы, движение", 4f);
        }

        private void BuildEnvironment()
        {
            var sky = new ProceduralSkyMaterial
            {
                SkyTopColor = new Color(0.36f, 0.5f, 0.68f),
                SkyHorizonColor = new Color(0.72f, 0.76f, 0.8f),
                GroundBottomColor = new Color(0.2f, 0.19f, 0.18f),
                GroundHorizonColor = new Color(0.6f, 0.6f, 0.6f),
                SunAngleMax = 30f
            };
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Sky,
                AmbientLightEnergy = 0.9f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                FogEnabled = true,
                FogLightColor = new Color(0.72f, 0.76f, 0.82f),
                FogDensity = 0.0015f
            };
            AddChild(new WorldEnvironment { Environment = env });

            _sun = new DirectionalLight3D
            {
                Name = "Sun",
                LightEnergy = 1.25f,
                LightColor = new Color(1f, 0.95f, 0.86f),
                ShadowEnabled = true,
                DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits,
                RotationDegrees = new Vector3(-48f, 35f, 0f)
            };
            AddChild(_sun);
        }

        public void ApplySettings()
        {
            var s = GameContext.Settings;
            QualityPresets.ApplyToSun(_sun, s.GraphicsPreset);
            if (GameContext.Effects != null) GameContext.Effects.ShowTracers = s.ShowTracers;
            foreach (var t in _targets)
                if (IsInstanceValid(t) && t.IsAlive && t.IsInsideTree()) t.Respawn();
        }

        private void ResetStats()
        {
            CommitStats();
            GameContext.Session = new TrainingStats();
            _player.Respawn();
            foreach (var t in _targets) t.Respawn();
            GameContext.Hud?.ShowMessage("Статистика сброшена", 1.5f);
        }

        private void CommitStats()
        {
            if (GameContext.Session == null) return;
            GameContext.Session.AddTo(GameContext.Lifetime);
            GameContext.Lifetime.SaveLifetime();
            GameContext.Session.Reset();
        }

        private void Exit()
        {
            CommitStats();
            GameContext.Settings.Save();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            GameContext.App.EndMatch();
        }
    }
}
