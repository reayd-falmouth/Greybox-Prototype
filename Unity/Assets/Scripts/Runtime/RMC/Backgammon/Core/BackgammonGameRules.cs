using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    public static class BackgammonGameRules
    {
        public static bool HasWon(int[] checkers)
        {
            int total = 0;
            for (int i = 0; i < checkers.Length; i++)
                total += checkers[i];
            return total == 0;
        }

        /// <summary>End of turn: opponent becomes logical P1 for MoveGenerator (see EngineCLI).</summary>
        public static void SwapSidesForNextTurn(GameState state)
        {
            state.PlayerOnRoll = 1 - state.PlayerOnRoll;
            (state.Player1Checkers, state.Player2Checkers) = (state.Player2Checkers, state.Player1Checkers);
        }

        public static void SyncBoardArrayFromCheckerArrays(GameState state)
        {
            for (int i = 0; i <= 24; i++)
            {
                state.Board[0, i] = state.Player1Checkers[i];
                state.Board[1, i] = state.Player2Checkers[i];
            }
        }
    }
}
