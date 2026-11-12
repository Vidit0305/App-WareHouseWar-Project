using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace WarehouseDuel.EditorTools
{
    /// <summary>Creates the agreed project folder layout (idempotent — existing folders are left alone).</summary>
    internal static class FolderStructure
    {
        public static readonly string[] All =
        {
            "Assets/Art/Characters",
            "Assets/Art/Environment",
            "Assets/Art/Weapons",
            "Assets/Art/Materials",
            "Assets/Art/Textures",
            "Assets/Audio/Music",
            "Assets/Audio/SFX",
            "Assets/Prefabs/Players",
            "Assets/Prefabs/Weapons",
            "Assets/Prefabs/Environment",
            "Assets/Prefabs/UI",
            "Assets/Scenes",
            "Assets/Scripts/Core",
            "Assets/Scripts/Player",
            "Assets/Scripts/Weapons",
            "Assets/Scripts/Multiplayer",
            "Assets/Scripts/Match",
            "Assets/Scripts/UI",
            "Assets/Scripts/Audio",
            "Assets/Scripts/Environment",
            "Assets/Scripts/Editor",
            "Assets/ScriptableObjects/Weapons",
            "Assets/ScriptableObjects/GameSettings",
            "Assets/UI/Fonts",
            "Assets/UI/Icons",
            "Assets/UI/Sprites",
            "Assets/Settings",
        };

        public static void Apply(SetupReport report)
        {
            int created = All.Count(EnsureFolder);
            report.Pass(created == 0
                ? "Folder structure already complete."
                : $"Created {created} missing folder(s).");
        }

        public static IEnumerable<string> Missing() => All.Where(path => !AssetDatabase.IsValidFolder(path));

        /// <summary>Creates <paramref name="path"/> and any missing parents. Returns true if anything was created.</summary>
        public static bool EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (path == "Assets" || AssetDatabase.IsValidFolder(path)) return false;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
            return true;
        }
    }
}
