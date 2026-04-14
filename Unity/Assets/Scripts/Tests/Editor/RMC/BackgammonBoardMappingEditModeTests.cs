using EngineCore;
using System.Collections.Generic;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonBoardMappingEditModeTests
{
    [Test]
    public void StartingPosition_MapsKeyEnginePointsToExpectedBoardIndices()
    {
        BackgammonBoardLayout.SetHorizontal(true);
        GameState st = PositionId.Decode("4HPwATDgc/ABMA");
        Assert.AreEqual(2, st.Player1Checkers[23]);
        Assert.AreEqual(0, BackgammonBoardLayout.EnginePointToBoardIndex(23));
        Assert.AreEqual(5, st.Player1Checkers[5]);
        Assert.AreEqual(18, BackgammonBoardLayout.EnginePointToBoardIndex(5));
        Assert.AreEqual(5, st.Player1Checkers[12]);
        Assert.AreEqual(11, BackgammonBoardLayout.EnginePointToBoardIndex(12));
        Assert.AreEqual(3, st.Player1Checkers[7]);
        Assert.AreEqual(16, BackgammonBoardLayout.EnginePointToBoardIndex(7));
    }

    [Test]
    public void EnginePoint_BoardIndex_RoundTrips()
    {
        BackgammonBoardLayout.SetHorizontal(true);
        for (int e = 0; e < 24; e++)
        {
            int b = BackgammonBoardLayout.EnginePointToBoardIndex(e);
            Assert.AreEqual(e, BackgammonBoardLayout.BoardIndexToEnginePoint(b));
        }
    }

    [Test]
    public void VerticalMapping_IsPermutation_AndRoundTrips()
    {
        BackgammonBoardLayout.SetHorizontal(false);
        var seen = new HashSet<int>();
        for (int e = 0; e < 24; e++)
        {
            int b = BackgammonBoardLayout.EnginePointToBoardIndex(e);
            Assert.IsTrue(b >= 0 && b < 24, $"board index out of range for engine {e}");
            Assert.IsTrue(seen.Add(b), $"duplicate board index {b}");
            Assert.AreEqual(e, BackgammonBoardLayout.BoardIndexToEnginePoint(b));
        }
    }

    [Test]
    public void VerticalMapping_DiffersFromHorizontal()
    {
        int[] horizontal = new int[24];
        int[] vertical = new int[24];
        BackgammonBoardLayout.SetHorizontal(true);
        for (int e = 0; e < 24; e++)
            horizontal[e] = BackgammonBoardLayout.EnginePointToBoardIndex(e);
        BackgammonBoardLayout.SetHorizontal(false);
        for (int e = 0; e < 24; e++)
            vertical[e] = BackgammonBoardLayout.EnginePointToBoardIndex(e);

        int sameSlots = 0;
        for (int e = 0; e < 24; e++)
            if (horizontal[e] == vertical[e]) sameSlots++;

        Assert.Less(sameSlots, 24, "vertical mapping should not equal horizontal mapping");
    }

    [Test]
    public void ApplyTurn_ThenSwap_PreservesFifteenCheckersPerSide()
    {
        BackgammonBoardLayout.SetHorizontal(true);
        GameState st = PositionId.Decode("4HPwATDgc/ABMA");
        st.Dice1 = 3;
        st.Dice2 = 1;
        var legals = MoveGenerator.GenerateLegalTurns(st);
        Assert.IsNotEmpty(legals);
        GameState next = MoveGenerator.ApplyTurn(st, legals[0]);
        BackgammonGameRules.SwapSidesForNextTurn(next);
        int s1 = 0, s2 = 0;
        for (int i = 0; i <= 24; i++)
        {
            s1 += next.Player1Checkers[i];
            s2 += next.Player2Checkers[i];
        }

        Assert.AreEqual(15, s1);
        Assert.AreEqual(15, s2);
    }
}
