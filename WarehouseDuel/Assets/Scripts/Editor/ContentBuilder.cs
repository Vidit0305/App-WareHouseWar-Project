using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WarehouseDuel.Core;
using WarehouseDuel.EditorTools.Audio;
using WarehouseDuel.EditorTools.Effects;
using WarehouseDuel.EditorTools.Map;
using WarehouseDuel.EditorTools.UI;
using WarehouseDuel.EditorTools.Weapons;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.EditorTools
{
    /// <summary>
    /// Generates all non-network content: project settings, audio, UI art, effects, weapons and the warehouse map.
    /// Used by <c>GameBuilder</c> (full build) and runnable on its own from the command line:
    ///   Unity.exe -batchmode -quit -projectPath WarehouseDuel -executeMethod WarehouseDuel.EditorTools.ContentBuilder.BuildContentBatch
    /// </summary>
    public static class ContentBuilder
    {
        public const string EffectsLibraryPath = "Assets/Resources/EffectsLibrary.asset";

        [MenuItem("Tools/Warehouse Duel/Build Content Only (no networking)", priority = 5)]
        public static void BuildContentMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = new SetupReport();
            BuildContent(report, buildMap: true);
            report.PrintToConsole("Content build");
        }

        /// <summary>Phase 1 settings + generated assets. Returns the weapon catalog (null on failure).</summary>
        internal static WeaponCatalog BuildContent(SetupReport report, bool buildMap)
        {
            try
            {
                EnsureTmpEssentials(report);
                FolderStructure.Apply(report);
                PlayerSettingsConfigurator.Apply(report);
                LayerConfigurator.Apply(report);
                SceneBuilder.BuildScenes(report, overwriteExisting: false);
                OutdoorMapBuilder.EnsureScenes(report);
                AssetDatabase.SaveAssets();

                AudioGenerator.Generate(report);
                UiArt.BuildAll(report);
                EffectsBuilder.BuildAll(report);
                BuildEffectsLibrary(report);
                WeaponCatalog catalog = WeaponAssetBuilder.BuildAll(report);
                AssignWeaponIcons(catalog, report);
                if (buildMap)
                {
                    WarehouseMapBuilder.Build(report);
                    OutdoorMapBuilder.Build(report);
                }
                BuildSettingsConfigurator.Apply(report);
                AssetDatabase.SaveAssets();
                return catalog;
            }
            catch (Exception e)
            {
                report.Error($"Content build stopped: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
                return null;
            }
        }

        public static void BuildContentBatch()
        {
            var report = new SetupReport();
            BuildContent(report, buildMap: true);
            report.PrintToConsole("Content build");
            ProjectValidator.Run(showDialog: false);
            EditorApplication.Exit(report.ErrorCount == 0 ? 0 : 1);
        }

        /// <summary>Imports TextMesh Pro's essential resources (default font, settings) from the uGUI package if missing.</summary>
        internal static void EnsureTmpEssentials(SetupReport report)
        {
            if (SceneBuilder.TmpEssentialsImported())
            {
                report.Pass("TextMesh Pro essential resources present.");
                return;
            }
            var ugui = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            string package = ugui != null ? System.IO.Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage") : null;
            if (package == null || !System.IO.File.Exists(package))
            {
                report.Error("TMP Essential Resources.unitypackage not found in the uGUI package.");
                return;
            }
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (SceneBuilder.TmpEssentialsImported()) report.Pass("Imported TextMesh Pro essential resources.");
            else report.Error("TextMesh Pro essential resources did not import.");
        }

        internal static void BuildEffectsLibrary(SetupReport report)
        {
            FolderStructure.EnsureFolder("Assets/Resources");
            var library = AssetDatabase.LoadAssetAtPath<EffectsLibrary>(EffectsLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<EffectsLibrary>();
                AssetDatabase.CreateAsset(library, EffectsLibraryPath);
            }
            library.muzzleFlash = EffectsBuilder.MuzzleFlash;
            library.impactEnvironment = EffectsBuilder.ImpactEnvironment;
            library.impactPlayer = EffectsBuilder.ImpactPlayer;
            library.bulletHole = EffectsBuilder.BulletHole;
            library.elimination = EffectsBuilder.Elimination;
            library.spawn = EffectsBuilder.SpawnShimmer;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            report.Pass($"Effects library: {EffectsLibraryPath}");
        }

        internal static void AssignWeaponIcons(WeaponCatalog catalog, SetupReport report)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                WeaponDefinition weapon = catalog.Get(i);
                if (weapon == null || weapon.worldModelPrefab == null) continue;
                Texture2D texture = UiArt.WeaponSilhouette(weapon.worldModelPrefab, weapon.worldModelPrefab.name.Replace("Weapon_", ""));
                weapon.icon = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(texture));
                EditorUtility.SetDirty(weapon);
            }
            AssetDatabase.SaveAssets();
            report.Pass("Weapon icons rendered from the weapon models.");
        }
    }
}
