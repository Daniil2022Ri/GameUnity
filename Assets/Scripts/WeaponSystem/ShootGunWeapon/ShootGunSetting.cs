using System;
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
    /// 7m -> 1m, shell reload time 2s -> 0.5s. Every pellet draws a yellow ray and damages
    /// any <see cref="IDamageable"/> it hits. Implements <see cref="IWeapon"/>: input is
    /// driven from outside (e.g. the ShotingPlayerSystem), this component only handles
    /// firing and reloading behaviour.
    /// </summary>
    public class ShootGunSetting : MonoBehaviour, IWeapon
    {
        private const int MaxLevel = 5;
        private const int MagazineCapacity = 8; // always 8 shells

        [Header("Weapon Level")]
        [SerializeField, Range(1, MaxLevel)] private int level = 1;

        [Header("Level-scaled stats (level1 -> level5)")]
        [SerializeField] private float damageMin = 20f;   // per pellet
        [SerializeField] private float damageMax = 80f;   // per pellet
        [SerializeField] private float spreadRadiusMin = 7f;
        [SerializeField] private float spreadRadiusMax = 1f;
        [SerializeField] private float shellReloadTimeMin = 2f;
        [SerializeField] private float shellReloadTimeMax = 0.5f;

        [Header("Shot pattern")]
        [Tooltip("Number of pellets fired per trigger pull.")]
        [SerializeField, Range(1, 20)] private int pelletCount = 8;
        [SerializeField] private float maxRange = 25f;

        [Header("Shooting")]
        [Tooltip("Where shots originate. Falls back to this transform if empty.")]
        [SerializeField] private Transform muzzle;
        [Tooltip("Layers that block bullets (terrain, enemies, props...).")]
        [SerializeField] private LayerMask shootMask = ~0;
        [Tooltip("Seconds between shots.")]
        [SerializeField] private float fireInterval = 0.8f;
        [Tooltip("True keeps shooting while the trigger is held; false - one shot per fresh press.")]
        [SerializeField] private bool fullAuto = false;
        [SerializeField] private Color rayColor = Color.yellow;
        [SerializeField] private float rayDebugTime = 0.3f;

        private int _currentAmmo;
        private bool _isReloading;
        private float _fireCooldown;

        // Level-interpolated, read-only stats.
        public int MagazineSize => MagazineCapacity;
        public int PelletCount => pelletCount;
        public float PelletDamage => Mathf.Lerp(damageMin, damageMax, LevelT);
        public float SpreadRadius => Mathf.Lerp(spreadRadiusMin, spreadRadiusMax, LevelT);
        public float ShellReloadTime => Mathf.Lerp(shellReloadTimeMin, shellReloadTimeMax, LevelT);

        public int Level => level;
        public int CurrentAmmo => _currentAmmo;
        public bool IsReloading => _isReloading;
        public bool FullAuto => fullAuto;

        public bool CanFire => _currentAmmo > 0 && !_isReloading && _fireCooldown <= 0f;
        public bool CanReload => _currentAmmo < MagazineCapacity && !_isReloading;

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
            _currentAmmo = MagazineCapacity;
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

            // All pellets share the muzzle origin but get an independent random offset,
            // so the spray is tight near the barrel and widens with distance.
            for (int i = 0; i < pelletCount; i++)
                ShootRay(ApplySpread(AimDirection));

            OnAmmoChanged?.Invoke(_currentAmmo, MagazineSize);
            OnWeaponFired?.Invoke();
        }

        private Vector3 ApplySpread(Vector3 aimDir)
        {
            float spread = SpreadRadius;
            if (spread <= 0.0001f)
                return aimDir;

            Vector3 center = Origin + aimDir * maxRange;

            Vector3 right = Vector3.Cross(aimDir, Vector3.up);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(aimDir, transform.right);
            right.Normalize();
            Vector3 up = Vector3.Cross(right, aimDir).normalized;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = spread * Mathf.Sqrt(UnityEngine.Random.value);

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
                    target.TakeDamage(PelletDamage);
            }

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

        /// <summary>Inserts shells one at a time; each shell costs ShellReloadTime seconds.</summary>
        private IEnumerator ReloadRoutine()
        {
            while (_currentAmmo < MagazineCapacity)
            {
                yield return new WaitForSeconds(ShellReloadTime);
                _currentAmmo++;

                OnAmmoChanged?.Invoke(_currentAmmo, MagazineSize);
            }

            _isReloading = false;
            OnReloadFinished?.Invoke();
        }

        /// <summary>Changes the weapon level.</summary>
        public void SetLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, MaxLevel);
        }
    }
}
