using System.Reflection;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

/// <summary>When the AI (player 0) offers a double, the human must take/drop — do not run <c>CoAiRespondDouble</c>.</summary>
public class BackgammonDoubleOfferResponderEditModeTests
{
    [Test]
    public void OfferDouble_WhenOpponentOwnsCube_DoesNotStartDoubleOffer()
    {
        var go = new GameObject("BgDoubleOwnerDenied");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_openingRollResolved", true);
            SetPrivate(ctrl, "_rolledThisTurn", false);
            ctrl.State.PlayerOnRoll = 0;
            ctrl.State.CubeValue = 2;
            ctrl.State.CubeOwner = 1;

            ctrl.OfferDouble();

            Assert.IsFalse(ctrl.AwaitingDoubleResponse, "Double offer should be rejected when the opponent owns the cube.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OfferDouble_WhenCurrentPlayerOwnsCube_ButLocalDoesNot_OwnerRejectsOffer()
    {
        var go = new GameObject("BgDoubleCurrentOwnerNotLocal");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_openingRollResolved", true);
            SetPrivate(ctrl, "_rolledThisTurn", false);
            ctrl.State.PlayerOnRoll = 0;
            ctrl.State.CubeValue = 2;
            ctrl.State.CubeOwner = 0;

            ctrl.OfferDouble();

            Assert.IsFalse(ctrl.AwaitingDoubleResponse, "Double offer should be blocked when local player does not own the cube.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OfferDouble_WhenCubeCentered_StartsDoubleOffer()
    {
        var go = new GameObject("BgDoubleCenteredAllowed");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_openingRollResolved", true);
            SetPrivate(ctrl, "_rolledThisTurn", false);
            ctrl.State.PlayerOnRoll = 0;
            ctrl.State.CubeValue = 2;
            ctrl.State.CubeOwner = 3;

            ctrl.OfferDouble();

            Assert.IsTrue(ctrl.AwaitingDoubleResponse, "Double offer should be allowed when cube is centered.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OfferDouble_WhenLocalPlayerOwnsCube_StartsDoubleOffer()
    {
        var go = new GameObject("BgDoubleLocalOwnerAllowed");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_openingRollResolved", true);
            SetPrivate(ctrl, "_rolledThisTurn", false);
            ctrl.State.PlayerOnRoll = 1;
            ctrl.State.CubeValue = 2;
            ctrl.State.CubeOwner = 1;

            ctrl.OfferDouble();

            Assert.IsTrue(ctrl.AwaitingDoubleResponse, "Double offer should be allowed when local player owns the cube.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OfferDouble_WhenAiOnRoll_OpponentIsHuman_AwaitingDoubleRemainsForHumanResponse()
    {
        bool prevAi = BackgammonSettings.OpponentIsAi;
        var go = new GameObject("BgDoubleResponder");
        try
        {
            BackgammonSettings.OpponentIsAi = true;
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_openingRollResolved", true);
            SetPrivate(ctrl, "_rolledThisTurn", false);
            ctrl.State.PlayerOnRoll = 0;
            ctrl.State.CubeValue = 1;

            ctrl.OfferDouble();

            Assert.IsTrue(
                ctrl.AwaitingDoubleResponse,
                "Human must respond when AI offers; CoAiRespondDouble must not run for responder P1.");
        }
        finally
        {
            BackgammonSettings.OpponentIsAi = prevAi;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ShouldAiOfferDoubleBeforeRoll_WhenNoAiCubeApi_ReturnsFalse()
    {
        bool prevAi = BackgammonSettings.OpponentIsAi;
        var go = new GameObject("BgAiDoubleFallback");
        try
        {
            BackgammonSettings.OpponentIsAi = true;
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_openingRollResolved", true);
            SetPrivate(ctrl, "_rolledThisTurn", false);
            ctrl.State.PlayerOnRoll = 0;
            ctrl.State.CubeValue = 2;
            ctrl.State.CubeOwner = 3;

            bool shouldOffer = (bool)InvokePrivate(ctrl, "ShouldAiOfferDoubleBeforeRoll");

            Assert.IsFalse(shouldOffer, "AI should not offer when strict AI cube evaluator is unavailable.");
        }
        finally
        {
            BackgammonSettings.OpponentIsAi = prevAi;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void CoAiRespondDouble_WhenAiCubeEvaluatorUnavailable_DefaultsToTake()
    {
        var go = new GameObject("BgAiRespondDefaultTake");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_awaitingDoubleResponse", true);
            SetPrivate(ctrl, "_busy", false);
            SetPrivate(ctrl, "_doubleOfferedByPlayer", 0);
            ctrl.State.CubeValue = 2;
            ctrl.State.CubeOwner = 3;

            var routine = (System.Collections.IEnumerator)InvokePrivate(ctrl, "CoAiRespondDouble");
            while (routine.MoveNext()) { }

            Assert.IsFalse(ctrl.AwaitingDoubleResponse, "Response flow should complete.");
            Assert.AreEqual(4, ctrl.State.CubeValue, "Strict default should take and double cube value.");
            Assert.AreEqual(1, ctrl.State.CubeOwner, "Responder should own cube after taking.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ShouldAiOfferDoubleBeforeRoll_WhenCubeOwnedByOpponent_ReturnsFalse()
    {
        bool prevAi = BackgammonSettings.OpponentIsAi;
        var go = new GameObject("BgAiDoubleOwnerFalse");
        try
        {
            BackgammonSettings.OpponentIsAi = true;
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivate(ctrl, "_openingRollResolved", true);
            SetPrivate(ctrl, "_rolledThisTurn", false);
            ctrl.State.PlayerOnRoll = 0;
            ctrl.State.CubeValue = 2;
            ctrl.State.CubeOwner = 1;
            bool shouldOffer = (bool)InvokePrivate(ctrl, "ShouldAiOfferDoubleBeforeRoll");

            Assert.IsFalse(shouldOffer, "AI should not offer when it cannot legally offer due to cube ownership.");
        }
        finally
        {
            BackgammonSettings.OpponentIsAi = prevAi;
            Object.DestroyImmediate(go);
        }
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(f, Is.Not.Null, $"Expected field '{name}'.");
        f.SetValue(target, value);
    }

    private static object InvokePrivate(object target, string methodName)
    {
        MethodInfo m = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(m, Is.Not.Null, $"Expected method '{methodName}'.");
        return m.Invoke(target, null);
    }

}
