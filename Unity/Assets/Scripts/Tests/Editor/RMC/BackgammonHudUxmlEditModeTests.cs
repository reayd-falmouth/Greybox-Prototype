using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Ensures <c>Resources/Layouts/BackgammonHUD</c> exposes unique, bindable names for <see cref="BackgammonHudController"/>.
/// </summary>
public class BackgammonHudUxmlEditModeTests
{
    [Test]
    public void BackgammonHUD_Resources_Loads_And_HasRequiredNamedElements()
    {
        var vta = Resources.Load<VisualTreeAsset>("Layouts/BackgammonHUD");
        Assert.IsNotNull(vta, "Expected VisualTreeAsset at Resources path Layouts/BackgammonHUD");

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
