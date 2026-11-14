using System.Runtime.CompilerServices;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 절차적 픽셀 이펙트가 공용 PixelQuadBuffer에 그림을 그리는 도우미. 같은 줄에서 이어지는 같은 색 픽셀을 한 쿼드로 합치고,
    /// 그린 픽셀 범위를 모아 메쉬 경계를 좁게 잡으며, 직전과 같은 그림이면 업로드를 건너뛴다(가이드 10.2~10.3).
    ///
    /// 입자 모양은 5가지뿐이다: `.`(Dot) `+`(Plus) `x`(Cross) `●`(Circle) `◆`(Diamond).
    /// 선은 L자 모서리를 제거해 1px 굵기를 유지하고(DOUBLE PIXEL), 타원은 PixelEllipse의 midpoint 윤곽만 쓴다(JAGGIES).
    /// 좌표는 모두 월드 픽셀(32 PPU 정수 격자)이며 y가 위쪽이다.
    /// </summary>
    public sealed class PixelPainter
    {
        public const int ShapeDot = 0;
        public const int ShapePlus = 1;
        public const int ShapeCross = 2;
        public const int ShapeCircle = 3;
        public const int ShapeDiamond = 4;

        private const float PixelUnit = 1.0f / 32.0f;
        private const int LineCapacity = 2048;
        private const int SpacedCapacity = 128;
        private const float TwoPi = Mathf.PI * 2.0f;
        // RingArcWithHead 머리 뒤쪽 색 구간 경계(라디안). 0.04 / 0.1 / 0.2 바퀴.
        private const float HeadRadians0 = 0.04f * TwoPi;
        private const float HeadRadians1 = 0.1f * TwoPi;
        private const float HeadRadians2 = 0.2f * TwoPi;

        // RingPoint/RingArcWithHead용 윤곽 각도 표 칸 수. 표 하나는 (rx, ry) 하나의 윤곽 좌표와 점마다의 바닥 원 기준 각도다.
        private const int RingAngleTableCount = 4;
        private const int RingAngleInitialCapacity = 256;
        // RingPoint 구간 탐색: 윤곽 점을 각도 구간으로 나눠 두고 목표 각도 근처 구간만 본다.
        // 여유(라디안)는 RingPoint 식의 float 오차(3e-6 미만)보다 훨씬 크게 잡아, 여유 밖의 점이 원래 전체 탐색에서 뽑힐 수 없게 한다.
        private const int RingBucketCount = 64;
        private const double RingSearchMargin = 1e-3;
        // 점 목록 캐시 칸 수·열쇠 길이·처음 크기. 종류 0은 빈 칸이다.
        private const int DotListSlotCount = 8;
        private const int DotListKeyLength = 6;
        private const int DotListInitialCapacity = 256;
        private const int DotListKindRing = 1;
        private const int DotListKindLattice = 2;

        // LineCapacity / SpacedCapacity는 처음 크기다. 넘치면 두 배로 늘어난다.
        private static int[] linePixelX = new int[LineCapacity];
        private static int[] linePixelY = new int[LineCapacity];

        // 윤곽 각도 표(Atan2 결과 캐시). 각도는 (rx, ry)만으로 정해지므로 같은 링을 다시 그릴 때 Atan2를 다시 하지 않는다.
        // 값은 ClockAngle과 같은 식으로 한 번 계산해 담아 둔 것이라 매번 계산한 값과 비트까지 같다.
        // 칸이 모자라면 가장 오래 전에 채운 칸부터 덮어쓴다. 표 길이는 모자랄 때만 두 배로 늘어난다.
        private static readonly int[] ringTableRx = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
        private static readonly int[] ringTableRy = new int[RingAngleTableCount];
        private static readonly int[] ringTableCount = new int[RingAngleTableCount];
        private static readonly int[][] ringTableX = new int[RingAngleTableCount][];
        private static readonly int[][] ringTableY = new int[RingAngleTableCount][];
        private static readonly float[][] ringTableAngle = new float[RingAngleTableCount][];
        // 각도 구간별 점 번호(구간 순, 같은 구간 안에서는 번호 오름차순). 구간 b의 점은 ringTableBucketItems[t][start[b] .. start[b + 1]).
        private static readonly int[][] ringTableBucketStart = new int[RingAngleTableCount][];
        private static readonly int[][] ringTableBucketItems = new int[RingAngleTableCount][];
        private static readonly int[] ringBucketCursor = new int[RingBucketCount];
        private static int ringTableNext;

        // 점 목록 캐시(RingArcCached / LatticeBandCached). 같은 인자로 다시 그리면 원래 함수가 Dot을 부를 오프셋을 같은 순서로 그대로 다시 찍는다.
        // 열쇠는 결과를 정하는 인자 전부(종류 + 정수 5개, float 인자는 비트 그대로)라 열쇠가 같으면 점 목록도 같다.
        // 칸이 모자라면 가장 오래 전에 채운 칸부터 덮어쓴다. 목록 길이는 모자랄 때만 두 배로 늘어난다.
        private static readonly int[] dotListKey = new int[DotListSlotCount * DotListKeyLength];
        private static readonly int[] dotListCount = new int[DotListSlotCount];
        private static readonly int[][] dotListX = new int[DotListSlotCount][];
        private static readonly int[][] dotListY = new int[DotListSlotCount][];
        private static int dotListNext;

        // 내부 의존성
        private PixelQuadBuffer buffer;
        private int[] spacedMinX = new int[SpacedCapacity];
        private int[] spacedMinY = new int[SpacedCapacity];
        private int[] spacedMaxX = new int[SpacedCapacity];
        private int[] spacedMaxY = new int[SpacedCapacity];

        // 상태 변수
        private int spacedCount;
        private bool bRunActive;
        private int runX0;
        private int runX1;
        private int runY;
        private Color32 runColor;
        private int minX;
        private int minY;
        private int maxX;
        private int maxY;
        private float translationX;
        private float translationY;

        public int VertexCount => null != buffer ? buffer.VertexCount : 0;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 버퍼를 비우고 월드 -> 로컬 변환(회전/스케일 없는 평행이동)을 정한다. 이번 프레임의 그리기를 시작한다.
        /// </summary>
        public void Begin(PixelQuadBuffer _buffer, in Matrix4x4 _worldToLocal, float _meshZ)
        {
            buffer = _buffer;
            buffer.Clear();
            buffer.SetWorldTransform(_worldToLocal, _meshZ);

            translationX = _worldToLocal.m03;
            translationY = _worldToLocal.m13;
            bRunActive = false;
            spacedCount = 0;
            minX = int.MaxValue;
            minY = int.MaxValue;
            maxX = int.MinValue;
            maxY = int.MinValue;
        }

        /// <summary>
        /// 그리기를 끝내고 메쉬에 올린다. 직전과 정점 수와 내용 해시가 같으면 올리지 않는다. 경계는 실제로 그린 범위로 좁힌다.
        /// 그릴 것이 없으면 렌더러를 끈다. _hash/_vertexCount는 호출 측이 들고 있다가 재생 시작 때 -1로 초기화해야 한다.
        /// </summary>
        public void Commit(Mesh _mesh, MeshRenderer _renderer, ref ulong _hash, ref int _vertexCount)
        {
            FlushRun();

            bool bHasContent = 0 < buffer.VertexCount;
            if (_renderer.enabled != bHasContent) _renderer.enabled = bHasContent;

            if (false == bHasContent)
            {
                if (0 != _vertexCount)
                {
                    buffer.Upload(_mesh, 0);
                    _vertexCount = 0;
                    _hash = 0UL;
                }

                return;
            }

            if (buffer.VertexCount == _vertexCount && buffer.ContentHash == _hash) return;

            buffer.Upload(_mesh, 0);

            float x0 = minX * PixelUnit + translationX - PixelUnit;
            float y0 = minY * PixelUnit + translationY - PixelUnit;
            float x1 = maxX * PixelUnit + translationX + PixelUnit;
            float y1 = maxY * PixelUnit + translationY + PixelUnit;
            _mesh.bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0.0f), new Vector3(x1 - x0, y1 - y0, 1.0f));

            _vertexCount = buffer.VertexCount;
            _hash = buffer.ContentHash;
        }

        // 그리기 도우미

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dot(int _x, int _y, Color32 _color)
        {
            AddRun(_x, _x + 1, _y, _color);
        }

        /// <summary>같은 줄 [_x0, _x1] (양끝 포함)</summary>
        public void HLine(int _x0, int _x1, int _y, Color32 _color)
        {
            if (_x1 < _x0) return;
            AddRun(_x0, _x1 + 1, _y, _color);
        }

        /// <summary>직사각형 [_x0, _x1) x [_y0, _y1)</summary>
        public void Rect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (_x1 <= _x0 || _y1 <= _y0) return;

            FlushRun();
            Emit(_x0, _y0, _x1, _y1, _color);
        }

        /// <summary>
        /// 입자 하나. `.` 1px, `+` 3x3 십자, `x` 3x3 대각 십자, `●` 4x4(모서리 없음), `◆` 5x5 마름모.
        /// </summary>
        public void Shape(int _shape, int _x, int _y, Color32 _color)
        {
            switch (_shape)
            {
                case ShapePlus:
                    HLine(_x, _x, _y - 1, _color);
                    HLine(_x - 1, _x + 1, _y, _color);
                    HLine(_x, _x, _y + 1, _color);
                    break;

                case ShapeCross:
                    Dot(_x - 1, _y - 1, _color);
                    Dot(_x + 1, _y - 1, _color);
                    Dot(_x, _y, _color);
                    Dot(_x - 1, _y + 1, _color);
                    Dot(_x + 1, _y + 1, _color);
                    break;

                case ShapeCircle:
                    HLine(_x - 1, _x, _y - 2, _color);
                    HLine(_x - 2, _x + 1, _y - 1, _color);
                    HLine(_x - 2, _x + 1, _y, _color);
                    HLine(_x - 1, _x, _y + 1, _color);
                    break;

                case ShapeDiamond:
                    HLine(_x, _x, _y - 2, _color);
                    HLine(_x - 1, _x + 1, _y - 1, _color);
                    HLine(_x - 2, _x + 2, _y, _color);
                    HLine(_x - 1, _x + 1, _y + 1, _color);
                    HLine(_x, _x, _y + 2, _color);
                    break;

                default:
                    Dot(_x, _y, _color);
                    break;
            }
        }

        /// <summary>
        /// Shape와 같은 입자를 그리되, 이번 프레임(Begin 이후)에 이미 SpacedShape로 그린 입자와 겹치거나 맞닿으면
        /// `.`로 줄여 그리고, `.`도 맞닿으면 그리지 않는다. 서로 다른 입자가 이어 붙어 한 덩어리의 뚱뚱한 모양(`x`와 `+`의 합집합 등)이
        /// 되는 것을 막는다. 먼저 그린 입자가 우선이므로, 입자 배열 순서가 고정이면 같은 프레임 결과도 고정이다.
        /// </summary>
        public void SpacedShape(int _shape, int _x, int _y, Color32 _color)
        {
            int shape = _shape;

            if (false == TryReserveShape(shape, _x, _y))
            {
                if (ShapeDot == shape || false == TryReserveShape(ShapeDot, _x, _y)) return;
                shape = ShapeDot;
            }

            Shape(shape, _x, _y, _color);
        }

        /// <summary>
        /// midpoint 윤곽 위의 링. _dashOff가 0이면 끊김 없는 실선이고, 0보다 크면 윤곽 픽셀 개수 기준으로 _dashOn칸 켜고 _dashOff칸 끄는
        /// 점선이다(점선 길이가 둘레 어디서나 같고, 윤곽 한 바퀴를 주기로 고르게 나눠 시작점에서 잘리지 않는다).
        /// _dashShift(윤곽 픽셀)를 키우면 점선이 화면 시계 방향으로 한 칸씩 흐른다.
        /// </summary>
        public void RingArc(int _cx, int _cy, int _rx, int _ry, int _dashOn, int _dashOff, float _dashShift, Color32 _color)
        {
            int count = PixelEllipse.Build(_rx, _ry);
            int period = _dashOn + _dashOff;
            int shift = Mathf.FloorToInt(_dashShift);
            float cellScale = 0 < _dashOff ? Mathf.Max(1, Mathf.RoundToInt(count / (float)period)) / (float)count : 0.0f;
            float duty = 0 < _dashOff ? _dashOn / (float)period : 1.0f;

            for (int i = 0; i < count; i++)
            {
                if (0 < _dashOff && Mathf.Repeat((i + shift) * cellScale, 1.0f) >= duty) continue;
                Dot(_cx + PixelEllipse.OutX[i], _cy + PixelEllipse.OutY[i], _color);
            }
        }

        /// <summary>
        /// RingArc에 밝은 구간(머리)을 더한다. _headAngle(라디안, 시계 방향)에서 뒤쪽으로 가까운 점일수록 밝은 색을 쓴다(_c0 가장 밝음 ~ _c3 기본).
        /// 머리는 바닥 원 기준 각도라 속도가 둘레 어디서나 같다.
        /// </summary>
        /// <remarks>윤곽은 각도 표에서 읽으므로 PixelEllipse 공용 버퍼(OutX/OutY)를 다시 채운다는 보장이 없다.</remarks>
        public void RingArcWithHead(int _cx, int _cy, int _rx, int _ry, int _dashOn, int _dashOff, float _dashShift, float _headAngle, Color32 _c0, Color32 _c1, Color32 _c2, Color32 _c3)
        {
            int table = GetRingAngleTable(_rx, _ry);
            int count = ringTableCount[table];
            int[] ringX = ringTableX[table];
            int[] ringY = ringTableY[table];
            float[] ringAngle = ringTableAngle[table];
            int period = _dashOn + _dashOff;
            int shift = Mathf.FloorToInt(_dashShift);
            float cellScale = 0 < _dashOff ? Mathf.Max(1, Mathf.RoundToInt(count / (float)period)) / (float)count : 0.0f;
            float duty = 0 < _dashOff ? _dashOn / (float)period : 1.0f;

            for (int i = 0; i < count; i++)
            {
                if (0 < _dashOff && Mathf.Repeat((i + shift) * cellScale, 1.0f) >= duty) continue;

                float behind = Mathf.Repeat(_headAngle - ringAngle[i], TwoPi);
                Color32 color = behind < HeadRadians0 ? _c0 : (behind < HeadRadians1 ? _c1 : (behind < HeadRadians2 ? _c2 : _c3));
                Dot(_cx + ringX[i], _cy + ringY[i], color);
            }
        }

        /// <summary>
        /// 링 윤곽 위에서 _clockAngle(라디안, 동쪽 0, 화면 시계 방향)에 가장 가까운 픽셀의 오프셋. 표식과 꼬리 점을 링 선 위에 정확히 얹을 때 쓴다.
        /// 윤곽은 각도 표에서 읽으므로 PixelEllipse 공용 버퍼(OutX/OutY)를 다시 채운다는 보장이 없다(그 버퍼를 읽으려면 먼저 Build를 부른다).
        /// </summary>
        public static void RingPoint(int _rx, int _ry, float _clockAngle, out int _x, out int _y)
        {
            int table = GetRingAngleTable(_rx, _ry);
            int best = FindNearestInBuckets(table, _clockAngle);
            if (0 > best) best = FindNearestLinear(table, _clockAngle);

            _x = ringTableX[table][best];
            _y = ringTableY[table][best];
        }

        // 원래 방식: 모든 윤곽 점에서 차이가 가장 작은 점(같으면 앞 번호).
        private static int FindNearestLinear(int _table, float _clockAngle)
        {
            int count = ringTableCount[_table];
            float[] ringAngle = ringTableAngle[_table];
            int best = 0;
            float bestDiff = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                float diff = Mathf.Abs(Mathf.Repeat(ringAngle[i] - _clockAngle + Mathf.PI, TwoPi) - Mathf.PI);
                if (diff >= bestDiff) continue;

                bestDiff = diff;
                best = i;
            }

            return best;
        }

        // 구간 탐색. FindNearestLinear와 같은 점을 돌려준다(못 정하면 -1, 그때는 원래 방식으로 찾는다).
        // 1) 목표 각도의 구간에서 바깥으로 넓혀 가며 처음 점이 있는 구간을 찾아, 그 점들의 실제 원형 거리로 임시 최단 거리를 정한다.
        //    진짜 최단 거리는 이보다 길 수 없다.
        // 2) 원래 식(float)의 오차는 3e-6 미만이므로, 실제 거리가 (임시 최단 거리 + 여유)보다 먼 점은 원래 방식에서도
        //    가장 가까운 점보다 차이가 커서 뽑히지 않고 동점도 될 수 없다. 그 범위를 덮는 구간(경계 반올림 대비 한 칸씩 더)의 점만
        //    원래 식으로 계산해, 차이가 가장 작은 점(같으면 앞 번호)을 고른다. 원래 방식의 선택 규칙과 같다.
        //    범위가 링 절반 이상을 덮으면(아주 작은 링) 이득이 없으므로 원래 방식으로 넘긴다.
        private static int FindNearestInBuckets(int _table, float _clockAngle)
        {
            int[] start = ringTableBucketStart[_table];
            int[] items = ringTableBucketItems[_table];
            float[] ringAngle = ringTableAngle[_table];
            double period = TwoPi;
            double bucketWidth = period / RingBucketCount;
            int home = BucketOf(_clockAngle);

            double provisional = double.MaxValue;
            for (int step = 0; step <= RingBucketCount / 2 && double.MaxValue == provisional; step++)
            {
                provisional = System.Math.Min(provisional, BucketMinDistance(start, items, ringAngle, (home + step) % RingBucketCount, _clockAngle, period));
                if (0 < step)
                {
                    provisional = System.Math.Min(provisional, BucketMinDistance(start, items, ringAngle, (home - step + RingBucketCount) % RingBucketCount, _clockAngle, period));
                }
            }

            if (double.MaxValue == provisional) return -1;

            // 구간 번호는 float로 계산하므로 경계 근처에서 양쪽 각도가 한 칸씩 어긋날 수 있다. 그만큼(두 칸) 더 본다.
            int span = (int)System.Math.Ceiling((provisional + RingSearchMargin) / bucketWidth) + 2;
            if (RingBucketCount <= span * 2 + 1) return -1;

            int best = int.MaxValue;
            float bestDiff = float.MaxValue;
            for (int k = -span; k <= span; k++)
            {
                int bucket = ((home + k) % RingBucketCount + RingBucketCount) % RingBucketCount;
                for (int n = start[bucket]; n < start[bucket + 1]; n++)
                {
                    int i = items[n];
                    float diff = Mathf.Abs(Mathf.Repeat(ringAngle[i] - _clockAngle + Mathf.PI, TwoPi) - Mathf.PI);
                    if (diff > bestDiff || (diff == bestDiff && i > best)) continue;

                    bestDiff = diff;
                    best = i;
                }
            }

            return int.MaxValue == best ? -1 : best;
        }

        // 각도(0~2pi, 끝값 포함)가 속한 구간 번호
        private static int BucketOf(float _angle)
        {
            int bucket = (int)(_angle * (RingBucketCount / TwoPi));
            return bucket < 0 ? 0 : (RingBucketCount <= bucket ? RingBucketCount - 1 : bucket);
        }

        // 구간 _bucket 안 점들과 _clockAngle 사이의 실제 원형 거리(배정밀도) 최솟값. 점이 없으면 double.MaxValue.
        private static double BucketMinDistance(int[] _start, int[] _items, float[] _ringAngle, int _bucket, float _clockAngle, double _period)
        {
            double best = double.MaxValue;
            for (int n = _start[_bucket]; n < _start[_bucket + 1]; n++)
            {
                double d = ((double)_ringAngle[_items[n]] - _clockAngle) % _period;
                if (d < 0.0) d += _period;
                if (_period - d < d) d = _period - d;
                if (d < best) best = d;
            }
            return best;
        }

        // (_rx, _ry) 윤곽의 각도 표 칸 번호. 없으면 가장 오래 전에 채운 칸에 PixelEllipse.Build 결과와 점마다의 ClockAngle을 담는다.
        // 칸 열쇠는 호출 측이 넘긴 반지름 그대로다(ClockAngle이 보정 전 _rx, _ry로 계산하므로).
        private static int GetRingAngleTable(int _rx, int _ry)
        {
            for (int t = 0; t < RingAngleTableCount; t++)
            {
                if (ringTableRx[t] == _rx && ringTableRy[t] == _ry) return t;
            }

            int slot = ringTableNext;
            ringTableNext = (ringTableNext + 1) % RingAngleTableCount;

            int count = PixelEllipse.Build(_rx, _ry);
            if (null == ringTableX[slot] || ringTableX[slot].Length < count)
            {
                int capacity = null == ringTableX[slot] ? RingAngleInitialCapacity : ringTableX[slot].Length * 2;
                while (capacity < count) capacity *= 2;
                ringTableX[slot] = new int[capacity];
                ringTableY[slot] = new int[capacity];
                ringTableAngle[slot] = new float[capacity];
            }

            if (null == ringTableBucketStart[slot]) ringTableBucketStart[slot] = new int[RingBucketCount + 1];
            if (null == ringTableBucketItems[slot] || ringTableBucketItems[slot].Length < ringTableX[slot].Length) ringTableBucketItems[slot] = new int[ringTableX[slot].Length];

            int[] ringX = ringTableX[slot];
            int[] ringY = ringTableY[slot];
            float[] ringAngle = ringTableAngle[slot];
            int[] bucketStart = ringTableBucketStart[slot];
            int[] bucketItems = ringTableBucketItems[slot];
            System.Array.Clear(bucketStart, 0, bucketStart.Length);
            for (int i = 0; i < count; i++)
            {
                ringX[i] = PixelEllipse.OutX[i];
                ringY[i] = PixelEllipse.OutY[i];
                ringAngle[i] = ClockAngle(i, _rx, _ry);
                bucketStart[BucketOf(ringAngle[i]) + 1]++;
            }

            // 구간별로 모으기(계수 정렬, 같은 구간 안에서는 번호 오름차순 유지)
            for (int b = 0; b < RingBucketCount; b++)
            {
                bucketStart[b + 1] += bucketStart[b];
                ringBucketCursor[b] = bucketStart[b];
            }
            for (int i = 0; i < count; i++)
            {
                int bucket = BucketOf(ringAngle[i]);
                bucketItems[ringBucketCursor[bucket]++] = i;
            }

            ringTableRx[slot] = _rx;
            ringTableRy[slot] = _ry;
            ringTableCount[slot] = count;
            return slot;
        }

        // 열쇠가 같은 점 목록 칸 번호. 없으면 -1.
        private static int FindDotList(int _kind, int _a, int _b, int _c, int _d, int _e)
        {
            for (int s = 0; s < DotListSlotCount; s++)
            {
                int k = s * DotListKeyLength;
                if (dotListKey[k] == _kind && dotListKey[k + 1] == _a && dotListKey[k + 2] == _b
                    && dotListKey[k + 3] == _c && dotListKey[k + 4] == _d && dotListKey[k + 5] == _e) return s;
            }
            return -1;
        }

        // 가장 오래 전에 채운 칸을 비우고 열쇠를 적는다. 점은 AddDotListPoint로 채운다.
        private static int BeginDotList(int _kind, int _a, int _b, int _c, int _d, int _e)
        {
            int slot = dotListNext;
            dotListNext = (dotListNext + 1) % DotListSlotCount;

            int k = slot * DotListKeyLength;
            dotListKey[k] = _kind;
            dotListKey[k + 1] = _a;
            dotListKey[k + 2] = _b;
            dotListKey[k + 3] = _c;
            dotListKey[k + 4] = _d;
            dotListKey[k + 5] = _e;
            dotListCount[slot] = 0;
            if (null == dotListX[slot])
            {
                dotListX[slot] = new int[DotListInitialCapacity];
                dotListY[slot] = new int[DotListInitialCapacity];
            }
            return slot;
        }

        private static void AddDotListPoint(int _slot, int _x, int _y)
        {
            int count = dotListCount[_slot];
            if (dotListX[_slot].Length == count)
            {
                System.Array.Resize(ref dotListX[_slot], count * 2);
                System.Array.Resize(ref dotListY[_slot], count * 2);
            }
            dotListX[_slot][count] = _x;
            dotListY[_slot][count] = _y;
            dotListCount[_slot] = count + 1;
        }

        private void DrawDotList(int _slot, int _cx, int _cy, Color32 _color)
        {
            int count = dotListCount[_slot];
            int[] xs = dotListX[_slot];
            int[] ys = dotListY[_slot];
            for (int i = 0; i < count; i++) Dot(_cx + xs[i], _cy + ys[i], _color);
        }

        // 윤곽 점 _index의 바닥 원 기준 각도(동쪽 0, 화면 시계 방향으로 증가, 0~2pi). 직전 PixelEllipse.Build 결과를 읽는다.
        private static float ClockAngle(int _index, int _rx, int _ry)
        {
            float angle = -Mathf.Atan2(PixelEllipse.OutY[_index] * (float)_rx, PixelEllipse.OutX[_index] * (float)_ry);
            return angle < 0.0f ? angle + TwoPi : angle;
        }

        /// <summary>
        /// 타원 바깥 가장자리에서 _bandWidth px 안쪽까지의 띠를 체커 도트로 채운다(충격파가 지나가는 자리).
        /// </summary>
        public void DitherBand(int _cx, int _cy, int _radius, int _bandWidth, float _squashY, Color32 _color)
        {
            int ry = Mathf.Max(1, Mathf.RoundToInt(_radius * _squashY));
            int innerRadius = _radius - _bandWidth;
            float innerRy = innerRadius * _squashY;

            for (int y = -ry; y <= ry; y++)
            {
                float ny = y / (ry + 0.5f);
                float rest = 1.0f - ny * ny;
                if (rest < 0.0f) continue;

                int outer = Mathf.FloorToInt((_radius + 0.5f) * Mathf.Sqrt(rest));
                int inner = 0;
                if (0 < innerRadius && Mathf.Abs(y) < innerRy)
                {
                    float iny = y / (innerRy + 0.5f);
                    inner = Mathf.Max(0, Mathf.CeilToInt((innerRadius + 0.5f) * Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - iny * iny))));
                }

                for (int x = inner; x <= outer; x++)
                {
                    if (0 != ((x + y) & 1)) continue;

                    Dot(_cx + x, _cy + y, _color);
                    if (0 != x) Dot(_cx - x, _cy + y, _color);
                }
            }
        }

        /// <summary>
        /// 타원 바깥 가장자리에서 _bandWidth px 안쪽까지의 띠에 성긴 격자(_period칸마다 한 점)로 도트를 깐다(은은한 바닥 표시용).
        /// </summary>
        public void LatticeBand(int _cx, int _cy, int _radius, int _bandWidth, float _squashY, int _period, Color32 _color)
        {
            int ry = Mathf.Max(1, Mathf.RoundToInt(_radius * _squashY));
            int innerRadius = _radius - _bandWidth;
            float innerRy = innerRadius * _squashY;

            for (int y = -ry; y <= ry; y++)
            {
                float ny = y / (ry + 0.5f);
                float rest = 1.0f - ny * ny;
                if (rest < 0.0f) continue;

                int outer = Mathf.FloorToInt((_radius + 0.5f) * Mathf.Sqrt(rest));
                int inner = -outer;
                if (0 < innerRadius && Mathf.Abs(y) < innerRy)
                {
                    float iny = y / (innerRy + 0.5f);
                    inner = Mathf.CeilToInt((innerRadius + 0.5f) * Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - iny * iny)));
                }

                for (int x = -outer; x <= outer; x++)
                {
                    if (-inner < x && x < inner) { x = inner - 1; continue; }
                    if (0 != ((x + 2 * y) % _period + _period) % _period) continue;

                    Dot(_cx + x, _cy + y, _color);
                }
            }
        }

        /// <summary>
        /// RingArc와 같은 점을 같은 순서로 찍는다. 같은 (_rx, _ry, 점선, 이동 칸)으로 다시 그리면 윤곽·점선 계산을 건너뛰고 기억해 둔 점 목록을 쓴다.
        /// 크기가 매 프레임 바뀌는 링에는 이득이 없으므로 RingArc를 쓴다. PixelEllipse 공용 버퍼(OutX/OutY)를 다시 채운다는 보장이 없다.
        /// </summary>
        public void RingArcCached(int _cx, int _cy, int _rx, int _ry, int _dashOn, int _dashOff, float _dashShift, Color32 _color)
        {
            int shift = Mathf.FloorToInt(_dashShift);
            int slot = FindDotList(DotListKindRing, _rx, _ry, _dashOn, _dashOff, shift);
            if (0 > slot)
            {
                // RingArc 본문과 같은 계산(Dot 대신 목록에 담는다)
                slot = BeginDotList(DotListKindRing, _rx, _ry, _dashOn, _dashOff, shift);
                int count = PixelEllipse.Build(_rx, _ry);
                int period = _dashOn + _dashOff;
                float cellScale = 0 < _dashOff ? Mathf.Max(1, Mathf.RoundToInt(count / (float)period)) / (float)count : 0.0f;
                float duty = 0 < _dashOff ? _dashOn / (float)period : 1.0f;

                for (int i = 0; i < count; i++)
                {
                    if (0 < _dashOff && Mathf.Repeat((i + shift) * cellScale, 1.0f) >= duty) continue;
                    AddDotListPoint(slot, PixelEllipse.OutX[i], PixelEllipse.OutY[i]);
                }
            }

            DrawDotList(slot, _cx, _cy, _color);
        }

        /// <summary>
        /// LatticeBand와 같은 점을 같은 순서로 찍는다. 같은 (_radius, _bandWidth, _squashY, _period)로 다시 그리면 띠 계산을 건너뛰고 기억해 둔 점 목록을 쓴다.
        /// 크기가 매 프레임 바뀌는 띠에는 이득이 없으므로 LatticeBand를 쓴다.
        /// </summary>
        public void LatticeBandCached(int _cx, int _cy, int _radius, int _bandWidth, float _squashY, int _period, Color32 _color)
        {
            int squashBits = System.BitConverter.SingleToInt32Bits(_squashY);
            int slot = FindDotList(DotListKindLattice, _radius, _bandWidth, squashBits, _period, 0);
            if (0 > slot)
            {
                // LatticeBand 본문과 같은 계산(Dot 대신 목록에 담는다)
                slot = BeginDotList(DotListKindLattice, _radius, _bandWidth, squashBits, _period, 0);
                int ry = Mathf.Max(1, Mathf.RoundToInt(_radius * _squashY));
                int innerRadius = _radius - _bandWidth;
                float innerRy = innerRadius * _squashY;

                for (int y = -ry; y <= ry; y++)
                {
                    float ny = y / (ry + 0.5f);
                    float rest = 1.0f - ny * ny;
                    if (rest < 0.0f) continue;

                    int outer = Mathf.FloorToInt((_radius + 0.5f) * Mathf.Sqrt(rest));
                    int inner = -outer;
                    if (0 < innerRadius && Mathf.Abs(y) < innerRy)
                    {
                        float iny = y / (innerRy + 0.5f);
                        inner = Mathf.CeilToInt((innerRadius + 0.5f) * Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - iny * iny)));
                    }

                    for (int x = -outer; x <= outer; x++)
                    {
                        if (-inner < x && x < inner) { x = inner - 1; continue; }
                        if (0 != ((x + 2 * y) % _period + _period) % _period) continue;

                        AddDotListPoint(slot, x, y);
                    }
                }
            }

            DrawDotList(slot, _cx, _cy, _color);
        }

        /// <summary>
        /// 정수 격자 직선(Bresenham) + L자 모서리 제거. _dashOn/_dashOff가 0보다 크면 점선(칸 단위)이다.
        /// </summary>
        public void Line(int _x0, int _y0, int _x1, int _y1, int _dashOn, int _dashOff, int _phase, Color32 _color)
        {
            int count = BuildLine(_x0, _y0, _x1, _y1);
            int period = _dashOn + _dashOff;

            for (int i = 0; i < count; i++)
            {
                if (0 < _dashOff && ((i + _phase) % period + period) % period >= _dashOn) continue;
                Dot(linePixelX[i], linePixelY[i], _color);
            }
        }

        // 내부 메서드

        // 모양의 점유 상자(중심 기준 -left..+right, -down..+up)가 기록된 상자와 1칸 이내로 맞닿지 않으면 기록하고 true를 돌려준다.
        private bool TryReserveShape(int _shape, int _x, int _y)
        {
            int left = 0;
            int right = 0;
            int down = 0;
            int up = 0;

            switch (_shape)
            {
                case ShapePlus:
                case ShapeCross:
                    left = 1;
                    right = 1;
                    down = 1;
                    up = 1;
                    break;

                case ShapeCircle:
                    left = 2;
                    right = 1;
                    down = 2;
                    up = 1;
                    break;

                case ShapeDiamond:
                    left = 2;
                    right = 2;
                    down = 2;
                    up = 2;
                    break;
            }

            int x0 = _x - left;
            int x1 = _x + right;
            int y0 = _y - down;
            int y1 = _y + up;

            for (int i = 0; i < spacedCount; i++)
            {
                if (x0 <= spacedMaxX[i] + 1 && spacedMinX[i] - 1 <= x1 && y0 <= spacedMaxY[i] + 1 && spacedMinY[i] - 1 <= y1) return false;
            }

            // 기록 상자가 꽉 차면 입자를 버리지 않고 네 배열을 함께 두 배로 늘린다
            if (spacedMinX.Length <= spacedCount)
            {
                int grown = spacedMinX.Length * 2;
                System.Array.Resize(ref spacedMinX, grown);
                System.Array.Resize(ref spacedMinY, grown);
                System.Array.Resize(ref spacedMaxX, grown);
                System.Array.Resize(ref spacedMaxY, grown);
            }

            spacedMinX[spacedCount] = x0;
            spacedMinY[spacedCount] = y0;
            spacedMaxX[spacedCount] = x1;
            spacedMaxY[spacedCount] = y1;
            spacedCount++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushRun()
        {
            if (false == bRunActive) return;

            bRunActive = false;
            Emit(runX0, runY, runX1, runY + 1, runColor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddRun(int _x0, int _x1Exclusive, int _y, Color32 _color)
        {
            if (true == bRunActive && runY == _y && runX1 == _x0 && true == SameColor(runColor, _color))
            {
                runX1 = _x1Exclusive;
                return;
            }

            FlushRun();
            bRunActive = true;
            runX0 = _x0;
            runX1 = _x1Exclusive;
            runY = _y;
            runColor = _color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Emit(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            buffer.AddWorldRect(_x0, _y0, _x1, _y1, _color);

            if (_x0 < minX) minX = _x0;
            if (_y0 < minY) minY = _y0;
            if (maxX < _x1) maxX = _x1;
            if (maxY < _y1) maxY = _y1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SameColor(Color32 _a, Color32 _b)
        {
            return _a.r == _b.r && _a.g == _b.g && _a.b == _b.b && _a.a == _b.a;
        }

        private static int BuildLine(int _x0, int _y0, int _x1, int _y1)
        {
            int dx = Mathf.Abs(_x1 - _x0);
            int dy = Mathf.Abs(_y1 - _y0);
            int sx = _x0 < _x1 ? 1 : -1;
            int sy = _y0 < _y1 ? 1 : -1;
            int error = dx - dy;
            int x = _x0;
            int y = _y0;
            int n = 0;

            while (true)
            {
                // 선이 버퍼보다 길면 통째로 버리지 않고 버퍼를 두 배로 늘린다
                if (linePixelX.Length <= n)
                {
                    System.Array.Resize(ref linePixelX, linePixelX.Length * 2);
                    System.Array.Resize(ref linePixelY, linePixelY.Length * 2);
                }

                linePixelX[n] = x;
                linePixelY[n] = y;
                n++;

                if (x == _x1 && y == _y1) break;

                int doubled = 2 * error;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += sx;
                }

                if (doubled < dx)
                {
                    error += dx;
                    y += sy;
                }
            }

            // 앞뒤 픽셀이 이미 이어져 있으면 가운데(L자 모서리)를 제거한다
            int w = 0;
            for (int i = 0; i < n; i++)
            {
                if (0 < i && i < n - 1 && 0 < w)
                {
                    int prevX = linePixelX[w - 1];
                    int prevY = linePixelY[w - 1];
                    if (Mathf.Abs(linePixelX[i + 1] - prevX) <= 1 && Mathf.Abs(linePixelY[i + 1] - prevY) <= 1) continue;
                }

                linePixelX[w] = linePixelX[i];
                linePixelY[w] = linePixelY[i];
                w++;
            }

            return w;
        }
    }
}
