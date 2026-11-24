using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 한 나무 스프라이트(와 단계/변형)에 대한 디버프 장식 배치. 나무 실루엣 마스크 안의 불투명 픽셀에서 위치를 골라 만들고, 같은 입력이면 항상 같은 결과다.
    /// 얼룩·줄무늬·균열은 수관 너비를 개수만큼 나눈 칸마다 하나씩, 실루엣 가장자리에서 떨어진 안쪽에 놓아 한쪽에 몰리거나 가장자리에서 잘리지 않게 한다.
    /// 줄무늬는 그 열의 수관 윗면 바로 아래에서 시작해 몇 칸마다 한 칸씩 비켜 가며 흘러내린다. 균열은 나무 그림의 어두운 결을 따라 갈라진다.
    /// 독액은 밑동이 아닌 수관 아래 가장자리 열에 맺힌다.
    /// 좌표는 스프라이트 픽셀(아래 행 0, 위쪽 양수)이다. 만들 때만 할당하고(스프라이트당 한 번, 캐시), 재생 중에는 읽기만 한다.
    /// </summary>
    public sealed class TreeDebuffLayout
    {
        public const int MaxPatchSpans = 24;
        public const int MaxStreaks = 4;
        public const int MaxCracks = 3;
        public const int MaxCrackPixels = 40;
        public const int MaxDrips = 4;
        public const int MaxFlakeSpots = 24;

        // 각진 얼룩(행 단위 구간)
        public int patchSpanCount;
        public readonly int[] patchY = new int[MaxPatchSpans];
        public readonly int[] patchX0 = new int[MaxPatchSpans];
        public readonly int[] patchX1 = new int[MaxPatchSpans];

        // 흘러내린 점액 줄무늬(위에서 아래로 이어진 픽셀 경로). 앞쪽 streakWide칸은 두 칸 굵기(오른쪽 그늘), 끝은 방울 머리.
        public const int MaxStreakPixels = 18;
        public int streakCount;
        public readonly int[] streakX = new int[MaxStreaks];
        public readonly int[] streakTopY = new int[MaxStreaks];
        public readonly int[] streakLength = new int[MaxStreaks];
        public readonly int[,] streakPathX = new int[MaxStreaks, MaxStreakPixels];
        public readonly int[,] streakPathY = new int[MaxStreaks, MaxStreakPixels];
        public readonly int[] streakWide = new int[MaxStreaks];

        // 균열(마스크 안에 든 픽셀만)
        public int crackCount;
        public readonly int[] crackPixelCount = new int[MaxCracks];
        public readonly int[,] crackX = new int[MaxCracks, MaxCrackPixels];
        public readonly int[,] crackY = new int[MaxCracks, MaxCrackPixels];

        // 독액이 맺히는 곳(수관 아래 가장자리)
        public int dripCount;
        public readonly int[] dripX = new int[MaxDrips];
        public readonly int[] dripY = new int[MaxDrips];

        // 부패 조각이 떨어져 나오는 후보 위치(수관 안)
        public int flakeSpotCount;
        public readonly int[] flakeX = new int[MaxFlakeSpots];
        public readonly int[] flakeY = new int[MaxFlakeSpots];

        public int centerX;
        public int topY;
        public int bottomY;

        private static readonly Dictionary<Sprite, TreeDebuffLayout[]> cache = new Dictionary<Sprite, TreeDebuffLayout[]>(64);

        // 배치 중 이미 놓은 장식이 차지한 사각형(스프라이트 픽셀, 양 끝 포함). 얼룩·줄무늬·균열이 한 자리에 겹쳐 뭉치지 않게 한다. Build 안에서만 쓴다(메인 스레드).
        private const int MaxOccupied = 16;
        private static readonly int[] occX0 = new int[MaxOccupied];
        private static readonly int[] occX1 = new int[MaxOccupied];
        private static readonly int[] occY0 = new int[MaxOccupied];
        private static readonly int[] occY1 = new int[MaxOccupied];
        private static int occCount;

        public static TreeDebuffLayout Get(Sprite _sprite, TreeSilhouetteData _mask, int _tier, int _variant)
        {
            TreeDebuffLayout[] slots;
            if (false == cache.TryGetValue(_sprite, out slots))
            {
                slots = new TreeDebuffLayout[8];
                cache[_sprite] = slots;
            }

            int index = (_tier - 1) * 2 + (_variant & 1);
            if (null == slots[index])
            {
                slots[index] = new TreeDebuffLayout();
                slots[index].Build(_sprite, _mask, _tier, _variant);
            }

            return slots[index];
        }

        private static uint HashName(string _name)
        {
            uint h = 2166136261u;
            for (int i = 0; i < _name.Length; i++)
            {
                h = unchecked((h ^ _name[i]) * 16777619u);
            }

            return h;
        }

        private void Build(Sprite _sprite, TreeSilhouetteData _mask, int _tier, int _variant)
        {
            System.Random rng = new System.Random(unchecked((int)(HashName(_sprite.name) * 31u + (uint)_tier * 7u + (uint)_variant * 101u)));

            int minX = _mask.minX;
            int maxX = _mask.maxX;
            int minY = _mask.minY;
            int maxY = _mask.maxY;
            if (maxX < minX || maxY < minY)
            {
                minX = 0;
                maxX = Mathf.Max(0, _mask.width - 1);
                minY = 0;
                maxY = Mathf.Max(0, _mask.height - 1);
            }

            int h = maxY - minY;
            centerX = (minX + maxX) / 2;
            topY = maxY;
            bottomY = minY;
            occCount = 0;

            // 얼룩: 세로 가운데 구간, 수관 너비를 개수만큼 나눈 칸마다 하나(실루엣에서 4칸 안쪽)
            int patchTarget = _tier;
            patchSpanCount = 0;
            for (int p = 0; p < patchTarget; p++)
            {
                int cx;
                int cy;
                int sx0;
                int sx1;
                SlotRange(minX + 3, maxX - 3, p, patchTarget, out sx0, out sx1);
                int py0 = minY + h / 4;
                int py1 = maxY - h / 4;
                // 칸이 여럿이면 높이를 번갈아 두어 가로 한 줄로 늘어서지 않게 한다
                if (1 < patchTarget)
                {
                    int mid = (py0 + py1) / 2;
                    if (0 == ((p + _variant) & 1)) py0 = mid - 1;
                    else py1 = mid + 1;
                }

                PickInner(_mask, rng, sx0, sx1, py0, py1, 4, 3, 5, 3, 3, out cx, out cy);
                Occupy(cx - 5, cx + 5, cy - 3, cy + 3);
                for (int k = -3; k <= 3; k++)
                {
                    if (MaxPatchSpans <= patchSpanCount) break;
                    int hw = rng.Next(3, 6) - (3 == Mathf.Abs(k) ? 1 : 0);
                    patchY[patchSpanCount] = cy + k;
                    patchX0[patchSpanCount] = cx - hw + rng.Next(-1, 2);
                    patchX1[patchSpanCount] = cx + hw + rng.Next(-1, 2);
                    patchSpanCount++;
                }
            }

            // 줄무늬: 칸마다 하나, 그 열의 수관 윗면 바로 아래에서 시작해 아래로 흐른다(가장자리에서 2칸 안쪽)
            streakCount = Mathf.Min(MaxStreaks, 1 == _tier ? 1 : (2 == _tier ? 2 : 4));
            for (int s = 0; s < streakCount; s++)
            {
                int sx0;
                int sx1;
                SlotRange(minX + 3, maxX - 3, s, streakCount, out sx0, out sx1);
                int sx = rng.Next(sx0, sx1 + 1);
                int sy = -1;
                // 앞의 몇 번은 얼룩과 겹치지 않는 열을 찾고, 그래도 없으면 겹쳐도 된다
                for (int tries = 0; tries < 16 && sy < 0; tries++)
                {
                    int top = ColumnTop(_mask, sx, maxY);
                    int y = top - rng.Next(2, 5);
                    bool bFree = 10 <= tries || false == IsOccupied(sx - 1, sx + 2, y - 10, y);
                    if (0 <= top && y >= maxY - h / 2 && true == IsInner(_mask, sx, y, 2, 1) && true == bFree) sy = y;
                    else sx = rng.Next(sx0, sx1 + 1);
                }

                if (sy < 0) PickInner(_mask, rng, sx0, sx1, maxY - h / 2, maxY - 3, 2, 1, 1, 10, 2, out sx, out sy);
                streakX[s] = sx;
                streakTopY[s] = sy;
                TraceStreak(_mask, rng, s, sx, sy, rng.Next(8, 15));
                Occupy(sx - 1, sx + 2, sy - streakLength[s], sy);
            }

            // 균열: 윗부분에서 시작해 나무 그림의 어두운 결(잎 덩어리 사이 그늘·선)을 따라 한 칸씩 아래로 갈라진다.
            // 시작점은 칸마다 하나(실루엣에서 3칸 안쪽), 근처에서 가장 어두운 픽셀로 옮겨 시작한다.
            crackCount = Mathf.Min(MaxCracks, _tier);
            for (int c = 0; c < crackCount; c++)
            {
                int x;
                int y;
                int cx0;
                int cx1;
                SlotRange(minX + 3, maxX - 3, c, crackCount, out cx0, out cx1);
                PickInner(_mask, rng, cx0, cx1, maxY - h / 2, maxY - 3, 3, 2, 3, 12, 3, out x, out y);
                Occupy(x - 3, x + 3, y - 12, y);
                SnapToDark(_mask, ref x, ref y);
                crackPixelCount[c] = TraceCrack(_mask, rng, c, x, y, rng.Next(9, 15));
            }

            // 독액: 수관 아래 가장자리 열. 밑동(스프라이트 아래까지 이어진 열)과 옆 실루엣 끝은 빼고, 남은 열을 고르게 나눈 자리에 맺힌다.
            dripCount = 0;
            int want = 1 == _tier ? 2 : (2 == _tier ? 3 : 4);
            int validCount = 0;
            for (int x = minX + 2; x <= maxX - 2; x++)
            {
                if (true == IsDripColumn(_mask, x, minY)) validCount++;
            }

            for (int d = 0; d < want && dripCount < MaxDrips && 0 < validCount; d++)
            {
                int pick = Mathf.Clamp(Mathf.RoundToInt((d + 0.5f) * validCount / want), 0, validCount - 1);
                int seen = 0;
                for (int x = minX + 2; x <= maxX - 2; x++)
                {
                    if (false == IsDripColumn(_mask, x, minY)) continue;
                    if (seen++ != pick) continue;

                    dripX[dripCount] = x;
                    dripY[dripCount] = ColumnBottom(_mask, x) - 1;
                    dripCount++;
                    break;
                }
            }

            // 조각 후보
            flakeSpotCount = 0;
            for (int f = 0; f < MaxFlakeSpots; f++)
            {
                int x;
                int y;
                PickSolid(_mask, rng, minX, maxX, minY + 2, maxY - 2, out x, out y);
                flakeX[flakeSpotCount] = x;
                flakeY[flakeSpotCount] = y;
                flakeSpotCount++;
            }
        }

        // [_x0, _x1]을 _count칸으로 나눈 _index번째 칸(양 끝 포함).
        private static void SlotRange(int _x0, int _x1, int _index, int _count, out int _s0, out int _s1)
        {
            if (_x1 < _x0) _x1 = _x0;
            float width = (_x1 - _x0 + 1) / (float)Mathf.Max(1, _count);
            _s0 = _x0 + Mathf.FloorToInt(width * _index);
            _s1 = Mathf.Max(_s0, _x0 + Mathf.CeilToInt(width * (_index + 1)) - 1);
        }

        private static bool IsInner(TreeSilhouetteData _mask, int _x, int _y, int _marginX, int _marginY)
        {
            return true == _mask.IsSolid(_x, _y)
                && true == _mask.IsSolid(_x - _marginX, _y) && true == _mask.IsSolid(_x + _marginX, _y)
                && true == _mask.IsSolid(_x, _y - _marginY) && true == _mask.IsSolid(_x, _y + _marginY);
        }

        private static void Occupy(int _x0, int _x1, int _y0, int _y1)
        {
            if (MaxOccupied <= occCount) return;
            occX0[occCount] = _x0;
            occX1[occCount] = _x1;
            occY0[occCount] = _y0;
            occY1[occCount] = _y1;
            occCount++;
        }

        private static bool IsOccupied(int _x0, int _x1, int _y0, int _y1)
        {
            for (int i = 0; i < occCount; i++)
            {
                if (_x0 <= occX1[i] && occX0[i] <= _x1 && _y0 <= occY1[i] && occY0[i] <= _y1) return true;
            }

            return false;
        }

        // 실루엣에서 여백만큼 안쪽이고, 차지할 자리(점에서 좌우 _halfW, 아래로 _below, 위로 _above)가 이미 놓은 장식과 겹치지 않는 픽셀을 고른다.
        // 겹치지 않는 곳이 없으면 겹침은 허용하고, 그래도 없으면 여백 없이(예전 방식) 고른다.
        private static void PickInner(TreeSilhouetteData _mask, System.Random _rng, int _x0, int _x1, int _y0, int _y1, int _marginX, int _marginY, int _halfW, int _below, int _above, out int _x, out int _y)
        {
            int y0 = Mathf.Min(_y0, _y1);
            int y1 = Mathf.Max(_y0, _y1);
            for (int tries = 0; tries < 160; tries++)
            {
                int x = _rng.Next(_x0, _x1 + 1);
                int y = _rng.Next(y0, y1 + 1);
                if (tries < 100 && true == IsOccupied(x - _halfW, x + _halfW, y - _below, y + _above)) continue;
                if (true == IsInner(_mask, x, y, _marginX, _marginY))
                {
                    _x = x;
                    _y = y;
                    return;
                }
            }

            PickSolid(_mask, _rng, _x0, _x1, y0, y1, out _x, out _y);
        }

        private static int ColumnTop(TreeSilhouetteData _mask, int _x, int _fromY)
        {
            for (int y = _fromY; y >= 0; y--)
            {
                if (true == _mask.IsSolid(_x, y)) return y;
            }

            return -1;
        }

        private static int ColumnBottom(TreeSilhouetteData _mask, int _x)
        {
            for (int y = 0; y < _mask.height; y++)
            {
                if (true == _mask.IsSolid(_x, y)) return y;
            }

            return -1;
        }

        // 수관 아래 가장자리 열: 밑동처럼 스프라이트 아래까지 이어지지 않고(맨 아래 실루엣 행보다 4칸 이상 위), 양옆 열도 아래 가장자리가 비슷한 곳.
        private static bool IsDripColumn(TreeSilhouetteData _mask, int _x, int _minY)
        {
            int yb = ColumnBottom(_mask, _x);
            if (yb < _minY + 4) return false;
            int left = ColumnBottom(_mask, _x - 1);
            int right = ColumnBottom(_mask, _x + 1);
            return left >= _minY + 3 && right >= _minY + 3 && Mathf.Abs(left - yb) <= 2 && Mathf.Abs(right - yb) <= 2;
        }

        // 줄무늬 경로: 한 칸씩 아래로, 4~7칸마다 한 칸 옆으로 비켜 간다(실루엣 옆 가장자리 쪽으로는 가지 않는다). 아래가 비면 거기서 멈춘다.
        private void TraceStreak(TreeSilhouetteData _mask, System.Random _rng, int _s, int _x, int _y, int _length)
        {
            int count = 0;
            int x = _x;
            int y = _y;
            int straight = _rng.Next(4, 8);
            while (count < _length && count < MaxStreakPixels && true == _mask.IsSolid(x, y))
            {
                streakPathX[_s, count] = x;
                streakPathY[_s, count] = y;
                count++;

                if (--straight <= 0)
                {
                    straight = _rng.Next(4, 8);
                    int dir = _rng.Next(0, 2) * 2 - 1;
                    if (false == IsInner(_mask, x + dir, y - 1, 2, 0)) dir = -dir;
                    if (true == IsInner(_mask, x + dir, y - 1, 2, 0)) x += dir;
                }

                y--;
            }

            streakLength[_s] = count;
            streakWide[_s] = Mathf.Max(1, Mathf.RoundToInt(count * 0.55f));
        }

        private static void PickSolid(TreeSilhouetteData _mask, System.Random _rng, int _x0, int _x1, int _y0, int _y1, out int _x, out int _y)
        {
            int y0 = Mathf.Min(_y0, _y1);
            int y1 = Mathf.Max(_y0, _y1);
            for (int tries = 0; tries < 200; tries++)
            {
                int x = _rng.Next(_x0, _x1 + 1);
                int y = _rng.Next(y0, y1 + 1);
                if (true == _mask.IsSolid(x, y))
                {
                    _x = x;
                    _y = y;
                    return;
                }
            }

            _x = (_x0 + _x1) / 2;
            _y = (y0 + y1) / 2;
        }

        // 시작점 둘레 2칸 안(실루엣 2칸 안쪽)에서 가장 어두운 픽셀로 옮긴다.
        private static void SnapToDark(TreeSilhouetteData _mask, ref int _x, ref int _y)
        {
            int bx = _x;
            int by = _y;
            float best = _mask.Tone(_x, _y);
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = _x + dx;
                    int y = _y + dy;
                    if (false == IsInner(_mask, x, y, 2, 2)) continue;
                    float t = _mask.Tone(x, y);
                    if (t < best)
                    {
                        best = t;
                        bx = x;
                        by = y;
                    }
                }
            }

            _x = bx;
            _y = by;
        }

        // 균열 경로: 한 칸씩 아래로, 바로 아래·왼아래·오른아래 중 그림이 가장 어두운 쪽으로 간다(곧게 가는 쪽을 조금 더 좋아하고,
        // 처음 고른 방향의 반대로 꺾는 건 덜 좋아해 지그재그가 되지 않는다). 실루엣 가장자리 1칸은 피하고, 아래가 비면 거기서 멈춘다.
        private int TraceCrack(TreeSilhouetteData _mask, System.Random _rng, int _c, int _x, int _y, int _length)
        {
            int count = 0;
            int x = _x;
            int y = _y;
            int lean = _rng.Next(0, 2) * 2 - 1;
            int lastDx = 0;
            while (count < _length && count < MaxCrackPixels && true == _mask.IsSolid(x, y))
            {
                crackX[_c, count] = x;
                crackY[_c, count] = y;
                count++;

                int bestDx = 0;
                float best = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    if (false == IsInner(_mask, nx, y - 1, 1, 0)) continue;
                    float score = _mask.Tone(nx, y - 1);
                    if (0 != dx) score += 0.12f;
                    if (-lean == dx) score += 0.1f;
                    if (0 != dx && dx == lastDx) score += 0.15f;
                    if (score < best)
                    {
                        best = score;
                        bestDx = dx;
                    }
                }

                if (float.MaxValue == best) break;
                x += bestDx;
                lastDx = bestDx;
                y--;
            }

            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache.Clear();
        }
    }

    /// <summary>바닥 감염 링의 모양(단계별 반지름, 1px 미드포인트 타원 점 목록). 한 번만 만든다.</summary>
    public static class TreeDebuffRing
    {
        public static readonly int[] RadiusX = { 0, 18, 23, 28 };

        private static readonly int[][] pointX = new int[4][];
        private static readonly int[][] pointY = new int[4][];

        public static int RadiusY(int _tier)
        {
            return RadiusX[_tier] / 2;
        }

        public static int PointCount(int _tier)
        {
            Ensure(_tier);
            return pointX[_tier].Length;
        }

        public static int X(int _tier, int _index)
        {
            return pointX[_tier][_index];
        }

        public static int Y(int _tier, int _index)
        {
            return pointY[_tier][_index];
        }

        // 5번째마다 비워 점선 모양으로 만든다(기본 링 스프라이트와 강조 구간이 같은 규칙을 쓴다)
        public static bool IsGap(int _index)
        {
            return 4 == (_index % 5);
        }

        private static int CompareAngle(Vector3 _a, Vector3 _b)
        {
            return _a.z.CompareTo(_b.z);
        }

        private static void AddFour(List<Vector3> _pts, HashSet<long> _seen, int _x, int _y, int _rx, int _ry)
        {
            AddPoint(_pts, _seen, _x, _y, _rx, _ry);
            AddPoint(_pts, _seen, -_x, _y, _rx, _ry);
            AddPoint(_pts, _seen, _x, -_y, _rx, _ry);
            AddPoint(_pts, _seen, -_x, -_y, _rx, _ry);
        }

        private static void AddPoint(List<Vector3> _pts, HashSet<long> _seen, int _x, int _y, int _rx, int _ry)
        {
            long k = ((long)(_x + 512) << 16) | (long)(_y + 512);
            if (true == _seen.Contains(k)) return;
            _seen.Add(k);
            _pts.Add(new Vector3(_x, _y, Mathf.Atan2(_y / (float)_ry, _x / (float)_rx)));
        }

        private static void Ensure(int _tier)
        {
            if (null != pointX[_tier]) return;

            int rx = RadiusX[_tier];
            int ry = RadiusY(_tier);
            List<Vector3> pts = new List<Vector3>(256);
            HashSet<long> seen = new HashSet<long>();

            int x = 0;
            int y = ry;
            long rx2 = (long)rx * rx;
            long ry2 = (long)ry * ry;
            long dx = 0;
            long dy = 2 * rx2 * y;
            double d1 = ry2 - rx2 * ry + 0.25 * rx2;

            while (dx < dy)
            {
                AddFour(pts, seen, x, y, rx, ry);
                x++;
                dx += 2 * ry2;
                if (d1 < 0) d1 += dx + ry2;
                else
                {
                    y--;
                    dy -= 2 * rx2;
                    d1 += dx - dy + ry2;
                }
            }

            double d2 = ry2 * (x + 0.5) * (x + 0.5) + rx2 * (double)(y - 1) * (y - 1) - rx2 * ry2;
            while (0 <= y)
            {
                AddFour(pts, seen, x, y, rx, ry);
                y--;
                dy -= 2 * rx2;
                if (0 < d2) d2 += rx2 - dy;
                else
                {
                    x++;
                    dx += 2 * ry2;
                    d2 += dx - dy + rx2;
                }
            }

            pts.Sort(CompareAngle);
            pointX[_tier] = new int[pts.Count];
            pointY[_tier] = new int[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                pointX[_tier][i] = Mathf.RoundToInt(pts[i].x);
                pointY[_tier][i] = Mathf.RoundToInt(pts[i].y);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < pointX.Length; i++)
            {
                pointX[i] = null;
                pointY[i] = null;
            }
        }
    }
}
