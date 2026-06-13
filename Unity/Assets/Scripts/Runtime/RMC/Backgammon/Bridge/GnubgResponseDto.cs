using System;

namespace Runtime.RMC.Backgammon.Bridge
{
    /// <summary>
    /// DTO matching GNUBG Python bridge JSON output format (from MoneySession)
    /// </summary>
    [Serializable]
    public class GnubgResponseDto
    {
        public int[] bestMove;              // Flat array: [13, 11, 24, 18]
        public GnubgHintContainer hint;     // Container with nested hint array
        public object[] cfevaluate;         // Mixed array for cube decisions
    }

    [Serializable]
    public class GnubgHintContainer
    {
        public string gnubgid;
        public GnubgHintMove[] hint;        // Array named "hint" inside container
    }

    [Serializable]
    public class GnubgHintMove
    {
        public int movenum;
        public string move;                 // e.g. "13/11 24/18"
        public float equity;
        public float eqdiff;
        public string type;
    }
}
