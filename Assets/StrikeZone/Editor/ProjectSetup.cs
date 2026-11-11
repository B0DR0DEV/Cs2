using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace StrikeZone.EditorTools
{
    /// <summary>
    /// Generates everything the project needs that is awkward to keep as hand-written YAML:
    /// the URP pipeline asset, material templates (so shaders are included in the build),
    /// the bootstrap scene and Android player settings. Idempotent — safe to run on every
    /// build, which is exactly what CI does. Menu: Strike Zone → Setup Project.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const string SettingsFolder = "Assets/Settings";
        private const string GeneratedFolder = "Assets/Resources/Generated";
        private const string UrpAssetPath = SettingsFolder + "/SZ_URP.asset";
        private const string RendererPath = SettingsFolder + "/SZ_URP_Renderer.asset";

        public const string ApplicationId = "com.strikezone.game";

        [MenuItem("Strike Zone/Setup Project")]
        public static void SetupFromMenu()
        {
            Run();
            Debug.Log("[StrikeZone] Project setup complete.");
        }

        public static void Run()
        {
            EnsureFolder("Assets", "Settings");
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Generated");

            var pipeline = EnsureRenderPipeline();
            EnsureMaterialTemplates(pipeline != null);
            EnsureScene();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        // ------------------------------------------------------------------ URP

        private static UniversalRenderPipelineAsset EnsureRenderPipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset == null)
            {
                try
                {
                    var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererPath);
                    asset = UniversalRenderPipelineAsset.Create(rendererData);
                    AssetDatabase.CreateAsset(asset, UrpAssetPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[StrikeZone] Could not create URP asset, falling back to the built-in pipeline: " + e.Message);
                    return null;
                }
            }

            // Mobile-first defaults; runtime presets adjust scale/shadows/MSAA further.
            asset.supportsHDR = false;
            asset.msaaSampleCount = 1;
            asset.renderScale = 1f;
            asset.shadowDistance = 40f;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            return asset;
        }

        // ------------------------------------------------------------------ materials

        private static void EnsureMaterialTemplates(bool urp)
        {
            Shader lit = urp ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (lit == null) lit = Shader.Find("Standard");
            CreateOrUpdateMaterial("SZ_Lit", lit, null);

            Shader sprite = Shader.Find("Sprites/Default");
            CreateOrUpdateMaterial("SZ_Sprite", sprite, null);

            Shader sky = Shader.Find("Skybox/Procedural");
            CreateOrUpdateMaterial("SZ_Sky", sky, m =>
            {
                if (m.HasProperty("_SunSize")) m.SetFloat("_SunSize", 0.035f);
                if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 0.9f);
                if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", new Color(0.55f, 0.6f, 0.68f));
                if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.37f));
                if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1.15f);
            });
        }

        private static void CreateOrUpdateMaterial(string name, Shader shader, Action<Material> configure)
        {
            if (shader == null)
            {
                Debug.LogWarning("[StrikeZone] Shader for " + name + " not found; runtime fallback will be used.");
                return;
            }
            string path = GeneratedFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.enableInstancing = true;
            if (configure != null) configure(mat);
            EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ scene

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ------------------------------------------------------------------ player settings

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "StrikeZone Team";
            PlayerSettings.productName = "Strike Zone";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;

            SetActiveInputHandling(2);
        }

        /// <summary>
        /// 0 = Input Manager (old), 1 = Input System package, 2 = both. Phase 1 reads touch and
        /// keyboard through the classic Input class and uGUI's StandaloneInputModule, so it needs
        /// the old backend enabled. There is no public API for this field, hence SerializedObject.
        /// </summary>
        private static void SetActiveInputHandling(int value)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            SerializedProperty prop = so.FindProperty("activeInputHandler");
            if (prop == null) return;
            if (prop.intValue != value)
            {
                prop.intValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[StrikeZone] Active input handling set to " + value);
            }
        }
    }
}
