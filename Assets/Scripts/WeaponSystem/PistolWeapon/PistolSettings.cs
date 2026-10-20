using Combat;
using UnityEngine;
using WeaponSystem.Shooting;

namespace WeaponSystem.PistolWeapon
{
    /// <summary>
    /// Pistol weapon: characteristics scale with weapon level (1..5) and shooting is
    /// raycast based. Stats interpolate linearly from a level-1 value to a level-5 value:
    /// magazine 8 -> 30, damage 10 -> 50, spread radius 7m -> 0m, reload 3s -> 0.5s.
    /// Every shot draws a ray (Debug) and damages any <see cref="IDamageable"/> hit.
    /// Semi-auto: one shot per fresh trigger pull. All mechanics (ammo, cooldown, reload,
    /// spread, events) live in <see cref="WeaponSettings"/>.
    /// </summary>
    public class PistolSettings : WeaponSettings
    {
        public override int MinMagazineSize => 8;
        public override int MaxMagazineSize => 30;

        public override float MinDamage => 10f;
        public override float MaxDamage => 50f;

        public override float MinSpreadRadius => 7f; // meters of inaccuracy at the target
        public override float MaxSpreadRadius => 0f; // perfect accuracy at max level

        public override float MinReloadTime => 3f;
        public override float MaxReloadTime => 0.5f;

        public override float FireInterval => 0.2f;
        public override float MaxRange => 60f;
        public override bool FullAuto => false;

        /// <summary>Leveling up also tops the magazine up to the new size.</summary>
        public override void SetLevel(int newLevel)
        {
            //super.SetLevel(newLevel);
            _currentAmmo = MagazineSize;
        }
    }
}