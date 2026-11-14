using UnityEngine;

namespace WarehouseDuel.Weapons
{
    /// <summary>
    /// Ordered list of the weapons available in a match. The index in this list is what goes over the
    /// network (one byte), so host and client builds must share the same catalog order.
    /// Order: 0 Rifle, 1 SMG, 2 Shotgun, 3 Pistol, 4 Sniper, 5 Machine Gun, 6 Knife, 7 RPG.
    /// </summary>
    [CreateAssetMenu(menuName = "Warehouse Duel/Weapon Catalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        public const int MaxWeapons = 8;

        // Fixed catalog indices (network ids). Never reorder: 0..3 predate the bigger arsenal.
        public const int Rifle = 0;
        public const int Smg = 1;
        public const int Shotgun = 2;
        public const int Pistol = 3;
        public const int Sniper = 4;
        public const int MachineGun = 5;
        public const int Knife = 6;
        public const int Rpg = 7;

        /// <summary>Weapons every player carries besides their primary (see <see cref="WeaponController"/> loadout).</summary>
        public const int SidearmIndex = Pistol;
        public const int MeleeIndex = Knife;

        [SerializeField] private WeaponDefinition[] weapons = new WeaponDefinition[MaxWeapons];

        public int Count => weapons.Length;

        public WeaponDefinition Get(int index) =>
            weapons.Length == 0 ? null : weapons[Mathf.Clamp(index, 0, weapons.Length - 1)];

        /// <summary>Like <see cref="Get"/> but null for an index outside the catalog (no clamping).</summary>
        public WeaponDefinition GetOrNull(int index) => IsValidIndex(index) ? weapons[index] : null;

        public bool IsValidIndex(int index) => index >= 0 && index < weapons.Length && weapons[index] != null;

        public void SetWeapons(WeaponDefinition[] definitions) => weapons = definitions;
    }
}
