using System.Collections.Generic;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.UI;

public class BackgammonHudLegalSignatureEditModeTests
{
    [Test]
    public void ComputeLegalSignature_SameLegals_ProducesSameSignature()
    {
        var legalsA = new List<Turn>
        {
            new Turn { Moves = new List<Move> { new Move { From = 6, To = 5, IsHit = false } } },
            new Turn { Moves = new List<Move> { new Move { From = 8, To = 3, IsHit = true } } }
        };
        var legalsB = new List<Turn>
        {
            new Turn { Moves = new List<Move> { new Move { From = 6, To = 5, IsHit = false } } },
            new Turn { Moves = new List<Move> { new Move { From = 8, To = 3, IsHit = true } } }
        };

        string sigA = OptionsModalController.ComputeLegalSignature(legalsA);
        string sigB = OptionsModalController.ComputeLegalSignature(legalsB);

        Assert.AreEqual(sigA, sigB);
    }

    [Test]
    public void ComputeLegalSignature_ChangedLegals_ProducesDifferentSignature()
    {
        var legalsA = new List<Turn>
        {
            new Turn { Moves = new List<Move> { new Move { From = 6, To = 5, IsHit = false } } }
        };
        var legalsB = new List<Turn>
        {
            new Turn { Moves = new List<Move> { new Move { From = 6, To = 4, IsHit = false } } }
        };

        string sigA = OptionsModalController.ComputeLegalSignature(legalsA);
        string sigB = OptionsModalController.ComputeLegalSignature(legalsB);

        Assert.AreNotEqual(sigA, sigB);
    }
}
