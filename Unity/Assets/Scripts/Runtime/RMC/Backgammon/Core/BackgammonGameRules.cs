using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    public static class BackgammonGameRules
    {
        private const int CheckersPerPlayer = 15;

        public static bool HasWon(int[] checkers)
        {
            return CountCheckersOnBoardOrBar(checkers) == 0;
        }

        public static int CountCheckersOnBoardOrBar(int[] checkers)
        {
            int total = 0;
            for (int i = 0; i < checkers.Length; i++)
                total += checkers[i];
            return total;
        }

        public static int CountBorneOffCheckers(int[] checkers)
        {
            return CheckersPerPlayer - CountCheckersOnBoardOrBar(checkers);
        }

        public static bool IsGammonLoss(int[] loserCheckers)
        {
            return CountBorneOffCheckers(loserCheckers) == 0;
        }

        public static bool IsBackgammonLoss(int[] loserCheckers, int barEngineIndex)
        {
            if (!IsGammonLoss(loserCheckers))
                return false;

            if (barEngineIndex >= 0 && barEngineIndex < loserCheckers.Length && loserCheckers[barEngineIndex] > 0)
                return true;

            // Engine-side checker arrays can be represented from either player perspective depending on turn normalization.
            // Treat either home quadrant as eligible so we don't miss backgammon classification at game end.
            for (int point = 0; point <= 5 && point < loserCheckers.Length; point++)
            {
                if (loserCheckers[point] > 0)
                    return true;
            }

            for (int point = 18; point <= 23 && point < loserCheckers.Length; point++)
            {
                if (loserCheckers[point] > 0)
                    return true;
            }

            return false;
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

        public static bool ValidateCheckerCounts(GameState state, out string error)
        {
            int p1Total = CountCheckersOnBoardOrBar(state.Player1Checkers);
            int p2Total = CountCheckersOnBoardOrBar(state.Player2Checkers);

            if (p1Total > CheckersPerPlayer)
            {
                error = $"Player1 has {p1Total} checkers (max {CheckersPerPlayer})";
                return false;
            }
            if (p2Total > CheckersPerPlayer)
            {
                error = $"Player2 has {p2Total} checkers (max {CheckersPerPlayer})";
                return false;
            }
            error = null;
            return true;
        }

        public static string GetCheckerDistribution(GameState state)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Player1: {CountCheckersOnBoardOrBar(state.Player1Checkers)} total");
            for (int i = 0; i < 25; i++)
                if (state.Player1Checkers[i] > 0)
                    sb.AppendLine($"  [{i}]: {state.Player1Checkers[i]}");

            sb.AppendLine($"Player2: {CountCheckersOnBoardOrBar(state.Player2Checkers)} total");
            for (int i = 0; i < 25; i++)
                if (state.Player2Checkers[i] > 0)
                    sb.AppendLine($"  [{i}]: {state.Player2Checkers[i]}");

            return sb.ToString();
        }
    }
}
