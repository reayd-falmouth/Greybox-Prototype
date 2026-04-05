using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Board sync spawns checkers with <see cref="BoardPoint.AddChecker(GameObject, bool)"/> animated:false
/// so they are not left at world origin while a coroutine runs (edit mode has no advancing play loop).
/// </summary>
public class CheckerInstantPlacementEditModeTests
{
    [Test]
    public void AddChecker_NotAnimated_PlacesAtStackWorldPositionImmediately()
    {
        var pointGo = new GameObject("BoardPoint");
        pointGo.transform.position = new Vector3(7f, 0.2f, -3f);
        var bp = pointGo.AddComponent<BoardPoint>();
        bp.Initialize(0, true, Color.gray, 0.05f, 0.45f);

        var checkerGo = new GameObject("Checker");
        checkerGo.transform.position = Vector3.zero;
        checkerGo.AddComponent<Checker>();

        Vector3 expected = bp.GetPositionForIndex(0);
        bp.AddChecker(checkerGo, animated: false);

        Assert.Greater(Vector3.Distance(checkerGo.transform.position, Vector3.zero), 1f,
            "Should not remain at world origin after instant placement.");
        Assert.Less(Vector3.Distance(checkerGo.transform.position, expected), 0.02f,
            "World position should match stack slot 0.");
        Assert.AreSame(bp.transform, checkerGo.transform.parent);
    }
}
