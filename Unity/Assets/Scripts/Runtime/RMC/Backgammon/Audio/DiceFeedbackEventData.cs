/// <summary>Payload for <see cref="BackgammonGameController.OnDiceFeedbackEvent"/>.</summary>
public readonly struct DiceFeedbackEventData
{
    public readonly DiceFeedbackEventType EventType;
    /// <summary>Doubling cube value after the rule applied (e.g. after autodouble).</summary>
    public readonly int CubeValueAfter;
    /// <summary>Opening roll: P0 die value (e.g. tie value for autodouble).</summary>
    public readonly int OpeningDiePlayer0;
    /// <summary>Opening roll: P1 die value.</summary>
    public readonly int OpeningDiePlayer1;
    /// <summary>Opening winner player index (0/1) when event is OpeningRollWinnerResolved; otherwise -1.</summary>
    public readonly int OpeningRollWinnerPlayerIndex;
    /// <summary>Winner player index (0/1) for GameEnded; otherwise -1.</summary>
    public readonly int GameWinnerPlayerIndex;
    /// <summary>Points awarded during game settlement for GameEnded.</summary>
    public readonly int GamePointsAwarded;
    /// <summary>String reason for game-end diagnostics (e.g. "bear-off", "double-drop").</summary>
    public readonly string GameEndReason;

    public DiceFeedbackEventData(
        DiceFeedbackEventType eventType,
        int cubeValueAfter,
        int openingDiePlayer0 = 0,
        int openingDiePlayer1 = 0,
        int openingRollWinnerPlayerIndex = -1,
        int gameWinnerPlayerIndex = -1,
        int gamePointsAwarded = 0,
        string gameEndReason = null)
    {
        EventType = eventType;
        CubeValueAfter = cubeValueAfter;
        OpeningDiePlayer0 = openingDiePlayer0;
        OpeningDiePlayer1 = openingDiePlayer1;
        OpeningRollWinnerPlayerIndex = openingRollWinnerPlayerIndex;
        GameWinnerPlayerIndex = gameWinnerPlayerIndex;
        GamePointsAwarded = gamePointsAwarded;
        GameEndReason = gameEndReason;
    }
}
