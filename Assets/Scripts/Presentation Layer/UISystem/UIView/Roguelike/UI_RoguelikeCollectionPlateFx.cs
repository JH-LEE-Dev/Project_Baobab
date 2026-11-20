using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 도감 제목판의 완성 연출(E9 최초 완성 축하 · E7 완성 상태 유지). 승인된 시뮬레이션(Docs/Roguelike/_Work/CardCollection/template.html v2)의 값을 그대로 옮겼다.
/// 레어 카드 등장 연출(UI_RoguelikeRareRevealFx)과 같은 언어를 제목판(272x24)에 옮긴 것이다: 구운 빛 텍스처(T_RareCard_*) + 가산 머티리얼(M_UILootGlowAdd) + 같은 규약
/// (정점 알파 = 세기 / GlowMax, 머티리얼 인스턴스 없음, 자식 Canvas 격리)으로 그린다. 회전 빛살은 없고 금빛 덩이 7개가 뭉게뭉게 퍼진다.
///
/// 시간은 도감 창이 프레임(1/60초) 단위로 정한 값(ev = 차오름이 끝난 프레임 0)을 매 프레임 넘기는 Render 하나로 그린다(연출 전체가 ev의 함수라 건너뛰기도 시간만 당기면 끝난다).
/// 좌표는 시뮬레이션과 같은 640x360 좌표(왼쪽 위 원점, 아래쪽 +)이고, 층 루트(FxBack · FxFront)가 도감 창 좌표의 왼쪽 위에 맞춰져 있다.
/// 이미지는 지난 값 캐시로 쓰고(바뀐 것만 갱신), 입자 · 빛 점은 프레임이 바뀔 때만 다시 채운다. 초기화 이후 힙 할당이 없다.
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
    public const int WhiteFrames = 7;
    public const int TraceStartFrame = 47;
    public const int MeetFrame = 71;
    public const int IdleFrame = 84;
    public const int SwapFrame = 174;
    public const int EndFrame = 210;
    public const float IdleFadeFrames = 18f;
    public const float DimAlpha = 0.35f;

    private const int ShardCount = 22;
    private const int SpotCount = 8;
    private const int BlobCount = 7;
    private const int TraceBeats = 8;
    // 한쪽 반 경로 길이: 위 가운데 → 모서리 137 + 옆 25 + 아래 모서리 → 아래 가운데 137
    private const float TraceHalf = 137f + 25f + 137f;

    public static readonly int[] BeatFrames = { 0, 12, 20 };
    public static readonly int[] StepFrames = { 0, 5, 8, 12, 15, 18, 20, 22, 24 };
    private static readonly float[] BeatSeconds = BuildSeconds(BeatFrames);
    private static readonly float[] StepSeconds = BuildSeconds(StepFrames);

    // 픽셀 모양(레어 Pix)
    private const int ShapeDot = 0;
    private const int ShapeCircle = 1;
    private const int ShapePlus = 2;
    private const int ShapePlus5 = 3;
    private const int ShapeCross = 4;
    private const int ShapeCross5 = 5;
    private const int ShapeDiamond = 6;

    // 등급색 = 금
    private static readonly Color32 CoreColor = new Color32(255, 247, 220, 255);
    private static readonly Color32 MainColor = new Color32(243, 204, 89, 255);
    private static readonly Color32 DeepColor = new Color32(168, 116, 74, 255);
    private static readonly Color32 WhiteColor = new Color32(255, 255, 255, 255);
    private static readonly Color32[] FamilyColors = { new Color32(255, 240, 180, 255), new Color32(243, 204, 89, 255), new Color32(168, 116, 74, 255) };

    // 제목판 둘레 반짝이 자리(판 중심 기준 x, y)
    private static readonly int[] SpotX = { -151, 130, 155, -130, -25, 92, -168, 38 };
    private static readonly int[] SpotY = { -17, -18, 17, 26, -19, 38, -16, 43 };
    private static readonly float[] ShardAngle = { -150f, -128f, -112f, -100f, -86f, -74f, -60f, -40f, -20f, -165f, -8f, -95f, -70f, -118f, -140f, -52f, 12f, 192f, -30f, -158f, -106f, -80f };
    private static readonly int[] ShardShape = { ShapeDiamond, ShapePlus5, ShapeCross5, ShapeCircle, ShapeDiamond, ShapePlus, ShapeCross, ShapeDot, ShapeDiamond, ShapePlus5, ShapeCircle, ShapeCross5 };
    private static readonly int[] ReducedShape = { ShapeDot, ShapeDot, ShapeDot, ShapePlus, ShapeDot, ShapeCross, ShapePlus };

    // 뭉게뭉게 빛 덩이: 판 중심 기준 앵커 x, y, 반지름, 한 번 피어오르는 주기(초), 위상, 색(0 주색 · 1 짙은색 · 2 심색), 세기 배율
    private static readonly float[] BlobAnchorX = { -176f, -112f, -44f, 44f, 112f, 176f, 0f };
    private static readonly float[] BlobAnchorY = { 12f, 32f, 42f, 42f, 32f, 12f, 30f };
    private static readonly float[] BlobRadius = { 44f, 40f, 46f, 46f, 40f, 44f, 36f };
    private static readonly float[] BlobPeriod = { 3.6f, 4.2f, 3.3f, 3.9f, 4.5f, 3.1f, 5.0f };
    private static readonly float[] BlobPhase = { 0.00f, 0.31f, 0.62f, 0.17f, 0.48f, 0.79f, 0.93f };
    private static readonly int[] BlobTone = { 0, 1, 0, 1, 0, 1, 2 };
    private static readonly float[] BlobWeight = { 1.0f, 1.0f, 0.9f, 1.0f, 1.0f, 1.0f, 0.6f };

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
    [Header("뒤 층 (제목판 뒤)")]
    [SerializeField] private Image haloCoreImage;
    [SerializeField] private Image haloMainImage;
    [SerializeField] private Image haloDeepImage;
    [SerializeField] private Image discCoreImage;
    [SerializeField] private Image[] blobImages = new Image[BlobCount];

    [Header("앞 층 (제목판 위)")]
    [SerializeField] private Image edgeRingImage;
    [SerializeField] private Image[] ghostWideImages = new Image[2];
    [SerializeField] private Image[] ghostCoreImages = new Image[2];
    [SerializeField] private Image shockMainImage;
    [SerializeField] private Image shockCoreImage;
    [SerializeField] private Image meetHaloImage;
    [SerializeField] private Image meetLineImage;
    [SerializeField] private UI_RoguelikeRareRevealParticles softGraphic;
    [SerializeField] private UI_RoguelikeRareRevealParticles pixelGraphic;

    //내부 의존성
    private FxImage haloCore;
    private FxImage haloMain;
    private FxImage haloDeep;
    private FxImage discCore;
    private FxImage[] blobs;
    private FxImage edgeRing;
    private FxImage[] ghostWide;
    private FxImage[] ghostCore;
    private FxImage shockMain;
    private FxImage shockCore;
    private FxImage meetHalo;
    private FxImage meetLine;
    private bool bInitialized;

    //상태 변수
    private int plateWidth = DefaultPlateWidth;
    private float fxAlpha = 1f;
    private bool bHasParticles;

    /// <summary>제목판 가로 폭(언어별로 272보다 넓을 수 있다). 후광 · 충격 테두리 · 둘레 경로가 이 폭을 따른다.</summary>
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
        DrawBack(_tt, _it, _rt, cy);
        DrawFront(_tt, _it, cy, _plateDx);
    }

    /// <summary>모든 빛 · 입자를 끈다(창을 닫을 때 · 완성 연출이 없을 때).</summary>
    public void HideAll()
    {
        if (false == bInitialized) return;

        haloCore.Hide();
        haloMain.Hide();
        haloDeep.Hide();
        discCore.Hide();
        for (int i = 0; blobs.Length > i; i++) blobs[i].Hide();
        edgeRing.Hide();
        for (int i = 0; ghostWide.Length > i; i++) ghostWide[i].Hide();
        for (int i = 0; ghostCore.Length > i; i++) ghostCore[i].Hide();
        shockMain.Hide();
        shockCore.Hide();
        meetHalo.Hide();
        meetLine.Hide();

        if (true == bHasParticles)
        {
            bHasParticles = false;
            if (null != softGraphic) softGraphic.ClearQuads();
            if (null != pixelGraphic) pixelGraphic.ClearQuads();
        }
    }

    // 일반 비즈니스 로직 및 내부 메서드

    private void EnsureInitialized()
    {
        if (true == bInitialized) return;

        bInitialized = true;
        haloCore = Wrap(haloCoreImage);
        haloMain = Wrap(haloMainImage);
        haloDeep = Wrap(haloDeepImage);
        discCore = Wrap(discCoreImage);
        blobs = WrapAll(blobImages);
        edgeRing = Wrap(edgeRingImage);
        ghostWide = WrapAll(ghostWideImages);
        ghostCore = WrapAll(ghostCoreImages);
        shockMain = Wrap(shockMainImage);
        shockCore = Wrap(shockCoreImage);
        meetHalo = Wrap(meetHaloImage);
        meetLine = Wrap(meetLineImage);
    }

    private static FxImage Wrap(Image _image)
    {
        return new FxImage(_image);
    }

    private static FxImage[] WrapAll(Image[] _images)
    {
        FxImage[] result = new FxImage[_images.Length];
        for (int i = 0; _images.Length > i; i++) result[i] = new FxImage(_images[i]);
        return result;
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

    private static float EaseOut(float _v)
    {
        return 1f - (1f - _v) * (1f - _v);
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

    // ---------- 뒤 층: 후광 3겹(+ 백열 심) · 뭉게뭉게 빛 덩이 7개 ----------

    private void DrawBack(float _t, float _it, float _rt, float _cy)
    {
        float pT = PangFrame / FrameRate;
        float haloI;
        float haloR;
        float spread;
        float bigger;
        float burstK = 0f;

        if (_t < pT)
        {
            // ② 두근: 박자마다 번쩍이며 차오름(레어 0.12 + 0.35 x 진행 + 0.3 x 박자 번쩍, 반지름 30 + 6 x 번쩍 + 6 x 진행)
            float pr = Clamp01(_t / pT);
            float sp = Spike(_t, 0.07f);
            haloI = 0.12f + 0.35f * pr + 0.3f * sp;
            haloR = 30f + 6f * sp + 6f * pr;
            spread = 0.55f + 0.2f * pr;
            bigger = 0.7f + 0.1f * pr;
        }
        else
        {
            // ③ 팡 → ⑤ 유지
            float p = _t - pT;
            haloI = 0.15f > p ? 1.3f : 0.5f + 0.8f * Mathf.Exp(-(p - 0.15f) / 0.22f);
            haloR = 0.13f > p ? 34f + 44f * EaseOut(p / 0.13f) : 50f + 28f * Mathf.Exp(-(p - 0.13f) / 0.25f);
            if (0f < _it) haloI += 0.08f * Mathf.Sin(_it * Mathf.PI * 2f / 1.8f);

            float e = EaseOut(Clamp01(p / 0.12f));
            float after = Mathf.Exp(-Mathf.Max(0f, p - 0.12f) / 0.5f);
            spread = 0.75f + (1f + 0.75f * after - 0.75f) * e;
            bigger = 0.8f + (1f + 0.6f * Mathf.Exp(-Mathf.Max(0f, p - 0.12f) / 0.45f) - 0.8f) * e;
            burstK = Mathf.Exp(-p / 0.45f);
        }

        // 카드 반지름 R의 원(가로 2R, 세로 2.64R)을 판 바깥쪽 연장으로 옮김
        float w = plateWidth + 2f * (haloR - 25f);
        float h = PlateHeight + 2.64f * (haloR - 25f);
        haloCore.ShowCentered(PlateCenterX, _cy, w, h, Glow(CoreColor, Mathf.Min(1f, haloI * 0.85f)));
        haloMain.ShowCentered(PlateCenterX, _cy, w, h, Glow(MainColor, Mathf.Min(1f, haloI * 0.6f)));
        haloDeep.ShowCentered(PlateCenterX, _cy, w, h, Glow(DeepColor, Mathf.Min(1f, haloI * 0.25f)));
        if (1f < haloI) discCore.ShowCentered(PlateCenterX, _cy, plateWidth * 0.55f, 34f, Glow(WhiteColor, Mathf.Min(1f, haloI - 1f)));
        else discCore.Hide();

        // 뭉게뭉게 빛 덩이: 후광 세기에 따라 떠오르고, 각자 3~5초 주기로 작게 → 크게 피어오르며(위로 18px) 사라진다
        float intensity = Mathf.Min(1f, haloI * 0.42f * 1.6f);
        for (int j = 0; BlobCount > j; j++)
        {
            float per = BlobPeriod[j];
            float ph = BlobPhase[j];
            float u = (_rt / per + ph) % 1f;
            if (0f > u) u += 1f;

            float env = 0.3f + 0.7f * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.4f);
            float r = BlobRadius[j] * (0.55f + 0.75f * u) * bigger;
            float x = PlateCenterX + BlobAnchorX[j] * spread + Mathf.Sin(_rt * 0.7f + ph * 6.283f) * 10f;
            float y = _cy + BlobAnchorY[j] * (0.6f + 0.4f * spread) - 18f * u + Mathf.Cos(_rt * 0.55f + ph * 9f) * 6f;
            Color32 color = 0 == BlobTone[j] ? MainColor : (1 == BlobTone[j] ? DeepColor : CoreColor);
            blobs[j].ShowCentered(x, y, r * 2.5f, r * 1.8f, Glow(color, intensity * BlobWeight[j] * (env * (1f - burstK) + 0.9f * burstK)));
        }
    }

    // ---------- 앞 층: 두근 새는 빛 · 잔상 2장 · 충격 테두리 · 딱 섬광 · 입자 · 타다다닥 ----------

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

        // ③ 잔상 2장: 제목판 실루엣 블러가 [1.45배 · 1.25배]로 번지며 사라짐
        for (int i = 0; 2 > i; i++)
        {
            float delay = 0 == i ? 0.05f : 0.13f;
            float life = 0.36f;
            float maxScale = 0 == i ? 1.45f : 1.25f;
            float weight = 0 == i ? 0.55f : 0.35f;
            float q = p - delay;
            if (0f > p || 0f > q || life < q)
            {
                ghostWide[i].Hide();
                ghostCore[i].Hide();
                continue;
            }

            float u = q / life;
            float s = 1f + (maxScale - 1f) * EaseOut(u);
            float a = Mathf.Pow(1f - u, 1.6f) * 0.6f * weight;
            ghostWide[i].ShowCentered(PlateCenterX, _cy, (plateWidth + 28f) * s, (PlateHeight + 28f) * s, Glow(MainColor, a));
            ghostCore[i].ShowCentered(PlateCenterX, _cy, (plateWidth + 8f) * s, (PlateHeight + 8f) * s, Glow(Lerp(WhiteColor, MainColor, 0.3f + 0.7f * u), a * 0.5f));
        }

        // 제목판 모양 충격 테두리 1겹(지연 0.02초, 0.5초, e = 1 + 30 x EaseOut)
        float shockQ = p - 0.02f;
        if (0f <= p && 0f <= shockQ && 0.5f >= shockQ)
        {
            float u = shockQ / 0.5f;
            float e = 1f + 30f * EaseOut(u);
            float a = Mathf.Pow(1f - u, 1.3f) * 0.8f;
            float w = plateWidth + e * 2f + 42f;
            float h = PlateHeight + e * 2f * 1.15f + 42f;
            shockMain.ShowCentered(PlateCenterX, _cy, w, h, Glow(MainColor, a * 0.8f));
            shockCore.ShowCentered(PlateCenterX, _cy, w, h, Glow(Lerp(WhiteColor, CoreColor, u), a));
        }
        else
        {
            shockMain.Hide();
            shockCore.Hide();
        }

        // 딱: 제목판 바깥 테두리 빛 섬광
        float dm = _t - MeetFrame / FrameRate;
        if (0f <= dm && 0.45f > dm)
        {
            float k = 0.06f > dm ? 1f : Mathf.Exp(-(dm - 0.06f) / 0.1f);
            meetHalo.ShowCentered(PlateCenterX, _cy, plateWidth + 2f + 50f, PlateHeight + 2f + 50f, Glow(MainColor, k));
            meetLine.ShowCentered(PlateCenterX, _cy, plateWidth + 2f + 50f, PlateHeight + 2f + 50f, Glow(CoreColor, k));
        }
        else
        {
            meetHalo.Hide();
            meetLine.Hide();
        }

        // 부드러운 빛 점 + 픽셀 입자
        if (null == softGraphic || null == pixelGraphic) return;

        softGraphic.BeginQuads();
        pixelGraphic.BeginQuads();

        if (_t < pT + 0.05f) DrawTease(_t, _cy);
        if (0f <= p && 1.1f > p) DrawShards(p, _cy);
        float tq = _t - TraceStartFrame / FrameRate;
        if (0f <= tq && 0.9f > tq) DrawTrace(tq, _t, _cy);
        if (0f <= p) DrawIdle(_it, _cy);

        softGraphic.Commit();
        pixelGraphic.Commit();
        bHasParticles = true;
    }

    // 빛 점: 중심 = 입자 픽셀이 그려지는 반올림 칸의 정중앙(칸 좌상단 + 0.5, ● 2x2만 + 1). 좌표는 시뮬레이션 좌표(왼쪽 위 원점, 아래쪽 +).
    private void GlowDot(float _u, float _v, float _radius, Color32 _rgb, float _intensity, int _shape)
    {
        if (0.003f >= _intensity) return;

        float half = ShapeCircle == _shape ? 1f : 0.5f;
        softGraphic.Soft(RoundHalfUp(_u) + half, -(RoundHalfUp(_v) + half), _radius, Glow(_rgb, _intensity));
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
            case ShapeCross:
                PxRect(x - 1, y - 1, 1, 1, c);
                PxRect(x + 1, y - 1, 1, 1, c);
                PxRect(x, y, 1, 1, c);
                PxRect(x - 1, y + 1, 1, 1, c);
                PxRect(x + 1, y + 1, 1, 1, c);
                break;
            case ShapeCross5:
                PxRect(x - 2, y - 2, 1, 1, c);
                PxRect(x + 2, y - 2, 1, 1, c);
                PxRect(x - 1, y - 1, 1, 1, c);
                PxRect(x + 1, y - 1, 1, 1, c);
                PxRect(x, y, 1, 1, c);
                PxRect(x - 1, y + 1, 1, 1, c);
                PxRect(x + 1, y + 1, 1, 1, c);
                PxRect(x - 2, y + 2, 1, 1, c);
                PxRect(x + 2, y + 2, 1, 1, c);
                break;
            case ShapeDiamond:
                PxRect(x, y - 2, 1, 1, c);
                PxRect(x - 1, y - 1, 3, 1, c);
                PxRect(x - 2, y, 5, 1, c);
                PxRect(x - 1, y + 1, 3, 1, c);
                PxRect(x, y + 2, 1, 1, c);
                break;
        }

        // 큰 모양은 가운데 한 칸을 밝게
        if (ShapePlus5 == _shape || ShapeCross5 == _shape || ShapeDiamond == _shape) PxRect(x, y, 1, 1, PixelColor(Lerp(_rgb, WhiteColor, 0.6f)));
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
            GlowDot(cu, cv, 6f, MainColor, 0.5f * (1f - d / 0.2f), shape);
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

    // ③ 보석 조각 22개(금 계열)
    private void DrawShards(float _p, float _cy)
    {
        float q = _p;
        const float kd = 2.4f;
        const float gr = 420f;
        float e = 1f - Mathf.Exp(-kd * q);
        for (int j = 0; ShardCount > j; j++)
        {
            float life = 0.62f + Rnd(j + 11) * 0.42f;
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

            int shape = ShardShape[j % 12];
            if (0.55f < fr) shape = ReducedShape[shape];
            bool bBig = ShapePlus5 == shape || ShapeCross5 == shape || ShapeDiamond == shape;
            GlowDot(u, v, bBig ? 7f : 4f, Tone(0), 0.45f * (1f - fr), shape);
            Pix(shape, u, v, 0.05f > q ? WhiteColor : Tone(tone));
        }
    }

    private void PathPoint(float _d, int _dir, float _cy, out float _u, out float _v)
    {
        float sx = 0 < _dir ? PlateRight : PlateLeft - 1;
        float top = _cy - 12f - 1f;
        float bottom = _cy + 12f;
        if (137f >= _d)
        {
            _u = Mathf.Max(PlateLeft - 1, Mathf.Min(PlateRight, PlateCenterX + _d * _dir));
            _v = top;
            return;
        }

        if (137f + 25f >= _d)
        {
            _u = sx;
            _v = top + (_d - 137f);
            return;
        }

        _u = sx - (_d - 137f - 25f - 1f) * _dir;
        _v = bottom;
    }

    // ④ 빛 머리 2개가 위 가운데에서 갈라져 둘레를 8박으로 뛰어 아래 가운데에서 만난다
    private void DrawTrace(float _q, float _t, float _cy)
    {
        int step = 0;
        for (int j = 0; TraceBeats >= j; j++)
        {
            if (_q >= StepSeconds[j]) step = j;
        }

        float headD = Mathf.Min(TraceHalf, step / (float)TraceBeats * TraceHalf);
        float pu;
        float pv;

        // 지나간 길: 머리가 지나간 시각부터 0.4초 식음
        for (int d = 0; d <= headD; d++)
        {
            int sj = Mathf.CeilToInt(d / TraceHalf * TraceBeats);
            float age = _q - StepSeconds[Mathf.Min(TraceBeats, sj)];
            if (0.4f < age) continue;

            float k = 1f - age / 0.4f;
            for (int dir = 1; -1 <= dir; dir -= 2)
            {
                PathPoint(d, dir, _cy, out pu, out pv);
                Color32 color = 0.05f > age ? WhiteColor : Lerp(MainColor, CoreColor, k * 0.6f);
                if (0.25f < k || 0 == (Mathf.FloorToInt(age * 24f) & 1)) Pix(ShapeDot, pu, pv, color);
                if (0 == d % 3) GlowDot(pu, pv, 5f, MainColor, 0.35f * k, ShapeDot);
            }
        }

        // 머리 + 박자 반짝
        if (StepSeconds[TraceBeats] + 0.02f > _q)
        {
            for (int dir = 1; -1 <= dir; dir -= 2)
            {
                PathPoint(headD, dir, _cy, out pu, out pv);
                GlowDot(pu, pv, 11f, Lerp(MainColor, WhiteColor, 0.5f), 0.9f, ShapePlus5);
                Pix(ShapePlus5, pu, pv, WhiteColor);
            }
        }

        for (int j = 1; TraceBeats > j; j++)
        {
            float d = _q - StepSeconds[j];
            if (0f > d || 0.22f < d) continue;

            int fr = Mathf.FloorToInt(d * 24f);
            int shape = TraceBeatShape(fr);
            for (int dir = 1; -1 <= dir; dir -= 2)
            {
                PathPoint(j / (float)TraceBeats * TraceHalf, dir, _cy, out pu, out pv);
                Pix(shape, pu, pv, CoreColor);
            }
        }

        // 주변 8곳 반짝이: 박자마다 하나씩
        for (int j = 0; SpotCount > j; j++)
        {
            float tj = StepSeconds[Mathf.Min(TraceBeats, j + 1)] - 0.01f;
            float d = _q - tj;
            if (0f > d || 0.25f < d) continue;

            int fr = Mathf.FloorToInt(d * 24f);
            int shape = 0 != (j & 1) ? SpotCrossShape(fr) : ShapeShift(fr);
            float su = PlateCenterX + SpotX[j];
            float sv = _cy + SpotY[j];
            GlowDot(su, sv, 9f, Tone(0), 0.7f * (1f - d / 0.25f), shape);
            Pix(shape, su, sv, 2 > fr ? WhiteColor : Tone(0));
        }

        // 만남 '딱': 아래 가운데 빛 + 옆으로 튀는 불티 8개
        float dm = _t - MeetFrame / FrameRate;
        if (0f <= dm && 0.45f > dm)
        {
            float k = 0.06f > dm ? 1f : Mathf.Exp(-(dm - 0.06f) / 0.1f);
            GlowDot(PlateCenterX, _cy + 12f, 26f, Lerp(MainColor, WhiteColor, 0.5f), k, ShapeDot);
            float fr = dm / 0.45f;
            for (int j = 0; 8 > j; j++)
            {
                if (0.75f < fr && 0 != (Mathf.FloorToInt(dm * 24f) & 1)) continue;

                int dir = 0 != (j & 1) ? 1 : -1;
                float sp = 60f + Rnd(j + 50) * 70f;
                float vy = -30f - Rnd(j + 60) * 60f;
                float u = PlateCenterX + dir * sp * dm;
                float v = _cy + 12f + vy * dm + 160f * dm * dm;
                int shape = 0.4f > fr ? (0 != j % 3 ? ShapePlus : ShapeDiamond) : ShapeDot;
                GlowDot(u, v, 5f, Tone(0), 0.4f * (1f - fr), shape);
                Pix(shape, u, v, Tone(0.4f > fr ? 0 : 1));
            }
        }
    }

    // [2, 3, 5, 2, 0]
    private static int TraceBeatShape(int _frame)
    {
        switch (Mathf.Min(4, _frame))
        {
            case 0: return ShapePlus;
            case 1: return ShapePlus5;
            case 2: return ShapeCross5;
            case 3: return ShapePlus;
            default: return ShapeDot;
        }
    }

    // [0, 4, 5, 4, 0]
    private static int SpotCrossShape(int _frame)
    {
        switch (Mathf.Min(4, _frame))
        {
            case 1: return ShapeCross;
            case 2: return ShapeCross5;
            case 3: return ShapeCross;
            default: return ShapeDot;
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
            GlowDot(u, v, 4f, Tone(0), 0.3f * (1f - fr), shape);
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
            int shape = ShapeShift(fr);
            float su = PlateCenterX + SpotX[j];
            float sv = _cy + SpotY[j];
            GlowDot(su, sv, 7f, Tone(0), 0.5f * (1f - d / 0.25f), shape);
            Pix(shape, su, sv, Tone(0));
        }
    }

    // 유니티 라이프사이클 이벤트 함수

    private void OnDisable()
    {
        HideAll();
    }
}
