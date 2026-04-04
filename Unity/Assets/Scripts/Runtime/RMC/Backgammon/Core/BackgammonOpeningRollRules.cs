using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>Opening roll: first die = player 0, second = player 1; tie = false (re-roll).</summary>
    public static class BackgammonOpeningRollRules
    {
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
