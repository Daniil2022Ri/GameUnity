using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UI.MessageUI
{
    /// <summary>
    /// Queue-based on-screen message feed (pickups, low ammo, "level up", failed actions...).
    ///
    /// One message is shown at a time: it fades in, stays for its duration and fades out,
    /// then the next queued message is played. Timings use unscaled time, so messages keep
    /// working while the game is paused (<see cref="Time.timeScale"/> = 0).
    ///
    /// Attach it to a UI panel that has a TMP text; the <see cref="CanvasGroup"/> used for the
    /// fading is taken from the same object or added automatically. The panel itself is never
    /// disabled (a disabled object would kill the coroutine), only its alpha changes.
    ///
    /// Call it from anywhere:
    /// <code>
    /// MessageManager.Instance.Show("Out of ammo", MessageType.Warning);
    /// MessageManager.TryShow("Reloading");               // safe when no manager is in the scene
    /// </code>
    /// </summary>
    [DisallowMultipleComponent]
    public class MessageManager : MonoBehaviour
    {
        /// <summary>Most recently enabled manager; null when the scene has none.</summary>
        public static MessageManager Instance { get; private set; }

        [Header("View")]
        [Tooltip("Panel that fades in/out. Taken from this object or added automatically when missing.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Label that shows the message. Falls back to the first TMP text in children.")]
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Timing (seconds, unscaled)")]
        [Tooltip("How long a message stays fully visible before it fades out.")]
        [SerializeField] private float defaultDuration = 2.5f;
        [SerializeField] private float fadeInTime = 0.15f;
        [SerializeField] private float fadeOutTime = 0.35f;

        [Header("Queue")]
        [Tooltip("When the queue grows past this limit the oldest messages are dropped.")]
        [SerializeField, Min(1)] private int maxQueuedMessages = 8;

        [Header("Colours per type")]
        [SerializeField] private Color infoColor = Color.white;
        [SerializeField] private Color successColor = new Color(0.35f, 0.9f, 0.4f);
        [SerializeField] private Color warningColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color errorColor = new Color(1f, 0.35f, 0.3f);

        private readonly Queue<MessageEntry> _queue = new Queue<MessageEntry>();
        private Coroutine _routine;

        /// <summary>True while a message is on screen or still waiting in the queue.</summary>
        public bool IsShowing => _routine != null;

        private void Awake()
        {
            // UI managers are one per scene; the last one enabled wins and old references
            // simply become null when that object is destroyed.
            Instance = this;

            ResolveReferences();
        }

        private void OnDisable()
        {
            // Disabling the object kills its coroutines, so clear the handle and the visuals
            // instead of leaving a stale "showing" flag behind.
            _queue.Clear();
            StopRoutine();
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Shows a message with the default type and duration.</summary>
        public void Show(string message) => Show(message, MessageType.Info, defaultDuration);

        /// <summary>Shows a message with the default duration.</summary>
        public void Show(string message, MessageType type) => Show(message, type, defaultDuration);

        /// <summary>Shows an info message for the given duration.</summary>
        public void Show(string message, float duration) => Show(message, MessageType.Info, duration);

        /// <summary>
        /// Queues a message. <paramref name="duration"/> &lt;= 0 uses the default duration.
        /// Set <paramref name="interruptCurrent"/> for important messages: the queue is dropped
        /// and the new message replaces whatever is on screen right now.
        /// </summary>
        public void Show(string message, MessageType type, float duration, bool interruptCurrent = false)
        {
            if (string.IsNullOrWhiteSpace(message) || messageText == null)
                return;

            var entry = new MessageEntry(message, type, duration > 0f ? duration : defaultDuration);

            if (interruptCurrent)
            {
                _queue.Clear();
                StopRoutine();
                SetAlpha(0f);
            }

            if (_queue.Count >= maxQueuedMessages)
                _queue.Dequeue();

            _queue.Enqueue(entry);

            // A running coroutine picks the new entry up as soon as it finishes the current one.
            if (_routine == null)
                _routine = StartCoroutine(PlayQueue());
        }

        /// <summary>Shows a message on the scene's manager; false when there is none.</summary>
        public static bool TryShow(string message, MessageType type = MessageType.Info, float duration = -1f)
        {
            if (Instance == null)
            {
                Debug.LogWarning($"{nameof(MessageManager)}: no instance in the scene - " +
                                 $"message \"{message}\" was dropped.");
                return false;
            }

            Instance.Show(message, type, duration);
            return true;
        }

        /// <summary>Drops the queue and hides the panel immediately.</summary>
        public void Clear()
        {
            _queue.Clear();
            StopRoutine();
            SetAlpha(0f);
        }

        private IEnumerator PlayQueue()
        {
            while (_queue.Count > 0)
            {
                MessageEntry entry = _queue.Dequeue();
                Apply(entry);

                yield return FadeTo(1f, fadeInTime);
                yield return new WaitForSecondsRealtime(entry.Duration);
                yield return FadeTo(0f, fadeOutTime);
            }

            _routine = null; // only now is nothing queued any more
        }

        private void Apply(MessageEntry entry)
        {
            messageText.text = entry.Text;
            messageText.color = GetColor(entry.Type);
        }

        private Color GetColor(MessageType type) => type switch
        {
            MessageType.Success => successColor,
            MessageType.Warning => warningColor,
            MessageType.Error => errorColor,
            _ => infoColor
        };

        private IEnumerator FadeTo(float target, float duration)
        {
            if (canvasGroup == null)
                yield break;

            if (duration <= 0f)
            {
                canvasGroup.alpha = target;
                yield break;
            }

            float start = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = target;
        }

        private void StopRoutine()
        {
            if (_routine == null)
                return;

            StopCoroutine(_routine);
            _routine = null;
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // A message feed must never eat clicks, and it must be invisible while idle.
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (messageText == null)
                messageText = GetComponentInChildren<TextMeshProUGUI>(true);

            if (messageText == null)
                Debug.LogWarning($"{nameof(MessageManager)}: no TextMeshProUGUI found - " +
                                 "messages will not appear.", this);
        }

        /// <summary>One queued message.</summary>
        private readonly struct MessageEntry
        {
            public MessageEntry(string text, MessageType type, float duration)
            {
                Text = text;
                Type = type;
                Duration = duration;
            }

            public string Text { get; }
            public MessageType Type { get; }
            public float Duration { get; }
        }
    }
}
