using System;
using UnityEngine;
using UnityEngine.Audio;

namespace UI.Settings
{
    /// <summary>
    /// Player-facing options: master/music/sfx volume, mouse sensitivity, graphics quality,
    /// fullscreen and v-sync. Values live in <see cref="PlayerPrefs"/> and are applied
    /// immediately, so the settings panel only has to call the setters.
    ///
    /// The object is created on demand and survives scene loads, so gameplay code can read
    /// <c>GameSettings.Instance.MouseSensitivity</c> without the menu being loaded first.
    /// Volumes go through an optional <see cref="AudioMixer"/> (exposed parameters) and fall
    /// back to <see cref="AudioListener.volume"/> when no mixer is assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSettings : MonoBehaviour
    {
        private const string ObjectName = "GameSettings";

        private const string MasterKey = "settings.masterVolume";
        private const string MusicKey = "settings.musicVolume";
        private const string SfxKey = "settings.sfxVolume";
        private const string SensitivityKey = "settings.mouseSensitivity";
        private const string QualityKey = "settings.qualityLevel";
        private const string FullscreenKey = "settings.fullscreen";
        private const string VsyncKey = "settings.vsync";

        private static GameSettings _instance;

        /// <summary>Persistent settings; created automatically when the game has none.</summary>
        public static GameSettings Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<GameSettings>();
                if (_instance == null)
                {
                    var host = new GameObject(ObjectName);
                    _instance = host.AddComponent<GameSettings>();
                }

                return _instance;
            }
        }

        /// <summary>True when a settings object already exists (does not create one).</summary>
        public static bool Exists => _instance != null;

        [Header("Defaults")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
        [SerializeField] private float defaultMouseSensitivity = 1f;
        [SerializeField] private bool defaultFullscreen = true;
        [SerializeField] private bool defaultVSync = true;
        [Tooltip("Quality level restored by ResetToDefaults. -1 = the highest available.")]
        [SerializeField] private int defaultQualityLevel = -1;

        [Header("Audio (optional)")]
        [Tooltip("Assign a mixer to control music and sfx separately. " +
                 "Leave empty to drive AudioListener.volume with the master slider only.")]
        [SerializeField] private AudioMixer mixer;
        [Tooltip("Exposed mixer parameter names (Edit > Project Settings > Audio > Mixer > parameter context menu).")]
        [SerializeField] private string masterParameter = "MasterVolume";
        [SerializeField] private string musicParameter = "MusicVolume";
        [SerializeField] private string sfxParameter = "SfxVolume";
        [Tooltip("Mixer value in dB used when a slider is at zero.")]
        [SerializeField] private float silenceDb = -80f;

        [Header("Mouse")]
        [Tooltip("Allowed range of the sensitivity slider (x = minimum, y = maximum).")]
        [SerializeField] private Vector2 sensitivityRange = new Vector2(0.1f, 3f);

        [Header("Startup")]
        [Tooltip("Apply the stored values as soon as the object is created.")]
        [SerializeField] private bool applyOnAwake = true;

        public float MasterVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public float MouseSensitivity { get; private set; }
        public int QualityLevel { get; private set; }
        public bool Fullscreen { get; private set; }
        public bool VSync { get; private set; }

        /// <summary>Allowed sensitivity range, for configuring the UI slider.</summary>
        public Vector2 SensitivityRange => sensitivityRange;

        /// <summary>Quality names from Project Settings, for the dropdown.</summary>
        public string[] QualityNames => QualitySettings.names;

        public int QualityCount => QualitySettings.names.Length;

        /// <summary>Fired after any value changed (panel repaints itself from it).</summary>
        public event Action Changed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"{nameof(GameSettings)}: duplicate instance - " +
                                 "the persistent one is used, this copy is removed.", this);
                Destroy(this);
                return;
            }

            _instance = this;

            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            Load();

            if (applyOnAwake)
                Apply();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterKey, defaultMasterVolume);
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, defaultMusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, defaultSfxVolume);

            MouseSensitivity = Mathf.Clamp(
                PlayerPrefs.GetFloat(SensitivityKey, defaultMouseSensitivity),
                sensitivityRange.x, sensitivityRange.y);

            int qualityCount = Mathf.Max(1, QualitySettings.names.Length);
            QualityLevel = Mathf.Clamp(
                PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, qualityCount - 1);

            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, defaultFullscreen ? 1 : 0) == 1;
            VSync = PlayerPrefs.GetInt(VsyncKey, defaultVSync ? 1 : 0) == 1;
        }

        /// <summary>Pushes every stored value into the engine.</summary>
        public void Apply()
        {
            ApplyAudio();

            QualitySettings.SetQualityLevel(QualityLevel, true);
            Screen.fullScreen = Fullscreen;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
        }

        private void ApplyAudio()
        {
            if (mixer != null)
            {
                SetMixerVolume(masterParameter, MasterVolume);
                SetMixerVolume(musicParameter, MusicVolume);
                SetMixerVolume(sfxParameter, SfxVolume);
                return;
            }

            AudioListener.volume = MasterVolume;
        }

        private void SetMixerVolume(string parameter, float linear)
        {
            if (string.IsNullOrWhiteSpace(parameter))
                return;

            // Sliders are linear 0..1, mixer parameters are in dB (-80 = silence).
            float db = linear <= 0.0001f ? silenceDb : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(parameter, db);
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            ApplyAudio();
            Save();
            Changed?.Invoke();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            ApplyAudio();
            Save();
            Changed?.Invoke();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            ApplyAudio();
            Save();
            Changed?.Invoke();
        }

        public void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, sensitivityRange.x, sensitivityRange.y);
            Save();
            Changed?.Invoke();
        }

        public void SetQualityLevel(int index)
        {
            int qualityCount = Mathf.Max(1, QualitySettings.names.Length);
            QualityLevel = Mathf.Clamp(index, 0, qualityCount - 1);

            QualitySettings.SetQualityLevel(QualityLevel, true);
            Save();
            Changed?.Invoke();
        }

        public void SetFullscreen(bool value)
        {
            Fullscreen = value;
            Screen.fullScreen = value;
            Save();
            Changed?.Invoke();
        }

        public void SetVSync(bool value)
        {
            VSync = value;
            QualitySettings.vSyncCount = value ? 1 : 0;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Restores the inspector defaults, applies and saves them.</summary>
        public void ResetToDefaults()
        {
            MasterVolume = Mathf.Clamp01(defaultMasterVolume);
            MusicVolume = Mathf.Clamp01(defaultMusicVolume);
            SfxVolume = Mathf.Clamp01(defaultSfxVolume);
            MouseSensitivity = Mathf.Clamp(defaultMouseSensitivity, sensitivityRange.x, sensitivityRange.y);

            int qualityCount = Mathf.Max(1, QualitySettings.names.Length);
            QualityLevel = defaultQualityLevel < 0
                ? qualityCount - 1
                : Mathf.Clamp(defaultQualityLevel, 0, qualityCount - 1);

            Fullscreen = defaultFullscreen;
            VSync = defaultVSync;

            Apply();
            Save();
            Changed?.Invoke();
        }

        private void Save()
        {
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
            PlayerPrefs.SetInt(QualityKey, QualityLevel);
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(VsyncKey, VSync ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
