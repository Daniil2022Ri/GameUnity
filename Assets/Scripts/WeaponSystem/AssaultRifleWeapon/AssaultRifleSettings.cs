using UnityEngine;
using WeaponSystem.Shooting;

namespace WeaponSystem.AssaultRifleWeapon
{
    /// <summary>
    /// Assault rifle (AK-74M style): the all-rounder. Full-auto, medium magazine and damage,
    /// decent reach and moderate spread. Stats interpolate level 1 -> 5: magazine 30 -> 45,
    /// damage 15 -> 40 per hit, spread 2m -> 0.5m, reload 2.5s -> 1s. All mechanics live in
    /// <see cref="WeaponSettings"/>; this class only defines its own stats.
    /// </summary>
    public class AssaultRifleSettings : WeaponSettings
    {
        public override int MinMagazineSize => 30;
        public override int MaxMagazineSize => 45;

        public override float MinDamage => 15f;
        public override float MaxDamage => 40f;

        public override float MinSpreadRadius => 2f;
        public override float MaxSpreadRadius => 0.5f;

        public override float MinReloadTime => 2.5f;
        public override float MaxReloadTime => 1f;

        public override float FireInterval => 0.1f; // ~600 rounds/min
        public override float MaxRange => 100f;
        public override bool FullAuto => true;
    }
}