using System.Collections.Generic;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

public class BoardManagerMovePreviewPointHighlightsEditModeTests
{
    [Test]
    public void PreviewMoveDestinationPoints_HighlightsOnlyMappedBoardDestinations()
    {
        var boardGo = new GameObject("BoardManager");
        var manager = boardGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];

        int boardIndexForEngine5 = BackgammonBoardLayout.EnginePointToBoardIndex(5);
        int boardIndexForEngine3 = BackgammonBoardLayout.EnginePointToBoardIndex(3);

        manager.allPoints[boardIndexForEngine5] = CreatePoint("Point5");
        manager.allPoints[boardIndexForEngine3] = CreatePoint("Point3");

        manager.PreviewMoveDestinationPoints(new HashSet<int> { 5, 3, -1 });

        Assert.IsTrue(manager.allPoints[boardIndexForEngine5].IsHighlighted);
        Assert.IsTrue(manager.allPoints[boardIndexForEngine3].IsHighlighted);
    }

    [Test]
    public void ClearMovePreviewPointHighlights_ClearsOnlyPreviewHighlights()
    {
        var boardGo = new GameObject("BoardManager");
        var manager = boardGo.AddComponent<BoardManager>();
        manager.allPoints = new BoardPoint[24];

        int boardIndexForEngine8 = BackgammonBoardLayout.EnginePointToBoardIndex(8);
        manager.allPoints[boardIndexForEngine8] = CreatePoint("Point8");

        manager.PreviewMoveDestinationPoints(new HashSet<int> { 8 });
        Assert.IsTrue(manager.allPoints[boardIndexForEngine8].IsHighlighted);

        manager.ClearMovePreviewPointHighlights();
        Assert.IsFalse(manager.allPoints[boardIndexForEngine8].IsHighlighted);
    }

    private static BoardPoint CreatePoint(string name)
    {
        var pointGo = new GameObject(name);
        var point = pointGo.AddComponent<BoardPoint>();
        point.pointRenderer = pointGo.AddComponent<MeshRenderer>();
        point.normalColor = Color.gray;
        point.SetHighlighted(false);
        return point;
    }
}
