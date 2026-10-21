using System;
using UI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace UI.MainMenu
{
    /// <summary>
    /// In-game pause overlay: freezes time, pauses audio, unlocks the cursor and shows the
    /// pause buttons plus the same settings panel as the main menu. Everything is wired in the
    /// inspector; assign gameplay roots to disable them while paused so no script keeps
    /// reading input (shots, movement, AI).
    ///
    /// The pause key is the rebindable <see cref="GameAction.Pause"/> action when a
    /// <see cref="KeyBindingManager"/> exists, otherwise <see cref="fallbackPauseKey"/>.
    /// Escape first closes the settings sub-panel and only then resumes the game.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuUI : MonoBehaviour
    {
        /// <summary>Pause menu of the current scene (null when the scene has none).</summary>
        public static PauseMenuUI Instance { get; private set; }

        /// <summary>True while any pause menu keeps the game paused.</summary>
        public static bool IsPaused => Instance != null && Instance._isPaused;

        [Header("Root")]
        [Tooltip("Child overlay enabled while paused (NOT the object that holds this script).")]
        [SerializeField] private GameObject pauseRoot;
        [Tooltip("Panel with Resume / Settings / Main menu / Quit buttons.")]
        [SerializeField] private GameObject buttonsPanel;
        [Tooltip("Settings panel shown from the pause menu (optional).")]
        [SerializeField] private GameObject settingsPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Pause behaviour")]
        [Tooltip("Time.timeScale used while paused. 0 = fully frozen.")]
        [SerializeField] private float pausedTimeScale = 0f;
        [Tooltip("Pause all audio through AudioListener.pause.")]
        [SerializeField] private bool pauseAudio = true;
        [Tooltip("Unlock and show the cursor while paused.")]
        [SerializeField] private bool unlockCursor = true;
        [Tooltip("Key used when no KeyBindingManager exists in the scene.")]
        [SerializeField] private KeyCode fallbackPauseKey = KeyCode.Escape;
        [Tooltip("Gameplay objects disabled while paused (player, spawners, ...).")]
        [SerializeField] private GameObject[] gameplayRoots;
        [Tooltip("HUD objects hidden while paused.")]
        [SerializeField] private GameObject[] hudRoots;

        [Header("Scenes")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [Tooltip("Optional loader; falls back to the persistent LevelLoader.Instance.")]
        [SerializeField] private LevelLoader levelLoader;

        /// <summary>Fired right after the game was paused.</summary>
        public event Action Paused;

        /// <summary>Fired right after the game resumed.</summary>
        public event Action Resumed;

        private bool _isPaused;
        private float _previousTimeScale = 1f;
        private bool _previousAudioPause;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;

        private void Awake()
        {
            Instance = this;

            if (pauseRoot == gameObject)
            {
                Debug.LogWarning($"{nameof(PauseMenuUI)}: pauseRoot must be a child overlay, " +
                                 "otherwise enabling/disabling it would stop this script.", this);
                pauseRoot = buttonsPanel;
            }
            else if (pauseRoot == null)
            {
                pauseRoot = buttonsPanel;
            }

            if (resumeButton != null)
                resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(ShowSettings);
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);

            if (pauseRoot != null)
                pauseRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (resumeButton != null)
                resumeButton.onClick.RemoveListener(Resume);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(ShowSettings);
            if (mainMenuButton != null)
                mainMenuButton.onClick.RemoveListener(GoToMainMenu);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(Quit);

            // Leaving the scene while paused must not keep the game frozen.
            if (_isPaused)
                RestoreEngineState();

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!PausePressed())
                return;

            // Escape is the "cancel" key while a binding is being captured.
            if (KeyBindingManager.Exists && KeyBindingManager.Instance.IsRebinding)
                return;

            if (!_isPaused)
            {
                Pause();
                return;
            }

            // Inside the pause menu Escape walks one level back before resuming.
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                ShowButtons();
                return;
            }

            Resume();
        }

        /// <summary>Freezes the game and shows the pause overlay. No-op when already paused.</summary>
        public void Pause()
        {
            if (_isPaused)
                return;

            _isPaused = true;

            _previousTimeScale = Time.timeScale;
            _previousAudioPause = AudioListener.pause;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;

            Time.timeScale = pausedTimeScale;

            if (pauseAudio)
                AudioListener.pause = true;

            if (unlockCursor)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            SetActiveAll(gameplayRoots, false);
            SetActiveAll(hudRoots, false);

            ShowButtons();

            if (pauseRoot != null)
                pauseRoot.SetActive(true);

            Paused?.Invoke();
        }

        /// <summary>Restores time, audio, cursor and gameplay objects, then hides the overlay.</summary>
        public void Resume()
        {
            if (!_isPaused)
                return;

            _isPaused = false;

            if (pauseRoot != null)
                pauseRoot.SetActive(false);

            SetActiveAll(gameplayRoots, true);
            SetActiveAll(hudRoots, true);

            RestoreEngineState();
            Resumed?.Invoke();
        }

        public void Toggle()
        {
            if (_isPaused)
                Resume();
            else
                Pause();
        }

        /// <summary>Shows the settings sub-panel inside the pause overlay.</summary>
        public void ShowSettings()
        {
            if (buttonsPanel != null)
                buttonsPanel.SetActive(false);
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        /// <summary>Shows the pause buttons (also used as "back" from the settings sub-panel).</summary>
        public void ShowButtons()
        {
            if (buttonsPanel != null)
                buttonsPanel.SetActive(true);
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        /// <summary>Restores the game state and loads the main menu scene.</summary>
        public void GoToMainMenu()
        {
            LevelLoader loader = ResolveLoader();
            Resume();

            if (loader != null)
                loader.LoadScene(mainMenuSceneName);
        }

        public void Quit()
        {
            Resume();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool PausePressed()
        {
            if (KeyBindingManager.Exists)
                return KeyBindingManager.Instance.IsDown(GameAction.Pause);

            return fallbackPauseKey != KeyCode.None && Input.GetKeyDown(fallbackPauseKey);
        }

        private static void SetActiveAll(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            foreach (GameObject target in objects)
            {
                if (target != null)
                    target.SetActive(active);
            }
        }

        private void RestoreEngineState()
        {
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            AudioListener.pause = _previousAudioPause;
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLock;
        }

        private LevelLoader ResolveLoader()
        {
            if (levelLoader != null)
                return levelLoader;

            levelLoader = LevelLoader.Instance;
            return levelLoader;
        }
    }
}
