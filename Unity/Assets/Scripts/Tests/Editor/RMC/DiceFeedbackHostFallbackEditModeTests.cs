using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class DiceFeedbackHostFallbackEditModeTests
{
    private class TestFeedbackPlayer : MonoBehaviour
    {
        public bool Played { get; private set; }
        public void PlayFeedbacks()
        {
            Played = true;
        }
    }

    [Test]
    public void TryPlay_GeneralReset_FallsBackToOpeningResetSlot()
    {
        var go = new GameObject("DiceFeedbackHostFallback");
        var host = go.AddComponent<DiceFeedbackHost>();
        var player = go.AddComponent<TestFeedbackPlayer>();

        try
        {
            var slotType = typeof(DiceFeedbackHost).GetNestedType("FeedbackSlot", BindingFlags.Public);
            Assert.IsNotNull(slotType);
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("eventType").SetValue(slot, DiceFeedbackEventType.OpeningRollTieDiceResetPickup);
            slotType.GetField("feedbackPlayer").SetValue(slot, player);

            IList list = (IList)System.Activator.CreateInstance(typeof(List<>).MakeGenericType(slotType));
            list.Add(slot);

            FieldInfo slotsField = typeof(DiceFeedbackHost).GetField("feedbackSlots", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(slotsField);
            slotsField.SetValue(host, list);

            bool played = host.TryPlay(DiceFeedbackEventType.GeneralDiceResetPickup);
            Assert.IsTrue(played);
            Assert.IsTrue(player.Played);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
