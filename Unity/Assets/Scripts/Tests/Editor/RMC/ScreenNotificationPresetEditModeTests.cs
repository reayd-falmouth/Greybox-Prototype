using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ScreenNotificationPresetEditModeTests
{
    [Test]
    public void TryResolveMessage_ReturnsAutoDouble_ForOpeningRollTieAutodouble()
    {
        var list = new List<ScreenNotificationController.DiceFeedbackNotificationEntry>
        {
            new ScreenNotificationController.DiceFeedbackNotificationEntry
            {
                eventType = DiceFeedbackEventType.OpeningRollTieAutodouble,
                message = "AutoDouble!",
                displayDurationSeconds = 0f,
                fontSize = 0,
                labelOffsetPixels = Vector2.zero
            }
        };

        bool ok = ScreenNotificationController.TryResolveMessage(
            list,
            DiceFeedbackEventType.OpeningRollTieAutodouble,
            out string msg,
            out float dur);

        Assert.IsTrue(ok);
        Assert.AreEqual("AutoDouble!", msg);
        Assert.AreEqual(0f, dur);
    }

    [Test]
    public void TryResolvePreset_UsesExplicitFontAndOffset()
    {
        var list = new List<ScreenNotificationController.DiceFeedbackNotificationEntry>
        {
            new ScreenNotificationController.DiceFeedbackNotificationEntry
            {
                eventType = DiceFeedbackEventType.OpeningRollTieAutodouble,
                message = "Hi",
                displayDurationSeconds = 0f,
                fontSize = 48,
                labelOffsetPixels = new Vector2(12f, -8f)
            }
        };

        bool ok = ScreenNotificationController.TryResolvePreset(
            list,
            DiceFeedbackEventType.OpeningRollTieAutodouble,
            72,
            Vector2.zero,
            out ScreenNotificationController.NotificationPresetResolved r);

        Assert.IsTrue(ok);
        Assert.AreEqual("Hi", r.Message);
        Assert.AreEqual(48, r.ResolvedFontSize);
        Assert.AreEqual(new Vector2(12f, -8f), r.ResolvedLabelOffsetPixels);
    }

    [Test]
    public void TryResolvePreset_UsesDefaultsWhenFontZeroAndOffsetZero()
    {
        var list = new List<ScreenNotificationController.DiceFeedbackNotificationEntry>
        {
            new ScreenNotificationController.DiceFeedbackNotificationEntry
            {
                eventType = DiceFeedbackEventType.OpeningRollTieAutodouble,
                message = "Hi",
                displayDurationSeconds = 0f,
                fontSize = 0,
                labelOffsetPixels = Vector2.zero
            }
        };

        bool ok = ScreenNotificationController.TryResolvePreset(
            list,
            DiceFeedbackEventType.OpeningRollTieAutodouble,
            99,
            new Vector2(3f, 4f),
            out ScreenNotificationController.NotificationPresetResolved r);

        Assert.IsTrue(ok);
        Assert.AreEqual(99, r.ResolvedFontSize);
        Assert.AreEqual(new Vector2(3f, 4f), r.ResolvedLabelOffsetPixels);
    }
}
