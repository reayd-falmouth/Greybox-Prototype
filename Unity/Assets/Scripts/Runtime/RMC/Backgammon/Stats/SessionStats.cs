using System.Collections.Generic;

namespace Runtime.RMC.Backgammon.Stats
{
    public class SessionStats
    {
        public int GamesPlayedThisSession { get; private set; }
        public int GamesWonThisSession    { get; private set; }
        public int TotalRollsThisSession  { get; private set; }
        public int SessionScore           { get; private set; }
        public int SessionBankBalance     { get; private set; }
        public int TotalDiceRolled        { get; private set; }

        private readonly int[] _diceRollCounts = new int[7];
        public IReadOnlyList<int> DiceRollCounts => _diceRollCounts;

        public void RecordDiceRoll(int die1, int die2)
        {
            if (die1 >= 1 && die1 <= 6) { _diceRollCounts[die1]++; TotalDiceRolled++; }
            if (die2 >= 1 && die2 <= 6) { _diceRollCounts[die2]++; TotalDiceRolled++; }
            TotalRollsThisSession++;
        }

        public void RecordGameEnd(bool playerWon, int score, int bankBalance)
        {
            GamesPlayedThisSession++;
            if (playerWon) GamesWonThisSession++;
            SessionScore = score;
            SessionBankBalance = bankBalance;
        }

        public void Reset()
        {
            GamesPlayedThisSession = 0;
            GamesWonThisSession    = 0;
            TotalRollsThisSession  = 0;
            SessionScore           = 0;
            SessionBankBalance     = 0;
            TotalDiceRolled        = 0;
            for (int i = 0; i < _diceRollCounts.Length; i++)
                _diceRollCounts[i] = 0;
        }

        public float GetDiceFacePercent(int face)
        {
            if (face < 1 || face > 6 || TotalDiceRolled == 0) return 0f;
            return _diceRollCounts[face] / (float)TotalDiceRolled * 100f;
        }
    }
}
