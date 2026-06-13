using NUnit.Framework;
using Runtime.RMC.Backgammon.Theme;
using UnityEditor;
using UnityEngine;

public class BackgammonThemeSoEditModeTests
{
    private const string DefaultThemePath  = "Assets/Settings/Presets/Themes/Theme_Default.asset";
    private const string ThemeLibraryPath  = "Assets/Settings/Presets/Themes/ThemeLibrary.asset";

    [Test]
    public void BackgammonThemeSo_CanBeInstantiatedWithDefaultValues()
    {
        var so = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        Assert.IsNotNull(so);
        Assert.AreEqual("Default", so.displayName);
        Object.DestroyImmediate(so);
    }

    [Test]
    public void BackgammonThemeLibrarySo_CanBeInstantiatedEmpty()
    {
        var lib = ScriptableObject.CreateInstance<BackgammonThemeLibrarySo>();
        Assert.IsNotNull(lib);
        Assert.AreEqual(0, lib.Count);
        Object.DestroyImmediate(lib);
    }

    [Test]
    public void DefaultThemeSo_LoadsFromAssetDatabase_WhenAssetExists()
    {
        var so = AssetDatabase.LoadAssetAtPath<BackgammonThemeSo>(DefaultThemePath);
        if (so == null)
        {
            Assert.Ignore($"Asset not yet created at {DefaultThemePath} — create it via the Unity Editor first.");
            return;
        }
        Assert.AreEqual("Default", so.displayName, "Default theme displayName should be 'Default'");
    }

    [Test]
    public void ThemeLibrarySo_LoadsFromAssetDatabase_WhenAssetExists()
    {
        var lib = AssetDatabase.LoadAssetAtPath<BackgammonThemeLibrarySo>(ThemeLibraryPath);
        if (lib == null)
        {
            Assert.Ignore($"Asset not yet created at {ThemeLibraryPath} — create it via the Unity Editor first.");
            return;
        }
        Assert.GreaterOrEqual(lib.Count, 1, "Theme library should contain at least one preset");
    }

    [Test]
    public void ThemeLibrarySo_GetTheme_ReturnsNull_ForOutOfRangeIndex()
    {
        var lib = ScriptableObject.CreateInstance<BackgammonThemeLibrarySo>();
        Assert.IsNull(lib.GetTheme(-1));
        Assert.IsNull(lib.GetTheme(0));
        Assert.IsNull(lib.GetTheme(99));
        Object.DestroyImmediate(lib);
    }

    [Test]
    public void ThemeLibrarySo_GetTheme_ReturnsCorrectEntry()
    {
        var lib = ScriptableObject.CreateInstance<BackgammonThemeLibrarySo>();
        var theme = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        theme.displayName = "TestTheme";
        lib.themes.Add(theme);

        Assert.AreSame(theme, lib.GetTheme(0));
        Assert.IsNull(lib.GetTheme(1));

        Object.DestroyImmediate(lib);
        Object.DestroyImmediate(theme);
    }
}
