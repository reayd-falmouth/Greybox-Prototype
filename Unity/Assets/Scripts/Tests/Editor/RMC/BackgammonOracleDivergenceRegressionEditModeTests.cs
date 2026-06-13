using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

namespace Runtime.RMC.Backgammon.Tests.Editor
{
    public class BackgammonOracleDivergenceRegressionEditModeTests
    {
        [Test]
        public void Snapshot_Dice11_ContainsTurnMatchingGnubgTopHintCanonicalPath()
        {
            // Deterministic repro snapshot from oracle divergence report.
            GameState state = PositionId.Decode("mGfwATDgc/ABMA");
            state.Dice1 = 1;
            state.Dice2 = 1;

            List<Turn> turns = MoveGenerator.GenerateLegalTurns(state);
            Assert.IsNotEmpty(turns, "Expected legal turns for repro snapshot.");

            string gnubgHint = "8/7(2) 6/5(2)";
            List<string> hintCandidates = InvokeBuildHintMovePathCandidates(gnubgHint);
            Assert.IsNotEmpty(hintCandidates, "Expected at least one move-path candidate parsed from GNUbg hint.");

            HashSet<string> canonicalHintCandidates = new HashSet<string>(
                hintCandidates.Select(CanonicalizeMovePath),
                StringComparer.Ordinal);

            bool foundMatchingTurn = turns
                .Where(t => t?.Moves != null && t.Moves.Count > 0)
                .Select(t => string.Join(";", t.Moves.Select(m => $"{m.From}->{m.To}")))
                .Select(CanonicalizeMovePath)
                .Any(canonical => canonicalHintCandidates.Contains(canonical));

            Assert.IsTrue(
                foundMatchingTurn,
                "Expected legal-turn space to include GNUbg top-hint move path (canonical-equivalent).");
        }

        [Test]
        public void OraclePositionIdComparator_NormalizesSwapAndFlipVariants()
        {
            GameState state = PositionId.Decode("4HPwATDgc/ABMA");
            // Make the board explicitly asymmetric so transform variants are meaningful.
            state.Player1Checkers[0] = Math.Max(0, state.Player1Checkers[0] - 1);
            state.Player1Checkers[5] += 1;
            string gnubgResultingId = PositionId.Encode(state) + ":MYAEAAAAAAAA";

            string flipOnRollId = BuildFlipOnRollId(state);
            List<string> candidates = new List<string> { flipOnRollId };
            bool normalizedMatch = InvokeContainsPositionId(candidates, gnubgResultingId);

            Assert.IsTrue(normalizedMatch, "Normalized compare should match swap/flip equivalent candidate IDs.");
        }

        [Test]
        public void Snapshot_Dice11_SearchEngineBestTurn_MatchesGnubgCanonicalHintPath()
        {
            if (!BackgammonAIService.TryGetSearchEngine(out SearchEngine engine))
            {
                Assert.Ignore("BackgammonAIService search engine unavailable in this environment.");
                return;
            }

            GameState state = PositionId.Decode("mGfwATDgc/ABMA");
            state.Dice1 = 1;
            state.Dice2 = 1;

            var match = new MatchState
            {
                MatchLength = 0,
                Cube = 1,
                CubeOwner = -1
            };

            Turn best = engine.GetBestTurn(state, match, depth: 2);
            Assert.IsNotNull(best, "Expected a best turn for repro snapshot.");
            string bestPath = string.Join(";", best.Moves.Select(m => $"{m.From}->{m.To}"));
            string bestCanonical = CanonicalizeMovePath(bestPath);

            List<string> hintCandidates = InvokeBuildHintMovePathCandidates("8/7(2) 6/5(2)");
            HashSet<string> canonicalHintCandidates = new HashSet<string>(
                hintCandidates.Select(CanonicalizeMovePath),
                StringComparer.Ordinal);

            Assert.IsTrue(
                canonicalHintCandidates.Contains(bestCanonical),
                $"Expected engine best turn to match GNUbg canonical hint path. best={bestPath}");
        }

        [Test]
        public void Snapshot_Dice33_HeavyBranch_ProducesBoundedFinalCandidateCount()
        {
            if (!BackgammonAIService.TryGetSearchEngine(out SearchEngine engine))
            {
                Assert.Ignore("BackgammonAIService search engine unavailable in this environment.");
                return;
            }

            GameState state = PositionId.Decode("mOfgYQCwC3nAKA");
            state.Dice1 = 3;
            state.Dice2 = 3;
            var match = new MatchState
            {
                MatchLength = 0,
                Cube = 1,
                CubeOwner = -1
            };

            Turn best = engine.GetBestTurn(state, match, depth: 2);
            Assert.IsNotNull(best, "Expected a best turn for heavy-branch snapshot.");
            Assert.Greater(engine.LastLegalTurnCount, 100, "Expected heavy legal turn count for this regression snapshot.");
            Assert.LessOrEqual(engine.LastTelemetry.FinalCandidates, 18, "Expected staged filtering to keep finalists bounded.");
        }

        [Test]
        public void BuildHintMovePathCandidates_SupportsBarAndOffNotation()
        {
            List<string> candidates = InvokeBuildHintMovePathCandidates("bar/20 6/off");
            Assert.IsNotEmpty(candidates, "Expected candidates for GNUbg bar/off notation.");

            bool containsCommonZeroBasedPath = candidates.Any(path =>
                string.Equals(path, "24->19;5->0", StringComparison.Ordinal) ||
                string.Equals(path, "24->19;6->0", StringComparison.Ordinal));
            Assert.IsTrue(containsCommonZeroBasedPath, "Expected at least one zero-based candidate mapping for bar/off notation.");
        }

        [TestCase("mGfwATDgc/ABMA", 1, 1)]
        [TestCase("mGfwISCwG/gAWA", 5, 6)]
        [TestCase("mGfwIQSwC3xAWA", 2, 2)]
        [TestCase("mOfgYQCwC3nAKA", 3, 3)]
        public void Snapshot_Regressions_ProduceDeterministicBestTurn(string positionId, int die1, int die2)
        {
            if (!BackgammonAIService.TryGetSearchEngine(out SearchEngine engine))
            {
                Assert.Ignore("BackgammonAIService search engine unavailable in this environment.");
                return;
            }

            GameState state = PositionId.Decode(positionId);
            state.Dice1 = die1;
            state.Dice2 = die2;
            var match = new MatchState
            {
                MatchLength = 0,
                Cube = 1,
                CubeOwner = -1
            };

            Turn first = engine.GetBestTurn(state, match, depth: 2);
            Turn second = engine.GetBestTurn(state, match, depth: 2);

            Assert.IsNotNull(first, $"Expected best turn for snapshot {positionId} {die1}/{die2}.");
            Assert.IsNotNull(second, $"Expected deterministic re-run for snapshot {positionId} {die1}/{die2}.");
            Assert.AreEqual(first.Moves.Count, second.Moves.Count, $"Move count should be deterministic for snapshot {positionId}.");
            Assert.Greater(engine.LastLegalTurnCount, 0, $"Expected legal turns for snapshot {positionId}.");
            Assert.Greater(engine.LastTelemetry.FinalCandidates, 0, $"Expected finalists for snapshot {positionId}.");
        }

        private static string BuildFlipOnRollId(GameState state)
        {
            var flipOnRoll = new GameState
            {
                Player1Checkers = (int[])state.Player1Checkers.Clone(),
                Player2Checkers = (int[])state.Player2Checkers.Clone(),
                CubeValue = state.CubeValue,
                CubeOwner = state.CubeOwner,
                PlayerOnRoll = 1 - state.PlayerOnRoll,
                PlayerToDecide = state.PlayerToDecide,
                Dice1 = state.Dice1,
                Dice2 = state.Dice2,
                MatchLength = state.MatchLength,
                Player1Score = state.Player1Score,
                Player2Score = state.Player2Score
            };

            return PositionId.Encode(flipOnRoll);
        }

        private static List<string> InvokeBuildHintMovePathCandidates(string hintMoveText)
        {
            MethodInfo method = typeof(BackgammonGameController).GetMethod(
                "BuildHintMovePathCandidates",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Expected BuildHintMovePathCandidates helper to exist.");

            object result = method.Invoke(null, new object[] { hintMoveText });
            Assert.IsInstanceOf<List<string>>(result);
            return (List<string>)result;
        }

        private static bool InvokeContainsPositionId(IReadOnlyList<string> candidateIds, string gnubgResultingId)
        {
            MethodInfo method = typeof(BackgammonGameController).GetMethod(
                "ContainsPositionId",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Expected ContainsPositionId helper to exist.");

            object result = method.Invoke(null, new object[] { candidateIds, gnubgResultingId });
            return result is bool value && value;
        }

        private static string CanonicalizeMovePath(string movePath)
        {
            if (string.IsNullOrWhiteSpace(movePath))
            {
                return string.Empty;
            }

            return string.Join(
                ";",
                movePath.Split(';')
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .OrderBy(s => s, StringComparer.Ordinal));
        }
    }
}
