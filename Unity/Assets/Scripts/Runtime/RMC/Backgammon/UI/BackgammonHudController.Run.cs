using Runtime.RMC.Backgammon;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.UI;
using UnityEngine.UIElements;

/// <summary>
/// Partial class — Run Mode popup show/hide methods and display utilities.
/// Element refs are queried at call time from uiDocument; binding/unbinding
/// and refresh logic live in BackgammonRunModeManager (the Run mode provider).
/// </summary>
public partial class BackgammonHudController
{
    public void ShowRunCashoutPopup(RunSessionResult result)
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        var layer = root.Q<VisualElement>("RunCashoutPopupLayer");
        if (layer == null) return;

        string bossLine = result.SessionType == RunSessionType.Boss
            ? $"Boss: {GetBossVariantDisplayName(result.BossVariant)}"
            : string.Empty;

        var titleLabel    = root.Q<Label>("RunCashoutTitleLabel");
        var bossLabel     = root.Q<Label>("RunCashoutBossLabel");
        var scoreLabel    = root.Q<Label>("RunCashoutScoreLabel");
        var rewardLabel   = root.Q<Label>("RunCashoutRewardLabel");
        var collectButton = root.Q<Button>("RunCashoutCollectButton");

        if (titleLabel    != null) titleLabel.text    = "Session Complete!";
        if (bossLabel     != null) bossLabel.text     = bossLine;
        if (scoreLabel    != null) scoreLabel.text    = $"Score: {result.Score} / {result.Threshold}";
        if (rewardLabel   != null) rewardLabel.text   = $"Reward: ${result.Reward}";
        if (collectButton != null) collectButton.text = $"Collect ${result.Reward}";

        layer.style.display = DisplayStyle.Flex;
        var cashoutCard = root.Q<VisualElement>("RunCashoutPopupCard");
        PopupAnimator.Show(cashoutCard, this);
        PlayPopupOpenSound();
    }

    public void HideRunCashoutPopup()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null) return;
        var layer = root.Q<VisualElement>("RunCashoutPopupLayer");
        var card  = root.Q<VisualElement>("RunCashoutPopupCard");
        if (card != null) PopupAnimator.Hide(card, this, () => { if (layer != null) layer.style.display = DisplayStyle.None; });
    }

    public void ShowRunOverPopup(int totalCurrency)
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        var layer = root.Q<VisualElement>("RunOverPopupLayer");
        if (layer == null) return;

        var currencyLabel = root.Q<Label>("RunOverCurrencyLabel");
        if (currencyLabel != null) currencyLabel.text = $"Total earned: ${totalCurrency}";

        layer.style.display = DisplayStyle.Flex;
        var runOverCard = root.Q<VisualElement>("RunOverPopupCard");
        PopupAnimator.Show(runOverCard, this);
        PlayPopupOpenSound();
    }

    public void HideRunOverPopup()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null) return;
        var layer = root.Q<VisualElement>("RunOverPopupLayer");
        var card  = root.Q<VisualElement>("RunOverPopupCard");
        if (card != null) PopupAnimator.Hide(card, this, () => { if (layer != null) layer.style.display = DisplayStyle.None; });
    }

    public static string GetBossVariantDisplayName(BossVariantType variant)
    {
        return variant switch
        {
            BossVariantType.NoCube          => "Frozen Cube",
            BossVariantType.NoDoublets      => "Cursed Dice",
            BossVariantType.HigherThreshold => "Sudden Death",
            BossVariantType.FewerGames      => "Blitz",
            BossVariantType.Nackgammon      => "Nackgammon",
            BossVariantType.Hypergammon     => "Hypergammon",
            BossVariantType.AceyDeucey      => "Acey-Deucey",
            _                               => "Standard",
        };
    }
}
