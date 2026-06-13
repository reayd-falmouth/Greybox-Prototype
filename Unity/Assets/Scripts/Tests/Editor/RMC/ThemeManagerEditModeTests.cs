using NUnit.Framework;
using Runtime.RMC.Backgammon.Theme;
using UnityEngine;

public class ThemeManagerEditModeTests
{
    [Test]
    public void ThemeManager_CanBeAddedToGameObject()
    {
        var go = new GameObject("ThemeManagerTest");
        try
        {
            var tm = go.AddComponent<ThemeManager>();
            Assert.IsNotNull(tm);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BackgammonThemeSo_DefaultColors_MatchExpectedValues()
    {
        var so = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        try
        {
            Assert.AreEqual(new Color(1f, 0.42f, 0f), so.checker1BaseColor, "Checker1 base color default");
            Assert.AreEqual(Color.yellow,              so.checker1EmissionColor, "Checker1 emission default");
            Assert.AreEqual(new Color(0.1f, 0.1f, 0.1f), so.checker2BaseColor, "Checker2 base color default");
            Assert.AreEqual(Color.red,                 so.checker2EmissionColor, "Checker2 emission default");
            Assert.AreEqual(new Color(0.2f, 0.85f, 1f, 1f), so.movableHighlightColor, "Highlight color default");
        }
        finally
        {
            Object.DestroyImmediate(so);
        }
    }

    [Test]
    public void BackgammonThemeSerializer_ToData_PreservesChecker1Intensity()
    {
        var so = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        so.checker1EmissionIntensity = 4.5f;
        try
        {
            BackgammonThemeData data = BackgammonThemeSerializer.ToData(so);
            Assert.AreEqual(4.5f, data.c1intensity, 1e-4f, "Emission intensity should be preserved");
        }
        finally
        {
            Object.DestroyImmediate(so);
        }
    }

    [Test]
    public void BackgammonThemeSerializer_ApplyData_WritesBackToSo()
    {
        var source = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        source.checker1BaseColor = new Color(0.11f, 0.22f, 0.33f);
        source.diceBodyColor     = new Color(0.44f, 0.55f, 0.66f);

        BackgammonThemeData data = BackgammonThemeSerializer.ToData(source);

        var target = ScriptableObject.CreateInstance<BackgammonThemeSo>();
        BackgammonThemeSerializer.ApplyData(data, target);

        Assert.AreEqual(source.checker1BaseColor.r, target.checker1BaseColor.r, 1e-4f, "checker1BaseColor.r");
        Assert.AreEqual(source.checker1BaseColor.g, target.checker1BaseColor.g, 1e-4f, "checker1BaseColor.g");
        Assert.AreEqual(source.diceBodyColor.r,     target.diceBodyColor.r,     1e-4f, "diceBodyColor.r");

        Object.DestroyImmediate(source);
        Object.DestroyImmediate(target);
    }
}
