using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Theme
{
    public class ThemeManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private BoardManager boardManager;

        [Header("Theme Library")]
        [SerializeField] private BackgammonThemeLibrarySo themeLibrary;

        public event System.Action<BackgammonThemeSo> OnThemeApplied;

        private void OnEnable()
        {
            BackgammonSettings.OnGraphicsSettingsChanged += ApplyActiveTheme;
        }

        private void OnDisable()
        {
            BackgammonSettings.OnGraphicsSettingsChanged -= ApplyActiveTheme;
        }

        public void ApplyActiveTheme()
        {
            BackgammonThemeSo theme = ResolveActiveTheme();
            if (theme != null)
                ApplyTheme(theme);
        }

        public void ApplyTheme(BackgammonThemeSo theme)
        {
            if (theme == null) return;
            boardManager?.ApplyTheme(theme);
            OnThemeApplied?.Invoke(theme);
        }

        private BackgammonThemeSo ResolveActiveTheme()
        {
            if (themeLibrary == null) return null;

            int idx = BackgammonSettings.ThemeIndex;
            if (idx == 3)
            {
                BackgammonThemeSo fallback = themeLibrary.GetTheme(0);
                BackgammonThemeData data = BackgammonThemeSerializer.LoadCustom(fallback);
                BackgammonThemeSo runtimeSo = ScriptableObject.CreateInstance<BackgammonThemeSo>();
                BackgammonThemeSerializer.ApplyData(data, runtimeSo);
                return runtimeSo;
            }

            return themeLibrary.GetTheme(idx);
        }
    }
}
