using Runtime.RMC.Backgammon.Core;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD mode provider for Match Play games. Supplies display strings
/// and routes game-start logic for the MatchPlay game mode.
/// </summary>
public class MatchPlayModeManager : HudModeProviderBase
{
    private Label _targetMatchScoreLabel;

    public override GameModeType SupportedMode => GameModeType.MatchPlay;

    public override string ScoreDisplay
        => GameController != null ? GameController.CurrentMatchScore.ToString() : "0";

    public override string GamesDisplay
    {
        get
        {
            if (GameController == null) return "—";
            int maxGames  = Mathf.Max(1, GameController.CurrentMatchMaxGames);
            int gamesLeft = Mathf.Max(0, maxGames - GameController.CurrentMatchGamesPlayed);
            return $"{gamesLeft}/{maxGames}";
        }
    }

    public override string StakeDisplay
        => GameController != null ? $"${GameController.CurrentMatchBaseStake}" : "—";

    public override string HeadingDisplay => "Match Play";

    public override void BindToHud(VisualElement root, BackgammonHudController hud)
    {
        base.BindToHud(root, hud);
        _targetMatchScoreLabel = root.Q<Label>("TargetMatchScoreLabel");
    }

    public override void UnbindFromHud()
    {
        _targetMatchScoreLabel = null;
        base.UnbindFromHud();
    }

    public override void RefreshModeHud(VisualElement root, BackgammonGameController ctrl)
    {
        if (_targetMatchScoreLabel != null)
            _targetMatchScoreLabel.text = $"Target Score: {ctrl.CurrentMatchTargetScore}";
    }

    public override void StartGame(string seedString, string startingPositionId)
    {
        GameController?.StartNewGameWithConfig(
            GameModeType.MatchPlay,
            null,
            startingPositionId,
            seedString);
    }
}
