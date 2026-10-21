using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settings
{
    /// <summary>
    /// Container for the key-rebinding lines plus an optional "reset all" button.
    /// Rows register themselves, so the panel only has to reset/repaint them.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyBindingPanelUI : MonoBehaviour
    {
        [Header("Rows")]
        [Tooltip("Leave empty to collect every KeyBindingRowUI in children automatically.")]
        [SerializeField] private List<KeyBindingRowUI> rows = new List<KeyBindingRowUI>();
        [SerializeField] private bool autoCollectRows = true;

        [Header("Actions")]
        [SerializeField] private Button resetAllButton;

        private void Awake()
        {
            if (autoCollectRows && rows.Count == 0)
                rows.AddRange(GetComponentsInChildren<KeyBindingRowUI>(true));

            if (resetAllButton != null)
                resetAllButton.onClick.AddListener(ResetAll);
        }

        private void OnDestroy()
        {
            if (resetAllButton != null)
                resetAllButton.onClick.RemoveListener(ResetAll);
        }

        /// <summary>Restores every built-in key and repaints the rows.</summary>
        public void ResetAll()
        {
            if (!KeyBindingManager.Exists)
                return;

            KeyBindingManager.Instance.ResetToDefaults();
            Refresh();
        }

        /// <summary>Repaints every row (e.g. after changing the bindings from code).</summary>
        public void Refresh()
        {
            foreach (KeyBindingRowUI row in rows)
            {
                if (row != null)
                    row.Refresh();
            }
        }
    }
}
