namespace WarehouseWar.Core
{
    /// <summary>
    /// Project-wide structural identifiers (scene names, layers, PlayerPrefs keys).
    /// Gameplay tuning values (damage, match length, respawn time...) do NOT belong here —
    /// they live in ScriptableObjects under Assets/ScriptableObjects so designers can change them.
    /// </summary>
    public static class GameConstants
    {
        /// <summary>Display name used for the product name and title screens. Change it here to rebrand.</summary>
        public const string GameTitle = "Warehouse War";

        public static class Scenes
        {
            public const string SplashScreen = "SplashScreen";
            public const string MainMenu = "MainMenu";
            public const string Lobby = "Lobby";
            public const string WarehouseMap = "WarehouseMap";
            public const string DesertMap = "DesertMap";
            public const string ForestMap = "ForestMap";
            public const string SnowMap = "SnowMap";

            /// <summary>Playable maps; the index is the map id sent over the network (RoomState.Map).</summary>
            public static readonly string[] Maps = { WarehouseMap, DesertMap, ForestMap, SnowMap };
            public static readonly string[] MapNames = { "Warehouse", "Desert", "Forest", "Snow" };
            /// <summary>Map choice value meaning "pick one at random when the match starts".</summary>
            public const int RandomMap = 255;

            public static bool IsMap(string sceneName) => System.Array.IndexOf(Maps, sceneName) >= 0;

            public const string Folder = "Assets/Scenes";

            /// <summary>
            /// Build-index order. Photon Fusion refers to networked scenes by build index
            /// (SceneRef.FromIndex), so host and client builds MUST share this exact order.
            /// </summary>
            public static readonly string[] BuildOrder = { SplashScreen, MainMenu, Lobby, WarehouseMap, DesertMap, ForestMap, SnowMap };

            public static string PathOf(string sceneName) => $"{Folder}/{sceneName}.unity";
        }

        public static class Layers
        {
            /// <summary>Static level geometry: walls, floor, containers, crates. Blocks movement and bullets.</summary>
            public const string Environment = "Environment";

            /// <summary>Player character controllers (movement collision only).</summary>
            public const string Player = "Player";

            /// <summary>Fusion lag-compensated hitboxes. Used only by hit queries, never by physics movement.</summary>
            public const string Hitbox = "Hitbox";

            /// <summary>Local first-person arms/weapon model, rendered on top so it never clips into walls.</summary>
            public const string FirstPersonView = "FirstPersonView";

            /// <summary>
            /// Fixed user-layer slots. Slots 8+ are user layers in every supported Unity version.
            /// </summary>
            public static readonly (string name, int index)[] Definitions =
            {
                (Environment, 8),
                (Player, 9),
                (Hitbox, 10),
                (FirstPersonView, 11),
            };
        }

        public static class PrefKeys
        {
            public const string PlayerName = "wd.playerName";
            public const string TargetFrameRate = "wd.targetFrameRate";
        }

        public static class Defaults
        {
            public const int TargetFrameRate = 60;
            public const int MinFrameRate = 30;
            public const int MaxFrameRate = 120;
        }
    }
}
