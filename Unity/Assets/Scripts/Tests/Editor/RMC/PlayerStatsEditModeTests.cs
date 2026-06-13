using NUnit.Framework;
using Runtime.RMC.Backgammon.Stats;

public class PlayerStatsEditModeTests
{
    [SetUp]
    public void SetUp() => PlayerStats.ResetAll();

    [TearDown]
    public void TearDown() => PlayerStats.ResetAll();

    [Test]
    public void PlayerStats_DefaultRating_Is1500()
    {
        Assert.AreEqual(1500f, PlayerStats.Rating, 0.001f);
    }

    [Test]
    public void PlayerStats_RecordGameEnd_IncrementsGameCount()
    {
        PlayerStats.RecordGameEnd(true, 10, 10);
        Assert.AreEqual(1, PlayerStats.TotalGamesPlayed);
        PlayerStats.RecordGameEnd(false, 5, 5);
        Assert.AreEqual(2, PlayerStats.TotalGamesPlayed);
    }

    [Test]
    public void PlayerStats_RecordGameEnd_UpdatesHighestScore_WhenBetter()
    {
        PlayerStats.RecordGameEnd(true, 20, 20);
        Assert.AreEqual(20, PlayerStats.HighestScore);
        PlayerStats.RecordGameEnd(true, 50, 50);
        Assert.AreEqual(50, PlayerStats.HighestScore);
    }

    [Test]
    public void PlayerStats_RecordGameEnd_KeepsHighestScore_WhenWorse()
    {
        PlayerStats.RecordGameEnd(true, 50, 50);
        PlayerStats.RecordGameEnd(true, 10, 10);
        Assert.AreEqual(50, PlayerStats.HighestScore);
    }

    [Test]
    public void PlayerStats_RecordGameEnd_RatingIncreases_OnWin()
    {
        float before = PlayerStats.Rating;
        PlayerStats.RecordGameEnd(true, 10, 10);
        Assert.Greater(PlayerStats.Rating, before);
    }

    [Test]
    public void PlayerStats_RecordGameEnd_RatingDecreases_OnLoss()
    {
        float before = PlayerStats.Rating;
        PlayerStats.RecordGameEnd(false, 0, 0);
        Assert.Less(PlayerStats.Rating, before);
    }

    [Test]
    public void PlayerStats_ResetAll_ClearsAllValues()
    {
        PlayerStats.RecordGameEnd(true, 100, 100);
        PlayerStats.RecordSessionStart();
        PlayerStats.ResetAll();

        Assert.AreEqual(1500f, PlayerStats.Rating, 0.001f);
        Assert.AreEqual(0, PlayerStats.HighestScore);
        Assert.AreEqual(0, PlayerStats.TotalGamesPlayed);
        Assert.AreEqual(0, PlayerStats.TotalSessionsPlayed);
        Assert.AreEqual(0, PlayerStats.HighestMoneyEarned);
    }
}
