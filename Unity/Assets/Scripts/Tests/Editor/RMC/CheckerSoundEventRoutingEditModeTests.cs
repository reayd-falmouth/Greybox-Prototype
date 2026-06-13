using System.Reflection;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

public class CheckerSoundEventRoutingEditModeTests
{
    [Test]
    public void ClassifyCheckerSoundEventForAppliedMove_MapsExpectedEventTypes()
    {
        Assert.AreEqual(
            CheckerSoundEventType.Move,
            BackgammonGameController.ClassifyCheckerSoundEventForAppliedMove(new Move { From = 8, To = 5 }));

        Assert.AreEqual(
            CheckerSoundEventType.HitToBar,
            BackgammonGameController.ClassifyCheckerSoundEventForAppliedMove(new Move { From = 8, To = 5, IsHit = true }));

        Assert.AreEqual(
            CheckerSoundEventType.EnterFromBar,
            BackgammonGameController.ClassifyCheckerSoundEventForAppliedMove(new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 22 }));

        Assert.AreEqual(
            CheckerSoundEventType.BearOff,
            BackgammonGameController.ClassifyCheckerSoundEventForAppliedMove(new Move { From = 2, To = -1 }));
    }

    [Test]
    public void EmitCheckerSoundEventForUndo_RaisesUndoEvent()
    {
        var go = new GameObject("BackgammonGameController");
        var controller = go.AddComponent<BackgammonGameController>();
        CheckerSoundEventData captured = default;
        bool raised = false;
        controller.OnCheckerSoundEvent += evt =>
        {
            captured = evt;
            raised = true;
        };

        MethodInfo emitUndo = typeof(BackgammonGameController).GetMethod(
            "EmitCheckerSoundEventForUndo",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(emitUndo, "Expected private undo-emitter method to exist.");
        emitUndo.Invoke(controller, new object[] { new Move { From = 6, To = 5 } });

        Assert.IsTrue(raised);
        Assert.AreEqual(CheckerSoundEventType.Undo, captured.EventType);
        Assert.IsTrue(captured.IsUndo);
        Assert.AreEqual(6, captured.From);
        Assert.AreEqual(5, captured.To);
    }

    [Test]
    public void EmitCheckerSoundEventForAppliedMove_IsActionTypedNotPlayerTyped()
    {
        var go = new GameObject("BackgammonGameController_CheckerSound_ActionTyped");
        var controller = go.AddComponent<BackgammonGameController>();
        controller.NewGame();
        try
        {
            CheckerSoundEventData captured = default;
            bool raised = false;
            controller.OnCheckerSoundEvent += evt =>
            {
                captured = evt;
                raised = true;
            };

            MethodInfo emitApplied = typeof(BackgammonGameController).GetMethod(
                "EmitCheckerSoundEventForAppliedMove",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(emitApplied, "Expected private applied-move emitter method to exist.");

            controller.State.PlayerOnRoll = 0;
            emitApplied.Invoke(controller, new object[] { new Move { From = 8, To = 5, IsHit = false } });
            Assert.IsTrue(raised);
            Assert.AreEqual(CheckerSoundEventType.Move, captured.EventType);

            raised = false;
            controller.State.PlayerOnRoll = 1;
            emitApplied.Invoke(controller, new object[] { new Move { From = 8, To = 5, IsHit = false } });
            Assert.IsTrue(raised);
            Assert.AreEqual(CheckerSoundEventType.Move, captured.EventType);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void EmitCheckerSoundEventForAppliedMove_BarEntryHit_EmitsEnterThenHit()
    {
        var go = new GameObject("BackgammonGameController_CheckerSound_BarEntryHit");
        var controller = go.AddComponent<BackgammonGameController>();
        controller.NewGame();
        try
        {
            var captured = new System.Collections.Generic.List<CheckerSoundEventData>();
            controller.OnCheckerSoundEvent += evt => captured.Add(evt);

            MethodInfo emitApplied = typeof(BackgammonGameController).GetMethod(
                "EmitCheckerSoundEventForAppliedMove",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(emitApplied, "Expected private applied-move emitter method to exist.");

            emitApplied.Invoke(controller, new object[]
            {
                new Move
                {
                    From = BackgammonBoardLayout.BarEngineIndex,
                    To = 22,
                    IsHit = true
                }
            });

            Assert.AreEqual(2, captured.Count);
            Assert.AreEqual(CheckerSoundEventType.EnterFromBar, captured[0].EventType);
            Assert.AreEqual(CheckerSoundEventType.HitToBar, captured[1].EventType);
            Assert.IsFalse(captured[0].IsUndo);
            Assert.IsFalse(captured[1].IsUndo);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
