using System.Collections.Generic;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Entry point. The scene in the build is empty; this runs after it loads and builds
    /// the whole training range in code: config → materials → map → audio → player → UI.
    /// Building from code keeps the phase-1 project free of hand-edited scene files and
    /// makes the CI build reproducible. Phase 8 maps become authored scenes with baked light.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        private Light _sun;
        private InputRouter _input;
        private readonly List<TargetDummy> _targets = new List<TargetDummy>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Instance != null) return;
            var go = new GameObject("StrikeZone");
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Setup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameContext.Clear();
            }
        }

        private void Setup()
        {
            // ------------------------------------------------------------ platform
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Input.multiTouchEnabled = true;
            Layers.ConfigureCollisionMatrix();
            Haptics.EnsurePermissionIsRequested();

            // ------------------------------------------------------------ data
            var settings = GameSettings.Load();
            GameContext.Settings = settings;
            var db = ConfigLoader.Load();
            GameContext.Database = db;
            GameContext.Stats = new TrainingStats();
            var materials = new MaterialLibrary(db);
            GameContext.Materials = materials;

            // ------------------------------------------------------------ lighting
            SetupLighting(materials);

            // ------------------------------------------------------------ world
            var layout = new BlockoutMapBuilder(materials).Build(transform);
            // Combine all static blocks: a few dozen draw calls for the whole range.
            StaticBatchingUtility.Combine(layout.StaticRoot.gameObject);

            // ------------------------------------------------------------ audio
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            var audio = audioGo.AddComponent<AudioManager>();
            audio.Initialize(new ProceduralAudio(), null);
            GameContext.Audio = audio;

            // ------------------------------------------------------------ UI + input
            UiFactory.EnsureEventSystem(transform);
            Canvas hudCanvas = UiFactory.CreateCanvas("HudCanvas", 0, transform);
            Canvas touchCanvas = UiFactory.CreateCanvas("TouchCanvas", 10, transform);
            Canvas menuCanvas = UiFactory.CreateCanvas("MenuCanvas", 20, transform);
            _input = TouchControlsBuilder.Build(transform, touchCanvas);
            _input.TouchVisible = Application.isMobilePlatform || Input.touchSupported;

            // ------------------------------------------------------------ player
            var player = PlayerController.Create(transform, db, layout.PlayerSpawn, layout.PlayerYaw, _input);
            GameContext.Player = player;
            audio.SetListener(player.CameraTransform);

            var effectsGo = new GameObject("Effects");
            effectsGo.transform.SetParent(transform, false);
            var effects = effectsGo.AddComponent<EffectsManager>();
            effects.Initialize(materials, player.Camera, audio);
            GameContext.Effects = effects;

            // ------------------------------------------------------------ targets
            var targetsRoot = new GameObject("Targets").transform;
            targetsRoot.SetParent(transform, false);
            foreach (var spawn in layout.Targets)
                _targets.Add(TargetDummy.Create(targetsRoot, spawn.Position, spawn.Yaw, spawn.Mode, spawn.StrafeA, spawn.StrafeB));

            // ------------------------------------------------------------ HUD & menus
            var hud = hudCanvas.gameObject.AddComponent<Hud>();
            GameContext.Hud = hud;
            hud.Initialize(player, hudCanvas);
            var menus = menuCanvas.gameObject.AddComponent<GameMenus>();
            menus.Initialize(db, player, _input, menuCanvas, ApplySettings, ResetStats);

            ApplySettings();
        }

        private void SetupLighting(MaterialLibrary materials)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = new Color(1f, 0.95f, 0.86f);
            _sun.intensity = 1.25f;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.85f;
            RenderSettings.sun = _sun;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.26f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.76f, 0.82f);
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 350f;
            if (materials.Sky != null) RenderSettings.skybox = materials.Sky;
        }

        /// <summary>Re-applies settings after a change in the menu.</summary>
        public void ApplySettings()
        {
            var s = GameContext.Settings;
            Application.targetFrameRate = s.TargetFps;
            QualityPresets.Apply(s.GraphicsPreset, _sun);
            if (GameContext.Audio != null) GameContext.Audio.MasterVolume = s.MasterVolume;
            if (GameContext.Effects != null) GameContext.Effects.ShowTracers = s.ShowTracers;
            foreach (var t in _targets)
                if (t != null && t.IsAlive) t.Respawn();
        }

        private void ResetStats()
        {
            if (GameContext.Stats != null) GameContext.Stats.Reset();
            if (GameContext.Player != null) GameContext.Player.Respawn();
            foreach (var t in _targets)
                if (t != null) t.Respawn();
            if (GameContext.Hud != null) GameContext.Hud.ShowMessage("Статистика сброшена", 1.5f);
        }
    }
}
