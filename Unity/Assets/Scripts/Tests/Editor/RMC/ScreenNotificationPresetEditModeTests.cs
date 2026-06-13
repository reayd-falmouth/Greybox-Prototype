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
            null,
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
            null,
            out ScreenNotificationController.NotificationPresetResolved r);

        Assert.IsTrue(ok);
        Assert.AreEqual(99, r.ResolvedFontSize);
        Assert.AreEqual(new Vector2(3f, 4f), r.ResolvedLabelOffsetPixels);
    }

    [Test]
    public void TryResolvePreset_UsesAudioClipOverride()
    {
        var clip = AudioClip.Create("TestClip", 1, 1, 44100, false);
        var list = new List<ScreenNotificationController.DiceFeedbackNotificationEntry>
        {
            new ScreenNotificationController.DiceFeedbackNotificationEntry
            {
                eventType = DiceFeedbackEventType.OpeningRollTieAutodouble,
                message = "Hi",
                displayDurationSeconds = 0f,
                fontSize = 0,
                labelOffsetPixels = Vector2.zero,
                audioClipOverride = clip
            }
        };

        bool ok = ScreenNotificationController.TryResolvePreset(
            list,
            DiceFeedbackEventType.OpeningRollTieAutodouble,
            72,
            Vector2.zero,
            null,
            out ScreenNotificationController.NotificationPresetResolved r);

        Assert.IsTrue(ok);
        Assert.AreEqual(clip, r.ResolvedAudioClip);
    }

    [Test]
    public void TryResolvePreset_UsesDefaultAudioClip_WhenOverrideIsNull()
    {
        var defaultClip = AudioClip.Create("DefaultClip", 1, 1, 44100, false);
        var list = new List<ScreenNotificationController.DiceFeedbackNotificationEntry>
        {
            new ScreenNotificationController.DiceFeedbackNotificationEntry
            {
                eventType = DiceFeedbackEventType.OpeningRollTieAutodouble,
                message = "Hi",
                displayDurationSeconds = 0f,
                fontSize = 0,
                labelOffsetPixels = Vector2.zero,
                audioClipOverride = null
            }
        };

        bool ok = ScreenNotificationController.TryResolvePreset(
            list,
            DiceFeedbackEventType.OpeningRollTieAutodouble,
            72,
            Vector2.zero,
            defaultClip,
            out ScreenNotificationController.NotificationPresetResolved r);

        Assert.IsTrue(ok);
        Assert.AreEqual(defaultClip, r.ResolvedAudioClip);
    }

    [Test]
    public void ResolveOpeningRollWinnerMessage_PlayerWinner_UsesPlayerMessage()
    {
        string msg = ScreenNotificationController.ResolveOpeningRollWinnerMessage(1, "Your go", "AI goes first");
        Assert.AreEqual("Your go", msg);
    }

    [Test]
    public void ResolveOpeningRollWinnerMessage_AiWinner_UsesAiMessage()
    {
        string msg = ScreenNotificationController.ResolveOpeningRollWinnerMessage(0, "Your go", "AI goes first");
        Assert.AreEqual("AI goes first", msg);
    }

    [Test]
    public void IsQueueOwnedNotificationEvent_ReturnsTrue_ForCubeEvents()
    {
        Assert.IsTrue(ScreenNotificationController.IsQueueOwnedNotificationEvent(DiceFeedbackEventType.OpeningRollTieAutodouble));
        Assert.IsTrue(ScreenNotificationController.IsQueueOwnedNotificationEvent(DiceFeedbackEventType.CubeValueChanged));
        Assert.IsTrue(ScreenNotificationController.IsQueueOwnedNotificationEvent(DiceFeedbackEventType.GameEnded));
        Assert.IsFalse(ScreenNotificationController.IsQueueOwnedNotificationEvent(DiceFeedbackEventType.OpeningRollWinnerResolved));
    }
}
