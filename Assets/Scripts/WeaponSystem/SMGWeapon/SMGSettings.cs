using UnityEngine;
using WeaponSystem.Shooting;

namespace WeaponSystem.SMGWeapon
{
    /// <summary>
    /// Submachine gun (MP5 style): close-quarters shredder. Very fast fire rate with light
    /// damage per hit and a medium magazine; spread is noticeable at range, so it shines up
    /// close. Stats interpolate level 1 -> 5: magazine 30 -> 45, damage 6 -> 18,
    /// spread 5m -> 1m, reload 2s -> 0.8s. Mechanics live in <see cref="WeaponSettings"/>.
    ///
    /// A real MP5 is fully automatic - the flag below starts as full-auto and can be switched
    /// to semi-auto in the inspector if you want one shot per trigger pull.
    /// </summary>
    public class SMGSettings : WeaponSettings
    {
        [Tooltip("True - keeps firing while the trigger is held (real MP5). False - one shot per pull.")]
        [SerializeField] private bool fullAuto = true;

        public override int MinMagazineSize => 30;
        public override int MaxMagazineSize => 45;

        public override float MinDamage => 6f;
        public override float MaxDamage => 18f;

        public override float MinSpreadRadius => 5f;
        public override float MaxSpreadRadius => 1f;

        public override float MinReloadTime => 2f;
        public override float MaxReloadTime => 0.8f;

        public override float FireInterval => 0.07f; // ~850 rounds/min
        public override float MaxRange => 60f;
        public override bool FullAuto => fullAuto;
    }
}