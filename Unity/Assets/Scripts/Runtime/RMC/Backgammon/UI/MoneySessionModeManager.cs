using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD mode provider for Money Session games. Supplies display strings
/// and routes game-start logic for the MoneySession game mode.
/// </summary>
public class MoneySessionModeManager : HudModeProviderBase
{
    private MoneySessionConfig _pendingConfig;

    public override GameModeType SupportedMode => GameModeType.MoneySession;

    public override string ScoreDisplay
    {
        get
        {
            var world = PrestigeService.CurrentPreset;
            string sym = world?.currencySymbol ?? "$";
            return $"{sym}{MoneyBalanceService.Balance:N0}";
        }
    }

    public override string GamesDisplay
    {
        get
        {
            if (GameController == null) return "—";
            return (GameController.MoneySessionGamesPlayed + 1).ToString();
        }
    }

    public override string StakeDisplay
    {
        get
        {
            if (GameController == null) return "—";
            int stake = GameController.MoneySessionBaseStake;
            if (stake <= 0) return "—";
            var world = PrestigeService.CurrentPreset;
            string sym = world?.currencySymbol ?? "$";
            return $"{sym}{stake}";
        }
    }

    public override string HeadingDisplay
    {
        get
        {
            var world = PrestigeService.CurrentPreset;
            return world != null ? $"Money Session — {world.displayName}" : "Stones X Dice";
        }
    }

    public override void BindToHud(VisualElement root, BackgammonHudController hud)
    {
        base.BindToHud(root, hud);
        MoneyBalanceService.OnBalanceChanged  += OnExternalChange;
        PrestigeService.OnPrestigeChanged     += OnPrestigeChange;
    }

    public override void UnbindFromHud()
    {
        MoneyBalanceService.OnBalanceChanged  -= OnExternalChange;
        PrestigeService.OnPrestigeChanged     -= OnPrestigeChange;
        base.UnbindFromHud();
    }

    private void OnExternalChange(int _) => Hud?.RefreshAll(GameController);
    private void OnPrestigeChange()       => Hud?.RefreshAll(GameController);

    /// <summary>Called by BackgammonHudController.OnStartNewGame before StartGame.</summary>
    public void Configure(MoneySessionConfig config) => _pendingConfig = config;

    public override void StartGame(string seedString, string startingPositionId)
    {
        GameController?.StartNewGameWithConfig(
            GameModeType.MoneySession,
            _pendingConfig,
            startingPositionId,
            seedString);
    }
}
