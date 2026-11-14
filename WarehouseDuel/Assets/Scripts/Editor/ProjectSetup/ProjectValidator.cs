using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WarehouseWar.Core;
using Object = UnityEngine.Object;

namespace WarehouseWar.EditorTools
{
    /// <summary>
    /// Read-only health check of the project. Changes nothing; reports what is missing and how to fix it.
    /// Photon types are looked up by name so this compiles before the Fusion SDK is imported.
    /// </summary>
    internal static class ProjectValidator
    {
        public static SetupReport Run(bool showDialog)
        {
            var report = new SetupReport();

            CheckUnityVersion(report);
            CheckRenderPipeline(report);
            CheckInputSystem(report);
            CheckEditorSettings(report);
            CheckTextMeshPro(report);
            CheckFolders(report);
            CheckLayers(report);
            CheckScenes(report);
            CheckMobilePlatforms(report);
            CheckPhotonFusion(report);

            report.PrintToConsole("Project validation");
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Warehouse War — Validation", report.Summary + "\n\nDetails are in the Console.", "OK");
            }
            return report;
        }

        private static void CheckUnityVersion(SetupReport report)
        {
            string version = Application.unityVersion; // e.g. "6000.3.24f1"
            string[] parts = version.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                if (major > 6000 || (major == 6000 && minor >= 3)) report.Pass($"Unity {version}");
                else report.Warn($"Unity {version}: this project targets Unity 6.3 LTS (6000.3.x).");
            }
            else report.Warn($"Could not parse Unity version '{version}'.");
        }

        private static void CheckRenderPipeline(SetupReport report)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                report.Pass($"URP active ('{urp.name}', quality level '{QualitySettings.names[QualitySettings.GetQualityLevel()]}').");
            }
            else
            {
                report.Error("URP is not the active render pipeline. Create the project from the 'Universal 3D' template, or assign a URP asset in Project Settings > Graphics and > Quality.");
            }
        }

        private static void CheckInputSystem(SetupReport report)
        {
#if ENABLE_INPUT_SYSTEM
            report.Pass("Input System package is active.");
#if ENABLE_LEGACY_INPUT_MANAGER
            report.Info("Active Input Handling = Both. That works; the game itself uses only the Input System.");
#endif
#else
            report.Error("Input System is not active. Install 'Input System' in the Package Manager and set Project Settings > Player > Active Input Handling to 'Input System Package (New)'.");
#endif
        }

        private static void CheckEditorSettings(SetupReport report)
        {
            if (EditorSettings.serializationMode == SerializationMode.ForceText) report.Pass("Asset Serialization = Force Text.");
            else report.Error("Asset Serialization must be Force Text (Photon Fusion requirement). Run the Phase 1 setup.");

            if (PlayerSettings.colorSpace == ColorSpace.Linear) report.Pass("Color Space = Linear.");
            else report.Warn("Color Space is Gamma; Linear is expected for URP.");
        }

        private static void CheckTextMeshPro(SetupReport report)
        {
            if (SceneBuilder.TmpEssentialsImported()) report.Pass("TextMesh Pro Essential Resources imported.");
            else report.Error("TextMesh Pro Essential Resources missing: Window > TextMeshPro > Import TMP Essential Resources.");
        }

        private static void CheckFolders(SetupReport report)
        {
            string[] missing = FolderStructure.Missing().ToArray();
            if (missing.Length == 0) report.Pass("Folder structure complete.");
            else report.Warn($"Missing folders: {string.Join(", ", missing)}");
        }

        private static void CheckLayers(SetupReport report)
        {
            foreach (var (name, index) in GameConstants.Layers.Definitions)
            {
                string actual = LayerMask.LayerToName(index);
                if (actual == name) report.Pass($"Layer {index} = {name}");
                else report.Error($"Layer {index} should be '{name}' but is '{actual}'.");
            }
        }

        private static void CheckScenes(SetupReport report)
        {
            string[] enabledInBuild = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string[] order = GameConstants.Scenes.BuildOrder;

            for (int i = 0; i < order.Length; i++)
            {
                string path = GameConstants.Scenes.PathOf(order[i]);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    report.Error($"Scene missing: {path}");
                }
                else if (i >= enabledInBuild.Length || enabledInBuild[i] != path)
                {
                    report.Error($"Build index {i} should be {path}. Run the Phase 1 setup to fix the build list.");
                }
                else report.Pass($"Build index {i}: {order[i]}");
            }

            // Generated scenes must not contain empty asset references for the weapon catalog.
            foreach (string sceneName in order)
            {
                string path = GameConstants.Scenes.PathOf(sceneName);
                if (!System.IO.File.Exists(path)) continue;
                int missing = System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(path), @"catalog: \{fileID: 0\}").Count;
                if (missing > 0) report.Error($"{sceneName}: {missing} component(s) have no WeaponCatalog assigned. Rebuild the scenes.");
            }

            if (enabledInBuild.Length > order.Length)
            {
                report.Warn($"Extra scenes are enabled in the build: {string.Join(", ", enabledInBuild.Skip(order.Length))}");
            }
        }

        private static void CheckMobilePlatforms(SetupReport report)
        {
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                report.Pass("Android Build Support module installed.");
            else
                report.Error("Android Build Support is not installed. Install the Android Build Support module (+ OpenJDK + SDK & NDK Tools) for this editor.");

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP
                && (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0)
                report.Pass("Android: IL2CPP + ARM64.");
            else
                report.Error("Android must use IL2CPP with ARM64 enabled (Google Play requirement). Run the Phase 1 setup.");

            if (PlayerSettings.Android.forceInternetPermission) report.Pass("Android: INTERNET permission forced on.");
            else report.Error("Android INTERNET permission is not forced; Photon will fail to connect on device.");

            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            if (active == BuildTarget.Android) report.Pass($"Active build target: {active}.");
            else report.Info($"Active build target is {active}. Switch to Android via File > Build Profiles when you are ready to build for a phone.");
        }

        private static void CheckPhotonFusion(SetupReport report)
        {
            bool fusionPresent = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Any(t => t.FullName == "Fusion.NetworkRunner");
            if (!fusionPresent)
            {
                report.Error("Photon Fusion 2 SDK not found. Import the Fusion 2.1.x .unitypackage (see docs/PHASE-1-SETUP.md, step 5).");
                return;
            }
            report.Pass("Photon Fusion SDK imported (Fusion.NetworkRunner found).");

            bool hasConfig = AssetDatabase.FindAssets("NetworkProjectConfig")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(path => path.EndsWith(".fusion", StringComparison.OrdinalIgnoreCase));
            if (hasConfig) report.Pass("NetworkProjectConfig.fusion found.");
            else report.Error("NetworkProjectConfig.fusion not found. Open the Fusion Hub (Tools > Fusion > Fusion Hub) so Fusion can create its config.");

            Object appSettings = FindPhotonAppSettings();
            if (appSettings == null)
            {
                report.Error("PhotonAppSettings asset not found. Open the Fusion Hub and enter your App ID.");
                return;
            }

            if (!TryFindSerializedString(appSettings, "AppIdFusion", out string appId))
            {
                report.Warn("Could not locate the 'AppIdFusion' field in PhotonAppSettings (SDK layout may differ). Check the App ID manually in the Fusion Hub.");
            }
            else if (string.IsNullOrWhiteSpace(appId))
            {
                report.Error("Photon Fusion App ID is empty. Create a Fusion 2 app on dashboard.photonengine.com and paste its App ID into the Fusion Hub.");
            }
            else if (!Guid.TryParse(appId.Trim(), out _))
            {
                report.Error($"Photon App ID '{appId}' is not a valid GUID. Copy it again from the Photon dashboard.");
            }
            else report.Pass("Photon Fusion App ID is set.");

            if (TryFindSerializedString(appSettings, "FixedRegion", out string region) && !string.IsNullOrWhiteSpace(region))
            {
                report.Info($"PhotonAppSettings Fixed Region = '{region}'. Leave this empty: room codes carry the host's region (Phase 5).");
            }
        }

        private static Object FindPhotonAppSettings()
        {
            string guid = AssetDatabase.FindAssets("t:PhotonAppSettings").FirstOrDefault()
                          ?? AssetDatabase.FindAssets("PhotonAppSettings").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static bool TryFindSerializedString(Object asset, string propertyName, out string value)
        {
            SerializedProperty iterator = new SerializedObject(asset).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.name == propertyName && iterator.propertyType == SerializedPropertyType.String)
                {
                    value = iterator.stringValue;
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
