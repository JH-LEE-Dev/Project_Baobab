using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>웨이브 시작 초기 나무 수와 군집 스폰 위치 규칙(기획서 v2.3 3-1 "초기 나무" · "생성 위치").</summary>
public class ClusterSpawnRulesTests
{
    private RoguelikeTestFactory factory;
    private RoguelikeBalanceSO balance;

    [SetUp]
    public void SetUp()
    {
        factory = new RoguelikeTestFactory();
        balance = factory.CreateBalance();
    }

    [TearDown]
    public void TearDown()
    {
        factory.DestroyAll();
    }

    [Test]
    public void InitialTrees_AreSpawnRateTimesTenSeconds_BelowThreshold()
    {
        // 1웨이브 약 5그루 ~ 30웨이브 약 25그루, 임계치 T(40)보다 항상 낮다.
        Assert.AreEqual(5, WaveSchedule.Get(balance, 1).initialTrees);
        Assert.AreEqual(15, WaveSchedule.Get(balance, 16).initialTrees);
        // 30웨이브 초당 2.45 × 10초 = 24.5(약 25). 스폰 주기를 거쳐 계산하므로 반올림 경계에서 24 또는 25다.
        Assert.That(WaveSchedule.Get(balance, 30).initialTrees, Is.InRange(24, 25));

        for (int wave = 1; wave <= 35; wave++)
        {
            WaveSpec spec = WaveSchedule.Get(balance, wave);
            Assert.Less(spec.initialTrees, balance.threshold, "웨이브 " + wave);
            Assert.LessOrEqual(spec.initialTrees, spec.killTarget, "웨이브 " + wave);
        }
    }

    [Test]
    public void InitialTrees_ClampedByThresholdAndTarget()
    {
        Assert.AreEqual(39, ClusterSpawnRules.InitialTreeCount(0.1f, 10f, 40, 200));
        Assert.AreEqual(3, ClusterSpawnRules.InitialTreeCount(0.1f, 10f, 40, 3));
        Assert.AreEqual(0, ClusterSpawnRules.InitialTreeCount(0.5f, 0f, 40, 30));
    }

    [Test]
    public void PickIndex_StaysInsideClusterAndAwayFromPlayer()
    {
        List<Vector3> free = new List<Vector3>();
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -5; y <= 5; y++) free.Add(new Vector3(x * 0.5f, y * 0.25f, 0f));
        }

        Vector3 center = new Vector3(3f, 0f, 0f);
        Vector3 player = new Vector3(2.5f, 0f, 0f);
        System.Random rng = new System.Random(3);
        List<int> scratch = new List<int>();

        for (int i = 0; i < 200; i++)
        {
            int index = ClusterSpawnRules.PickIndex(free, center, 1.5f, player, 0.6f, rng, scratch);
            Assert.GreaterOrEqual(index, 0);
            Assert.LessOrEqual(ClusterSpawnRules.IsoDistanceSq(free[index], center), 1.5f * 1.5f + 0.0001f, "군집 반경 안");
            Assert.GreaterOrEqual(ClusterSpawnRules.IsoDistanceSq(free[index], player), 0.6f * 0.6f, "플레이어 바로 옆 금지");
        }
    }

    [Test]
    public void PickIndex_ExpandsWhenClusterIsFull_AndReturnsMinusOneWhenEmpty()
    {
        List<Vector3> free = new List<Vector3> { new Vector3(5f, 0f, 0f) };
        List<int> scratch = new List<int>();

        Assert.AreEqual(0, ClusterSpawnRules.PickIndex(free, Vector3.zero, 1f, new Vector3(-3f, 0f, 0f), 0.5f, new System.Random(1), scratch));
        Assert.AreEqual(-1, ClusterSpawnRules.PickIndex(new List<Vector3>(), Vector3.zero, 1f, Vector3.zero, 0.5f, new System.Random(1), scratch));

        // 남은 칸이 플레이어 바로 옆뿐이면 심지 않는다(-1).
        List<Vector3> nearPlayer = new List<Vector3> { new Vector3(0.2f, 0f, 0f) };
        Assert.AreEqual(-1, ClusterSpawnRules.PickIndex(nearPlayer, Vector3.zero, 1f, Vector3.zero, 0.5f, new System.Random(1), scratch));
    }

    [Test]
    public void GapAnchor_OnlyWhenNearestTreeFartherThanR()
    {
        List<Vector3> trees = new List<Vector3> { new Vector3(6f, 0f, 0f), new Vector3(-8f, 0f, 0f) };

        Assert.IsTrue(ClusterSpawnRules.TryGetGapAnchor(trees, Vector3.zero, 4f, out Vector3 anchor));
        Assert.AreEqual(new Vector3(6f, 0f, 0f), anchor, "가장 가까운 나무 쪽 군집");

        Assert.IsFalse(ClusterSpawnRules.TryGetGapAnchor(trees, new Vector3(5f, 0f, 0f), 4f, out _), "가까운 나무가 있으면 평소 군집");
        Assert.IsFalse(ClusterSpawnRules.TryGetGapAnchor(new List<Vector3>(), Vector3.zero, 4f, out _), "나무가 없으면 평소 군집");
    }
}
