using System.Collections.Generic;
using System.IO;
using EngineCore;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    public enum AiDoubleResponseAction
    {
        Take = 0,
        Drop = 1
    }

    public readonly struct AiCubeDecision
    {
        public readonly bool ShouldOffer;
        public readonly string Reason;
        public readonly bool FromAiEvaluator;

        public AiCubeDecision(bool shouldOffer, string reason, bool fromAiEvaluator)
        {
            ShouldOffer = shouldOffer;
            Reason = reason ?? string.Empty;
            FromAiEvaluator = fromAiEvaluator;
        }
    }

    public readonly struct AiDoubleResponseDecision
    {
        public readonly AiDoubleResponseAction Action;
        public readonly string Reason;
        public readonly bool FromAiEvaluator;

        public AiDoubleResponseDecision(AiDoubleResponseAction action, string reason, bool fromAiEvaluator)
        {
            Action = action;
            Reason = reason ?? string.Empty;
            FromAiEvaluator = fromAiEvaluator;
        }
    }

    /// <summary>Lazily loads GNUBG weights and exposes <see cref="SearchEngine"/>.</summary>
    public static class BackgammonAIService
    {
        private sealed class NetRoleSelection
        {
            public NeuralNet Contact;
            public NeuralNet Prune;
            public NeuralNet Crashed;
            public NeuralNet Race;
            public string ContactReason;
            public string PruneReason;
            public string CrashedReason;
            public string RaceReason;
        }

        static SearchEngine _engine;
        static CubeEvaluator _cubeEvaluator;
        static bool _loadFailed;
        static bool _loggedPruneFallback;
        static bool _loggedCrashedFallback;

        public static bool TryGetSearchEngine(out SearchEngine engine)
        {
            engine = null;
            if (_loadFailed) return false;
            if (_engine != null)
            {
                engine = _engine;
                return true;
            }

            try
            {
                string dir = BackgammonEnginePaths.GetDataDirectoryOrFallback(EnginePathResolver.GetDataDirectory);
                string weights = Path.Combine(dir, "gnubg.weights");
                List<NeuralNet> nets = WeightParser.Load(weights);
                NetRoleSelection roles = ResolveNetRoles(nets);
                NeuralNet contact = roles.Contact;

                if (contact == null)
                {
                    Debug.LogError("BackgammonAIService: no 250-input contact net in gnubg.weights.");
                    _loadFailed = true;
                    return false;
                }

                NeuralNet prune = roles.Prune;
                NeuralNet crashed = roles.Crashed;
                NeuralNet race = roles.Race;
                Debug.Log($"BackgammonAIService: net-role mapping contact={DescribeNet(contact)} ({roles.ContactReason}) prune={DescribeNet(prune)} ({roles.PruneReason}) crashed={DescribeNet(crashed)} ({roles.CrashedReason}) race={DescribeNet(race)} ({roles.RaceReason}).");
                if (prune == null)
                {
                    if (!_loggedPruneFallback)
                    {
                        Debug.LogWarning("BackgammonAIService: prune net not detected; AI will evaluate all legal turns.");
                        _loggedPruneFallback = true;
                    }
                }
                else
                {
                    Debug.Log($"BackgammonAIService: prune net detected (inputs={prune.InputCount}, hidden={prune.HiddenCount}, outputs={prune.OutputCount}).");
                }

                if (crashed == null)
                {
                    if (!_loggedCrashedFallback)
                    {
                        Debug.LogWarning("BackgammonAIService: crashed net not detected; using contact net fallback for crashed-class evaluations.");
                        _loggedCrashedFallback = true;
                    }
                }
                else
                {
                    Debug.Log($"BackgammonAIService: crashed net detected (inputs={crashed.InputCount}, hidden={crashed.HiddenCount}, outputs={crashed.OutputCount}).");
                }

                var bearoff = new BearoffEvaluator(dir);
                var cube = new CubeEvaluator();
                _cubeEvaluator = cube;
                _engine = new SearchEngine(contact, race, bearoff, cube, prune, crashed);
                engine = _engine;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("BackgammonAIService: failed to load AI — " + ex.Message);
                _loadFailed = true;
                return false;
            }
        }

        public static void ClearSearchEngineCache()
        {
            _engine?.ClearCache();
        }

        public static bool TryEvaluateAllTurns(
            GameState state,
            MatchState match,
            IReadOnlyList<Turn> turns,
            out List<(Turn turn, float equity)> rankedTurns)
        {
            rankedTurns = null;
            if (!TryGetSearchEngine(out var engine)) return false;
            if (turns == null || turns.Count == 0) return false;

            var results = new List<(Turn, float)>(turns.Count);
            foreach (var turn in turns)
            {
                if (turn?.ResultingState == null)
                {
                    results.Add((turn, 0f));
                    continue;
                }
                // Equity from the resulting position is evaluated from the opponent's POV, so negate
                float equity = -engine.GetEquity(turn.ResultingState, match);
                results.Add((turn, equity));
            }

            results.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            rankedTurns = results;
            return true;
        }

        public static bool TryEvaluateDoubleOffer(GameState state, MatchState match, out AiCubeDecision decision)
        {
            decision = new AiCubeDecision(false, "ai-unavailable", false);
            if (state == null || match == null)
            {
                decision = new AiCubeDecision(false, "invalid-state", false);
                return false;
            }

            if (!TryGetSearchEngine(out _))
            {
                decision = new AiCubeDecision(false, "search-engine-unavailable", false);
                return false;
            }

            if (_cubeEvaluator == null)
            {
                decision = new AiCubeDecision(false, "cube-evaluator-unavailable", false);
                return false;
            }

            if (TryInvokeCubeOfferDecision(_cubeEvaluator, state, match, out bool shouldOffer, out string reason))
            {
                decision = new AiCubeDecision(shouldOffer, reason, true);
                return true;
            }

            decision = new AiCubeDecision(false, "cube-evaluator-api-not-found", false);
            return false;
        }

        public static bool TryEvaluateDoubleResponse(GameState state, MatchState match, out AiDoubleResponseDecision decision)
        {
            decision = new AiDoubleResponseDecision(AiDoubleResponseAction.Take, "ai-unavailable-default-take", false);
            if (state == null || match == null)
            {
                decision = new AiDoubleResponseDecision(AiDoubleResponseAction.Take, "invalid-state-default-take", false);
                return false;
            }

            if (!TryGetSearchEngine(out _))
            {
                decision = new AiDoubleResponseDecision(AiDoubleResponseAction.Take, "search-engine-unavailable-default-take", false);
                return false;
            }

            if (_cubeEvaluator == null)
            {
                decision = new AiDoubleResponseDecision(AiDoubleResponseAction.Take, "cube-evaluator-unavailable-default-take", false);
                return false;
            }

            if (TryInvokeCubeResponseDecision(_cubeEvaluator, state, match, out AiDoubleResponseAction action, out string reason))
            {
                decision = new AiDoubleResponseDecision(action, reason, true);
                return true;
            }

            decision = new AiDoubleResponseDecision(AiDoubleResponseAction.Take, "cube-evaluator-api-not-found-default-take", false);
            return false;
        }

        private static bool TryInvokeCubeOfferDecision(CubeEvaluator cube, GameState state, MatchState match, out bool shouldOffer, out string reason)
        {
            shouldOffer = false;
            reason = "unknown";
            if (cube == null) return false;
            var t = cube.GetType();
            string[] candidateMethodNames =
            {
                "ShouldOfferDouble",
                "ShouldDouble",
                "EvaluateDoubleOffer"
            };
            for (int i = 0; i < candidateMethodNames.Length; i++)
            {
                var m = t.GetMethod(candidateMethodNames[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (m == null) continue;
                object result = m.GetParameters().Length switch
                {
                    2 => m.Invoke(cube, new object[] { state, match }),
                    1 => m.Invoke(cube, new object[] { state }),
                    _ => null
                };

                if (result is bool b)
                {
                    shouldOffer = b;
                    reason = $"cube-api:{candidateMethodNames[i]}";
                    return true;
                }
            }

            return false;
        }

        private static bool TryInvokeCubeResponseDecision(CubeEvaluator cube, GameState state, MatchState match, out AiDoubleResponseAction action, out string reason)
        {
            action = AiDoubleResponseAction.Take;
            reason = "unknown";
            if (cube == null) return false;
            var t = cube.GetType();
            string[] candidateMethodNames =
            {
                "ShouldTakeDouble",
                "EvaluateDoubleResponse",
                "ShouldAcceptDouble"
            };
            for (int i = 0; i < candidateMethodNames.Length; i++)
            {
                var m = t.GetMethod(candidateMethodNames[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (m == null) continue;
                object result = m.GetParameters().Length switch
                {
                    2 => m.Invoke(cube, new object[] { state, match }),
                    1 => m.Invoke(cube, new object[] { state }),
                    _ => null
                };
                if (result is bool take)
                {
                    action = take ? AiDoubleResponseAction.Take : AiDoubleResponseAction.Drop;
                    reason = $"cube-api:{candidateMethodNames[i]}";
                    return true;
                }
            }

            return false;
        }

        private static NetRoleSelection ResolveNetRoles(List<NeuralNet> nets)
        {
            NetRoleSelection roles = new NetRoleSelection();
            for (int i = 0; i < nets.Count; i++)
            {
                if (nets[i].InputCount == 250)
                {
                    roles.Contact = nets[i];
                    roles.ContactReason = "first 250-input net";
                    break;
                }
            }

            if (roles.Contact == null)
            {
                roles.ContactReason = "not-found";
                roles.PruneReason = "blocked:no-contact";
                roles.CrashedReason = "blocked:no-contact";
                roles.RaceReason = "blocked:no-contact";
                return roles;
            }

            roles.Prune = SelectPruneNet(nets, roles.Contact);
            roles.PruneReason = roles.Prune != null ? "smallest hidden-size compatible net below contact" : "not-found";
            roles.Crashed = SelectCrashedNet(nets, roles.Contact, roles.Prune);
            roles.CrashedReason = roles.Crashed != null ? "compatible net between prune/contact hidden sizes" : "not-found";
            roles.Race = SelectRaceNet(nets, roles.Contact, roles.Prune, roles.Crashed);
            roles.RaceReason = roles.Race != null ? "largest remaining compatible net" : "fallback-to-contact";
            return roles;
        }

        private static string DescribeNet(NeuralNet net)
        {
            if (net == null) return "none";
            return $"i{net.InputCount}-h{net.HiddenCount}-o{net.OutputCount}";
        }

        private static NeuralNet SelectPruneNet(List<NeuralNet> nets, NeuralNet contact)
        {
            NeuralNet best = null;
            for (int i = 0; i < nets.Count; i++)
            {
                NeuralNet candidate = nets[i];
                if (ReferenceEquals(candidate, contact)) continue;
                if (candidate.InputCount != contact.InputCount) continue;
                if (candidate.OutputCount != contact.OutputCount) continue;

                // Prefer smaller hidden layer than contact net, which is typical for prune nets.
                if (candidate.HiddenCount >= contact.HiddenCount) continue;
                if (best == null || candidate.HiddenCount < best.HiddenCount)
                    best = candidate;
            }

            return best;
        }

        private static NeuralNet SelectCrashedNet(List<NeuralNet> nets, NeuralNet contact, NeuralNet prune)
        {
            NeuralNet best = null;
            for (int i = 0; i < nets.Count; i++)
            {
                NeuralNet candidate = nets[i];
                if (ReferenceEquals(candidate, contact) || ReferenceEquals(candidate, prune)) continue;
                if (candidate.InputCount != contact.InputCount) continue;
                if (candidate.OutputCount != contact.OutputCount) continue;
                if (candidate.HiddenCount >= contact.HiddenCount) continue;
                if (prune != null && candidate.HiddenCount <= prune.HiddenCount) continue;

                if (best == null || candidate.HiddenCount > best.HiddenCount)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static NeuralNet SelectRaceNet(List<NeuralNet> nets, NeuralNet contact, NeuralNet prune, NeuralNet crashed)
        {
            NeuralNet best = null;
            for (int i = 0; i < nets.Count; i++)
            {
                NeuralNet candidate = nets[i];
                if (ReferenceEquals(candidate, contact) || ReferenceEquals(candidate, prune) || ReferenceEquals(candidate, crashed)) continue;
                if (candidate.InputCount != contact.InputCount) continue;
                if (candidate.OutputCount != contact.OutputCount) continue;

                if (best == null || candidate.HiddenCount > best.HiddenCount)
                {
                    best = candidate;
                }
            }

            return best;
        }
    }
}
