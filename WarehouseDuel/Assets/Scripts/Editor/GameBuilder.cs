using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using WarehouseDuel.Core;
using WarehouseDuel.EditorTools.Audio;
using WarehouseDuel.EditorTools.Effects;
using WarehouseDuel.EditorTools.Gameplay;
using WarehouseDuel.EditorTools.Map;
using WarehouseDuel.EditorTools.UI;
using WarehouseDuel.EditorTools.Weapons;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.EditorTools
{
    /// <summary>
    /// One-click (or command-line) generation of the whole game from code, in dependency order:
    /// project settings → audio → UI art → effects → weapons → gameplay prefabs → map → menus/HUD → validation.
    /// Every step is idempotent: re-running updates assets in place and keeps their GUIDs.
    ///
    /// Command line (no Editor window):
    ///   Unity.exe -batchmode -quit -projectPath WarehouseDuel -executeMethod WarehouseDuel.EditorTools.GameBuilder.BuildAllBatch -logFile build.log
    ///   Unity.exe -batchmode -quit -projectPath WarehouseDuel -executeMethod WarehouseDuel.EditorTools.GameBuilder.BuildAndroidBatch -logFile android.log
    /// </summary>
    public static class GameBuilder
    {
        public const string AndroidOutput = "Builds/Android/WarehouseDuel.apk";

        [MenuItem("Tools/Warehouse Duel/Build Everything (all phases)", priority = 0)]
        public static void BuildEverythingMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupReport report = BuildEverything();
            EditorUtility.DisplayDialog("Warehouse Duel", $"Build finished: {report.Summary}\n\nDetails are in the Console.", "OK");
        }

        [MenuItem("Tools/Warehouse Duel/Build Android APK", priority = 50)]
        public static void BuildAndroidMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAndroid(out string message);
            EditorUtility.DisplayDialog("Android build", message, "OK");
        }

        internal static SetupReport BuildEverything()
        {
            var report = new SetupReport();
            try
            {
                // Phases 1-4 content: settings, audio, UI art, effects, weapons (map is built after the prefabs).
                WeaponCatalog catalog = ContentBuilder.BuildContent(report, buildMap: false);
                if (catalog == null) throw new InvalidOperationException("Content build failed; see errors above.");
                FusionConfigurator.Apply(report);
                GameplayPrefabBuilder.BuildAll(report, catalog);
                FusionConfigurator.RebuildPrefabTable(report);

                // Scenes.
                WarehouseMapBuilder.Build(report);
                HudBuilder.Build(report, catalog);
                MenuScenesBuilder.BuildMainMenu(report, catalog);
                MenuScenesBuilder.BuildLobby(report, catalog);
                BuildSettingsConfigurator.Apply(report);
                AssetDatabase.SaveAssets();

                SceneBuilder.OpenStartScene();
            }
            catch (Exception e)
            {
                report.Error($"Build stopped: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
            }

            report.PrintToConsole("Build everything");
            ProjectValidator.Run(showDialog: false);
            return report;
        }

        public static bool BuildAndroid(out string message)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    message = "Could not switch to the Android platform. Is Android Build Support installed?";
                    Debug.LogError(message);
                    return false;
                }
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(AndroidOutput));
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AndroidOutput,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport result = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = result.summary;
            bool ok = summary.result == BuildResult.Succeeded;
            message = ok
                ? $"APK built: {Path.GetFullPath(AndroidOutput)} ({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalMinutes:0.0} min)"
                : $"Android build {summary.result}: {summary.totalErrors} error(s). See the Console.";
            if (ok) Debug.Log("[Warehouse Duel] " + message);
            else Debug.LogError("[Warehouse Duel] " + message);
            return ok;
        }

        // ------------------------------------------------------------------ batch entry points

        public static void BuildAllBatch()
        {
            SetupReport report = BuildEverything();
            EditorApplication.Exit(report.ErrorCount == 0 ? 0 : 1);
        }

        public static void BuildAndroidBatch()
        {
            bool ok = BuildAndroid(out _);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void ValidateBatch()
        {
            SetupReport report = ProjectValidator.Run(showDialog: false);
            var map = new SetupReport();
            EditorSceneManager.OpenScene(GameConstants.Scenes.PathOf(GameConstants.Scenes.WarehouseMap));
            MapValidator.Run(map);
            map.PrintToConsole("Warehouse map validation");
            EditorApplication.Exit(report.ErrorCount + map.ErrorCount == 0 ? 0 : 1);
        }

        /// <summary>
        /// Windows development build used by the automated two-instance multiplayer test (AutoTestDriver).
        /// Same scenes and game code as Android; much faster to build and run on the development PC.
        /// </summary>
        public static void BuildWindowsTestBatch()
        {
            const string output = "Builds/WindowsTest/WarehouseDuel.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            BuildReport result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development,
            });
            Debug.Log($"[Warehouse Duel] Windows test build: {result.summary.result}, {result.summary.totalErrors} error(s) -> {Path.GetFullPath(output)}");
            EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
