using NUnit.Framework;
using Runtime.RMC.Backgammon.Stats;

public class SessionStatsEditModeTests
{
    private SessionStats _stats;

    [SetUp]
    public void SetUp() => _stats = new SessionStats();

    [Test]
    public void SessionStats_InitialState_AllZero()
    {
        Assert.AreEqual(0, _stats.GamesPlayedThisSession);
        Assert.AreEqual(0, _stats.TotalRollsThisSession);
        Assert.AreEqual(0, _stats.TotalDiceRolled);
        Assert.AreEqual(0f, _stats.GetDiceFacePercent(1), 0.001f);
    }

    [Test]
    public void SessionStats_RecordDiceRoll_IncrementsCorrectFaces()
    {
        _stats.RecordDiceRoll(3, 5);
        Assert.AreEqual(1, _stats.DiceRollCounts[3]);
        Assert.AreEqual(1, _stats.DiceRollCounts[5]);
        Assert.AreEqual(2, _stats.TotalDiceRolled);
        Assert.AreEqual(1, _stats.TotalRollsThisSession);
    }

    [Test]
    public void SessionStats_RecordDiceRoll_DoublesCountBothFaces()
    {
        _stats.RecordDiceRoll(6, 6);
        Assert.AreEqual(2, _stats.DiceRollCounts[6]);
        Assert.AreEqual(2, _stats.TotalDiceRolled);
    }

    [Test]
    public void SessionStats_GetDiceFacePercent_Returns0_WhenNoRolls()
    {
        Assert.AreEqual(0f, _stats.GetDiceFacePercent(1), 0.001f);
    }

    [Test]
    public void SessionStats_GetDiceFacePercent_Correct_AfterRolls()
    {
        _stats.RecordDiceRoll(1, 1); // 2 ones out of 2 total → 100%
        Assert.AreEqual(100f, _stats.GetDiceFacePercent(1), 0.001f);
        Assert.AreEqual(0f,   _stats.GetDiceFacePercent(2), 0.001f);
    }

    [Test]
    public void SessionStats_Reset_ClearsDistribution()
    {
        _stats.RecordDiceRoll(4, 4);
        _stats.Reset();
        Assert.AreEqual(0, _stats.TotalDiceRolled);
        Assert.AreEqual(0f, _stats.GetDiceFacePercent(4), 0.001f);
    }

    [Test]
    public void SessionStats_RecordGameEnd_IncrementsGameCount()
    {
        _stats.RecordGameEnd(true, 10, 10);
        Assert.AreEqual(1, _stats.GamesPlayedThisSession);
        Assert.AreEqual(1, _stats.GamesWonThisSession);
        _stats.RecordGameEnd(false, 8, 8);
        Assert.AreEqual(2, _stats.GamesPlayedThisSession);
        Assert.AreEqual(1, _stats.GamesWonThisSession);
    }
}
