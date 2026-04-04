using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonOpeningRollEditModeTests
{
    [Test]
    public void OpeningRoll_Tie_ReturnsFalse_LeavesDiceUnset()
    {
        var st = PositionId.Decode("4HPwATDgc/ABMA");
        bool ok = BackgammonOpeningRollRules.TryApplyOpeningDice(3, 3, st);
        Assert.IsFalse(ok);
        Assert.AreEqual(0, st.Dice1);
        Assert.AreEqual(0, st.Dice2);
    }

    [Test]
    public void OpeningRoll_Player0Wins_HigherDie_IsOnRoll()
    {
        var st = PositionId.Decode("4HPwATDgc/ABMA");
        Assert.IsTrue(BackgammonOpeningRollRules.TryApplyOpeningDice(5, 2, st));
        Assert.AreEqual(0, st.PlayerOnRoll);
        Assert.AreEqual(5, st.Dice1);
        Assert.AreEqual(2, st.Dice2);
    }

    [Test]
    public void OpeningRoll_Player1Wins_HigherDie_IsOnRoll()
    {
        var st = PositionId.Decode("4HPwATDgc/ABMA");
        Assert.IsTrue(BackgammonOpeningRollRules.TryApplyOpeningDice(1, 6, st));
        Assert.AreEqual(1, st.PlayerOnRoll);
        Assert.AreEqual(1, st.Dice1);
        Assert.AreEqual(6, st.Dice2);
    }
}
