using EngineCore;
using NUnit.Framework;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

public class BoardManagerVisualUndoHitEditModeTests
{
    [SetUp]
    public void SetUp()
    {
        BackgammonBoardLayout.SetHorizontal(true);
    }

    [Test]
    public void TryApplySingleVisualUndoMove_HitMove_RestoresCapturedCheckerFromBarPoint()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;
        manager.barBlackPoint = CreatePoint("BarBlackPoint");

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

        bool applied = manager.TryApplySingleVisualMove(new Move { From = 6, To = 5, IsHit = true }, PlayerColor.White);
        Assert.IsTrue(applied);
        Assert.AreEqual(1, manager.barBlackPoint.checkers.Count);

        bool undone = manager.TryApplySingleVisualUndoMove(new Move { From = 6, To = 5, IsHit = true });
        Assert.IsTrue(undone);

        Assert.AreEqual(1, fromPoint.checkers.Count);
        Assert.AreSame(moving, fromPoint.checkers[0]);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(blot, toPoint.checkers[0]);
        Assert.AreEqual(0, manager.barBlackPoint.checkers.Count);
    }

    [Test]
    public void TryApplySingleVisualUndoMove_HitMoveWithoutCapturedOnBar_ReturnsFalseForSyncFallback()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;
        manager.barBlackPoint = CreatePoint("BarBlackPoint");

        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(5);
        BoardPoint fromPoint = CreatePoint("FromPoint");
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[fromBoardIndex] = fromPoint;
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("MovingWhite", PlayerColor.White);
        toPoint.AddChecker(moving, animated: false);

        bool undone = manager.TryApplySingleVisualUndoMove(new Move { From = 6, To = 5, IsHit = true });
        Assert.IsFalse(undone);
    }

    [Test]
    public void TryApplySingleVisualUndoMove_BlackHitMove_RestoresCapturedWhiteChecker()
    {
        var managerGo = new GameObject("BoardManagerBlackUndo");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;
        manager.barWhitePoint = CreatePoint("BarWhitePoint");

        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(17);
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(18);
        BoardPoint fromPoint = CreatePoint("FromPointBlack");
        BoardPoint toPoint = CreatePoint("ToPointBlack");
        manager.allPoints[fromBoardIndex] = fromPoint;
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("MovingBlack", PlayerColor.Black);
        GameObject blot = CreateChecker("HitWhite", PlayerColor.White);
        fromPoint.AddChecker(moving, animated: false);
        toPoint.AddChecker(blot, animated: false);

        bool applied = manager.TryApplySingleVisualMove(new Move { From = 6, To = 5, IsHit = true }, PlayerColor.Black);
        Assert.IsTrue(applied);
        Assert.AreEqual(1, manager.barWhitePoint.checkers.Count);

        bool undone = manager.TryApplySingleVisualUndoMove(new Move { From = 6, To = 5, IsHit = true }, PlayerColor.Black, out string failureReason);
        Assert.IsTrue(undone, $"Expected black undo to succeed, reason={failureReason}");
        Assert.AreEqual("ok", failureReason);

        Assert.AreEqual(1, fromPoint.checkers.Count);
        Assert.AreSame(moving, fromPoint.checkers[0]);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(blot, toPoint.checkers[0]);
        Assert.AreEqual(0, manager.barWhitePoint.checkers.Count);
    }

    [Test]
    public void TryApplySingleVisualUndoMove_BearOffMove_RestoresCheckerToSourcePoint()
    {
        var managerGo = new GameObject("BoardManagerBearOffUndo");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.bearOffWhitePoint = CreatePoint("BearOffWhitePoint");

        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
        BoardPoint fromPoint = CreatePoint("FromPointWhite");
        manager.allPoints[fromBoardIndex] = fromPoint;

        GameObject moving = CreateChecker("MovingWhite", PlayerColor.White);
        fromPoint.AddChecker(moving, animated: false);

        bool applied = manager.TryApplySingleVisualMove(new Move { From = 6, To = -1, IsHit = false }, PlayerColor.White);
        Assert.IsTrue(applied);
        Assert.AreEqual(0, fromPoint.checkers.Count);
        Assert.AreEqual(1, manager.bearOffWhitePoint.checkers.Count);

        bool undone = manager.TryApplySingleVisualUndoMove(new Move { From = 6, To = -1, IsHit = false }, PlayerColor.White, out string failureReason);
        Assert.IsTrue(undone, $"Expected bear-off undo to succeed, reason={failureReason}");
        Assert.AreEqual("ok", failureReason);
        Assert.AreEqual(1, fromPoint.checkers.Count);
        Assert.AreSame(moving, fromPoint.checkers[0]);
        Assert.AreEqual(0, manager.bearOffWhitePoint.checkers.Count);
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
