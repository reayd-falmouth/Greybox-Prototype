using NUnit.Framework;
using Runtime.RMC.Backgammon;
using Runtime.RMC.Backgammon.Core;
using System.Reflection;
using UnityEngine;

public class BackgammonRunModeManagerEditModeTests
{
    private GameObject _go;
    private BackgammonGameController _controller;
    private BackgammonRunModeManager _manager;
    private RunConfig _cfg;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("RunModeManagerTest");
        _controller = _go.AddComponent<BackgammonGameController>();
        _manager = _go.AddComponent<BackgammonRunModeManager>();

        // Wire controller reference via reflection (SerializeField)
        var field = typeof(BackgammonRunModeManager)
            .GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(_manager, _controller);

        // Manually call OnEnable to subscribe events
        typeof(BackgammonRunModeManager)
            .GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(_manager, null);

        _cfg = RunConfig.BuildDefault(anteCount: 3, scaleFactor: 1.5f, baseSmall: 50, maxGames: 3);
        _manager.StartRun(_cfg);
    }

    [TearDown]
    public void TearDown()
    {
        typeof(BackgammonRunModeManager)
            .GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(_manager, null);
        Object.DestroyImmediate(_go);
    }

    private void SimulateGameEnd(int winnerIdx, int baseStake = 1, int cubeValue = 1, int gammonMult = 1)
    {
        // Fire the event directly on the controller to simulate a game ending
        typeof(BackgammonGameController)
            .GetField("OnGameEndedWithScore", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(_controller);

        // Invoke via the event field delegate
        var eventInfo = typeof(BackgammonGameController)
            .GetField("OnGameEndedWithScore", BindingFlags.Public | BindingFlags.Instance);
        var del = eventInfo?.GetValue(_controller) as System.Action<int, int, int, int>;
        del?.Invoke(winnerIdx, baseStake, cubeValue, gammonMult);
    }

    [Test]
    public void StartRun_InitialisesRunState()
    {
        Assert.IsNotNull(_manager.RunState);
        Assert.AreEqual(0, _manager.RunState.CurrentAnteIndex);
        Assert.AreEqual(0, _manager.RunState.CurrentSessionIndex);
        Assert.AreEqual(0, _manager.RunState.RunningScore);
    }

    [Test]
    public void GameWin_AccumulatesScore()
    {
        // Human player index = BackgammonPlayerRoles.LocalPlayerIndex = 1
        SimulateGameEnd(BackgammonPlayerRoles.LocalPlayerIndex, baseStake: 1, cubeValue: 5, gammonMult: 1);
        Assert.AreEqual(5, _manager.RunState.RunningScore);
    }

    [Test]
    public void GammonWin_DoublesGameScore()
    {
        SimulateGameEnd(BackgammonPlayerRoles.LocalPlayerIndex, baseStake: 1, cubeValue: 2, gammonMult: 2);
        Assert.AreEqual(4, _manager.RunState.RunningScore);
    }

    [Test]
    public void AiWin_DoesNotAccumulateScore()
    {
        int aiIndex = BackgammonPlayerRoles.LocalPlayerIndex == 0 ? 1 : 0;
        SimulateGameEnd(aiIndex, baseStake: 1, cubeValue: 8, gammonMult: 3);
        Assert.AreEqual(0, _manager.RunState.RunningScore);
    }

    [Test]
    public void SessionComplete_WhenScoreExceedsThreshold_FiresEvent()
    {
        bool fired = false;
        _manager.OnRunSessionComplete += _ => fired = true;

        int threshold = _manager.RunState.CurrentSession(_cfg).ScoreThreshold; // 50
        // One big win that exceeds threshold
        SimulateGameEnd(BackgammonPlayerRoles.LocalPlayerIndex, baseStake: threshold + 1, cubeValue: 1, gammonMult: 1);

        Assert.IsTrue(fired);
    }

    [Test]
    public void SessionComplete_SetsHasPendingFlag()
    {
        int threshold = _manager.RunState.CurrentSession(_cfg).ScoreThreshold;
        SimulateGameEnd(BackgammonPlayerRoles.LocalPlayerIndex, baseStake: threshold + 1, cubeValue: 1, gammonMult: 1);
        Assert.IsTrue(_manager.HasPendingSessionComplete);
    }

    [Test]
    public void RunFailed_WhenGamesExhaustedBeforeThreshold_FiresEvent()
    {
        bool failed = false;
        _manager.OnRunFailed += () => failed = true;

        int maxGames = _manager.RunState.CurrentSession(_cfg).MaxGames; // 3
        // Lose all games (AI wins)
        int aiIndex = BackgammonPlayerRoles.LocalPlayerIndex == 0 ? 1 : 0;
        for (int i = 0; i < maxGames; i++)
            SimulateGameEnd(aiIndex, baseStake: 1, cubeValue: 1, gammonMult: 1);

        Assert.IsTrue(failed);
    }

    [Test]
    public void OnSessionAcknowledged_AdvancesSessionIndex()
    {
        int threshold = _manager.RunState.CurrentSession(_cfg).ScoreThreshold;
        SimulateGameEnd(BackgammonPlayerRoles.LocalPlayerIndex, baseStake: threshold + 1, cubeValue: 1, gammonMult: 1);
        Assert.IsTrue(_manager.HasPendingSessionComplete);

        _manager.OnSessionAcknowledged();

        Assert.AreEqual(1, _manager.RunState.CurrentSessionIndex);
        Assert.IsFalse(_manager.HasPendingSessionComplete);
    }

    [Test]
    public void TotalCurrency_IncreasesAfterSessionComplete()
    {
        int threshold = _manager.RunState.CurrentSession(_cfg).ScoreThreshold;
        int reward    = _manager.RunState.CurrentSession(_cfg).Reward;
        SimulateGameEnd(BackgammonPlayerRoles.LocalPlayerIndex, baseStake: threshold + 1, cubeValue: 1, gammonMult: 1);

        Assert.AreEqual(reward, _manager.RunState.TotalCurrency);
    }
}
