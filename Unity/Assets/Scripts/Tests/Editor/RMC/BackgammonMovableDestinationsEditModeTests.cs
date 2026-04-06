using System.Collections.Generic;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;

public class BackgammonMovableDestinationsEditModeTests
{
    [Test]
    public void CollectDistinctFirstMoveTos_FiltersByFrom_UnionsDistinctTo()
    {
        var legals = new List<Turn>
        {
            new Turn { Moves = new List<Move> { new Move { From = 8, To = 5 } } },
            new Turn { Moves = new List<Move> { new Move { From = 8, To = 3 }, new Move { From = 3, To = 1 } } },
            new Turn { Moves = new List<Move> { new Move { From = 6, To = 4 } } }
        };

        var dest = new HashSet<int>();
        BackgammonMovableDestinations.CollectDistinctFirstMoveTos(8, legals, dest);

        Assert.AreEqual(2, dest.Count);
        Assert.IsTrue(dest.Contains(5));
        Assert.IsTrue(dest.Contains(3));
    }

    [Test]
    public void CollectDistinctFirstMoveTos_IncludesBearOff_MinusOne()
    {
        var legals = new List<Turn>
        {
            new Turn { Moves = new List<Move> { new Move { From = 2, To = -1 } } }
        };

        var dest = new HashSet<int>();
        BackgammonMovableDestinations.CollectDistinctFirstMoveTos(2, legals, dest);

        Assert.IsTrue(dest.Contains(-1));
    }
}
