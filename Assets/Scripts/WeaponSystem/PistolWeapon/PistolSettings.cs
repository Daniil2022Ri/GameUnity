using Combat;
using System;
using UnityEngine;
using WeaponSystem.Shooting;

namespace WeaponSystem.PistolWeapon
{
    /// <summary>
    /// Pistol weapon: characteristics scale with weapon level (1..5) and shooting is
    /// raycast based. Stats interpolate linearly from a level-1 value to a level-5 value:
    /// magazine 8 -> 30, damage 10 -> 50, spread radius 7m -> 0m, reload 3s -> 0.5s.
    /// Every shot draws a yellow ray (Debug) and damages any <see cref="IDamageable"/> hit.
    /// Implements <see cref="IWeapon"/>: input is driven from outside (e.g. the
    /// ShotingPlayerSystem), this component only handles firing and reloading behaviour.
    /// </summary>
    public class PistolSettings : MonoBehaviour, IWeapon
    {
        private const int MaxLevel = 5;

        [Header("Weapon Level")]
        [SerializeField, Range(1, MaxLevel)] private int level = 1;

        [Header("Level-scaled stats (level1 -> level5)")]
        [SerializeField] private int magSizeMin = 8;
        [SerializeField] private int magSizeMax = 30;
        [SerializeField] private float damageMin = 10f;
        [SerializeField] private float damageMax = 50f;
        [SerializeField] private float spreadRadiusMin = 7f;   // meters of inaccuracy at the target
        [SerializeField] private float spreadRadiusMax = 0f;   // perfect accuracy at max level
        [SerializeField] private float reloadTimeMin = 3f;
        [SerializeField] private float reloadTimeMax = 0.5f;

        [Header("Shooting")]
        [Tooltip("Where shots originate. Falls back to this transform if empty.")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private float maxRange = 60f;
        [Tooltip("Layers that block bullets (terrain, enemies, props...).")]
        [SerializeField] private LayerMask shootMask = ~0;
        [Tooltip("Seconds between shots when holding the fire button.")]
        [SerializeField] private float fireInterval = 0.2f;
        [SerializeField] private bool fullAuto = false;
        [SerializeField] private Color rayColor = Color.yellow;
        [SerializeField] private float rayDebugTime = 0.3f;

        private int _currentAmmo;
        private bool _isReloading;
        private float _fireCooldown;

        // Level-interpolated, read-only stats.
        public int MagazineSize => Mathf.RoundToInt(Mathf.Lerp(magSizeMin, magSizeMax, LevelT));
        public float Damage => Mathf.Lerp(damageMin, damageMax, LevelT);
        public float SpreadRadius => Mathf.Lerp(spreadRadiusMin, spreadRadiusMax, LevelT);
        public float ReloadTime => Mathf.Lerp(reloadTimeMin, reloadTimeMax, LevelT);

        public int Level => level;
        public int CurrentAmmo => _currentAmmo;
        public bool IsReloading => _isReloading;
        public bool FullAuto => fullAuto;

        public bool CanFire => _currentAmmo > 0 && !_isReloading && _fireCooldown <= 0f;
        public bool CanReload => _currentAmmo < MagazineSize && !_isReloading;

        // Weapon events forwarded to whoever drives this weapon (player system, UI, ...).
        public event Action<int, int> OnAmmoChanged;
        public event Action OnWeaponFired;
        public event Action OnReloadStarted;
        public event Action OnReloadFinished;

        private float LevelT => (level - 1) / (float)(MaxLevel - 1);

        private Vector3 Origin => muzzle != null ? muzzle.position : transform.position;
        private Vector3 AimDirection => muzzle != null ? muzzle.forward : transform.forward;

        private void Awake()
        {
            _currentAmmo = MagazineSize;
        }

        private void Update()
        {
            // The fire cooldown lives in the weapon, so the rate of fire stays the same
            // no matter who drives it (player shooting system, AI, debug scripts, ...).
            if (_fireCooldown > 0f)
                _fireCooldown -= Time.deltaTime;
        }

        public void Fire()
        {
            if (!CanFire)
                return;

            _currentAmmo--;
            _fireCooldown = fireInterval;

            Vector3 direction = ApplySpread(AimDirection);
            ShootRay(direction);

            OnAmmoChanged?.Invoke(_currentAmmo, MagazineSize);
            OnWeaponFired?.Invoke();
        }

        /// <summary>
        /// Offsets the aim by a point chosen uniformly inside a disc of <see cref="SpreadRadius"/>
        /// meters at <see cref="maxRange"/>. Rays fan out from the muzzle, so the spread grows with
        /// distance (clustered near the barrel, wider at the target).
        /// </summary>
        private Vector3 ApplySpread(Vector3 aimDir)
        {
            float spread = SpreadRadius;
            if (spread <= 0.0001f)
                return aimDir;

            Vector3 center = Origin + aimDir * maxRange;

            // Orthonormal basis around the aim direction.
            Vector3 right = Vector3.Cross(aimDir, Vector3.up);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(aimDir, transform.right);
            right.Normalize();
            Vector3 up = Vector3.Cross(right, aimDir).normalized;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = spread * Mathf.Sqrt(UnityEngine.Random.value); // sqrt keeps the disc uniform

            Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            return (center + offset - Origin).normalized;
        }

        private void ShootRay(Vector3 direction)
        {
            float distance = maxRange;

            if (Physics.Raycast(Origin, direction, out RaycastHit hit, distance, shootMask,
                    QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;

                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                    target.TakeDamage(Damage);
            }

            // Yellow aim line for feedback.
            Debug.DrawRay(Origin, direction * distance, rayColor, rayDebugTime);
        }

        public void StartReload()
        {
            if (!CanReload)
                return;

            _isReloading = true;
            OnReloadStarted?.Invoke();
            StartCoroutine(ReloadRoutine());
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            yield return new WaitForSeconds(ReloadTime);
            _currentAmmo = MagazineSize;
            _isReloading = false;

            OnAmmoChanged?.Invoke(_currentAmmo, MagazineSize);
            OnReloadFinished?.Invoke();
        }

        /// <summary>Changes the weapon level and tops the magazine up to the new size.</summary>
        public void SetLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, MaxLevel);
            _currentAmmo = MagazineSize;
        }
    }
}
