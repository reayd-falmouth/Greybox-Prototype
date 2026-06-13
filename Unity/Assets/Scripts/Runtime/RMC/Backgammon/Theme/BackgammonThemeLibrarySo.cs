using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Theme
{
    [CreateAssetMenu(fileName = "ThemeLibrary", menuName = "RMC/Backgammon/Theme Library")]
    public class BackgammonThemeLibrarySo : ScriptableObject
    {
        public List<BackgammonThemeSo> themes = new();

        public BackgammonThemeSo GetTheme(int index) =>
            (themes != null && index >= 0 && index < themes.Count) ? themes[index] : null;

        public int Count => themes?.Count ?? 0;
    }
}
