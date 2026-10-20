using System;

namespace Combat
{
    /// <summary>
    /// Contract for any entity that can take damage and report its vital state:
    /// the player (<c>PlayerSetting</c>), enemies (<c>EnemyLightSettings</c>),
    /// destructible props, etc. Weapons, AI and traps can target the same way
    /// regardless of the concrete class.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>True once health has dropped to zero.</summary>
        bool IsDead { get; }

        /// <summary>Current health (never below zero).</summary>
        float CurrentHealth { get; }

        /// <summary>Maximum health (may grow with level).</summary>
        float MaxHealth { get; }

        /// <summary>Fired whenever current/max health changes. Arguments: current, max.</summary>
        event Action<float, float> OnHealthChanged;

        /// <summary>Fired with the damage actually applied to health.</summary>
        event Action<float> OnDamaged;

        /// <summary>Fired once when the entity dies.</summary>
        event Action OnDeath;

        /// <summary>Applies incoming damage. No-op for non-positive amounts or dead targets.</summary>
        void TakeDamage(float amount);
    }
}