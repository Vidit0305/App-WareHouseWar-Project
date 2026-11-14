using UnityEditor;
using UnityEditor.SceneManagement;
using WarehouseDuel.EditorTools.Map;

namespace WarehouseDuel.EditorTools
{
    /// <summary>Menu entry points: Tools > Warehouse Duel.</summary>
    public static class ProjectSetupMenu
    {
        private const string MenuRoot = "Tools/Warehouse Duel/";

        [MenuItem(MenuRoot + "Phase 1 - Run Project Setup", priority = 1)]
        public static void RunPhase1Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var report = new SetupReport();
            FolderStructure.Apply(report);
            PlayerSettingsConfigurator.Apply(report);
            LayerConfigurator.Apply(report);
            SceneBuilder.BuildScenes(report, overwriteExisting: false);
            OutdoorMapBuilder.EnsureScenes(report);
            BuildSettingsConfigurator.Apply(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.PrintToConsole("Phase 1 setup");
            SceneBuilder.OpenStartScene();

            EditorUtility.DisplayDialog("Phase 1 setup finished",
                report.Summary + "\n\nDetails are in the Console. Next: Tools > Warehouse Duel > Validate Project.", "OK");
        }

        [MenuItem(MenuRoot + "Phase 1 - Rebuild Scaffold Scenes (overwrites)", priority = 2)]
        public static void RebuildScaffoldScenes()
        {
            bool confirmed = EditorUtility.DisplayDialog("Rebuild scenes?",
                "This deletes and regenerates SplashScreen, MainMenu, Lobby and WarehouseMap.\n\n" +
                "Any manual edits to those scenes will be lost.", "Rebuild", "Cancel");
            if (!confirmed || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var report = new SetupReport();
            SceneBuilder.BuildScenes(report, overwriteExisting: true);
            OutdoorMapBuilder.EnsureScenes(report);
            BuildSettingsConfigurator.Apply(report);
            AssetDatabase.SaveAssets();
            report.PrintToConsole("Rebuild scaffold scenes");
            SceneBuilder.OpenStartScene();
        }

        [MenuItem(MenuRoot + "Validate Project", priority = 20)]
        public static void ValidateProject() => ProjectValidator.Run(showDialog: true);
    }
}
