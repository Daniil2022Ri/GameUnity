using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settings
{
    /// <summary>
    /// One line of the key-rebinding screen: action name, current key and a button that
    /// captures the next key press. Place several under a <see cref="KeyBindingPanelUI"/>
    /// and pick an action for each one in the inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyBindingRowUI : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private GameAction action = GameAction.MoveForward;

        [Header("View")]
        [Tooltip("Label with the action name. Filled automatically in OnEnable.")]
        [SerializeField] private TextMeshProUGUI actionLabel;
        [Tooltip("Label that shows the bound key.")]
        [SerializeField] private TextMeshProUGUI keyLabel;
        [Tooltip("Button that starts listening for a key. Falls back to the first Button in children.")]
        [SerializeField] private Button rebindButton;
        [Tooltip("Optional object highlighted while this row waits for a key.")]
        [SerializeField] private GameObject listeningIndicator;
        [SerializeField] private string listeningText = "Press any key...";

        public GameAction Action => action;

        private static KeyBindingManager ManagerOrNull =>
            KeyBindingManager.Exists ? KeyBindingManager.Instance : null;

        private void Awake()
        {
            if (rebindButton == null)
                rebindButton = GetComponentInChildren<Button>(true);

            if (rebindButton != null)
                rebindButton.onClick.AddListener(HandleRebindClicked);
        }

        private void OnEnable()
        {
            KeyBindingManager manager = KeyBindingManager.Instance;
            manager.BindingChanged += HandleBindingChanged;
            manager.RebindingStarted += HandleRebindingStarted;
            manager.RebindingCanceled += HandleRebindingCanceled;

            if (actionLabel != null)
                actionLabel.text = KeyBindingManager.GetActionDisplayName(action);

            Refresh();
        }

        private void OnDisable()
        {
            KeyBindingManager manager = ManagerOrNull;
            if (manager == null)
                return;

            manager.BindingChanged -= HandleBindingChanged;
            manager.RebindingStarted -= HandleRebindingStarted;
            manager.RebindingCanceled -= HandleRebindingCanceled;
        }

        private void OnDestroy()
        {
            if (rebindButton != null)
                rebindButton.onClick.RemoveListener(HandleRebindClicked);
        }

        /// <summary>Repaints the key label: "Press any key..." while listening, the key otherwise.</summary>
        public void Refresh()
        {
            KeyBindingManager manager = ManagerOrNull;
            bool listening = manager != null && manager.IsRebinding && manager.RebindingAction == action;

            if (keyLabel != null)
            {
                KeyCode key = manager != null ? manager.GetKey(action) : KeyBindingManager.DefaultKeyFor(action);
                keyLabel.text = listening ? listeningText : KeyBindingManager.GetKeyDisplayName(key);
            }

            if (listeningIndicator != null)
                listeningIndicator.SetActive(listening);
        }

        private void HandleRebindClicked()
        {
            KeyBindingManager manager = KeyBindingManager.Instance;

            if (manager.IsRebinding && manager.RebindingAction == action)
            {
                manager.CancelRebind();
                return;
            }

            manager.StartRebind(action);
        }

        private void HandleBindingChanged(GameAction changedAction, KeyCode key) => Refresh();
        private void HandleRebindingStarted(GameAction startedAction) => Refresh();
        private void HandleRebindingCanceled() => Refresh();
    }
}
