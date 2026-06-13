/// <summary>Game events that can trigger per-die MMF feedback (Feel). Add values at the end to keep serialized order stable.</summary>
public enum DiceFeedbackEventType
{
    OpeningRollTieAutodouble = 0,
    OpeningRollTieDiceResetPickup = 1,
    OpeningRollWinnerResolved = 2,
    GeneralDiceResetPickup = 3,
    CubeValueChanged = 4,
    GameEnded = 5,
    NoLegalMoves = 6,
    CubeOffered = 7,
    BeaverOffered = 8
}
