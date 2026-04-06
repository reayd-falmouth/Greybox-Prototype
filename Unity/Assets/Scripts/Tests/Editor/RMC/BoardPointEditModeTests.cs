using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <see cref="BoardPoint"/> stack direction follows point <see cref="Transform.forward"/> so Horiz/Vert board rotation stays consistent.
/// </summary>
public class BoardPointEditModeTests
{
    [Test]
    public void BoardPoint_InwardDirectionWorld_BottomRow_MatchesTransformForward()
    {
        var go = new GameObject("BoardPointTest");
        try
        {
            var bp = go.AddComponent<BoardPoint>();
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            bp.Initialize(0, true, Color.white, 0.05f, 0.45f);

            Vector3 expected = go.transform.forward;
            Assert.Less(Vector3.Distance(bp.InwardDirectionWorld, expected), 1e-5f,
                "Bottom row inward should align with transform.forward.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BoardPoint_InwardDirectionWorld_TopRow_OpposesTransformForward()
    {
        var go = new GameObject("BoardPointTest");
        try
        {
            var bp = go.AddComponent<BoardPoint>();
            go.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
            bp.Initialize(12, false, Color.white, 0.05f, 0.45f);

            Vector3 expected = -go.transform.forward;
            Assert.Less(Vector3.Distance(bp.InwardDirectionWorld, expected), 1e-5f,
                "Top row inward should align with -transform.forward.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BoardPoint_GetPositionForIndex_StackAlongInwardAfterRotation()
    {
        var go = new GameObject("BoardPointTest");
        try
        {
            var bp = go.AddComponent<BoardPoint>();
            go.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            bp.Initialize(0, true, Color.white, 0.05f, 0.45f);

            Vector3 p0 = bp.GetPositionForIndex(0);
            Vector3 p1 = bp.GetPositionForIndex(1);
            Vector3 delta = (p1 - p0).normalized;
            Vector3 inward = bp.InwardDirectionWorld.normalized;
            Assert.Less(Vector3.Distance(delta, inward), 0.02f,
                "Consecutive stack positions should lie along InwardDirectionWorld.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
