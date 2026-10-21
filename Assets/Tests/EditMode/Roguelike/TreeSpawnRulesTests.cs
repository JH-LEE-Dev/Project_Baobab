using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>웨이브 시작 초기 나무 수 · 묶음 크기 · 분산 스폰 위치 규칙(기획서 v2.5 3-1 "초기 나무" · "스폰 속도 · 묶음" · "생성 위치").</summary>
public class TreeSpawnRulesTests
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
    public void InitialTrees_AreHalfOfThreshold_SameForEveryWave()
    {
        // T 40이면 20그루, 웨이브 번호와 무관하다. 목표 처치 수(1웨이브 30)보다 적다.
        for (int wave = 1; wave <= 35; wave++)
        {
            WaveSpec spec = WaveSchedule.Get(balance, wave);
            Assert.AreEqual(20, spec.initialTrees, "웨이브 " + wave);
            Assert.LessOrEqual(spec.initialTrees, spec.killTarget, "웨이브 " + wave);
        }
    }

    [Test]
    public void InitialTrees_FollowReducedThreshold_AndClampToTarget()
    {
        Assert.AreEqual(17, TreeSpawnRules.InitialTreeCount(35, 0.5f, 200), "계약으로 줄어든 T의 절반(내림)");
        Assert.AreEqual(3, TreeSpawnRules.InitialTreeCount(40, 0.5f, 3), "목표 처치 수를 넘지 않는다");
        Assert.AreEqual(39, TreeSpawnRules.InitialTreeCount(40, 1f, 200), "T보다 항상 낮다");
        Assert.AreEqual(0, TreeSpawnRules.InitialTreeCount(40, 0f, 30));
    }

    [Test]
    public void TakeBatch_CarriesFractionToKeepAverageRate()
    {
        // 1웨이브 초당 0.5 × 3초 = 1.5그루 → 1, 2, 1, 2 ...
        float carry = 0f;
        int total = 0;
        int[] expected = { 1, 2, 1, 2 };
        for (int i = 0; i < expected.Length; i++)
        {
            int batch = TreeSpawnRules.TakeBatch(ref carry, 2f, 3f);
            Assert.AreEqual(expected[i], batch, "묶음 " + (i + 1));
            total += batch;
        }

        Assert.AreEqual(6, total, "12초 동안 초당 0.5그루");

        // 30웨이브 초당 2.45 × 3초 = 7.35그루 → 대부분 7, 이월이 쌓이면 8.
        carry = 0f;
        total = 0;
        for (int i = 0; i < 20; i++) total += TreeSpawnRules.TakeBatch(ref carry, 1f / 2.45f, 3f);
        Assert.That(total, Is.InRange(146, 147), "60초 동안 초당 2.45그루");
    }

    [Test]
    public void PickScatteredIndex_KeepsSpacingAndAwayFromPlayer()
    {
        List<Vector3> free = new List<Vector3>();
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -5; y <= 5; y++) free.Add(new Vector3(x * 0.5f, y * 0.25f, 0f));
        }

        Vector3 player = new Vector3(2.5f, 0f, 0f);
        List<Vector3> occupied = new List<Vector3> { new Vector3(-2f, 0f, 0f), new Vector3(0f, 0.5f, 0f) };
        System.Random rng = new System.Random(3);
        List<int> scratch = new List<int>();

        for (int i = 0; i < 200; i++)
        {
            int index = TreeSpawnRules.PickScatteredIndex(free, occupied, 1f, player, 0.6f, rng, scratch);
            Assert.GreaterOrEqual(index, 0);
            Assert.GreaterOrEqual(TreeSpawnRules.IsoDistanceSq(free[index], player), 0.6f * 0.6f, "플레이어 바로 옆 금지");
            for (int k = 0; k < occupied.Count; k++)
            {
                Assert.GreaterOrEqual(TreeSpawnRules.IsoDistanceSq(free[index], occupied[k]), 1f - 0.0001f, "최소 간격");
            }
        }
    }

    [Test]
    public void PickScatteredIndex_DropsSpacingWhenNoRoom_AndReturnsMinusOneNearPlayerOnly()
    {
        List<int> scratch = new List<int>();

        // 간격을 지킬 칸이 없으면 간격만 포기하고 심는다.
        List<Vector3> crowded = new List<Vector3> { new Vector3(5f, 0f, 0f) };
        List<Vector3> occupied = new List<Vector3> { new Vector3(5.2f, 0f, 0f) };
        Assert.AreEqual(0, TreeSpawnRules.PickScatteredIndex(crowded, occupied, 1f, new Vector3(-3f, 0f, 0f), 0.5f, new System.Random(1), scratch));

        Assert.AreEqual(-1, TreeSpawnRules.PickScatteredIndex(new List<Vector3>(), occupied, 1f, Vector3.zero, 0.5f, new System.Random(1), scratch));

        // 남은 칸이 플레이어 바로 옆뿐이면 심지 않는다(-1).
        List<Vector3> nearPlayer = new List<Vector3> { new Vector3(0.2f, 0f, 0f) };
        Assert.AreEqual(-1, TreeSpawnRules.PickScatteredIndex(nearPlayer, null, 1f, Vector3.zero, 0.5f, new System.Random(1), scratch));
    }
}
