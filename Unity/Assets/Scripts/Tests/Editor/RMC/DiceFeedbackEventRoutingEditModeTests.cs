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
}
