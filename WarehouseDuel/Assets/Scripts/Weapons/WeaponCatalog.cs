using UnityEngine;

namespace WarehouseDuel.Weapons
{
    /// <summary>
    /// Ordered list of the weapons available in a match. The index in this list is what goes over the
    /// network (one byte), so host and client builds must share the same catalog order.
    /// </summary>
    [CreateAssetMenu(menuName = "Warehouse Duel/Weapon Catalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        public const int MaxWeapons = 4;

        [SerializeField] private WeaponDefinition[] weapons = new WeaponDefinition[MaxWeapons];

        public int Count => weapons.Length;

        public WeaponDefinition Get(int index) =>
            weapons.Length == 0 ? null : weapons[Mathf.Clamp(index, 0, weapons.Length - 1)];

        public bool IsValidIndex(int index) => index >= 0 && index < weapons.Length && weapons[index] != null;

        public void SetWeapons(WeaponDefinition[] definitions) => weapons = definitions;
    }
}
