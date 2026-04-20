using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DiceFeedbackEventRoutingEditModeTests
{
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
}
