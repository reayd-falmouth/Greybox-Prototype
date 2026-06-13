using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonGameRulesScoringEditModeTests
{
    [Test]
    public void IsGammonLoss_True_WhenLoserHasNoCheckersBorneOff()
    {
        int[] loser = new int[25];
        loser[6] = 15;
        Assert.IsTrue(BackgammonGameRules.IsGammonLoss(loser));
    }

    [Test]
    public void IsGammonLoss_False_WhenLoserHasBorneOffCheckers()
    {
        int[] loser = new int[25];
        loser[6] = 12;
        Assert.IsFalse(BackgammonGameRules.IsGammonLoss(loser));
    }

    [Test]
    public void IsBackgammonLoss_True_WhenGammonAndCheckerOnBar()
    {
        int[] loser = new int[25];
        loser[5] = 14;
        loser[24] = 1;
        Assert.IsTrue(BackgammonGameRules.IsBackgammonLoss(loser, 24));
    }
}
