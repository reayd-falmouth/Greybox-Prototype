using EngineCore;
using NUnit.Framework;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using System.Reflection;
using UnityEngine;

public class BoardManagerApplySingleVisualMoveEditModeTests
{
    [SetUp]
    public void SetUp()
    {
        BackgammonBoardLayout.SetHorizontal(true);
    }

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
    public void TryApplySingleVisualMove_HitBlot_UsesBarPointWhenAvailable()
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

        bool ok = manager.TryApplySingleVisualMove(new Move { From = 6, To = 5 });

        Assert.IsTrue(ok);
        Assert.AreEqual(0, manager.barBlackAnchor.childCount);
        Assert.AreEqual(1, manager.barBlackPoint.checkers.Count);
        Assert.AreSame(blot, manager.barBlackPoint.checkers[0]);
    }

    [Test]
    public void TryApplySingleVisualMove_BlackMover_HitWhiteMovesToWhiteBar()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;

        int mappedFromEngine = 23 - 6;
        int mappedToEngine = 23 - 5;
        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedFromEngine);
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
        BoardPoint fromPoint = CreatePoint("FromPoint");
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[fromBoardIndex] = fromPoint;
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("MovingBlack", PlayerColor.Black);
        GameObject blot = CreateChecker("HitWhite", PlayerColor.White);
        fromPoint.AddChecker(moving, animated: false);
        toPoint.AddChecker(blot, animated: false);

        bool ok = manager.TryApplySingleVisualMove(new Move { From = 6, To = 5 }, PlayerColor.Black);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, fromPoint.checkers.Count);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(moving, toPoint.checkers[0]);
        Assert.AreEqual(1, manager.barWhiteAnchor.childCount);
        Assert.AreSame(blot, manager.barWhiteAnchor.GetChild(0).gameObject);
    }

    [Test]
    public void TryApplySingleVisualMove_BlackMover_EnterFromBlackBar()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;

        int mappedToEngine = 23 - 5;
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("BarBlackChecker", PlayerColor.Black);
        moving.transform.SetParent(manager.barBlackAnchor, false);

        bool ok = manager.TryApplySingleVisualMove(
            new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 5 },
            PlayerColor.Black);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, manager.barBlackAnchor.childCount);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(moving, toPoint.checkers[0]);
    }

    [Test]
    public void TryApplySingleVisualMove_BlackMover_EnterFromBlackBar_ReturnsMovedCheckerAndAnimating()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;

        int mappedToEngine = 23 - 5;
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("BarBlackChecker", PlayerColor.Black);
        moving.transform.SetParent(manager.barBlackAnchor, false);

        bool ok = manager.TryApplySingleVisualMove(
            new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 5 },
            out Checker movedChecker,
            PlayerColor.Black);

        Assert.IsTrue(ok);
        Assert.IsNotNull(movedChecker);
        Assert.AreSame(moving, movedChecker.gameObject);
        Assert.IsTrue(movedChecker.IsMoving);
    }

    [Test]
    public void TryApplySingleVisualMove_BlackMover_EnterFromBarPoint()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;
        manager.barBlackPoint = CreatePoint("BarBlackPoint");

        int mappedToEngine = 23 - 5;
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("BarBlackChecker", PlayerColor.Black);
        manager.barBlackPoint.AddChecker(moving, animated: false);

        bool ok = manager.TryApplySingleVisualMove(
            new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 5 },
            PlayerColor.Black);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, manager.barBlackPoint.checkers.Count);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(moving, toPoint.checkers[0]);
    }

    [Test]
    public void TryApplySingleVisualMove_BlackMover_EnterFromBarPoint_ReturnsMovedCheckerAndAnimating()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;
        manager.barBlackPoint = CreatePoint("BarBlackPoint");

        int mappedToEngine = 23 - 5;
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("BarBlackChecker", PlayerColor.Black);
        manager.barBlackPoint.AddChecker(moving, animated: false);

        bool ok = manager.TryApplySingleVisualMove(
            new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 5 },
            out Checker movedChecker,
            PlayerColor.Black);

        Assert.IsTrue(ok);
        Assert.IsNotNull(movedChecker);
        Assert.AreSame(moving, movedChecker.gameObject);
        Assert.IsTrue(movedChecker.IsMoving);
    }

    [Test]
    public void TryApplySingleVisualMove_BarPointPresentButAnchorHasChecker_UsesAnchorFallback()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;
        manager.barBlackPoint = CreatePoint("BarBlackPoint");

        int mappedToEngine = 23 - 5;
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject moving = CreateChecker("BarBlackAnchorChecker", PlayerColor.Black);
        moving.transform.SetParent(manager.barBlackAnchor, false);
        Assert.AreEqual(0, manager.barBlackPoint.checkers.Count);
        Assert.AreEqual(1, manager.barBlackAnchor.childCount);

        bool ok = manager.TryApplySingleVisualMove(
            new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 5 },
            PlayerColor.Black);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, manager.barBlackAnchor.childCount);
        Assert.AreEqual(1, toPoint.checkers.Count);
        Assert.AreSame(moving, toPoint.checkers[0]);
    }

    [Test]
    public void TryApplySingleVisualMove_BlackMover_DoesNotMoveWhiteTopChecker()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.barWhiteAnchor = new GameObject("BarWhite").transform;
        manager.barBlackAnchor = new GameObject("BarBlack").transform;

        int mappedFromEngine = 23 - 6;
        int mappedToEngine = 23 - 5;
        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedFromEngine);
        int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
        BoardPoint fromPoint = CreatePoint("FromPoint");
        BoardPoint toPoint = CreatePoint("ToPoint");
        manager.allPoints[fromBoardIndex] = fromPoint;
        manager.allPoints[toBoardIndex] = toPoint;

        GameObject whiteTop = CreateChecker("WhiteTop", PlayerColor.White);
        fromPoint.AddChecker(whiteTop, animated: false);

        bool ok = manager.TryApplySingleVisualMove(new Move { From = 6, To = 5 }, PlayerColor.Black);

        Assert.IsFalse(ok);
        Assert.AreEqual(1, fromPoint.checkers.Count);
        Assert.AreSame(whiteTop, fromPoint.checkers[0]);
        Assert.AreEqual(0, toPoint.checkers.Count);
    }

    [Test]
    public void TryApplySingleVisualMove_BearOff_StacksOnWhiteBearOffPoint()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];
        manager.bearOffWhitePoint = CreatePoint("BearOffWhitePoint");

        int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
        BoardPoint fromPoint = CreatePoint("FromPoint");
        manager.allPoints[fromBoardIndex] = fromPoint;
        GameObject moving = CreateChecker("MovingWhite", PlayerColor.White);
        fromPoint.AddChecker(moving, animated: false);

        bool ok = manager.TryApplySingleVisualMove(new Move { From = 6, To = -1 });

        Assert.IsTrue(ok);
        Assert.AreEqual(0, fromPoint.checkers.Count);
        Assert.AreEqual(1, manager.bearOffWhitePoint.checkers.Count);
        Assert.AreSame(moving, manager.bearOffWhitePoint.checkers[0]);
        Assert.AreSame(manager.bearOffWhitePoint.transform, moving.transform.parent);
    }

    [Test]
    public void SyncCheckersFromGameState_VisualDecodeStableAcrossPlayerOnRoll()
    {
        bool oldOpponentIsAi = BackgammonSettings.OpponentIsAi;
        try
        {
            BackgammonSettings.OpponentIsAi = true;

            var managerGo = new GameObject("BoardManager");
            var manager = managerGo.AddComponent<BoardManager>();
            manager.allPoints = new BoardPoint[24];
            manager.barWhiteAnchor = new GameObject("BarWhite").transform;
            manager.barBlackAnchor = new GameObject("BarBlack").transform;
            manager.whiteCheckerPrefab = CreateCheckerPrefab("WhitePrefab");
            manager.blackCheckerPrefab = CreateCheckerPrefab("BlackPrefab");
            int boardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
            BoardPoint point = CreatePoint("Point");
            manager.allPoints[boardIndex] = point;

            var state = new GameState
            {
                Player1Checkers = new int[25],
                Player2Checkers = new int[25]
            };

            // Human turn (PlayerOnRoll=1): logical P1 is human.
            state.PlayerOnRoll = 1;
            state.Player1Checkers[6] = 1;
            state.Player2Checkers[17] = 2; // mirrored opponent stack for enginePoint=6
            manager.SyncCheckersFromGameState(state);
            AssertPointColorCounts(point, white: 1, black: 2);

            // Visual decode invariant: same arrays decode identically regardless of PlayerOnRoll.
            state.PlayerOnRoll = 0;
            manager.SyncCheckersFromGameState(state);
            AssertPointColorCounts(point, white: 1, black: 2);
        }
        finally
        {
            BackgammonSettings.OpponentIsAi = oldOpponentIsAi;
        }
    }

    [Test]
    public void GenerateBoard_CreatesRaisedCenterBarPoints()
    {
        var managerGo = new GameObject("BoardManager");
        var manager = managerGo.AddComponent<BoardManager>();

        manager.pointPrefab = CreatePointPrefab("PointPrefab");
        manager.whiteCheckerPrefab = CreateCheckerPrefab("WhitePrefab");
        manager.blackCheckerPrefab = CreateCheckerPrefab("BlackPrefab");
        manager.pointMaterial = new Material(Shader.Find("Unlit/Color"));

        manager.leftHalfFloor = CreateFloor("LeftFloor", new Vector3(-1f, 0f, 0f));
        manager.rightHalfFloor = CreateFloor("RightFloor", new Vector3(1f, 0f, 0f));

        SetPrivateField(manager, "barPointLiftY", 0.2f);
        SetPrivateField(manager, "barPointSpacingX", 0.4f);
        SetPrivateField(manager, "barCenterOffsetX", 0f);
        SetPrivateField(manager, "barCenterOffsetZ", 0f);

        manager.GenerateBoard();

        Assert.IsNotNull(manager.barWhitePoint);
        Assert.IsNotNull(manager.barBlackPoint);
        Assert.Greater(manager.barWhitePoint.transform.position.y, 0.19f);
        Assert.Greater(manager.barBlackPoint.transform.position.y, 0.19f);
        float spacing = Mathf.Abs(manager.barBlackPoint.transform.position.x - manager.barWhitePoint.transform.position.x);
        Assert.GreaterOrEqual(spacing, 0.39f);
        Assert.IsNotNull(manager.bearOffWhitePoint);
        Assert.IsNotNull(manager.bearOffBlackPoint);
    }

    private static void AssertPointColorCounts(BoardPoint point, int white, int black)
    {
        int whiteCount = 0;
        int blackCount = 0;
        for (int i = 0; i < point.checkers.Count; i++)
        {
            Checker checker = point.checkers[i].GetComponent<Checker>();
            if (checker == null) continue;
            if (checker.color == PlayerColor.White)
                whiteCount++;
            else if (checker.color == PlayerColor.Black)
                blackCount++;
        }

        Assert.AreEqual(white, whiteCount);
        Assert.AreEqual(black, blackCount);
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

    private static GameObject CreateCheckerPrefab(string name)
    {
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        prefab.name = name;
        if (prefab.GetComponent<Checker>() == null)
            prefab.AddComponent<Checker>();
        return prefab;
    }

    private static GameObject CreatePointPrefab(string name)
    {
        var prefab = new GameObject(name);
        prefab.AddComponent<BoardPoint>();
        return prefab;
    }

    private static Transform CreateFloor(string name, Vector3 position)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = name;
        floor.transform.position = position;
        floor.transform.localScale = new Vector3(2f, 0.1f, 4f);
        return floor.transform;
    }

    private static void SetPrivateField(BoardManager manager, string fieldName, object value)
    {
        FieldInfo field = typeof(BoardManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Missing private field {fieldName}");
        field.SetValue(manager, value);
    }
}
