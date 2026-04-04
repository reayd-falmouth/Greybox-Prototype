using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonBoardMappingEditModeTests
{
    [Test]
    public void StartingPosition_MapsKeyEnginePointsToExpectedBoardIndices()
    {
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
        for (int e = 0; e < 24; e++)
        {
            int b = BackgammonBoardLayout.EnginePointToBoardIndex(e);
            Assert.AreEqual(e, BackgammonBoardLayout.BoardIndexToEnginePoint(b));
        }
    }

    [Test]
    public void ApplyTurn_ThenSwap_PreservesFifteenCheckersPerSide()
    {
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
