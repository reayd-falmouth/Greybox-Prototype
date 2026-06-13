using NUnit.Framework;
using Runtime.RMC.Backgammon;
using Runtime.RMC.Backgammon.Core;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Edit-mode tests for all IHudModeProvider implementations.
/// Verifies display string contracts and SupportedMode values.
/// Also provides regression coverage for the score-display bug:
/// RunModeProvider.ScoreDisplay must reflect RunState.RunningScore, not a game-win count.
/// </summary>
public class HudModeProviderEditModeTests
{
    private GameObject _go;
    private BackgammonGameController _controller;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("ProviderTest");
        _controller = _go.AddComponent<BackgammonGameController>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
    }

    // ── MoneySessionModeManager ─────────────────────────────────────────────

    [Test]
    public void MoneySession_SupportedMode_IsMoneySession()
    {
        var provider = _go.AddComponent<MoneySessionModeManager>();
        Assert.AreEqual(GameModeType.MoneySession, provider.SupportedMode);
    }

    [Test]
    public void MoneySession_HeadingDisplay_IsMoneySession()
    {
        var provider = _go.AddComponent<MoneySessionModeManager>();
        SetControllerRef(provider, _controller);
        Assert.AreEqual("Money Session", provider.HeadingDisplay);
    }

    [Test]
    public void MoneySession_ScoreDisplay_ReturnsFormattedString()
    {
        var provider = _go.AddComponent<MoneySessionModeManager>();
        SetField(_controller, "_moneySessionPlayer1Score", 10);
        SetField(_controller, "_moneySessionPlayer2Score", 20);
        SetControllerRef(provider, _controller);
        Assert.AreEqual("$10 vs $20", provider.ScoreDisplay);
    }

    [Test]
    public void MoneySession_GamesDisplay_ReturnsOneBasedCount()
    {
        var provider = _go.AddComponent<MoneySessionModeManager>();
        SetField(_controller, "_moneySessionGamesPlayed", 2);
        SetControllerRef(provider, _controller);
        Assert.AreEqual("3", provider.GamesDisplay);
    }

    [Test]
    public void MoneySession_StakeDisplay_ReturnsFormattedDollar()
    {
        var provider = _go.AddComponent<MoneySessionModeManager>();
        var cfg = new MoneySessionConfig { BaseStake = 5 };
        SetField(_controller, "_moneySessionConfig", cfg);
        SetControllerRef(provider, _controller);
        Assert.AreEqual("$5", provider.StakeDisplay);
    }

    [Test]
    public void MoneySession_StakeDisplay_WhenZeroBaseStake_ReturnsDash()
    {
        var provider = _go.AddComponent<MoneySessionModeManager>();
        SetControllerRef(provider, _controller);
        Assert.AreEqual("—", provider.StakeDisplay);
    }

    // ── MatchPlayModeManager ────────────────────────────────────────────────

    [Test]
    public void MatchPlay_SupportedMode_IsMatchPlay()
    {
        var provider = _go.AddComponent<MatchPlayModeManager>();
        Assert.AreEqual(GameModeType.MatchPlay, provider.SupportedMode);
    }

    [Test]
    public void MatchPlay_HeadingDisplay_IsMatchPlay()
    {
        var provider = _go.AddComponent<MatchPlayModeManager>();
        SetControllerRef(provider, _controller);
        Assert.AreEqual("Match Play", provider.HeadingDisplay);
    }

    [Test]
    public void MatchPlay_ScoreDisplay_DelegatesToCurrentMatchScore()
    {
        var provider = _go.AddComponent<MatchPlayModeManager>();
        // CurrentMatchScore for Run mode reads _player1MatchScore/_player2MatchScore directly.
        // Set mode to Run on the controller so we can control the value without needing a GameState instance.
        SetField(_controller, "_currentGameMode", GameModeType.Run);
        SetField(_controller, "_player1MatchScore", 3);
        SetControllerRef(provider, _controller);
        Assert.AreEqual("3", provider.ScoreDisplay);
    }

    [Test]
    public void MatchPlay_GamesDisplay_ReturnsRemainingSlashMax()
    {
        var provider = _go.AddComponent<MatchPlayModeManager>();
        SetField(_controller, "_gamesPlayedInCurrentMatch", 1);
        SetControllerRef(provider, _controller);
        // CurrentMatchMaxGames is always 3
        Assert.AreEqual("2/3", provider.GamesDisplay);
    }

    // ── BackgammonRunModeManager (Run mode provider) ────────────────────────

    [Test]
    public void Run_SupportedMode_IsRun()
    {
        var provider = _go.AddComponent<BackgammonRunModeManager>();
        Assert.AreEqual(GameModeType.Run, provider.SupportedMode);
    }

    [Test]
    public void Run_ScoreDisplay_WhenRunStateNull_ReturnsZero()
    {
        var provider = _go.AddComponent<BackgammonRunModeManager>();
        Assert.AreEqual("0", provider.ScoreDisplay);
    }

    [Test]
    public void Run_ScoreDisplay_ReflectsRunningScore_BugRegression()
    {
        // This is the regression test for the original bug:
        // ScoreDisplay must return the session running score, NOT a game-win count.
        var manager = _go.AddComponent<BackgammonRunModeManager>();
        SetField(manager, "_controller", _controller);
        InvokePrivate(manager, "OnEnable");

        var cfg = RunConfig.BuildDefault(anteCount: 3, scaleFactor: 1.5f, baseSmall: 50, maxGames: 3);
        manager.StartRun(cfg);

        // Human wins 2 games worth 5 points each
        FireGameEnded(_controller, BackgammonPlayerRoles.LocalPlayerIndex, baseStake: 5, cubeValue: 1, gammonMult: 1);
        Assert.AreEqual("5", manager.ScoreDisplay, "After first win ScoreDisplay should be 5");

        FireGameEnded(_controller, BackgammonPlayerRoles.LocalPlayerIndex, baseStake: 5, cubeValue: 1, gammonMult: 1);
        Assert.AreEqual("10", manager.ScoreDisplay, "After second win ScoreDisplay should be 10");

        InvokePrivate(manager, "OnDisable");
    }

    [Test]
    public void Run_ScoreDisplay_AiWin_DoesNotChangeScore()
    {
        var manager = _go.AddComponent<BackgammonRunModeManager>();
        SetField(manager, "_controller", _controller);
        InvokePrivate(manager, "OnEnable");

        var cfg = RunConfig.BuildDefault(anteCount: 3, scaleFactor: 1.5f, baseSmall: 50, maxGames: 3);
        manager.StartRun(cfg);

        int aiIdx = BackgammonPlayerRoles.LocalPlayerIndex == 0 ? 1 : 0;
        FireGameEnded(_controller, aiIdx, baseStake: 10, cubeValue: 4, gammonMult: 3);
        Assert.AreEqual("0", manager.ScoreDisplay);

        InvokePrivate(manager, "OnDisable");
    }

    [Test]
    public void Run_GamesDisplay_ReflectsGamesRemaining()
    {
        var manager = _go.AddComponent<BackgammonRunModeManager>();
        SetField(manager, "_controller", _controller);
        InvokePrivate(manager, "OnEnable");

        var cfg = RunConfig.BuildDefault(anteCount: 3, scaleFactor: 1.5f, baseSmall: 50, maxGames: 3);
        manager.StartRun(cfg);

        Assert.AreEqual("3", manager.GamesDisplay);

        int aiIdx = BackgammonPlayerRoles.LocalPlayerIndex == 0 ? 1 : 0;
        FireGameEnded(_controller, aiIdx, 1, 1, 1);
        Assert.AreEqual("2", manager.GamesDisplay);

        InvokePrivate(manager, "OnDisable");
    }

    [Test]
    public void Run_HeadingDisplay_ContainsAnte1Small()
    {
        var manager = _go.AddComponent<BackgammonRunModeManager>();
        SetField(manager, "_controller", _controller);
        InvokePrivate(manager, "OnEnable");

        var cfg = RunConfig.BuildDefault(anteCount: 3, scaleFactor: 1.5f, baseSmall: 50, maxGames: 3);
        manager.StartRun(cfg);

        string heading = manager.HeadingDisplay;
        Assert.IsTrue(heading.Contains("Ante 1"), $"Expected heading to contain 'Ante 1', got: {heading}");
        Assert.IsTrue(heading.Contains("Small"), $"Expected heading to contain 'Small', got: {heading}");

        InvokePrivate(manager, "OnDisable");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void SetControllerRef(HudModeProviderBase provider, BackgammonGameController ctrl)
    {
        // HudModeProviderBase.GameController is protected; set via reflection
        typeof(HudModeProviderBase)
            .GetField("GameController", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(provider, ctrl);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
            ?.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(target, null);
    }

    private static void FireGameEnded(BackgammonGameController ctrl,
        int winnerIdx, int baseStake, int cubeValue, int gammonMult)
    {
        var field = typeof(BackgammonGameController)
            .GetField("OnGameEndedWithScore", BindingFlags.Public | BindingFlags.Instance);
        var del = field?.GetValue(ctrl) as System.Action<int, int, int, int>;
        del?.Invoke(winnerIdx, baseStake, cubeValue, gammonMult);
    }
}
