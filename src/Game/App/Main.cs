using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// Application root. Loads config and generated assets once, then switches between
    /// the main menu and a match. Everything else is built in code, so the repository
    /// contains no binary scenes and the CI export is fully reproducible.
    /// </summary>
    public partial class Main : Node
    {
        private Node _current;
        private LoadingScreen _loading;
        private AudioStreamPlayer _uiPlayer;

        public override void _Ready()
        {
            GameContext.App = this;
            DisplayServer.ScreenSetKeepOn(true);

            GameContext.Settings = GameSettings.Load();
            GameContext.Lifetime = TrainingStats.LoadLifetime();
            GameContext.Database = LoadDatabase();
            GameContext.Materials = new MaterialLibrary(GameContext.Database);
            GameContext.Sounds = new SoundBank();
            _uiPlayer = new AudioStreamPlayer { Name = "UiSound" };
            AddChild(_uiPlayer);

            ApplyGlobalSettings();
            ShowMainMenu();
        }

        private static GameDatabase LoadDatabase()
        {
            string weapons = FileAccess.GetFileAsString("res://config/weapons.json");
            string rules = FileAccess.GetFileAsString("res://config/rules.json");
            GameDatabase db = ConfigJson.Parse(weapons, rules);
            foreach (string error in db.Errors) GD.PushError("[StrikeZone] config: " + error);
            return db;
        }

        public void ApplyGlobalSettings()
        {
            var s = GameContext.Settings;
            Engine.MaxFps = s.TargetFps;
            AudioServer.SetBusVolumeDb(0, s.MasterVolume <= 0.001f ? -80f : Mathf.LinearToDb(s.MasterVolume));
            QualityPresets.Apply(GetViewport(), s.GraphicsPreset);
        }

        public void PlayUi(string sound)
        {
            var stream = GameContext.Sounds?.Get(sound);
            if (stream == null || _uiPlayer == null) return;
            _uiPlayer.Stream = stream;
            _uiPlayer.Play();
        }

        public void ShowMainMenu()
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            ReplaceCurrent(new MainMenu());
        }

        /// <summary>Shows a loading screen for a moment, then builds the training range.</summary>
        public void StartTraining(TrainingOptions options)
        {
            ReplaceCurrent(null);
            _loading = new LoadingScreen();
            AddChild(_loading);
            _loading.Show("ПОЛИГОН", options.ModeTitle);
            // Two frames: one to draw the loading screen, one to build the level.
            GetTree().CreateTimer(0.15).Timeout += () =>
            {
                var match = new TrainingMatch(options);
                ReplaceCurrent(match);
                if (_loading != null)
                {
                    _loading.QueueFree();
                    _loading = null;
                }
            };
        }

        public void EndMatch()
        {
            ReplaceCurrent(null);
            GameContext.ClearMatch();
            ShowMainMenu();
        }

        private void ReplaceCurrent(Node next)
        {
            if (_current != null)
            {
                _current.QueueFree();
                _current = null;
            }
            if (next != null)
            {
                _current = next;
                AddChild(next);
            }
        }

        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest || what == NotificationApplicationPaused)
            {
                GameContext.Settings?.Save();
                GameContext.Lifetime?.SaveLifetime();
            }
        }
    }
}
