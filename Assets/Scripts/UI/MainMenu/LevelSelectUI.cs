using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.MainMenu
{
    /// <summary>
    /// Generates one button per <see cref="LevelCatalog"/> entry inside <see cref="content"/>,
    /// optionally locks the levels the player has not reached yet and routes clicks to
    /// <see cref="LevelLoader"/>. Rebuilds itself whenever <see cref="LevelProgress"/> changes.
    ///
    /// Setup: put this on the level-select panel, assign the catalog, a content object with a
    /// Layout Group and a button prefab that has a TMP text child. No extra code needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LevelCatalog catalog;

        [Header("View")]
        [Tooltip("Parent that receives the generated buttons (add a Layout Group to arrange them).")]
        [SerializeField] private Transform content;
        [Tooltip("Prefab with a Button and a TMP text child. The label shows displayName.")]
        [SerializeField] private Button levelButtonPrefab;
        [Tooltip("Disable levels the player has not reached yet.")]
        [SerializeField] private bool lockNotReachedLevels = true;

        [Header("Loading")]
        [Tooltip("Optional explicit loader; falls back to the persistent LevelLoader.Instance.")]
        [SerializeField] private LevelLoader levelLoader;

        [Header("Styling")]
        [SerializeField] private Color unlockedLabelColor = Color.white;
        [SerializeField] private Color lockedLabelColor = new Color(1f, 1f, 1f, 0.45f);

        private readonly List<Button> _spawned = new List<Button>();

        private void OnEnable()
        {
            LevelProgress.Changed += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            LevelProgress.Changed -= Rebuild;
        }

        /// <summary>Destroys the current buttons and creates them again from the catalog.</summary>
        public void Rebuild()
        {
            Clear();

            if (catalog == null || content == null || levelButtonPrefab == null)
            {
                Debug.LogWarning($"{nameof(LevelSelectUI)}: catalog, content or button prefab is not assigned.",
                    this);
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                LevelCatalog.LevelInfo info = catalog.Get(i);
                if (info == null)
                    continue;

                Button button = Instantiate(levelButtonPrefab, content);
                button.gameObject.SetActive(true);

                bool unlocked = !lockNotReachedLevels || LevelProgress.IsUnlocked(i);
                button.interactable = unlocked;

                int levelIndex = i;
                button.onClick.AddListener(() => HandleLevelClicked(levelIndex));

                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = string.IsNullOrWhiteSpace(info.displayName)
                        ? $"Level {i + 1}"
                        : info.displayName;
                    label.color = unlocked ? unlockedLabelColor : lockedLabelColor;
                }

                _spawned.Add(button);
            }
        }

        private void HandleLevelClicked(int levelIndex)
        {
            if (levelLoader == null)
                levelLoader = LevelLoader.Instance;

            levelLoader.LoadLevelAt(levelIndex);
        }

        private void Clear()
        {
            foreach (Button button in _spawned)
            {
                if (button == null)
                    continue;

                button.onClick.RemoveAllListeners();
                // Detach first so layout groups do not keep a slot for a frame.
                button.transform.SetParent(null, false);
                Destroy(button.gameObject);
            }

            _spawned.Clear();
        }
    }
}
