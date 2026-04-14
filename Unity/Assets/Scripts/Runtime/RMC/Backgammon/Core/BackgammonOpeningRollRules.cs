using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>Opening roll: first die = player 0, second = player 1; tie = false (re-roll).</summary>
    public static class BackgammonOpeningRollRules
    {
        public static int ApplyOpeningTieAutodouble(GameState state)
        {
            int current = state != null ? state.CubeValue : 1;
            int safeCurrent = current <= 0 ? 1 : current;
            int doubled = safeCurrent >= 64 ? 64 : safeCurrent * 2;
            if (state != null)
                state.CubeValue = doubled;
            return doubled;
        }

        public static bool TryApplyOpeningDice(int dieForPlayer0, int dieForPlayer1, GameState state)
        {
            if (dieForPlayer0 == dieForPlayer1) return false;
            state.PlayerOnRoll = dieForPlayer0 > dieForPlayer1 ? 0 : 1;
            state.Dice1 = dieForPlayer0;
            state.Dice2 = dieForPlayer1;
            return true;
        }
    }
}
