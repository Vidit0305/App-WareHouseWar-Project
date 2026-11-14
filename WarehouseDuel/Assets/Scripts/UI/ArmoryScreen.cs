using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseWar.Core;
using WarehouseWar.Player;
using WarehouseWar.Weapons;

namespace WarehouseWar.UI
{
    /// <summary>
    /// Armory: browse every weapon (the menu soldier swaps to the one you look at), compare stats and equip up to
    /// three of them. The three loadout slots are what you carry into every match (and what the lobby shows).
    /// Tap a slot, pick a weapon, press EQUIP; equipping a weapon that is already in another slot swaps the two.
    /// </summary>
    public sealed class ArmoryScreen : MenuScreen
    {
        [SerializeField] private WeaponCatalog catalog;
        [SerializeField] private SoldierAnimator character;

        [Header("Weapon list")]
        [SerializeField] private Button[] weaponTiles = new Button[8];
        [SerializeField] private Image[] tileFrames = new Image[8];
        [SerializeField] private Image[] tileIcons = new Image[8];
        [SerializeField] private TMP_Text[] tileNames = new TMP_Text[8];
        [SerializeField] private GameObject[] equippedTags = new GameObject[8];
        [SerializeField] private TMP_Text[] equippedTagTexts = new TMP_Text[8];

        [Header("Loadout slots")]
        [SerializeField] private Button[] slotButtons = new Button[3];
        [SerializeField] private Image[] slotFrames = new Image[3];
        [SerializeField] private Image[] slotIcons = new Image[3];
        [SerializeField] private TMP_Text[] slotNames = new TMP_Text[3];

        [Header("Details")]
        [SerializeField] private TMP_Text weaponName;
        [SerializeField] private TMP_Text weaponDescription;
        [SerializeField] private RectTransform[] statBars = new RectTransform[4]; // damage, fire rate, range, magazine
        [SerializeField] private TMP_Text[] statValues = new TMP_Text[4];
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;

        private static readonly Color Gold = new Color(1f, 0.69f, 0.13f, 1f);
        private static readonly Color Ink = new Color(0.15f, 0.12f, 0.05f);

        private int selected;
        private int activeSlot;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < weaponTiles.Length; i++)
            {
                int index = i;
                weaponTiles[i].onClick.AddListener(() => Select(index));
                WeaponDefinition def = catalog != null ? catalog.Get(i) : null;
                if (def == null) continue;
                tileNames[i].text = def.displayName.ToUpperInvariant();
                if (def.icon != null) tileIcons[i].sprite = def.icon;
            }
            for (int s = 0; s < slotButtons.Length; s++)
            {
                int slot = s;
                if (slotButtons[s] != null) slotButtons[s].onClick.AddListener(() => SelectSlot(slot));
            }
            equipButton.onClick.AddListener(() =>
            {
                GameSettings.SetLoadoutWeapon(activeSlot, selected);
                if (character != null) character.SetLoadout(GameSettings.Loadout, selected);
                Refresh();
            });
        }

        private void OnEnable()
        {
            activeSlot = 0;
            Select(GameSettings.Loadout[0]);
        }

        public override void Close()
        {
            int[] loadout = GameSettings.Loadout;
            if (character != null) character.SetLoadout(loadout, loadout[0]);
            base.Close();
        }

        private void SelectSlot(int slot)
        {
            activeSlot = Mathf.Clamp(slot, 0, GameSettings.LoadoutSize - 1);
            Select(GameSettings.Loadout[activeSlot]);
        }

        private void Select(int index)
        {
            selected = index;
            if (character != null) character.SetLoadout(GameSettings.Loadout, index);
            Refresh();
        }

        private void Refresh()
        {
            int[] loadout = GameSettings.Loadout;
            for (int i = 0; i < weaponTiles.Length; i++)
            {
                bool isSelected = i == selected;
                tileFrames[i].color = isSelected ? Gold : new Color(1f, 1f, 1f, 0.08f);
                tileNames[i].color = isSelected ? Ink : Color.white;
                tileIcons[i].color = isSelected ? Ink : Color.white;
                int slot = System.Array.IndexOf(loadout, i);
                if (equippedTags[i] != null) equippedTags[i].SetActive(slot >= 0);
                if (slot >= 0 && i < equippedTagTexts.Length && equippedTagTexts[i] != null) equippedTagTexts[i].text = $"SLOT {slot + 1}";
            }

            for (int s = 0; s < slotButtons.Length && s < loadout.Length; s++)
            {
                bool active = s == activeSlot;
                WeaponDefinition def = catalog != null ? catalog.Get(loadout[s]) : null;
                if (slotFrames[s] != null) slotFrames[s].color = active ? Gold : new Color(1f, 1f, 1f, 0.1f);
                if (slotIcons[s] != null)
                {
                    slotIcons[s].sprite = def != null ? def.icon : null;
                    slotIcons[s].enabled = def != null && def.icon != null;
                    slotIcons[s].color = active ? Ink : Color.white;
                }
                if (slotNames[s] != null)
                {
                    slotNames[s].text = def != null ? def.displayName.ToUpperInvariant() : "EMPTY";
                    slotNames[s].color = active ? Ink : Color.white;
                }
            }

            WeaponDefinition weapon = catalog != null ? catalog.Get(selected) : null;
            if (weapon == null) return;
            weaponName.text = weapon.displayName.ToUpperInvariant();
            weaponDescription.text = weapon.description;
            SetStat(0, weapon.DisplayDamage / 100f, weapon.pellets > 1 ? $"{weapon.damage:0}×{weapon.pellets}" : weapon.IsExplosive ? $"{weapon.DisplayDamage:0} SPLASH" : $"{weapon.DisplayDamage:0}");
            SetStat(1, weapon.roundsPerMinute / 900f, weapon.IsMelee ? $"{weapon.roundsPerMinute:0} SLASH" : $"{weapon.roundsPerMinute:0} RPM");
            SetStat(2, weapon.DisplayRange / 120f, weapon.DisplayRange < 10f ? $"{weapon.DisplayRange:0.#} m" : $"{weapon.DisplayRange:0} m");
            SetStat(3, weapon.usesAmmo ? weapon.magazineSize / 32f : 1f, weapon.usesAmmo ? weapon.magazineSize.ToString() : "∞");

            int inSlot = System.Array.IndexOf(loadout, selected);
            bool equippedHere = inSlot == activeSlot;
            equipButton.interactable = !equippedHere;
            equipLabel.text = equippedHere ? $"EQUIPPED · SLOT {activeSlot + 1}"
                : inSlot >= 0 ? $"MOVE TO SLOT {activeSlot + 1}"
                : $"EQUIP TO SLOT {activeSlot + 1}";
        }

        private void SetStat(int index, float normalised, string value)
        {
            if (index < statBars.Length && statBars[index] != null)
            {
                Vector2 max = statBars[index].anchorMax;
                max.x = Mathf.Clamp(normalised, 0.05f, 1f);
                statBars[index].anchorMax = max;
            }
            if (index < statValues.Length && statValues[index] != null) statValues[index].text = value;
        }
    }
}
