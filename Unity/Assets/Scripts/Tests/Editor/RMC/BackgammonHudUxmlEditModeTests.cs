using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Ensures <c>Resources/Layouts/BackgammonHUD</c> exposes unique, bindable names for <see cref="BackgammonHudController"/>.
/// </summary>
public class BackgammonHudUxmlEditModeTests
{
    private const string UxmlAssetPath = "Assets/Settings/UIToolkit/Resources/Layouts/BackgammonHUD.uxml";
    private const string UxmlGuid = "b0b8cf66be4ba1d43babb0555293673b";

    [Test]
    public void BackgammonHUD_Resources_Loads_And_HasRequiredNamedElements()
    {
        // Prefer GUID (stable if the file moves), then path, then Resources.
        string pathByGuid = AssetDatabase.GUIDToAssetPath(UxmlGuid);
        var vta = string.IsNullOrEmpty(pathByGuid)
            ? null
            : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(pathByGuid);
        if (vta == null)
            vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlAssetPath);
        if (vta == null)
            vta = Resources.Load<VisualTreeAsset>("Layouts/BackgammonHUD");
        Assert.IsNotNull(vta, "Expected VisualTreeAsset (GUID " + UxmlGuid + "), path " + UxmlAssetPath + ", or Resources Layouts/BackgammonHUD");

        VisualElement root = vta.CloneTree();
        Assert.IsNotNull(root.Q<VisualElement>("ScreenRoot"));
        Assert.IsNotNull(root.Q<Button>("RollButton"));
        Assert.IsNotNull(root.Q<Button>("UndoButton"));
        Assert.IsNotNull(root.Q<Button>("PlayMoveButton"));
        Assert.IsNotNull(root.Q<Button>("ViewHorizButton"));
        Assert.IsNotNull(root.Q<Button>("ViewVertButton"));
        Assert.IsNotNull(root.Q<Button>("DoubleButton"));
        Assert.IsNotNull(root.Q<Button>("TakeDoubleButton"));
        Assert.IsNotNull(root.Q<Button>("DropDoubleButton"));
        Assert.IsNotNull(root.Q<ScrollView>("LegalMovesScroll"));
        Assert.IsNotNull(root.Q<Label>("StatusLabel"));
        Assert.IsNotNull(root.Q<Label>("DiceLabel"));
        Assert.IsNotNull(root.Q<Label>("PositionIdLabel"));
        Assert.IsNotNull(root.Q<Label>("GameScoreValue"));
        Assert.IsNotNull(root.Q<Label>("ChipsValue"));
        Assert.IsNotNull(root.Q<Label>("MultiplierValue"));
        Assert.IsNotNull(root.Q<VisualElement>("DoublePanel"));
        Assert.IsNotNull(root.Q<VisualElement>("TakeDropPanel"));
        Assert.IsNotNull(root.Q<VisualElement>("SettingsPanel"));
    }
}
