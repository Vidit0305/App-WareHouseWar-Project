using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.Player;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Armory: browse the four weapons (the menu soldier swaps to the one you look at), compare stats,
    /// and equip one as your default lobby weapon.
    /// </summary>
    public sealed class ArmoryScreen : MenuScreen
    {
        [SerializeField] private WeaponCatalog catalog;
        [SerializeField] private SoldierAnimator character;

        [Header("Weapon list")]
        [SerializeField] private Button[] weaponTiles = new Button[4];
        [SerializeField] private Image[] tileFrames = new Image[4];
        [SerializeField] private Image[] tileIcons = new Image[4];
        [SerializeField] private TMP_Text[] tileNames = new TMP_Text[4];
        [SerializeField] private GameObject[] equippedTags = new GameObject[4];

        [Header("Details")]
        [SerializeField] private TMP_Text weaponName;
        [SerializeField] private TMP_Text weaponDescription;
        [SerializeField] private RectTransform[] statBars = new RectTransform[4]; // damage, fire rate, range, magazine
        [SerializeField] private TMP_Text[] statValues = new TMP_Text[4];
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;

        private int selected;

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
            equipButton.onClick.AddListener(() =>
            {
                GameSettings.PreferredWeapon = selected;
                Select(selected);
            });
        }

        private void OnEnable() => Select(GameSettings.PreferredWeapon);

        public override void Close()
        {
            if (character != null) character.SetWeapon(GameSettings.PreferredWeapon);
            base.Close();
        }

        private void Select(int index)
        {
            selected = index;
            int equipped = GameSettings.PreferredWeapon;
            for (int i = 0; i < weaponTiles.Length; i++)
            {
                tileFrames[i].color = i == index ? new Color(1f, 0.69f, 0.13f, 1f) : new Color(1f, 1f, 1f, 0.08f);
                tileNames[i].color = i == index ? new Color(0.15f, 0.12f, 0.05f) : Color.white;
                tileIcons[i].color = i == index ? new Color(0.15f, 0.12f, 0.05f) : Color.white;
                if (equippedTags[i] != null) equippedTags[i].SetActive(i == equipped);
            }
            if (character != null) character.SetWeapon(index);

            WeaponDefinition def = catalog != null ? catalog.Get(index) : null;
            if (def == null) return;
            weaponName.text = def.displayName.ToUpperInvariant();
            weaponDescription.text = def.description;
            SetStat(0, def.DisplayDamage / 100f, def.pellets > 1 ? $"{def.damage:0}×{def.pellets}" : def.IsExplosive ? $"{def.DisplayDamage:0} SPLASH" : $"{def.DisplayDamage:0}");
            SetStat(1, def.roundsPerMinute / 900f, def.IsMelee ? $"{def.roundsPerMinute:0} SLASH" : $"{def.roundsPerMinute:0} RPM");
            SetStat(2, def.DisplayRange / 60f, def.DisplayRange < 10f ? $"{def.DisplayRange:0.#} m" : $"{def.DisplayRange:0} m");
            SetStat(3, def.usesAmmo ? def.magazineSize / 32f : 1f, def.usesAmmo ? def.magazineSize.ToString() : "∞");

            bool isEquipped = index == equipped;
            equipButton.interactable = !isEquipped;
            equipLabel.text = isEquipped ? "EQUIPPED" : "EQUIP";
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
