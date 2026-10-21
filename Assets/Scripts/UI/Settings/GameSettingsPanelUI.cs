using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settings
{
    /// <summary>
    /// Binds the settings panel widgets to <see cref="GameSettings"/>. Every field is optional:
    /// assign only the controls the panel actually has. Values are applied and saved
    /// immediately (no "Apply" button needed); <see cref="resetButton"/> restores defaults.
    ///
    /// The panel repaints itself from <see cref="GameSettings.Changed"/> and guards its own
    /// callbacks, so assigning values from code never causes a feedback loop.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSettingsPanelUI : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private TextMeshProUGUI masterValueLabel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private TextMeshProUGUI musicValueLabel;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI sfxValueLabel;

        [Header("Mouse")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TextMeshProUGUI sensitivityValueLabel;

        [Header("Graphics")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;

        [Header("Actions")]
        [SerializeField] private Button resetButton;

        private bool _refreshing;

        private GameSettings Settings => GameSettings.Instance;

        private void Awake()
        {
            GameSettings settings = Settings;

            ConfigureSlider(masterSlider, 0f, 1f);
            ConfigureSlider(musicSlider, 0f, 1f);
            ConfigureSlider(sfxSlider, 0f, 1f);
            ConfigureSlider(sensitivitySlider, settings.SensitivityRange.x, settings.SensitivityRange.y);

            BuildQualityOptions(settings);

            if (masterSlider != null)
                masterSlider.onValueChanged.AddListener(HandleMasterChanged);
            if (musicSlider != null)
                musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            if (sensitivitySlider != null)
                sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
            if (qualityDropdown != null)
                qualityDropdown.onValueChanged.AddListener(HandleQualityChanged);
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
            if (vsyncToggle != null)
                vsyncToggle.onValueChanged.AddListener(HandleVSyncChanged);
            if (resetButton != null)
                resetButton.onClick.AddListener(HandleReset);
        }

        private void OnEnable()
        {
            Settings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (GameSettings.Exists)
                GameSettings.Instance.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            if (masterSlider != null)
                masterSlider.onValueChanged.RemoveListener(HandleMasterChanged);
            if (musicSlider != null)
                musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            if (sfxSlider != null)
                sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
            if (sensitivitySlider != null)
                sensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
            if (qualityDropdown != null)
                qualityDropdown.onValueChanged.RemoveListener(HandleQualityChanged);
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.RemoveListener(HandleFullscreenChanged);
            if (vsyncToggle != null)
                vsyncToggle.onValueChanged.RemoveListener(HandleVSyncChanged);
            if (resetButton != null)
                resetButton.onClick.RemoveListener(HandleReset);
        }

        /// <summary>Repaints every widget from the current settings without retriggering the setters.</summary>
        public void Refresh()
        {
            if (_refreshing)
                return;

            _refreshing = true;

            GameSettings settings = Settings;

            if (masterSlider != null)
                masterSlider.value = settings.MasterVolume;
            if (masterValueLabel != null)
                masterValueLabel.text = Percent(settings.MasterVolume);

            if (musicSlider != null)
                musicSlider.value = settings.MusicVolume;
            if (musicValueLabel != null)
                musicValueLabel.text = Percent(settings.MusicVolume);

            if (sfxSlider != null)
                sfxSlider.value = settings.SfxVolume;
            if (sfxValueLabel != null)
                sfxValueLabel.text = Percent(settings.SfxVolume);

            if (sensitivitySlider != null)
                sensitivitySlider.value = settings.MouseSensitivity;
            if (sensitivityValueLabel != null)
                sensitivityValueLabel.text = settings.MouseSensitivity.ToString("0.00");

            if (qualityDropdown != null)
                qualityDropdown.value = settings.QualityLevel;
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = settings.Fullscreen;
            if (vsyncToggle != null)
                vsyncToggle.isOn = settings.VSync;

            _refreshing = false;
        }

        private void HandleMasterChanged(float value)
        {
            if (_refreshing)
                return;

            Settings.SetMasterVolume(value);

            if (masterValueLabel != null)
                masterValueLabel.text = Percent(value);
        }

        private void HandleMusicChanged(float value)
        {
            if (_refreshing)
                return;

            Settings.SetMusicVolume(value);

            if (musicValueLabel != null)
                musicValueLabel.text = Percent(value);
        }

        private void HandleSfxChanged(float value)
        {
            if (_refreshing)
                return;

            Settings.SetSfxVolume(value);

            if (sfxValueLabel != null)
                sfxValueLabel.text = Percent(value);
        }

        private void HandleSensitivityChanged(float value)
        {
            if (_refreshing)
                return;

            Settings.SetMouseSensitivity(value);

            if (sensitivityValueLabel != null)
                sensitivityValueLabel.text = value.ToString("0.00");
        }

        private void HandleQualityChanged(int index)
        {
            if (!_refreshing)
                Settings.SetQualityLevel(index);
        }

        private void HandleFullscreenChanged(bool value)
        {
            if (!_refreshing)
                Settings.SetFullscreen(value);
        }

        private void HandleVSyncChanged(bool value)
        {
            if (!_refreshing)
                Settings.SetVSync(value);
        }

        private void HandleReset()
        {
            Settings.ResetToDefaults();
            Refresh();
        }

        private void BuildQualityOptions(GameSettings settings)
        {
            if (qualityDropdown == null)
                return;

            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(settings.QualityNames));
        }

        private static void ConfigureSlider(Slider slider, float min, float max)
        {
            if (slider == null)
                return;

            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
