using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class RunStateEditModeTests
{
    private RunConfig _cfg;
    private RunState  _state;

    [SetUp]
    public void SetUp()
    {
        _cfg   = RunConfig.BuildDefault();
        _state = new RunState();
        _state.ResetForNewSession();
    }

    [Test]
    public void InitialState_RunningScore_IsZero()
    {
        Assert.AreEqual(0, _state.RunningScore);
    }

    [Test]
    public void InitialState_GamesPlayed_IsZero()
    {
        Assert.AreEqual(0, _state.GamesPlayedThisSession);
    }

    [Test]
    public void ScoreAccumulation_TwoGames_CorrectTotal()
    {
        _state.RunningScore += 30;
        _state.RunningScore += 40;
        Assert.AreEqual(70, _state.RunningScore);
    }

    [Test]
    public void SessionComplete_WhenScoreReachesThreshold()
    {
        int threshold = _state.CurrentSession(_cfg).ScoreThreshold; // 50
        _state.RunningScore = threshold;
        Assert.GreaterOrEqual(_state.RunningScore, threshold);
    }

    [Test]
    public void RunFailed_WhenGamesExhaustedBeforeThreshold()
    {
        int maxGames = _state.CurrentSession(_cfg).MaxGames;
        _state.GamesPlayedThisSession = maxGames;
        int threshold = _state.CurrentSession(_cfg).ScoreThreshold;
        _state.RunningScore = threshold - 1;

        bool failed = _state.GamesPlayedThisSession >= maxGames && _state.RunningScore < threshold;
        Assert.IsTrue(failed);
    }

    [Test]
    public void AdvanceSession_ResetsScoreAndGames()
    {
        _state.RunningScore = 999;
        _state.GamesPlayedThisSession = 3;
        _state.AdvanceSession(_cfg);

        Assert.AreEqual(0, _state.RunningScore);
        Assert.AreEqual(0, _state.GamesPlayedThisSession);
    }

    [Test]
    public void AdvanceSession_IncrementsSessionIndex()
    {
        Assert.AreEqual(0, _state.CurrentSessionIndex);
        _state.AdvanceSession(_cfg);
        Assert.AreEqual(1, _state.CurrentSessionIndex);
    }

    [Test]
    public void AdvanceAnte_AfterThreeSessions_IncrementsAnteIndex()
    {
        _state.AdvanceSession(_cfg); // session 1
        _state.AdvanceSession(_cfg); // session 2 (Boss)
        _state.AdvanceSession(_cfg); // wraps to Ante 2, session 0
        Assert.AreEqual(1, _state.CurrentAnteIndex);
        Assert.AreEqual(0, _state.CurrentSessionIndex);
    }

    [Test]
    public void RunWon_AfterFinalAnteAndSession()
    {
        // 3 antes × 3 sessions = 9 advances to win
        for (int i = 0; i < 9; i++)
            _state.AdvanceSession(_cfg);

        Assert.IsTrue(_state.IsRunWon);
    }

    [Test]
    public void BossSession_IsBossSession_IsTrue_AtSessionIndex2()
    {
        _state.AdvanceSession(_cfg); // -> session 1
        _state.AdvanceSession(_cfg); // -> session 2 (Boss)
        Assert.IsTrue(_state.IsBossSession);
    }

    [Test]
    public void CurrentSession_ReturnsCorrectConfig()
    {
        var session = _state.CurrentSession(_cfg);
        Assert.AreEqual(RunSessionType.Small, session.SessionType);
        Assert.AreEqual(50, session.ScoreThreshold);
    }
}
