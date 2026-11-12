using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WarehouseDuel.EditorTools
{
    /// <summary>
    /// Bakes the environment lighting (ambient probe + reflection) for a generated scene without lightmaps.
    /// Without this, scenes created from code have no lighting data and fall back to a default bluish ambient,
    /// both in the Editor and in builds. Baked GI and realtime GI stay off (cheap on mobile; all lights are realtime).
    /// </summary>
    internal static class LightingBaker
    {
        private const string SettingsPath = "Assets/Settings/WarehouseDuel_Lighting.lighting";

        public static void BakeEnvironment(Scene scene, SetupReport report)
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "WarehouseDuel_Lighting" };
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.bakedGI = false;
            settings.realtimeGI = false;
            EditorUtility.SetDirty(settings);

            Lightmapping.lightingSettings = settings;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (Lightmapping.Bake())
            {
                EditorSceneManager.SaveScene(scene);
                report.Pass($"Environment lighting baked for {scene.name}.");
            }
            else
            {
                report.Warn($"Environment lighting bake failed for {scene.name}; ambient light may look wrong.");
            }
        }
    }
}
