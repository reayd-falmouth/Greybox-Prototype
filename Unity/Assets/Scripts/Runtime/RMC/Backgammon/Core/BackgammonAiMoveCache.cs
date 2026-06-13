using System.Collections.Generic;
using EngineCore;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

/// <summary>
/// Static in-memory + disk cache for AI move and cube decisions.
/// Owns all three dictionaries that previously lived as statics on BackgammonGameController.
/// </summary>
internal static class BackgammonAiMoveCache
{
    private const string MoveCacheVersion = "v1";
    private const int MoveCacheCapacity = 512;
    private const string CubeDecisionCacheVersion = "v1";
    private const int CubeDecisionCacheCapacity = 512;

    private static readonly Dictionary<string, Turn> MoveCache = new(System.StringComparer.Ordinal);
    private static readonly Queue<string> MoveCacheKeyOrder = new();
    private static readonly Dictionary<string, AiCubeDecision> CubeOfferCache = new(System.StringComparer.Ordinal);
    private static readonly Queue<string> CubeOfferCacheKeyOrder = new();
    private static readonly Dictionary<string, AiDoubleResponseDecision> CubeResponseCache = new(System.StringComparer.Ordinal);
    private static readonly Queue<string> CubeResponseCacheKeyOrder = new();

    private static int _moveHitCount;
    private static int _moveMissCount;
    private static int _cubeOfferHitCount;
    private static int _cubeOfferMissCount;
    private static int _cubeResponseHitCount;
    private static int _cubeResponseMissCount;
    private static bool _loadedFromDisk;
    private static bool _loadAttempted;
    private static BackgammonAiMoveCacheStorageMode _storageMode = BackgammonAiMoveCacheStorageMode.None;

    // ── Configuration ─────────────────────────────────────────────────────────

    internal static void Configure(BackgammonAiMoveCacheStorageMode mode)
    {
        _storageMode = mode;
    }

    internal static void EnsureLoadedFromDisk()
    {
        if (_loadAttempted) return;
        _loadAttempted = true;

        if (_storageMode == BackgammonAiMoveCacheStorageMode.None)
        {
            Debug.Log("[Backgammon][AI][Cache] Startup mode=memory-only loaded=0 discarded=0");
            return;
        }

        var orderedKeys = new List<string>();
        var orderedCubeOfferKeys = new List<string>();
        var orderedCubeResponseKeys = new List<string>();
        bool loadedOk = BackgammonAiMoveDiskCache.TryLoad(
            _storageMode,
            orderedKeys,
            MoveCache,
            out int loadedCount,
            out int discardedCount,
            out string message,
            orderedCubeOfferKeys,
            CubeOfferCache,
            orderedCubeResponseKeys,
            CubeResponseCache,
            out int loadedCubeOfferCount,
            out int discardedCubeOfferCount,
            out int loadedCubeResponseCount,
            out int discardedCubeResponseCount);

        MoveCacheKeyOrder.Clear();
        foreach (string k in orderedKeys) MoveCacheKeyOrder.Enqueue(k);
        CubeOfferCacheKeyOrder.Clear();
        foreach (string k in orderedCubeOfferKeys) CubeOfferCacheKeyOrder.Enqueue(k);
        CubeResponseCacheKeyOrder.Clear();
        foreach (string k in orderedCubeResponseKeys) CubeResponseCacheKeyOrder.Enqueue(k);

        _loadedFromDisk = loadedOk;
        string path = BackgammonAiMoveDiskCache.GetCachePath(_storageMode);
        string modeLabel = _storageMode == BackgammonAiMoveCacheStorageMode.Json ? "disk-json" : "disk-binary";
        if (loadedOk)
        {
            Debug.Log($"[Backgammon][AI][Cache] Startup mode={modeLabel} path={path} loaded={loadedCount} discarded={discardedCount} size={MoveCache.Count} state={message}");
            Debug.Log($"[Backgammon][AI][Cube][Cache] Startup mode={modeLabel} path={path} offerLoaded={loadedCubeOfferCount} offerDiscarded={discardedCubeOfferCount} offerSize={CubeOfferCache.Count} responseLoaded={loadedCubeResponseCount} responseDiscarded={discardedCubeResponseCount} responseSize={CubeResponseCache.Count} state={message}");
        }
        else
        {
            Debug.LogWarning($"[Backgammon][AI][Cache] Startup load failed mode={modeLabel} path={path} loaded={loadedCount} discarded={discardedCount} state={message}; using memory cache.");
            Debug.LogWarning($"[Backgammon][AI][Cube][Cache] Startup load failed mode={modeLabel} path={path} offerLoaded={loadedCubeOfferCount} offerDiscarded={discardedCubeOfferCount} responseLoaded={loadedCubeResponseCount} responseDiscarded={discardedCubeResponseCount} state={message}; using memory cache.");
        }
    }

    // ── Move cache ────────────────────────────────────────────────────────────

    internal static bool TryGetTurn(string key, out Turn turn)
    {
        turn = null;
        if (string.IsNullOrWhiteSpace(key)) { _moveMissCount++; return false; }
        if (!MoveCache.TryGetValue(key, out Turn stored)) { _moveMissCount++; return false; }
        turn = CloneTurn(stored);
        _moveHitCount++;
        return turn != null;
    }

    internal static void StoreTurn(string key, Turn turn)
    {
        if (string.IsNullOrWhiteSpace(key) || turn?.Moves == null || turn.ResultingState == null) return;
        MoveCache[key] = CloneTurn(turn);
        MoveCacheKeyOrder.Enqueue(key);
        while (MoveCache.Count > MoveCacheCapacity && MoveCacheKeyOrder.Count > 0)
        {
            string evicted = MoveCacheKeyOrder.Dequeue();
            if (MoveCache.ContainsKey(evicted) && !string.Equals(evicted, key, System.StringComparison.Ordinal))
                MoveCache.Remove(evicted);
        }

        PersistToDisk("store");
    }

    // ── Cube offer cache ──────────────────────────────────────────────────────

    internal static bool TryGetCubeOffer(string key, out AiCubeDecision decision)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(key)) { _cubeOfferMissCount++; return false; }
        if (!CubeOfferCache.TryGetValue(key, out decision)) { _cubeOfferMissCount++; return false; }
        _cubeOfferHitCount++;
        return true;
    }

    internal static void StoreCubeOffer(string key, AiCubeDecision decision)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        CubeOfferCache[key] = decision;
        CubeOfferCacheKeyOrder.Enqueue(key);
        while (CubeOfferCache.Count > CubeDecisionCacheCapacity && CubeOfferCacheKeyOrder.Count > 0)
        {
            string evicted = CubeOfferCacheKeyOrder.Dequeue();
            if (CubeOfferCache.ContainsKey(evicted) && !string.Equals(evicted, key, System.StringComparison.Ordinal))
                CubeOfferCache.Remove(evicted);
        }

        PersistToDisk("cube-offer-store");
    }

    // ── Cube response cache ───────────────────────────────────────────────────

    internal static bool TryGetCubeResponse(string key, out AiDoubleResponseDecision decision)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(key)) { _cubeResponseMissCount++; return false; }
        if (!CubeResponseCache.TryGetValue(key, out decision)) { _cubeResponseMissCount++; return false; }
        _cubeResponseHitCount++;
        return true;
    }

    internal static void StoreCubeResponse(string key, AiDoubleResponseDecision decision)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        CubeResponseCache[key] = decision;
        CubeResponseCacheKeyOrder.Enqueue(key);
        while (CubeResponseCache.Count > CubeDecisionCacheCapacity && CubeResponseCacheKeyOrder.Count > 0)
        {
            string evicted = CubeResponseCacheKeyOrder.Dequeue();
            if (CubeResponseCache.ContainsKey(evicted) && !string.Equals(evicted, key, System.StringComparison.Ordinal))
                CubeResponseCache.Remove(evicted);
        }

        PersistToDisk("cube-response-store");
    }

    // ── Key builders ──────────────────────────────────────────────────────────

    internal static string BuildMoveKey(
        GameState state,
        MatchState match,
        int depth,
        SearchEngine.SearchQualityPreset qualityPreset,
        bool stagedPruneFilteringEnabled,
        bool forceFullTurnEvaluation)
    {
        if (state == null || match == null) return string.Empty;
        string snapshotId = BuildGnubgSnapshotId(state, match);
        if (string.IsNullOrWhiteSpace(snapshotId)) return string.Empty;
        return $"{MoveCacheVersion}:{snapshotId}:d={depth}:q={(int)qualityPreset}:prune={stagedPruneFilteringEnabled}:full={forceFullTurnEvaluation}";
    }

    internal static string BuildCubeDecisionKey(GameState state, MatchState match, string decisionKind)
    {
        if (state == null || match == null || string.IsNullOrWhiteSpace(decisionKind)) return string.Empty;
        string snapshotId = BuildGnubgSnapshotId(state, match);
        if (string.IsNullOrWhiteSpace(snapshotId)) return string.Empty;
        return $"{CubeDecisionCacheVersion}:{decisionKind}:{snapshotId}";
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    internal static void PersistToDisk(string context)
    {
        if (_storageMode == BackgammonAiMoveCacheStorageMode.None) return;
        bool saved = BackgammonAiMoveDiskCache.TrySave(
            _storageMode,
            BuildOrderedMoveEntries(),
            out string message,
            BuildOrderedCubeOfferEntries(),
            BuildOrderedCubeResponseEntries());
        if (!saved)
        {
            string path = BackgammonAiMoveDiskCache.GetCachePath(_storageMode);
            Debug.LogWarning($"[Backgammon][AI][Cache] Persist failed context={context} path={path} state={message}");
        }
    }

    internal static void Clear()
    {
        MoveCache.Clear();
        MoveCacheKeyOrder.Clear();
        CubeOfferCache.Clear();
        CubeOfferCacheKeyOrder.Clear();
        CubeResponseCache.Clear();
        CubeResponseCacheKeyOrder.Clear();
        _moveHitCount = 0;
        _moveMissCount = 0;
        _cubeOfferHitCount = 0;
        _cubeOfferMissCount = 0;
        _cubeResponseHitCount = 0;
        _cubeResponseMissCount = 0;
        BackgammonAiMoveDiskCache.ClearFiles();
        Debug.Log("[Backgammon][AI][Cache] Cleared persistent move + cube caches via explicit call.");
    }

    // ── Logging helpers ───────────────────────────────────────────────────────

    internal static void LogMoveDecision(string phase, string cacheKey, bool hit, bool debugEnabled)
    {
        if (!debugEnabled) return;
        Debug.Log($"[Backgammon][AI][Cache] phase={phase} hit={hit} key={cacheKey} hits={_moveHitCount} misses={_moveMissCount} size={MoveCache.Count}");
    }

    internal static void LogCubeDecision(string phase, string cacheKey, bool hit, string decisionSummary)
    {
        string compactKey = string.IsNullOrWhiteSpace(cacheKey) ? "<none>" : cacheKey.GetHashCode().ToString("X8");
        Debug.Log(
            $"[Backgammon][AI][Cube][Cache] phase={phase} hit={hit} keyHash={compactKey} decision={decisionSummary} " +
            $"offerHits={_cubeOfferHitCount} offerMisses={_cubeOfferMissCount} offerSize={CubeOfferCache.Count} " +
            $"responseHits={_cubeResponseHitCount} responseMisses={_cubeResponseMissCount} responseSize={CubeResponseCache.Count}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildGnubgSnapshotId(GameState state, MatchState match)
    {
        if (state == null || match == null) return string.Empty;
        var oracleMatch = new MatchState
        {
            Cube = match.Cube,
            CubeOwner = match.CubeOwner,
            PlayerOnRoll = state.PlayerOnRoll,
            IsCrawford = match.IsCrawford,
            GameState = 0,
            Turn = 0,
            Doubled = false,
            Resigned = 0,
            MatchLength = match.MatchLength,
            Player0Score = match.Player0Score,
            Player1Score = match.Player1Score,
            JacobyRule = match.JacobyRule,
            BeaversAllowed = match.BeaversAllowed
        };
        oracleMatch.Dice[0] = state.Dice1;
        oracleMatch.Dice[1] = state.Dice2;
        return $"{PositionId.Encode(state)}:{MatchId.Encode(oracleMatch)}";
    }

    private static Turn CloneTurn(Turn turn)
    {
        if (turn?.Moves == null || turn.ResultingState == null) return null;
        var moves = new System.Collections.Generic.List<Move>(turn.Moves.Count);
        foreach (Move m in turn.Moves)
            moves.Add(new Move { From = m.From, To = m.To, IsHit = m.IsHit });
        return new Turn { Moves = moves, ResultingState = CloneGameState(turn.ResultingState) };
    }

    private static GameState CloneGameState(GameState s)
    {
        if (s == null) return null;
        return new GameState
        {
            Player1Checkers = (int[])s.Player1Checkers.Clone(),
            Player2Checkers = (int[])s.Player2Checkers.Clone(),
            CubeValue = s.CubeValue,
            CubeOwner = s.CubeOwner,
            PlayerOnRoll = s.PlayerOnRoll,
            PlayerToDecide = s.PlayerToDecide,
            Dice1 = s.Dice1,
            Dice2 = s.Dice2,
            MatchLength = s.MatchLength,
            Player1Score = s.Player1Score,
            Player2Score = s.Player2Score
        };
    }

    private static List<KeyValuePair<string, Turn>> BuildOrderedMoveEntries()
    {
        var ordered = new List<KeyValuePair<string, Turn>>(MoveCache.Count);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (string k in MoveCacheKeyOrder)
        {
            if (string.IsNullOrWhiteSpace(k) || seen.Contains(k)) continue;
            if (!MoveCache.TryGetValue(k, out Turn t) || t == null) continue;
            seen.Add(k);
            ordered.Add(new KeyValuePair<string, Turn>(k, CloneTurn(t)));
        }
        foreach (var kvp in MoveCache)
        {
            if (seen.Contains(kvp.Key) || kvp.Value == null) continue;
            seen.Add(kvp.Key);
            ordered.Add(new KeyValuePair<string, Turn>(kvp.Key, CloneTurn(kvp.Value)));
        }
        return ordered;
    }

    private static List<KeyValuePair<string, AiCubeDecision>> BuildOrderedCubeOfferEntries()
    {
        var ordered = new List<KeyValuePair<string, AiCubeDecision>>(CubeOfferCache.Count);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (string k in CubeOfferCacheKeyOrder)
        {
            if (string.IsNullOrWhiteSpace(k) || seen.Contains(k)) continue;
            if (!CubeOfferCache.TryGetValue(k, out AiCubeDecision d)) continue;
            seen.Add(k);
            ordered.Add(new KeyValuePair<string, AiCubeDecision>(k, d));
        }
        foreach (var kvp in CubeOfferCache)
        {
            if (seen.Contains(kvp.Key)) continue;
            seen.Add(kvp.Key);
            ordered.Add(new KeyValuePair<string, AiCubeDecision>(kvp.Key, kvp.Value));
        }
        return ordered;
    }

    private static List<KeyValuePair<string, AiDoubleResponseDecision>> BuildOrderedCubeResponseEntries()
    {
        var ordered = new List<KeyValuePair<string, AiDoubleResponseDecision>>(CubeResponseCache.Count);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (string k in CubeResponseCacheKeyOrder)
        {
            if (string.IsNullOrWhiteSpace(k) || seen.Contains(k)) continue;
            if (!CubeResponseCache.TryGetValue(k, out AiDoubleResponseDecision d)) continue;
            seen.Add(k);
            ordered.Add(new KeyValuePair<string, AiDoubleResponseDecision>(k, d));
        }
        foreach (var kvp in CubeResponseCache)
        {
            if (seen.Contains(kvp.Key)) continue;
            seen.Add(kvp.Key);
            ordered.Add(new KeyValuePair<string, AiDoubleResponseDecision>(kvp.Key, kvp.Value));
        }
        return ordered;
    }
}
