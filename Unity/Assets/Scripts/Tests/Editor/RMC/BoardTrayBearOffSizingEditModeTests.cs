using NUnit.Framework;
using UnityEngine;

public class BoardTrayBearOffSizingEditModeTests
{
    [Test]
    public void ComputeTrayInnerGap_AddsSmallClearanceToCheckerWidth()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();

        const float checkerWidth = 0.04545455f;
        float computed = manager.ComputeTrayInnerGap(checkerWidth);

        Assert.Greater(computed, checkerWidth);
        Assert.AreEqual(0.05345455f, computed, 0.000001f);
    }

    [Test]
    public void GetTrayBearOffStackStepY_MatchesExpectedHalfCheckerThicknessStep()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();

        Assert.AreEqual(0.02272728f, manager.GetTrayBearOffStackStepY(), 0.0000001f);
    }
}
