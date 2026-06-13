using EngineCore;
using NUnit.Framework;

public class SearchEngineAbHarnessEditModeTests
{
    [Test]
    public void ComparePipelines_SeedScenario_ReturnsDeterministicCandidates()
    {
        var contact = new NeuralNet(250, 4, 5, 0, 1f, 1f);
        var prune = new NeuralNet(250, 2, 5, 0, 1f, 1f);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune);

        var state = new GameState
        {
            PlayerOnRoll = 0,
            Dice1 = 1,
            Dice2 = 1,
            CubeValue = 1
        };
        state.Player1Checkers[23] = 2;
        state.Player1Checkers[24] = 1;
        state.Player2Checkers[0] = 2;
        state.Player2Checkers[24] = 1;
        EnsureStateHasLegalTurns(state);

        var match = new MatchState
        {
            MatchLength = 0,
            Cube = 1,
            CubeOwner = -1
        };

        var first = engine.ComparePipelines(state, match, depth: 2);
        var second = engine.ComparePipelines(state, match, depth: 2);

        Assert.IsNotNull(first.baselineBest);
        Assert.IsNotNull(first.candidateBest);
        Assert.AreEqual(first.baselineBest.Moves.Count, second.baselineBest.Moves.Count);
        Assert.AreEqual(first.candidateBest.Moves.Count, second.candidateBest.Moves.Count);
    }

    private static void EnsureStateHasLegalTurns(GameState state)
    {
        for (int die1 = 1; die1 <= 6; die1++)
        {
            for (int die2 = 1; die2 <= 6; die2++)
            {
                state.Dice1 = die1;
                state.Dice2 = die2;
                if (MoveGenerator.GenerateLegalTurns(state).Count > 0)
                {
                    return;
                }
            }
        }

        Assert.Fail("Expected at least one dice pair to produce legal turns for AB harness state.");
    }
}
