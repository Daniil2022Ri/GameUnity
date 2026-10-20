using UnityEngine;
using WeaponSystem.Shooting;

namespace WeaponSystem.MachineGunWeapon
{
    /// <summary>
    /// Machine gun (RPK-74M style): sustained full-auto fire with a large magazine.
    /// The barrel heats up while the trigger is held - extra spread accumulates with every
    /// shot (up to a cap) and slowly cools back down when not firing. Stats interpolate
    /// level 1 -> 5: magazine 75 -> 100, damage 8 -> 25, base spread 4m -> 1.5m,
    /// reload 4s -> 2s. Mechanics live in <see cref="WeaponSettings"/>.
    /// </summary>
    public class MachineGunSettings : WeaponSettings
    {
        [Header("Sustained fire spread")]
        [Tooltip("Extra spread radius, in meters, added per shot while the trigger is held.")]
        [SerializeField] private float spreadPerShot = 0.1f;
        [Tooltip("Maximum extra spread radius the barrel can wander to, in meters.")]
        [SerializeField] private float maxExtraSpread = 2.5f;
        [Tooltip("How fast the extra spread cools down per second when not firing.")]
        [SerializeField] private float spreadDecayPerSecond = 3f;

        private float _heat; // accumulated extra spread, capped at maxExtraSpread

        public override int MinMagazineSize => 75;
        public override int MaxMagazineSize => 100;

        public override float MinDamage => 8f;
        public override float MaxDamage => 25f;

        public override float MinSpreadRadius => 4f;
        public override float MaxSpreadRadius => 1.5f;

        public override float MinReloadTime => 4f;
        public override float MaxReloadTime => 2f;

        public override float FireInterval => 0.08f; // very fast
        public override float MaxRange => 80f;
        public override bool FullAuto => true;

        public override float SpreadRadius
        {
            get { return Mathf.Lerp(MinSpreadRadius, MaxSpreadRadius, LevelT) + _heat; }
        }

        protected override void OnWeaponShot()
        {
            _heat = Mathf.Min(maxExtraSpread, _heat + spreadPerShot);
        }

        protected override void UpdateWeapon()
        {
            // The barrel cools only while not actively firing.
            if (_fireCooldown > 0f)
                return;

            _heat = Mathf.Max(0f, _heat - spreadDecayPerSecond * Time.deltaTime);
        }
    }
}