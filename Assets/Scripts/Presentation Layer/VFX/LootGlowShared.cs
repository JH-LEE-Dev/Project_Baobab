using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 전리품 빛(마을 기둥 VFX_LootPillarGlow, 상세 HUD UI_LootRevealGlow)이 같이 쓰는 계산.
    /// - 팔레트: 아우라 HDR 색에서 원색 / 덮기 색 / 밝은 심 색을 만든다(승인된 시뮬레이션과 같은 식).
    /// - 떠오르는 입자: 경과 틱만으로 위치·모양·밝기가 정해지는 닫힌 식. 크기(퍼짐·상승 거리)만 매개변수로 받는다.
    /// 모든 함수는 할당이 없다.
    /// </summary>
    public static class LootGlowShared
    {
        /// <summary>떠오르는 입자 하나의 상태. Visible이 false면 이번 틱에는 그리지 않는다.</summary>
        public struct MoteState
        {
            public bool bVisible;
            public int x;          // 기준 칸에서의 가로 칸 수
            public int yDown;      // 기준 칸에서의 세로 칸 수(화면 아래쪽이 +)
            public int shape;      // PixelPainter.Shape* 상수
            public float bright;   // 0~1
        }

        /// <summary>입자 퍼짐·상승 크기. 마을 기둥 = Village, 상세 HUD = 2배.</summary>
        public struct MoteScale
        {
            public float spreadX;
            public float spreadY;
            public float riseMin;
            public float riseRange;
            public float sway;

            public MoteScale(float _spreadX, float _spreadY, float _riseMin, float _riseRange, float _sway)
            {
                spreadX = _spreadX;
                spreadY = _spreadY;
                riseMin = _riseMin;
                riseRange = _riseRange;
                sway = _sway;
            }
        }

        public static readonly MoteScale VillageMotes = new MoteScale(8.0f, 3.0f, 18.0f, 16.0f, 1.2f);
        public static readonly MoteScale RevealMotes = new MoteScale(17.0f, 6.0f, 36.0f, 32.0f, 2.4f);

        // 입자 모양별 발광 비율(큰 모양일수록 낮춘다)
        public const float GlowDiamond = 0.55f;
        public const float GlowCircle = 0.75f;
        public const float GlowPlusCross = 0.85f;
        public const float GlowDot = 1.0f;

        private static readonly int[] MoteBigShapes = { PixelPainter.ShapeDiamond, PixelPainter.ShapePlus, PixelPainter.ShapeCircle, PixelPainter.ShapeCross };
        private static readonly int[] MoteMidShapes = { PixelPainter.ShapePlus, PixelPainter.ShapeCircle, PixelPainter.ShapeCross, PixelPainter.ShapePlus };

        // 팔레트

        /// <summary>
        /// _aura(선형 HDR)에서 시뮬레이션 팔레트를 만든다. _hdr = 원색, _tint = 정규화 색을 흰색 쪽으로 55%, _core = 원색을 2.2 흰색 쪽으로 45%.
        /// _normal = 정규화 색(입자 정점 rgb). _hdrMax/_coreMax = 각 색의 가장 큰 채널(입자 발광 배율 계산용).
        /// </summary>
        public static void BuildPalette(Color _aura, out Color _hdr, out Color _tint, out Color _core, out Color _normal, out float _hdrMax, out float _coreMax)
        {
            Color c = _aura;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (0.0001f >= max)
            {
                c = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                max = 1.0f;
            }

            _hdr = new Color(c.r, c.g, c.b, 1.0f);
            _hdrMax = max;
            _normal = new Color(c.r / max, c.g / max, c.b / max, 1.0f);
            _tint = new Color(Mathf.Lerp(_normal.r, 1.0f, 0.55f), Mathf.Lerp(_normal.g, 1.0f, 0.55f), Mathf.Lerp(_normal.b, 1.0f, 0.55f), 1.0f);
            _core = new Color(Mathf.Lerp(c.r, 2.2f, 0.45f), Mathf.Lerp(c.g, 2.2f, 0.45f), Mathf.Lerp(c.b, 2.2f, 0.45f), 1.0f);
            _coreMax = Mathf.Max(_core.r, Mathf.Max(_core.g, _core.b));
        }

        // 떠오르는 입자

        /// <summary>
        /// 시뮬레이션 moteState를 그대로 옮긴 닫힌 식. 주기 54~71틱(30fps), 슬롯마다 위상이 다르다. _seed는 인스턴스마다 다른 값.
        /// </summary>
        public static MoteState EvaluateMote(int _index, int _tick, float _seed, in MoteScale _scale)
        {
            MoteState m = default;
            float p = _seed;
            int period = 54 + Mathf.FloorToInt(Hash(_index * 7.3f + p * 3.1f) * 18.0f);
            int phase = Mathf.FloorToInt(Hash(_index * 3.7f + p * 11.9f) * period);
            int local = _tick + phase;
            int lt = local % period;
            int cycle = (local / period) % 1000;
            float life = lt / (float)period;

            m.bright = Smooth(0.0f, 0.08f, life) * (1.0f - Smooth(0.82f, 1.0f, life));
            if (0.02f >= m.bright) return m;

            float seed = _index * 13.1f + cycle * 7.7f + p * 5.3f;
            float ex = Hash(seed) * 2.0f - 1.0f;
            float ey = (Hash(seed + 1.0f) * 2.0f - 1.0f) * 0.5f;
            float rise = _scale.riseMin + Hash(seed + 2.0f) * _scale.riseRange;
            float sway = Mathf.Sin(life * Mathf.PI * 2.0f * 0.8f + seed) * _scale.sway;
            float riseEase = 1.0f - Mathf.Pow(1.0f - life, 1.6f);
            m.x = RoundHalfUp(ex * _scale.spreadX + sway);
            m.yDown = RoundHalfUp(ey * _scale.spreadY - rise * riseEase);

            if (0.06f > life) m.shape = PixelPainter.ShapeDot;
            else if (0.14f > life) m.shape = 0 != (_index & 1) ? PixelPainter.ShapePlus : PixelPainter.ShapeCircle;
            else if (0.45f > life) m.shape = MoteBigShapes[_index & 3];
            else if (0.72f > life) m.shape = MoteMidShapes[_index & 3];
            else m.shape = PixelPainter.ShapeDot;

            m.bVisible = true;
            return m;
        }

        public static float ShapeGlow(int _shape)
        {
            switch (_shape)
            {
                case PixelPainter.ShapeDiamond: return GlowDiamond;
                case PixelPainter.ShapeCircle: return GlowCircle;
                case PixelPainter.ShapePlus:
                case PixelPainter.ShapeCross: return GlowPlusCross;
                default: return GlowDot;
            }
        }

        // 수식 도우미

        public static float Hash(float _n)
        {
            float v = Mathf.Sin(_n * 127.1f + 311.7f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        public static float Smooth(float _e0, float _e1, float _x)
        {
            float t = Mathf.Clamp01((_x - _e0) / (_e1 - _e0));
            return t * t * (3.0f - 2.0f * t);
        }

        public static int RoundHalfUp(float _v)
        {
            return Mathf.FloorToInt(_v + 0.5f);
        }

        public static float EaseOutCubic(float _x)
        {
            float inv = 1.0f - _x;
            return 1.0f - inv * inv * inv;
        }

        /// <summary>번쩍 세기: _hold초 동안 1을 유지하고, _length초까지 줄어 0이 된다. 0초 이전은 0.</summary>
        public static float PeakDecay(float _time, float _hold, float _length)
        {
            if (0.0f > _time) return 0.0f;
            if (_hold > _time) return 1.0f;
            if (_length <= _time) return 0.0f;
            return 1.0f - EaseOutCubic((_time - _hold) / (_length - _hold));
        }
    }
}
