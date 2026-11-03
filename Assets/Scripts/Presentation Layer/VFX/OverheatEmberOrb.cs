using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 4스테이지 불씨의 불덩이(열확산 구체) 계산과 그리기 공용 코어. VFX_OverheatEmber(바닥 불씨 본체)와
    /// VFX_OverheatEmberOneShot(소멸 연출)이 같이 쓴다. 승인 시뮬레이션의 orbStep/orbColor를 그대로 옮겼다.
    ///
    /// - 13x15 열 격자를 30Hz 고정 간격으로 갱신한다. 구(중심 (6, 10), 반지름 4.6) 안에서는 각도와 시간에 따라 세기가 다른
    ///   열을 넣어 뜨거운 얼룩이 표면을 돌고, 구 밖에서는 아래 칸 열을 받아 식으며 둘레 전체에서 불길이 2~4px 핥고 올라간다.
    /// - 구 윤곽은 각도·시간에 따라 꿈틀대서 완전한 원이 되지 않는다.
    /// - 색: 구 안은 검붉음~빨강 위주(나선 결과 왼쪽 위 가장자리 빛이 한두 단계 밝힘), 번쩍일 때만 주황·노랑까지 오른다.
    ///   맴도는 불티(주황·노랑)보다 짙게 유지하는 것이 승인 조건이다.
    /// - _scale이 1보다 작으면(흡입 막바지) 구를 그만큼 작게 그리고 구 밖 불길은 그리지 않는다.
    ///
    /// 좌표: 격자는 y가 아래로 자란다. 그릴 때 월드 픽셀(y 위쪽)로 뒤집는다. 힙 할당이 없다.
    /// </summary>
    public sealed class OverheatEmberOrb
    {
        public const int Width = 13;
        public const int Height = 15;
        public const int CenterX = 6;
        public const int CenterY = 10;

        public const int ToneCount = 6;
        public const int DarkCount = 3;

        private const float Radius = 4.6f;
        private const float StepInterval = 1.0f / 30.0f;
        private const int CodeNone = -1;
        private const int CodeDark = 10;

        // 시뮬레이션 색(어두움 -> 밝음). 정점 알파 = 발광 세기(BrandWrapGlow). 구 몸통 색은 발광을 거의 주지 않아 블룸에 뭉개지지 않게 한다.
        private static readonly byte[] ToneR = { 0x5c, 0xa3, 0xdc, 0xff, 0xff, 0xff };
        private static readonly byte[] ToneG = { 0x0b, 0x18, 0x30, 0x6a, 0xb3, 0xf0 };
        private static readonly byte[] ToneB = { 0x07, 0x0c, 0x16, 0x24, 0x40, 0xb4 };
        private static readonly float[] ToneGlow = { 0.0f, 0.0f, 0.08f, 0.25f, 0.45f, 0.7f };
        private static readonly byte[] DarkR = { 0x1c, 0x31, 0x4c };
        private static readonly byte[] DarkG = { 0x04, 0x07, 0x0c };
        private static readonly byte[] DarkB = { 0x03, 0x05, 0x07 };

        // 칸별 상수 표(구 중심 기준 각도 a, 거리). 칸 위치는 고정이라 처음 한 번만 계산하고,
        // 시간 항은 호출당 한 번 구해 덧셈 정리 sin(A+B) = sinA·cosB + cosA·sinB로 합친다(칸마다 Atan2/Sin을 다시 구하지 않는다).
        private const int CellCount = Width * Height;
        private static readonly float[] CellAngle = new float[CellCount];
        private static readonly float[] CellDist = new float[CellCount];
        private static readonly float[] CellSin3 = new float[CellCount];
        private static readonly float[] CellCos3 = new float[CellCount];
        private static readonly float[] CellSin5 = new float[CellCount];
        private static readonly float[] CellCos5 = new float[CellCount];
        private static readonly float[] CellRim = new float[CellCount];

        static OverheatEmberOrb()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = y * Width + x;
                    float dx = x - CenterX;
                    float dy = y - CenterY;
                    float angle = Mathf.Atan2(dy, dx);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    CellAngle[index] = angle;
                    CellDist[index] = dist;
                    CellSin3[index] = Mathf.Sin(angle * 3.0f);
                    CellCos3[index] = Mathf.Cos(angle * 3.0f);
                    CellSin5[index] = Mathf.Sin(angle * 5.0f);
                    CellCos5[index] = Mathf.Cos(angle * 5.0f);
                    // 왼쪽 위 가장자리 빛 방향값(거리 0인 중심 칸은 0)
                    CellRim[index] = Mathf.Max(0.0f, (-dx * 0.6f - dy * 0.8f) / (0.0f == dist ? 1.0f : dist)) * 0.28f;
                }
            }
        }

        // 내부 의존성
        private readonly float[] heat = new float[Width * Height];
        private readonly float[] nextHeat = new float[Width * Height];

        // 상태 변수
        private float accumulator;
        private uint randomState = 1u;
        // 이번 호출의 시간 항: 윤곽 꿈틀임 sin/cos(t·3.1 + φ·6), sin/cos(t·4.7 - φ·3)
        private float wobbleSin3;
        private float wobbleCos3;
        private float wobbleSin5;
        private float wobbleCos5;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>격자를 비우고 난수 씨앗을 정한다. 풀에서 다시 꺼낼 때 부른다.</summary>
        public void Reset(uint _seed)
        {
            System.Array.Clear(heat, 0, heat.Length);
            System.Array.Clear(nextHeat, 0, nextHeat.Length);
            accumulator = 0.0f;
            randomState = 0u == _seed ? 1u : _seed;
        }

        /// <summary>
        /// 30Hz 고정 간격으로 격자를 갱신한다. _fade(0~1)는 넣는 열의 세기(소멸 때 0으로 줄인다), _flare(0~1.3)는 번쩍임 세기다.
        /// </summary>
        public void Step(float _dt, float _time, float _phase, float _fade, float _flare)
        {
            accumulator += _dt;
            while (StepInterval <= accumulator)
            {
                accumulator -= StepInterval;
                StepOnce(_time, _phase, _fade, _flare);
            }
        }

        /// <summary>격자를 한 번에 _count번 갱신한다(단발 연출이 처음부터 타오르는 상태로 시작하도록 미리 데울 때).</summary>
        public void Prewarm(int _count, float _time, float _phase)
        {
            for (int i = 0; i < _count; i++)
            {
                StepOnce(_time + i * StepInterval, _phase, 1.0f, 0.0f);
            }
        }

        /// <summary>
        /// 구 중심(_centerX, _centerY, 월드 픽셀)에 불덩이를 그린다. _pulse는 1.2초 주기 맥동(0~1), _scale은 크기 배율(1 이하).
        /// </summary>
        public void Draw(PixelPainter _painter, int _centerX, int _centerY, float _time, float _phase, float _fade, float _pulse,
            float _flare, float _scale, Color32[] _tones, Color32[] _darks)
        {
            float scale = Mathf.Clamp(_scale, 0.2f, 1.0f);
            PrepareWobble(_time, _phase);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int code = ColorCode(x, y, _time, _phase, _fade, _pulse, _flare, scale);
                    if (CodeNone == code) continue;

                    Color32 color = CodeDark == code ? _darks[2] : _tones[code];
                    _painter.Dot(_centerX + (x - CenterX), _centerY - (y - CenterY), color);
                }
            }
        }

        /// <summary>
        /// 정점 색 표를 만든다. 선형 색 공간이면 sRGB 목표 색을 선형으로 바꿔 넣어야 화면에서 같은 색으로 보인다.
        /// _tones: 빨강 계열 6단계(어두움 -> 밝음), _darks: 검붉음 3단계.
        /// </summary>
        public static void BuildPalette(Color32[] _tones, Color32[] _darks, float _glowScale)
        {
            bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;

            for (int i = 0; i < ToneCount; i++)
            {
                Color color = new Color32(ToneR[i], ToneG[i], ToneB[i], 255);
                if (true == bLinear) color = color.linear;
                color.a = Mathf.Clamp01(ToneGlow[i] * _glowScale);
                _tones[i] = color;
            }

            for (int i = 0; i < DarkCount; i++)
            {
                Color color = new Color32(DarkR[i], DarkG[i], DarkB[i], 255);
                if (true == bLinear) color = color.linear;
                color.a = 0.0f;
                _darks[i] = color;
            }
        }

        /// <summary>1.2초 주기 맥동(0~1). _phase는 불씨마다 다른 시작 위상이다.</summary>
        public static float Pulse(float _time, float _phase)
        {
            return 0.5f - 0.5f * Mathf.Cos((_time / 1.2f + _phase) * Mathf.PI * 2.0f);
        }

        // 일반 비즈니스 로직 및 내부 메서드

        // 구 중심에서 꿈틀대는 윤곽까지의 상대 거리(1이 윤곽)
        private static float OrbDistance(float _x, float _y, float _time, float _phase)
        {
            float dx = _x - CenterX;
            float dy = _y - CenterY;
            float angle = Mathf.Atan2(dy, dx);
            float radius = Radius * (1.0f + 0.1f * Mathf.Sin(angle * 3.0f + _time * 3.1f + _phase * 6.0f)
                + 0.06f * Mathf.Sin(angle * 5.0f - _time * 4.7f + _phase * 3.0f));
            return Mathf.Sqrt(dx * dx + dy * dy) / radius;
        }

        private void PrepareWobble(float _time, float _phase)
        {
            float t3 = _time * 3.1f + _phase * 6.0f;
            float t5 = _time * 4.7f - _phase * 3.0f;
            wobbleSin3 = Mathf.Sin(t3);
            wobbleCos3 = Mathf.Cos(t3);
            wobbleSin5 = Mathf.Sin(t5);
            wobbleCos5 = Mathf.Cos(t5);
        }

        // OrbDistance(칸 x, 칸 y)와 같은 값을 칸별 표와 이번 호출의 시간 항으로 구한다. PrepareWobble을 먼저 불러야 한다.
        private float CellOrbDistance(int _index)
        {
            float wobble3 = CellSin3[_index] * wobbleCos3 + CellCos3[_index] * wobbleSin3;  // sin(3a + t·3.1 + φ·6)
            float wobble5 = CellSin5[_index] * wobbleCos5 - CellCos5[_index] * wobbleSin5;  // sin(5a - (t·4.7 - φ·3))
            return CellDist[_index] / (Radius * (1.0f + 0.1f * wobble3 + 0.06f * wobble5));
        }

        private void StepOnce(float _time, float _phase, float _fade, float _flare)
        {
            PrepareWobble(_time, _phase);

            float t4 = _time * 4.2f + _phase * 6.0f;
            float sourceSin = Mathf.Sin(t4);
            float sourceCos = Mathf.Cos(t4);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = y * Width + x;
                    float d = CellOrbDistance(index);

                    float up = 0.0f;
                    if (y < Height - 1)
                    {
                        int sx = x;
                        if (Rand01() < 0.2f) sx += Rand01() < 0.5f ? -1 : 1;

                        int below = (y + 1) * Width;
                        up = (heat[below + Clamp(sx - 1)] + heat[below + Clamp(sx)] * 2.0f + heat[below + Clamp(sx + 1)]) * 0.25f;
                    }

                    float value;
                    if (d < 1.0f)
                    {
                        float swirl3 = CellSin3[index] * sourceCos + CellCos3[index] * sourceSin;  // sin(3a + t·4.2 + φ·6)
                        float source = (1.0f - d * d * 0.5f) * (0.72f + 0.28f * swirl3 + Rand01() * 0.3f)
                            * _fade * (1.0f + _flare * 0.4f);
                        value = (source * 0.35f + up * 0.4f + heat[index] * 0.12f) * 0.9f;
                    }
                    else
                    {
                        value = (up * 0.86f + heat[index] * 0.1f) * (0.84f + Rand01() * 0.13f);
                    }

                    nextHeat[index] = Mathf.Min(1.25f, value);
                }
            }

            System.Array.Copy(nextHeat, heat, heat.Length);
        }

        private int ColorCode(int _x, int _y, float _time, float _phase, float _fade, float _pulse, float _flare, float _scale)
        {
            int index = _y * Width + _x;
            bool bFullSize = 1.0f <= _scale;
            float dx = _x - CenterX;
            float dy = _y - CenterY;
            // 원래 크기면 칸별 표를 쓰고, 흡입 막바지(축소)에는 칸 위치가 격자 밖이라 직접 계산한다
            float d = true == bFullSize ? CellOrbDistance(index) : OrbDistance(CenterX + dx / _scale, CenterY + dy / _scale, _time, _phase);

            if (d < 1.0f)
            {
                float angle;
                float rimBase;
                if (true == bFullSize)
                {
                    angle = CellAngle[index];
                    rimBase = CellRim[index];
                }
                else
                {
                    angle = Mathf.Atan2(dy, dx);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (0.0f == dist) dist = 1.0f;
                    rimBase = Mathf.Max(0.0f, (-dx * 0.6f - dy * 0.8f) / dist) * 0.28f;
                }

                // 나선 결: 두 갈래 나선이 돌며 결을 따라 밝아진다
                float swirl = Mathf.Max(0.0f, Mathf.Sin(angle * 2.0f + d * 7.0f - _time * 4.5f + _phase * 6.0f));
                float swirl2 = swirl * swirl;
                float vein = 0.18f < d ? swirl2 * swirl2 * swirl2 * 0.42f * _fade : 0.0f;
                // 왼쪽 위 가장자리 빛
                float rim = 0.74f < d ? rimBase : 0.0f;

                int gx = Mathf.Clamp(Mathf.RoundToInt(CenterX + dx / _scale), 0, Width - 1);
                int gy = Mathf.Clamp(Mathf.RoundToInt(CenterY + dy / _scale), 0, Height - 1);
                float h = heat[gy * Width + gx] + (1.0f - d) * 0.15f + _pulse * 0.06f + _flare * 0.32f + vein + rim;

                if (1.05f < h && 0.3f < _flare) return 4;
                if (0.8f < h) return 3;
                if (0.57f < h) return 2;
                if (0.37f < h) return 1;
                if (0.2f < h) return 0;
                return CodeDark;
            }

            if (0.01f < Mathf.Abs(_scale - 1.0f)) return CodeNone;

            float outside = heat[index] + _flare * 0.12f;
            if (0.55f < outside) return 2;
            if (0.35f < outside) return 1;
            if (0.18f < outside) return 0;
            return CodeNone;
        }

        private static int Clamp(int _x)
        {
            return _x < 0 ? 0 : (_x > Width - 1 ? Width - 1 : _x);
        }

        private float Rand01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0xFFFFFF) / 16777216.0f;
        }
    }
}
