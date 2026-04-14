using EngineCore;

public readonly struct CheckerSoundEventData
{
    public readonly CheckerSoundEventType EventType;
    public readonly int MoverPlayerIndex;
    public readonly int From;
    public readonly int To;
    public readonly bool IsHit;
    public readonly bool IsUndo;

    public CheckerSoundEventData(
        CheckerSoundEventType eventType,
        int moverPlayerIndex,
        int from,
        int to,
        bool isHit,
        bool isUndo)
    {
        EventType = eventType;
        MoverPlayerIndex = moverPlayerIndex;
        From = from;
        To = to;
        IsHit = isHit;
        IsUndo = isUndo;
    }

    public static CheckerSoundEventData FromMove(CheckerSoundEventType eventType, int moverPlayerIndex, Move move, bool isUndo)
    {
        return new CheckerSoundEventData(
            eventType,
            moverPlayerIndex,
            move.From,
            move.To,
            move.IsHit,
            isUndo);
    }
}
