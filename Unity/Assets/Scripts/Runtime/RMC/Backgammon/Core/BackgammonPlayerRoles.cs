namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// When playing vs AI (<see cref="Settings.BackgammonSettings.OpponentIsAi"/>), Player 0 is the AI and Player 1 is the human.
    /// </summary>
    public static class BackgammonPlayerRoles
    {
        public static bool IsAiTurnInOpponentAiMode(int playerOnRoll) => playerOnRoll == 0;
    }
}
