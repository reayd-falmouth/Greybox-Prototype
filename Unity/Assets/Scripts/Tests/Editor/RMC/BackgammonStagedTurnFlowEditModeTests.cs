using System.Collections;
using System.Reflection;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

public class BackgammonStagedTurnFlowEditModeTests
{
    [Test]
    public void CheckerClickPath_AppliesSingleMove_DoesNotFinalizeTurn()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);
            int playerOnRollBefore = ctrl.State.PlayerOnRoll;
            int turnsCompletedBefore = ctrl.TurnsCompletedThisGame;
            Move first = ctrl.CurrentLegalTurns[0].Moves[0];

            bool applied = ctrl.TryApplyPreferredTurnForFrom(first.From, true);

            Assert.IsTrue(applied);
            Assert.IsTrue(ctrl.HasRolledThisTurn);
            Assert.AreEqual(playerOnRollBefore, ctrl.State.PlayerOnRoll);
            Assert.AreEqual(turnsCompletedBefore, ctrl.TurnsCompletedThisGame);
            if (ctrl.CurrentLegalTurns.Count > 0)
            {
                bool hasNonNoOpFirstMove = false;
                for (int i = 0; i < ctrl.CurrentLegalTurns.Count; i++)
                {
                    Turn t = ctrl.CurrentLegalTurns[i];
                    if (t?.Moves == null || t.Moves.Count == 0) continue;
                    Move m = t.Moves[0];
                    if (m.From != m.To)
                    {
                        hasNonNoOpFirstMove = true;
                        break;
                    }
                }
                Assert.IsTrue(hasNonNoOpFirstMove, "Remaining staged legal turns should not degrade into from==to no-op moves.");
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Finalize_BecomesAvailableWhenNoLegalsRemain_ThenAdvancesTurn()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);

            int startPlayer = ctrl.State.PlayerOnRoll;
            int guard = 0;
            while (ctrl.CurrentLegalTurns.Count > 0 && guard++ < 8)
            {
                Move m = ctrl.CurrentLegalTurns[0].Moves[0];
                Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m.From, true));
            }

            Assert.AreEqual(0, ctrl.CurrentLegalTurns.Count);
            Assert.IsTrue(ctrl.CanFinalizeCurrentTurn);
            Assert.IsTrue(ctrl.TryFinalizeCurrentTurn());
            Assert.IsFalse(ctrl.HasRolledThisTurn);
            Assert.AreEqual(startPlayer == 0 ? 1 : 0, ctrl.State.PlayerOnRoll);
            Assert.AreEqual(1, ctrl.TurnsCompletedThisGame);
            Assert.IsFalse(ctrl.CanUndo, "Undo stack should clear when the turn completes.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void UndoStack_ClearedAfterHumanFinalizesTurn_WithStagedMoves()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(1, 1);

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);
            Move m1 = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m1.From, true));
            Assert.IsTrue(ctrl.CanUndo, "Expected at least one undo frame after a move within the roll.");

            int guard = 0;
            while (ctrl.CurrentLegalTurns.Count > 0 && guard++ < 16)
            {
                Move m = ctrl.CurrentLegalTurns[0].Moves[0];
                Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m.From, true));
            }

            Assert.AreEqual(0, ctrl.CurrentLegalTurns.Count);
            Assert.IsTrue(ctrl.TryFinalizeCurrentTurn());
            Assert.IsFalse(ctrl.CanUndo, "Undo must not span completed turns.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Undo_IsLifo_PerSingleMove()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(1, 1);

            int[] preP1 = (int[])ctrl.State.Player1Checkers.Clone();
            int[] preP2 = (int[])ctrl.State.Player2Checkers.Clone();

            Move m1 = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m1.From, true));
            int[] midP1 = (int[])ctrl.State.Player1Checkers.Clone();
            int[] midP2 = (int[])ctrl.State.Player2Checkers.Clone();

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);
            Move m2 = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m2.From, true));

            Assert.IsTrue(ctrl.TryUndoLastMove());
            CollectionAssert.AreEqual(midP1, ctrl.State.Player1Checkers);
            CollectionAssert.AreEqual(midP2, ctrl.State.Player2Checkers);

            Assert.IsTrue(ctrl.TryUndoLastMove());
            CollectionAssert.AreEqual(preP1, ctrl.State.Player1Checkers);
            CollectionAssert.AreEqual(preP2, ctrl.State.Player2Checkers);
            Assert.IsTrue(ctrl.HasRolledThisTurn);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AiTurn_ClearsUndoStack_OnCompletion()
    {
        var go = new GameObject("BackgammonGameController_AiUndoTest");
        try
        {
            BackgammonSettings.OpponentIsAi = true;
            var ctrl = go.AddComponent<BackgammonGameController>();
            SetPrivateField(ctrl, "<Match>k__BackingField", new MatchState());
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);
            ctrl.State.PlayerOnRoll = 0;

            IEnumerator aiTurn = InvokeNonPublicEnumerator(ctrl, "CoAiTurn");
            int guard = 0;
            while (aiTurn.MoveNext() && guard++ < 128)
            {
            }

            Assert.Less(guard, 128, "Expected AI coroutine to complete within guard limit.");
            Assert.IsFalse(ctrl.CanUndo, "Undo stack should clear when the AI turn completes.");
        }
        finally
        {
            BackgammonSettings.OpponentIsAi = true;
            Object.DestroyImmediate(go);
        }
    }

    private static IEnumerator InvokeNonPublicEnumerator(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' to exist.");
        object result = method.Invoke(target, args);
        Assert.That(result, Is.InstanceOf<IEnumerator>(), $"Expected '{methodName}' to return IEnumerator.");
        return (IEnumerator)result;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }
}
