using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 희귀 카드(에메랄드 · 프리즘 · 루비) 등장 연출. 승인된 시안(Docs/Roguelike/_Work/RareCardReveal_Plan.md, 팡 방식 G = 잔상 + 충격파 + 빛살)을 그대로 옮겼다.
///
/// 시간표(희귀 카드 시작 = 0초):
///   ① 두근 0 ~ 0.45   뒷면 테두리에서 등급색 빛이 새고, 박자 {0, 0.2, 0.34}마다 1px씩 떨리며 모서리가 반짝인다. 접히는 동안 등급색 실루엣이 0.75까지 차오른다.
///   ② 팡   0.65(공개)  흰 실루엣 → 등급색, 후광 폭발, 카드 잔상 2장 확대, 카드 모양 충격파, 회전 빛살 12줄, 보석 조각 22개
///   ③ 타다다닥 0.99 ~ 1.394  빛 머리 2개가 위 가운데에서 갈라져 테두리를 점점 빠르게 뛰어 내려가 아래에서 만난다('딱'). 주변 8곳 반짝
///   ④ 유지 1.594 ~     후광 숨쉬기, 회전 빛살 은은하게, 피어오르는 입자, 주변 반짝
///
/// 그리는 방식(성능): 빛은 빌더가 구운 알파 텍스처 Image(HDR 세기 = 정점 알파, UIGlow 가산)라 매 프레임 계산은 위치·크기·회전·색 쓰기뿐이다.
/// 값이 바뀐 Image만 쓰고(지난 값 캐시), 끝난 요소는 끈다. 픽셀 입자와 부드러운 빛 점은 30fps 틱이 바뀔 때만 다시 채운다. 초기화 이후 힙 할당 없음.
/// 층: 뒤 층(이 컴포넌트가 붙은 RareFxBack_n)은 모든 카드보다 먼저, 앞 층(RareFxFront_n)은 모든 카드 위에 그려진다. 둘 다 자식 Canvas라
/// 매 프레임 변화가 화면의 나머지를 다시 배칭하지 않는다.
///
/// 좌표: 층 루트 = 카드 그림 가운데(슬롯 위치 + (25, 41)). 자식 위치는 루트 기준 px, y는 위쪽이 +. 시안 좌표(게임 px, y 아래로 증가)는
/// 카드 왼쪽 위 기준 (u, v)로 받아 루트 기준(u - 25, 41 - v)으로 바꾼다.
/// 시간은 슬롯이 Tick으로 넘기는 dt를 이 컴포넌트가 직접 센다(슬롯과 같은 dt라 어긋나지 않는다). 시작 전에는 음수 시간(지연)이다.
/// </summary>
[DisallowMultipleComponent]
public class UI_RoguelikeRareRevealFx : MonoBehaviour
{
    // 시간표(희귀 카드 시작 기준 초)
    // 효과음 지점을 이만큼(초)보다 오래 전에 지나쳤으면(프레임이 크게 끊김) 소리를 내지 않는다. 한꺼번에 몰려 터지는 것을 막는다.
    private const float CueStaleSeconds = 0.25f;
    public const float TeaseSeconds = 0.45f;
    public const float RevealSeconds = 0.65f;       // 두근 0.45 + 접힘 0.2
    private const float TraceStartSeconds = 0.99f;  // 두근 0.45 + 뒤집기 0.5 + 0.04
    private const float MeetOffsetSeconds = 0.404f;
    private const float IdleOffsetSeconds = 0.2f;   // 만남 뒤
    private const float MeetSeconds = TraceStartSeconds + MeetOffsetSeconds;
    private const float IdleStartSeconds = MeetSeconds + IdleOffsetSeconds;

    private const float GlowMax = 2.5f;          // M_UILootGlowAdd / M_UILootParticles의 _GlowMax와 같아야 한다
    private const float PixelGlow = 1.0f;        // 픽셀 입자 발광 배율(1 = 색 그대로)
    private const float TickRate = 30.0f;
    private const float RootOffsetX = 25.0f;     // 슬롯 왼쪽 아래 기준 카드 그림 가운데
    private const float RootOffsetY = 41.0f;
    private const float CenterY = 0.5f;          // 시안의 카드 세로 중심(Y + 40.5)이 루트에서 위로 0.5px
    public const int CardWidth = 50;
    public const int CardRows = 81;
    private const int TraceHalf = 26 + CardRows + 1 + 26;   // 위 반변 + 옆변 + 아래 반변
    public const float RayMaxLength = 118.0f;
    private const float PrismHueSpeed = 140.0f;

    // 픽셀 모양(시안 pix())
    private const int ShapeDot = 0;
    private const int ShapeCircle = 1;
    private const int ShapePlus = 2;
    private const int ShapePlus5 = 3;
    private const int ShapeCross = 4;
    private const int ShapeCross5 = 5;
    private const int ShapeDiamond = 6;

    /// <summary>두근 박자 시각(희귀 카드 시작 기준 초). 슬롯이 같은 표로 떨림(1px)을 만든다.</summary>
    public static readonly float[] BeatTimes = { 0.0f, 0.2f, 0.34f };
    private static readonly float[] TraceSteps = { 0.0f, 0.075f, 0.14f, 0.198f, 0.25f, 0.296f, 0.336f, 0.372f, 0.404f };
    // 카드 중심 기준 반짝이 자리(시안 SPOTS, 게임 px, y 아래로 증가)
    private static readonly float[] SpotX = { -36.0f, 31.0f, 37.0f, -31.0f, -6.0f, 22.0f, -40.0f, 9.0f };
    private static readonly float[] SpotY = { -30.0f, -46.0f, 6.0f, 27.0f, -54.0f, 47.0f, -3.0f, 58.0f };
    // 시안 RAYS[deg, 폭deg, 길이배]
    private static readonly float[] RayDeg = { 0.0f, 27.0f, 58.0f, 86.0f, 112.0f, 141.0f, 172.0f, 205.0f, 232.0f, 262.0f, 291.0f, 322.0f };
    private static readonly float[] RayWidthDeg = { 9.0f, 5.0f, 11.0f, 4.0f, 8.0f, 6.0f, 10.0f, 4.0f, 9.0f, 6.0f, 11.0f, 5.0f };
    private static readonly float[] RayLengthMul = { 1.0f, 0.7f, 0.95f, 0.6f, 0.85f, 0.75f, 1.0f, 0.55f, 0.9f, 0.7f, 0.95f, 0.65f };
    // 시안 SHARDS 각도(도)와 모양
    private static readonly float[] ShardAngle = { -150.0f, -128.0f, -112.0f, -100.0f, -86.0f, -74.0f, -60.0f, -40.0f, -20.0f, -165.0f, -8.0f, -95.0f, -70.0f, -118.0f, -140.0f, -52.0f, 12.0f, 192.0f, -30.0f, -158.0f, -106.0f, -80.0f };
    private static readonly int[] ShardShapes = { ShapeDiamond, ShapePlus5, ShapeCross5, ShapeCircle, ShapeDiamond, ShapePlus, ShapeCross, ShapeDot, ShapeDiamond, ShapePlus5, ShapeCircle, ShapeCross5 };
    private static readonly int[] ReducedShape = { ShapeDot, ShapeDot, ShapeDot, ShapePlus, ShapeDot, ShapeCross, ShapePlus };
    // 잔상 [지연, 길이, 최대 배율, 가중]
    private static readonly float[] GhostDelay = { 0.05f, 0.13f };
    private static readonly float[] GhostDuration = { 0.36f, 0.36f };
    private static readonly float[] GhostMaxScale = { 1.45f, 1.25f };
    private static readonly float[] GhostWeight = { 0.55f, 0.35f };
    private static readonly int[] BeatCorner = { 0, 1, 2 };

    private const int ShardCount = 22;
    private const int SpotCount = 8;
    private const int SparkCount = 8;
    private const int GroupCount = 6;

    // 팔레트(시안 GRADES, sRGB). 입자 계열 = 등급별 (밝음, 중간, 어두움) 묶음 여러 개
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32[] PrismBase =
    {
        new Color32(179, 245, 204, 255), new Color32(168, 235, 255, 255), new Color32(199, 189, 255, 255),
        new Color32(255, 184, 224, 255), new Color32(255, 204, 184, 255), new Color32(255, 242, 179, 255),
    };

    private sealed class Palette
    {
        public Color32 core;
        public Color32 main;
        public Color32 deep;
        public Color32[] families;   // 계열 수 x 3톤
        public int familyCount;
        public bool bPrism;
    }

    /// <summary>Image 하나를 지난 값 캐시로 쓴다. 크기는 일반 Image = localScale(기준 크기 대비), 9-slice = sizeDelta.</summary>
    private sealed class FxImage
    {
        private readonly Image image;
        private readonly RectTransform rect;
        private readonly bool bSliced;
        private readonly Vector2 baseSize;
        private bool bOn;
        private Vector2 position;
        private Vector2 size;
        private float angle;
        private Color32 color;

        public FxImage(Image _image)
        {
            image = _image;
            rect = _image.rectTransform;
            bSliced = Image.Type.Sliced == _image.type;
            baseSize = rect.sizeDelta;
            image.raycastTarget = false;
            image.maskable = false;
            Reset();
        }

        public void Show(float _x, float _y, float _width, float _height, float _angle, Color32 _color)
        {
            if (0 == _color.a)
            {
                Hide();
                return;
            }

            if (false == bOn)
            {
                bOn = true;
                image.enabled = true;
            }

            if (_x != position.x || _y != position.y)
            {
                position = new Vector2(_x, _y);
                rect.anchoredPosition = position;
            }

            if (true == bSliced)
            {
                if (_width != size.x || _height != size.y)
                {
                    size = new Vector2(_width, _height);
                    rect.sizeDelta = size;
                }
            }
            else
            {
                float sx = _width / baseSize.x;
                float sy = _height / baseSize.y;
                if (sx != size.x || sy != size.y)
                {
                    size = new Vector2(sx, sy);
                    rect.localScale = new Vector3(sx, sy, 1.0f);
                }
            }

            if (_angle != angle)
            {
                angle = _angle;
                rect.localRotation = Quaternion.Euler(0.0f, 0.0f, _angle);
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

        public void Reset()
        {
            bOn = true;
            position = new Vector2(float.NaN, 0.0f);
            size = new Vector2(float.NaN, 0.0f);
            angle = float.NaN;
            color = new Color32(0, 0, 0, 0);
            Hide();
        }
    }

    //외부 의존성 (빌더가 채움)
    [Header("뒤 층 (이 컴포넌트가 붙은 RareFxBack_n 아래)")]
    [SerializeField] private RectTransform backRoot;
    [SerializeField] private Image haloCoreImage;
    [SerializeField] private Image haloMainImage;
    [SerializeField] private Image haloDeepImage;
    [SerializeField] private Image[] prismBlobImages = new Image[GroupCount];
    [SerializeField] private Image whiteCoreImage;
    [SerializeField] private RectTransform raysRoot;
    [SerializeField] private Image[] rayImages = new Image[GroupCount];

    [Header("앞 층 (RareFxFront_n 아래)")]
    [SerializeField] private RectTransform frontRoot;
    [SerializeField] private Image edgeGlowImage;
    [SerializeField] private Image[] ghostWideImages = new Image[2];
    [SerializeField] private Image[] ghostCoreImages = new Image[2];
    [SerializeField] private Image shockMainImage;
    [SerializeField] private Image shockCoreImage;
    [SerializeField] private Image meetHaloImage;
    [SerializeField] private Image meetLineImage;
    [SerializeField] private UI_RoguelikeRareRevealParticles softGraphic;
    [SerializeField] private UI_RoguelikeRareRevealParticles pixelGraphic;

    [Header("카드 실루엣")]
    [SerializeField, Tooltip("카드 위에 등급색 실루엣을 덮을 때 쓰는 머티리얼(덮기 UIGlow). 카드가 이 머티리얼을 flashImage에 잠깐 씌운다.")]
    private Material silhouetteMaterial;

    //내부 의존성
    private FxImage haloCore;
    private FxImage haloMain;
    private FxImage haloDeep;
    private FxImage[] prismBlobs;
    private FxImage whiteCore;
    private FxImage[] rays;
    private FxImage edgeGlow;
    private FxImage[] ghostWide;
    private FxImage[] ghostCore;
    private FxImage shockMain;
    private FxImage shockCore;
    private FxImage meetHalo;
    private FxImage meetLine;

    private static readonly Palette[] Palettes = BuildPalettes();
    private static readonly float[] ShardSpeed = BuildShardTable(3, 120.0f, 110.0f);
    private static readonly float[] ShardLife = BuildShardTable(11, 0.62f, 0.42f);
    private static readonly float[] ShardOffsetX = BuildCenteredShardTable(21, 12.0f);
    private static readonly float[] ShardOffsetY = BuildCenteredShardTable(31, 20.0f);

    //상태 변수
    private bool bInitialized;
    private bool bPlaying;
    private float time;
    private Palette palette;
    private float alpha = 1.0f;
    private int flipWidth = CardWidth;
    private int liftPixels;
    private int shakePixels;
    private Vector2 lastRootPosition = new Vector2(float.NaN, 0.0f);
    private int lastTick = int.MinValue;
    private Color32 silhouetteColor;
    // 효과음 지점을 지나쳤는지 가르는 직전 시각. 같은 지점의 효과음이 프레임 경계에서 두 번 나지 않게 한다.
    private float cueTime;

    /// <summary>재생 중(지연 대기 포함)이면 true. Stop 전까지 유지 단계가 계속된다.</summary>
    public bool IsPlaying => bPlaying;

    /// <summary>두근 시작 ~ 유지 시작 사이면 true(다른 카드 어둡게 구간).</summary>
    public bool IsSpotlight => true == bPlaying && 0.0f <= time && time < IdleStartSeconds;

    /// <summary>이번 프레임 카드 위에 덮을 실루엣 색(알파 = 세기, 0이면 없음).</summary>
    public Color32 SilhouetteColor => silhouetteColor;

    public Material SilhouetteMaterial => silhouetteMaterial;

    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// 등급(0 에메랄드 · 1 프리즘 · 2 루비)의 주색 · 심 · 어두운 색. 빛기둥 소환(UI_RoguelikeSummonPillarFx)이 같은 팔레트를 쓴다.
    /// 프리즘은 주색이 _time(초)에 따라 색상환을 돈다. 범위를 벗어난 등급은 에메랄드로 본다.
    /// </summary>
    public static void GetGradeColors(int _gradeIndex, float _time, out Color32 _main, out Color32 _core, out Color32 _deep)
    {
        Palette source = Palettes[Mathf.Clamp(_gradeIndex, 0, Palettes.Length - 1)];
        _core = source.core;
        _deep = source.deep;
        _main = true == source.bPrism ? Hsl(_time * PrismHueSpeed, 0.95f, 0.68f) : source.main;
    }

    /// <summary>
    /// 연출을 시작한다. _teaseDelay초 뒤(= 이 희귀 카드의 시작 시각)부터 두근이 시작된다. _gradeIndex는 카드와 같다(0 에메랄드, 1 프리즘, 2 루비).
    /// </summary>
    public void Play(int _gradeIndex, float _teaseDelay)
    {
        EnsureInitialized();
        if (0 > _gradeIndex || Palettes.Length <= _gradeIndex) return;

        palette = Palettes[_gradeIndex];
        time = -_teaseDelay;
        cueTime = time - 0.0001f;
        bPlaying = true;
        alpha = 1.0f;
        silhouetteColor = new Color32(0, 0, 0, 0);
        HideAll();
    }

    /// <summary>즉시 멈추고 모두 숨긴다.</summary>
    public void Stop()
    {
        bPlaying = false;
        silhouetteColor = new Color32(0, 0, 0, 0);
        if (true == bInitialized) HideAll();
    }

    /// <summary>
    /// 슬롯이 매 프레임 부른다. _dt = 슬롯이 쓴 같은 시간(초), _slotPosition = 슬롯 anchoredPosition, _alpha = 슬롯 canvasGroup 알파,
    /// _flipWidth·_flipLift = 카드의 현재 뒤집힘 폭·뜀, _visualOffset = 카드 그림의 실제 위치(x = 떨림, y = 뜀 + 호버), _bFront = 앞면이 드러났는지.
    /// 호버로 카드가 올라가면 층 전체가 같이 올라간다(뜀은 연출 식이 따로 쓰므로 호버분만 층 위치에 더한다).
    /// </summary>
    public void Tick(float _dt, Vector2 _slotPosition, float _alpha, int _flipWidth, int _flipLift, Vector2 _visualOffset, bool _bFront)
    {
        if (false == bPlaying) return;

        time += _dt;
        PlaySoundCues();
        alpha = _alpha;
        flipWidth = _flipWidth;
        liftPixels = Mathf.Max(0, _flipLift);
        shakePixels = Mathf.RoundToInt(_visualOffset.x);
        float hoverLift = Mathf.Round(_visualOffset.y) - _flipLift;

        Vector2 rootPosition = new Vector2(_slotPosition.x + RootOffsetX, _slotPosition.y + RootOffsetY + hoverLift);
        if (rootPosition.x != lastRootPosition.x || rootPosition.y != lastRootPosition.y)
        {
            lastRootPosition = rootPosition;
            if (null != backRoot) backRoot.anchoredPosition = rootPosition;
            if (null != frontRoot) frontRoot.anchoredPosition = rootPosition;
        }

        if (0.0f > time)
        {
            silhouetteColor = new Color32(0, 0, 0, 0);
            return;
        }

        UpdateSilhouette(_bFront);
        UpdateBackImages();
        UpdateFrontImages();

        int tick = Mathf.FloorToInt(time * TickRate);
        if (tick != lastTick)
        {
            lastTick = tick;
            RebuildParticles();
        }
    }

    // 일반 비즈니스 로직 및 내부 메서드

    /// <summary>
    /// 이번 프레임에 지나친 효과음 지점에서 소리를 낸다(임시 연결 - 전용 클립 수급 후 교체, 기획서 C-2).
    /// 두근 박자(BeatTimes) = RareRevealBeat, 타다다닥 박자(TraceSteps 1 ~ 마지막 직전) = RareRevealTick(박자 j마다 피치 1 + j/12), 만남 = RareRevealChime.
    /// 팡(공개 순간)은 슬롯이 기존 AbilityAppear로 내므로 여기서 내지 않는다.
    /// </summary>
    private void PlaySoundCues()
    {
        float from = cueTime;
        float to = time;
        cueTime = to;

        for (int i = 0; i < BeatTimes.Length; i++)
        {
            if (true == IsCueCrossed(BeatTimes[i], from, to)) Sound.PlayUI(SoundID.RareRevealBeat);
        }

        int n = TraceSteps.Length - 1;
        for (int j = 1; j < n; j++)
        {
            if (true == IsCueCrossed(TraceStartSeconds + TraceSteps[j], from, to)) Sound.PlayUI(SoundID.RareRevealTick, 1.0f, 1.0f + j / 12.0f);
        }

        if (true == IsCueCrossed(MeetSeconds, from, to)) Sound.PlayUI(SoundID.RareRevealChime);
    }

    private static bool IsCueCrossed(float _eventTime, float _from, float _to)
    {
        return _from < _eventTime && _eventTime <= _to && _to - _eventTime <= CueStaleSeconds;
    }

    private void EnsureInitialized()
    {
        if (true == bInitialized) return;
        bInitialized = true;

        haloCore = Wrap(haloCoreImage);
        haloMain = Wrap(haloMainImage);
        haloDeep = Wrap(haloDeepImage);
        whiteCore = Wrap(whiteCoreImage);
        prismBlobs = WrapAll(prismBlobImages);
        rays = WrapAll(rayImages);
        edgeGlow = Wrap(edgeGlowImage);
        ghostWide = WrapAll(ghostWideImages);
        ghostCore = WrapAll(ghostCoreImages);
        shockMain = Wrap(shockMainImage);
        shockCore = Wrap(shockCoreImage);
        meetHalo = Wrap(meetHaloImage);
        meetLine = Wrap(meetLineImage);
    }

    private static FxImage Wrap(Image _image)
    {
        return null != _image ? new FxImage(_image) : null;
    }

    private static FxImage[] WrapAll(Image[] _images)
    {
        int count = null != _images ? _images.Length : 0;
        FxImage[] result = new FxImage[count];
        for (int i = 0; i < count; i++) result[i] = Wrap(_images[i]);
        return result;
    }

    private void HideAll()
    {
        HideOne(haloCore);
        HideOne(haloMain);
        HideOne(haloDeep);
        HideOne(whiteCore);
        HideMany(prismBlobs);
        HideMany(rays);
        HideOne(edgeGlow);
        HideMany(ghostWide);
        HideMany(ghostCore);
        HideOne(shockMain);
        HideOne(shockCore);
        HideOne(meetHalo);
        HideOne(meetLine);
        lastTick = int.MinValue;
        lastRootPosition = new Vector2(float.NaN, 0.0f);
        if (null != softGraphic) softGraphic.ClearQuads();
        if (null != pixelGraphic) pixelGraphic.ClearQuads();
    }

    private static void HideOne(FxImage _image)
    {
        if (null != _image) _image.Reset();
    }

    private static void HideMany(FxImage[] _images)
    {
        if (null == _images) return;
        for (int i = 0; i < _images.Length; i++) HideOne(_images[i]);
    }

    // ---------- 색 ----------

    private static Palette[] BuildPalettes()
    {
        Palette emerald = new Palette
        {
            core = new Color32(235, 255, 240, 255),
            main = new Color32(70, 235, 140, 255),
            deep = new Color32(20, 150, 85, 255),
            families = new Color32[]
            {
                new Color32(205, 255, 220, 255), new Color32(95, 220, 150, 255), new Color32(35, 145, 92, 255),
                new Color32(237, 245, 255, 255), new Color32(189, 209, 237, 255), new Color32(132, 158, 199, 255),
            },
            familyCount = 2,
            bPrism = false,
        };

        Palette prism = new Palette
        {
            core = new Color32(250, 246, 255, 255),
            main = new Color32(200, 170, 255, 255),
            deep = new Color32(130, 105, 225, 255),
            families = new Color32[GroupCount * 3],
            familyCount = GroupCount,
            bPrism = true,
        };
        for (int i = 0; i < GroupCount; i++)
        {
            prism.families[i * 3] = PrismBase[i];
            prism.families[i * 3 + 1] = Scale(PrismBase[i], 0.78f);
            prism.families[i * 3 + 2] = Scale(PrismBase[i], 0.52f);
        }

        Palette ruby = new Palette
        {
            core = new Color32(255, 234, 214, 255),
            main = new Color32(255, 80, 48, 255),
            deep = new Color32(185, 22, 36, 255),
            families = new Color32[]
            {
                new Color32(255, 220, 130, 255), new Color32(255, 150, 60, 255), new Color32(190, 70, 30, 255),
                new Color32(255, 184, 89, 255), new Color32(242, 82, 46, 255), new Color32(140, 26, 31, 255),
            },
            familyCount = 2,
            bPrism = false,
        };

        return new Palette[] { emerald, prism, ruby };
    }

    private static Color32 Scale(Color32 _c, float _k)
    {
        return new Color32((byte)RoundHalfUp(_c.r * _k), (byte)RoundHalfUp(_c.g * _k), (byte)RoundHalfUp(_c.b * _k), 255);
    }

    // 시안 lerpC: 채널마다 반올림
    private static Color32 Lerp(Color32 _a, Color32 _b, float _k)
    {
        return new Color32(
            (byte)RoundHalfUp(_a.r + (_b.r - _a.r) * _k),
            (byte)RoundHalfUp(_a.g + (_b.g - _a.g) * _k),
            (byte)RoundHalfUp(_a.b + (_b.b - _a.b) * _k),
            255);
    }

    // 시안 hsl(0~360, 0~1, 0~1)
    private static Color32 Hsl(float _h, float _s, float _l)
    {
        float h = ((_h % 360.0f) + 360.0f) % 360.0f;
        float c = (1.0f - Mathf.Abs(2.0f * _l - 1.0f)) * _s;
        float x = c * (1.0f - Mathf.Abs((h / 60.0f) % 2.0f - 1.0f));
        float m = _l - c * 0.5f;
        float r;
        float g;
        float b;
        if (60.0f > h) { r = c; g = x; b = 0.0f; }
        else if (120.0f > h) { r = x; g = c; b = 0.0f; }
        else if (180.0f > h) { r = 0.0f; g = c; b = x; }
        else if (240.0f > h) { r = 0.0f; g = x; b = c; }
        else if (300.0f > h) { r = x; g = 0.0f; b = c; }
        else { r = c; g = 0.0f; b = x; }
        return new Color32((byte)RoundHalfUp((r + m) * 255.0f), (byte)RoundHalfUp((g + m) * 255.0f), (byte)RoundHalfUp((b + m) * 255.0f), 255);
    }

    // 시안 mainCol: 프리즘은 시간에 따라 색상환을 돈다
    private Color32 MainColor(float _k)
    {
        if (false == palette.bPrism) return palette.main;
        return Hsl(time * PrismHueSpeed + _k * 60.0f, 0.95f, 0.68f);
    }

    private Color32 Family(int _family, int _tone)
    {
        return palette.families[(_family % palette.familyCount) * 3 + _tone];
    }

    private static int RoundHalfUp(float _v)
    {
        return Mathf.FloorToInt(_v + 0.5f);
    }

    private static byte ToByte(float _v)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(_v) * 255.0f);
    }

    // 가산 색: sRGB 바이트 그대로(셰이더가 선형으로 푼다), 세기 = 정점 알파 x 2.5
    private Color32 Glow(Color32 _rgb, float _intensity)
    {
        _rgb.a = ToByte(_intensity * alpha / GlowMax);
        return _rgb;
    }

    private Color32 PixelColor(Color32 _rgb)
    {
        _rgb.a = ToByte(PixelGlow * alpha / GlowMax);
        return _rgb;
    }

    private static float Clamp01(float _v)
    {
        return Mathf.Clamp01(_v);
    }

    private static float EaseOut(float _v)
    {
        return 1.0f - (1.0f - _v) * (1.0f - _v);
    }

    // 박자 뒤 번쩍: 가장 최근 박자 기준 exp(-d / tau)의 최댓값
    private static float Spike(float _time, float _tau)
    {
        float spike = 0.0f;
        for (int i = 0; i < BeatTimes.Length; i++)
        {
            float d = _time - BeatTimes[i];
            if (0.0f <= d) spike = Mathf.Max(spike, Mathf.Exp(-d / _tau));
        }

        return spike;
    }

    // 시안 rnd(n) = frac(sin(n * 127.1 + 311.7) * 43758.5453), 배정밀도로 계산한다
    private static float Rnd(double _n)
    {
        double x = Math.Sin(_n * 127.1 + 311.7) * 43758.5453;
        return (float)(x - Math.Floor(x));
    }

    // 시안: v = 120 + rnd(j+3)*110, life = 0.62 + rnd(j+11)*0.42, ox = (rnd(j+21)-0.5)*12, oy = (rnd(j+31)-0.5)*20
    private static float[] BuildShardTable(int _seed, float _base, float _range)
    {
        float[] table = new float[ShardCount];
        for (int j = 0; j < ShardCount; j++) table[j] = _base + Rnd(j + _seed) * _range;
        return table;
    }

    private static float[] BuildCenteredShardTable(int _seed, float _range)
    {
        float[] table = new float[ShardCount];
        for (int j = 0; j < ShardCount; j++) table[j] = (Rnd(j + _seed) - 0.5f) * _range;
        return table;
    }

    // ---------- 카드 실루엣 ----------

    private void UpdateSilhouette(bool _bFront)
    {
        silhouetteColor = new Color32(0, 0, 0, 0);

        if (false == _bFront)
        {
            // 접히는 동안 등급색으로 차오른다(0 → 0.75)
            float ft = time - TeaseSeconds;
            if (0.0f > ft) return;

            Color32 c = MainColor(0.0f);
            c.a = ToByte(0.75f * Clamp01(ft / (RevealSeconds - TeaseSeconds)));
            silhouetteColor = c;
            return;
        }

        // 공개: 흰색 0.12초 유지 → 0.22초에 걸쳐 투명해지며 등급색으로
        float p = time - RevealSeconds;
        if (0.0f > p) return;

        if (0.12f > p)
        {
            silhouetteColor = new Color32(255, 255, 255, 255);
        }
        else if (0.34f > p)
        {
            float k = (p - 0.12f) / 0.22f;
            Color32 c = Lerp(White, MainColor(0.0f), Clamp01(k * 1.6f));
            c.a = ToByte(1.0f - k);
            silhouetteColor = c;
        }
    }

    // ---------- 이미지로 그리는 빛 ----------

    private void UpdateBackImages()
    {
        float y = CenterY + liftPixels;
        Color32 m0 = MainColor(0.0f);
        float haloI = 0.0f;
        float haloR = 34.0f;

        if (RevealSeconds > time)
        {
            // ① 두근: 뒤 층 후광이 박자마다 번쩍이며 차오른다
            float progress = Clamp01(time / RevealSeconds);
            float spike = Spike(time, 0.07f);
            haloI = 0.12f + 0.35f * progress + 0.3f * spike;
            haloR = 30.0f + 6.0f * spike + 6.0f * progress;
        }
        else
        {
            // ② 팡 → ④ 유지
            float p = time - RevealSeconds;
            haloI = 0.15f > p ? 1.3f : 0.5f + 0.8f * Mathf.Exp(-(p - 0.15f) / 0.22f);
            haloR = 0.13f > p ? 34.0f + 44.0f * EaseOut(p / 0.13f) : 50.0f + 28.0f * Mathf.Exp(-(p - 0.13f) / 0.25f);
            if (IdleStartSeconds < time) haloI += 0.08f * Mathf.Sin((time - IdleStartSeconds) * Mathf.PI * 2.0f / 1.8f);
        }

        if (0.0f < haloI)
        {
            float w = haloR * 2.0f;
            float h = haloR * 2.0f * 1.32f;
            if (null != haloCore) haloCore.Show(0.0f, y, w, h, 0.0f, Glow(palette.core, Mathf.Min(1.0f, haloI * 0.85f)));
            if (null != haloMain) haloMain.Show(0.0f, y, w, h, 0.0f, Glow(m0, Mathf.Min(1.0f, haloI * 0.6f)));
            if (null != haloDeep) haloDeep.Show(0.0f, y, w, h, 0.0f, Glow(palette.deep, Mathf.Min(1.0f, haloI * 0.25f)));

            // 프리즘: 무지개 덩이 6개가 천천히 돈다
            for (int k = 0; k < prismBlobs.Length; k++)
            {
                if (null == prismBlobs[k]) continue;
                if (false == palette.bPrism)
                {
                    prismBlobs[k].Hide();
                    continue;
                }

                float an = time * 0.9f + k * Mathf.PI / 3.0f;
                float rr = haloR * 0.55f;
                prismBlobs[k].Show(Mathf.Cos(an) * rr, y - Mathf.Sin(an) * rr * 1.32f, rr * 2.0f, rr * 2.0f * 1.32f, 0.0f, Glow(PrismBase[k], Mathf.Min(1.0f, haloI * 0.42f)));
            }

            // 백열 심
            if (null != whiteCore)
            {
                if (1.0f < haloI) whiteCore.Show(0.0f, y, 44.0f, 44.0f * 1.4f, 0.0f, Glow(White, Mathf.Min(1.0f, haloI - 1.0f)));
                else whiteCore.Hide();
            }
        }
        else
        {
            HideOne2(haloCore);
            HideOne2(haloMain);
            HideOne2(haloDeep);
            HideOne2(whiteCore);
            HideMany2(prismBlobs);
        }

        UpdateRays();
    }

    // 회전 빛살: 팡에서 자라며 번쩍였다가 유지 단계에 0.28 x 0.7로 남는다
    private void UpdateRays()
    {
        if (RevealSeconds > time)
        {
            HideMany2(rays);
            return;
        }

        float p = time - RevealSeconds;
        float grow = EaseOut(Clamp01(p / 0.16f));
        float intensity = (0.15f > p ? 1.0f : 0.28f + 0.72f * Mathf.Exp(-(p - 0.15f) / 0.3f)) * 0.7f;
        float lengthMul = (0.15f > p ? 1.0f : 0.62f + 0.38f * Mathf.Exp(-(p - 0.15f) / 0.35f)) * grow;
        if (0.01f >= intensity || 0.001f >= lengthMul)
        {
            HideMany2(rays);
            return;
        }

        // 부모가 가로 Lmul, 세로 Lmul x 1.15로 늘리고 자식이 회전한다(시안의 scale(1, 1.15) 뒤 회전과 같다)
        float d = RayMaxLength * 2.0f;
        if (null != raysRoot)
        {
            Vector3 scale = new Vector3(lengthMul, lengthMul * 1.15f, 1.0f);
            if (scale != raysRoot.localScale) raysRoot.localScale = scale;
            Vector2 pos = new Vector2(0.0f, CenterY);
            if (pos != raysRoot.anchoredPosition) raysRoot.anchoredPosition = pos;
        }

        float rotation = -time * 0.35f * Mathf.Rad2Deg;
        for (int g = 0; g < rays.Length; g++)
        {
            if (null == rays[g]) continue;
            Color32 col = true == palette.bPrism ? PrismBase[g] : (0 != (g & 1) ? MainColor(g) : palette.core);
            rays[g].Show(0.0f, 0.0f, d, d, rotation, Glow(col, intensity));
        }
    }

    private static void HideOne2(FxImage _image)
    {
        if (null != _image) _image.Hide();
    }

    private static void HideMany2(FxImage[] _images)
    {
        if (null == _images) return;
        for (int i = 0; i < _images.Length; i++) HideOne2(_images[i]);
    }

    private void UpdateFrontImages()
    {
        Color32 m0 = MainColor(0.0f);

        // ① 두근: 카드 테두리에서 새는 빛(카드 사각형 둘레, 현재 뒤집힘 폭·뜀 반영)
        if (RevealSeconds + 0.05f > time)
        {
            float spike = Spike(time, 0.08f);
            float a = 0.25f + 0.45f * Clamp01(time / TeaseSeconds) + 0.5f * spike;
            // 두근 박자 효과음은 PlaySoundCues가 낸다.
            if (null != edgeGlow) edgeGlow.Show(shakePixels, CenterY + liftPixels, flipWidth + 1 + 2 * EdgeCentreOffset, CardRows + 1 + 2 * EdgeCentreOffset, 0.0f, Glow(m0, Mathf.Min(1.0f, a * 0.8f)));
        }
        else
        {
            HideOne2(edgeGlow);
        }

        UpdateGhosts();
        UpdateShock();
        UpdateMeet();
    }

    // 링 텍스처(빌더가 굽는다)의 선 중심 ~ 이미지 가장자리 거리(px). 빌더(RareCardRevealBuilder)가 같은 값으로 굽는다.
    public const float EdgeCentreOffset = 14.5f;
    public const float ShockCentreOffset = 21.0f;
    public const float MeetCentreOffset = 25.0f;

    // B 카드 잔상: 카드 실루엣 블러가 커지며 번지고 사라진다
    private void UpdateGhosts()
    {
        if (RevealSeconds > time)
        {
            HideMany2(ghostWide);
            HideMany2(ghostCore);
            return;
        }

        float p = time - RevealSeconds;
        Color32 m0 = MainColor(0.0f);
        for (int i = 0; i < GhostDelay.Length; i++)
        {
            float q = p - GhostDelay[i];
            if (0.0f > q || GhostDuration[i] < q)
            {
                if (i < ghostWide.Length) HideOne2(ghostWide[i]);
                if (i < ghostCore.Length) HideOne2(ghostCore[i]);
                continue;
            }

            float u = q / GhostDuration[i];
            float s = 1.0f + (GhostMaxScale[i] - 1.0f) * EaseOut(u);
            float a = Mathf.Pow(1.0f - u, 1.6f) * 0.6f * GhostWeight[i];
            // 팡(공개 순간 R = 0.65초) 효과음은 슬롯의 AbilityAppear가 낸다.
            if (i < ghostWide.Length && null != ghostWide[i]) ghostWide[i].Show(0.0f, CenterY, GhostWideWidth * s, GhostWideHeight * s, 0.0f, Glow(m0, a));
            if (i < ghostCore.Length && null != ghostCore[i]) ghostCore[i].Show(0.0f, CenterY, GhostCoreWidth * s, GhostCoreHeight * s, 0.0f, Glow(Lerp(White, m0, 0.3f + 0.7f * u), a * 0.5f));
        }
    }

    // 잔상 텍스처 크기(카드 50 x 82 + 여백 14 / 4). 빌더가 같은 값으로 굽는다.
    public const float GhostWideWidth = 78.0f;
    public const float GhostWideHeight = 110.0f;
    public const float GhostCoreWidth = 58.0f;
    public const float GhostCoreHeight = 90.0f;

    // C 카드 모양 충격파: 카드 윤곽 모양 빛 테두리가 바깥으로 퍼진다
    private void UpdateShock()
    {
        float q = time - RevealSeconds - 0.02f;
        if (0.0f > q || 0.5f < q)
        {
            HideOne2(shockMain);
            HideOne2(shockCore);
            return;
        }

        float u = q / 0.5f;
        float e = 1.0f + 30.0f * EaseOut(u);
        float a = Mathf.Pow(1.0f - u, 1.3f) * 0.8f;
        float w = CardWidth + e * 2.0f;
        float h = CardRows + e * 2.0f * 1.15f;
        Color32 m1 = MainColor(1.0f);
        if (null != shockMain) shockMain.Show(0.0f, CenterY, w + 2.0f * ShockCentreOffset, h + 2.0f * ShockCentreOffset, 0.0f, Glow(m1, a * 0.8f));
        if (null != shockCore) shockCore.Show(0.0f, CenterY, w + 2.0f * ShockCentreOffset, h + 2.0f * ShockCentreOffset, 0.0f, Glow(Lerp(White, palette.core, u), a));
    }

    // 만남 '딱': 카드 바깥 테두리 빛 섬광
    private void UpdateMeet()
    {
        float dm = time - MeetSeconds;
        if (0.0f > dm || 0.45f <= dm)
        {
            HideOne2(meetHalo);
            HideOne2(meetLine);
            return;
        }

        float k = 0.06f > dm ? 1.0f : Mathf.Exp(-(dm - 0.06f) / 0.1f);
        float w = CardWidth + 2.0f;
        float h = CardRows + 2.0f;
        // 만남 차임 효과음은 PlaySoundCues가 낸다.
        if (null != meetHalo) meetHalo.Show(0.0f, CenterY, w + 2.0f * MeetCentreOffset, h + 2.0f * MeetCentreOffset, 0.0f, Glow(MainColor(0.0f), k));
        if (null != meetLine) meetLine.Show(0.0f, CenterY, w + 2.0f * MeetCentreOffset, h + 2.0f * MeetCentreOffset, 0.0f, Glow(palette.core, k));
    }

    // ---------- 픽셀 입자·부드러운 빛 점 (30fps 틱마다) ----------

    private void RebuildParticles()
    {
        if (null == softGraphic || null == pixelGraphic) return;

        softGraphic.BeginQuads();
        pixelGraphic.BeginQuads();

        if (RevealSeconds + 0.05f > time) DrawTease();

        if (RevealSeconds <= time)
        {
            float p = time - RevealSeconds;
            if (1.1f > p) DrawShards(p);

            float q = time - TraceStartSeconds;
            if (0.0f <= q && 0.9f > q) DrawTrace(q);

            DrawIdle(time - IdleStartSeconds);
        }

        softGraphic.Commit();
        pixelGraphic.Commit();
    }

    // 시안 glowDot: 카드 왼쪽 위 기준 (u, v)
    private void GlowDot(float _u, float _v, float _radius, Color32 _rgb, float _intensity)
    {
        if (0.003f >= _intensity) return;
        // 빛 중심 = 픽셀 입자(Pix)가 그려지는 반올림 칸의 정중앙(칸 좌상단 + 0.5). 소수 좌표를 그대로 쓰면 평균 0.5px 왼쪽 위로 치우친다.
        softGraphic.Soft(RoundHalfUp(_u) + 0.5f - RootOffsetX, RootOffsetY - (RoundHalfUp(_v) + 0.5f), _radius, Glow(_rgb, _intensity));
    }

    // 시안 pix: 카드 왼쪽 위 기준 (u, v)에 JS Math.round로 맞춘 칸. 칸은 [x, x+1) x [y, y+1)(y 아래로 증가)
    private void PxRect(int _x, int _y, int _w, int _h, Color32 _rgb)
    {
        float x0 = _x - RootOffsetX;
        float y1 = RootOffsetY - _y;
        pixelGraphic.Rect(x0, y1 - _h, x0 + _w, y1, _rgb);
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

        // 큰 모양은 가운데 한 칸을 밝게(시안 pixCore)
        if (ShapePlus5 == _shape || ShapeCross5 == _shape || ShapeDiamond == _shape) PxRect(x, y, 1, 1, PixelColor(Lerp(_rgb, White, 0.6f)));
    }

    // ① 두근의 모서리 반짝 + 뒷면 금 마름모 자리 빛
    private void DrawTease()
    {
        float spike = Spike(time, 0.08f);
        Color32 m0 = MainColor(0.0f);
        float u0 = RootOffsetX + shakePixels - flipWidth * 0.5f;
        float v0 = -liftPixels;

        // 뒷면 가운데 금 마름모가 등급색으로 빛난다
        GlowDot(RootOffsetX + shakePixels, 40.0f - liftPixels, 9.0f + 6.0f * spike, m0, 0.35f + 0.5f * spike);

        // 박자마다 모서리 한 곳에서 `. + +5 + .` 24Hz
        for (int j = 0; j < BeatTimes.Length; j++)
        {
            float d = time - BeatTimes[j];
            if (0.0f > d || 0.2f < d) continue;

            float cu;
            float cv;
            switch (BeatCorner[j])
            {
                case 0: cu = u0 - 1.0f; cv = v0 - 1.0f; break;
                case 1: cu = u0 + flipWidth; cv = v0 + CardRows; break;
                default: cu = u0 + flipWidth; cv = v0 - 1.0f; break;
            }

            int f = Mathf.FloorToInt(d * 24.0f);
            int shape = 0 == f ? ShapeDot : (1 == f ? ShapePlus : (2 == f ? ShapePlus5 : (3 == f ? ShapePlus : ShapeDot)));
            Pix(shape, cu, cv, palette.core);
            GlowDot(cu, cv, 6.0f, m0, 0.5f * (1.0f - d / 0.2f));
        }
    }

    // ② 팡의 보석 조각 22개
    private void DrawShards(float _p)
    {
        float q = Mathf.Floor(_p * 30.0f) / 30.0f;
        const float kd = 2.4f;
        const float g = 420.0f;
        float e = 1.0f - Mathf.Exp(-kd * q);

        for (int j = 0; j < ShardCount; j++)
        {
            float life = ShardLife[j];
            if (life < q) continue;

            float angle = ShardAngle[j] * Mathf.Deg2Rad;
            float vx = Mathf.Cos(angle) * ShardSpeed[j];
            float vy = Mathf.Sin(angle) * ShardSpeed[j];
            float u = RootOffsetX + ShardOffsetX[j] + vx / kd * e;
            float v = 40.5f + ShardOffsetY[j] + (vy + g / kd) / kd * e - g * q / kd;
            float fr = q / life;
            int fam = j % GroupCount;
            int tone = 0.35f > fr ? 0 : (0.7f > fr ? 1 : 2);
            if (0.82f < fr && 0 != (Mathf.FloorToInt(q * 24.0f) & 1)) continue;

            int shape = ShardShapes[j % ShardShapes.Length];
            if (0.55f < fr) shape = ReducedShape[shape];
            Color32 col = 0.05f > q ? White : Family(fam, tone);
            bool bBig = ShapePlus5 == shape || ShapeCross5 == shape || ShapeDiamond == shape;
            GlowDot(u, v, true == bBig ? 7.0f : 4.0f, Family(fam, 0), 0.45f * (1.0f - fr));
            Pix(shape, u, v, col);
        }
    }

    // 타다다닥 경로(카드 바깥 1px): 위 가운데 → 옆(오른쪽 시계 / 왼쪽 반시계) → 아래 가운데
    private static void PathPoint(float _d, int _dir, out float _u, out float _v)
    {
        if (26.0f >= _d)
        {
            _u = 25.0f + _d * _dir;
            _v = -1.0f;
        }
        else if (26.0f + CardRows + 1 >= _d)
        {
            _u = 0 < _dir ? CardWidth : -1.0f;
            _v = -1.0f + (_d - 26.0f);
        }
        else
        {
            _u = (0 < _dir ? CardWidth : -1.0f) - (_d - 27.0f - CardRows) * _dir;
            _v = CardRows;
        }
    }

    // ③ 타다다닥
    private void DrawTrace(float _q)
    {
        int n = TraceSteps.Length - 1;
        Color32 m0 = MainColor(0.0f);
        int step = 0;
        for (int j = 0; j <= n; j++)
        {
            if (_q >= TraceSteps[j]) step = j;
        }

        float headD = Mathf.Min(TraceHalf, (float)step / n * TraceHalf);

        // 지나간 길: 머리가 지나간 시각부터 식는다
        for (int d = 0; d <= headD; d++)
        {
            int sj = Mathf.CeilToInt((float)d / TraceHalf * n);
            float tp = TraceSteps[Mathf.Min(n, sj)];
            float age = _q - tp;
            if (0.4f < age) continue;

            float k = 1.0f - age / 0.4f;
            Color32 main = MainColor(d / 20.0f);
            for (int dir = 1; dir >= -1; dir -= 2)
            {
                PathPoint(d, dir, out float pu, out float pv);
                Color32 col = 0.05f > age ? White : Lerp(main, palette.core, k * 0.6f);
                if (0.25f < k || 0 == (Mathf.FloorToInt(age * 24.0f) & 1)) Pix(ShapeDot, pu, pv, col);
                if (0 == d % 3) GlowDot(pu, pv, 5.0f, main, 0.35f * k);
            }
        }

        // 머리 + 박자 반짝
        if (_q < TraceSteps[n] + 0.02f)
        {
            for (int dir = 1; dir >= -1; dir -= 2)
            {
                PathPoint(headD, dir, out float pu, out float pv);
                GlowDot(pu, pv, 11.0f, Lerp(m0, White, 0.5f), 0.9f);
                Pix(ShapePlus5, pu, pv, White);
                // 타다다닥 틱 효과음은 PlaySoundCues가 낸다.
            }
        }

        for (int j = 1; j < n; j++)
        {
            float d = _q - TraceSteps[j];
            if (0.0f > d || 0.22f < d) continue;

            int f = Mathf.FloorToInt(d * 24.0f);
            int shape = 0 == f ? ShapePlus : (1 == f ? ShapePlus5 : (2 == f ? ShapeCross5 : (3 == f ? ShapePlus : ShapeDot)));
            for (int dir = 1; dir >= -1; dir -= 2)
            {
                PathPoint((float)j / n * TraceHalf, dir, out float pu, out float pv);
                Pix(shape, pu, pv, palette.core);
            }
        }

        // 주변 8곳 반짝이: 박자마다 하나씩
        for (int j = 0; j < SpotCount; j++)
        {
            float tj = TraceSteps[Mathf.Min(n, j + 1)] - 0.01f;
            float d = _q - tj;
            if (0.0f > d || 0.25f < d) continue;

            int f = Mathf.FloorToInt(d * 24.0f);
            int shape;
            if (0 != (j & 1)) shape = 0 == f ? ShapeDot : (1 == f || 3 == f ? ShapeCross : (2 == f ? ShapeCross5 : ShapeDot));
            else shape = 0 == f ? ShapeDot : (1 == f || 3 == f ? ShapePlus : (2 == f ? ShapePlus5 : ShapeDot));
            float su = RootOffsetX + SpotX[j];
            float sv = 40.5f + SpotY[j];
            Color32 fam0 = Family(j, 0);
            GlowDot(su, sv, 9.0f, fam0, 0.7f * (1.0f - d / 0.25f));
            Pix(shape, su, sv, 2 > f ? White : fam0);
        }

        // 만남 '딱': 아래 가운데 빛 + 옆으로 튀는 불티
        float dm = time - MeetSeconds;
        if (0.0f <= dm && 0.45f > dm)
        {
            float k = 0.06f > dm ? 1.0f : Mathf.Exp(-(dm - 0.06f) / 0.1f);
            GlowDot(RootOffsetX, CardRows, 22.0f, Lerp(m0, White, 0.5f), k);

            float q2 = Mathf.Floor(dm * 30.0f) / 30.0f;
            float fr = q2 / 0.45f;
            for (int j = 0; j < SparkCount; j++)
            {
                int dir = 0 != (j & 1) ? 1 : -1;
                float speed = 60.0f + Rnd(j + 50) * 70.0f;
                float vy = -30.0f - Rnd(j + 60) * 60.0f;
                float u = RootOffsetX + dir * speed * q2;
                float v = CardRows + vy * q2 + 160.0f * q2 * q2;
                if (0.75f < fr && 0 != (Mathf.FloorToInt(q2 * 24.0f) & 1)) continue;

                int shape = 0.4f > fr ? (0 != j % 3 ? ShapePlus : ShapeDiamond) : ShapeDot;
                Pix(shape, u, v, Family(j, 0.4f > fr ? 0 : 1));
                GlowDot(u, v, 5.0f, Family(j, 0), 0.4f * (1.0f - fr));
            }
        }
    }

    // ④ 유지: 피어오르는 입자 + 주변 반짝
    private void DrawIdle(float _it)
    {
        if (-0.1f >= _it) return;

        int mFirst = Mathf.Max(0, Mathf.FloorToInt((_it - 1.8f) / 0.11f));
        int mLast = Mathf.FloorToInt(_it / 0.11f);
        for (int m = mFirst; m <= mLast; m++)
        {
            float age = Mathf.Floor((_it - m * 0.11f) * 30.0f) / 30.0f;
            if (0.0f > age || 1.8f < age) continue;

            float su = RootOffsetX + (Rnd(m + 1) - 0.5f) * 66.0f;
            float sv = 40.5f + 30.0f - Rnd(m + 2) * 40.0f;
            float u = su + Mathf.Sin(age * 3.0f + m) * 2.5f;
            float v = sv - age * (14.0f + Rnd(m + 3) * 10.0f);
            float fr = age / 1.8f;
            int tone = 0.4f > fr ? 0 : (0.75f > fr ? 1 : 2);
            if (0.8f < fr && 0 != (Mathf.FloorToInt(age * 24.0f) & 1)) continue;

            // 카드 몸통 위는 피해서 가장자리에서만 보이게
            if (-1.0f < u && CardWidth + 1 > u && -1.0f < v && CardRows + 1 > v) continue;

            int shape = Rnd(m + 4) < 0.25f ? ShapePlus : (Rnd(m + 5) < 0.5f ? ShapeCircle : ShapeDot);
            GlowDot(u, v, 4.0f, Family(m, 0), 0.3f * (1.0f - fr));
            Pix(shape, u, v, Family(m, tone));
        }

        for (int j = 0; j < SpotCount; j++)
        {
            const float period = 4.2f;
            float off = (j * 0.77f) % period;
            float d = ((_it - off) % period + period) % period;
            if (0.0f > _it - off || 0.25f < d) continue;

            int f = Mathf.FloorToInt(d * 24.0f);
            int shape = 0 == f ? ShapeDot : (1 == f || 3 == f ? ShapePlus : (2 == f ? ShapePlus5 : ShapeDot));
            float su = RootOffsetX + SpotX[j];
            float sv = 40.5f + SpotY[j];
            Color32 fam0 = Family(j, 0);
            GlowDot(su, sv, 7.0f, fam0, 0.5f * (1.0f - d / 0.25f));
            Pix(shape, su, sv, fam0);
        }
    }
}
