using System.Collections.Generic;
using NUnit.Framework;

public class BackgammonBearOffPositionImporterEditModeTests
{
    [Test]
    public void ParseEntries_ExtractsUniquePositionIdsInOrder()
    {
        var lines = new List<string>
        {
            "[Backgammon][BearOffDebug] source=human move=5->-1 hit=False playerOnRoll=1 dice=0/0 cube=4 pid=n3sDAIB3mwEADA",
            "[Backgammon][BearOffDebug] source=human move=2->-1 hit=False playerOnRoll=1 dice=0/0 cube=4 pid=n70BAMC7zQAABg",
            "[Backgammon][BearOffDebug] source=human move=5->-1 hit=False playerOnRoll=1 dice=0/0 cube=4 pid=n3sDAIB3mwEADA"
        };

        List<BackgammonDebugPositionLibrary.Entry> parsed = BackgammonBearOffPositionImporter.ParseEntries(lines);

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual("n3sDAIB3mwEADA", parsed[0].positionId);
        Assert.AreEqual("n70BAMC7zQAABg", parsed[1].positionId);
        Assert.AreEqual("BearOff 01", parsed[0].label);
        Assert.AreEqual("BearOff 02", parsed[1].label);
    }
}
