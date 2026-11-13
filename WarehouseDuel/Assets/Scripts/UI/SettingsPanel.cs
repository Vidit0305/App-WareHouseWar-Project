using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.UI.Widgets;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Settings screen (main menu and in-match pause): music/SFX volume, camera and aim sensitivity,
    /// invert Y, graphics quality, frame-rate cap, control layout and button size. Saved immediately.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider musicVolume;
        [SerializeField] private Slider sfxVolume;
        [SerializeField] private Slider lookSensitivity;
        [SerializeField] private Slider aimSensitivity;
        [SerializeField] private Slider buttonSize;
        [SerializeField] private Toggle invertY;
        [SerializeField] private OptionSelector quality;
        [SerializeField] private OptionSelector frameRate;
        [SerializeField] private OptionSelector layout;
        [SerializeField] private TMP_Text lookValue;
        [SerializeField] private TMP_Text aimValue;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text sfxValue;
        [SerializeField] private TMP_Text buttonSizeValue;

        [Header("Tabs")]
        [SerializeField] private Button[] tabButtons = new Button[0];
        [SerializeField] private Image[] tabBackgrounds = new Image[0];
        [SerializeField] private TMP_Text[] tabLabels = new TMP_Text[0];
        [SerializeField] private GameObject[] pages = new GameObject[0];
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text versionText;

        private static readonly string[] FrameRateOptions = { "30 FPS (battery saver)", "60 FPS" };
        private static readonly string[] LayoutOptions = { "Standard", "Dual fire buttons", "Left-handed" };

        private bool initialising;
        private int tab;

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }
            if (resetButton != null) resetButton.onClick.AddListener(() =>
            {
                GameSettings.ResetToDefaults();
                OnEnable();
            });
            if (versionText != null) versionText.text = $"Version {Application.version}";

            musicVolume.onValueChanged.AddListener(v => { if (!initialising) GameSettings.MusicVolume = v; RefreshLabels(); });
            sfxVolume.onValueChanged.AddListener(v => { if (!initialising) GameSettings.SfxVolume = v; RefreshLabels(); });
            lookSensitivity.onValueChanged.AddListener(v => { if (!initialising) GameSettings.LookSensitivity = v; RefreshLabels(); });
            aimSensitivity.onValueChanged.AddListener(v => { if (!initialising) GameSettings.AimSensitivity = v; RefreshLabels(); });
            buttonSize.onValueChanged.AddListener(v => { if (!initialising) GameSettings.ButtonScale = v; RefreshLabels(); });
            invertY.onValueChanged.AddListener(v => { if (!initialising) GameSettings.InvertY = v; });
            quality.Changed += i => GameSettings.QualityLevel = i;
            frameRate.Changed += i => GameSettings.TargetFrameRate = i == 0 ? 30 : 60;
            layout.Changed += i => GameSettings.Layout = (ControlLayout)i;

            lookSensitivity.minValue = aimSensitivity.minValue = GameSettings.MinSensitivity;
            lookSensitivity.maxValue = aimSensitivity.maxValue = GameSettings.MaxSensitivity;
            buttonSize.minValue = 0.75f;
            buttonSize.maxValue = 1.35f;
        }

        private void OnEnable()
        {
            initialising = true;
            musicVolume.value = GameSettings.MusicVolume;
            sfxVolume.value = GameSettings.SfxVolume;
            lookSensitivity.value = GameSettings.LookSensitivity;
            aimSensitivity.value = GameSettings.AimSensitivity;
            buttonSize.value = GameSettings.ButtonScale;
            invertY.isOn = GameSettings.InvertY;
            quality.SetOptions(QualitySettings.names, GameSettings.QualityLevel);
            frameRate.SetOptions(FrameRateOptions, GameSettings.TargetFrameRate >= 60 ? 1 : 0);
            layout.SetOptions(LayoutOptions, (int)GameSettings.Layout);
            initialising = false;
            RefreshLabels();
            SelectTab(tab);
        }

        private void SelectTab(int index)
        {
            tab = index;
            if (pages.Length > 0) MenuScreen.ShowTab(index, tabBackgrounds, tabLabels, pages);
        }

        private void RefreshLabels()
        {
            if (lookValue != null) lookValue.text = lookSensitivity.value.ToString("0.00");
            if (aimValue != null) aimValue.text = aimSensitivity.value.ToString("0.00");
            if (musicValue != null) musicValue.text = $"{musicVolume.value * 100f:0}%";
            if (sfxValue != null) sfxValue.text = $"{sfxVolume.value * 100f:0}%";
            if (buttonSizeValue != null) buttonSizeValue.text = $"{buttonSize.value * 100f:0}%";
        }
    }
}
