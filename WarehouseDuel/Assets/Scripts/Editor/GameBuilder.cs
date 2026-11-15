using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using WarehouseWar.Core;
using WarehouseWar.EditorTools.Audio;
using WarehouseWar.EditorTools.Effects;
using WarehouseWar.EditorTools.Gameplay;
using WarehouseWar.EditorTools.Map;
using WarehouseWar.EditorTools.UI;
using WarehouseWar.EditorTools.Weapons;
using WarehouseWar.Weapons;

namespace WarehouseWar.EditorTools
{
    /// <summary>
    /// One-click (or command-line) generation of the whole game from code, in dependency order:
    /// project settings → audio → UI art → effects → weapons → gameplay prefabs → map → menus/HUD → validation.
    /// Every step is idempotent: re-running updates assets in place and keeps their GUIDs.
    ///
    /// Command line (no Editor window):
    ///   Unity.exe -batchmode -quit -projectPath WarehouseWar -executeMethod WarehouseWar.EditorTools.GameBuilder.BuildAllBatch -logFile build.log
    ///   Unity.exe -batchmode -quit -projectPath WarehouseWar -executeMethod WarehouseWar.EditorTools.GameBuilder.BuildAndroidBatch -logFile android.log
    /// </summary>
    public static class GameBuilder
    {
        public const string AndroidOutput = "Builds/Android/WarehouseWar.apk";

        [MenuItem("Tools/Warehouse War/Build Everything (all phases)", priority = 0)]
        public static void BuildEverythingMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupReport report = BuildEverything();
            EditorUtility.DisplayDialog("Warehouse War", $"Build finished: {report.Summary}\n\nDetails are in the Console.", "OK");
        }

        [MenuItem("Tools/Warehouse War/Build Android APK", priority = 50)]
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
                BrandingConfigurator.Apply(report);
                FusionConfigurator.Apply(report);
                GameplayPrefabBuilder.BuildAll(report, catalog);
                CharacterBuilder.Build(report);
                ProfileArtBuilder.Build(report);
                FusionConfigurator.RebuildPrefabTable(report);

                // Scenes.
                WarehouseMapBuilder.Build(report);
                OutdoorMapBuilder.Build(report);
                HudBuilder.Build(report, catalog);
                MenuArtBuilder.Build(report);
                MenuScenesBuilder.BuildMainMenu(report, catalog);
                MenuScenesBuilder.BuildLobby(report, catalog);
                SplashBuilder.Build(report);
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
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.Generic;

            // "-developmentBuild" on the command line: debuggable diagnostic build next to the normal APK.
            bool development = System.Environment.GetCommandLineArgs().Contains("-developmentBuild");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = development ? AndroidOutput.Replace(".apk", "_Debug.apk") : AndroidOutput,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                // LZ4HC: game data is compressed in small blocks Unity can seek into, and stored uncompressed in the
                // APK zip. Without it the zip DEFLATEs the data and every backward seek re-inflates the file from the
                // start, which froze startup on a black screen once the data grew (round 3).
                options = BuildOptions.CompressWithLz4HC | (development ? BuildOptions.Development : BuildOptions.None),
            };

            BuildReport result = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = result.summary;
            bool ok = summary.result == BuildResult.Succeeded;
            message = ok
                ? $"APK built: {Path.GetFullPath(AndroidOutput)} ({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalMinutes:0.0} min)"
                : $"Android build {summary.result}: {summary.totalErrors} error(s). See the Console.";
            if (ok) Debug.Log("[Warehouse War] " + message);
            else Debug.LogError("[Warehouse War] " + message);
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
            MapValidator.RunAllMaps(map);
            map.PrintToConsole("Map validation (all maps)");
            EditorApplication.Exit(report.ErrorCount + map.ErrorCount == 0 ? 0 : 1);
        }

        /// <summary>
        /// Windows development build used by the automated two-instance multiplayer test (AutoTestDriver).
        /// Same scenes and game code as Android; much faster to build and run on the development PC.
        /// </summary>
        public static void BuildWindowsTestBatch()
        {
            const string output = "Builds/WindowsTest/WarehouseWar.exe";
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
            Debug.Log($"[Warehouse War] Windows test build: {result.summary.result}, {result.summary.totalErrors} error(s) -> {Path.GetFullPath(output)}");
            EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
