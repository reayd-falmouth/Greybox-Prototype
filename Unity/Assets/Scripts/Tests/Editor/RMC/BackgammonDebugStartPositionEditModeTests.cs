using EngineCore;
using NUnit.Framework;
using UnityEngine;

public class BackgammonDebugStartPositionEditModeTests
{
    [Test]
    public void TryStartFromPositionId_ValidPid_UpdatesControllerState()
    {
        var go = new GameObject("DebugStartPositionTest");
        var controller = go.AddComponent<BackgammonGameController>();

        const string pid = "n3sDAIB3mwEADA";
        bool ok = controller.TryStartFromPositionId(pid);

        Assert.IsTrue(ok);
        Assert.IsNotNull(controller.State);
        Assert.AreEqual(pid, PositionId.Encode(controller.State));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TryStartFromPositionId_InvalidPid_ReturnsFalse()
    {
        var go = new GameObject("DebugStartInvalidPidTest");
        var controller = go.AddComponent<BackgammonGameController>();

        bool ok = controller.TryStartFromPositionId("not-a-valid-position-id");

        Assert.IsFalse(ok);

        Object.DestroyImmediate(go);
    }
}
