using System;
using UnityEngine;

namespace EnemysBrain.lightEnemys
{
    /// <summary>
    /// Stats for a "light" (chaser) enemy. All combat values are derived from the
    /// enemy level: each level above 1 boosts every stat by a fixed percent of its
    /// base value. Exposes the damage this enemy deals to the player.
    /// </summary>
    public class EnemyLightSettings : MonoBehaviour
    {
        [Header("Level")]
        [SerializeField, Range(1, 25)] private int lvlEnemyLight = 1;
        [Tooltip("Growth applied to every stat's base value for each level above 1 (0.10 = +10%).")]
        [SerializeField, Range(0f, 1f)] private float statGrowthPerLevel = 0.10f;

        [Header("Base stats (values at level 1)")]
        [SerializeField, Range(1f, 500f)] private float baseHeal = 100f;
        [SerializeField, Range(1f, 10f)] private float baseMoveSpeed = 4f;
        [SerializeField, Range(1f, 50f)] private float baseAttackSpeed = 1f;
        [SerializeField, Range(1f, 1.5f)] private float baseAttackRange = 2f;
        [SerializeField, Range(3f, 35f)] private float baseAttackDamage = 10f;

        // Runtime health.
        private float _currentHealth;

        public int LvlEnemyLight => lvlEnemyLight;
        public bool IsDead => _currentHealth <= 0f;

        public float MaxHealth => ScaleByLevel(baseHeal);
        public float CurrentHealth => _currentHealth;
        public float MoveSpeed => ScaleByLevel(baseMoveSpeed);
        public float AttackRange => ScaleByLevel(baseAttackRange);
        public float AttackDamage => ScaleByLevel(baseAttackDamage);

        /// <summary>Seconds between attacks (derived from attacks-per-second).</summary>
        public float AttackInterval => 1f / Mathf.Max(0.01f, ScaleByLevel(baseAttackSpeed));

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action<float> OnDamaged;
        public event Action OnDeath;

        private void Awake()
        {
            _currentHealth = MaxHealth;
        }

        /// <summary>Base value scaled by the enemy level.</summary>
        private float ScaleByLevel(float baseValue)
        {
            int levelsAboveBase = Mathf.Max(0, lvlEnemyLight - 1);
            return baseValue * (1f + statGrowthPerLevel * levelsAboveBase);
        }

        /// <summary>Applies damage to this enemy and broadcasts health/death events.</summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            OnDamaged?.Invoke(amount);
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

            if (_currentHealth <= 0f)
                OnDeath?.Invoke();
        }
    }
}
