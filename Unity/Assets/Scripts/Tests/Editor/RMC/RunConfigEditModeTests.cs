using NUnit.Framework;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

public class RunConfigEditModeTests
{
    [Test]
    public void BuildDefault_Ante1Small_Threshold_Is50()
    {
        var cfg = RunConfig.BuildDefault();
        Assert.AreEqual(50, cfg.Antes[0].SmallSession.ScoreThreshold);
    }

    [Test]
    public void BuildDefault_Ante1Big_Threshold_Is75()
    {
        var cfg = RunConfig.BuildDefault();
        // k=1: 50 * 1.5^1 = 75
        Assert.AreEqual(75, cfg.Antes[0].BigSession.ScoreThreshold);
    }

    [Test]
    public void BuildDefault_Ante1Boss_Threshold_Is112()
    {
        var cfg = RunConfig.BuildDefault();
        // k=2: 50 * 1.5^2 = 112.5 -> 113 (Mathf.RoundToInt)
        int expected = Mathf.RoundToInt(50 * Mathf.Pow(1.5f, 2));
        Assert.AreEqual(expected, cfg.Antes[0].BossSession.ScoreThreshold);
    }

    [Test]
    public void BuildDefault_Ante2Small_Threshold_Is168()
    {
        var cfg = RunConfig.BuildDefault();
        // k=3: 50 * 1.5^3 = 168.75 -> 169
        int expected = Mathf.RoundToInt(50 * Mathf.Pow(1.5f, 3));
        Assert.AreEqual(expected, cfg.Antes[1].SmallSession.ScoreThreshold);
    }

    [Test]
    public void BuildDefault_BossPool_Ante1_DoesNotContainNoCube()
    {
        var cfg = RunConfig.BuildDefault();
        foreach (var entry in cfg.Antes[0].BossVariantPool)
            Assert.AreNotEqual(BossVariantType.NoCube, entry.Variant);
    }

    [Test]
    public void BuildDefault_BossPool_Ante2_ContainsNoCube()
    {
        var cfg = RunConfig.BuildDefault();
        bool found = false;
        foreach (var entry in cfg.Antes[1].BossVariantPool)
            if (entry.Variant == BossVariantType.NoCube) found = true;
        Assert.IsTrue(found);
    }

    [Test]
    public void BuildDefault_RewardIsApprox40Percent_OfThreshold()
    {
        var cfg = RunConfig.BuildDefault();
        // Reward rounded to nearest 5, so allow ±5
        int threshold = cfg.Antes[0].SmallSession.ScoreThreshold;
        int reward    = cfg.Antes[0].SmallSession.Reward;
        Assert.AreEqual(threshold * 0.4f, reward, 5f);
    }

    [Test]
    public void BuildDefault_AllSessionTypes_AreCorrect()
    {
        var cfg = RunConfig.BuildDefault();
        for (int a = 0; a < cfg.Antes.Count; a++)
        {
            Assert.AreEqual(RunSessionType.Small, cfg.Antes[a].SmallSession.SessionType);
            Assert.AreEqual(RunSessionType.Big,   cfg.Antes[a].BigSession.SessionType);
            Assert.AreEqual(RunSessionType.Boss,  cfg.Antes[a].BossSession.SessionType);
        }
    }

    [Test]
    public void BuildDefault_MaxGames_IsSetOnAllSessions()
    {
        var cfg = RunConfig.BuildDefault(maxGames: 3);
        for (int a = 0; a < cfg.Antes.Count; a++)
        {
            Assert.AreEqual(3, cfg.Antes[a].SmallSession.MaxGames);
            Assert.AreEqual(3, cfg.Antes[a].BigSession.MaxGames);
            Assert.AreEqual(3, cfg.Antes[a].BossSession.MaxGames);
        }
    }
}
