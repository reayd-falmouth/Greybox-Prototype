using System.Threading.Tasks;
using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    public interface IBackgammonAIEvaluator
    {
        Task<Turn> EvaluateBestTurnAsync(GameState state, MatchState match, int depth);

        Task<AiCubeDecision> EvaluateDoubleOfferAsync(GameState state, MatchState match);

        Task<AiDoubleResponseDecision> EvaluateDoubleResponseAsync(GameState state, MatchState match);

        void ClearCache();
    }
}
