using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Player;
using WarehouseDuel.Profile;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Wardrobe: a card per costume (character) on the left, details on the right; the menu soldier in the middle
    /// changes into whichever costume you look at. Owned costumes can be selected (saved, and shown to everyone in
    /// the match); locked ones can be bought with Stars right here.
    /// </summary>
    public sealed class CharacterScreen : MenuScreen
    {
        [SerializeField] private SoldierLook preview;
        [SerializeField] private SoldierAnimator previewAnimator;

        [Header("Cards")]
        [SerializeField] private Button[] cards = new Button[0];
        [SerializeField] private Image[] cardFrames = new Image[0];
        [SerializeField] private Image[] portraits = new Image[0];
        [SerializeField] private TMP_Text[] cardNames = new TMP_Text[0];
        [SerializeField] private GameObject[] selectedTags = new GameObject[0];
        [SerializeField] private GameObject[] lockTags = new GameObject[0];
        [SerializeField] private TMP_Text[] priceTexts = new TMP_Text[0];

        [Header("Details")]
        [SerializeField] private TMP_Text characterName;
        [SerializeField] private TMP_Text characterRole;
        [SerializeField] private TMP_Text characterDescription;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectLabel;

        private int viewing;

        protected override void Awake()
        {
            base.Awake();
            CharacterCatalog catalog = CharacterCatalog.Instance;
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].onClick.AddListener(() => View(index));
                CharacterCatalog.Entry entry = catalog != null ? catalog.Get(i) : null;
                if (entry == null) continue;
                if (i < cardNames.Length) cardNames[i].text = entry.name.ToUpperInvariant();
                if (i < portraits.Length && entry.portrait != null) portraits[i].sprite = entry.portrait;
            }
            selectButton.onClick.AddListener(() =>
            {
                if (!Shop.IsOwned(Shop.Category.Costume, viewing) && !Shop.Buy(Shop.Category.Costume, viewing)) return;
                PlayerProfile.CharacterIndex = viewing;
                View(viewing);
            });
        }

        private void OnEnable()
        {
            Shop.Changed += Refresh;
            View(PlayerProfile.CharacterIndex);
        }

        private void OnDisable() => Shop.Changed -= Refresh;

        private void Refresh() => View(viewing);

        public override void Close()
        {
            if (preview != null) preview.Apply(PlayerProfile.CharacterIndex);
            base.Close();
        }

        private void View(int index)
        {
            viewing = index;
            int chosen = PlayerProfile.CharacterIndex;
            for (int i = 0; i < cards.Length; i++)
            {
                bool on = i == index;
                cardFrames[i].color = on ? new Color(1f, 0.69f, 0.13f, 1f) : new Color(1f, 1f, 1f, 0.08f);
                cardNames[i].color = on ? new Color(0.15f, 0.12f, 0.05f) : Color.white;
                if (i < selectedTags.Length && selectedTags[i] != null) selectedTags[i].SetActive(i == chosen);
                bool locked = !Shop.IsOwned(Shop.Category.Costume, i);
                if (i < lockTags.Length && lockTags[i] != null) lockTags[i].SetActive(locked);
                if (locked && i < priceTexts.Length && priceTexts[i] != null) priceTexts[i].text = Shop.Price(Shop.Category.Costume, i).ToString();
            }

            if (preview != null && preview.Applied != index)
            {
                preview.Apply(index);
                if (previewAnimator != null) previewAnimator.PlayInspect();
            }

            CharacterCatalog.Entry entry = CharacterCatalog.Instance != null ? CharacterCatalog.Instance.Get(index) : null;
            if (entry != null)
            {
                characterName.text = entry.name.ToUpperInvariant();
                characterRole.text = entry.role.ToUpperInvariant();
                characterDescription.text = entry.description;
            }
            bool isChosen = index == chosen;
            bool owned = Shop.IsOwned(Shop.Category.Costume, index);
            if (owned)
            {
                selectButton.interactable = !isChosen;
                selectLabel.text = isChosen ? "SELECTED" : "SELECT";
            }
            else
            {
                int price = Shop.Price(Shop.Category.Costume, index);
                selectButton.interactable = Shop.CanAfford(Shop.Category.Costume, index);
                selectLabel.text = selectButton.interactable ? $"BUY  {price} STARS" : $"NEED {price} STARS";
            }
        }
    }
}
