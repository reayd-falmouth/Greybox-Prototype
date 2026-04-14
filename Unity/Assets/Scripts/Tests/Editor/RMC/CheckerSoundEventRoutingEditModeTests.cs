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
}
