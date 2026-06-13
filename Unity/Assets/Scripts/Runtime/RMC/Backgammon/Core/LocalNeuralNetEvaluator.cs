using System.Threading.Tasks;
using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    public class LocalNeuralNetEvaluator : IBackgammonAIEvaluator
    {
        public Task<Turn> EvaluateBestTurnAsync(GameState state, MatchState match, int depth)
        {
            if (!BackgammonAIService.TryGetSearchEngine(out SearchEngine engine))
                return Task.FromResult<Turn>(null);

            return Task.Run(() => engine.GetBestTurn(state, match, depth));
        }

        public Task<AiCubeDecision> EvaluateDoubleOfferAsync(GameState state, MatchState match)
        {
            BackgammonAIService.TryEvaluateDoubleOffer(state, match, out var decision);
            return Task.FromResult(decision);
        }

        public Task<AiDoubleResponseDecision> EvaluateDoubleResponseAsync(GameState state, MatchState match)
        {
            BackgammonAIService.TryEvaluateDoubleResponse(state, match, out var decision);
            return Task.FromResult(decision);
        }

        public void ClearCache()
        {
            BackgammonAIService.ClearSearchEngineCache();
        }
    }
}
