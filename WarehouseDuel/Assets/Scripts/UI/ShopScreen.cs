using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Profile;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Shop: spend Stars on costumes (characters), avatars and banners. Stars are earned in online matches
    /// (see <see cref="Shop"/>). Each item shows its picture, price, and OWNED once bought.
    /// </summary>
    public sealed class ShopScreen : MenuScreen
    {
        [System.Serializable]
        public sealed class Item
        {
            public Button buy;
            public TMP_Text label;
            public GameObject owned;
            public GameObject priceIcon;
        }

        [SerializeField] private TMP_Text starsText;
        [SerializeField] private TMP_Text statusText;

        [Header("Tabs: costumes, avatars, banners")]
        [SerializeField] private Button[] tabButtons = new Button[3];
        [SerializeField] private Image[] tabBackgrounds = new Image[3];
        [SerializeField] private TMP_Text[] tabLabels = new TMP_Text[3];
        [SerializeField] private GameObject[] pages = new GameObject[3];

        [SerializeField] private Item[] costumes = new Item[0];
        [SerializeField] private Item[] avatars = new Item[0];
        [SerializeField] private Item[] banners = new Item[0];

        private const string EarnHint = "Earn stars in online matches: +5 per match, +2 per elimination, +15 for a win.";

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => ShowTab(index, tabBackgrounds, tabLabels, pages));
            }
            Hook(costumes, Shop.Category.Costume);
            Hook(avatars, Shop.Category.Avatar);
            Hook(banners, Shop.Category.Banner);
        }

        private void OnEnable()
        {
            Shop.Changed += Refresh;
            ShowTab(0, tabBackgrounds, tabLabels, pages);
            if (statusText != null) statusText.text = EarnHint;
            Refresh();
        }

        private void OnDisable() => Shop.Changed -= Refresh;

        private void Hook(Item[] items, Shop.Category category)
        {
            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                items[i].buy.onClick.AddListener(() => Buy(category, index));
            }
        }

        private void Buy(Shop.Category category, int index)
        {
            if (Shop.Buy(category, index))
            {
                if (statusText != null) statusText.text = "<color=#50D773>Unlocked!</color> Equip it in the Wardrobe or your Profile.";
                WarehouseDuel.Core.Haptics.Medium();
            }
            else if (statusText != null && !Shop.IsOwned(category, index))
            {
                statusText.text = $"<color=#EB503C>Not enough stars.</color> {EarnHint}";
            }
        }

        private void Refresh()
        {
            if (starsText != null) starsText.text = Shop.Stars.ToString();
            Fill(costumes, Shop.Category.Costume);
            Fill(avatars, Shop.Category.Avatar);
            Fill(banners, Shop.Category.Banner);
        }

        private static void Fill(Item[] items, Shop.Category category)
        {
            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                bool owned = Shop.IsOwned(category, i);
                item.owned.SetActive(owned);
                item.buy.gameObject.SetActive(!owned);
                if (owned) continue;
                item.label.text = Shop.Price(category, i).ToString();
                item.buy.interactable = Shop.CanAfford(category, i);
            }
        }
    }
}
