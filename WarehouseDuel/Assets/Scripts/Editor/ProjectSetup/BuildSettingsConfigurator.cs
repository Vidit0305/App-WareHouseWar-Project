using System.Collections.Generic;
using UnityEditor;
using WarehouseDuel.Core;

namespace WarehouseDuel.EditorTools
{
    /// <summary>
    /// Puts the game's scenes into the build list in the fixed order defined by
    /// <see cref="GameConstants.Scenes.BuildOrder"/>. Other scenes (e.g. the template's SampleScene)
    /// are kept in the list but disabled so they are not shipped.
    /// </summary>
    internal static class BuildSettingsConfigurator
    {
        public static void Apply(SetupReport report)
        {
            var ordered = new List<EditorBuildSettingsScene>();

            foreach (string sceneName in GameConstants.Scenes.BuildOrder)
            {
                string path = GameConstants.Scenes.PathOf(sceneName);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    report.Error($"Scene {path} does not exist, so the build order is incomplete. Run the setup again after fixing earlier errors.");
                    continue;
                }
                ordered.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (ordered.Exists(s => s.path == existing.path)) continue;
                ordered.Add(new EditorBuildSettingsScene(existing.path, false));
                if (existing.enabled) report.Info($"Disabled unrelated scene in the build list: {existing.path}");
            }

            EditorBuildSettings.scenes = ordered.ToArray();
            report.Pass($"Build scene list: {string.Join(" → ", GameConstants.Scenes.BuildOrder)} (indices 0–{GameConstants.Scenes.BuildOrder.Length - 1}).");
        }
    }
}
