using EnemysBrain.lightEnemys;
using System.Collections;
using UnityEngine;

namespace WeaponSystem.ShootGunWeapon
{
    /// <summary>
    /// Shotgun weapon: characteristics scale with weapon level (1..5). A single trigger pull
    /// launches several pellets from the muzzle (they leave clustered and fan out with
    /// distance). Magazine is fixed at 8 shells; reload inserts one shell at a time.
    /// Level interpolation (level1 -> level5): damage 20 -> 80 (per pellet), spread radius
    /// 7m -> 1m, shell reload time 2s -> 0.5s. Every pellet draws a yellow ray and damages
    /// any <see cref="EnemyLightSettings"/> it hits.
    /// </summary>
    public class ShootGunSetting : MonoBehaviour
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
        [SerializeField] private KeyCode reloadKey = KeyCode.R;
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

        private float LevelT => MaxLevel > 1 ? (level - 1) / (float)(MaxLevel - 1) : 0f;

        private Vector3 Origin => muzzle != null ? muzzle.position : transform.position;
        private Vector3 AimDirection => muzzle != null ? muzzle.forward : transform.forward;

        private void Awake()
        {
            _currentAmmo = MagazineCapacity;
        }

        private void Update()
        {
            HandleInput();

            if (_fireCooldown > 0f)
                _fireCooldown -= Time.deltaTime;
        }

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0) && !_isReloading && _fireCooldown <= 0f)
            {
                if (_currentAmmo > 0)
                {
                    Fire();
                    _fireCooldown = fireInterval;
                }
                else
                {
                    StartReload();
                }
            }

            if (Input.GetKeyDown(reloadKey))
                StartReload();
        }

        public void Fire()
        {
            if (_isReloading || _currentAmmo <= 0)
                return;

            _currentAmmo--;

            // All pellets share the muzzle origin but get an independent random offset,
            // so the spray is tight near the barrel and widens with distance.
            for (int i = 0; i < pelletCount; i++)
                ShootRay(ApplySpread(AimDirection));
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

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = spread * Mathf.Sqrt(Random.value);

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

                var enemy = hit.collider.GetComponentInParent<EnemyLightSettings>();
                if (enemy != null && !enemy.IsDead)
                    enemy.TakeDamage(PelletDamage);
            }

            Debug.DrawRay(Origin, direction * distance, rayColor, rayDebugTime);
        }

        private void StartReload()
        {
            if (_isReloading || _currentAmmo == MagazineCapacity)
                return;

            _isReloading = true;
            StartCoroutine(ReloadRoutine());
        }

        /// <summary>Inserts shells one at a time; each shell costs ShellReloadTime seconds.</summary>
        private IEnumerator ReloadRoutine()
        {
            while (_currentAmmo < MagazineCapacity)
            {
                yield return new WaitForSeconds(ShellReloadTime);
                _currentAmmo++;
            }

            _isReloading = false;
        }

        /// <summary>Changes the weapon level.</summary>
        public void SetLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, MaxLevel);
        }
    }
}
