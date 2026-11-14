using System;
using UnityEngine;
using WarehouseWar.Profile;

namespace WarehouseWar.Player
{
    /// <summary>
    /// Dresses a soldier model as one of the characters in <see cref="CharacterCatalog"/>: swaps the uniform,
    /// vest, skin, boots, strap and mask materials, shows or hides the standard helmet and face mask, and
    /// switches on that character's accessories (built into the model by the editor tooling, inactive by default).
    /// </summary>
    public sealed class SoldierLook : MonoBehaviour
    {
        [SerializeField] private Renderer[] slotRenderers = new Renderer[0];
        [SerializeField] private int[] rendererSlots = new int[0];
        [SerializeField] private GameObject helmet;
        [SerializeField] private GameObject faceMask;
        [SerializeField] private GameObject[] accessories = new GameObject[0];
        [SerializeField] private int[] accessoryCharacter = new int[0];

        private Material[] baseMaterials;

        /// <summary>The character currently shown, or -1 before the first <see cref="Apply"/>.</summary>
        public int Applied { get; private set; } = -1;

        public event Action Changed;

        /// <param name="catalogOverride">Editor tooling passes the catalog it is building; at runtime leave it null.</param>
        public void Apply(int index, CharacterCatalog catalogOverride = null)
        {
            if (baseMaterials == null)
            {
                baseMaterials = new Material[slotRenderers.Length];
                for (int i = 0; i < slotRenderers.Length; i++) baseMaterials[i] = slotRenderers[i] != null ? slotRenderers[i].sharedMaterial : null;
            }

            CharacterCatalog catalog = catalogOverride != null ? catalogOverride : CharacterCatalog.Instance;
            CharacterCatalog.Entry entry = catalog != null ? catalog.Get(index) : null;
            if (catalog != null) index = Mathf.Clamp(index, 0, Mathf.Max(0, catalog.Count - 1));

            for (int i = 0; i < slotRenderers.Length; i++)
            {
                if (slotRenderers[i] == null) continue;
                Material replacement = entry != null && i < rendererSlots.Length ? entry.MaterialFor((CharacterCatalog.Slot)rendererSlots[i]) : null;
                slotRenderers[i].sharedMaterial = replacement != null ? replacement : baseMaterials[i];
            }
            if (helmet != null) helmet.SetActive(entry == null || entry.helmet);
            if (faceMask != null) faceMask.SetActive(entry == null || entry.faceMask);
            for (int i = 0; i < accessories.Length; i++)
            {
                if (accessories[i] != null) accessories[i].SetActive(i < accessoryCharacter.Length && accessoryCharacter[i] == index);
            }

            Applied = index;
            Changed?.Invoke();
        }
    }
}
