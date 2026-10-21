using System;
using UnityEngine;

namespace UI.MainMenu
{
    /// <summary>
    /// Persistent level progression stored in <see cref="PlayerPrefs"/>:
    /// how many levels from the start are unlocked and which one was reached last
    /// (used by the "Continue" flow of the main menu).
    /// </summary>
    public static class LevelProgress
    {
        private const string UnlockedCountKey = "progress.unlockedLevels";
        private const string LastLevelKey = "progress.lastLevelIndex";

        /// <summary>Fired after any change, so level-select screens can rebuild themselves.</summary>
        public static event Action Changed;

        /// <summary>How many levels (from the start of the catalog) are available. Never below 1.</summary>
        public static int UnlockedCount
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(UnlockedCountKey, 1));
            private set
            {
                PlayerPrefs.SetInt(UnlockedCountKey, Mathf.Max(1, value));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Catalog index of the level the player should continue from.</summary>
        public static int LastLevelIndex
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(LastLevelKey, 0));
            set
            {
                PlayerPrefs.SetInt(LastLevelKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static bool IsUnlocked(int levelIndex) => levelIndex >= 0 && levelIndex < UnlockedCount;

        /// <summary>Marks the level as passed: the next one becomes available.</summary>
        public static void MarkCompleted(int levelIndex)
        {
            if (levelIndex < 0)
                return;

            LastLevelIndex = levelIndex + 1;

            int requiredUnlocked = levelIndex + 2;
            if (UnlockedCount < requiredUnlocked)
                UnlockedCount = requiredUnlocked;
        }

        /// <summary>Unlocks a single level (and everything before it) without completing anything.</summary>
        public static void UnlockLevel(int levelIndex)
        {
            if (levelIndex < 0)
                return;

            if (UnlockedCount <= levelIndex)
                UnlockedCount = levelIndex + 1;
        }

        /// <summary>Opens every level - handy for testing the menu.</summary>
        public static void UnlockAll(int levelCount)
        {
            if (levelCount > 0)
                UnlockedCount = levelCount;
        }

        /// <summary>Wipes the progression and returns to a single unlocked level.</summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(UnlockedCountKey);
            PlayerPrefs.DeleteKey(LastLevelKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
