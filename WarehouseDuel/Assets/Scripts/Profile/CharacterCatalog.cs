using System;
using UnityEngine;

namespace WarehouseWar.Profile
{
    /// <summary>
    /// The playable characters: name, role, description, portrait, and the materials that replace the soldier's
    /// base materials (uniform camo, vest, skin, boots, straps, face mask). Accessories (hats, masks, armour...)
    /// are built into every soldier model and switched on by <see cref="Player.SoldierLook"/>.
    /// Lives in Resources so the network code and every scene can resolve a character index.
    /// </summary>
    public sealed class CharacterCatalog : ScriptableObject
    {
        public const string ResourcePath = "CharacterCatalog";

        /// <summary>Material slots of the soldier model, in this order in <see cref="Entry.materials"/>.</summary>
        public enum Slot { Camo, CamoDark, Vest, Skin, Boots, Strap, Mask }

        /// <summary>Base material names the slots replace (used for loose objects such as the first-person hands).</summary>
        public static readonly string[] BaseMaterialNames =
        {
            "M_Soldier_Camo", "M_Soldier_CamoDark", "M_Soldier_Vest", "M_Soldier_Skin", "M_Soldier_Boots", "M_Soldier_Strap", "M_Soldier_Mask",
        };

        [Serializable]
        public sealed class Entry
        {
            public string name;
            public string role;
            [TextArea] public string description;
            public Sprite portrait;
            public Material[] materials = new Material[7]; // null = keep the base material
            public bool helmet = true;
            public bool faceMask = true;

            public Material MaterialFor(Slot slot)
            {
                int i = (int)slot;
                return materials != null && i < materials.Length ? materials[i] : null;
            }
        }

        public Entry[] characters = new Entry[0];

        private static CharacterCatalog instance;

        public static CharacterCatalog Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<CharacterCatalog>(ResourcePath);
                return instance;
            }
        }

        public int Count => characters != null ? characters.Length : 0;

        public Entry Get(int index) => Count == 0 ? null : characters[Mathf.Clamp(index, 0, Count - 1)];

        /// <summary>Swaps base soldier materials on a loose object (e.g. first-person hands) for this character's.</summary>
        public static void ApplyToRenderers(GameObject root, int characterIndex)
        {
            Entry entry = Instance != null ? Instance.Get(characterIndex) : null;
            if (root == null || entry == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material current = renderer.sharedMaterial;
                if (current == null) continue;
                int slot = Array.IndexOf(BaseMaterialNames, current.name);
                if (slot < 0) continue;
                Material replacement = entry.MaterialFor((Slot)slot);
                if (replacement != null) renderer.sharedMaterial = replacement;
            }
        }
    }
}
