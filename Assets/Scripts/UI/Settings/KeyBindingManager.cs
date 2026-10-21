using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Settings
{
    /// <summary>
    /// Owns every rebindable key of the game and persists the player's choices in
    /// <see cref="PlayerPrefs"/>. Gameplay scripts read keys through this manager
    /// (<see cref="IsPressed"/> / <see cref="IsDown"/> / <see cref="GetKey"/>) instead of
    /// hard-coded <see cref="KeyCode"/>s, so a rebind in the menu takes effect immediately.
    ///
    /// A manager is created on demand and survives scene loads, so nothing in gameplay has to
    /// care whether the menu was visited first. A duplicate placed in a scene is destroyed
    /// automatically (the persistent one keeps the data).
    ///
    /// Rebinding: <see cref="StartRebind"/> captures the next key press; Escape cancels.
    /// When the chosen key is already used by another action, the two keys are swapped.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyBindingManager : MonoBehaviour
    {
        private const string PrefPrefix = "keys.";
        private const string ObjectName = "KeyBindingManager";

        private static KeyBindingManager _instance;

        /// <summary>Persistent manager; created automatically when the game has none.</summary>
        public static KeyBindingManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<KeyBindingManager>();
                if (_instance == null)
                {
                    var host = new GameObject(ObjectName);
                    _instance = host.AddComponent<KeyBindingManager>();
                }

                return _instance;
            }
        }

        /// <summary>True when a manager already exists (does not create one).</summary>
        public static bool Exists => _instance != null;

        [Serializable]
        public struct BindingOverride
        {
            public GameAction action;
            [Tooltip("Key used until the player rebinds this action.")]
            public KeyCode key;
        }

        [Header("Defaults")]
        [Tooltip("Optional overrides of the built-in defaults. Actions not listed keep their default key.")]
        [SerializeField] private List<BindingOverride> defaultOverrides = new List<BindingOverride>();

        [Header("Behaviour")]
        [Tooltip("Key that cancels a pending rebind.")]
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

        private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        private readonly Dictionary<GameAction, KeyCode> _keys = new Dictionary<GameAction, KeyCode>();
        private GameAction? _rebinding;
        private Action<KeyCode> _onRebound;
        private Action _onCanceled;

        public event Action<GameAction, KeyCode> BindingChanged;
        public event Action<GameAction> RebindingStarted;
        public event Action RebindingCanceled;

        /// <summary>True while waiting for the player to press a key.</summary>
        public bool IsRebinding => _rebinding.HasValue;

        /// <summary>Action currently waiting for a key (meaningful only while <see cref="IsRebinding"/>).</summary>
        public GameAction RebindingAction => _rebinding ?? default;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // The persistent manager already holds the data; a scene copy is redundant.
                Debug.LogWarning($"{nameof(KeyBindingManager)}: duplicate instance - " +
                                 "the persistent one is used, this copy is removed.", this);
                Destroy(this);
                return;
            }

            _instance = this;

            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            Load();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Load()
        {
            _keys.Clear();

            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
                _keys[action] = DefaultKeyFor(action);

            foreach (BindingOverride entry in defaultOverrides)
            {
                if (entry.key != KeyCode.None)
                    _keys[entry.action] = entry.key;
            }

            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                string prefKey = PrefPrefix + action;
                if (!PlayerPrefs.HasKey(prefKey))
                    continue;

                int stored = PlayerPrefs.GetInt(prefKey);
                if (Enum.IsDefined(typeof(KeyCode), stored))
                    _keys[action] = (KeyCode)stored;
            }
        }

        /// <summary>Built-in default key per action (overridden by <see cref="defaultOverrides"/>).</summary>
        public static KeyCode DefaultKeyFor(GameAction action) => action switch
        {
            GameAction.MoveForward => KeyCode.W,
            GameAction.MoveBackward => KeyCode.S,
            GameAction.MoveLeft => KeyCode.A,
            GameAction.MoveRight => KeyCode.D,
            GameAction.Sprint => KeyCode.LeftShift,
            GameAction.Fire => KeyCode.Mouse0,
            GameAction.Reload => KeyCode.R,
            GameAction.Interact => KeyCode.E,
            GameAction.Pause => KeyCode.Escape,
            _ => KeyCode.None
        };

        /// <summary>Key currently bound to the action (never throws for unknown values).</summary>
        public KeyCode GetKey(GameAction action) =>
            _keys.TryGetValue(action, out KeyCode key) ? key : DefaultKeyFor(action);

        public bool IsPressed(GameAction action) => Input.GetKey(GetKey(action));
        public bool IsDown(GameAction action) => Input.GetKeyDown(GetKey(action));
        public bool IsUp(GameAction action) => Input.GetKeyUp(GetKey(action));

        /// <summary>
        /// Binds a key to the action and saves it. If the key already belongs to another
        /// action, the two keys are swapped so nothing ends up bound twice.
        /// </summary>
        public void SetKey(GameAction action, KeyCode key, bool save = true)
        {
            if (key == KeyCode.None)
                return;

            KeyCode previous = GetKey(action);
            if (previous == key)
                return;

            GameAction? conflict = FindActionWithKey(key, action);
            if (conflict.HasValue)
            {
                _keys[conflict.Value] = previous;
                if (save)
                    SaveKey(conflict.Value);

                BindingChanged?.Invoke(conflict.Value, previous);
            }

            _keys[action] = key;
            if (save)
                SaveKey(action);

            BindingChanged?.Invoke(action, key);
        }

        /// <summary>Waits for the next key press and binds it. The cancel key (Escape) aborts.</summary>
        public void StartRebind(GameAction action, Action<KeyCode> onRebound = null, Action onCanceled = null)
        {
            if (_rebinding.HasValue)
                CancelRebind();

            _rebinding = action;
            _onRebound = onRebound;
            _onCanceled = onCanceled;

            RebindingStarted?.Invoke(action);
        }

        /// <summary>Stops waiting for a key without changing the binding.</summary>
        public void CancelRebind()
        {
            if (!_rebinding.HasValue)
                return;

            _rebinding = null;
            _onRebound = null;
            _onCanceled = null;

            RebindingCanceled?.Invoke();
        }

        /// <summary>Restores the built-in key of one action.</summary>
        public void ResetAction(GameAction action)
        {
            SetKey(action, DefaultKeyFor(action));
        }

        /// <summary>Restores every built-in key.</summary>
        public void ResetToDefaults()
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
                SetKey(action, DefaultKeyFor(action));
        }

        private void Update()
        {
            if (!_rebinding.HasValue)
                return;

            if (Input.GetKeyDown(cancelKey))
            {
                Action canceled = _onCanceled;
                CancelRebind();
                canceled?.Invoke();
                return;
            }

            foreach (KeyCode key in AllKeys)
            {
                if (key == KeyCode.None || key == cancelKey || !Input.GetKeyDown(key))
                    continue;

                GameAction action = _rebinding.Value;
                Action<KeyCode> callback = _onRebound;

                // Clear the "waiting for key" state before binding, so the row repaints with
                // the new key instead of staying on "Press any key...".
                _rebinding = null;
                _onRebound = null;
                _onCanceled = null;

                SetKey(action, key);
                callback?.Invoke(key);
                return;
            }
        }

        private GameAction? FindActionWithKey(KeyCode key, GameAction except)
        {
            foreach (KeyValuePair<GameAction, KeyCode> pair in _keys)
            {
                if (pair.Key != except && pair.Value == key)
                    return pair.Key;
            }

            return null;
        }

        private void SaveKey(GameAction action)
        {
            PlayerPrefs.SetInt(PrefPrefix + action, (int)GetKey(action));
            PlayerPrefs.Save();
        }

        /// <summary>Human-readable key name for the UI ("Left Mouse", "Left Shift", "W"...).</summary>
        public static string GetKeyDisplayName(KeyCode key) => key switch
        {
            KeyCode.None => "-",
            KeyCode.Mouse0 => "Left Mouse",
            KeyCode.Mouse1 => "Right Mouse",
            KeyCode.Mouse2 => "Middle Mouse",
            KeyCode.LeftShift => "Left Shift",
            KeyCode.RightShift => "Right Shift",
            KeyCode.LeftControl => "Left Ctrl",
            KeyCode.RightControl => "Right Ctrl",
            KeyCode.LeftAlt => "Left Alt",
            KeyCode.RightAlt => "Right Alt",
            KeyCode.Return => "Enter",
            KeyCode.Escape => "Esc",
            KeyCode.Space => "Space",
            KeyCode.Backspace => "Backspace",
            KeyCode.Tab => "Tab",
            KeyCode.UpArrow => "Up",
            KeyCode.DownArrow => "Down",
            KeyCode.LeftArrow => "Left",
            KeyCode.RightArrow => "Right",
            _ => key.ToString()
        };

        /// <summary>Readable action name for the UI ("Move Forward"...).</summary>
        public static string GetActionDisplayName(GameAction action) => action switch
        {
            GameAction.MoveForward => "Move Forward",
            GameAction.MoveBackward => "Move Backward",
            GameAction.MoveLeft => "Move Left",
            GameAction.MoveRight => "Move Right",
            GameAction.Sprint => "Sprint",
            GameAction.Fire => "Fire",
            GameAction.Reload => "Reload",
            GameAction.Interact => "Interact",
            GameAction.Pause => "Pause",
            _ => action.ToString()
        };
    }
}
