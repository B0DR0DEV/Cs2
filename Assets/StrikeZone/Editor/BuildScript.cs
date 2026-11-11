using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StrikeZone.EditorTools
{
    /// <summary>
    /// Command-line and menu builds. CI (game-ci/unity-builder) calls
    /// <c>-executeMethod StrikeZone.EditorTools.BuildScript.BuildAndroidCI</c> and passes
    /// -customBuildPath, -androidVersionCode, -buildVersion and optional keystore arguments.
    /// Without a keystore the APK is debug-signed, which is fine for side-loading tests.
    /// </summary>
    public static class BuildScript
    {
        private const string DefaultOutput = "build/Android/StrikeZone.apk";

        [MenuItem("Strike Zone/Build Android APK")]
        public static void BuildAndroidFromMenu()
        {
            string path = EditorUtility.SaveFilePanel("Save APK", "build/Android", "StrikeZone", "apk");
            if (string.IsNullOrEmpty(path)) return;
            BuildAndroid(path, false);
        }

        public static void BuildAndroidCI()
        {
            string output = GetArg("-customBuildPath");
            if (string.IsNullOrEmpty(output)) output = DefaultOutput;
            if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) output = Path.Combine(output, "StrikeZone.apk");

            string versionCode = GetArg("-androidVersionCode");
            int code;
            if (!string.IsNullOrEmpty(versionCode) && int.TryParse(versionCode, out code) && code > 0)
                PlayerSettings.Android.bundleVersionCode = code;

            string version = GetArg("-buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
            else PlayerSettings.bundleVersion = "0.1.0";

            ConfigureKeystore();
            BuildAndroid(output, true);
        }

        private static void BuildAndroid(string output, bool exitOnFinish)
        {
            ProjectSetup.Run();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;

            string dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[StrikeZone] Build " + summary.result + ": " + output + " (" + (summary.totalSize / (1024 * 1024)) + " MB, " +
                      summary.totalTime + ", " + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings)");

            if (exitOnFinish) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static void ConfigureKeystore()
        {
            string keystore = GetArg("-androidKeystoreName");
            string keystorePass = GetArg("-androidKeystorePass");
            string alias = GetArg("-androidKeyaliasName");
            string aliasPass = GetArg("-androidKeyaliasPass");
            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore) || string.IsNullOrEmpty(alias))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
