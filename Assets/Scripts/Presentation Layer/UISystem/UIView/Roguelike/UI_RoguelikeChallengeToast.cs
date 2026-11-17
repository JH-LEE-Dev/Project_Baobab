using System;
using PresentationLayer.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도전과제 달성 팝업(B안 - Docs/Roguelike/_Work/ChallengeToast_B_Plan.md, 승인된 움직임 기준 구현 ChallengeToast_B_Sim.html).
/// 화면 오른쪽 위(여백 8px)에 284x48 한 줄 고정: 금색 머리띠(284x22, 밑줄 2px 포함) + 그 아래 4px 겹쳐 내려오는 어두운 판(278x30, 보이는 높이 26).
/// 머리띠에 깃발 + 머리말(Roguelike_Challenge_ClearedToast), 판에 제목(호출 측이 번역해 넘긴 문자열)을 한 줄로 보인다.
///
/// 시간표(30fps 틱, 정수 픽셀, unscaled): 0~0.20 띠가 가운데에서 펼쳐짐(흰 -> 크림 -> 금), 0.12~0.34 판이 내려옴(OutBack),
///   0.20 깃발 통통 · 머리말 안착 · 조각 14개 터짐 · 띠 양 끝 + 반짝임, 0.30~0.48 제목이 왼쪽부터 드러남, 0.30~0.75 · 2.20~2.65 광택,
///   3.20~3.32 판이 띠 속으로 접힘, 3.30~3.50 띠가 오른쪽 화면 밖으로 빠짐. 앞 팝업이 끝나고 0.15초 뒤 다음 팝업이 나온다(대기열 13).
/// DOTween 시퀀스를 만들지 않고 Update에서 틱 번호가 바뀔 때만 값을 직접 계산해 넣는다. 재생 중 힙 할당이 없다(조각 · 대기열은 고정 배열, 난수는 정수 xorshift).
/// 그리는 그래픽(광택 · 반짝임 · 조각)은 UI_LootGlowParticles 하나(기본 UI 머티리얼, 불투명 색)다. 모든 그래픽은 Raycast Target을 받지 않는다.
/// 호출: UIView_RoguelikeHUD.ShowChallengeToast(in RoguelikeChallengeViewData). 제목 이외의 필드(tip, bCleared)는 쓰지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class UI_RoguelikeChallengeToast : MonoBehaviour
{
    public const int QueueCapacity = 13;
    public const int Width = 284;
    public const int BandHeight = 22;
    public const int PanelVisibleHeight = 26;
    public const int TitleAreaWidth = 270;

    // 문구 키(RoguelikeUI.json)
    private const string HeaderKey = "Roguelike_Challenge_ClearedToast";

    private const float TickRate = 30.0f;
    private const float TickInterval = 1.0f / TickRate;
    private const int MaxCatchUpTicks = 6;
    // 경계 시각(0.1 · 0.3초 등)이 부동소수 오차로 한 틱 어긋나지 않도록 틱 번호에 더하는 작은 값
    private const float TickBias = 0.001f;

    private const float BandOpenSeconds = 0.2f;
    private const float BandWhiteEnd = 0.1f;
    private const float BandCreamEnd = 0.16f;
    private const float PanelStart = 0.12f;
    private const float PanelSeconds = 0.22f;
    private const float TitleStart = 0.3f;
    private const float TitleSeconds = 0.18f;
    private const float AccentTime = 0.2f;
    private const float HeaderPopEnd = 0.26f;
    private const float FlagBounceHeight = 6.0f;
    private const float FlagBounceSeconds = 0.45f;
    private const float FlagBounceFrequency = 14.0f;
    private const float SheenSeconds = 0.45f;
    private const float SheenFirstStart = 0.3f;
    private const float SheenSecondStart = 2.2f;
    private const float SparkleEnd = 0.55f;
    private const float SparkleLargeEnd = 0.4f;
    private const float FoldStart = 3.2f;
    private const float FoldSeconds = 0.12f;
    private const float SlideStart = 3.3f;
    private const float SlideSeconds = 0.2f;
    private const float EndTime = 3.5f;
    private const float GapSeconds = 0.15f;
    private const int SlideDistance = Width + 16;
    private const int FlagLeft = 4;
    private const int FlagTop = 2;
    private const int HeaderShift = 8;

    private const int ShardCapacity = 14;
    private const float ShardGravity = 260.0f;
    private const int ShardSpawnTick = 6;

    private static readonly Color32 ColorWhite = new Color32(0xFF, 0xFF, 0xFF, 255);
    private static readonly Color32 ColorCream = new Color32(0xFF, 0xF6, 0xD8, 255);
    private static readonly Color32 ColorGold = new Color32(0xFF, 0xD4, 0x59, 255);
    private static readonly Color32 ColorAmber = new Color32(0xFF, 0x9D, 0x2E, 255);

    private enum EToastState
    {
        Idle,
        Playing,
        Gap,
    }

    private struct Shard
    {
        public bool bActive;
        public int kind;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public float life;
        public float max;
    }

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Challenge Toast)
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Tooltip("미끄러져 나가는 모든 요소의 부모(루트와 같은 크기).")] private RectTransform body;
    [SerializeField] private RectTransform bandRect;
    [SerializeField] private Image bandImage;
    [SerializeField, Tooltip("판을 위에서 잘라 내려오게 하는 마스크(RectMask2D, 폭 278).")] private RectTransform panelMask;
    [SerializeField, Tooltip("제목을 왼쪽부터 드러내는 마스크(RectMask2D, 폭 270 x 높이 23).")] private RectMask2D titleClip;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private RectTransform flagRect;
    [SerializeField] private Image flagImage;
    [SerializeField] private RectTransform headerRect;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private CanvasGroup headerGroup;
    [SerializeField, Tooltip("광택 · 반짝임 · 조각을 그리는 그래픽(원점 = 왼쪽 위, y 위로 증가).")] private UI_LootGlowParticles fx;

    //내부 의존성
    private LocalizationManager localizationManager;
    private readonly RoguelikeChallengeViewData[] queue = new RoguelikeChallengeViewData[QueueCapacity];
    private readonly Shard[] shards = new Shard[ShardCapacity];

    //상태 변수
    private EToastState state = EToastState.Idle;
    private int queueHead;
    private int queueCount;
    private float elapsed;
    private float gapElapsed;
    private int lastTick;
    private int simTick;
    private bool bSpawned;
    private int playOrder;
    private uint rngState = 1u;
    private int titleWidth;
    private int headerWidth;

    // 마지막으로 반영한 값(바뀔 때만 RectTransform · 색을 쓴다)
    private int appliedSlide;
    private int appliedBandWidth;
    private int appliedColorStep;
    private int appliedPanelHeight;
    private int appliedTitleState;
    private int appliedFlagOffset;
    private int appliedHeaderPop;
    private bool bAppliedHeaderShown;

    // 캐싱 델리게이트
    private Action languageChangedHandler;

    /// <summary>대기 중이거나 재생 중인 팝업이 있는가.</summary>
    public bool IsBusy => EToastState.Idle != state || 0 < queueCount;

    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>머리말 문구를 읽고 언어 변경을 구독한다. 팝업은 숨김 상태로 시작한다.</summary>
    public void Initialize(LocalizationManager _localizationManager)
    {
        if (null != localizationManager) localizationManager.OnLanguageChanged -= languageChangedHandler;

        localizationManager = _localizationManager;
        if (null == languageChangedHandler) languageChangedHandler = OnLanguageChanged;
        if (null != localizationManager)
        {
            localizationManager.OnLanguageChanged -= languageChangedHandler;
            localizationManager.OnLanguageChanged += languageChangedHandler;
        }

        ApplyHeaderText();
        SetHidden();
    }

    /// <summary>
    /// 달성 팝업을 띄운다. 재생 중이면 대기열(13)에 넣어 앞 팝업이 끝난 뒤 하나씩 보여 준다. 제목은 이미 번역된 문자열(_data.title)을 그대로 쓴다.
    /// </summary>
    public void Show(in RoguelikeChallengeViewData _data)
    {
        if (QueueCapacity <= queueCount) return;

        queue[(queueHead + queueCount) % QueueCapacity] = _data;
        queueCount++;

        if (EToastState.Idle == state) StartNext();
    }

    /// <summary>대기열과 재생을 즉시 지운다(런 종료 · 씬 전환).</summary>
    public void Clear()
    {
        for (int i = 0; QueueCapacity > i; i++) queue[i] = default;
        queueHead = 0;
        queueCount = 0;

        for (int i = 0; ShardCapacity > i; i++) shards[i].bActive = false;
        state = EToastState.Idle;
        elapsed = 0.0f;
        gapElapsed = 0.0f;
        SetHidden();
    }

    // 일반 비즈니스 로직 및 내부 메서드

    private void SetHidden()
    {
        if (null != canvasGroup)
        {
            canvasGroup.alpha = 0.0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (null != fx) fx.ClearQuads();
    }

    private static int RoundHalfUp(float _value)
    {
        return Mathf.FloorToInt(_value + 0.5f);
    }

    private static int EvenCeil(float _value)
    {
        int width = Mathf.CeilToInt(_value);
        return 0 != (width & 1) ? width + 1 : width;
    }

    private static float EaseOutCubic(float _x)
    {
        float inverse = 1.0f - Mathf.Clamp01(_x);
        return 1.0f - inverse * inverse * inverse;
    }

    private static float EaseOutBack(float _x)
    {
        float x = Mathf.Clamp01(_x) - 1.0f;
        const float c = 1.70158f;
        return 1.0f + (c + 1.0f) * x * x * x + c * x * x;
    }

    private float NextFloat()
    {
        rngState ^= rngState << 13;
        rngState ^= rngState >> 17;
        rngState ^= rngState << 5;
        return (rngState % 10000u) / 10000.0f;
    }

    private string LocalizeHeader()
    {
        return null != localizationManager ? localizationManager.GetText(HeaderKey) : string.Empty;
    }

    /// <summary>머리말을 새 언어 문구로 바꾸고 폭(짝수 px)을 다시 잰다.</summary>
    private void ApplyHeaderText()
    {
        if (null == headerText || null == headerRect) return;

        string text = LocalizeHeader();
        headerText.text = text;
        headerWidth = EvenCeil(headerText.GetPreferredValues(text, 10000.0f, 10000.0f).x);
        headerRect.sizeDelta = new Vector2(headerWidth, headerRect.sizeDelta.y);
    }

    /// <summary>제목을 넣고 폭(짝수 px)을 다시 재 가운데에 놓는다.</summary>
    private void ApplyTitleText(string _title)
    {
        if (null == titleText) return;

        string text = null != _title ? _title : string.Empty;
        titleText.text = text;
        titleWidth = EvenCeil(titleText.GetPreferredValues(text, 10000.0f, 10000.0f).x);
        RectTransform rect = titleText.rectTransform;
        rect.sizeDelta = new Vector2(titleWidth, rect.sizeDelta.y);
    }

    private void StartNext()
    {
        if (0 >= queueCount) return;

        RoguelikeChallengeViewData data = queue[queueHead];
        queue[queueHead] = default;
        queueHead = (queueHead + 1) % QueueCapacity;
        queueCount--;

        ApplyTitleText(data.title);
        ApplyHeaderText();

        for (int i = 0; ShardCapacity > i; i++) shards[i].bActive = false;
        rngState = (uint)(77 + playOrder * 31);
        if (0u == rngState) rngState = 1u;
        playOrder++;

        elapsed = 0.0f;
        gapElapsed = 0.0f;
        lastTick = -1;
        simTick = -1;
        bSpawned = false;
        appliedSlide = int.MinValue;
        appliedBandWidth = int.MinValue;
        appliedColorStep = int.MinValue;
        appliedPanelHeight = int.MinValue;
        appliedTitleState = int.MinValue;
        appliedFlagOffset = int.MinValue;
        appliedHeaderPop = int.MinValue;
        bAppliedHeaderShown = true;

        if (null != canvasGroup)
        {
            canvasGroup.alpha = 1.0f;
            canvasGroup.blocksRaycasts = false;
        }

        state = EToastState.Playing;
        AdvanceTo(0);
    }

    private void FinishPlaying()
    {
        for (int i = 0; ShardCapacity > i; i++) shards[i].bActive = false;
        SetHidden();
        state = EToastState.Gap;
        gapElapsed = 0.0f;
    }

    /// <summary>틱 하나씩 조각을 진행한 뒤(최대 6틱) 마지막 틱의 모습으로 한 번 그린다.</summary>
    private void AdvanceTo(int _tick)
    {
        int first = Mathf.Max(simTick + 1, _tick - MaxCatchUpTicks + 1);
        for (int tick = first; _tick >= tick; tick++) StepShards(tick);

        simTick = _tick;
        lastTick = _tick;
        Draw(_tick);
    }

    // 틱 하나: 0.2초(6틱)에 조각 14개를 만들고, 만든 틱에도 한 번 진행한다(시뮬레이션과 같은 순서).
    private void StepShards(int _tick)
    {
        if (false == bSpawned && ShardSpawnTick <= _tick)
        {
            bSpawned = true;
            SpawnShards();
        }

        for (int i = 0; ShardCapacity > i; i++)
        {
            if (false == shards[i].bActive) continue;

            shards[i].life += TickInterval;
            shards[i].vy += ShardGravity * TickInterval;
            shards[i].x += shards[i].vx * TickInterval;
            shards[i].y += shards[i].vy * TickInterval;
            if (shards[i].life >= shards[i].max) shards[i].bActive = false;
        }
    }

    // 조각 14개: 3곳(깃발 쪽 / 오른쪽 끝 / 가운데)에서 위로 튀고 중력으로 떨어진다. 모양 k % 3 = ◆ + ●, 색 크림 · 금 · 호박. 난수 호출 순서는 시뮬레이션과 같다(x, vx, vy, max).
    private void SpawnShards()
    {
        for (int k = 0; ShardCapacity > k; k++)
        {
            int side = k % 3;
            float x = 0 == side ? FlagLeft + NextFloat() * 20.0f : (1 == side ? Width - 24.0f + NextFloat() * 20.0f : Width * 0.5f - 40.0f + NextFloat() * 80.0f);
            float vx = (NextFloat() - 0.5f) * 90.0f + (0 == side ? -30.0f : (1 == side ? 30.0f : 0.0f));
            float vy = -60.0f - NextFloat() * 70.0f;
            float max = 0.55f + NextFloat() * 0.4f;

            shards[k].bActive = true;
            shards[k].kind = side;
            shards[k].x = x;
            shards[k].y = 2.0f;
            shards[k].vx = vx;
            shards[k].vy = vy;
            shards[k].life = 0.0f;
            shards[k].max = max;
        }
    }

    // 왼쪽 위 기준 칸(y 아래로 증가) -> 그래픽 좌표(원점 왼쪽 위, y 위로 증가)
    private void PutRect(int _x, int _y, int _width, int _height, in Color32 _color)
    {
        fx.Rect(_x, -(_y + _height), _x + _width, -_y, _color);
    }

    private void Draw(int _tick)
    {
        if (null == fx) return;

        float t = (_tick + TickBias) * TickInterval;
        bool bOut = FoldStart < t;
        float fold = true == bOut ? EaseOutCubic((t - FoldStart) / FoldSeconds) : 0.0f;
        int slide = SlideStart < t ? RoundHalfUp(EaseOutCubic((t - SlideStart) / SlideSeconds) * SlideDistance) : 0;
        int bandWidth = Mathf.Max(2, RoundHalfUp(Width * EaseOutCubic(t / BandOpenSeconds) * 0.5f) * 2);

        int panelHeight = PanelStart > t ? 0 : RoundHalfUp(PanelVisibleHeight * EaseOutBack((t - PanelStart) / PanelSeconds));
        if (true == bOut) panelHeight = RoundHalfUp(PanelVisibleHeight * (1.0f - fold));

        ApplySlide(slide);
        ApplyBand(bandWidth, t);
        ApplyPanel(panelHeight, bOut, t);

        bool bBandOpen = Width <= bandWidth;
        ApplyAccent(bBandOpen, t);

        fx.BeginQuads();
        if (true == bBandOpen)
        {
            DrawSheen((t - SheenFirstStart) / SheenSeconds);
            DrawSheen((t - SheenSecondStart) / SheenSeconds);
            if (AccentTime < t && SparkleEnd > t) DrawSparkles(SparkleLargeEnd > t ? 3 : 2);
        }

        DrawShards();
        fx.Commit();
    }

    private void ApplySlide(int _slide)
    {
        if (_slide == appliedSlide || null == body) return;

        appliedSlide = _slide;
        body.anchoredPosition = new Vector2(_slide, 0.0f);
    }

    // 띠: 폭(가운데에서 펼쳐짐)과 색(흰 0.1초 -> 크림 0.16초 -> 금)
    private void ApplyBand(int _width, float _t)
    {
        if (null != bandRect && _width != appliedBandWidth)
        {
            appliedBandWidth = _width;
            bandRect.sizeDelta = new Vector2(_width, BandHeight);
        }

        int step = BandWhiteEnd > _t ? 0 : (BandCreamEnd > _t ? 1 : 2);
        if (null != bandImage && step != appliedColorStep)
        {
            appliedColorStep = step;
            bandImage.color = 0 == step ? ColorWhite : (1 == step ? ColorCream : ColorGold);
        }
    }

    // 판: 띠 아래에서 높이만큼만 보이고(3px 이하는 숨김), 제목은 0.3초부터 왼쪽부터 드러난다(퇴장 중에는 전부 보임).
    private void ApplyPanel(int _panelHeight, bool _bOut, float _t)
    {
        int height = 3 < _panelHeight ? _panelHeight : 0;
        if (null != panelMask && height != appliedPanelHeight)
        {
            appliedPanelHeight = height;
            panelMask.sizeDelta = new Vector2(panelMask.sizeDelta.x, height);
        }

        if (null == titleClip || null == titleGroup) return;

        int revealed = 0;
        if (true == _bOut) revealed = titleWidth;
        else if (TitleStart < _t) revealed = Mathf.Max(1, RoundHalfUp(titleWidth * EaseOutCubic((_t - TitleStart) / TitleSeconds)));

        if (revealed == appliedTitleState) return;

        appliedTitleState = revealed;
        bool bShown = 0 < revealed;
        titleGroup.alpha = true == bShown ? 1.0f : 0.0f;

        // 글자는 마스크 가운데에 놓이므로 글자 왼쪽 끝(135 - 폭 / 2)에서 revealed만큼만 남기고 오른쪽을 자른다.
        float textLeft = TitleAreaWidth * 0.5f - titleWidth * 0.5f;
        float rightPadding = Mathf.Max(0.0f, TitleAreaWidth - (textLeft + revealed));
        titleClip.padding = new Vector4(0.0f, 0.0f, rightPadding, 0.0f);
    }

    // 띠가 다 펼쳐진 뒤에만 깃발 · 머리말이 보인다. 깃발은 0.2초에 통통(6px에서 감쇠, 0.45초), 머리말은 0.26초까지 2px 위에 있다.
    private void ApplyAccent(bool _bBandOpen, float _t)
    {
        if (null != flagImage && null != headerGroup && _bBandOpen != bAppliedHeaderShown)
        {
            bAppliedHeaderShown = _bBandOpen;
            flagImage.enabled = _bBandOpen;
            headerGroup.alpha = true == _bBandOpen ? 1.0f : 0.0f;
        }

        if (false == _bBandOpen) return;

        int bounce = AccentTime > _t
            ? -(int)FlagBounceHeight
            : RoundHalfUp(-FlagBounceHeight * Mathf.Abs(Mathf.Cos((_t - AccentTime) * FlagBounceFrequency)) * Mathf.Max(0.0f, 1.0f - (_t - AccentTime) / FlagBounceSeconds));
        if (null != flagRect && bounce != appliedFlagOffset)
        {
            appliedFlagOffset = bounce;
            flagRect.anchoredPosition = new Vector2(FlagLeft, -(FlagTop + bounce));
        }

        int pop = HeaderPopEnd > _t ? -2 : 0;
        if (null != headerRect && pop != appliedHeaderPop)
        {
            appliedHeaderPop = pop;
            headerRect.anchoredPosition = new Vector2(HeaderShift, -pop);
        }
    }

    // 광택: 줄마다 한 칸씩 왼쪽으로 밀린 5px 크림 띠 + 오른쪽 6칸 뒤 흰 1px. 띠 위(밑줄 제외 20줄)에서만 보인다.
    private void DrawSheen(float _p)
    {
        if (0.0f > _p || 1.0f < _p) return;

        int cx = RoundHalfUp(-12.0f + (Width + 24) * _p);
        int rows = BandHeight - 2;
        for (int r = 0; rows > r; r++)
        {
            int x0 = Mathf.Max(0, cx - r);
            int x1 = Mathf.Min(Width, cx - r + 5);
            if (x0 < x1) PutRect(x0, r, x1 - x0, 1, ColorCream);

            int wx = cx - r + 6;
            if (0 <= wx && Width > wx) PutRect(wx, r, 1, 1, ColorWhite);
        }
    }

    // 띠 양 끝 + 반짝임(흰색): 처음 0.4초는 반지름 3, 이후 2.
    private void DrawSparkles(int _size)
    {
        DrawSparkleAt(-1, _size);
        DrawSparkleAt(Width, _size);
    }

    private void DrawSparkleAt(int _x, int _size)
    {
        PutRect(_x - _size, 0, _size * 2 + 1, 1, ColorWhite);
        PutRect(_x, -_size, 1, _size * 2 + 1, ColorWhite);
    }

    private void DrawShards()
    {
        for (int i = 0; ShardCapacity > i; i++)
        {
            if (false == shards[i].bActive) continue;

            float a = shards[i].life / shards[i].max;
            int shape = shards[i].kind;
            if (0.7f < a) shape = 3;
            else if (0.35f < a) shape = 0 == shape ? 1 : 2;

            Color32 color = 0 == shards[i].kind ? ColorCream : (1 == shards[i].kind ? ColorGold : ColorAmber);
            if (0.85f < a) color.a = 128;

            int x = RoundHalfUp(shards[i].x);
            int y = RoundHalfUp(shards[i].y);
            if (0 == shape)
            {
                PutRect(x, y - 2, 1, 1, color);
                PutRect(x - 1, y - 1, 3, 1, color);
                PutRect(x - 2, y, 5, 1, color);
                PutRect(x - 1, y + 1, 3, 1, color);
                PutRect(x, y + 2, 1, 1, color);
            }
            else if (1 == shape)
            {
                PutRect(x, y - 1, 1, 1, color);
                PutRect(x - 1, y, 3, 1, color);
                PutRect(x, y + 1, 1, 1, color);
            }
            else if (2 == shape)
            {
                PutRect(x, y, 2, 2, color);
            }
            else
            {
                PutRect(x, y, 1, 1, color);
            }
        }
    }

    private void OnLanguageChanged()
    {
        ApplyHeaderText();
        if (null != titleText) ApplyTitleText(titleText.text);
        appliedTitleState = int.MinValue;
    }

    // 유니티 라이프사이클 이벤트 함수

    private void Update()
    {
        if (EToastState.Idle == state) return;

        float deltaTime = Time.unscaledDeltaTime;
        if (EToastState.Gap == state)
        {
            gapElapsed += deltaTime;
            if (GapSeconds > gapElapsed) return;

            state = EToastState.Idle;
            StartNext();
            return;
        }

        elapsed += deltaTime;
        int tick = Mathf.FloorToInt(elapsed * TickRate);
        if (tick == lastTick) return;

        if (EndTime * TickRate <= tick)
        {
            FinishPlaying();
            return;
        }

        AdvanceTo(tick);
    }

    private void OnDisable()
    {
        if (EToastState.Idle != state || 0 < queueCount) Clear();
    }

    private void OnDestroy()
    {
        if (null != localizationManager && null != languageChangedHandler) localizationManager.OnLanguageChanged -= languageChangedHandler;
    }
}
