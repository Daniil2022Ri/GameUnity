using UI.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.MainMenu
{
    /// <summary>
    /// Main menu controller: switches between the main / levels / settings panels, starts the
    /// game from the last reached level and quits. All wiring is done through serialized
    /// references, so the whole menu can be assembled in the inspector.
    ///
    /// Panels are plain GameObjects (disable the ones you do not need); buttons are optional,
    /// so the same script also works for a menu without a "Quit" button, for example.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject levelsPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button levelsButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button quitButton;

        [Header("Play")]
        [Tooltip("Optional explicit loader; falls back to the persistent LevelLoader.Instance.")]
        [SerializeField] private LevelLoader levelLoader;
        [Tooltip("Scene loaded by Play when no catalog is available.")]
        [SerializeField] private string fallbackFirstLevelScene = "Level1";

        [Header("Behaviour")]
        [Tooltip("Key that closes the inner panels. None disables the shortcut.")]
        [SerializeField] private KeyCode backKey = KeyCode.Escape;
        [SerializeField] private bool showCursor = true;

        private void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(Play);
            if (levelsButton != null)
                levelsButton.onClick.AddListener(ShowLevels);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(ShowSettings);
            if (backButton != null)
                backButton.onClick.AddListener(ShowMain);
            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);

            if (showCursor)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            ShowMain();
        }

        private void OnDestroy()
        {
            if (playButton != null)
                playButton.onClick.RemoveListener(Play);
            if (levelsButton != null)
                levelsButton.onClick.RemoveListener(ShowLevels);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(ShowSettings);
            if (backButton != null)
                backButton.onClick.RemoveListener(ShowMain);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(Quit);
        }

        private void Update()
        {
            if (backKey == KeyCode.None || !Input.GetKeyDown(backKey))
                return;

            // Do not steal Escape while a key-rebinding row is waiting for a key.
            if (KeyBindingManager.Exists && KeyBindingManager.Instance.IsRebinding)
                return;

            if (IsOpen(levelsPanel) || IsOpen(settingsPanel))
                ShowMain();
        }

        public void ShowMain()
        {
            SetActive(mainPanel, true);
            SetActive(levelsPanel, false);
            SetActive(settingsPanel, false);
        }

        public void ShowLevels()
        {
            SetActive(mainPanel, false);
            SetActive(levelsPanel, true);
            SetActive(settingsPanel, false);
        }

        public void ShowSettings()
        {
            SetActive(mainPanel, false);
            SetActive(levelsPanel, false);
            SetActive(settingsPanel, true);
        }

        /// <summary>Continues from the last reached level (or starts the first one).</summary>
        public void Play()
        {
            LevelLoader loader = ResolveLoader();

            if (loader != null && loader.Catalog != null && loader.Catalog.Count > 0)
            {
                int index = Mathf.Clamp(LevelProgress.LastLevelIndex, 0, loader.Catalog.Count - 1);
                loader.LoadLevelAt(index);
                return;
            }

            if (string.IsNullOrWhiteSpace(fallbackFirstLevelScene))
            {
                Debug.LogWarning($"{nameof(MainMenuUI)}: no catalog on the loader and no fallback scene set.", this);
                return;
            }

            SceneManager.LoadScene(fallbackFirstLevelScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private LevelLoader ResolveLoader()
        {
            if (levelLoader != null)
                return levelLoader;

            levelLoader = LevelLoader.Instance;
            return levelLoader;
        }

        private static bool IsOpen(GameObject panel) => panel != null && panel.activeSelf;

        private static void SetActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }
    }
}
