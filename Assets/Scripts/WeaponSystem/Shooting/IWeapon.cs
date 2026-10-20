using System;

namespace WeaponSystem.Shooting
{
    /// <summary>
    /// Contract for any player weapon. It only describes behaviour - firing and
    /// reloading. Input is deliberately not part of it, so the same weapon can be
    /// driven by the player's shooting system, an AI or a debug script alike.
    /// The weapon keeps authoritative state (ammo, cooldown, reload) and enforces
    /// its own rules inside <see cref="Fire"/> / <see cref="StartReload"/>.
    /// </summary>
    public interface IWeapon
    {
        /// <summary>Bullets/shells currently in the magazine.</summary>
        int CurrentAmmo { get; }

        /// <summary>Maximum ammo the magazine can hold.</summary>
        int MagazineSize { get; }

        /// <summary>True while a reload is in progress (firing is blocked).</summary>
        bool IsReloading { get; }

        /// <summary>
        /// True when the weapon keeps shooting in bursts while the trigger is held;
        /// false means one shot per fresh trigger press.
        /// </summary>
        bool FullAuto { get; }

        /// <summary>True when a shot may be released right now (has ammo, not reloading, cooldown over).</summary>
        bool CanFire { get; }

        /// <summary>True when a reload can actually start (magazine not full, not already reloading).</summary>
        bool CanReload { get; }

        /// <summary>Fired whenever the ammo counter changes. Arguments: current, max.</summary>
        event Action<int, int> OnAmmoChanged;

        /// <summary>Fired once for every shot actually released.</summary>
        event Action OnWeaponFired;

        /// <summary>A reload has started.</summary>
        event Action OnReloadStarted;

        /// <summary>A reload has finished (magazine is full again).</summary>
        event Action OnReloadFinished;

        /// <summary>Spends one unit of ammo and fires. No-op when <see cref="CanFire"/> is false.</summary>
        void Fire();

        /// <summary>Starts a reload. No-op when <see cref="CanReload"/> is false.</summary>
        void StartReload();
    }
}