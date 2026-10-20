using System;
using System.Collections;
using Combat;
using UnityEngine;

namespace WeaponSystem.Shooting
{
    /// <summary>
    /// Shared base for every weapon in the game. Implements the whole
    /// <see cref="IWeapon"/> contract - ammo, fire cooldown, reload, events and level
    /// scaling - plus the hitscan shooting (raycast + spread) all weapons share here.
    /// Subclasses only provide their own stats through virtual getters and can override a
    /// few hooks to change behaviour (pellet count, shell-by-shell reload, spread build-up).
    ///
    /// Because every stat goes through a virtual getter, a weapon is fully described by
    /// its subclass; the inspector only exposes the level and the muzzle/debug fields.
    /// If data-driven balancing is needed later, these getters can be swapped for
    /// ScriptableObject configs without touching the subclass files.
    /// </summary>
    public abstract class WeaponSettings : MonoBehaviour, IWeapon
    {
        private const int MaxLevel = 5;

        [Header("Weapon Level")]
        [SerializeField, Range(1, MaxLevel)] private int level = 1;

        [Header("Shooting")]
        [Tooltip("Where shots originate. Falls back to this transform if empty.")]
        [SerializeField] private Transform muzzle;
        [Tooltip("Layers that block bullets (terrain, enemies, props...).")]
        [SerializeField] private LayerMask shootMask = ~0;
        [SerializeField] private Color rayColor = Color.yellow;
        [SerializeField] private float rayDebugTime = 0.3f;

        // Runtime state. Protected so subclasses can read/alter it in overrides.
        protected int _currentAmmo;
        protected bool _isReloading;
        protected float _fireCooldown;

        // -----------------------------------------------------------------
        // Virtual stats. Each weapon overrides the endpoints it cares about.
        // -----------------------------------------------------------------

        /// <summary>Magazine size at level 1.</summary>
        public virtual int MinMagazineSize => 8;
        /// <summary>Magazine size at max level.</summary>
        public virtual int MaxMagazineSize => 30;
        /// <summary>Damage per projectile at level 1.</summary>
        public virtual float MinDamage => 10f;
        /// <summary>Damage per projectile at max level.</summary>
        public virtual float MaxDamage => 50f;
        /// <summary>Spread radius (meters of inaccuracy at the target) at level 1.</summary>
        public virtual float MinSpreadRadius => 7f;
        /// <summary>Spread radius at max level.</summary>
        public virtual float MaxSpreadRadius => 0f;
        /// <summary>Reload time at level 1 (whole magazine; per shell for shell-fed weapons).</summary>
        public virtual float MinReloadTime => 3f;
        /// <summary>Reload time at max level.</summary>
        public virtual float MaxReloadTime => 0.5f;
        /// <summary>Seconds between trigger pulls (the weapon's own cooldown throttles fire rate).</summary>
        public virtual float FireInterval => 0.2f;
        /// <summary>Maximum raycast distance in meters.</summary>
        public virtual float MaxRange => 60f;
        /// <summary>How many projectiles one trigger pull releases (shotgun: pellets).</summary>
        public virtual int ProjectilesPerShot => 1;
        /// <summary>True = keeps firing while the trigger is held; false = one shot per fresh press.</summary>
        public virtual bool FullAuto => false;

        // -----------------------------------------------------------------
        // Level-scaled, read-only stats (interpolated linearly 1 -> MaxLevel).
        // -----------------------------------------------------------------

        public int MagazineSize => Mathf.RoundToInt(Mathf.Lerp(MinMagazineSize, MaxMagazineSize, LevelT));
        public float Damage => Mathf.Lerp(MinDamage, MaxDamage, LevelT);
        public virtual float SpreadRadius => Mathf.Lerp(MinSpreadRadius, MaxSpreadRadius, LevelT);
        public float ReloadTime => Mathf.Lerp(MinReloadTime, MaxReloadTime, LevelT);

        public int Level => level;
        public int CurrentAmmo => _currentAmmo;
        public bool IsReloading => _isReloading;

        public bool CanFire => _currentAmmo > 0 && !_isReloading && _fireCooldown <= 0f;
        public bool CanReload => _currentAmmo < MagazineSize && !_isReloading;

        // Weapon events forwarded to whoever drives this weapon (player system, UI, ...).
        public event Action<int, int> OnAmmoChanged;
        public event Action OnWeaponFired;
        public event Action OnReloadStarted;
        public event Action OnReloadFinished;

        // C# only lets the declaring class invoke its own events, so subclasses raise them
        // through these helpers (e.g. a shotgun notifying once per inserted shell).
        /// <summary>Notifies listeners that the ammo counter changed. Call after editing ammo.</summary>
        protected void RaiseAmmoChanged() => OnAmmoChanged?.Invoke(_currentAmmo, MagazineSize);

        /// <summary>Notifies listeners that a reload has started.</summary>
        protected void RaiseReloadStarted() => OnReloadStarted?.Invoke();

        /// <summary>Notifies listeners that a reload has finished.</summary>
        protected void RaiseReloadFinished() => OnReloadFinished?.Invoke();

        /// <summary>0 at level 1, 1 at max level.</summary>
        protected float LevelT => (level - 1) / (float)(MaxLevel - 1);

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

            UpdateWeapon();
        }

        public void Fire()
        {
            if (!CanFire)
                return;

            _currentAmmo--;
            _fireCooldown = FireInterval;

            for (int i = 0; i < ProjectilesPerShot; i++)
                ShootRay(ApplySpread(AimDirection));

            RaiseAmmoChanged();
            OnWeaponFired?.Invoke();
            OnWeaponShot();
        }

        /// <summary>Per-frame hook for subclasses (e.g. cooling sustained spread).</summary>
        protected virtual void UpdateWeapon() { }

        /// <summary>Called once after every trigger pull that actually released a shot.</summary>
        protected virtual void OnWeaponShot() { }

        /// <summary>
        /// Offsets the aim by a point chosen uniformly inside a disc of <see cref="SpreadRadius"/>
        /// meters at <see cref="MaxRange"/>. Rays fan out from the muzzle, so the spread grows
        /// with distance (clustered near the barrel, wider at the target).
        /// </summary>
        private Vector3 ApplySpread(Vector3 aimDir)
        {
            float spread = SpreadRadius;
            if (spread <= 0.0001f)
                return aimDir;

            Vector3 center = Origin + aimDir * MaxRange;

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
            float distance = MaxRange;

            if (Physics.Raycast(Origin, direction, out RaycastHit hit, distance, shootMask,
                    QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;

                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                    target.TakeDamage(Damage);
            }

            Debug.DrawRay(Origin, direction * distance, rayColor, rayDebugTime);
        }

        public void StartReload()
        {
            if (!CanReload)
                return;

            _isReloading = true;
            RaiseReloadStarted();
            StartCoroutine(ReloadRoutine());
        }

        /// <summary>
        /// Template for the reload coroutine: <see cref="ReloadSequence"/> fills the magazine,
        /// then the base clears the reload flag and raises the shared events. Not virtual on
        /// purpose - subclasses customise the filling step only, so ammo/finished notifications
        /// can never be forgotten.
        /// </summary>
        private IEnumerator ReloadRoutine()
        {
            yield return ReloadSequence();

            _isReloading = false;
            RaiseAmmoChanged();
            RaiseReloadFinished();
        }

        /// <summary>
        /// Default reload: waits <see cref="ReloadTime"/> and refills the whole magazine.
        /// Shell-fed weapons (shotgun) override this to insert one shell at a time, calling
        /// <see cref="RaiseAmmoChanged"/> after every inserted shell.
        /// </summary>
        protected virtual IEnumerator ReloadSequence()
        {
            yield return new WaitForSeconds(ReloadTime);
            _currentAmmo = MagazineSize;
        }

        /// <summary>Changes the weapon level. Ammo is left untouched.</summary>
        public virtual void SetLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, MaxLevel);
        }
    }
}