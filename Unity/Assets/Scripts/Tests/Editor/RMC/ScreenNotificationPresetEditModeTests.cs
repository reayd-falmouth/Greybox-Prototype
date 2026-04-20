using System.Collections.Generic;
using NUnit.Framework;

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
                displayDurationSeconds = 0f
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
}
