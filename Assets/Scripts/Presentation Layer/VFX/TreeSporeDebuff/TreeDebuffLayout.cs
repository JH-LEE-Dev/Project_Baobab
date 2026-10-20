using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 한 나무 스프라이트(와 단계/변형)에 대한 디버프 장식 배치. 나무 실루엣 마스크 안의 불투명 픽셀에서 위치를 골라 만들고, 같은 입력이면 항상 같은 결과다.
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

        // 흘러내린 점액 줄무늬(맨 위 픽셀과 길이, 아래로 흐른다)
        public int streakCount;
        public readonly int[] streakX = new int[MaxStreaks];
        public readonly int[] streakTopY = new int[MaxStreaks];
        public readonly int[] streakLength = new int[MaxStreaks];

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

            // 얼룩: 마스크 안, 세로 가운데 구간
            int patchTarget = _tier;
            patchSpanCount = 0;
            for (int p = 0; p < patchTarget; p++)
            {
                int cx;
                int cy;
                PickSolid(_mask, rng, minX, maxX, minY + h / 4, maxY - h / 4, out cx, out cy);
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

            // 줄무늬: 수관 윗부분에서 시작해 아래로
            streakCount = Mathf.Min(MaxStreaks, 1 == _tier ? 1 : (2 == _tier ? 2 : 4));
            for (int s = 0; s < streakCount; s++)
            {
                int sx;
                int sy;
                PickSolid(_mask, rng, minX, maxX, maxY - h / 2, maxY - 3, out sx, out sy);
                streakX[s] = sx;
                streakTopY[s] = sy;
                streakLength[s] = rng.Next(7, 13);
            }

            // 균열: 윗부분에서 시작해 옆으로 틀며 아래로(마스크 밖 픽셀은 버린다)
            crackCount = Mathf.Min(MaxCracks, _tier);
            for (int c = 0; c < crackCount; c++)
            {
                int x;
                int y;
                PickSolid(_mask, rng, minX, maxX, maxY - h / 2, maxY - 3, out x, out y);
                int count = 0;
                int px = x;
                int py = y;
                for (int seg = 0; seg < 3; seg++)
                {
                    int nx = px + (0 == rng.Next(2) ? -1 : 1) * rng.Next(2, 5);
                    int ny = py - rng.Next(4, 8);
                    AppendLine(_mask, c, ref count, px, py, nx, ny);
                    px = nx;
                    py = ny;
                }

                crackPixelCount[c] = count;
            }

            // 독액: 수관 아래 가장자리 열
            dripCount = 0;
            int columns = maxX - minX - 5;
            int want = 1 == _tier ? 2 : (2 == _tier ? 3 : 4);
            int step = Mathf.Max(3, columns / (want + 1));
            for (int d = 0; d < want && dripCount < MaxDrips; d++)
            {
                int x = Mathf.Clamp(minX + 3 + (d + 1) * step, minX + 2, maxX - 2);
                int yb = -1;
                for (int y = 0; y < _mask.height; y++)
                {
                    if (true == _mask.IsSolid(x, y))
                    {
                        yb = y;
                        break;
                    }
                }

                if (yb < 0) continue;
                dripX[dripCount] = x;
                dripY[dripCount] = yb - 1;
                dripCount++;
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

        private void AppendLine(TreeSilhouetteData _mask, int _crack, ref int _count, int _x0, int _y0, int _x1, int _y1)
        {
            int dx = Mathf.Abs(_x1 - _x0);
            int dy = -Mathf.Abs(_y1 - _y0);
            int sx = _x0 < _x1 ? 1 : -1;
            int sy = _y0 < _y1 ? 1 : -1;
            int e = dx + dy;
            int x = _x0;
            int y = _y0;

            while (true)
            {
                if (_count < MaxCrackPixels && true == _mask.IsSolid(x, y))
                {
                    crackX[_crack, _count] = x;
                    crackY[_crack, _count] = y;
                    _count++;
                }

                if (x == _x1 && y == _y1) break;
                int e2 = 2 * e;
                if (e2 >= dy)
                {
                    e += dy;
                    x += sx;
                }

                if (e2 <= dx)
                {
                    e += dx;
                    y += sy;
                }
            }
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
