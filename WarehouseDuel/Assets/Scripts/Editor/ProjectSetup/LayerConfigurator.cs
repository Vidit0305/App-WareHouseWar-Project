using UnityEditor;
using WarehouseDuel.Core;

namespace WarehouseDuel.EditorTools
{
    /// <summary>Writes the project's named layers into ProjectSettings/TagManager.asset.</summary>
    internal static class LayerConfigurator
    {
        public static void Apply(SetupReport report)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                report.Error("Could not open ProjectSettings/TagManager.asset; layers not configured.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            foreach (var (name, index) in GameConstants.Layers.Definitions)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                if (slot.stringValue == name)
                {
                    report.Pass($"Layer {index} = {name}");
                }
                else if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = name;
                    report.Pass($"Layer {index} set to {name}");
                }
                else
                {
                    report.Error($"Layer slot {index} is already used by '{slot.stringValue}'. Rename or move it so '{name}' can use slot {index}.");
                }
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
