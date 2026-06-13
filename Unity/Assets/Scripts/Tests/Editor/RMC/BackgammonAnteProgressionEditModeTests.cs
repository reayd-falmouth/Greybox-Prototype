using System.Collections.Generic;
using System.Reflection;
using EngineCore;
using NUnit.Framework;
using UnityEngine;

public class BackgammonAnteProgressionEditModeTests
{
    [Test]
    public void AnteProgress_ConfiguredEmpty_UsesDefaultSingleAnte()
    {
        var go = new GameObject("AnteProgressTest_Default");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests", null, true);

        Assert.AreEqual(1, controller.TotalAntes);
        Assert.AreEqual(1, controller.CurrentAnteNumber);
        Assert.AreEqual(1, controller.CurrentMatchNumber);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void AnteProgress_TargetScoreCompletion_AdvancesMatchIndex()
    {
        var go = new GameObject("AnteProgressTest_MatchAdvance");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests", new List<int[]>
        {
            new[] { 2, 3, 5 }
        }, true);
        InvokeNonPublic(controller, "DebugSetAnteProgressForTests", 0, 0, 1, 0);
        InvokeNonPublic(controller, "DebugApplyWinnerToAnteProgressForTests", true);

        Assert.AreEqual(1, controller.CurrentAnteNumber);
        Assert.AreEqual(2, controller.CurrentMatchNumber);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void AnteProgress_AnteCompletion_AdvancesAnteIndex()
    {
        var go = new GameObject("AnteProgressTest_AnteAdvance");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests", new List<int[]>
        {
            new[] { 1, 1, 1 },
            new[] { 3, 5, 7 }
        }, true);
        InvokeNonPublic(controller, "DebugSetAnteProgressForTests", 0, 2, 0, 0);
        InvokeNonPublic(controller, "DebugApplyWinnerToAnteProgressForTests", true);

        Assert.AreEqual(2, controller.CurrentAnteNumber);
        Assert.AreEqual(1, controller.CurrentMatchNumber);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void AnteProgress_FinalAnteNoLoop_MarksRunComplete()
    {
        var go = new GameObject("AnteProgressTest_RunComplete");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests", new List<int[]>
        {
            new[] { 1, 1, 1 }
        }, false);
        InvokeNonPublic(controller, "DebugSetAnteProgressForTests", 0, 2, 0, 0);
        InvokeNonPublic(controller, "DebugApplyWinnerToAnteProgressForTests", true);

        bool runComplete = (bool)GetPrivateField(controller, "_runComplete");
        Assert.IsTrue(runComplete);
        Assert.AreEqual(1, controller.CurrentAnteNumber);
        Assert.AreEqual(1, controller.CurrentMatchNumber);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RunCurrency_NonCompletingGame_AppliesPerGameReward()
    {
        var go = new GameObject("AnteProgressTest_CurrencyGame");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests", new List<int[]>
        {
            new[] { 3, 5, 7 }
        }, true);
        InvokeNonPublic(controller, "DebugConfigureRunCurrencyForTests", 10, 2, -1, 5, 10);
        InvokeNonPublic(controller, "DebugSetAnteProgressForTests", 0, 0, 0, 0);
        InvokeNonPublic(controller, "DebugApplyWinnerToAnteProgressForTests", true);

        Assert.AreEqual(12, controller.RunCurrency);
        Assert.AreEqual(1, controller.CurrentAnteNumber);
        Assert.AreEqual(1, controller.CurrentMatchNumber);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RunCurrency_MatchAndAnteCompletion_AppliesBonusesAndCarriesForward()
    {
        var go = new GameObject("AnteProgressTest_CurrencyAnte");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests", new List<int[]>
        {
            new[] { 1, 1, 1 },
            new[] { 3, 5, 7 }
        }, true);
        InvokeNonPublic(controller, "DebugConfigureRunCurrencyForTests", 10, 2, -1, 5, 10);
        InvokeNonPublic(controller, "DebugSetAnteProgressForTests", 0, 2, 0, 0);
        InvokeNonPublic(controller, "DebugApplyWinnerToAnteProgressForTests", true);

        Assert.AreEqual(27, controller.RunCurrency);
        Assert.AreEqual(2, controller.CurrentAnteNumber);
        Assert.AreEqual(1, controller.CurrentMatchNumber);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void MatchScore_StartsAtZero_AndIncrementsAfterGameEndOnly()
    {
        var go = new GameObject("AnteProgressTest_ScoreIncrement");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests", new List<int[]>
        {
            new[] { 3, 5, 7 }
        }, true);
        InvokeNonPublic(controller, "DebugSetAnteProgressForTests", 0, 0, 0, 0);

        Assert.AreEqual(0, controller.CurrentMatchScore);
        InvokeNonPublic(controller, "DebugApplyWinnerToAnteProgressForTests", true);
        Assert.AreEqual(1, controller.CurrentMatchScore);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScoreGain_UsesBaseStakeCubeAndResultMultiplier()
    {
        var go = new GameObject("AnteProgressTest_ScoreFormula");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForTests",
            new List<int[]> { new[] { 1000, 1000, 1000 } },
            true);
        GameState state = PositionId.Decode("4HPwATDgc/ABMA");
        SetPrivateField(controller, "<State>k__BackingField", state);
        state.CubeValue = 2;
        state.Player1Checkers = new int[25];
        state.Player2Checkers = new int[25];
        state.Player1Checkers[1] = 15; // loser has borne off none and no bar checker => gammon multiplier (2x)

        int gain = (int)InvokeNonPublicWithResult(controller, "ComputeScoreGainForWinner", "Player 1 wins");
        Assert.AreEqual(4, gain);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void MatchCapReached_HigherScoreWinsEvenIfLastGameLost()
    {
        var go = new GameObject("AnteProgressTest_CapWinner");
        var controller = go.AddComponent<BackgammonGameController>();

        InvokeNonPublic(controller, "DebugConfigureAnteProgressionForScoreTests",
            new List<int[]> { new[] { 1000, 1000, 1000 } },
            new List<int[]> { new[] { 1, 1, 1 } },
            new List<int[]> { new[] { 2, 2, 2 } },
            true);
        InvokeNonPublic(controller, "DebugSetAnteProgressForTests", 0, 0, 3, 1);
        SetPrivateField(controller, "_gamesPlayedInCurrentMatch", 1);
        InvokeNonPublic(controller, "DebugApplyWinnerToAnteProgressForTests", false);

        Assert.AreEqual(1, controller.CurrentAnteNumber);
        Assert.AreEqual(2, controller.CurrentMatchNumber);
        Assert.AreEqual(0, controller.CurrentMatchGamesPlayed);

        Object.DestroyImmediate(go);
    }

    private static void InvokeNonPublic(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' to exist.");
        method.Invoke(target, args);
    }

    private static object InvokeNonPublicWithResult(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' to exist.");
        return method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
        return field.GetValue(target);
    }
}
