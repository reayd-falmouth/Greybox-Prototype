using EngineCore;
using NUnit.Framework;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

public class BoardManagerApplySingleVisualMoveEditModeTests
{
    [Test]
    public void TryApplySingleVisualMove_PointToPoint_MovesTopChecker()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;

        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(5);
        BoardPoint fromPoint = CreatePoint("FromPoint");
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[fromBoardIndex] = fromPoint;
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("MovingWhite", PlayerColor.White);
        fromPoint.AddChecker(moving, animated: false);

        bool ok = manager.TryApplySingleVisualMove(new Move { From = 6, To = 5 });

        Assert.IsTrue(ok);
        Assert.AreEqual(0, fromPoint.checkers.Count);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(moving, toPoint.checkers[0]);
        Assert.AreSame(toPoint.transform, moving.transform.parent);
    }

    [Test]
    public void TryApplySingleVisualMove_HitBlot_MovesOpponentToBar()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;

        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(5);
        BoardPoint fromPoint = CreatePoint("FromPoint");
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[fromBoardIndex] = fromPoint;
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("MovingWhite", PlayerColor.White);
        GameObject blot = CreateChecker("HitBlack", PlayerColor.Black);
        fromPoint.AddChecker(moving, animated: false);
        toPoint.AddChecker(blot, animated: false);

        bool ok = manager.TryApplySingleVisualMove(new Move { From = 6, To = 5 });

        Assert.IsTrue(ok);
        Assert.AreEqual(0, fromPoint.checkers.Count);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(moving, toPoint.checkers[0]);
        Assert.AreEqual(1, manager.barBlackAnchor.childCount);
        Assert.AreSame(blot, manager.barBlackAnchor.GetChild(0).gameObject);
    }

    [Test]
    public void TryApplySingleVisualMove_BearOff_ReturnsFalse()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];

        bool ok = manager.TryApplySingleVisualMove(new Move { From = 6, To = -1 });

        Assert.IsFalse(ok);
    }

    private static BoardPoint CreatePoint(string name)
    {
        var pointGo = new GameObject(name);
        var point = pointGo.AddComponent<BoardPoint>();
        point.pointRenderer = pointGo.AddComponent<MeshRenderer>();
        point.Initialize(0, true, Color.gray, 0.1f, 0.5f);
        return point;
    }

    private static GameObject CreateChecker(string name, PlayerColor color)
    {
        var checker = new GameObject(name);
        checker.AddComponent<Checker>().color = color;
        return checker;
    }
}
