using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 도감 제목판의 완성 연출(E9 최초 완성 축하 · E7 완성 상태 유지). 승인된 시뮬레이션(Docs/Roguelike/_Work/CardCollection/template_v5.html 확정안 D2 · P3)의 값을 그대로 옮겼다.
/// 구성: 두근 새는 빛 · 모서리 반짝 · 흰 실루엣 2프레임 · 작은 조각 8개 · 피어오르는 입자 · 둘레 8곳 반짝.
/// 레어 카드 등장 연출(UI_RoguelikeRareRevealFx)과 같은 언어를 제목판(272x24)에 옮긴 것이다: 구운 빛 텍스처(T_RareCard_EdgeRing) + 가산 머티리얼(M_UILootGlowAdd) + 같은 규약
/// (정점 알파 = 세기 / GlowMax, 머티리얼 인스턴스 없음, 자식 Canvas 격리)으로 그린다.
///
/// 시간은 도감 창이 프레임(1/60초) 단위로 정한 값(ev = 차오름이 끝난 프레임 0)을 매 프레임 넘기는 Render 하나로 그린다(연출 전체가 ev의 함수라 건너뛰기도 시간만 당기면 끝난다).
/// 좌표는 시뮬레이션과 같은 640x360 좌표(왼쪽 위 원점, 아래쪽 +)이고, 층 루트(FxFront)가 도감 창 좌표의 왼쪽 위에 맞춰져 있다.
/// 이미지는 지난 값 캐시로 쓰고(바뀐 것만 갱신), 입자는 프레임이 바뀔 때만 다시 채운다. 초기화 이후 힙 할당이 없다.
/// </summary>
[DisallowMultipleComponent]
public class UI_RoguelikeCollectionPlateFx : MonoBehaviour
{
    private const float GlowMax = 2.5f;          // M_UILootGlowAdd / M_UILootParticles의 _GlowMax와 같아야 한다
    private const float FrameRate = 60f;
    private const int PlateCenterX = 320;
    private const int PlateCenterY = 24;
    private const int DefaultPlateWidth = 272;
    private const int PlateHeight = 24;

    // E9 값(프레임, 시뮬레이션 E9K)
    public const int BeatCount = 3;
    public const int PangFrame = 27;
    public const int WhiteFrames = 2;
    public const int TraceStartFrame = 47;
    public const int MeetFrame = 71;
    public const int IdleFrame = 84;
    public const int SwapFrame = 174;
    public const int EndFrame = 210;
    public const float IdleFadeFrames = 18f;
    public const float DimAlpha = 0.35f;

    private const int ShardCount = 22;
    private const int SpotCount = 8;

    public static readonly int[] BeatFrames = { 0, 12, 20 };
    public static readonly int[] StepFrames = { 0, 5, 8, 12, 15, 18, 20, 22, 24 };
    private static readonly float[] BeatSeconds = BuildSeconds(BeatFrames);

    // 픽셀 모양(레어 Pix)
    private const int ShapeDot = 0;
    private const int ShapeCircle = 1;
    private const int ShapePlus = 2;
    private const int ShapePlus5 = 3;

    // 등급색 = 금
    private static readonly Color32 CoreColor = new Color32(255, 247, 220, 255);
    private static readonly Color32 MainColor = new Color32(243, 204, 89, 255);
    private static readonly Color32 WhiteColor = new Color32(255, 255, 255, 255);
    private static readonly Color32[] FamilyColors = { new Color32(255, 240, 180, 255), new Color32(243, 204, 89, 255), new Color32(168, 116, 74, 255) };

    // 제목판 둘레 반짝이 자리(판 중심 기준 x, y)
    private static readonly int[] SpotX = { -151, 130, 155, -130, -25, 92, -168, 38 };
    private static readonly int[] SpotY = { -17, -18, 17, 26, -19, 38, -16, 43 };
    private static readonly float[] ShardAngle = { -150f, -128f, -112f, -100f, -86f, -74f, -60f, -40f, -20f, -165f, -8f, -95f, -70f, -118f, -140f, -52f, 12f, 192f, -30f, -158f, -106f, -80f };

    // 유지 상태 둘레 8곳 반짝 24Hz 모양 순서(. + + . .)
    private static readonly int[] IdleSpotShapes = { ShapeDot, ShapePlus, ShapePlus, ShapeDot, ShapeDot };

    /// <summary>Image 하나를 지난 값 캐시로 쓴다. 왼쪽 위 피벗이라 (왼쪽, 위쪽, 폭, 높이)를 그대로 넣는다(시뮬레이션의 정수 반올림 규칙).</summary>
    private sealed class FxImage
    {
        private readonly Image image;
        private readonly RectTransform rect;
        private bool bOn;
        private float left = float.NaN;
        private float top;
        private float width = float.NaN;
        private float height;
        private Color32 color;

        public FxImage(Image _image)
        {
            image = _image;
            rect = _image.rectTransform;
            image.raycastTarget = false;
            image.maskable = false;
            bOn = true;
            Hide();
        }

        /// <summary>중심 (_cx, _cy) · 크기 (_w, _h)를 시뮬레이션처럼 왼쪽 위 / 크기를 정수로 반올림해 놓는다.</summary>
        public void ShowCentered(float _cx, float _cy, float _w, float _h, Color32 _color)
        {
            if (0 == _color.a)
            {
                Hide();
                return;
            }

            float l = Mathf.Floor(_cx - _w * 0.5f + 0.5f);
            float t = Mathf.Floor(_cy - _h * 0.5f + 0.5f);
            float w = Mathf.Floor(_w + 0.5f);
            float h = Mathf.Floor(_h + 0.5f);

            if (false == bOn)
            {
                bOn = true;
                image.enabled = true;
            }

            if (l != left || t != top)
            {
                left = l;
                top = t;
                rect.anchoredPosition = new Vector2(l, -t);
            }

            if (w != width || h != height)
            {
                width = w;
                height = h;
                rect.sizeDelta = new Vector2(w, h);
            }

            if (_color.r != color.r || _color.g != color.g || _color.b != color.b || _color.a != color.a)
            {
                color = _color;
                image.color = _color;
            }
        }

        public void Hide()
        {
            if (false == bOn) return;

            bOn = false;
            image.enabled = false;
        }
    }

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Collection)
    [Header("앞 층 (제목판 위)")]
    [SerializeField] private Image edgeRingImage;
    [SerializeField] private UI_RoguelikeRareRevealParticles pixelGraphic;

    //내부 의존성
    private FxImage edgeRing;
    private bool bInitialized;

    //상태 변수
    private int plateWidth = DefaultPlateWidth;
    private float fxAlpha = 1f;
    private bool bHasParticles;

    /// <summary>제목판 가로 폭(언어별로 272보다 넓을 수 있다). 새는 빛 · 피어오르는 입자가 이 폭을 따른다.</summary>
    public void SetPlateWidth(int _width)
    {
        plateWidth = Mathf.Max(DefaultPlateWidth, _width);
    }

    /// <summary>
    /// 이번 프레임의 연출을 그린다.
    ///   _ev = 차오름이 끝난 프레임을 0으로 센 값(E7은 큰 값), _tt = 두근 · 팡 시간(초, E7은 99), _it = 유지 시간(초, 유지 시작 = 0), _rt = 덩이 시간(초),
    ///   _fade = 유지 연출이 떠오르는 비율(0~1), _plateAlpha = 제목판 알파(열기 · 닫기 페이드), _plateDy = 제목판 세로 이동(정수), _plateDx = 두근 흔들림(정수).
    /// </summary>
    public void Render(int _ev, float _tt, float _it, float _rt, float _fade, float _plateAlpha, int _plateDy, int _plateDx)
    {
        EnsureInitialized();

        fxAlpha = _fade * _plateAlpha;
        if (0.003f >= fxAlpha)
        {
            HideAll();
            return;
        }

        float cy = PlateCenterY + _plateDy;
        DrawFront(_tt, _it, cy, _plateDx);
    }

    /// <summary>모든 빛 · 입자를 끈다(창을 닫을 때 · 완성 연출이 없을 때).</summary>
    public void HideAll()
    {
        if (false == bInitialized) return;

        edgeRing.Hide();

        if (true == bHasParticles)
        {
            bHasParticles = false;
            if (null != pixelGraphic) pixelGraphic.ClearQuads();
        }
    }

    // 일반 비즈니스 로직 및 내부 메서드

    private void EnsureInitialized()
    {
        if (true == bInitialized) return;

        bInitialized = true;
        edgeRing = Wrap(edgeRingImage);
    }

    private static FxImage Wrap(Image _image)
    {
        return new FxImage(_image);
    }

    private static float[] BuildSeconds(int[] _frames)
    {
        float[] seconds = new float[_frames.Length];
        for (int i = 0; _frames.Length > i; i++) seconds[i] = _frames[i] / FrameRate;
        return seconds;
    }

    private int PlateLeft => PlateCenterX - (plateWidth >> 1);
    private int PlateRight => PlateLeft + plateWidth;

    // 후광 · 빛 덩이 색: 감마 rgb + 정점 알파(= min(1, 세기) x 연출 알파 / GlowMax)
    private Color32 Glow(Color32 _rgb, float _intensity)
    {
        _rgb.a = ToByte(Mathf.Min(1f, _intensity) * fxAlpha / GlowMax);
        return _rgb;
    }

    private Color32 PixelColor(Color32 _rgb)
    {
        _rgb.a = ToByte(fxAlpha / GlowMax);
        return _rgb;
    }

    private static byte ToByte(float _v)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(_v * 255f), 0, 255);
    }

    private static float Clamp01(float _v)
    {
        return Mathf.Clamp01(_v);
    }

    private static int RoundHalfUp(float _v)
    {
        return Mathf.FloorToInt(_v + 0.5f);
    }

    private static Color32 Lerp(Color32 _a, Color32 _b, float _k)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(_a.r + (_b.r - _a.r) * _k),
            (byte)Mathf.RoundToInt(_a.g + (_b.g - _a.g) * _k),
            (byte)Mathf.RoundToInt(_a.b + (_b.b - _a.b) * _k), 255);
    }

    private static float Spike(float _time, float _tau)
    {
        float m = 0f;
        for (int i = 0; BeatSeconds.Length > i; i++)
        {
            float d = _time - BeatSeconds[i];
            if (0f <= d) m = Mathf.Max(m, Mathf.Exp(-d / _tau));
        }
        return m;
    }

    private static float Rnd(double _n)
    {
        double x = Math.Sin(_n * 127.1 + 311.7) * 43758.5453;
        return (float)(x - Math.Floor(x));
    }

    private static Color32 Tone(int _tone)
    {
        return FamilyColors[_tone];
    }

    // ---------- 앞 층: 두근 새는 빛 · 모서리 반짝 · 작은 조각 · 피어오르는 입자 · 둘레 반짝 ----------

    private void DrawFront(float _t, float _it, float _cy, int _dx)
    {
        float pT = PangFrame / FrameRate;
        float p = _t - pT;

        // ② 제목판 테두리 틈으로 새는 금빛(레어 EdgeGlow: 0.25 + 0.45 x 진행 + 0.5 x 번쩍, x0.8)
        if (_t < pT + 0.05f)
        {
            float a = 0.25f + 0.45f * Clamp01(_t / pT) + 0.5f * Spike(_t, 0.08f);
            edgeRing.ShowCentered(PlateCenterX + _dx, _cy, plateWidth + 1f + 29f, PlateHeight + 1f + 29f, Glow(MainColor, Mathf.Min(1f, a * 0.8f)));
        }
        else
        {
            edgeRing.Hide();
        }

        // 픽셀 입자
        if (null == pixelGraphic) return;

        pixelGraphic.BeginQuads();

        if (_t < pT + 0.05f) DrawTease(_t, _cy);
        if (0f <= p && 1.1f > p) DrawShards(p, _cy);
        if (0f <= p) DrawIdle(_it, _cy);

        pixelGraphic.Commit();
        bHasParticles = true;
    }

    // 픽셀 한 칸 사각형 [x, x+w) x [y, y+h)
    private void PxRect(int _x, int _y, int _w, int _h, Color32 _rgb)
    {
        pixelGraphic.Rect(_x, -(_y + _h), _x + _w, -_y, _rgb);
    }

    private void Pix(int _shape, float _u, float _v, Color32 _rgb)
    {
        int x = RoundHalfUp(_u);
        int y = RoundHalfUp(_v);
        Color32 c = PixelColor(_rgb);
        switch (_shape)
        {
            case ShapeDot:
                PxRect(x, y, 1, 1, c);
                break;
            case ShapeCircle:
                PxRect(x, y, 2, 2, c);
                break;
            case ShapePlus:
                PxRect(x - 1, y, 3, 1, c);
                PxRect(x, y - 1, 1, 3, c);
                break;
            case ShapePlus5:
                PxRect(x - 2, y, 5, 1, c);
                PxRect(x, y - 2, 1, 5, c);
                break;
        }

        // 큰 모양은 가운데 한 칸을 밝게
        if (ShapePlus5 == _shape) PxRect(x, y, 1, 1, PixelColor(Lerp(_rgb, WhiteColor, 0.6f)));
    }

    // ② 박자마다 모서리 한 곳에서 . + +5 + . 24Hz (왼쪽 위 · 오른쪽 아래 · 오른쪽 위)
    private void DrawTease(float _t, float _cy)
    {
        int x0 = PlateLeft;
        int x1 = PlateRight;
        float y0 = _cy - 12f;
        float y1 = _cy + 12f;
        for (int j = 0; BeatCount > j; j++)
        {
            float d = _t - BeatSeconds[j];
            if (0f > d || 0.2f < d) continue;

            float cu;
            float cv;
            if (0 == j) { cu = x0 - 1; cv = y0 - 1f; }
            else if (1 == j) { cu = x1; cv = y1; }
            else { cu = x1; cv = y0 - 1f; }

            int fr = Mathf.FloorToInt(d * 24f);
            int shape = ShapeShift(fr);
            Pix(shape, cu, cv, CoreColor);
        }
    }

    // [0, 2, 3, 2, 0] 24Hz 모양 순서
    private static int ShapeShift(int _frame)
    {
        switch (Mathf.Min(4, _frame))
        {
            case 1: return ShapePlus;
            case 2: return ShapePlus5;
            case 3: return ShapePlus;
            default: return ShapeDot;
        }
    }

    // ③ 작은 조각 8개(22개 중 3개마다 1개, 금 계열, + → . 만)
    private void DrawShards(float _p, float _cy)
    {
        float q = _p;
        const float kd = 2.4f;
        const float gr = 420f;
        float e = 1f - Mathf.Exp(-kd * q);
        for (int j = 0; ShardCount > j; j++)
        {
            if (0 != j % 3) continue;

            float life = (0.62f + Rnd(j + 11) * 0.42f) * 0.7f;
            if (life < q) continue;

            float ang = (0 == j % 3 ? ShardAngle[j] : -ShardAngle[j]) * Mathf.Deg2Rad;
            float sp = 120f + Rnd(j + 3) * 110f;
            float vx = Mathf.Cos(ang) * sp * 1.4f;
            float vy = Mathf.Sin(ang) * sp;
            float u = PlateCenterX + (Rnd(j + 21) - 0.5f) * 240f + vx / kd * e;
            float v = _cy + (Rnd(j + 31) - 0.5f) * 20f + (vy + gr / kd) / kd * e - gr * q / kd;
            float fr = q / life;
            int tone = 0.35f > fr ? 0 : (0.7f > fr ? 1 : 2);
            if (0.82f < fr && 0 != (Mathf.FloorToInt(q * 24f) & 1)) continue;

            int shape = 0.4f > fr ? ShapePlus : ShapeDot;
            Pix(shape, u, v, 0.05f > q ? WhiteColor : Tone(tone));
        }
    }

    // ⑤ 피어오르는 입자(0.055초마다 1개, 수명 1.8초) + 주변 8곳 반짝(4.2초 주기)
    private void DrawIdle(float _it, float _cy)
    {
        if (-0.1f >= _it) return;

        const float stepSeconds = 0.055f;
        int first = Mathf.Max(0, Mathf.FloorToInt((_it - 1.8f) / stepSeconds));
        int last = Mathf.FloorToInt(_it / stepSeconds);
        for (int m = first; last >= m; m++)
        {
            float age = _it - m * stepSeconds;
            if (0f > age || 1.8f < age) continue;

            float su = PlateCenterX + (Rnd(m + 1) - 0.5f) * (plateWidth + 16f);
            float sv = _cy + 26f - Rnd(m + 2) * 30f;
            float u = su + Mathf.Sin(age * 3f + m) * 2.5f;
            float v = sv - age * (14f + Rnd(m + 3) * 10f);
            float fr = age / 1.8f;
            int tone = 0.4f > fr ? 0 : (0.75f > fr ? 1 : 2);
            if (0.8f < fr && 0 != (Mathf.FloorToInt(age * 24f) & 1)) continue;
            if (u > PlateLeft - 1 && u < PlateRight + 1 && v > _cy - 13f && v < _cy + 13f) continue;

            int shape = 0.25f > Rnd(m + 4) ? ShapePlus : (0.5f > Rnd(m + 5) ? ShapeCircle : ShapeDot);
            Pix(shape, u, v, Tone(tone));
        }

        for (int j = 0; SpotCount > j; j++)
        {
            const float period = 4.2f;
            float off = j * 0.77f % period;
            if (_it - off < 0f) continue;

            float d = ((_it - off) % period + period) % period;
            if (0.25f < d) continue;

            int fr = Mathf.FloorToInt(d * 24f);
            int shape = IdleSpotShapes[Mathf.Min(4, fr)];
            float su = PlateCenterX + SpotX[j];
            float sv = _cy + SpotY[j];
            Pix(shape, su, sv, Tone(0));
        }
    }

    // 유니티 라이프사이클 이벤트 함수

    private void OnDisable()
    {
        HideAll();
    }
}
