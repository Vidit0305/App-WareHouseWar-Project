using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Player;
using WarehouseDuel.Profile;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Character select: a card per character on the left, details on the right; the menu soldier in the middle
    /// changes into whichever character you look at. SELECT makes it yours (saved, and shown to your opponent).
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
                PlayerProfile.CharacterIndex = viewing;
                View(viewing);
            });
        }

        private void OnEnable() => View(PlayerProfile.CharacterIndex);

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
            selectButton.interactable = !isChosen;
            selectLabel.text = isChosen ? "SELECTED" : "SELECT";
        }
    }
}
