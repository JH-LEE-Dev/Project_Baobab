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

        private static readonly int[] linePixelX = new int[LineCapacity];
        private static readonly int[] linePixelY = new int[LineCapacity];

        // 내부 의존성
        private PixelQuadBuffer buffer;
        private readonly int[] spacedMinX = new int[SpacedCapacity];
        private readonly int[] spacedMinY = new int[SpacedCapacity];
        private readonly int[] spacedMaxX = new int[SpacedCapacity];
        private readonly int[] spacedMaxY = new int[SpacedCapacity];

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
        public void RingArcWithHead(int _cx, int _cy, int _rx, int _ry, int _dashOn, int _dashOff, float _dashShift, float _headAngle, Color32 _c0, Color32 _c1, Color32 _c2, Color32 _c3)
        {
            int count = PixelEllipse.Build(_rx, _ry);
            int period = _dashOn + _dashOff;
            int shift = Mathf.FloorToInt(_dashShift);
            float cellScale = 0 < _dashOff ? Mathf.Max(1, Mathf.RoundToInt(count / (float)period)) / (float)count : 0.0f;
            float duty = 0 < _dashOff ? _dashOn / (float)period : 1.0f;

            for (int i = 0; i < count; i++)
            {
                if (0 < _dashOff && Mathf.Repeat((i + shift) * cellScale, 1.0f) >= duty) continue;

                float behind = Mathf.Repeat(_headAngle - ClockAngle(i, _rx, _ry), TwoPi);
                Color32 color = behind < HeadRadians0 ? _c0 : (behind < HeadRadians1 ? _c1 : (behind < HeadRadians2 ? _c2 : _c3));
                Dot(_cx + PixelEllipse.OutX[i], _cy + PixelEllipse.OutY[i], color);
            }
        }

        /// <summary>
        /// 링 윤곽 위에서 _clockAngle(라디안, 동쪽 0, 화면 시계 방향)에 가장 가까운 픽셀의 오프셋. 표식과 꼬리 점을 링 선 위에 정확히 얹을 때 쓴다.
        /// PixelEllipse 공용 버퍼를 다시 채우므로 같은 링을 그리는 도중에는 부르지 않는다.
        /// </summary>
        public static void RingPoint(int _rx, int _ry, float _clockAngle, out int _x, out int _y)
        {
            int count = PixelEllipse.Build(_rx, _ry);
            int best = 0;
            float bestDiff = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                float diff = Mathf.Abs(Mathf.Repeat(ClockAngle(i, _rx, _ry) - _clockAngle + Mathf.PI, TwoPi) - Mathf.PI);
                if (diff >= bestDiff) continue;

                bestDiff = diff;
                best = i;
            }

            _x = PixelEllipse.OutX[best];
            _y = PixelEllipse.OutY[best];
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

            if (SpacedCapacity <= spacedCount) return false;

            spacedMinX[spacedCount] = x0;
            spacedMinY[spacedCount] = y0;
            spacedMaxX[spacedCount] = x1;
            spacedMaxY[spacedCount] = y1;
            spacedCount++;
            return true;
        }

        private void FlushRun()
        {
            if (false == bRunActive) return;

            bRunActive = false;
            Emit(runX0, runY, runX1, runY + 1, runColor);
        }

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

        private void Emit(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            buffer.AddWorldRect(_x0, _y0, _x1, _y1, _color);

            if (_x0 < minX) minX = _x0;
            if (_y0 < minY) minY = _y0;
            if (maxX < _x1) maxX = _x1;
            if (maxY < _y1) maxY = _y1;
        }

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
                if (LineCapacity <= n) return 0;

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
