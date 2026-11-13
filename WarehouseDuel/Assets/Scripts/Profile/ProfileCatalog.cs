using UnityEngine;

namespace WarehouseDuel.Profile
{
    /// <summary>
    /// Preset avatars and banners (generated at build time by the editor tooling). Lives in Resources so every
    /// screen, the HUD and the network code can resolve an avatar/banner index without scene wiring.
    /// </summary>
    public sealed class ProfileCatalog : ScriptableObject
    {
        public const string ResourcePath = "ProfileCatalog";

        public Sprite[] avatars = new Sprite[0];
        public Sprite[] banners = new Sprite[0];
        public string[] bannerNames = new string[0];

        private static ProfileCatalog instance;

        public static ProfileCatalog Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<ProfileCatalog>(ResourcePath);
                return instance;
            }
        }

        public int AvatarCount => avatars != null ? avatars.Length : 0;
        public int BannerCount => banners != null ? banners.Length : 0;

        public Sprite Avatar(int index) => AvatarCount == 0 ? null : avatars[Mathf.Clamp(index, 0, AvatarCount - 1)];
        public Sprite Banner(int index) => BannerCount == 0 ? null : banners[Mathf.Clamp(index, 0, BannerCount - 1)];

        public string BannerName(int index) =>
            bannerNames != null && index >= 0 && index < bannerNames.Length ? bannerNames[index] : $"Banner {index + 1}";
    }
}
