using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DiceFeedbackEventRoutingEditModeTests
{
    [Test]
    public void ResetBothDiceManagersBetweenTurns_RolledState_EmitsGeneralDiceResetPickup()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_GeneralReset");
        var dm0Go = new GameObject("DiceManager0");
        var dm1Go = new GameObject("DiceManager1");
        var controller = go.AddComponent<BackgammonGameController>();
        var dm0 = dm0Go.AddComponent<Runtime.RMC._MyProject_.Dice.DiceManager>();
        var dm1 = dm1Go.AddComponent<Runtime.RMC._MyProject_.Dice.DiceManager>();
        try
        {
            SetPrivateField(controller, "diceManagerPlayer0", dm0);
            SetPrivateField(controller, "diceManagerPlayer1", dm1);
            SetPrivateField(controller, "_rolledThisTurn", true);

            DiceFeedbackEventData captured = default;
            bool raised = false;
            controller.OnDiceFeedbackEvent += evt =>
            {
                if (evt.EventType != DiceFeedbackEventType.GeneralDiceResetPickup) return;
                captured = evt;
                raised = true;
            };

            MethodInfo resetBetweenTurns = typeof(BackgammonGameController).GetMethod(
                "ResetBothDiceManagersBetweenTurns",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(resetBetweenTurns, "Expected private ResetBothDiceManagersBetweenTurns method to exist.");
            resetBetweenTurns.Invoke(controller, new object[] { "test", true });

            Assert.IsTrue(raised);
            Assert.AreEqual(DiceFeedbackEventType.GeneralDiceResetPickup, captured.EventType);
        }
        finally
        {
            Object.DestroyImmediate(dm0Go);
            Object.DestroyImmediate(dm1Go);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void FinalizeTurnAndAdvance_EmitsGeneralDiceResetPickup_WhenRolledThisTurn()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_FinalizeTurn");
        var dm0Go = new GameObject("DiceManager0");
        var dm1Go = new GameObject("DiceManager1");
        var controller = go.AddComponent<BackgammonGameController>();
        var dm0 = dm0Go.AddComponent<Runtime.RMC._MyProject_.Dice.DiceManager>();
        var dm1 = dm1Go.AddComponent<Runtime.RMC._MyProject_.Dice.DiceManager>();
        controller.NewGame();
        try
        {
            SetPrivateField(controller, "diceManagerPlayer0", dm0);
            SetPrivateField(controller, "diceManagerPlayer1", dm1);
            SetPrivateField(controller, "_rolledThisTurn", true);

            bool raised = false;
            controller.OnDiceFeedbackEvent += evt =>
            {
                if (evt.EventType == DiceFeedbackEventType.GeneralDiceResetPickup)
                    raised = true;
            };

            MethodInfo finalize = typeof(BackgammonGameController).GetMethod(
                "FinalizeTurnAndAdvance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(finalize, "Expected private FinalizeTurnAndAdvance method to exist.");
            finalize.Invoke(controller, null);

            Assert.IsTrue(raised, "Expected reset pickup feedback event after finalize turn.");
        }
        finally
        {
            Object.DestroyImmediate(dm0Go);
            Object.DestroyImmediate(dm1Go);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void EmitDiceFeedbackEvent_RaisesWithOpeningRollTieAutodoublePayload()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback");
        var controller = go.AddComponent<BackgammonGameController>();
        DiceFeedbackEventData captured = default;
        bool raised = false;
        controller.OnDiceFeedbackEvent += evt =>
        {
            captured = evt;
            raised = true;
        };

        MethodInfo emit = typeof(BackgammonGameController).GetMethod(
            "EmitDiceFeedbackEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(emit, "Expected private EmitDiceFeedbackEvent method to exist.");
        emit.Invoke(
            controller,
            new object[]
            {
                new DiceFeedbackEventData(DiceFeedbackEventType.OpeningRollTieAutodouble, cubeValueAfter: 2, openingDiePlayer0: 3, openingDiePlayer1: 3)
            });

        Assert.IsTrue(raised);
        Assert.AreEqual(DiceFeedbackEventType.OpeningRollTieAutodouble, captured.EventType);
        Assert.AreEqual(2, captured.CubeValueAfter);
        Assert.AreEqual(3, captured.OpeningDiePlayer0);
        Assert.AreEqual(3, captured.OpeningDiePlayer1);
    }

    [Test]
    public void EmitDiceFeedbackEvent_RaisesWithOpeningRollTieDiceResetPickupPayload()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_ResetPickup");
        var controller = go.AddComponent<BackgammonGameController>();
        DiceFeedbackEventData captured = default;
        bool raised = false;
        controller.OnDiceFeedbackEvent += evt =>
        {
            captured = evt;
            raised = true;
        };

        MethodInfo emit = typeof(BackgammonGameController).GetMethod(
            "EmitDiceFeedbackEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(emit, "Expected private EmitDiceFeedbackEvent method to exist.");
        emit.Invoke(
            controller,
            new object[]
            {
                new DiceFeedbackEventData(DiceFeedbackEventType.OpeningRollTieDiceResetPickup, cubeValueAfter: 4, openingDiePlayer0: 6, openingDiePlayer1: 6)
            });

        Assert.IsTrue(raised);
        Assert.AreEqual(DiceFeedbackEventType.OpeningRollTieDiceResetPickup, captured.EventType);
        Assert.AreEqual(4, captured.CubeValueAfter);
        Assert.AreEqual(6, captured.OpeningDiePlayer0);
        Assert.AreEqual(6, captured.OpeningDiePlayer1);
    }

    [Test]
    public void ApplyOpeningRollFromDice_NonTie_EmitsOpeningRollWinnerResolved()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_WinnerEmit");
        var controller = go.AddComponent<BackgammonGameController>();
        controller.NewGame();
        DiceFeedbackEventData captured = default;
        bool raised = false;
        controller.OnDiceFeedbackEvent += evt =>
        {
            if (evt.EventType != DiceFeedbackEventType.OpeningRollWinnerResolved) return;
            captured = evt;
            raised = true;
        };

        MethodInfo applyOpening = typeof(BackgammonGameController).GetMethod(
            "ApplyOpeningRollFromDice",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(applyOpening, "Expected private opening-roll method to exist.");
        applyOpening.Invoke(controller, new object[] { 2, 5 });

        Assert.IsTrue(raised);
        Assert.AreEqual(DiceFeedbackEventType.OpeningRollWinnerResolved, captured.EventType);
        Assert.AreEqual(2, captured.OpeningDiePlayer0);
        Assert.AreEqual(5, captured.OpeningDiePlayer1);
        Assert.AreEqual(1, captured.OpeningRollWinnerPlayerIndex);
    }

    [Test]
    public void ApplyOpeningRollFromDice_Tie_DoesNotEmitOpeningRollWinnerResolved()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_NoWinnerOnTie");
        var controller = go.AddComponent<BackgammonGameController>();
        controller.NewGame();
        bool raised = false;
        controller.OnDiceFeedbackEvent += evt =>
        {
            if (evt.EventType == DiceFeedbackEventType.OpeningRollWinnerResolved)
                raised = true;
        };

        MethodInfo applyOpening = typeof(BackgammonGameController).GetMethod(
            "ApplyOpeningRollFromDice",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(applyOpening, "Expected private opening-roll method to exist.");
        applyOpening.Invoke(controller, new object[] { 4, 4 });

        Assert.IsFalse(raised);
    }

    [Test]
    public void ApplyOpeningRollFromDice_Tie_QueuesScreenNotificationAfterAutoDoubleBeforeResetPickup()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_QueueOrdering");
        var controller = go.AddComponent<BackgammonGameController>();
        controller.NewGame();
        try
        {
            var timeline = new List<string>();
            controller.OnDiceFeedbackEvent += evt => timeline.Add($"dice:{evt.EventType}");
            controller.OnScreenNotificationEvent += evt => timeline.Add($"notify:{evt.EventType}");

            MethodInfo applyOpening = typeof(BackgammonGameController).GetMethod(
                "ApplyOpeningRollFromDice",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(applyOpening, "Expected private opening-roll method to exist.");
            applyOpening.Invoke(controller, new object[] { 4, 4 });

            TickPresentationQueueToDrain(controller);

            int autoDoubleIdx = timeline.IndexOf("dice:OpeningRollTieAutodouble");
            int notifyIdx = timeline.IndexOf("notify:OpeningRollTieAutodouble");
            int resetPickupIdx = timeline.IndexOf("dice:OpeningRollTieDiceResetPickup");
            Assert.That(autoDoubleIdx, Is.GreaterThanOrEqualTo(0));
            Assert.That(notifyIdx, Is.GreaterThan(autoDoubleIdx));
            Assert.That(resetPickupIdx, Is.GreaterThan(notifyIdx));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RespondDoubleTake_QueuesCubeValueChangedScreenNotification()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_DoubleTakeNotify");
        var controller = go.AddComponent<BackgammonGameController>();
        controller.NewGame();
        try
        {
            SetPrivateField(controller, "_awaitingDoubleResponse", true);
            SetPrivateField(controller, "_doubleOfferedByPlayer", 0);
            bool notified = false;
            controller.OnScreenNotificationEvent += evt =>
            {
                if (evt.EventType == DiceFeedbackEventType.CubeValueChanged)
                    notified = true;
            };

            controller.RespondDoubleTake();
            TickPresentationQueueToDrain(controller);

            Assert.IsTrue(notified, "Expected queued screen notification event for cube value change.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RespondDoubleDrop_QueuesGameEndedNotificationAndDoesNotImmediateReset()
    {
        var go = new GameObject("BackgammonGameController_DiceFeedback_DoubleDropNotify");
        var controller = go.AddComponent<BackgammonGameController>();
        controller.NewGame();
        try
        {
            SetPrivateField(controller, "_awaitingDoubleResponse", true);
            SetPrivateField(controller, "_doubleOfferedByPlayer", 0);
            controller.State.CubeValue = 8;
            int startRoundIndex = controller.GameRoundIndex;
            DiceFeedbackEventData captured = default;
            bool notified = false;
            controller.OnScreenNotificationEvent += evt =>
            {
                if (evt.EventType != DiceFeedbackEventType.GameEnded)
                    return;
                captured = evt;
                notified = true;
            };

            controller.RespondDoubleDrop();
            TickPresentationQueueToDrain(controller);

            Assert.IsTrue(notified, "Expected queued game-ended notification for drop.");
            Assert.AreEqual(DiceFeedbackEventType.GameEnded, captured.EventType);
            Assert.AreEqual(0, captured.GameWinnerPlayerIndex);
            Assert.AreEqual(8, captured.GamePointsAwarded, "Drop awards single game times cube.");
            Assert.AreEqual("DoubleDrop", captured.GameEndReason);
            Assert.AreEqual(startRoundIndex, controller.GameRoundIndex, "Drop should not auto-start a new game immediately.");
            Assert.IsTrue(controller.IsGameOver(out _), "Drop should set terminal game-over state.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TickPresentationQueueToDrain(BackgammonGameController controller)
    {
        FieldInfo queueField = typeof(BackgammonGameController).GetField(
            "_presentationEventQueue",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(queueField, Is.Not.Null, "Expected presentation queue field.");
        var queue = queueField.GetValue(controller) as BackgammonEventQueue;
        Assert.That(queue, Is.Not.Null, "Expected presentation queue to exist.");

        int safety = 0;
        while (queue.PendingCount > 0 && safety < 64)
        {
            queue.SetGameSpeedMultiplier(1f);
            queue.Tick(1f);
            safety++;
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }
}
