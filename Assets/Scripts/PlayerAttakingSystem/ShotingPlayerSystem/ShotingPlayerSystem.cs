using System;
using UnityEngine;
using WeaponSystem.Shooting;

namespace PlayerAttakingSystem.ShotingPlayerSystem
{
    /// <summary>
    /// Central player shooting controller. It reads fire/reload input and forwards it
    /// to the currently equipped <see cref="IWeapon"/>, starts a reload automatically
    /// when the trigger is pulled on an empty magazine, and re-broadcasts weapon events
    /// so the UI can be fed from a single place. Weapons themselves stay input-agnostic.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShotingPlayerSystem : MonoBehaviour
    {
        [Header("Weapon")]
        [Tooltip("Object that carries the weapon component (implements IWeapon). " +
                 "Leave empty and keep autoFindWeapon on to resolve the weapon from this object or its children.")]
        [SerializeField] private GameObject weaponObject;
        [SerializeField] private bool autoFindWeapon = true;

        [Header("Input")]
        [Tooltip("Fire button. Full-auto weapons shoot while it is held, semi-auto on a fresh press.")]
        [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode reloadKey = KeyCode.R;
        [Tooltip("Start reloading automatically when the fire button is used on an empty magazine.")]
        [SerializeField] private bool autoReloadOnEmpty = true;

        private IWeapon _weapon;

        public IWeapon CurrentWeapon => _weapon;
        public bool HasWeapon => _weapon != null;
        public int CurrentAmmo => _weapon?.CurrentAmmo ?? 0;
        public int MagazineSize => _weapon?.MagazineSize ?? 0;
        public bool IsReloading => _weapon?.IsReloading ?? false;

        /// <summary>Fired when the equipped weapon changes (non-null after a successful switch).</summary>
        public event Action<IWeapon> OnWeaponChanged;

        // Mirrors of the weapon events, kept here so the UI subscribes to one source.
        public event Action<int, int> OnAmmoChanged; // current, max
        public event Action OnWeaponFired;
        public event Action OnReloadStarted;
        public event Action OnReloadFinished;

        private void Awake()
        {
            if (weaponObject != null)
                Equip(weaponObject);
            else if (autoFindWeapon)
                Equip(gameObject);
        }

        private void Start()
        {
            // UI subscribing in OnEnable (after this Awake) may have missed the initial
            // broadcast, so repeat the current state once the scene has actually started.
            if (HasWeapon)
                OnAmmoChanged?.Invoke(CurrentAmmo, MagazineSize);
        }

        private void OnDestroy()
        {
            Unequip();
        }

        private void Update()
        {
            if (!HasWeapon)
                return;

            HandleFireInput();
            HandleReloadInput();
        }

        private void HandleFireInput()
        {
            if (_weapon.FullAuto)
            {
                // Fire every frame while held; the weapon's own cooldown throttles the fire rate.
                if (Input.GetKey(fireKey))
                    AttemptFire();
            }
            else if (Input.GetKeyDown(fireKey))
            {
                AttemptFire();
            }
        }

        private void HandleReloadInput()
        {
            if (Input.GetKeyDown(reloadKey))
                _weapon.StartReload(); // the weapon itself guards CanReload
        }

        private void AttemptFire()
        {
            if (_weapon.CanFire)
            {
                _weapon.Fire();
                return;
            }

            // Trigger pulled on an empty magazine -> reload instead of clicking on nothing.
            if (autoReloadOnEmpty && _weapon.CurrentAmmo <= 0 && _weapon.CanReload)
                _weapon.StartReload();
        }

        /// <summary>
        /// Equips the first <see cref="IWeapon"/> found on the given object (including its
        /// children). Returns false when the object carries no weapon; the current weapon
        /// is kept untouched in that case.
        /// </summary>
        public bool SetWeapon(GameObject weaponGO)
        {
            if (weaponGO == null || weaponGO.GetComponentInChildren<IWeapon>() == null)
                return false;

            Equip(weaponGO);
            return true;
        }

        /// <summary>Resolves the weapon on <paramref name="weaponGO"/> (object or children) and subscribes.</summary>
        private void Equip(GameObject weaponGO)
        {
            Unequip();

            _weapon = weaponGO != null ? weaponGO.GetComponentInChildren<IWeapon>() : null;
            if (_weapon == null)
                return;

            _weapon.OnAmmoChanged += HandleWeaponAmmo;
            _weapon.OnWeaponFired += HandleWeaponFired;
            _weapon.OnReloadStarted += HandleWeaponReloadStarted;
            _weapon.OnReloadFinished += HandleWeaponReloadFinished;

            OnWeaponChanged?.Invoke(_weapon);
            OnAmmoChanged?.Invoke(_weapon.CurrentAmmo, _weapon.MagazineSize);
        }

        /// <summary>Unsubscribes from the current weapon and drops the reference.</summary>
        private void Unequip()
        {
            if (_weapon == null)
                return;

            _weapon.OnAmmoChanged -= HandleWeaponAmmo;
            _weapon.OnWeaponFired -= HandleWeaponFired;
            _weapon.OnReloadStarted -= HandleWeaponReloadStarted;
            _weapon.OnReloadFinished -= HandleWeaponReloadFinished;
            _weapon = null;
        }

        private void HandleWeaponAmmo(int current, int max) => OnAmmoChanged?.Invoke(current, max);
        private void HandleWeaponFired() => OnWeaponFired?.Invoke();
        private void HandleWeaponReloadStarted() => OnReloadStarted?.Invoke();
        private void HandleWeaponReloadFinished() => OnReloadFinished?.Invoke();
    }
}