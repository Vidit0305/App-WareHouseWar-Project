using UnityEngine;
using WarehouseWar.Profile;
using WarehouseWar.Weapons;

namespace WarehouseWar.Bots
{
    /// <summary>Names, looks and loadouts for practice bots. Picked once per bot when it joins the room.</summary>
    public static class BotRoster
    {
        private static readonly string[] Names =
        {
            "Viper", "Ghost", "Blaze", "Rook", "Nova", "Havoc", "Rogue", "Specter",
            "Talon", "Onyx", "Jinx", "Saber", "Cobra", "Raven", "Frost", "Bishop",
        };

        /// <summary>Primary weapons a bot may pick (the rocket launcher is rarer: it appears once in a long list).</summary>
        private static readonly int[] Primaries =
        {
            WeaponCatalog.Rifle, WeaponCatalog.Rifle, WeaponCatalog.Smg, WeaponCatalog.Smg, WeaponCatalog.Shotgun,
            WeaponCatalog.Sniper, WeaponCatalog.MachineGun, WeaponCatalog.MachineGun, WeaponCatalog.Rpg,
        };

        private static int[] order;

        /// <summary>Bot number <paramref name="botIndex"/>'s profile. Index 0 reshuffles the names for a new practice session.</summary>
        public static void Pick(int botIndex, out string name, out byte weapon, out byte weapon2, out byte weapon3,
            out byte character, out byte avatar, out byte banner, out byte title, out byte level)
        {
            if (order == null || botIndex == 0) Shuffle();
            name = Names[order[botIndex % order.Length]];

            int primary = Primaries[Random.Range(0, Primaries.Length)];
            int second = WeaponCatalog.Pistol;
            if (Random.value < 0.35f)
            {
                // Sometimes a second long gun instead of the pistol.
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    int candidate = Primaries[Random.Range(0, Primaries.Length)];
                    if (candidate == primary) continue;
                    second = candidate;
                    break;
                }
            }
            weapon = (byte)primary;
            weapon2 = (byte)second;
            weapon3 = (byte)WeaponCatalog.Knife;

            CharacterCatalog characters = CharacterCatalog.Instance;
            int characterCount = characters != null && characters.Count > 0 ? characters.Count : 10;
            character = (byte)Random.Range(0, characterCount);

            ProfileCatalog profiles = ProfileCatalog.Instance;
            int avatars = profiles != null && profiles.AvatarCount > 0 ? profiles.AvatarCount : 1;
            int banners = profiles != null && profiles.BannerCount > 0 ? profiles.BannerCount : 1;
            avatar = (byte)Random.Range(0, Mathf.Min(avatars, 64));
            banner = (byte)Random.Range(0, Mathf.Min(banners, 64));
            title = (byte)Random.Range(0, 4);
            level = (byte)Random.Range(3, 40);
        }

        private static void Shuffle()
        {
            order = new int[Names.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }
    }
}
