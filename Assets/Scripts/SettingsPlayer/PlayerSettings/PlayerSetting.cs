using System;
using Combat;
using UnityEngine;

namespace SettingsPlayer.PlayerSettings
{
    /// <summary>
    /// Holds the player's combat/vital stats: health, stamina and armor.
    /// Handles taking damage (with armor mitigation), sprint stamina drain/regen
    /// and level-based stat growth.
    /// </summary>
    public class PlayerSetting : MonoBehaviour, IDamageable
    {
        [Header("Level")]
        [SerializeField] private int lvlPlayer = 1;
        [Tooltip("Bonus added to every stat's base reserve for each level above 1 (0.015 = 1.5%).")]
        [SerializeField] private float statBonusPerLevel = 0.015f;

        [Header("Base stats (reserve at level 1)")]
        [SerializeField, Range(0f, 1000f)] private float heal = 100f;
        [SerializeField, Range(0f, 1000f)] private float stamina = 100f;
        [SerializeField, Range(0f, 300f)] private float armor = 100f;

        [Header("Damage distribution")]
        [Tooltip("Share of incoming damage absorbed by armor while it still has value.")]
        [Range(0f, 1f)]
        [SerializeField] private float armorDamageFactor = 0.75f;

        [Header("Sprint stamina")]
        [Tooltip("Stamina drained per second while sprinting.")]
        [SerializeField] private float staminaDrainPerSecond = 20f;
        [Tooltip("Stamina regenerated per second while not sprinting.")]
        [SerializeField] private float staminaRegenPerSecond = 12f;
        [Tooltip("Stamina is also drained while moving at walk speed if the player is actually sprinting.")]
        [SerializeField] private bool regenOnlyWhenIdle = false;

        // Current (runtime) values.
        private float _currentHealth;
        private float _currentStamina;
        private float _currentArmor;

        private bool _isSprinting;
        private bool _isMoving;

        public int LvlPlayer => lvlPlayer;
        public bool IsDead => _currentHealth <= 0f;
        public bool IsSprinting => _isSprinting;

        public float CurrentHealth => _currentHealth;
        public float CurrentStamina => _currentStamina;
        public float CurrentArmor => _currentArmor;

        public float MaxHealth => ScaleByLevel(heal);
        public float MaxStamina => ScaleByLevel(stamina);
        public float MaxArmor => ScaleByLevel(armor);

        /// <summary>False when stamina is depleted, so sprinting must be disabled.</summary>
        public bool CanSprint => _currentStamina > 0f;

        public event Action<float, float> OnHealthChanged;   // current, max
        public event Action<float, float> OnStaminaChanged;  // current, max
        public event Action<float, float> OnArmorChanged;    // current, max
        public event Action<float> OnDamaged;
        public event Action OnDeath;
        public event Action<int> OnLevelUp;

        private void Awake()
        {
            // Start each run with full bars.
            _currentHealth = MaxHealth;
            _currentStamina = MaxStamina;
            _currentArmor = MaxArmor;
        }

        private void Start()
        {
            // Broadcast the initial state so listeners (e.g. UI) that subscribed in
            // OnEnable - which runs before this Awake/Start sequence finishes - paint the
            // correct values once. Without this, health/armor bars stay empty until the
            // first damage/heal, while stamina looks fine because it fires every frame.
            BroadcastCurrentState();
        }

        /// <summary>Pushes current stat values through the change events.</summary>
        public void BroadcastCurrentState()
        {
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
            OnStaminaChanged?.Invoke(_currentStamina, MaxStamina);
            OnArmorChanged?.Invoke(_currentArmor, MaxArmor);
            OnLevelUp?.Invoke(lvlPlayer);
        }

        private void Update()
        {
            UpdateStamina();
        }

        /// <summary>
        /// A stat's level-scaled maximum: base reserve + (level - 1) * bonus percent of the base.
        /// </summary>
        private float ScaleByLevel(float baseValue)
        {
            int levelsAboveBase = Mathf.Max(0, lvlPlayer - 1);
            return baseValue * (1f + statBonusPerLevel * levelsAboveBase);
        }

        private void UpdateStamina()
        {
            if (_isSprinting)
            {
                _currentStamina = Mathf.Max(0f, _currentStamina - staminaDrainPerSecond * Time.deltaTime);
                OnStaminaChanged?.Invoke(_currentStamina, MaxStamina);
            }
            else if (!regenOnlyWhenIdle || !_isMoving)
            {
                float regen = staminaRegenPerSecond * Time.deltaTime;
                _currentStamina = Mathf.Min(MaxStamina, _currentStamina + regen);
                OnStaminaChanged?.Invoke(_currentStamina, MaxStamina);
            }
        }

        /// <summary>
        /// Reports sprint state so stamina drain/regen can follow actual movement.
        /// Called by the movement controller every frame.
        /// </summary>
        public void SetSprinting(bool sprinting)
        {
            _isSprinting = sprinting;
        }

        /// <summary>Reports whether the character is moving at all (used for regen gating).</summary>
        public void SetMoving(bool moving)
        {
            _isMoving = moving;
        }

        /// <summary>
        /// Applies incoming damage. While armor holds value, it absorbs <see cref="armorDamageFactor"/>
        /// of the hit and health takes the rest; any armor overflow (armor running out mid-hit) spills
        /// into health. With no armor, all damage goes to health.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            float damageToHealth = amount;

            if (_currentArmor > 0f)
            {
                float toArmor = amount * armorDamageFactor;
                damageToHealth = amount * (1f - armorDamageFactor);

                float absorbed = Mathf.Min(toArmor, _currentArmor);
                _currentArmor -= absorbed;

                // Armor exhausted during this hit -> the remainder still hurts health.
                damageToHealth += toArmor - absorbed;

                OnArmorChanged?.Invoke(_currentArmor, MaxArmor);
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - damageToHealth);

            OnDamaged?.Invoke(damageToHealth);
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

            if (_currentHealth <= 0f)
                OnDeath?.Invoke();
        }

        /// <summary>Restores health, clamped to the level-scaled maximum.</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        /// <summary>Restores armor, clamped to the level-scaled maximum.</summary>
        public void RepairArmor(float amount)
        {
            if (amount <= 0f)
                return;

            _currentArmor = Mathf.Min(MaxArmor, _currentArmor + amount);
            OnArmorChanged?.Invoke(_currentArmor, MaxArmor);
        }

        /// <summary>Restores stamina, clamped to the level-scaled maximum.</summary>
        public void RestoreStamina(float amount)
        {
            if (amount <= 0f)
                return;

            _currentStamina = Mathf.Min(MaxStamina, _currentStamina + amount);
            OnStaminaChanged?.Invoke(_currentStamina, MaxStamina);
        }

        /// <summary>Advances the player by one level, granting the stat bonus.</summary>
        public void LevelUp()
        {
            SetLevel(lvlPlayer + 1);
        }

        /// <summary>
        /// Sets an absolute level. Each level above 1 raises every stat's maximum by the bonus
        /// percent of the base reserve, and tops up the current value by the same gained amount.
        /// </summary>
        public void SetLevel(int newLevel)
        {
            newLevel = Mathf.Max(1, newLevel);
            if (newLevel == lvlPlayer)
                return;

            // Remember the current maximums before the level change.
            float oldMaxHealth = MaxHealth;
            float oldMaxStamina = MaxStamina;
            float oldMaxArmor = MaxArmor;

            lvlPlayer = newLevel;

            // The gain in maximum is added straight to the current pools.
            float gainedHealth = MaxHealth - oldMaxHealth;
            float gainedStamina = MaxStamina - oldMaxStamina;
            float gainedArmor = MaxArmor - oldMaxArmor;

            if (!IsDead)
            {
                _currentHealth = Mathf.Min(MaxHealth, _currentHealth + gainedHealth);
                OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
            }

            _currentStamina = Mathf.Min(MaxStamina, _currentStamina + gainedStamina);
            OnStaminaChanged?.Invoke(_currentStamina, MaxStamina);

            _currentArmor = Mathf.Min(MaxArmor, _currentArmor + gainedArmor);
            OnArmorChanged?.Invoke(_currentArmor, MaxArmor);

            OnLevelUp?.Invoke(lvlPlayer);
        }
    }
}
