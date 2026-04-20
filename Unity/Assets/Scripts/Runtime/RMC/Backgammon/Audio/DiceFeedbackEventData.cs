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

    public DiceFeedbackEventData(
        DiceFeedbackEventType eventType,
        int cubeValueAfter,
        int openingDiePlayer0 = 0,
        int openingDiePlayer1 = 0,
        int openingRollWinnerPlayerIndex = -1)
    {
        EventType = eventType;
        CubeValueAfter = cubeValueAfter;
        OpeningDiePlayer0 = openingDiePlayer0;
        OpeningDiePlayer1 = openingDiePlayer1;
        OpeningRollWinnerPlayerIndex = openingRollWinnerPlayerIndex;
    }
}
