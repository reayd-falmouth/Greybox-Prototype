using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

/// <summary>
/// Verifies CalculatePipCountPlayer1 / CalculatePipCountPlayer2 read the correct arrays
/// and produce correct pip values. Player1Checkers and Player2Checkers use the same index
/// convention: index i = board point i, pip contribution = (i+1); index 24 = bar = 25 pips.
/// </summary>
public class BackgammonPipCountEditModeTests
{
    private GameObject _go;
    private BackgammonGameController _ctrl;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("PipCountTest");
        _ctrl = _go.AddComponent<BackgammonGameController>();
        _ctrl.NewGame();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
    }

    private GameState S => _ctrl.State;

    [Test]
    public void Player1_CheckerAtIndex0_Returns1Pip()
    {
        System.Array.Clear(S.Player1Checkers, 0, 25);
        S.Player1Checkers[0] = 1;
        Assert.AreEqual(1, _ctrl.CalculatePipCountPlayer1());
    }

    [Test]
    public void Player1_CheckerAtIndex22_Returns23Pips()
    {
        System.Array.Clear(S.Player1Checkers, 0, 25);
        S.Player1Checkers[22] = 1;
        Assert.AreEqual(23, _ctrl.CalculatePipCountPlayer1());
    }

    [Test]
    public void Player1_BarChecker_Returns25Pips()
    {
        System.Array.Clear(S.Player1Checkers, 0, 25);
        S.Player1Checkers[24] = 1;
        Assert.AreEqual(25, _ctrl.CalculatePipCountPlayer1());
    }

    [Test]
    public void Player2_CheckerAtIndex0_Returns1Pip()
    {
        System.Array.Clear(S.Player2Checkers, 0, 25);
        S.Player2Checkers[0] = 1;
        Assert.AreEqual(1, _ctrl.CalculatePipCountPlayer2());
    }

    [Test]
    public void Player2_CheckerAtIndex22_Returns23Pips()
    {
        System.Array.Clear(S.Player2Checkers, 0, 25);
        S.Player2Checkers[22] = 1;
        Assert.AreEqual(23, _ctrl.CalculatePipCountPlayer2());
    }

    [Test]
    public void Player2_BarChecker_Returns25Pips()
    {
        System.Array.Clear(S.Player2Checkers, 0, 25);
        S.Player2Checkers[24] = 1;
        Assert.AreEqual(25, _ctrl.CalculatePipCountPlayer2());
    }

    /// <summary>
    /// Standard backgammon starting position: both players have 167 pips.
    /// Layout per player (0-based engine indices): [23]=2, [12]=5, [7]=3, [5]=5
    /// = 2*24 + 5*13 + 3*8 + 5*6 = 48+65+24+30 = 167
    /// Verified against EngineCLI CreateStartingPosition which uses the same indices for both arrays.
    /// </summary>
    [Test]
    public void StandardStartPosition_BothPlayersHave167Pips()
    {
        var layout = new int[25];
        layout[23] = 2; layout[12] = 5; layout[7] = 3; layout[5] = 5;

        System.Array.Copy(layout, S.Player1Checkers, 25);
        System.Array.Copy(layout, S.Player2Checkers, 25);

        Assert.AreEqual(167, _ctrl.CalculatePipCountPlayer1(), "Player1 should have 167 pips at start");
        Assert.AreEqual(167, _ctrl.CalculatePipCountPlayer2(), "Player2 should have 167 pips at start");
    }

    /// <summary>
    /// Pip counts are stable across SwapSidesForNextTurn: swapping the arrays should not
    /// change what CalculatePipCountPlayer1/2 return (they always read the same named array).
    /// </summary>
    [Test]
    public void PipCounts_UnchangedBySwapSidesForNextTurn()
    {
        System.Array.Clear(S.Player1Checkers, 0, 25);
        System.Array.Clear(S.Player2Checkers, 0, 25);
        S.Player1Checkers[10] = 3;
        S.Player2Checkers[20] = 2;

        int p1Before = _ctrl.CalculatePipCountPlayer1();
        int p2Before = _ctrl.CalculatePipCountPlayer2();

        BackgammonGameRules.SwapSidesForNextTurn(S);

        // After swap Player1Checkers and Player2Checkers swap — so pip counts swap too.
        // The named accessors now return the other player's data.
        int p1After = _ctrl.CalculatePipCountPlayer1();
        int p2After = _ctrl.CalculatePipCountPlayer2();

        Assert.AreEqual(p2Before, p1After, "After swap, Player1Checkers holds what was Player2");
        Assert.AreEqual(p1Before, p2After, "After swap, Player2Checkers holds what was Player1");
    }

    /// <summary>
    /// After a hit: Player1's checker moves from a board point to bar.
    /// Player1 pip count must increase (bar = 25 pips, higher than point 5 = 6 pips).
    /// </summary>
    [Test]
    public void AfterHit_Player1PipCountReflectsBar()
    {
        System.Array.Clear(S.Player1Checkers, 0, 25);
        S.Player1Checkers[5] = 1;

        int pipsBefore = _ctrl.CalculatePipCountPlayer1();
        Assert.AreEqual(6, pipsBefore);

        S.Player1Checkers[5]--;
        S.Player1Checkers[24]++;

        int pipsAfter = _ctrl.CalculatePipCountPlayer1();
        Assert.AreEqual(25, pipsAfter);
        Assert.Greater(pipsAfter, pipsBefore);
    }
}
