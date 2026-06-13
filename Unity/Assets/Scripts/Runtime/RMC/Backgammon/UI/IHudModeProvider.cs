using Runtime.RMC.Backgammon.Core;
using UnityEngine.UIElements;

/// <summary>
/// Implemented by each game-mode manager to supply mode-specific display strings
/// and behaviour to BackgammonHudController without coupling the HUD to any
/// concrete manager type.
/// </summary>
public interface IHudModeProvider
{
    GameModeType SupportedMode { get; }

    /// <summary>Main score label. e.g. "$10 vs $20", "50", "3".</summary>
    string ScoreDisplay   { get; }

    /// <summary>Games label. Return null to hide the element.</summary>
    string GamesDisplay   { get; }

    /// <summary>Stake label. e.g. "$1", "50" (Run threshold).</summary>
    string StakeDisplay   { get; }

    /// <summary>Heading label. e.g. "Money Session", "Ante 1 — Small".</summary>
    string HeadingDisplay { get; }

    /// <summary>Called from RefreshAll() after shared labels are set.</summary>
    void RefreshModeHud(VisualElement root, BackgammonGameController ctrl);

    /// <summary>Routes the New Game modal confirm action for this mode.</summary>
    void StartGame(string seedString, string startingPositionId);

    /// <summary>Called from HUD OnEnable — provider binds its own popup elements and events.</summary>
    void BindToHud(VisualElement root, BackgammonHudController hud);

    /// <summary>Called from HUD OnDisable — provider unbinds and cleans up.</summary>
    void UnbindFromHud();
}
