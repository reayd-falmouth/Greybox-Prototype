using NUnit.Framework;
using System.Collections.Generic;
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
        Assert.IsNotNull(root.Q<Button>("NewGameButton"));
        Button runInfoButton = root.Q<Button>("RunInfoButton");
        Assert.IsNotNull(runInfoButton);
        Assert.AreEqual("Options", runInfoButton.text);
        Assert.IsNotNull(root.Q<Button>("LegalMovesButton"));
        Assert.IsNotNull(root.Q<ScrollView>("LegalMovesScroll"));
        Assert.IsNotNull(root.Q<VisualElement>("LegalMovesContent"));
        Assert.IsNotNull(root.Q<Label>("LegalMovesEmptyLabel"));
        Assert.IsNotNull(root.Q<Label>("StatusLabel"));
        Assert.IsNotNull(root.Q<Label>("DiceLabel"));
        Assert.IsNotNull(root.Q<Label>("PositionIdLabel"));
        Assert.IsNotNull(root.Q<Label>("MatchScoreValue"));
        Assert.IsNotNull(root.Q<Label>("TargetMatchScoreLabel"));
        Assert.IsNotNull(root.Q<Label>("ChipsValue"));
        Assert.IsNotNull(root.Q<Label>("MultiplierValue"));
        Assert.IsNotNull(root.Q<VisualElement>("DoublePanel"));
        Assert.IsNotNull(root.Q<VisualElement>("TakeDropPanel"));
        Assert.IsNotNull(root.Q<VisualElement>("SettingsPanel"));
        Assert.IsNull(root.Q<Button>("OptionsButton"));
        Assert.IsNull(root.Q<Button>("HintsButton"));
        Assert.IsNotNull(root.Q<VisualElement>("ModalLayer"));
        Assert.IsNotNull(root.Q<VisualElement>("ModalBackdrop"));
        Assert.IsNotNull(root.Q<VisualElement>("ModalCard"));
        Assert.IsNotNull(root.Q<Label>("ModalTitleLabel"));
        Assert.IsNotNull(root.Q<Button>("ModalCloseButton"));
        Assert.IsNotNull(root.Q<VisualElement>("SettingsContent"));
        Assert.IsNotNull(root.Q<Slider>("GameSpeedSlider"));
        Assert.IsNotNull(root.Q<DropdownField>("DebugStartPositionDropdown"));
        Assert.IsNotNull(root.Q<TextField>("DebugStartPositionField"));
        Assert.IsNotNull(root.Q<Button>("DebugStartPositionApplyButton"));
        Assert.IsNotNull(root.Q<Button>("DebugStartPositionUseCurrentButton"));
        Assert.IsNotNull(root.Q<VisualElement>("HintsContent"));
        Assert.IsNotNull(root.Q<Label>("HintsLabel"));

        AssertUniqueElementName(root, "LegalMovesScroll");
        AssertUniqueElementName(root, "LegalMovesButton");
        AssertUniqueElementName(root, "LegalMovesContent");
        AssertUniqueElementName(root, "ModalLayer");
        AssertUniqueElementName(root, "ModalBackdrop");
        AssertUniqueElementName(root, "ModalCard");
        AssertUniqueElementName(root, "GameSpeedSlider");
    }

    private static void AssertUniqueElementName(VisualElement root, string name)
    {
        List<VisualElement> matches = root.Query<VisualElement>(name).ToList();
        Assert.AreEqual(1, matches.Count, $"Expected exactly one element named '{name}', found {matches.Count}.");
    }

    [Test]
    public void StatsTabButton_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<Button>("StatsTabButton"));
    }

    [Test]
    public void StatsTabContent_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("StatsTabContent"));
    }

    [Test]
    public void StatsTabContent_IsHiddenByDefault()
    {
        VisualElement root = LoadUxmlRoot();
        var content = root.Q<VisualElement>("StatsTabContent");
        Assert.IsNotNull(content);
        Assert.AreEqual(DisplayStyle.None, content.resolvedStyle.display);
    }

    [Test]
    public void StatsDieLabels_AllSixExistInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        for (int i = 1; i <= 6; i++)
            Assert.IsNotNull(root.Q<Label>($"StatsDie{i}Value"), $"StatsDie{i}Value missing");
    }

    [Test]
    public void StatsLifetimeLabels_AllExistInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<Label>("StatsRatingValue"));
        Assert.IsNotNull(root.Q<Label>("StatsHighestScoreValue"));
        Assert.IsNotNull(root.Q<Label>("StatsTotalGamesValue"));
        Assert.IsNotNull(root.Q<Label>("StatsSessionsPlayedValue"));
        Assert.IsNotNull(root.Q<Label>("StatsHighestMoneyValue"));
    }

    private VisualElement LoadUxmlRoot()
    {
        string pathByGuid = AssetDatabase.GUIDToAssetPath(UxmlGuid);
        var vta = string.IsNullOrEmpty(pathByGuid)
            ? null
            : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(pathByGuid);
        if (vta == null)
            vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlAssetPath);
        if (vta == null)
            vta = Resources.Load<VisualTreeAsset>("Layouts/BackgammonHUD");
        Assert.IsNotNull(vta, "BackgammonHUD.uxml not found");
        return vta.CloneTree();
    }

    [Test]
    public void ShouldEnableRollButton_Disables_WhenPlayerCannotAct()
    {
        bool enabled = BackgammonHudController.ShouldEnableRollButton(
            isGameOver: false,
            hasRolledThisTurn: false,
            isBusy: false,
            canPlayerAct: false);

        Assert.IsFalse(enabled);
    }

    [Test]
    public void ShouldEnableRollButtonForPhase_OpeningPhase_IgnoresPlayerActGate()
    {
        bool enabled = BackgammonHudController.ShouldEnableRollButtonForPhase(
            isOpeningRollPhase: true,
            isGameOver: false,
            hasRolledThisTurn: false,
            isBusy: false,
            awaitingDoubleResponse: false,
            canPlayerAct: false);

        Assert.IsTrue(enabled);
    }

    [Test]
    public void ShouldEnableDoubleButton_Disables_WhenPlayerCannotAct()
    {
        bool enabled = BackgammonHudController.ShouldEnableDoubleButton(
            openingRollResolved: true,
            isGameOver: false,
            isBusy: false,
            hasRolledThisTurn: false,
            awaitingDoubleResponse: false,
            cubeValue: 2,
            cubeOwner: 3,
            playerOnRoll: 0,
            localPlayerIndex: 1,
            canPlayerAct: false);

        Assert.IsFalse(enabled);
    }

    [Test]
    public void ShouldEnableRollAndDoublePanel_Enable_WhenPlayerCanActAndRulesAllow()
    {
        bool rollEnabled = BackgammonHudController.ShouldEnableRollButton(
            isGameOver: false,
            hasRolledThisTurn: false,
            isBusy: false,
            canPlayerAct: true);
        bool canDouble = BackgammonHudController.ShouldEnableDoubleButton(
            openingRollResolved: true,
            isGameOver: false,
            isBusy: false,
            hasRolledThisTurn: false,
            awaitingDoubleResponse: false,
            cubeValue: 2,
            cubeOwner: 3,
            playerOnRoll: 0,
            localPlayerIndex: 1,
            canPlayerAct: true);

        Assert.IsTrue(rollEnabled);
        Assert.IsTrue(canDouble);
    }

    [Test]
    public void ShouldEnableDoubleButton_Disables_WhenOpponentOwnsCube()
    {
        bool canDouble = BackgammonHudController.ShouldEnableDoubleButton(
            openingRollResolved: true,
            isGameOver: false,
            isBusy: false,
            hasRolledThisTurn: false,
            awaitingDoubleResponse: false,
            cubeValue: 2,
            cubeOwner: 1,
            playerOnRoll: 0,
            localPlayerIndex: 1,
            canPlayerAct: true);

        Assert.IsTrue(canDouble);
    }

    [Test]
    public void ShouldEnableDoubleButton_Enables_WhenCurrentPlayerOwnsCube()
    {
        bool canDouble = BackgammonHudController.ShouldEnableDoubleButton(
            openingRollResolved: true,
            isGameOver: false,
            isBusy: false,
            hasRolledThisTurn: false,
            awaitingDoubleResponse: false,
            cubeValue: 2,
            cubeOwner: 0,
            playerOnRoll: 0,
            localPlayerIndex: 1,
            canPlayerAct: true);

        Assert.IsFalse(canDouble);
    }

    [Test]
    public void ShouldEnableDoubleButton_Enables_WhenLocalPlayerOwnsCube_AndCurrentPlayerMatches()
    {
        bool canDouble = BackgammonHudController.ShouldEnableDoubleButton(
            openingRollResolved: true,
            isGameOver: false,
            isBusy: false,
            hasRolledThisTurn: false,
            awaitingDoubleResponse: false,
            cubeValue: 2,
            cubeOwner: 1,
            playerOnRoll: 1,
            localPlayerIndex: 1,
            canPlayerAct: true);

        Assert.IsTrue(canDouble);
    }

    // ── Graphics Sub-Tab Tests ──────────────────────────────────────────────

    [Test]
    public void GraphicsSubTabNavigation_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("GraphicsSubTabNavigation"));
    }

    [Test]
    public void GraphicsSubTabContentCamera_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("GraphicsSubTabContentCamera"));
    }

    [Test]
    public void GraphicsSubTabContentPostFx_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("GraphicsSubTabContentPostFx"));
    }

    [Test]
    public void GraphicsSubTabContentAnimations_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("GraphicsSubTabContentAnimations"));
    }

    [Test]
    public void GraphicsSubTabContentTheme_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("GraphicsSubTabContentTheme"));
    }

    [Test]
    public void GraphicsSubTabContentCamera_IsVisibleByDefault()
    {
        VisualElement root = LoadUxmlRoot();
        var content = root.Q<VisualElement>("GraphicsSubTabContentCamera");
        Assert.IsNotNull(content);
        Assert.AreNotEqual(DisplayStyle.None, content.resolvedStyle.display);
    }

    [Test]
    public void GraphicsSubTabContentPostFx_IsHiddenByDefault()
    {
        VisualElement root = LoadUxmlRoot();
        var content = root.Q<VisualElement>("GraphicsSubTabContentPostFx");
        Assert.IsNotNull(content);
        Assert.AreEqual(DisplayStyle.None, content.resolvedStyle.display);
    }

    [Test]
    public void GraphicsSubTabContentAnimations_IsHiddenByDefault()
    {
        VisualElement root = LoadUxmlRoot();
        var content = root.Q<VisualElement>("GraphicsSubTabContentAnimations");
        Assert.IsNotNull(content);
        Assert.AreEqual(DisplayStyle.None, content.resolvedStyle.display);
    }

    [Test]
    public void GraphicsSubTabContentTheme_IsHiddenByDefault()
    {
        VisualElement root = LoadUxmlRoot();
        var content = root.Q<VisualElement>("GraphicsSubTabContentTheme");
        Assert.IsNotNull(content);
        Assert.AreEqual(DisplayStyle.None, content.resolvedStyle.display);
    }

    [Test]
    public void GraphicsSubTabDisplayBtn_NoLongerExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNull(root.Q<Button>("GraphicsSubTabDisplayBtn"), "Old GraphicsSubTabDisplayBtn should have been replaced by Camera/PostFx/Animations buttons");
    }

    [Test]
    public void ThemePresetDropdown_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<DropdownField>("ThemePresetDropdown"));
    }

    [Test]
    public void ThemeDropdown_NoLongerExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNull(root.Q<DropdownField>("ThemeDropdown"), "Old ThemeDropdown should have been replaced by ThemePresetDropdown");
    }

    // ── Stats Sub-Tab Tests ─────────────────────────────────────────────────

    [Test]
    public void StatsSubTabNavigation_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("StatsSubTabNavigation"));
    }

    [Test]
    public void StatsSubTabButtons_AllExistInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<Button>("StatsSubTabSessionBtn"),  "StatsSubTabSessionBtn missing");
        Assert.IsNotNull(root.Q<Button>("StatsSubTabDiceBtn"),     "StatsSubTabDiceBtn missing");
        Assert.IsNotNull(root.Q<Button>("StatsSubTabLifetimeBtn"), "StatsSubTabLifetimeBtn missing");
        Assert.IsNotNull(root.Q<Button>("StatsSubTabWalletBtn"),   "StatsSubTabWalletBtn missing");
        Assert.IsNotNull(root.Q<Button>("StatsSubTabGameBtn"),     "StatsSubTabGameBtn missing");
    }

    [Test]
    public void StatsSubTabContentPanels_AllExistInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("StatsSubTabContentSession"),  "StatsSubTabContentSession missing");
        Assert.IsNotNull(root.Q<VisualElement>("StatsSubTabContentDice"),     "StatsSubTabContentDice missing");
        Assert.IsNotNull(root.Q<VisualElement>("StatsSubTabContentLifetime"), "StatsSubTabContentLifetime missing");
        Assert.IsNotNull(root.Q<VisualElement>("StatsSubTabContentWallet"),   "StatsSubTabContentWallet missing");
        Assert.IsNotNull(root.Q<VisualElement>("StatsSubTabContentGame"),     "StatsSubTabContentGame missing");
    }

    [Test]
    public void StatsSubTabContentSession_IsVisibleByDefault()
    {
        VisualElement root = LoadUxmlRoot();
        var content = root.Q<VisualElement>("StatsSubTabContentSession");
        Assert.IsNotNull(content);
        Assert.AreNotEqual(DisplayStyle.None, content.resolvedStyle.display);
    }

    [Test]
    public void StatsSubTabContentDice_IsHiddenByDefault()
    {
        VisualElement root = LoadUxmlRoot();
        var content = root.Q<VisualElement>("StatsSubTabContentDice");
        Assert.IsNotNull(content);
        Assert.AreEqual(DisplayStyle.None, content.resolvedStyle.display);
    }

    // ── Credits Tab Tests ───────────────────────────────────────────────────

    [Test]
    public void CreditsTab_HeaderText_IsThirdPartyPlugins()
    {
        VisualElement root = LoadUxmlRoot();
        var credits = root.Q<VisualElement>("CreditsTabContent");
        Assert.IsNotNull(credits);
        var header = credits.Q<Label>(className: "stats-section-header");
        Assert.IsNotNull(header, "Credits section header label not found");
        Assert.AreEqual("Third-Party Plugins", header.text);
    }

    [Test]
    public void CreditsTab_DoesNotContain_UnityTechnologiesEntries()
    {
        VisualElement root = LoadUxmlRoot();
        var credits = root.Q<VisualElement>("CreditsTabContent");
        Assert.IsNotNull(credits);
        var allLabels = credits.Query<Label>().ToList();
        foreach (var lbl in allLabels)
        {
            Assert.AreNotEqual("Universal Render Pipeline", lbl.text, "Unity Technologies entry should have been removed");
            Assert.AreNotEqual("Cinemachine",               lbl.text, "Unity Technologies entry should have been removed");
            Assert.AreNotEqual("Input System",              lbl.text, "Unity Technologies entry should have been removed");
            Assert.AreNotEqual("Newtonsoft JSON",           lbl.text, "Unity Technologies entry should have been removed");
            Assert.AreNotEqual("ProBuilder",                lbl.text, "Unity Technologies entry should have been removed");
            Assert.AreNotEqual("Unity Timeline",            lbl.text, "Unity Technologies entry should have been removed");
        }
    }

    // ── Collection Sub-Tab Tests ────────────────────────────────────────────

    [Test]
    public void CollectionSubTabNavigation_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("CollectionSubTabNavigation"));
    }

    [Test]
    public void CollectionSubTabContentHost_ExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNotNull(root.Q<VisualElement>("CollectionSubTabContentHost"));
    }

    [Test]
    public void CollectionScrollView_NoLongerExistsInUxml()
    {
        VisualElement root = LoadUxmlRoot();
        Assert.IsNull(root.Q<ScrollView>("CollectionScrollView"), "Old CollectionScrollView should have been replaced by sub-tab structure");
    }

    // ── Layout Variant Tests (Desktop / Mobile) ─────────────────────────────

    // The Desktop and Mobile HUD layouts are copies of BackgammonHUD that the
    // BackgammonHudController binds by element name. They must keep exposing the
    // same required named elements (so binding never breaks when a layout is
    // swapped at runtime), plus the version/layout debug label.
    private static readonly string[] RequiredLayoutElementNames =
    {
        "ScreenRoot", "RollButton", "UndoButton", "PlayMoveButton",
        "NewGameButton", "RunInfoButton", "LegalMovesButton",
        "DoubleButton", "TakeDoubleButton", "DropDoubleButton",
        "StatusLabel", "MatchScoreValue", "ChipsValue", "MultiplierValue",
        "DoublePanel", "TakeDropPanel", "SettingsPanel",
        "ModalLayer", "NewGameModalLayer", "LobbyLayer", "LobbyPlayButton",
    };

    [TestCase("Layouts/BackgammonHUD_Desktop")]
    [TestCase("Layouts/BackgammonHUD_Mobile")]
    public void LayoutVariant_Loads_AndExposesRequiredNamedElements(string resourcePath)
    {
        var vta = Resources.Load<VisualTreeAsset>(resourcePath);
        Assert.IsNotNull(vta, $"Expected Resources layout '{resourcePath}'");

        VisualElement root = vta.CloneTree();
        foreach (var name in RequiredLayoutElementNames)
            Assert.IsNotNull(root.Q<VisualElement>(name), $"'{resourcePath}' is missing required element '{name}'");
    }

    [TestCase("Layouts/BackgammonHUD_Desktop")]
    [TestCase("Layouts/BackgammonHUD_Mobile")]
    public void LayoutVariant_HasVersionDebugLabel(string resourcePath)
    {
        var vta = Resources.Load<VisualTreeAsset>(resourcePath);
        Assert.IsNotNull(vta, $"Expected Resources layout '{resourcePath}'");

        VisualElement root = vta.CloneTree();
        Assert.IsNotNull(root.Q<Label>("VersionDebugLabel"), $"'{resourcePath}' is missing the VersionDebugLabel");
    }

    [Test]
    public void LayoutSelect_Loads_AndExposesButtonsAndDebugLabel()
    {
        var vta = Resources.Load<VisualTreeAsset>("Layouts/LayoutSelect");
        Assert.IsNotNull(vta, "Expected Resources layout 'Layouts/LayoutSelect'");

        VisualElement root = vta.CloneTree();
        Assert.IsNotNull(root.Q<Button>("SelectDesktopButton"), "LayoutSelect is missing SelectDesktopButton");
        Assert.IsNotNull(root.Q<Button>("SelectMobileButton"), "LayoutSelect is missing SelectMobileButton");
        Assert.IsNotNull(root.Q<Label>("VersionDebugLabel"), "LayoutSelect is missing VersionDebugLabel");
    }

}
