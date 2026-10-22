using NUnit.Framework;
using UnityEngine;

/// <summary>오프로드 차선 계산(섬 타원과 아래쪽 대각선의 교차).</summary>
public class OffroadLanesTests
{
    private static readonly Vector2 Center = new Vector2(3f, -2f);
    private const float Rx = 8f;
    private const float Ry = 4.5f;

    private static float EllipseValue(Vector2 _p)
    {
        Vector2 d = _p - Center;
        return d.x * d.x / (Rx * Rx) + d.y * d.y / (Ry * Ry);
    }

    [Test]
    public void DownDiagonal_PointsDownAndFollowsIsoRatio()
    {
        Vector2 right = OffroadLanes.DownDiagonal(true);
        Vector2 left = OffroadLanes.DownDiagonal(false);

        Assert.Greater(right.x, 0f);
        Assert.Less(left.x, 0f);
        Assert.Less(right.y, 0f);
        Assert.AreEqual(-0.5f, right.y / right.x, 0.0001f);
        Assert.AreEqual(1f, right.magnitude, 0.0001f);
    }

    [Test]
    public void Lane_EntryAndExitLieOnIsland_EntryIsUpstream()
    {
        foreach (bool bRight in new[] { true, false })
        {
            Vector2 dir = OffroadLanes.DownDiagonal(bRight);
            float half = OffroadLanes.HalfWidth(Rx, Ry, dir);

            for (int i = -4; i <= 4; i++)
            {
                float offset = half * 0.2f * i;
                Assert.IsTrue(OffroadLanes.TryGetLane(Center, Rx, Ry, dir, offset, out Vector2 entry, out Vector2 exit), "offset " + offset);

                Assert.AreEqual(1f, EllipseValue(entry), 0.001f);
                Assert.AreEqual(1f, EllipseValue(exit), 0.001f);
                Assert.Greater(Vector2.Dot(exit - entry, dir), 0f, "진입점에서 이탈점으로 진행 방향을 따라 달려야 한다");
            }
        }
    }

    [Test]
    public void Lane_OutsideHalfWidth_Misses()
    {
        Vector2 dir = OffroadLanes.DownDiagonal(true);
        float half = OffroadLanes.HalfWidth(Rx, Ry, dir);

        Assert.IsFalse(OffroadLanes.TryGetLane(Center, Rx, Ry, dir, half * 1.01f, out _, out _));
        Assert.IsTrue(OffroadLanes.TryGetLane(Center, Rx, Ry, dir, half * 0.99f, out _, out _));
    }

    [Test]
    public void SlotOffset_NeighborsKeepMinGap_InsideSpread()
    {
        const float half = 6f;
        const float spread = 0.75f;
        const float gap = 1.125f; // 오프로드 II(범위 +25%)의 돌진 판정 반지름

        int slots = OffroadLanes.SlotCount(half, spread, gap);
        Assert.AreEqual(8, slots, "9 ÷ 1.125");

        for (int i = 0; i < slots; i++)
        {
            float min = OffroadLanes.SlotOffset(i, slots, half, spread, gap, 0f);
            float max = OffroadLanes.SlotOffset(i, slots, half, spread, gap, 1f);
            Assert.GreaterOrEqual(min, -half * spread - 0.0001f);
            Assert.LessOrEqual(max, half * spread + 0.0001f);

            // 이웃 칸이 서로 가장 가깝게 흔들려도(이 칸은 끝까지 오른쪽, 다음 칸은 끝까지 왼쪽) 최소 간격 이상 떨어진다.
            if (i + 1 < slots) Assert.GreaterOrEqual(OffroadLanes.SlotOffset(i + 1, slots, half, spread, gap, 0f) - max, gap - 0.0001f, "칸 " + i);
        }
    }

    [Test]
    public void SlotCount_AtLeastOne()
    {
        Assert.AreEqual(1, OffroadLanes.SlotCount(0.2f, 0.75f, 1f));
        Assert.AreEqual(1, OffroadLanes.SlotCount(6f, 0.75f, 0f));
    }
}
