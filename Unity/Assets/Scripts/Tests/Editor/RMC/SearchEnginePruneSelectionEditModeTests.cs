using System;
using System.Collections.Generic;
using System.Reflection;
using EngineCore;
using NUnit.Framework;

public class SearchEnginePruneSelectionEditModeTests
{
    [Test]
    public void SelectTurnsForFullEvaluation_PruneNetPresent_UsesAdaptivePruneCount()
    {
        var contact = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune);
        var legalTurns = BuildTurns(40);

        List<Turn> selected = engine.SelectTurnsForFullEvaluation(legalTurns, CreateMoneyMatch());

        // minPrune(5) + floor(log2(40)) == 10
        Assert.AreEqual(10, selected.Count);
        Assert.AreEqual(10, engine.LastPrunedCandidateCount);
    }

    [Test]
    public void SelectTurnsForFullEvaluation_HeavyNode_UsesLowerDynamicPruneCap()
    {
        var contact = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune);
        var legalTurns = BuildTurns(260);

        List<Turn> selected = engine.SelectTurnsForFullEvaluation(legalTurns, CreateMoneyMatch());

        // minPrune(5) + floor(log2(260)) == 13, but heavy-node cap clamps to 12.
        Assert.AreEqual(12, selected.Count);
        Assert.AreEqual(12, engine.LastPrunedCandidateCount);
    }

    [Test]
    public void SelectTurnsForFullEvaluation_PruneNetMissing_KeepsAllCandidates()
    {
        var contact = CreateNet(250, 4, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), null);
        var legalTurns = BuildTurns(8);

        List<Turn> selected = engine.SelectTurnsForFullEvaluation(legalTurns, CreateMoneyMatch());

        Assert.AreEqual(8, selected.Count);
        Assert.AreEqual(8, engine.LastPrunedCandidateCount);
    }

    [Test]
    public void SelectTurnsForFullEvaluation_FiveOrFewerCandidates_DoesNotPrune()
    {
        var contact = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune);
        var legalTurns = BuildTurns(4);

        List<Turn> selected = engine.SelectTurnsForFullEvaluation(legalTurns, CreateMoneyMatch());

        Assert.AreEqual(4, selected.Count);
        Assert.AreEqual(4, engine.LastPrunedCandidateCount);
    }

    [Test]
    public void SelectTurnsForFullEvaluation_PruningDisabled_KeepsAllCandidates()
    {
        var contact = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune);
        var legalTurns = BuildTurns(30);

        List<Turn> selected = engine.SelectTurnsForFullEvaluation(legalTurns, CreateMoneyMatch(), allowPruning: false);

        Assert.AreEqual(30, selected.Count);
        Assert.AreEqual(30, engine.LastPrunedCandidateCount);
    }

    [Test]
    public void ShouldPreferCandidate_OnScoreTie_UsesTieBreakerThenPositionId()
    {
        bool betterTieBreaker = SearchEngine.ShouldPreferCandidate(
            candidateScore: 0.2f,
            candidateTieBreaker: 0.11f,
            candidatePositionId: "bbb",
            currentBestScore: 0.2f,
            currentBestTieBreaker: 0.10f,
            currentBestPositionId: "aaa");

        bool betterPositionId = SearchEngine.ShouldPreferCandidate(
            candidateScore: 0.2f,
            candidateTieBreaker: 0.10f,
            candidatePositionId: "aaa",
            currentBestScore: 0.2f,
            currentBestTieBreaker: 0.10f,
            currentBestPositionId: "bbb");

        bool worsePositionId = SearchEngine.ShouldPreferCandidate(
            candidateScore: 0.2f,
            candidateTieBreaker: 0.10f,
            candidatePositionId: "zzz",
            currentBestScore: 0.2f,
            currentBestTieBreaker: 0.10f,
            currentBestPositionId: "bbb");

        Assert.IsTrue(betterTieBreaker);
        Assert.IsTrue(betterPositionId);
        Assert.IsFalse(worsePositionId);
    }

    [Test]
    public void GetBestTurn_WhenLegalMovesExist_ReturnsTurn()
    {
        var contact = CreateNet(250, 4, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), null);
        GameState state = CreateState(0);
        state.Dice1 = 1;
        state.Dice2 = 1;

        Turn bestTurn = engine.GetBestTurn(state, CreateMoneyMatch(), depth: 1);

        Assert.IsNotNull(bestTurn);
        Assert.Greater(engine.LastLegalTurnCount, 0);
    }

    [Test]
    public void GetBestTurn_WhenNoLegalMovesExist_ReturnsNull()
    {
        var contact = CreateNet(250, 4, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), null);
        var state = new GameState
        {
            Dice1 = 0,
            Dice2 = 0,
            PlayerOnRoll = 0
        };

        Turn bestTurn = engine.GetBestTurn(state, CreateMoneyMatch(), depth: 1);

        Assert.IsNull(bestTurn);
        Assert.AreEqual(0, engine.LastLegalTurnCount);
    }

    [Test]
    public void CountUniqueTurnKeys_DuplicateTurns_CollapsesToUniqueCount()
    {
        var turns = BuildTurns(6);
        // Add exact duplicate references and a deep-ish duplicate.
        turns.Add(turns[1]);
        turns.Add(new Turn
        {
            Moves = new List<Move>
            {
                new Move
                {
                    From = turns[2].Moves[0].From,
                    To = turns[2].Moves[0].To,
                    IsHit = turns[2].Moves[0].IsHit
                }
            },
            ResultingState = CloneState(turns[2].ResultingState)
        });

        int uniqueCount = SearchEngine.CountUniqueTurnKeys(turns);

        Assert.Less(uniqueCount, turns.Count);
        Assert.AreEqual(6, uniqueCount);
    }

    [Test]
    public void ShouldPreferCandidate_SameInput_AlwaysDeterministic()
    {
        bool first = SearchEngine.ShouldPreferCandidate(0.4f, 0.2f, "aaa", 0.4f, 0.2f, "bbb");
        bool second = SearchEngine.ShouldPreferCandidate(0.4f, 0.2f, "aaa", 0.4f, 0.2f, "bbb");

        Assert.AreEqual(first, second);
        Assert.IsTrue(first);
    }

    [Test]
    public void GetBestTurn_PopulatesStageTelemetry()
    {
        var contact = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune)
        {
            QualityPreset = SearchEngine.SearchQualityPreset.Balanced
        };

        GameState state = CreateStateWithLegalTurns(3);
        Turn bestTurn = engine.GetBestTurn(state, CreateMoneyMatch(), depth: 2);

        Assert.IsNotNull(bestTurn);
        Assert.IsNotNull(engine.LastTelemetry.StageSummary);
        Assert.Greater(engine.LastUniqueCandidatePositionIds.Count, 0);
        Assert.Greater(engine.LastPruneCandidatePositionIds.Count, 0);
        Assert.AreEqual(engine.LastTelemetry.FinalCandidates, engine.LastFinalCandidatePositionIds.Count);
    }

    [Test]
    public void ComparePipelines_ReturnsBothBestTurns()
    {
        var contact = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune);
        GameState state = CreateStateWithLegalTurns(1);

        var result = engine.ComparePipelines(state, CreateMoneyMatch(), depth: 2);

        Assert.IsNotNull(result.baselineBest);
        Assert.IsNotNull(result.candidateBest);
    }

    [Test]
    public void GetBestTurn_Revalidation_DoesNotCollapseToTopTwoOnly()
    {
        var contact = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), prune)
        {
            QualityPreset = SearchEngine.SearchQualityPreset.Balanced
        };

        GameState state = CreateStateWithLegalTurns(2);

        Turn bestTurn = engine.GetBestTurn(state, CreateMoneyMatch(), depth: 2);

        Assert.IsNotNull(bestTurn);
        Assert.GreaterOrEqual(engine.LastTelemetry.FinalCandidates, 3);
    }

    [Test]
    public void CloneStateForOpponent_SwapsCheckerArraysForOpponentTurnSimulation()
    {
        var contact = CreateNet(250, 4, 5);
        var engine = new SearchEngine(contact, null, new BearoffEvaluator(""), new CubeEvaluator(), null);
        var state = new GameState
        {
            PlayerOnRoll = 0,
            Dice1 = 1,
            Dice2 = 1,
            CubeValue = 1
        };
        state.Player1Checkers[7] = 2;
        state.Player2Checkers[23] = 3;

        MethodInfo cloneForOpponent = typeof(SearchEngine).GetMethod(
            "CloneStateForOpponent",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(cloneForOpponent, "Expected CloneStateForOpponent to exist.");

        var opponentState = (GameState)cloneForOpponent.Invoke(engine, new object[] { state, 3, 4 });
        Assert.IsNotNull(opponentState);
        Assert.AreEqual(1, opponentState.PlayerOnRoll);
        Assert.AreEqual(3, opponentState.Dice1);
        Assert.AreEqual(4, opponentState.Dice2);
        Assert.AreEqual(3, opponentState.Player1Checkers[23], "Opponent checkers should become side-to-move checkers.");
        Assert.AreEqual(2, opponentState.Player2Checkers[7], "Original side-to-move checkers should become waiting side.");
    }

    [Test]
    public void SelectPrimaryNetForClass_UsesCrashedNetWhenProvided()
    {
        var contact = CreateNet(250, 6, 5);
        var race = CreateNet(250, 5, 5);
        var prune = CreateNet(250, 2, 5);
        var crashed = CreateNet(250, 4, 5);
        var engine = new SearchEngine(contact, race, new BearoffEvaluator(""), new CubeEvaluator(), prune, crashed);

        MethodInfo selectPrimary = typeof(SearchEngine).GetMethod(
            "SelectPrimaryNetForClass",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(selectPrimary, "Expected SelectPrimaryNetForClass helper to exist.");

        object selected = selectPrimary.Invoke(engine, new object[] { PositionClass.Crashed });
        Assert.AreSame(crashed, selected, "Crashed class should route to crashed net when configured.");
    }

    [Test]
    public void SelectOpponentTurnsForEvaluation_UsesTwoTierPruneAndReturnsBoundedCount()
    {
        var contact = CreateNet(250, 6, 5);
        var race = CreateNet(250, 5, 5);
        var crashed = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, race, new BearoffEvaluator(""), new CubeEvaluator(), prune, crashed);
        var legalTurns = BuildTurns(200);

        MethodInfo selectOpponent = typeof(SearchEngine).GetMethod(
            "SelectOpponentTurnsForEvaluation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(selectOpponent, "Expected SelectOpponentTurnsForEvaluation helper to exist.");

        List<Turn> selected = (List<Turn>)selectOpponent.Invoke(engine, new object[] { legalTurns, CreateMoneyMatch() });
        Assert.IsNotNull(selected);
        Assert.GreaterOrEqual(selected.Count, 3);
        Assert.LessOrEqual(selected.Count, 10);
    }

    [Test]
    public void EncodingPolicy_ClassSpecificVerified_FallsBackDeterministically()
    {
        var contact = CreateNet(250, 6, 5);
        var race = CreateNet(250, 5, 5);
        var crashed = CreateNet(250, 4, 5);
        var prune = CreateNet(250, 2, 5);
        var engine = new SearchEngine(contact, race, new BearoffEvaluator(""), new CubeEvaluator(), prune, crashed)
        {
            EncodingPolicy = SearchEngine.FeatureEncodingPolicy.ClassSpecificVerified
        };

        MethodInfo encode = typeof(SearchEngine).GetMethod(
            "EncodeFeaturesForClass",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(encode, "Expected EncodeFeaturesForClass helper to exist.");

        float[] raceFeatures = (float[])encode.Invoke(engine, new object[] { PositionClass.Race, CreateState(0).Player1Checkers, CreateState(0).Player2Checkers });
        float[] crashFeatures = (float[])encode.Invoke(engine, new object[] { PositionClass.Crashed, CreateState(1).Player1Checkers, CreateState(1).Player2Checkers });
        Assert.IsNotNull(raceFeatures);
        Assert.IsNotNull(crashFeatures);
        Assert.AreEqual(250, raceFeatures.Length);
        Assert.AreEqual(250, crashFeatures.Length);
    }

    private static List<Turn> BuildTurns(int count)
    {
        var turns = new List<Turn>(count);
        for (int i = 0; i < count; i++)
        {
            turns.Add(new Turn
            {
                Moves = new List<Move> { new Move { From = 24, To = 23 - (i % 6), IsHit = false } },
                ResultingState = CreateState(i)
            });
        }

        return turns;
    }

    private static GameState CreateState(int i)
    {
        var state = new GameState
        {
            PlayerOnRoll = i % 2,
            Dice1 = 3,
            Dice2 = 1,
            CubeValue = 1
        };
        state.Player1Checkers[23] = 2 + (i % 3);
        state.Player1Checkers[24] = i % 2;
        state.Player2Checkers[0] = 2 + ((i + 1) % 3);
        state.Player2Checkers[24] = (i + 1) % 2;
        return state;
    }

    private static GameState CreateStateWithLegalTurns(int seed)
    {
        GameState state = CreateState(seed);
        for (int die1 = 1; die1 <= 6; die1++)
        {
            for (int die2 = 1; die2 <= 6; die2++)
            {
                state.Dice1 = die1;
                state.Dice2 = die2;
                if (MoveGenerator.GenerateLegalTurns(state).Count > 0)
                {
                    return state;
                }
            }
        }

        Assert.Fail("Expected at least one dice pair to produce legal turns for test state.");
        return state;
    }

    private static MatchState CreateMoneyMatch()
    {
        return new MatchState
        {
            MatchLength = 0,
            Cube = 1,
            CubeOwner = -1
        };
    }

    private static NeuralNet CreateNet(int inputCount, int hiddenCount, int outputCount)
    {
        return new NeuralNet(inputCount, hiddenCount, outputCount, 0, 1f, 1f);
    }

    private static GameState CloneState(GameState original)
    {
        var clone = new GameState
        {
            PlayerOnRoll = original.PlayerOnRoll,
            Dice1 = original.Dice1,
            Dice2 = original.Dice2,
            CubeValue = original.CubeValue
        };
        Array.Copy(original.Player1Checkers, clone.Player1Checkers, original.Player1Checkers.Length);
        Array.Copy(original.Player2Checkers, clone.Player2Checkers, original.Player2Checkers.Length);
        return clone;
    }
}
