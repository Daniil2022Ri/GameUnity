using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.MainMenu
{
    /// <summary>
    /// Ordered list of the game's levels used by the main menu and the level-select screen.
    /// Create one asset via <c>Assets &gt; Create &gt; Game &gt; Level Catalog</c> and drag it into
    /// <see cref="LevelSelectUI"/> / <see cref="LevelLoader"/>.
    /// Scene names must match the Build Settings entries exactly.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Level Catalog", fileName = "LevelCatalog")]
    public class LevelCatalog : ScriptableObject
    {
        [Serializable]
        public class LevelInfo
        {
            [Tooltip("Name shown on the level button.")]
            public string displayName = "Level 1";

            [Tooltip("Scene name exactly as it appears in Build Settings.")]
            public string sceneName = "Level1";

            [Tooltip("Optional preview image (not used by the generated buttons by default).")]
            public Sprite thumbnail;
        }

        [Tooltip("Levels in play order. Index 0 is the first level.")]
        [SerializeField] private List<LevelInfo> levels = new List<LevelInfo>();

        public IReadOnlyList<LevelInfo> Levels => levels;
        public int Count => levels.Count;

        public LevelInfo Get(int index) =>
            index >= 0 && index < levels.Count ? levels[index] : null;

        public bool TryGetSceneName(int index, out string sceneName)
        {
            LevelInfo info = Get(index);
            sceneName = info != null ? info.sceneName : null;
            return !string.IsNullOrWhiteSpace(sceneName);
        }

        public int IndexOfScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return -1;

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].sceneName == sceneName)
                    return i;
            }

            return -1;
        }
    }
}
