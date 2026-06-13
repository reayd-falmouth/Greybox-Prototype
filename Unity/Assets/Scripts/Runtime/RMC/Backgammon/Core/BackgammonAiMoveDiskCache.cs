using System;
using System.Collections.Generic;
using System.IO;
using EngineCore;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    public enum BackgammonAiMoveCacheStorageMode
    {
        None = 0,
        Json = 1,
        Binary = 2
    }

    public static class BackgammonAiMoveDiskCache
    {
        private const int SchemaVersion = 1;
        private const string JsonFileName = "backgammon_ai_move_cache.json";
        private const string BinaryFileName = "backgammon_ai_move_cache.bin";

        [Serializable]
        private sealed class CacheEnvelope
        {
            public int version = SchemaVersion;
            public List<CacheEntry> entries = new();
            public List<CubeOfferDecisionEntry> cubeOfferEntries = new();
            public List<CubeResponseDecisionEntry> cubeResponseEntries = new();
        }

        [Serializable]
        private sealed class CacheEntry
        {
            public string key;
            public TurnDto turn;
        }

        [Serializable]
        private sealed class CubeOfferDecisionEntry
        {
            public string key;
            public CubeOfferDecisionDto decision;
        }

        [Serializable]
        private sealed class CubeResponseDecisionEntry
        {
            public string key;
            public CubeResponseDecisionDto decision;
        }

        [Serializable]
        private sealed class TurnDto
        {
            public List<MoveDto> moves = new();
            public List<int> diceUsed = new();
            public GameStateDto resultingState;
        }

        [Serializable]
        private sealed class MoveDto
        {
            public int from;
            public int to;
            public bool isHit;
        }

        [Serializable]
        private sealed class GameStateDto
        {
            public int[] player1Checkers;
            public int[] player2Checkers;
            public int cubeValue;
            public int cubeOwner;
            public int playerOnRoll;
            public int playerToDecide;
            public int dice1;
            public int dice2;
            public int matchLength;
            public int player1Score;
            public int player2Score;
        }

        [Serializable]
        private sealed class CubeOfferDecisionDto
        {
            public bool shouldOffer;
            public string reason;
            public bool fromAiEvaluator;
        }

        [Serializable]
        private sealed class CubeResponseDecisionDto
        {
            public int action;
            public string reason;
            public bool fromAiEvaluator;
        }

        public static string GetCachePath(BackgammonAiMoveCacheStorageMode mode, string rootOverride = null)
        {
            string root = string.IsNullOrWhiteSpace(rootOverride) ? Application.persistentDataPath : rootOverride;
            string file = mode == BackgammonAiMoveCacheStorageMode.Json ? JsonFileName : BinaryFileName;
            return Path.Combine(root, file);
        }

        public static bool TryLoad(
            BackgammonAiMoveCacheStorageMode mode,
            IReadOnlyList<string> orderedKeys,
            IDictionary<string, Turn> destination,
            out int loadedCount,
            out int discardedCount,
            out string message,
            IReadOnlyList<string> orderedCubeOfferKeys,
            IDictionary<string, AiCubeDecision> cubeOfferDestination,
            IReadOnlyList<string> orderedCubeResponseKeys,
            IDictionary<string, AiDoubleResponseDecision> cubeResponseDestination,
            out int loadedCubeOfferCount,
            out int discardedCubeOfferCount,
            out int loadedCubeResponseCount,
            out int discardedCubeResponseCount,
            string rootOverride = null)
        {
            loadedCount = 0;
            discardedCount = 0;
            loadedCubeOfferCount = 0;
            discardedCubeOfferCount = 0;
            loadedCubeResponseCount = 0;
            discardedCubeResponseCount = 0;
            if (destination == null)
            {
                message = "destination-null";
                return false;
            }

            if (mode == BackgammonAiMoveCacheStorageMode.None)
            {
                message = "disabled";
                return true;
            }

            string path = GetCachePath(mode, rootOverride);
            if (!File.Exists(path))
            {
                message = "no-file";
                return true;
            }

            try
            {
                CacheEnvelope envelope = mode == BackgammonAiMoveCacheStorageMode.Json
                    ? ReadJson(path)
                    : ReadBinary(path);
                if (envelope == null)
                {
                    message = "empty-envelope";
                    return true;
                }

                if (envelope.version != SchemaVersion)
                {
                    message = $"version-mismatch:{envelope.version}";
                    return true;
                }

                destination.Clear();
                if (orderedKeys is List<string> mutableKeys)
                    mutableKeys.Clear();

                for (int i = 0; i < envelope.entries.Count; i++)
                {
                    CacheEntry entry = envelope.entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    {
                        discardedCount++;
                        continue;
                    }

                    Turn turn = ToTurn(entry.turn);
                    if (turn == null)
                    {
                        discardedCount++;
                        continue;
                    }

                    destination[entry.key] = turn;
                    if (orderedKeys is List<string> list)
                        list.Add(entry.key);
                    loadedCount++;
                }

                if (cubeOfferDestination != null)
                {
                    cubeOfferDestination.Clear();
                    if (orderedCubeOfferKeys is List<string> mutableCubeOfferKeys)
                        mutableCubeOfferKeys.Clear();

                    List<CubeOfferDecisionEntry> cubeOfferEntries = envelope.cubeOfferEntries ?? new List<CubeOfferDecisionEntry>();
                    for (int i = 0; i < cubeOfferEntries.Count; i++)
                    {
                        CubeOfferDecisionEntry entry = cubeOfferEntries[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                        {
                            discardedCubeOfferCount++;
                            continue;
                        }

                        if (!TryToCubeOfferDecision(entry.decision, out AiCubeDecision decision))
                        {
                            discardedCubeOfferCount++;
                            continue;
                        }

                        cubeOfferDestination[entry.key] = decision;
                        if (orderedCubeOfferKeys is List<string> cubeOfferOrder)
                            cubeOfferOrder.Add(entry.key);
                        loadedCubeOfferCount++;
                    }
                }

                if (cubeResponseDestination != null)
                {
                    cubeResponseDestination.Clear();
                    if (orderedCubeResponseKeys is List<string> mutableCubeResponseKeys)
                        mutableCubeResponseKeys.Clear();

                    List<CubeResponseDecisionEntry> cubeResponseEntries = envelope.cubeResponseEntries ?? new List<CubeResponseDecisionEntry>();
                    for (int i = 0; i < cubeResponseEntries.Count; i++)
                    {
                        CubeResponseDecisionEntry entry = cubeResponseEntries[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                        {
                            discardedCubeResponseCount++;
                            continue;
                        }

                        if (!TryToCubeResponseDecision(entry.decision, out AiDoubleResponseDecision decision))
                        {
                            discardedCubeResponseCount++;
                            continue;
                        }

                        cubeResponseDestination[entry.key] = decision;
                        if (orderedCubeResponseKeys is List<string> cubeResponseOrder)
                            cubeResponseOrder.Add(entry.key);
                        loadedCubeResponseCount++;
                    }
                }

                message = "ok";
                return true;
            }
            catch (Exception ex)
            {
                // #region agent log
                try
                {
                    File.AppendAllText(
                        "debug-a6d9e6.log",
                        $"{{\"sessionId\":\"a6d9e6\",\"runId\":\"run1\",\"hypothesisId\":\"H5\",\"location\":\"BackgammonAiMoveDiskCache.TryLoad:catch\",\"message\":\"disk cache load exception\",\"data\":{{\"mode\":\"{mode}\",\"error\":\"{ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}{Environment.NewLine}");
                }
                catch
                {
                    // Ignore debug logging failures.
                }
                // #endregion
                message = $"read-failed:{ex.Message}";
                return false;
            }
        }

        public static bool TryLoad(
            BackgammonAiMoveCacheStorageMode mode,
            IReadOnlyList<string> orderedKeys,
            IDictionary<string, Turn> destination,
            out int loadedCount,
            out int discardedCount,
            out string message,
            string rootOverride = null)
        {
            return TryLoad(
                mode,
                orderedKeys,
                destination,
                out loadedCount,
                out discardedCount,
                out message,
                orderedCubeOfferKeys: null,
                cubeOfferDestination: null,
                orderedCubeResponseKeys: null,
                cubeResponseDestination: null,
                out _,
                out _,
                out _,
                out _,
                rootOverride);
        }

        public static bool TrySave(
            BackgammonAiMoveCacheStorageMode mode,
            IReadOnlyList<KeyValuePair<string, Turn>> orderedEntries,
            out string message,
            IReadOnlyList<KeyValuePair<string, AiCubeDecision>> orderedCubeOfferEntries = null,
            IReadOnlyList<KeyValuePair<string, AiDoubleResponseDecision>> orderedCubeResponseEntries = null,
            string rootOverride = null)
        {
            if (mode == BackgammonAiMoveCacheStorageMode.None)
            {
                message = "disabled";
                return true;
            }

            try
            {
                string path = GetCachePath(mode, rootOverride);
                string root = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(root))
                    Directory.CreateDirectory(root);

                var envelope = new CacheEnvelope();
                if (orderedEntries != null)
                {
                    for (int i = 0; i < orderedEntries.Count; i++)
                    {
                        KeyValuePair<string, Turn> kvp = orderedEntries[i];
                        if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null)
                            continue;
                        TurnDto dto = ToDto(kvp.Value);
                        if (dto?.resultingState == null || dto.moves == null || dto.moves.Count == 0)
                            continue;
                        envelope.entries.Add(new CacheEntry
                        {
                            key = kvp.Key,
                            turn = dto
                        });
                    }
                }

                if (orderedCubeOfferEntries != null)
                {
                    for (int i = 0; i < orderedCubeOfferEntries.Count; i++)
                    {
                        KeyValuePair<string, AiCubeDecision> kvp = orderedCubeOfferEntries[i];
                        if (string.IsNullOrWhiteSpace(kvp.Key))
                            continue;
                        CubeOfferDecisionDto dto = ToDto(kvp.Value);
                        if (dto == null)
                            continue;
                        envelope.cubeOfferEntries.Add(new CubeOfferDecisionEntry
                        {
                            key = kvp.Key,
                            decision = dto
                        });
                    }
                }

                if (orderedCubeResponseEntries != null)
                {
                    for (int i = 0; i < orderedCubeResponseEntries.Count; i++)
                    {
                        KeyValuePair<string, AiDoubleResponseDecision> kvp = orderedCubeResponseEntries[i];
                        if (string.IsNullOrWhiteSpace(kvp.Key))
                            continue;
                        CubeResponseDecisionDto dto = ToDto(kvp.Value);
                        if (dto == null)
                            continue;
                        envelope.cubeResponseEntries.Add(new CubeResponseDecisionEntry
                        {
                            key = kvp.Key,
                            decision = dto
                        });
                    }
                }

                if (mode == BackgammonAiMoveCacheStorageMode.Json)
                    WriteJson(path, envelope);
                else
                    WriteBinary(path, envelope);

                message = $"ok:moves={envelope.entries.Count},offer={envelope.cubeOfferEntries.Count},response={envelope.cubeResponseEntries.Count}";
                return true;
            }
            catch (Exception ex)
            {
                // #region agent log
                try
                {
                    File.AppendAllText(
                        "debug-a6d9e6.log",
                        $"{{\"sessionId\":\"a6d9e6\",\"runId\":\"run1\",\"hypothesisId\":\"H5\",\"location\":\"BackgammonAiMoveDiskCache.TrySave:catch\",\"message\":\"disk cache save exception\",\"data\":{{\"mode\":\"{mode}\",\"error\":\"{ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}{Environment.NewLine}");
                }
                catch
                {
                    // Ignore debug logging failures.
                }
                // #endregion
                message = $"write-failed:{ex.Message}";
                return false;
            }
        }

        public static bool TrySave(
            BackgammonAiMoveCacheStorageMode mode,
            IReadOnlyList<KeyValuePair<string, Turn>> orderedEntries,
            out string message,
            string rootOverride = null)
        {
            return TrySave(
                mode,
                orderedEntries,
                out message,
                orderedCubeOfferEntries: null,
                orderedCubeResponseEntries: null,
                rootOverride);
        }

        public static void ClearFiles(string rootOverride = null)
        {
            TryDelete(GetCachePath(BackgammonAiMoveCacheStorageMode.Json, rootOverride));
            TryDelete(GetCachePath(BackgammonAiMoveCacheStorageMode.Binary, rootOverride));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup; caller logs context.
            }
        }

        private static CacheEnvelope ReadJson(string path)
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonUtility.FromJson<CacheEnvelope>(json);
        }

        private static void WriteJson(string path, CacheEnvelope envelope)
        {
            string json = JsonUtility.ToJson(envelope, prettyPrint: true);
            File.WriteAllText(path, json);
        }

        private static CacheEnvelope ReadBinary(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);
            int version = reader.ReadInt32();
            int count = reader.ReadInt32();
            var envelope = new CacheEnvelope { version = version };
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                TurnDto turn = ReadTurn(reader);
                envelope.entries.Add(new CacheEntry { key = key, turn = turn });
            }

            int cubeOfferCount = reader.ReadInt32();
            for (int i = 0; i < cubeOfferCount; i++)
            {
                string key = reader.ReadString();
                CubeOfferDecisionDto decision = ReadCubeOfferDecision(reader);
                envelope.cubeOfferEntries.Add(new CubeOfferDecisionEntry { key = key, decision = decision });
            }

            int cubeResponseCount = reader.ReadInt32();
            for (int i = 0; i < cubeResponseCount; i++)
            {
                string key = reader.ReadString();
                CubeResponseDecisionDto decision = ReadCubeResponseDecision(reader);
                envelope.cubeResponseEntries.Add(new CubeResponseDecisionEntry { key = key, decision = decision });
            }
            return envelope;
        }

        private static void WriteBinary(string path, CacheEnvelope envelope)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(fs);
            writer.Write(envelope.version);
            writer.Write(envelope.entries.Count);
            for (int i = 0; i < envelope.entries.Count; i++)
            {
                CacheEntry entry = envelope.entries[i];
                writer.Write(entry.key ?? string.Empty);
                WriteTurn(writer, entry.turn);
            }

            writer.Write(envelope.cubeOfferEntries.Count);
            for (int i = 0; i < envelope.cubeOfferEntries.Count; i++)
            {
                CubeOfferDecisionEntry entry = envelope.cubeOfferEntries[i];
                writer.Write(entry.key ?? string.Empty);
                WriteCubeOfferDecision(writer, entry.decision);
            }

            writer.Write(envelope.cubeResponseEntries.Count);
            for (int i = 0; i < envelope.cubeResponseEntries.Count; i++)
            {
                CubeResponseDecisionEntry entry = envelope.cubeResponseEntries[i];
                writer.Write(entry.key ?? string.Empty);
                WriteCubeResponseDecision(writer, entry.decision);
            }
        }

        private static TurnDto ReadTurn(BinaryReader reader)
        {
            int moveCount = reader.ReadInt32();
            var moves = new List<MoveDto>(Mathf.Max(0, moveCount));
            for (int i = 0; i < moveCount; i++)
            {
                moves.Add(new MoveDto
                {
                    from = reader.ReadInt32(),
                    to = reader.ReadInt32(),
                    isHit = reader.ReadBoolean()
                });
            }

            int diceCount = reader.ReadInt32();
            var dice = new List<int>(Mathf.Max(0, diceCount));
            for (int i = 0; i < diceCount; i++)
                dice.Add(reader.ReadInt32());

            var state = new GameStateDto
            {
                player1Checkers = ReadIntArray(reader),
                player2Checkers = ReadIntArray(reader),
                cubeValue = reader.ReadInt32(),
                cubeOwner = reader.ReadInt32(),
                playerOnRoll = reader.ReadInt32(),
                playerToDecide = reader.ReadInt32(),
                dice1 = reader.ReadInt32(),
                dice2 = reader.ReadInt32(),
                matchLength = reader.ReadInt32(),
                player1Score = reader.ReadInt32(),
                player2Score = reader.ReadInt32()
            };
            return new TurnDto { moves = moves, diceUsed = dice, resultingState = state };
        }

        private static void WriteTurn(BinaryWriter writer, TurnDto turn)
        {
            List<MoveDto> moves = turn?.moves ?? new List<MoveDto>();
            writer.Write(moves.Count);
            for (int i = 0; i < moves.Count; i++)
            {
                MoveDto move = moves[i];
                writer.Write(move?.from ?? 0);
                writer.Write(move?.to ?? 0);
                writer.Write(move?.isHit ?? false);
            }

            List<int> dice = turn?.diceUsed ?? new List<int>();
            writer.Write(dice.Count);
            for (int i = 0; i < dice.Count; i++)
                writer.Write(dice[i]);

            GameStateDto state = turn?.resultingState ?? new GameStateDto();
            WriteIntArray(writer, state.player1Checkers);
            WriteIntArray(writer, state.player2Checkers);
            writer.Write(state.cubeValue);
            writer.Write(state.cubeOwner);
            writer.Write(state.playerOnRoll);
            writer.Write(state.playerToDecide);
            writer.Write(state.dice1);
            writer.Write(state.dice2);
            writer.Write(state.matchLength);
            writer.Write(state.player1Score);
            writer.Write(state.player2Score);
        }

        private static int[] ReadIntArray(BinaryReader reader)
        {
            int len = reader.ReadInt32();
            if (len <= 0) return Array.Empty<int>();
            var arr = new int[len];
            for (int i = 0; i < len; i++)
                arr[i] = reader.ReadInt32();
            return arr;
        }

        private static void WriteIntArray(BinaryWriter writer, int[] arr)
        {
            int len = arr?.Length ?? 0;
            writer.Write(len);
            for (int i = 0; i < len; i++)
                writer.Write(arr[i]);
        }

        private static CubeOfferDecisionDto ReadCubeOfferDecision(BinaryReader reader)
        {
            return new CubeOfferDecisionDto
            {
                shouldOffer = reader.ReadBoolean(),
                reason = reader.ReadString(),
                fromAiEvaluator = reader.ReadBoolean()
            };
        }

        private static void WriteCubeOfferDecision(BinaryWriter writer, CubeOfferDecisionDto decision)
        {
            CubeOfferDecisionDto dto = decision ?? new CubeOfferDecisionDto();
            writer.Write(dto.shouldOffer);
            writer.Write(dto.reason ?? string.Empty);
            writer.Write(dto.fromAiEvaluator);
        }

        private static CubeResponseDecisionDto ReadCubeResponseDecision(BinaryReader reader)
        {
            return new CubeResponseDecisionDto
            {
                action = reader.ReadInt32(),
                reason = reader.ReadString(),
                fromAiEvaluator = reader.ReadBoolean()
            };
        }

        private static void WriteCubeResponseDecision(BinaryWriter writer, CubeResponseDecisionDto decision)
        {
            CubeResponseDecisionDto dto = decision ?? new CubeResponseDecisionDto();
            writer.Write(dto.action);
            writer.Write(dto.reason ?? string.Empty);
            writer.Write(dto.fromAiEvaluator);
        }

        private static TurnDto ToDto(Turn turn)
        {
            if (turn == null || turn.Moves == null || turn.ResultingState == null)
                return null;

            var dto = new TurnDto
            {
                resultingState = ToDto(turn.ResultingState)
            };
            for (int i = 0; i < turn.Moves.Count; i++)
            {
                Move move = turn.Moves[i];
                dto.moves.Add(new MoveDto { from = move.From, to = move.To, isHit = move.IsHit });
            }

            if (turn.DiceUsed != null)
            {
                for (int i = 0; i < turn.DiceUsed.Count; i++)
                    dto.diceUsed.Add(turn.DiceUsed[i]);
            }

            return dto;
        }

        private static GameStateDto ToDto(GameState state)
        {
            if (state == null) return null;
            return new GameStateDto
            {
                player1Checkers = state.Player1Checkers != null ? (int[])state.Player1Checkers.Clone() : Array.Empty<int>(),
                player2Checkers = state.Player2Checkers != null ? (int[])state.Player2Checkers.Clone() : Array.Empty<int>(),
                cubeValue = state.CubeValue,
                cubeOwner = state.CubeOwner,
                playerOnRoll = state.PlayerOnRoll,
                playerToDecide = state.PlayerToDecide,
                dice1 = state.Dice1,
                dice2 = state.Dice2,
                matchLength = state.MatchLength,
                player1Score = state.Player1Score,
                player2Score = state.Player2Score
            };
        }

        private static Turn ToTurn(TurnDto dto)
        {
            if (dto == null || dto.resultingState == null || dto.moves == null || dto.moves.Count == 0)
                return null;

            var turn = new Turn
            {
                Moves = new List<Move>(dto.moves.Count),
                DiceUsed = new List<int>(dto.diceUsed?.Count ?? 0),
                ResultingState = ToGameState(dto.resultingState)
            };

            for (int i = 0; i < dto.moves.Count; i++)
            {
                MoveDto move = dto.moves[i];
                if (move == null) continue;
                turn.Moves.Add(new Move
                {
                    From = move.from,
                    To = move.to,
                    IsHit = move.isHit
                });
            }

            if (dto.diceUsed != null)
            {
                for (int i = 0; i < dto.diceUsed.Count; i++)
                    turn.DiceUsed.Add(dto.diceUsed[i]);
            }

            return turn;
        }

        private static GameState ToGameState(GameStateDto dto)
        {
            return new GameState
            {
                Player1Checkers = dto.player1Checkers != null ? (int[])dto.player1Checkers.Clone() : new int[25],
                Player2Checkers = dto.player2Checkers != null ? (int[])dto.player2Checkers.Clone() : new int[25],
                CubeValue = dto.cubeValue,
                CubeOwner = dto.cubeOwner,
                PlayerOnRoll = dto.playerOnRoll,
                PlayerToDecide = dto.playerToDecide,
                Dice1 = dto.dice1,
                Dice2 = dto.dice2,
                MatchLength = dto.matchLength,
                Player1Score = dto.player1Score,
                Player2Score = dto.player2Score
            };
        }

        private static CubeOfferDecisionDto ToDto(AiCubeDecision decision)
        {
            return new CubeOfferDecisionDto
            {
                shouldOffer = decision.ShouldOffer,
                reason = decision.Reason ?? string.Empty,
                fromAiEvaluator = decision.FromAiEvaluator
            };
        }

        private static CubeResponseDecisionDto ToDto(AiDoubleResponseDecision decision)
        {
            return new CubeResponseDecisionDto
            {
                action = (int)decision.Action,
                reason = decision.Reason ?? string.Empty,
                fromAiEvaluator = decision.FromAiEvaluator
            };
        }

        private static bool TryToCubeOfferDecision(CubeOfferDecisionDto dto, out AiCubeDecision decision)
        {
            if (dto == null)
            {
                decision = default;
                return false;
            }

            decision = new AiCubeDecision(dto.shouldOffer, dto.reason, dto.fromAiEvaluator);
            return true;
        }

        private static bool TryToCubeResponseDecision(CubeResponseDecisionDto dto, out AiDoubleResponseDecision decision)
        {
            if (dto == null)
            {
                decision = default;
                return false;
            }

            if (!Enum.IsDefined(typeof(AiDoubleResponseAction), dto.action))
            {
                decision = default;
                return false;
            }

            decision = new AiDoubleResponseDecision((AiDoubleResponseAction)dto.action, dto.reason, dto.fromAiEvaluator);
            return true;
        }
    }
}
