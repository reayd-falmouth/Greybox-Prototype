using System.Collections.Generic;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

/// <summary>
/// Ensures logical counts per board slot match <see cref="BoardManager.SpawnInitialCheckers"/> / Full Setup
/// when using EngineCore's P1 vs mirrored P2 indexing (same as <see cref="BoardManager.SyncCheckersFromGameState"/>).
/// </summary>
public class BackgammonSyncMirrorEditModeTests
{
    /// <summary>Keys = board slot 0..23; positive = white count, negative = black (see BoardManager.SpawnInitialCheckers).</summary>
    private static readonly Dictionary<int, int> FullSetupBoardCounts = new()
    {
        { 0, 2 }, { 11, 5 }, { 16, 3 }, { 18, 5 },
        { 5, -5 }, { 7, -3 }, { 12, -5 }, { 23, -2 }
    };

    private static void ExpectedWhiteBlack(int boardIdx, out int white, out int black)
    {
        white = 0;
        black = 0;
        if (!FullSetupBoardCounts.TryGetValue(boardIdx, out int v)) return;
        if (v > 0) white = v;
        else black = -v;
    }

    private static void CountsFromMirroredState(GameState state, int[] whitePerBoard, int[] blackPerBoard)
    {
        for (int b = 0; b < 24; b++)
        {
            whitePerBoard[b] = 0;
            blackPerBoard[b] = 0;
        }

        for (int enginePoint = 0; enginePoint < 24; enginePoint++)
        {
            int boardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(enginePoint);
            int p1 = state.Player1Checkers[enginePoint];
            int p2 = state.Player2Checkers[23 - enginePoint];
            whitePerBoard[boardIdx] += p1;
            blackPerBoard[boardIdx] += p2;
        }
    }

    [Test]
    public void StartingPosition_MirroredP2_MatchesFullSetupSlots()
    {
        GameState st = PositionId.Decode("4HPwATDgc/ABMA");
        var white = new int[24];
        var black = new int[24];
        CountsFromMirroredState(st, white, black);

        for (int boardIdx = 0; boardIdx < 24; boardIdx++)
        {
            ExpectedWhiteBlack(boardIdx, out int ew, out int eb);
            Assert.AreEqual(ew, white[boardIdx], $"white board slot {boardIdx}");
            Assert.AreEqual(eb, black[boardIdx], $"black board slot {boardIdx}");
            Assert.IsFalse(white[boardIdx] > 0 && black[boardIdx] > 0, $"slot {boardIdx} must not have both colors");
        }
    }
}
