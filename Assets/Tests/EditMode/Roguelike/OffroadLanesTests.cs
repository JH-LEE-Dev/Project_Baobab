using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>오프로드 차선 계산(섬 타원·마름모와 아래쪽 대각선의 교차).</summary>
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
    public void RowSpacing_IsoCell_IsOneOverRootFive()
    {
        Vector2 cell = new Vector2(1f, 0.5f);
        float expected = 1f / Mathf.Sqrt(5f);

        Assert.AreEqual(expected, OffroadLanes.RowSpacing(OffroadLanes.DownDiagonal(true), cell), 0.0001f);
        Assert.AreEqual(expected, OffroadLanes.RowSpacing(OffroadLanes.DownDiagonal(false), cell), 0.0001f);
    }

    [Test]
    public void RowRange_CoversEdgeRows_AndStaysInsideHalfWidth()
    {
        float spacing = 1f / Mathf.Sqrt(5f);
        float half = spacing * 10f; // 가장자리 줄이 반폭과 정확히 겹친다(섬 반지름은 칸 중심 기준)

        Assert.IsTrue(OffroadLanes.TryGetRowRange(half, 1f, spacing, 0f, out int kMin, out int kMax));
        Assert.AreEqual(-10, kMin, "가장자리 줄까지 후보");
        Assert.AreEqual(10, kMax, "가장자리 줄까지 후보");

        // 위상이 반 줄 어긋나면(섬 중심이 칸 중심보다 반 칸 아래) 반폭 바깥 반 줄까지 고른다 - 실제 가장자리 줄이 거기 있을 수 있다.
        Assert.IsTrue(OffroadLanes.TryGetRowRange(half, 1f, spacing, spacing * 0.5f, out kMin, out kMax));
        Assert.AreEqual(-11, kMin);
        Assert.AreEqual(10, kMax, "반폭 + 반 줄 자리의 가장자리 줄");
        for (int k = kMin; k <= kMax; k++) Assert.LessOrEqual(Mathf.Abs(spacing * 0.5f + k * spacing), half + spacing * 0.5f + 0.0001f, "줄 " + k);

        Assert.IsTrue(OffroadLanes.TryGetRowRange(half, 0.5f, spacing, 0f, out kMin, out kMax));
        Assert.AreEqual(-5, kMin);
        Assert.AreEqual(5, kMax);
    }

    private static float DiamondValue(Vector2 _p)
    {
        Vector2 d = _p - Center;
        return Mathf.Abs(d.x) / Rx + Mathf.Abs(d.y) / Ry;
    }

    [Test]
    public void DiamondLane_EntryAndExitLieOnIsland_EntryIsUpstream()
    {
        foreach (bool bRight in new[] { true, false })
        {
            Vector2 dir = OffroadLanes.DownDiagonal(bRight);
            float half = OffroadLanes.HalfWidth(IslandShapeType.Diamond, Rx, Ry, dir);

            for (int i = -4; i <= 4; i++)
            {
                float offset = half * 0.2f * i;
                Assert.IsTrue(OffroadLanes.TryGetLane(IslandShapeType.Diamond, Center, Rx, Ry, dir, offset, out Vector2 entry, out Vector2 exit), "offset " + offset);

                Assert.AreEqual(1f, DiamondValue(entry), 0.001f);
                Assert.AreEqual(1f, DiamondValue(exit), 0.001f);
                Assert.LessOrEqual(DiamondValue((entry + exit) * 0.5f), 1f + 0.0001f, "선분 가운데는 섬 안");
                Assert.Greater(Vector2.Dot(exit - entry, dir), 0f, "진입점에서 이탈점으로 진행 방향을 따라 달려야 한다");
            }
        }
    }

    [Test]
    public void DiamondLane_OutsideHalfWidth_Misses()
    {
        Vector2 dir = OffroadLanes.DownDiagonal(true);
        float half = OffroadLanes.HalfWidth(IslandShapeType.Diamond, Rx, Ry, dir);

        Assert.IsFalse(OffroadLanes.TryGetLane(IslandShapeType.Diamond, Center, Rx, Ry, dir, half * 1.01f, out _, out _));
        Assert.IsTrue(OffroadLanes.TryGetLane(IslandShapeType.Diamond, Center, Rx, Ry, dir, half * 0.99f, out _, out _));
    }

    [Test]
    public void IslandShape_DiamondTouchesScreenMidpoints()
    {
        Assert.IsTrue(IslandShape.Contains(IslandShapeType.Diamond, Rx, 0f, Rx, Ry));
        Assert.IsTrue(IslandShape.Contains(IslandShapeType.Diamond, 0f, -Ry, Rx, Ry));
        Assert.IsFalse(IslandShape.Contains(IslandShapeType.Diamond, Rx * 0.5f + 0.01f, Ry * 0.5f, Rx, Ry), "변 바깥");
        Assert.IsTrue(IslandShape.Contains(IslandShapeType.Ellipse, Rx * 0.5f + 0.01f, Ry * 0.5f, Rx, Ry), "같은 점이 타원에서는 안");
    }

    [Test]
    public void RowRange_ZeroSpacing_HasNoRows()
    {
        Assert.IsFalse(OffroadLanes.TryGetRowRange(6f, 1f, 0f, 0f, out _, out _));
    }

    // // 타일 위 차선(CollectLandLanes)

    /// <summary>아이소 칸 (1, 0.5)의 마름모 섬. 칸 (x, y)의 위치 = ((x − y) / 2, (x + y) / 4). 섬 밖은 물이다.</summary>
    private sealed class FakeIsland : OffroadLanes.ILandTiles
    {
        public const float RadiusX = 8f;
        public const float RadiusY = 4f;

        public Vector3Int WorldToCell(Vector3 _worldPos)
        {
            float u = _worldPos.x / 0.5f;
            float v = _worldPos.y / 0.25f;
            return new Vector3Int(Mathf.RoundToInt((u + v) * 0.5f), Mathf.RoundToInt((v - u) * 0.5f), 0);
        }

        public Vector3 CellToWorld(Vector3Int _cellPos) => new Vector3((_cellPos.x - _cellPos.y) * 0.5f, (_cellPos.x + _cellPos.y) * 0.25f, 0f);

        public bool IsWaterTile(Vector3Int _cellPos)
        {
            Vector3 p = CellToWorld(_cellPos);
            return false == IslandShape.Contains(IslandShapeType.Diamond, p.x, p.y, RadiusX, RadiusY);
        }
    }

    private static readonly Vector2 IsoCell = new Vector2(1f, 0.5f);
    private static float CellStep => Mathf.Sqrt(IsoCell.x * IsoCell.x + IsoCell.y * IsoCell.y) * 0.5f;

    // 게임처럼 섬 중심을 칸 중심보다 반 칸 아래(_bShifted) 또는 칸 중심 그대로 넘긴다.
    private static List<OffroadLanes.Lane> CollectFakeLanes(FakeIsland _island, bool _bShifted)
    {
        Vector2 center = _bShifted ? new Vector2(0f, -IsoCell.y * 0.5f) : Vector2.zero;
        List<OffroadLanes.Lane> lanes = new List<OffroadLanes.Lane>();
        OffroadLanes.CollectLandLanes(_island, IslandShapeType.Diamond, center, FakeIsland.RadiusX, FakeIsland.RadiusY, IsoCell, 1f, lanes);
        return lanes;
    }

    [Test]
    public void LandLanes_IncludeBothEdgeRows([Values(false, true)] bool _bShifted)
    {
        FakeIsland island = new FakeIsland();
        List<OffroadLanes.Lane> lanes = CollectFakeLanes(island, _bShifted);
        Vector2 center = _bShifted ? new Vector2(0f, -IsoCell.y * 0.5f) : Vector2.zero;

        foreach (bool bRight in new[] { true, false })
        {
            Vector2 dir = OffroadLanes.DownDiagonal(bRight);
            Vector2 perp = new Vector2(-dir.y, dir.x);

            // 육지 칸이 수직 방향으로 퍼진 양 끝(가장자리 줄)
            float landMin = float.MaxValue;
            float landMax = float.MinValue;
            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (island.IsWaterTile(cell)) continue;
                    float d = Vector2.Dot((Vector2)island.CellToWorld(cell) - center, perp);
                    landMin = Mathf.Min(landMin, d);
                    landMax = Mathf.Max(landMax, d);
                }
            }

            float laneMin = float.MaxValue;
            float laneMax = float.MinValue;
            foreach (OffroadLanes.Lane lane in lanes)
            {
                if (lane.bRight != bRight) continue;
                laneMin = Mathf.Min(laneMin, lane.offset);
                laneMax = Mathf.Max(laneMax, lane.offset);
            }

            Assert.AreEqual(landMin, laneMin, 0.001f, "한쪽 가장자리 줄");
            Assert.AreEqual(landMax, laneMax, 0.001f, "반대쪽 가장자리 줄");
        }
    }

    [Test]
    public void LandLanes_StartAndStopOnOutermostLandTiles([Values(false, true)] bool _bShifted)
    {
        FakeIsland island = new FakeIsland();
        List<OffroadLanes.Lane> lanes = CollectFakeLanes(island, _bShifted);
        Vector2 center = _bShifted ? new Vector2(0f, -IsoCell.y * 0.5f) : Vector2.zero;
        Assert.Greater(lanes.Count, 0);

        foreach (OffroadLanes.Lane lane in lanes)
        {
            Vector2 dir = OffroadLanes.DownDiagonal(lane.bRight);
            Vector2 perp = new Vector2(-dir.y, dir.x);

            // 소환 위치 · 멈추는 곳은 칸 중심이고 그 줄 위다.
            Vector3Int entryCell = island.WorldToCell(lane.entry);
            Vector3Int exitCell = island.WorldToCell(lane.exit);
            Assert.AreEqual(0f, Vector2.Distance(lane.entry, island.CellToWorld(entryCell)), 0.0001f);
            Assert.AreEqual(0f, Vector2.Distance(lane.exit, island.CellToWorld(exitCell)), 0.0001f);
            Assert.AreEqual(lane.offset, Vector2.Dot(lane.entry - center, perp), 0.001f);
            Assert.AreEqual(lane.offset, Vector2.Dot(lane.exit - center, perp), 0.001f);

            // 첫 · 마지막 육지 칸이고 바로 바깥은 물이다. 한 칸짜리 차선은 없다.
            Assert.IsFalse(island.IsWaterTile(entryCell));
            Assert.IsFalse(island.IsWaterTile(exitCell));
            Assert.IsTrue(island.IsWaterTile(island.WorldToCell(lane.entry - dir * CellStep)), "소환 위치 뒤는 물");
            Assert.IsTrue(island.IsWaterTile(island.WorldToCell(lane.exit + dir * CellStep)), "멈추는 곳 앞은 물");
            Assert.GreaterOrEqual(Vector2.Dot(lane.exit - lane.entry, dir), CellStep * 0.99f);
        }
    }

    [Test]
    public void LandLanes_FitSixCarsAtFixedGap()
    {
        // 소환 위치 최소 간격은 범위와 무관한 오프로드 판정 반지름(0.9)이다. 범위 증가와 상관없이 MAX 6대가 들어간다.
        const float gap = 0.9f;
        List<OffroadLanes.Lane> lanes = CollectFakeLanes(new FakeIsland(), true);

        int fit = 0;
        foreach (bool bRight in new[] { true, false })
        {
            float last = float.NegativeInfinity;
            List<float> offsets = new List<float>();
            foreach (OffroadLanes.Lane lane in lanes)
            {
                if (lane.bRight == bRight) offsets.Add(lane.offset);
            }

            offsets.Sort();
            foreach (float offset in offsets)
            {
                if (offset - last < gap) continue;
                last = offset;
                fit++;
            }
        }

        Assert.GreaterOrEqual(fit, 6);
    }
}
