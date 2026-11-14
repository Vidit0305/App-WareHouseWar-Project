using System;
using Fusion;
using Fusion.Editor;
using Fusion.Photon.Realtime;
using UnityEditor;

namespace WarehouseWar.EditorTools.Gameplay
{
    /// <summary>
    /// Configures Photon Fusion for this game (APIs verified against the Fusion 2.1.4 SDK):
    ///  - makes sure NetworkProjectConfig and PhotonAppSettings assets exist,
    ///  - enables lag compensation (required: HitboxManager removes itself otherwise),
    ///  - leaves Fixed Region empty (room codes carry the host's region),
    ///  - optionally writes the Photon App ID (command line: -photonAppId &lt;id&gt;),
    ///  - rebuilds the networked prefab table.
    /// </summary>
    internal static class FusionConfigurator
    {
        public static void Apply(SetupReport report)
        {
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<NetworkProjectConfigAsset>();
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<PhotonAppSettings>();

            NetworkProjectConfig config = NetworkProjectConfig.Global;
            LagCompensationSettings lag = config.LagCompensation;
            lag.Enabled = true;
            config.LagCompensation = lag;
            NetworkProjectConfigUtilities.SaveGlobalConfig();
            report.Pass("Fusion: lag compensation enabled in NetworkProjectConfig.");

            PhotonAppSettings settings = PhotonAppSettings.Global;
            string fromArgs = ArgValue("-photonAppId");
            if (!string.IsNullOrWhiteSpace(fromArgs))
            {
                settings.AppSettings.AppIdFusion = fromArgs.Trim();
                report.Pass("Fusion: Photon App ID written from the command line.");
            }
            settings.AppSettings.FixedRegion = string.Empty;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            if (Guid.TryParse(settings.AppSettings.AppIdFusion, out _)) report.Pass("Fusion: Photon App ID is set.");
            else report.Error("Fusion: Photon App ID is missing. Pass -photonAppId <id> or set it in Tools > Fusion > Fusion Hub.");
        }

        /// <summary>Registers every prefab with a NetworkObject so Runner.Spawn can find it. Call after prefabs are built.</summary>
        public static void RebuildPrefabTable(SetupReport report)
        {
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            report.Pass("Fusion: network prefab table rebuilt.");
        }

        private static string ArgValue(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
