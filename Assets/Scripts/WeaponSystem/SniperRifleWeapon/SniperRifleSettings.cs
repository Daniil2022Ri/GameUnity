using UnityEngine;
using WeaponSystem.Shooting;

namespace WeaponSystem.SniperRifleWeapon
{
    /// <summary>
    /// Sniper rifle (SVD style): the long-range king. Semi-auto, hitscan and perfectly
    /// accurate at every level - zero spread at any range - with a long reach (300m) and
    /// heavy per-shot damage, paid for by a slow fire rate and a small magazine.
    /// Stats interpolate level 1 -> 5: magazine 5 -> 10, damage 80 -> 200, reload 3s -> 1.5s.
    /// Mechanics live in <see cref="WeaponSettings"/>.
    /// </summary>
    public class SniperRifleSettings : WeaponSettings
    {
        public override int MinMagazineSize => 5;
        public override int MaxMagazineSize => 10;

        public override float MinDamage => 80f;
        public override float MaxDamage => 200f;

        public override float MinSpreadRadius => 0f; // perfectly accurate, always
        public override float MaxSpreadRadius => 0f;

        public override float MinReloadTime => 3f;
        public override float MaxReloadTime => 1.5f;

        public override float FireInterval => 1f; // ~60 rounds/min
        public override float MaxRange => 300f;
        public override bool FullAuto => false;
    }
}