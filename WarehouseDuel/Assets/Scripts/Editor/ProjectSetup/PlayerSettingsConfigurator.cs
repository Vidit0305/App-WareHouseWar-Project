using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using WarehouseDuel.Core;

namespace WarehouseDuel.EditorTools
{
    /// <summary>
    /// Applies editor + player settings for an Android landscape multiplayer game.
    /// Identity values (company, bundle id) are only written while they are still Unity's defaults,
    /// so re-running setup never overwrites values you changed yourself.
    /// </summary>
    internal static class PlayerSettingsConfigurator
    {
        // PLACEHOLDER identity — change before publishing. The bundle id must be unique on Google Play / App Store.
        public const string DefaultCompanyName = "WarehouseDuelStudio";
        public const string DefaultBundleIdentifier = "com.warehouseduelstudio.warehouseduel";

        public const int MinimumAndroidApiLevel = 26; // Android 8.0

        public static void Apply(SetupReport report)
        {
            ApplyEditorSettings(report);
            ApplyIdentity(report);
            ApplyOrientation(report);
            ApplyAndroid(report);

            PlayerSettings.runInBackground = true;
            report.Pass("Run In Background enabled (two local test instances keep simulating while unfocused).");
        }

        private static void ApplyEditorSettings(SetupReport report)
        {
            // Photon Fusion requires text serialization (its prefab/scene baking edits YAML).
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                report.Pass("Asset Serialization set to Force Text (required by Photon Fusion).");
            }
            else report.Pass("Asset Serialization is Force Text.");

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                report.Pass("Color Space set to Linear (URP lighting is authored for Linear).");
            }
            else report.Pass("Color Space is Linear.");
        }

        private static void ApplyIdentity(SetupReport report)
        {
            if (PlayerSettings.companyName == "DefaultCompany" || PlayerSettings.companyName == "Unity Technologies")
            {
                PlayerSettings.companyName = DefaultCompanyName;
                report.Pass($"Company Name set to placeholder '{DefaultCompanyName}'.");
            }

            string projectFolderName = new DirectoryInfo(Application.dataPath).Parent?.Name ?? string.Empty;
            if (PlayerSettings.productName == projectFolderName || PlayerSettings.productName == "My project"
                || PlayerSettings.productName.Contains("template"))
            {
                PlayerSettings.productName = GameConstants.GameTitle;
                report.Pass($"Product Name set to '{GameConstants.GameTitle}'.");
            }

            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android })
            {
                string current = PlayerSettings.GetApplicationIdentifier(target);
                bool isUnityDefault = string.IsNullOrEmpty(current)
                                      || current.StartsWith("com.DefaultCompany.", StringComparison.OrdinalIgnoreCase)
                                      || current.StartsWith("com.Company.", StringComparison.OrdinalIgnoreCase)
                                      || current.StartsWith("com.UnityTechnologies.", StringComparison.OrdinalIgnoreCase)
                                      || current.Contains("template");
                if (isUnityDefault)
                {
                    PlayerSettings.SetApplicationIdentifier(target, DefaultBundleIdentifier);
                    report.Warn($"{target.TargetName} bundle id set to PLACEHOLDER '{DefaultBundleIdentifier}' — replace it with your own before publishing.");
                }
                else report.Pass($"{target.TargetName} bundle id kept: {current}");
            }
        }

        private static void ApplyOrientation(SetupReport report)
        {
            // FPS controls are designed for landscape only; allow both landscape directions.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            report.Pass("Orientation: auto-rotate between Landscape Left/Right only.");
        }

        private static void ApplyAndroid(SetupReport report)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // required by Google Play
            report.Pass("Android: IL2CPP scripting backend, ARM64.");

            if ((int)PlayerSettings.Android.minSdkVersion < MinimumAndroidApiLevel)
            {
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinimumAndroidApiLevel;
                report.Pass($"Android: Minimum API level raised to {MinimumAndroidApiLevel}.");
            }
            else report.Pass($"Android: Minimum API level is {(int)PlayerSettings.Android.minSdkVersion}.");

            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Photon uses raw sockets; Unity cannot always detect that, so force the INTERNET permission.
            PlayerSettings.Android.forceInternetPermission = true;
            report.Pass("Android: Target API = highest installed; INTERNET permission forced on.");
        }
    }
}
