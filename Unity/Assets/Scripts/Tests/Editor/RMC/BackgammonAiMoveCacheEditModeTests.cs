using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonAiMoveCacheEditModeTests
{
    private string _tempCacheRoot;

    [SetUp]
    public void SetUp()
    {
        BackgammonGameController.ClearPersistentAiMoveCache();
        _tempCacheRoot = Path.Combine(Path.GetTempPath(), $"bg-cache-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempCacheRoot);
    }

    [TearDown]
    public void TearDown()
    {
        BackgammonGameController.ClearPersistentAiMoveCache();
        if (Directory.Exists(_tempCacheRoot))
            Directory.Delete(_tempCacheRoot, recursive: true);
    }

    [Test]
    public void BuildAiMoveCacheKey_ChangesWhenSearchConfigChanges()
    {
        MethodInfo buildKey = GetStaticNonPublicMethod("BuildAiMoveCacheKey");
        GameState state = CreateState();
        MatchState match = CreateMatch();

        string baseline = (string)buildKey.Invoke(
            null,
            new object[] { state, match, 2, SearchEngine.SearchQualityPreset.Balanced, true, false });
        string differentDepth = (string)buildKey.Invoke(
            null,
            new object[] { state, match, 3, SearchEngine.SearchQualityPreset.Balanced, true, false });
        string differentQuality = (string)buildKey.Invoke(
            null,
            new object[] { state, match, 2, (SearchEngine.SearchQualityPreset)99, true, false });
        string differentPruneToggle = (string)buildKey.Invoke(
            null,
            new object[] { state, match, 2, SearchEngine.SearchQualityPreset.Balanced, false, false });

        Assert.IsFalse(string.IsNullOrWhiteSpace(baseline));
        Assert.AreNotEqual(baseline, differentDepth);
        Assert.AreNotEqual(baseline, differentQuality);
        Assert.AreNotEqual(baseline, differentPruneToggle);
    }

    [Test]
    public void PersistentAiMoveCache_RoundTripsThenClearsOnlyOnManualClear()
    {
        MethodInfo cacheMethod = GetStaticNonPublicMethod("CacheAiTurn");
        MethodInfo tryGetMethod = GetStaticNonPublicMethod("TryGetCachedAiTurn");
        MethodInfo buildKey = GetStaticNonPublicMethod("BuildAiMoveCacheKey");
        // Field moved to BackgammonAiMoveCache (internal class in runtime assembly)
        Type cacheType = GetBackgammonAiMoveCacheType();
        FieldInfo cacheField = cacheType?.GetField("MoveCache", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(cacheField, "Expected field 'MoveCache' on BackgammonAiMoveCache.");

        GameState state = CreateState();
        MatchState match = CreateMatch();
        string key = (string)buildKey.Invoke(
            null,
            new object[] { state, match, 2, SearchEngine.SearchQualityPreset.Balanced, true, false });
        Turn sourceTurn = CreateTurn(state);

        cacheMethod.Invoke(null, new object[] { key, sourceTurn });
        var cache = cacheField.GetValue(null) as Dictionary<string, Turn>;
        Assert.IsNotNull(cache);
        Assert.AreEqual(1, cache.Count, "Expected a single cached move entry.");

        object[] tryGetArgs = { key, null };
        bool cacheHit = (bool)tryGetMethod.Invoke(null, tryGetArgs);
        Assert.IsTrue(cacheHit, "Expected cache hit for previously stored key.");
        Assert.IsNotNull(tryGetArgs[1]);
        Turn cachedTurn = (Turn)tryGetArgs[1];
        Assert.AreNotSame(sourceTurn, cachedTurn, "Cached turn should be cloned to avoid mutation bleed.");
        Assert.AreEqual(sourceTurn.Moves[0].From, cachedTurn.Moves[0].From);
        Assert.AreEqual(sourceTurn.Moves[0].To, cachedTurn.Moves[0].To);

        BackgammonGameController.ClearPersistentAiMoveCache();
        Assert.AreEqual(0, cache.Count, "Explicit clear should empty persistent cache.");
    }

    [Test]
    public void DiskCache_JsonMode_RoundTripsTurn()
    {
        var entries = BuildSingleCacheEntry();
        bool saved = BackgammonAiMoveDiskCache.TrySave(
            BackgammonAiMoveCacheStorageMode.Json,
            entries,
            out string saveMessage,
            _tempCacheRoot);
        Assert.IsTrue(saved, saveMessage);

        var destination = new Dictionary<string, Turn>(StringComparer.Ordinal);
        var order = new List<string>();
        bool loaded = BackgammonAiMoveDiskCache.TryLoad(
            BackgammonAiMoveCacheStorageMode.Json,
            order,
            destination,
            out int loadedCount,
            out int discardedCount,
            out string loadMessage,
            _tempCacheRoot);

        Assert.IsTrue(loaded, loadMessage);
        Assert.AreEqual(1, loadedCount);
        Assert.AreEqual(0, discardedCount);
        Assert.AreEqual(1, destination.Count);
    }

    [Test]
    public void DiskCache_BinaryMode_RoundTripsTurn()
    {
        var entries = BuildSingleCacheEntry();
        bool saved = BackgammonAiMoveDiskCache.TrySave(
            BackgammonAiMoveCacheStorageMode.Binary,
            entries,
            out string saveMessage,
            _tempCacheRoot);
        Assert.IsTrue(saved, saveMessage);

        var destination = new Dictionary<string, Turn>(StringComparer.Ordinal);
        var order = new List<string>();
        bool loaded = BackgammonAiMoveDiskCache.TryLoad(
            BackgammonAiMoveCacheStorageMode.Binary,
            order,
            destination,
            out int loadedCount,
            out int discardedCount,
            out string loadMessage,
            _tempCacheRoot);

        Assert.IsTrue(loaded, loadMessage);
        Assert.AreEqual(1, loadedCount);
        Assert.AreEqual(0, discardedCount);
        Assert.AreEqual(1, destination.Count);
    }

    [Test]
    public void DiskCache_CorruptBinary_FailsGracefully()
    {
        string path = BackgammonAiMoveDiskCache.GetCachePath(BackgammonAiMoveCacheStorageMode.Binary, _tempCacheRoot);
        File.WriteAllText(path, "corrupt-binary-content");

        var destination = new Dictionary<string, Turn>(StringComparer.Ordinal);
        var order = new List<string>();
        bool loaded = BackgammonAiMoveDiskCache.TryLoad(
            BackgammonAiMoveCacheStorageMode.Binary,
            order,
            destination,
            out int loadedCount,
            out int discardedCount,
            out string message,
            _tempCacheRoot);

        Assert.IsFalse(loaded);
        Assert.AreEqual(0, loadedCount);
        Assert.AreEqual(0, discardedCount);
        Assert.That(message, Does.StartWith("read-failed:"));
        Assert.AreEqual(0, destination.Count);
    }

    [Test]
    public void BuildAiCubeDecisionCacheKey_SameSnapshotSameKind_Matches()
    {
        MethodInfo buildKey = GetStaticNonPublicMethod("BuildAiCubeDecisionCacheKey");
        GameState state = CreateState();
        MatchState match = CreateMatch();

        string offerKeyA = (string)buildKey.Invoke(null, new object[] { state, match, "offer" });
        string offerKeyB = (string)buildKey.Invoke(null, new object[] { state, match, "offer" });
        string responseKey = (string)buildKey.Invoke(null, new object[] { state, match, "response" });

        Assert.IsFalse(string.IsNullOrWhiteSpace(offerKeyA));
        Assert.AreEqual(offerKeyA, offerKeyB);
        Assert.AreNotEqual(offerKeyA, responseKey);
    }

    [Test]
    public void DiskCache_JsonMode_RoundTripsCubeDecisions()
    {
        var entries = BuildSingleCacheEntry();
        var offerEntries = new List<KeyValuePair<string, AiCubeDecision>>
        {
            new("offer-key", new AiCubeDecision(true, "test-offer", true))
        };
        var responseEntries = new List<KeyValuePair<string, AiDoubleResponseDecision>>
        {
            new("response-key", new AiDoubleResponseDecision(AiDoubleResponseAction.Drop, "test-response", true))
        };

        bool saved = BackgammonAiMoveDiskCache.TrySave(
            BackgammonAiMoveCacheStorageMode.Json,
            entries,
            out string saveMessage,
            offerEntries,
            responseEntries,
            _tempCacheRoot);
        Assert.IsTrue(saved, saveMessage);

        var destination = new Dictionary<string, Turn>(StringComparer.Ordinal);
        var order = new List<string>();
        var cubeOfferDestination = new Dictionary<string, AiCubeDecision>(StringComparer.Ordinal);
        var cubeOfferOrder = new List<string>();
        var cubeResponseDestination = new Dictionary<string, AiDoubleResponseDecision>(StringComparer.Ordinal);
        var cubeResponseOrder = new List<string>();
        bool loaded = BackgammonAiMoveDiskCache.TryLoad(
            BackgammonAiMoveCacheStorageMode.Json,
            order,
            destination,
            out int loadedCount,
            out int discardedCount,
            out string loadMessage,
            cubeOfferOrder,
            cubeOfferDestination,
            cubeResponseOrder,
            cubeResponseDestination,
            out int loadedCubeOfferCount,
            out int discardedCubeOfferCount,
            out int loadedCubeResponseCount,
            out int discardedCubeResponseCount,
            _tempCacheRoot);

        Assert.IsTrue(loaded, loadMessage);
        Assert.AreEqual(1, loadedCount);
        Assert.AreEqual(0, discardedCount);
        Assert.AreEqual(1, loadedCubeOfferCount);
        Assert.AreEqual(0, discardedCubeOfferCount);
        Assert.AreEqual(1, loadedCubeResponseCount);
        Assert.AreEqual(0, discardedCubeResponseCount);
        Assert.IsTrue(cubeOfferDestination["offer-key"].ShouldOffer);
        Assert.AreEqual(AiDoubleResponseAction.Drop, cubeResponseDestination["response-key"].Action);
    }

    private static MethodInfo GetStaticNonPublicMethod(string name)
    {
        // Methods live in BackgammonAiMoveCache; controller has one-line forwarding shims.
        Type cacheType = GetBackgammonAiMoveCacheType();
        MethodInfo method = cacheType?.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? typeof(BackgammonGameController).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected method '{name}' to exist on BackgammonAiMoveCache or BackgammonGameController.");
        return method;
    }

    private static Type GetBackgammonAiMoveCacheType()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType("BackgammonAiMoveCache", throwOnError: false);
            if (t != null) return t;
        }
        return null;
    }

    private static GameState CreateState()
    {
        var state = new GameState
        {
            PlayerOnRoll = 0,
            Dice1 = 6,
            Dice2 = 1,
            CubeValue = 2,
            CubeOwner = -1,
            MatchLength = 7,
            Player1Score = 1,
            Player2Score = 2
        };
        state.Player1Checkers[23] = 2;
        state.Player1Checkers[12] = 3;
        state.Player2Checkers[0] = 2;
        state.Player2Checkers[11] = 3;
        return state;
    }

    private static MatchState CreateMatch()
    {
        var match = new MatchState
        {
            Cube = 2,
            CubeOwner = -1,
            PlayerOnRoll = 0,
            MatchLength = 7,
            Player0Score = 1,
            Player1Score = 2,
            JacobyRule = true,
            BeaversAllowed = true
        };
        match.Dice[0] = 6;
        match.Dice[1] = 1;
        return match;
    }

    private static Turn CreateTurn(GameState state)
    {
        return new Turn
        {
            Moves = new List<Move>
            {
                new Move { From = 23, To = 17, IsHit = false }
            },
            ResultingState = state
        };
    }

    private static List<KeyValuePair<string, Turn>> BuildSingleCacheEntry()
    {
        GameState state = CreateState();
        Turn turn = CreateTurn(state);
        return new List<KeyValuePair<string, Turn>>
        {
            new("test-key", turn)
        };
    }
}
