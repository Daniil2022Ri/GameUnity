using TMPro;
using UI.MainMenu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.PlayerUI
{
    /// <summary>
    /// HUD button that finishes the current level and loads the next one. Everything is wired
    /// in the inspector: it resolves the current catalog index from the scene name, unlocks the
    /// next level (optional) and then loads the next catalog scene, the next Build Settings
    /// scene or the main menu. An optional label switches to "Finish" on the last level.
    /// </summary>
    [DisallowMultipleComponent]
    public class NextLevelButtonUI : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("Button that loads the next level. Falls back to a Button on this object or in children.")]
        [SerializeField] private Button button;
        [Tooltip("Optional label: shows nextLevelText, or finishText on the last level.")]
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private string nextLevelText = "Next level";
        [SerializeField] private string finishText = "Finish";

        [Header("Flow")]
        [Tooltip("Optional loader; falls back to the persistent LevelLoader.Instance.")]
        [SerializeField] private LevelLoader levelLoader;
        [Tooltip("Mark the current level as completed before loading the next one.")]
        [SerializeField] private bool completeCurrentLevel = true;
        [Tooltip("Go to the main menu when there is no next level at all.")]
        [SerializeField] private bool goToMainMenuWhenFinished = true;
        [Tooltip("Hide the button when there is nothing to load.")]
        [SerializeField] private bool hideWhenUnavailable;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
                if (button == null)
                    button = GetComponentInChildren<Button>(true);
            }

            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnEnable()
        {
            LevelProgress.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LevelProgress.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>True when there is a next catalog level or a next scene in Build Settings.</summary>
        public bool HasNextLevel
        {
            get
            {
                LevelLoader loader = ResolveLoader();
                return TryGetNextCatalogScene(loader, out _) || HasNextBuildScene;
            }
        }

        /// <summary>Completes the current level and loads the next one (or the main menu).</summary>
        public void Advance()
        {
            LevelLoader loader = ResolveLoader();
            int levelIndex = CurrentCatalogIndex(loader);

            if (completeCurrentLevel && levelIndex >= 0)
                loader.CompleteLevel(levelIndex);

            if (TryGetNextCatalogScene(loader, out string nextScene))
            {
                loader.LoadScene(nextScene);
                return;
            }

            if (HasNextBuildScene)
            {
                loader.LoadNextLevel();
                return;
            }

            if (goToMainMenuWhenFinished)
                loader.LoadMainMenu();
        }

        /// <summary>Repaints the label and the button state from the current scene and progression.</summary>
        public void Refresh()
        {
            LevelLoader loader = ResolveLoader();
            bool hasNext = TryGetNextCatalogScene(loader, out _) || HasNextBuildScene;
            bool available = hasNext || goToMainMenuWhenFinished;

            if (label != null)
                label.text = hasNext ? nextLevelText : finishText;

            if (button == null)
                return;

            button.interactable = available;

            if (hideWhenUnavailable)
                button.gameObject.SetActive(available);
        }

        private void HandleClick() => Advance();

        private int CurrentCatalogIndex(LevelLoader loader)
        {
            if (loader == null || loader.Catalog == null)
                return -1;

            return loader.Catalog.IndexOfScene(SceneManager.GetActiveScene().name);
        }

        private bool TryGetNextCatalogScene(LevelLoader loader, out string sceneName)
        {
            sceneName = null;

            if (loader == null || loader.Catalog == null)
                return false;

            int index = CurrentCatalogIndex(loader);
            return index >= 0 && loader.Catalog.TryGetSceneName(index + 1, out sceneName);
        }

        private static bool HasNextBuildScene =>
            SceneManager.GetActiveScene().buildIndex + 1 < SceneManager.sceneCountInBuildSettings;

        private LevelLoader ResolveLoader()
        {
            if (levelLoader != null)
                return levelLoader;

            levelLoader = LevelLoader.Instance;
            return levelLoader;
        }
    }
}
