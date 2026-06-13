using NUnit.Framework;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

public class BoardManagerCheckerSourceResolutionEditModeTests
{
    [SetUp]
    public void SetUp()
    {
        BackgammonBoardLayout.SetHorizontal(true);
    }

    [Test]
    public void TryGetEngineFromForChecker_BarWhitePointChecker_ReturnsBarEngineIndex()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.barWhitePoint = CreatePoint("BarWhitePoint", BackgammonBoardLayout.BarEngineIndex);

        var checkerGo = new GameObject("WhiteBarChecker");
        var checker = checkerGo.AddComponent<Checker>();
        checker.color = PlayerColor.White;
        manager.barWhitePoint.AddChecker(checkerGo, animated: false);

        bool ok = manager.TryGetEngineFromForChecker(checker, out int engineFrom);

        Assert.IsTrue(ok);
        Assert.AreEqual(BackgammonBoardLayout.BarEngineIndex, engineFrom);
    }

    [Test]
    public void TryGetEngineFromForChecker_BarWhiteAnchorChecker_ReturnsBarEngineIndex()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.barWhiteAnchor = new GameObject("BarWhiteAnchor").transform;

        var checkerGo = new GameObject("WhiteBarAnchorChecker");
        var checker = checkerGo.AddComponent<Checker>();
        checker.color = PlayerColor.White;
        checkerGo.transform.SetParent(manager.barWhiteAnchor, false);

        bool ok = manager.TryGetEngineFromForChecker(checker, out int engineFrom);

        Assert.IsTrue(ok);
        Assert.AreEqual(BackgammonBoardLayout.BarEngineIndex, engineFrom);
    }

    [Test]
    public void TryGetEngineFromForChecker_NormalBoardPointChecker_UsesBoardMapping()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        const int boardPointIndex = 6;
        var point = CreatePoint("Point6", boardPointIndex);

        var checkerGo = new GameObject("PointChecker");
        var checker = checkerGo.AddComponent<Checker>();
        checker.color = PlayerColor.White;
        point.AddChecker(checkerGo, animated: false);

        bool ok = manager.TryGetEngineFromForChecker(checker, out int engineFrom);

        Assert.IsTrue(ok);
        Assert.AreEqual(BackgammonBoardLayout.BoardIndexToEnginePoint(boardPointIndex), engineFrom);
    }

    private static BoardPoint CreatePoint(string name, int pointIndex)
    {
        var pointGo = new GameObject(name);
        var point = pointGo.AddComponent<BoardPoint>();
        point.pointRenderer = pointGo.AddComponent<MeshRenderer>();
        point.Initialize(pointIndex, true, Color.gray, 0.1f, 0.5f);
        return point;
    }
}
