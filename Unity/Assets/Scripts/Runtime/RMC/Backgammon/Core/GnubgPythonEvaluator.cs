using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using EngineCore;
using Runtime.RMC.Backgammon.Bridge;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    public class GnubgPythonEvaluator : IBackgammonAIEvaluator
    {
        public async Task<Turn> EvaluateBestTurnAsync(GameState state, MatchState match, int depth)
        {
            try
            {
                string positionId = PositionId.Encode(state);
                string matchId = MatchId.Encode(match);
                string gnubgId = $"{positionId}:{matchId}";

                Task<string> bridgeTask = InvokeGnubgBridgeAsync(
                    matchRef: Guid.NewGuid().ToString(),
                    gameId: gnubgId,
                    variation: "standard",
                    jacoby: match.JacobyRule,
                    action: "hint"
                );

                string jsonPath = await bridgeTask;

                if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
                {
                    Debug.LogError("[GNUBG] JSON output not found");
                    return null;
                }

                string jsonContent = File.ReadAllText(jsonPath);
                Debug.LogWarning($"[GNUBG] ===== RAW JSON RESPONSE =====\n{jsonContent}\n================================");
                var response = JsonUtility.FromJson<GnubgResponseDto>(jsonContent);

                // Prefer hint array with string moves (more reliable than int array)
                if (response?.hint?.hint != null && response.hint.hint.Length > 0)
                {
                    string moveString = response.hint.hint[0].move;
                    Debug.LogWarning($"[GNUBG] Found {response.hint.hint.Length} hint entries");
                    Debug.LogWarning($"[GNUBG] Using hint[0].move: '{moveString}'");
                    return GnubgTurnAdapter.ParseMove(moveString, state);
                }

                // Fall back to bestMove int array (needs proper decoding - currently unsupported)
                if (response?.bestMove != null && response.bestMove.Length > 0)
                {
                    Debug.LogWarning($"[GNUBG] bestMove int array format not yet implemented: [{string.Join(", ", response.bestMove)}]");
                    return null;
                }

                Debug.LogWarning("[GNUBG] No move found in response");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GNUBG] Evaluation failed: {ex.Message}");
                return null;
            }
        }

        private static string ConvertBestMoveToString(int[] bestMove)
        {
            // bestMove format: [from1, to1, from2, to2, ...]
            // Convert to GNUBG string format: "from1/to1 from2/to2"
            if (bestMove == null || bestMove.Length == 0 || bestMove.Length % 2 != 0)
                return string.Empty;

            System.Collections.Generic.List<string> moves = new System.Collections.Generic.List<string>();
            for (int i = 0; i < bestMove.Length; i += 2)
            {
                moves.Add($"{bestMove[i]}/{bestMove[i + 1]}");
            }
            return string.Join(" ", moves);
        }

        public async Task<AiCubeDecision> EvaluateDoubleOfferAsync(GameState state, MatchState match)
        {
            try
            {
                string positionId = PositionId.Encode(state);
                string matchId = MatchId.Encode(match);
                string gnubgId = $"{positionId}:{matchId}";

                // Use "hint" action - the Python script always returns cubeinfo with any action
                Task<string> bridgeTask = InvokeGnubgBridgeAsync(
                    matchRef: Guid.NewGuid().ToString(),
                    gameId: gnubgId,
                    variation: "standard",
                    jacoby: match.JacobyRule,
                    action: "hint"
                );

                string jsonPath = await bridgeTask;

                if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
                    return new AiCubeDecision(false, "gnubg-no-output", false);

                string jsonContent = File.ReadAllText(jsonPath);

                // Parse the cubeinfo from JSON manually since Unity's JsonUtility is limited
                bool shouldOffer = ParseCubeOfferFromJson(jsonContent);
                return new AiCubeDecision(shouldOffer, "gnubg:cubeinfo-parsed", true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GNUBG] Cube offer eval failed: {ex.Message}");
                return new AiCubeDecision(false, "gnubg-error", false);
            }
        }

        private bool ParseCubeOfferFromJson(string jsonContent)
        {
            // The cfevaluate array format: [equity1, equity2, equity3, value, decision_code, "recommendation"]
            // Recommendations like "No double, beaver", "Double, take", "Double, pass", etc.
            // We look for explicit "Double" in the recommendation string

            int cfevalStart = jsonContent.IndexOf("\"cfevaluate\":");
            if (cfevalStart < 0) return false;

            int arrayStart = jsonContent.IndexOf("[", cfevalStart);
            if (arrayStart < 0) return false;

            int arrayEnd = jsonContent.IndexOf("]", arrayStart);
            if (arrayEnd < 0) return false;

            string arrayContent = jsonContent.Substring(arrayStart, arrayEnd - arrayStart);

            // Look for the recommendation string at the end of the array
            // It should contain "Double" if we should double
            int lastQuoteStart = arrayContent.LastIndexOf('"');
            if (lastQuoteStart < 0) return false;

            int secondLastQuoteStart = arrayContent.LastIndexOf('"', lastQuoteStart - 1);
            if (secondLastQuoteStart < 0) return false;

            string recommendation = arrayContent.Substring(secondLastQuoteStart + 1,
                lastQuoteStart - secondLastQuoteStart - 1);

            // Check if recommendation suggests doubling
            // Common patterns: "Double, take", "Double, pass", "Too good to double"
            if (recommendation.StartsWith("Double", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public async Task<AiDoubleResponseDecision> EvaluateDoubleResponseAsync(
            GameState state, MatchState match)
        {
            try
            {
                string positionId = PositionId.Encode(state);
                string matchId = MatchId.Encode(match);
                string gnubgId = $"{positionId}:{matchId}";

                // Use "hint" action - the Python script always returns cfevaluate with any action
                Task<string> bridgeTask = InvokeGnubgBridgeAsync(
                    matchRef: Guid.NewGuid().ToString(),
                    gameId: gnubgId,
                    variation: "standard",
                    jacoby: match.JacobyRule,
                    action: "hint"
                );

                string jsonPath = await bridgeTask;

                if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
                    return new AiDoubleResponseDecision(AiDoubleResponseAction.Take, "gnubg-no-output-default-take", false);

                string jsonContent = File.ReadAllText(jsonPath);

                // Parse the cfevaluate array from JSON
                // Format: [equity0, equity1, equity2, decision, ?, "recommendation string"]
                AiDoubleResponseAction action = ParseCubeResponseFromJson(jsonContent);
                string reason = action == AiDoubleResponseAction.Take ? "gnubg:take" : "gnubg:drop";
                return new AiDoubleResponseDecision(action, reason, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GNUBG] Cube response eval failed: {ex.Message}");
                return new AiDoubleResponseDecision(AiDoubleResponseAction.Take, "gnubg-error-default-take", false);
            }
        }

        private AiDoubleResponseAction ParseCubeResponseFromJson(string jsonContent)
        {
            // The cfevaluate array format: [equity1, equity2, equity3, value, decision_code, "recommendation"]
            // Recommendations when opponent doubles: "Take", "Pass", "Drop", etc.

            int cfevalStart = jsonContent.IndexOf("\"cfevaluate\":");
            if (cfevalStart < 0) return AiDoubleResponseAction.Take;

            int arrayStart = jsonContent.IndexOf("[", cfevalStart);
            if (arrayStart < 0) return AiDoubleResponseAction.Take;

            int arrayEnd = jsonContent.IndexOf("]", arrayStart);
            if (arrayEnd < 0) return AiDoubleResponseAction.Take;

            string arrayContent = jsonContent.Substring(arrayStart, arrayEnd - arrayStart);

            // Get the last string in the array (the recommendation)
            int lastQuoteStart = arrayContent.LastIndexOf('"');
            if (lastQuoteStart < 0) return AiDoubleResponseAction.Take;

            int secondLastQuoteStart = arrayContent.LastIndexOf('"', lastQuoteStart - 1);
            if (secondLastQuoteStart < 0) return AiDoubleResponseAction.Take;

            string recommendation = arrayContent.Substring(secondLastQuoteStart + 1,
                lastQuoteStart - secondLastQuoteStart - 1).ToLowerInvariant();

            // Check for drop/pass recommendations
            if (recommendation.Contains("pass") || recommendation.Contains("drop"))
            {
                return AiDoubleResponseAction.Drop;
            }

            // Default to take (conservative)
            return AiDoubleResponseAction.Take;
        }

        public void ClearCache()
        {
            // GNUBG doesn't have a persistent cache in this implementation
        }

        private static Task<string> InvokeGnubgBridgeAsync(
            string matchRef, string gameId, string variation, bool jacoby, string action)
        {
            const string BridgeTypeName = "Gnubg.Unity.Runtime.Bridge.GnubgPythonBridge";
            Type bridgeType = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length && bridgeType == null; i++)
            {
                bridgeType = assemblies[i].GetType(BridgeTypeName, throwOnError: false);
            }

            if (bridgeType == null)
            {
                Debug.LogError("[GNUBG] GnubgPythonBridge type not found");
                return Task.FromResult<string>(null);
            }

            var runAsync = bridgeType.GetMethod("RunAsync",
                BindingFlags.Public | BindingFlags.Static);

            if (runAsync == null)
            {
                Debug.LogError("[GNUBG] RunAsync method not found on GnubgPythonBridge");
                return Task.FromResult<string>(null);
            }

            object taskObj = runAsync.Invoke(null,
                new object[] { matchRef, gameId, variation, jacoby, action });

            return taskObj as Task<string>;
        }
    }
}
