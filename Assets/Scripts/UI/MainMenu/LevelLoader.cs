using UnityEngine;
using UnityEngine.SceneManagement;

namespace UI.MainMenu
{
    /// <summary>
    /// Scene switching shared by the menu and gameplay: load by catalog index, scene name or
    /// build index, restart, return to the menu, and mark a level as completed (which unlocks
    /// the next one through <see cref="LevelProgress"/>).
    ///
    /// Created on demand and persistent, so a "Play" button in the menu and a "Next level"
    /// button in gameplay can both call <c>LevelLoader.Instance</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelLoader : MonoBehaviour
    {
        private const string ObjectName = "LevelLoader";

        private static LevelLoader _instance;

        /// <summary>Persistent loader; created automatically when the game has none.</summary>
        public static LevelLoader Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<LevelLoader>();
                if (_instance == null)
                {
                    var host = new GameObject(ObjectName);
                    _instance = host.AddComponent<LevelLoader>();
                }

                return _instance;
            }
        }

        /// <summary>True when a loader already exists (does not create one).</summary>
        public static bool Exists => _instance != null;

        [Header("Scenes")]
        [Tooltip("Main menu scene name (used by LoadMainMenu and after the last level).")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [Tooltip("Optional level list: enables loading by index and unlock progression.")]
        [SerializeField] private LevelCatalog catalog;

        [Header("Progression")]
        [Tooltip("Unlock the next catalog level when CompleteLevel is called.")]
        [SerializeField] private bool unlockNextOnComplete = true;

        public LevelCatalog Catalog => catalog;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"{nameof(LevelLoader)}: duplicate instance - " +
                                 "the persistent one is used, this copy is removed.", this);
                Destroy(this);
                return;
            }

            _instance = this;

            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Loads a catalog entry by its 0-based index.</summary>
        public void LoadLevelAt(int levelIndex)
        {
            if (catalog == null)
            {
                Debug.LogError($"{nameof(LevelLoader)}: no LevelCatalog assigned on the loader.", this);
                return;
            }

            if (!catalog.TryGetSceneName(levelIndex, out string sceneName))
            {
                Debug.LogError($"{nameof(LevelLoader)}: catalog has no playable scene at index {levelIndex}.", this);
                return;
            }

            LoadScene(sceneName);
        }

        /// <summary>Loads a scene by name; logs an error when it is missing from Build Settings.</summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"{nameof(LevelLoader)}: scene name is empty.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"{nameof(LevelLoader)}: scene '{sceneName}' is not in Build Settings.", this);
                return;
            }

            SceneManager.LoadSceneAsync(sceneName);
        }

        /// <summary>Loads the next scene in Build Settings; the main menu after the last one.</summary>
        public void LoadNextLevel()
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;

            if (next < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(next);
                return;
            }

            LoadMainMenu();
        }

        /// <summary>Loads the catalog entry after <paramref name="completedLevelIndex"/>.</summary>
        public void LoadNextCatalogLevel(int completedLevelIndex)
        {
            int next = completedLevelIndex + 1;

            if (catalog != null && next < catalog.Count)
            {
                LoadLevelAt(next);
                return;
            }

            LoadMainMenu();
        }

        public void RestartLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadMainMenu()
        {
            LoadScene(mainMenuSceneName);
        }

        /// <summary>Marks the level as passed so the next catalog entry becomes available.</summary>
        public void CompleteLevel(int levelIndex)
        {
            if (unlockNextOnComplete)
                LevelProgress.MarkCompleted(levelIndex);
        }

        /// <summary>Marks the open scene as passed (resolved through the catalog by scene name).</summary>
        public void CompleteCurrentLevel()
        {
            int levelIndex = catalog != null
                ? catalog.IndexOfScene(SceneManager.GetActiveScene().name)
                : -1;

            if (levelIndex < 0)
            {
                Debug.LogWarning($"{nameof(LevelLoader)}: the open scene is not in the catalog - " +
                                 "progress was not saved.", this);
                return;
            }

            CompleteLevel(levelIndex);
        }
    }
}
