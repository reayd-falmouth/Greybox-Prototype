using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

public class BackgammonMovePreviewCurveEditModeTests
{
    [Test]
    public void FillQuadraticBezier_ZeroArc_IsStraightAlongChord()
    {
        var start = new Vector3(0f, 0f, 0f);
        var end = new Vector3(4f, 0f, 0f);
        Vector3 control = BackgammonMovePreviewCurve.GetControlPoint(start, end, 0f, 0f);
        var buf = new Vector3[32];
        int written = BackgammonMovePreviewCurve.FillQuadraticBezier(start, control, end, buf, 8);
        Assert.AreEqual(9, written);
        Assert.AreEqual(Vector3.Lerp(start, end, 0.5f).x, buf[4].x, 1e-4f);
        Assert.AreEqual(Vector3.Lerp(start, end, 0.5f).y, buf[4].y, 1e-4f);
    }

    [Test]
    public void FillQuadraticBezier_ArcHeight_PlacesMidpointAtExpectedHeight()
    {
        var start = new Vector3(0f, 0f, 0f);
        var end = new Vector3(2f, 0f, 0f);
        Vector3 control = BackgammonMovePreviewCurve.GetControlPoint(start, end, 1f, 0f);
        var buf = new Vector3[32];
        BackgammonMovePreviewCurve.FillQuadraticBezier(start, control, end, buf, 4);
        // t=0.5 → (1, 0.5, 0) for symmetric control (1,1,0)
        Assert.AreEqual(1f, buf[2].x, 1e-4f);
        Assert.AreEqual(0.5f, buf[2].y, 1e-4f);
    }

    [Test]
    public void FillQuadraticBezier_BufferTooSmall_ReturnsZero()
    {
        var buf = new Vector3[2];
        int written = BackgammonMovePreviewCurve.FillQuadraticBezier(
            Vector3.zero, Vector3.up, Vector3.right, buf, 8);
        Assert.AreEqual(0, written);
    }
}
