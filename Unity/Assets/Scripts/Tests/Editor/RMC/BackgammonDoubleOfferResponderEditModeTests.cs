using System.Reflection;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

/// <summary>When the AI (player 0) offers a double, the human must take/drop — do not run <c>CoAiRespondDouble</c>.</summary>
public class BackgammonDoubleOfferResponderEditModeTests
{
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

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(f, Is.Not.Null, $"Expected field '{name}'.");
        f.SetValue(target, value);
    }
}
