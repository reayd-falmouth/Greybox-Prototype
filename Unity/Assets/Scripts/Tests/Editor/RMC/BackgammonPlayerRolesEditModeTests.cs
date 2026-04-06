using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonPlayerRolesEditModeTests
{
    [Test]
    public void OpponentAiMode_Player0OnRoll_IsAiTurn()
    {
        Assert.IsTrue(BackgammonPlayerRoles.IsAiTurnInOpponentAiMode(0));
    }

    [Test]
    public void OpponentAiMode_Player1OnRoll_IsHumanTurn()
    {
        Assert.IsFalse(BackgammonPlayerRoles.IsAiTurnInOpponentAiMode(1));
    }
}
