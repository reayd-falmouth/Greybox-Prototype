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

    [Test]
    public void FillMoveVisualizerStyleBezier_WritesExpectedCount()
    {
        var start = new Vector3(0f, 0f, 0f);
        var end = new Vector3(4f, 0f, 0f);
        var buf = new Vector3[32];
        int written = BackgammonMovePreviewCurve.FillMoveVisualizerStyleBezier(
            start, end, 0.5f, 8, 0, Vector3.up, buf);
        Assert.AreEqual(9, written);
    }

    [Test]
    public void FillMoveVisualizerStyleBezier_BufferTooSmall_ReturnsZero()
    {
        var buf = new Vector3[2];
        int written = BackgammonMovePreviewCurve.FillMoveVisualizerStyleBezier(
            Vector3.zero, Vector3.right, 1f, 8, 0, Vector3.up, buf);
        Assert.AreEqual(0, written);
    }

    [Test]
    public void FillChord_WritesEndpoints()
    {
        var buf = new Vector3[4];
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);
        int n = BackgammonMovePreviewCurve.FillChord(a, b, buf);
        Assert.AreEqual(2, n);
        Assert.AreEqual(a, buf[0]);
        Assert.AreEqual(b, buf[1]);
    }

    [Test]
    public void FillChord_BufferTooSmall_ReturnsZero()
    {
        var buf = new Vector3[1];
        int n = BackgammonMovePreviewCurve.FillChord(Vector3.zero, Vector3.one, buf);
        Assert.AreEqual(0, n);
    }

    [Test]
    public void GetMoveVisualizerStyleControlPoint_FanIndexAlternatesSideAlongX()
    {
        var p0 = Vector3.zero;
        var p2 = new Vector3(4f, 0f, 0f);
        Vector3 up = Vector3.up;
        Vector3 c0 = BackgammonMovePreviewCurve.GetMoveVisualizerStyleControlPoint(p0, p2, 0.25f, 0, up);
        Vector3 c1 = BackgammonMovePreviewCurve.GetMoveVisualizerStyleControlPoint(p0, p2, 0.25f, 1, up);
        // fanIndex 0 vs 1 should offset control in opposite directions along Z for chord along X
        Assert.That(Mathf.Abs(c0.z - c1.z), Is.GreaterThan(1e-4f));
    }

    [Test]
    public void GetMoveVisualizerStyleControlPoint_VerticalBulgeOffsetsAlongPlaneNormal()
    {
        var p0 = Vector3.zero;
        var p2 = new Vector3(4f, 0f, 0f);
        Vector3 planeNormal = Vector3.up;
        Vector3 flat = BackgammonMovePreviewCurve.GetMoveVisualizerStyleControlPoint(
            p0, p2, 0f, 0, planeNormal, verticalBulgeFactor: 0f);
        Vector3 lifted = BackgammonMovePreviewCurve.GetMoveVisualizerStyleControlPoint(
            p0, p2, 0f, 0, planeNormal, verticalBulgeFactor: 0.2f);
        float dist = (p2 - p0).magnitude;
        Assert.AreEqual(flat.y + dist * 0.2f, lifted.y, 1e-4f);
        Assert.AreEqual(flat.x, lifted.x, 1e-4f);
        Assert.AreEqual(flat.z, lifted.z, 1e-4f);
    }
}
