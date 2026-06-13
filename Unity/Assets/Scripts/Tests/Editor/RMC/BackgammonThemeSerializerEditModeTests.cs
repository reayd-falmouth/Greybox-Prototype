using NUnit.Framework;
using Runtime.RMC.Backgammon.Theme;
using UnityEngine;

public class BackgammonThemeSerializerEditModeTests
{
    private const float Tolerance = 1e-4f;

    [Test]
    public void RoundTrip_DefaultValues_AllColorComponentsMatch()
    {
        var so = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        so.checker1BaseColor     = new Color(1f, 0.42f, 0f);
        so.checker1EmissionColor = Color.yellow;
        so.checker1EmissionIntensity = 3.41f;
        so.checker2BaseColor     = new Color(0.1f, 0.1f, 0.1f);
        so.checker2EmissionColor = Color.red;
        so.checker2EmissionIntensity = 2.0f;
        so.movableHighlightColor  = new Color(0.2f, 0.85f, 1f, 1f);
        so.boardPointDarkColor   = new Color(0.1f, 0.1f, 0.1f);
        so.boardPointLightColor  = new Color(0.9f, 0.9f, 0.9f);
        so.doublingCubeColor     = Color.white;
        so.doublingCubeEmission  = Color.black;
        so.diceBodyColor  = Color.red;
        so.dicePipColor   = Color.white;
        so.diceLuminosity = 1f;
        so.boardSurfaceColor = new Color(0.18f, 0.22f, 0.2f);
        so.uiAccentColor    = new Color(0.012f, 0.4f, 0.655f);
        so.uiSecondaryColor = new Color(0.34f, 0.34f, 0.34f);

        BackgammonThemeData data = BackgammonThemeSerializer.ToData(so);

        var restored = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        BackgammonThemeSerializer.ApplyData(data, restored);

        AssertColorEqual(so.checker1BaseColor,     restored.checker1BaseColor,     "checker1BaseColor");
        AssertColorEqual(so.checker1EmissionColor, restored.checker1EmissionColor, "checker1EmissionColor");
        Assert.AreEqual(so.checker1EmissionIntensity, restored.checker1EmissionIntensity, Tolerance, "checker1EmissionIntensity");
        AssertColorEqual(so.checker2BaseColor,     restored.checker2BaseColor,     "checker2BaseColor");
        AssertColorEqual(so.checker2EmissionColor, restored.checker2EmissionColor, "checker2EmissionColor");
        AssertColorEqual(so.boardPointDarkColor,   restored.boardPointDarkColor,   "boardPointDarkColor");
        AssertColorEqual(so.boardPointLightColor,  restored.boardPointLightColor,  "boardPointLightColor");
        AssertColorEqual(so.diceBodyColor,         restored.diceBodyColor,         "diceBodyColor");
        AssertColorEqual(so.boardSurfaceColor,     restored.boardSurfaceColor,     "boardSurfaceColor");
        AssertColorEqual(so.uiAccentColor,         restored.uiAccentColor,         "uiAccentColor");

        Object.DestroyImmediate(so);
        Object.DestroyImmediate(restored);
    }

    [Test]
    public void LoadCustom_WithNoSavedData_ReturnsFallbackValues()
    {
        UnityEngine.PlayerPrefs.DeleteKey("bg_custom_theme_json");

        var fallback = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        fallback.checker1BaseColor = new Color(0.5f, 0.5f, 0.5f);

        BackgammonThemeData data = BackgammonThemeSerializer.LoadCustom(fallback);

        Assert.AreEqual(fallback.checker1BaseColor.r, data.c1r, Tolerance, "Fallback c1r should match");
        Assert.AreEqual(fallback.checker1BaseColor.g, data.c1g, Tolerance, "Fallback c1g should match");
        Assert.AreEqual(fallback.checker1BaseColor.b, data.c1b, Tolerance, "Fallback c1b should match");

        Object.DestroyImmediate(fallback);
    }

    [Test]
    public void SaveCustom_ThenLoadCustom_RoundTrips()
    {
        var so = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        so.checker1BaseColor = new Color(0.33f, 0.66f, 0.99f);
        so.diceBodyColor     = new Color(0.1f, 0.2f, 0.3f);

        BackgammonThemeData original = BackgammonThemeSerializer.ToData(so);
        BackgammonThemeSerializer.SaveCustom(original);

        BackgammonThemeData loaded = BackgammonThemeSerializer.LoadCustom(null);

        Assert.AreEqual(original.c1r, loaded.c1r, Tolerance, "c1r round-trip");
        Assert.AreEqual(original.c1g, loaded.c1g, Tolerance, "c1g round-trip");
        Assert.AreEqual(original.c1b, loaded.c1b, Tolerance, "c1b round-trip");
        Assert.AreEqual(original.diceR, loaded.diceR, Tolerance, "diceR round-trip");

        UnityEngine.PlayerPrefs.DeleteKey("bg_custom_theme_json");
        Object.DestroyImmediate(so);
    }

    private static void AssertColorEqual(Color expected, Color actual, string label)
    {
        Assert.AreEqual(expected.r, actual.r, Tolerance, $"{label}.r");
        Assert.AreEqual(expected.g, actual.g, Tolerance, $"{label}.g");
        Assert.AreEqual(expected.b, actual.b, Tolerance, $"{label}.b");
    }
}
