using System.Collections;
using Combat;
using UnityEngine;
using WeaponSystem.Shooting;

namespace WeaponSystem.ShootGunWeapon
{
    /// <summary>
    /// Shotgun weapon: characteristics scale with weapon level (1..5). A single trigger pull
    /// launches several pellets from the muzzle (they leave clustered and fan out with
    /// distance). Magazine is fixed at 8 shells; reload inserts one shell at a time.
    /// Level interpolation (level1 -> level5): damage 20 -> 80 (per pellet), spread radius
    /// 7m -> 1m, shell reload time 2s -> 0.5s. All mechanics live in <see cref="WeaponSettings"/>;
    /// this class only overrides the stats, the pellet count and the shell-by-shell reload.
    /// </summary>
    public class ShootGunSetting : WeaponSettings
    {
        [Header("Shot pattern")]
        [Tooltip("Number of pellets fired per trigger pull.")]
        [SerializeField, Range(1, 20)] private int pelletCount = 8;

        public override int MinMagazineSize => 8;
        public override int MaxMagazineSize => 8; // always 8 shells

        public override float MinDamage => 20f; // per pellet
        public override float MaxDamage => 80f; // per pellet

        public override float MinSpreadRadius => 7f;
        public override float MaxSpreadRadius => 1f;

        public override float MinReloadTime => 2f; // per shell
        public override float MaxReloadTime => 0.5f;

        public override float FireInterval => 0.8f;
        public override float MaxRange => 25f;
        public override bool FullAuto => false;

        public override int ProjectilesPerShot => pelletCount;

        // Back-compat aliases, kept for anything reading the old property names.
        public int PelletCount => ProjectilesPerShot;
        public float PelletDamage => Damage;
        public float ShellReloadTime => ReloadTime;

        /// <summary>
        /// Inserts shells one at a time; each shell costs <see cref="ReloadTime"/> seconds.
        /// The base class clears the reload flag and fires OnReloadFinished afterwards.
        /// </summary>
        protected override IEnumerator ReloadSequence()
        {
            while (_currentAmmo < MagazineSize)
            {
                yield return new WaitForSeconds(ReloadTime);
                _currentAmmo++;

                RaiseAmmoChanged();
            }
        }
    }
}