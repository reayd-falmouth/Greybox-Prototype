using System.Collections.Generic;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonMovableFromPointsEditModeTests
{
    [Test]
    public void CollectMovableFromEnginePoints_EmptyLegals_ClearsDest()
    {
        var dest = new HashSet<int> { 3, 7 };
        BackgammonMovableFromPoints.CollectMovableFromEnginePoints(new List<Turn>(), dest);
        Assert.IsEmpty(dest);
    }

    [Test]
    public void CollectMovableFromEnginePoints_UnionsFirstMoveFromEachTurn()
    {
        var legals = new List<Turn>
        {
            new Turn
            {
                Moves = new List<Move>
                {
                    new Move { From = 6, To = 3 }
                }
            },
            new Turn
            {
                Moves = new List<Move>
                {
                    new Move { From = 8, To = 5 },
                    new Move { From = 5, To = 3 }
                }
            },
            new Turn
            {
                Moves = new List<Move>
                {
                    new Move { From = 6, To = 2 }
                }
            }
        };

        var dest = new HashSet<int>();
        BackgammonMovableFromPoints.CollectMovableFromEnginePoints(legals, dest);

        Assert.AreEqual(2, dest.Count);
        Assert.IsTrue(dest.Contains(6));
        Assert.IsTrue(dest.Contains(8));
    }

    [Test]
    public void CollectMovableFromEnginePoints_IncludesBar_From24()
    {
        var legals = new List<Turn>
        {
            new Turn
            {
                Moves = new List<Move>
                {
                    new Move { From = 24, To = 18 }
                }
            }
        };

        var dest = new HashSet<int>();
        BackgammonMovableFromPoints.CollectMovableFromEnginePoints(legals, dest);

        Assert.IsTrue(dest.Contains(24));
    }
}
