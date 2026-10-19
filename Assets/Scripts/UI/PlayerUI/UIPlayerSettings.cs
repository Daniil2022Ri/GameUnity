using SettingsPlayer.PlayerSettings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerUI
{
    /// <summary>
    /// Live HUD for <see cref="PlayerSetting"/>: shows health / stamina / armor bars and
    /// labels plus the player level, updating in real time through the stats' events.
    /// Also blinks a warning image while health drops below a threshold.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPlayerSettings : MonoBehaviour
    {
        [Header("Player source")]
        [Tooltip("Leave empty to auto-find a PlayerSetting in the scene on enable.")]
        [SerializeField] private PlayerSetting playerSetting;
        [SerializeField] private bool autoFindPlayer = true;

        [Header("Bars (set each Image's Image Type = Filled)")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image staminaFill;
        [SerializeField] private Image armorFill;

        [Header("Labels (optional)")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI staminaText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Low health blink")]
        [Tooltip("Image that flashes while health percent is at or below the threshold.")]
        [SerializeField] private Image lowHealthIndicator;
        [Tooltip("Health ratio (0..1) at which the warning starts blinking. 0.10 = 10%.")]
        [Range(0f, 1f)]
        [SerializeField] private float lowHealthThreshold = 0.10f;
        [Tooltip("Seconds between on/off blink toggles.")]
        [SerializeField] private float blinkInterval = 0.4f;

        private bool _hasPlayer;

        private void OnEnable()
        {
            ResolvePlayer();
            Subscribe(true);
            RefreshAll();
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void Update()
        {
            UpdateLowHealthBlink();
            RefreshAll();
        }

        private void ResolvePlayer()
        {
            if (playerSetting == null && autoFindPlayer)
                playerSetting = FindObjectOfType<PlayerSetting>();

            _hasPlayer = playerSetting != null;

            if (!_hasPlayer)
                Debug.LogWarning($"{nameof(UIPlayerSettings)}: no PlayerSetting assigned or found in scene.", this);
        }

        private void Subscribe(bool on)
        {
            if (playerSetting == null)
                return;

            if (on)
            {
                playerSetting.OnHealthChanged += HandleHealth;
                playerSetting.OnStaminaChanged += HandleStamina;
                playerSetting.OnArmorChanged += HandleArmor;
                playerSetting.OnLevelUp += HandleLevel;
            }
            else
            {
                playerSetting.OnHealthChanged -= HandleHealth;
                playerSetting.OnStaminaChanged -= HandleStamina;
                playerSetting.OnArmorChanged -= HandleArmor;
                playerSetting.OnLevelUp -= HandleLevel;
            }
        }

        /// <summary>Pushes the current values into the UI immediately.</summary>
        private void RefreshAll()
        {
            if (!_hasPlayer)
                return;

            HandleHealth(playerSetting.CurrentHealth, playerSetting.MaxHealth);
            HandleStamina(playerSetting.CurrentStamina, playerSetting.MaxStamina);
            HandleArmor(playerSetting.CurrentArmor, playerSetting.MaxArmor);
            HandleLevel(playerSetting.LvlPlayer);
        }

        private void HandleHealth(float current, float max)
        {
            ApplyBar(healthFill, healthText, current, max);
        }

        private void HandleStamina(float current, float max)
        {
            ApplyBar(staminaFill, staminaText, current, max);
        }

        private void HandleArmor(float current, float max)
        {
            ApplyBar(armorFill, armorText, current, max);
        }

        private void HandleLevel(int level)
        {
            if (levelText != null)
                levelText.text = $"LVL {level}";
        }

        private static void ApplyBar(Image fill, TextMeshProUGUI label, float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (fill != null)
                fill.fillAmount = ratio;

            if (label != null)
                label.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void UpdateLowHealthBlink()
        {
            if (lowHealthIndicator == null)
                return;

            if (!_hasPlayer)
            {
                lowHealthIndicator.enabled = false;
                return;
            }

            float max = playerSetting.MaxHealth;
            float percent = max > 0f ? playerSetting.CurrentHealth / max : 0f;
            bool isLow = !playerSetting.IsDead && percent <= lowHealthThreshold;

            // Square-wave blink on an unscaled clock so it keeps flashing even if slowed down.
            bool on = Mathf.FloorToInt(Time.unscaledTime / blinkInterval) % 2 == 0;
            lowHealthIndicator.enabled = isLow && on;
        }
    }
}
