using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;
using System.Collections.Generic;

public class BossVariantPoolEditModeTests
{
    [Test]
    public void DrawBossVariant_EmptyPool_ReturnsStandard()
    {
        var ante = new RunAnteConfig();
        ante.BossVariantPool.Clear();
        Assert.AreEqual(BossVariantType.Standard, ante.DrawBossVariant());
    }

    [Test]
    public void DrawBossVariant_AllNotYetImplemented_ReturnsStandard()
    {
        var ante = new RunAnteConfig();
        ante.BossVariantPool = new List<WeightedBossVariant>
        {
            new WeightedBossVariant { Variant = BossVariantType.Nackgammon, Weight = 100, NotYetImplemented = true },
        };
        Assert.AreEqual(BossVariantType.Standard, ante.DrawBossVariant());
    }

    [Test]
    public void DrawBossVariant_NeverDrawsNotYetImplemented()
    {
        var ante = new RunAnteConfig();
        ante.BossVariantPool = new List<WeightedBossVariant>
        {
            new WeightedBossVariant { Variant = BossVariantType.NoCube,      Weight = 50 },
            new WeightedBossVariant { Variant = BossVariantType.Nackgammon,  Weight = 9999, NotYetImplemented = true },
        };

        for (int i = 0; i < 200; i++)
            Assert.AreNotEqual(BossVariantType.Nackgammon, ante.DrawBossVariant());
    }

    [Test]
    public void DrawBossVariant_SingleEntry_AlwaysReturnsThatVariant()
    {
        var ante = new RunAnteConfig();
        ante.BossVariantPool = new List<WeightedBossVariant>
        {
            new WeightedBossVariant { Variant = BossVariantType.FewerGames, Weight = 100 },
        };

        for (int i = 0; i < 20; i++)
            Assert.AreEqual(BossVariantType.FewerGames, ante.DrawBossVariant());
    }

    [Test]
    public void DrawBossVariant_Ante1Pool_OnlyContainsAllowedVariants()
    {
        var cfg = RunConfig.BuildDefault();
        var allowed = new HashSet<BossVariantType> { BossVariantType.Standard, BossVariantType.FewerGames };

        for (int i = 0; i < 100; i++)
        {
            var drawn = cfg.Antes[0].DrawBossVariant();
            Assert.IsTrue(allowed.Contains(drawn), $"Unexpected variant drawn for Ante 1: {drawn}");
        }
    }
}
