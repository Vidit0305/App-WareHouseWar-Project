using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>
    /// App identity art: imports the branding textures and assigns the Android launcher icons
    /// (legacy square, round, and adaptive background/foreground layers). Also turns off the
    /// Unity splash logo (optional for Unity 6 Personal) so the game's own splash shows first.
    /// Source art lives in Assets/Art/Branding (generated from the user's avatar and background).
    /// </summary>
    internal static class BrandingConfigurator
    {
        public const string Folder = "Assets/Art/Branding";
        private const string Square = Folder + "/AppIcon_Square.png";
        private const string Round = Folder + "/AppIcon_Round.png";
        private const string AdaptiveBackground = Folder + "/AppIcon_AdaptiveBackground.png";
        private const string AdaptiveForeground = Folder + "/AppIcon_AdaptiveForeground.png";
        private const string Splash = Folder + "/Splash_Background.png";

        public static void Apply(SetupReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(Square) == null)
            {
                report.Warn($"Branding art not found in {Folder}; app icon unchanged.");
                return;
            }

            Import(Square, sprite: false, maxSize: 1024);
            Import(Round, sprite: true, maxSize: 1024);           // also used as the HUD/lobby avatar
            Import(AdaptiveBackground, sprite: false, maxSize: 512);
            Import(AdaptiveForeground, sprite: false, maxSize: 512);
            Import(Splash, sprite: true, maxSize: 2048);

            var square = AssetDatabase.LoadAssetAtPath<Texture2D>(Square);
            var round = AssetDatabase.LoadAssetAtPath<Texture2D>(Round);
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveBackground);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveForeground);

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { square }, IconKind.Any);
            Assign(AndroidPlatformIconKind.Legacy, square);
            Assign(AndroidPlatformIconKind.Round, round);
            Assign(AndroidPlatformIconKind.Adaptive, background, foreground);
            report.Pass("App icon: legacy, round and adaptive Android icons set from the avatar art.");

            PlayerSettings.SplashScreen.show = false;
            report.Pass("Unity splash logo disabled; the game's own splash screen shows first.");
        }

        private static void Assign(PlatformIconKind kind, params Texture2D[] layers)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (PlatformIcon icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }

        private static void Import(string path, bool sprite, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (sprite)
            {
                // The full-screen splash art: ASTC 6×6 keeps it sharp at a fraction of the size.
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android", overridden = true, maxTextureSize = maxSize,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50,
                });
            }
            importer.SaveAndReimport();
        }
    }
}
