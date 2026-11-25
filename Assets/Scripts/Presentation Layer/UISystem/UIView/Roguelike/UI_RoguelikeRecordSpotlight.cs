using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기록 창(나이테 기록판) 위쪽의 스포트라이트: 선택한 기록 한 건을 크게 보여 준다. 계획서 RecordUI_Implementation_Plan.md 11장(v2 배치).
///   높이: 랭킹 탭 109(머리 줄 · 웨이브 길 · 카드 띠), 최근 탭 65(머리 줄 · 웨이브 길). 가로 안쪽 여백 12(내용 x 12~328), 위 여백 12.
///   머리 줄(y 12~44, 세 탭 같은 자리): 메달 28 (12, 14) · 기준 아이콘 16 (46, 20) + 큰 숫자 (65, 20) · 결과 도장(큰 숫자 끝 ~ 칩 묶음 사이 가운데) · 칩 2x2(열 x 212 / 276, 줄 y 12 / 28).
///     메달: 랭킹 탭은 순위(1 금 · 2 은 · 3 동 · 4~5 나무), 최근 탭은 이번 판이 든 랭킹 순위(없으면 빈 그루터기).
///     칩: 클리어 · 최근 = 나무 · 총 시간 · 카드 · MAX 수, 언리밋 = 나무 · 총 시간 · 클리어 시간 · 카드. 값은 작은 숫자 그림 글꼴이고 0부터 올라간다.
///   웨이브 길(y 47~57): 칸 8x7(10단위 8x9) 간격 1(x 12~281) + 끝 깃발 10 (284, 47) + 언리밋이면 ∞ + 단계(끝선 328에 오른쪽 정렬).
///   아래칸(랭킹 탭): 카드 띠 (12, 61) 316x36 - 화면 UIView_RoguelikeRecords가 채우고 스크롤한다.
///
/// [연출 - 모두 unscaled, 위치는 정수 픽셀, 크기 변화 없음(팻 픽셀 방지)]
///   PlayIntro: 메달이 12px 위에서 튕기며 꽂힘 → 큰 숫자 굴림 → 길 점등(막 켜진 칸은 흰 칸) → 끝 깃발 → 칩(값이 올라감) → 도장(6px 위에서 떨어져 꽂힘).
///   PlaySelection: 위아래 나무판(덧문)이 0.07초에 닫혔다가 내용을 바꾸고 0.07초에 열린다. 숫자는 이전 값에서 새 값으로, 길은 차이만큼만 켜지거나 꺼진다.
///     덧문은 고정 크기 타일을 스텐실 마스크(Mask) 안에 두고 마스크 높이만 정수로 바꾼다(무늬가 밀리지 않는다).
/// </summary>
public class UI_RoguelikeRecordSpotlight : MonoBehaviour
{
    public const int TierCount = 5;
    public const int ChipCount = 4;
    public const float RankingHeight = 109f;
    public const float RecentHeight = 65f;
    private const int TierEmpty = 4;
    private const int TierWood = 3;
    private const int FrameBorder = 4;

    // 타임라인(초, 등장 시작 기준). 시뮬레이션 RecordUI_Redesign_Sim.html과 같은 값.
    private const float SelectionSpeed = 1.6f;
    private const float FrameFadeDelay = 0.02f;
    private const float FrameFadeSeconds = 0.14f;
    private const float ShutterCloseSeconds = 0.07f;
    private const float ShutterOpenSeconds = 0.07f;
    private const float MedalDelay = 0.08f;
    private const float MedalSeconds = 0.30f;
    private const float MedalDropHeight = 12f;
    private const float KeyDelay = 0.12f;
    private const float KeyFadeSeconds = 0.1f;
    private const float NumberDelay = 0.16f;
    private const float NumberSeconds = 0.5f;
    private const float TickInterval = 0.045f;
    private const float ChipDelay = 0.30f;
    private const float ChipStagger = 0.05f;
    private const float ChipSeconds = 0.18f;
    private const float ChipSlide = 6f;
    private const float ChipCountDelay = 0.40f;
    private const float ChipCountSeconds = 0.6f;
    private const float TrailDelay = 0.22f;
    private const float TrailStep = 0.016f;
    private const float TrailSelectionDelay = 0.05f;
    private const float TrailSelectionStep = 0.012f;
    private const float FlagSeconds = 0.3f;
    private const float UnlimitDelay = 0.1f;
    private const float UnlimitSeconds = 0.2f;
    private const float StampDelay = 0.95f;
    private const float StampSeconds = 0.10f;
    private const float StampDrop = 6f;
    private const float BlinkSeconds = 0.4f;
    private const float GoldShinePeriod = 2.6f;
    private const float SilverShinePeriod = 3.6f;
    private const float SnapSeconds = 99f;

    // 배치(스포트라이트 왼쪽 위 기준, 아래쪽 +)
    private static readonly Vector2 MedalPosition = new Vector2(12f, 14f);
    private const float KeyNumberX = 65f;
    private const float HeaderTop = 12f;
    private const float HeaderHeight = 32f;
    private const float ChipBlockLeft = 212f;
    private const float TrailX = 12f;
    private const float TrailY = 50f;
    private const float TrailPipStep = 9f;
    private const float FlagX = 284f;
    private const float FlagY = 47f;
    private const float ContentRight = 329f;
    private const float UnlimitIconY = 50f;
    private const float UnlimitGap = 1f;
    private const float EmptyCenterY = 28f;

    private static readonly Color DustColorA = new Color32(0x8F, 0x6A, 0x50, 255);
    private static readonly Color DustColorB = new Color32(0x5A, 0x3C, 0x2C, 255);
    private static readonly Color DustColorC = new Color32(0xCA, 0x8F, 0x73, 255);
    private static readonly Color SparkHi = new Color32(0xFF, 0xE0, 0x8A, 255);
    private static readonly Color SparkBase = new Color32(0xFF, 0xD0, 0x60, 255);
    private static readonly Color SparkLate = new Color32(0xC8, 0x96, 0x2E, 255);
    private static readonly Color RedSpark = new Color32(0xE0, 0x4B, 0x3E, 255);
    private static readonly Color WhiteSpark = new Color32(0xFF, 0xF4, 0xDC, 255);
    // 덧문이 맞닿는 선: 순위 색(금 · 은 · 동 · 나무 · 빈칸)
    private static readonly Color[] SeamColors =
    {
        new Color32(0xFF, 0xD0, 0x60, 255), new Color32(0xB2, 0xBD, 0xCD, 255), new Color32(0xB7, 0x5A, 0x14, 255),
        new Color32(0x8F, 0x56, 0x3B, 255), new Color32(0x46, 0x2D, 0x1F, 255),
    };

    private enum EPlayMode
    {
        None = 0,
        Intro = 1,
        Selection = 2,
    }

    private enum EKeyIcon
    {
        Clear = 0,
        Over = 1,
        Retire = 2,
        Unlimit = 3,
    }

    private enum EChip
    {
        Trees = 0,
        PlayTime = 1,
        Cards = 2,
        Max = 3,
        ClearTime = 4,
    }

    /// <summary>한 기록을 그리는 데 필요한 값(값 형식).</summary>
    private struct Content
    {
        public bool bValid;
        public int tab;
        public int tier;
        public int rank;
        public bool bKeyTime;
        public int keyValue;
        public UI_RoguelikeBigNumber.EPalette palette;
        public EKeyIcon keyIcon;
        public int lit;
        public UI_RoguelikeWaveTrail.EEnd end;
        public int unlimit;
        public int trees;
        public int playSeconds;
        public int clearSeconds;
        public int cardCount;
        public int maxCount;
    }

    /// <summary>선택이 바뀌어 덧문이 닫힌 순간(새 내용으로 바뀐 직후). 화면은 이때 카드 띠를 새 기록으로 채운다.</summary>
    public event Action ContentSwappedEvent;
    /// <summary>결과 도장이 꽂힌 순간. 화면은 판을 1px 흔든다.</summary>
    public event Action StampLandedEvent;

    //외부 의존성 (프리팹에서 연결)
    [Header("틀")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image frameImage;
    [SerializeField, Tooltip("순위 색 테두리: 금 · 은 · 동 · 나무 · 빈 기록.")] private Sprite[] frameSprites = new Sprite[TierCount];
    [SerializeField, Tooltip("오른쪽 위 나이테 무늬(빈 기록에서는 숨긴다).")] private GameObject ringPattern;

    [Header("메달 · 기준값 · 도장")]
    [SerializeField] private RectTransform medalRect;
    [SerializeField] private Image medalImage;
    [SerializeField, Tooltip("순위 1~5를 새긴 28px 메달.")] private Sprite[] medalSprites = new Sprite[5];
    [SerializeField] private Sprite medalEmptySprite;
    [SerializeField, Tooltip("최근 기록이 랭킹에 못 든 판의 빈 그루터기.")] private Sprite medalStumpSprite;
    [SerializeField] private CanvasGroup keyGroup;
    [SerializeField] private Image keyIconImage;
    [SerializeField, Tooltip("클리어(금 깃발) · 게임오버(빨강 깃발) · 포기(흰 깃발) · 언리밋(∞) 16px.")] private Sprite[] keyIconSprites = new Sprite[4];
    [SerializeField] private UI_RoguelikeBigNumber bigNumber;
    [SerializeField] private RectTransform stampRect;
    [SerializeField] private Image stampImage;
    [SerializeField, Tooltip("CLEAR · GAME OVER · RETIRE 도장.")] private Sprite[] stampSprites = new Sprite[3];

    [Header("칩")]
    [SerializeField] private RectTransform[] chipRects = new RectTransform[ChipCount];
    [SerializeField] private CanvasGroup[] chipGroups = new CanvasGroup[ChipCount];
    [SerializeField] private Image[] chipIcons = new Image[ChipCount];
    [SerializeField] private UI_RoguelikeSmallNumber[] chipNumbers = new UI_RoguelikeSmallNumber[ChipCount];
    [SerializeField] private Sprite treeSprite;
    [SerializeField] private Sprite cardSprite;
    [SerializeField] private Sprite clockSprite;
    [SerializeField] private Sprite clearTimeSprite;
    [SerializeField, Tooltip("M(MAX) 배지(7x9).")] private Sprite maxSprite;

    [Header("웨이브 길")]
    [SerializeField] private UI_RoguelikeWaveTrail trail;
    [SerializeField] private RectTransform flagRect;
    [SerializeField] private CanvasGroup flagGroup;
    [SerializeField] private Image flagImage;
    [SerializeField, Tooltip("끝 깃발 10px: 클리어 · 게임오버 · 포기(도달 전에는 흰 깃발을 흐리게).")] private Sprite[] flagSprites = new Sprite[3];
    [SerializeField] private CanvasGroup unlimitGroup;
    [SerializeField] private RectTransform unlimitIcon;
    [SerializeField, Tooltip("언리밋 단계. 끝선 328에 오른쪽 정렬.")] private UI_RoguelikeSmallNumber unlimitNumber;

    [Header("덧문 · 빈 기록")]
    [SerializeField] private RectTransform shutterTop;
    [SerializeField] private RectTransform shutterBottom;
    [SerializeField] private Image seamTop;
    [SerializeField] private Image seamBottom;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private UI_RoguelikeRecordFx fx;

    //내부 의존성
    private readonly Vector2[] chipBases = new Vector2[ChipCount];
    private readonly EChip[] chipKinds = new EChip[ChipCount];
    private readonly int[] chipTargets = new int[ChipCount];
    private readonly int[] chipShown = new int[ChipCount];

    //상태 변수
    private Content current;
    private Content previous;
    private Content pending;
    private EPlayMode mode;
    private float startTime;
    private bool bSwapped;
    private bool bLanded;
    private bool bFlagFired;
    private bool bStampLanded;
    private int lastTickValue;
    private float lastTickTime;
    private int lastEmberPip;
    private int lastShineCycle;
    private Vector2 stampBase;
    private bool bBasesCaptured;
    private int innerHeight = (int)RankingHeight - FrameBorder * 2;

    /// <summary>지금 그리는 내용의 등장 시각부터 지난 시간(선택 변경이면 덧문이 닫힌 뒤부터, 1.6배속). 카드 띠 등장에 쓴다.</summary>
    public float LocalTime { get; private set; }
    /// <summary>선택 변경 연출 중인지(카드 띠 등장 지연이 짧아진다).</summary>
    public bool IsSelectionPlay => EPlayMode.Selection == mode;
    /// <summary>선택 변경 연출에서 아직 덧문이 닫히기 전인지(이전 내용이 보이는 중).</summary>
    public bool IsBeforeSwap => EPlayMode.Selection == mode && false == bSwapped;

    /// <summary>
    /// 탭에 들어오거나 창을 열 때: 처음부터 등장 연출. _bValid가 거짓이면 빈 기록 모양(_emptyMessage)을 보인다.
    /// _rank: 랭킹 탭은 순위(0부터), 최근 탭은 이번 판이 든 랭킹 순위(-1이면 못 듦).
    /// </summary>
    public void PlayIntro(in RoguelikeRecordViewData _record, bool _bValid, int _rank, int _tab, string _emptyMessage)
    {
        CaptureBases();
        previous = default;
        current = BuildContent(in _record, _bValid, _rank, _tab);
        ApplyHeight(_tab);
        ApplyContent(_emptyMessage);
        mode = EPlayMode.Intro;
        startTime = Time.unscaledTime;
        ResetOnceFlags();
        RenderShutter(0f);
        if (null != fx) fx.Clear();
        RenderFrame();
    }

    /// <summary>같은 탭에서 다른 순위를 골랐을 때: 덧문을 닫고 내용을 바꾼 뒤 연다.</summary>
    public void PlaySelection(in RoguelikeRecordViewData _record, int _rank, int _tab)
    {
        CaptureBases();
        pending = BuildContent(in _record, true, _rank, _tab);
        previous = current;
        mode = EPlayMode.Selection;
        startTime = Time.unscaledTime;
        bSwapped = false;
        Sound.PlayUI(SoundID.OptionClick);
    }

    /// <summary>등장 시작을 _delay초 늦춘다(창을 열 때 판이 올라오는 동안 기다린다).</summary>
    public void ShiftStart(float _delay)
    {
        startTime += _delay;
    }

    /// <summary>연출 없이 끝 상태로 그린다(언어 변경 · Localization QA 툴). 소리 · 입자 · 도장 알림은 내지 않는다.</summary>
    public void SnapToEnd()
    {
        if (EPlayMode.None == mode) return;

        if (EPlayMode.Selection == mode && false == bSwapped)
        {
            bSwapped = true;
            current = pending;
            ApplyContent(null);
        }

        mode = EPlayMode.Intro;
        startTime = Time.unscaledTime - SnapSeconds;
        bLanded = true;
        bFlagFired = true;
        bStampLanded = true;
        lastTickValue = current.keyValue;
        lastShineCycle = Mathf.FloorToInt(SnapSeconds / (0 == current.tier ? GoldShinePeriod : SilverShinePeriod));
        RenderShutter(0f);
        Tick();
    }

    /// <summary>연출을 멈추고 끝 상태로 둔다(창을 닫을 때 · 탭을 넘길 때).</summary>
    public void Stop()
    {
        mode = EPlayMode.None;
        RenderShutter(0f);
        if (null != fx) fx.Clear();
    }

    private void ResetOnceFlags()
    {
        bLanded = false;
        bFlagFired = false;
        bStampLanded = false;
        lastTickValue = int.MinValue;
        lastTickTime = -1f;
        lastEmberPip = 0;
        lastShineCycle = 0;
        for (int i = 0; ChipCount > i; i++) chipShown[i] = -1;
    }

    private void CaptureBases()
    {
        if (true == bBasesCaptured) return;
        bBasesCaptured = true;

        for (int i = 0; ChipCount > i; i++)
        {
            if (null != chipRects[i]) chipBases[i] = chipRects[i].anchoredPosition;
        }
    }

    /// <summary>탭에 따라 스포트라이트 높이를 바꾼다(랭킹 109 · 최근 65). 틀은 늘어나는 9-슬라이스, 아래 덧문은 아래쪽에 붙어 따라온다.</summary>
    private void ApplyHeight(int _tab)
    {
        RectTransform rect = (RectTransform)transform;
        float height = 0 == _tab ? RecentHeight : RankingHeight;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        innerHeight = (int)height - FrameBorder * 2;
    }

    private static Content BuildContent(in RoguelikeRecordViewData _record, bool _bValid, int _rank, int _tab)
    {
        Content c = new Content { bValid = _bValid, tab = _tab, rank = _rank, tier = TierEmpty };
        if (false == _bValid) return c;

        c.tier = 0 == _tab ? TierWood : Mathf.Clamp(_rank, 0, TierWood);
        c.trees = _record.treesFelled;
        c.playSeconds = Mathf.FloorToInt(_record.playTimeSeconds);
        c.clearSeconds = Mathf.FloorToInt(_record.rankingTimeSeconds);
        c.unlimit = _record.unlimitWave;
        c.lit = true == _record.bCleared ? UI_RoguelikeWaveTrail.PipCount : Mathf.Clamp(_record.reachedWave, 0, UI_RoguelikeWaveTrail.PipCount);
        c.end = true == _record.bCleared ? UI_RoguelikeWaveTrail.EEnd.Clear : (true == _record.bAbandoned ? UI_RoguelikeWaveTrail.EEnd.Retire : UI_RoguelikeWaveTrail.EEnd.Over);

        int count = null != _record.cards ? _record.cards.Count : 0;
        c.cardCount = count;
        for (int i = 0; count > i; i++)
        {
            RoguelikeOwnedCardViewData card = _record.cards[i];
            bool bLeveled = ECardKind.Active == card.kind || ECardKind.Passive == card.kind;
            if (true == bLeveled && 0 < card.maxLevel && card.level >= card.maxLevel) c.maxCount++;
        }

        // 기준값: 언리밋 탭 = 단계, 클리어 탭 = 클리어 시간(못 깼으면 도달 웨이브), 최근 탭 = 도달(언리밋이면 단계)
        if (2 == _tab || (0 == _tab && true == _record.bCleared && 0 < _record.unlimitWave))
        {
            c.keyIcon = EKeyIcon.Unlimit;
            c.keyValue = _record.unlimitWave;
            c.palette = UI_RoguelikeBigNumber.EPalette.Mint;
        }
        else if (true == _record.bCleared)
        {
            c.keyIcon = EKeyIcon.Clear;
            c.bKeyTime = 1 == _tab;
            c.keyValue = 1 == _tab ? c.clearSeconds : UI_RoguelikeWaveTrail.PipCount;
            c.palette = UI_RoguelikeBigNumber.EPalette.Gold;
        }
        else
        {
            bool bRetire = true == _record.bAbandoned;
            c.keyIcon = true == bRetire ? EKeyIcon.Retire : EKeyIcon.Over;
            c.keyValue = _record.reachedWave;
            c.palette = true == bRetire ? UI_RoguelikeBigNumber.EPalette.White : UI_RoguelikeBigNumber.EPalette.Red;
        }
        return c;
    }

    /// <summary>바뀌지 않는 부분(그림 · 켜고 끄기 · 도장 자리 · 칩 종류)을 지금 내용(current)으로 한 번에 맞춘다.</summary>
    private void ApplyContent(string _emptyMessage)
    {
        bool bValid = current.bValid;
        bool bRecent = 0 == current.tab;

        if (null != frameImage && TierCount > current.tier) frameImage.sprite = frameSprites[current.tier];
        if (null != ringPattern) ringPattern.SetActive(bValid);

        if (null != medalImage)
        {
            Sprite medal;
            if (false == bValid) medal = medalEmptySprite;
            else if (0 > current.rank || medalSprites.Length <= current.rank) medal = medalStumpSprite;
            else medal = medalSprites[current.rank];
            medalImage.sprite = medal;
        }

        if (null != keyGroup) keyGroup.gameObject.SetActive(bValid);
        if (null != keyIconImage) keyIconImage.sprite = keyIconSprites[(int)current.keyIcon];

        ApplyChips();
        ApplyStamp();

        if (null != flagRect) flagRect.gameObject.SetActive(bValid);
        bool bUnlimit = bValid && 0 < current.unlimit;
        if (null != unlimitGroup) unlimitGroup.gameObject.SetActive(bUnlimit);
        if (true == bUnlimit && null != unlimitNumber)
        {
            unlimitNumber.SetInteger(current.unlimit, UI_RoguelikeSmallNumber.EPalette.Mint);
            // ∞ 아이콘은 숫자 외곽선 앞 1px 띄움(보이는 간격 2px)
            if (null != unlimitIcon) unlimitIcon.anchoredPosition = new Vector2(ContentRight - unlimitNumber.Width - UnlimitGap - unlimitIcon.sizeDelta.x, -UnlimitIconY);
        }
        if (null != trail) trail.SetGhost(false == bValid);

        if (null != emptyText)
        {
            emptyText.gameObject.SetActive(false == bValid);
            if (false == bValid)
            {
                emptyText.text = null != _emptyMessage ? _emptyMessage : string.Empty;
                // 가운데 정렬 대신 폭을 재서 왼쪽 위치를 내림으로 정한다(홀수 폭의 반 픽셀 방지). 세로는 UI_RoguelikePixelText가 기준선을 맞춘다.
                float width = Mathf.Ceil(emptyText.GetPreferredValues(emptyText.text, 10000f, 10000f).x);
                RectTransform rect = emptyText.rectTransform;
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
                rect.anchoredPosition = new Vector2(Mathf.Floor((((RectTransform)transform).rect.width - width) * 0.5f), rect.anchoredPosition.y);
            }
        }
    }

    /// <summary>칩 4개의 종류 · 아이콘 · 목표값. 클리어 · 최근 = 나무 · 총 시간 · 카드 · MAX, 언리밋 = 나무 · 총 시간 · 클리어 시간 · 카드.</summary>
    private void ApplyChips()
    {
        bool bValid = current.bValid;
        chipKinds[0] = EChip.Trees;
        chipKinds[1] = EChip.PlayTime;
        chipKinds[2] = 2 == current.tab ? EChip.ClearTime : EChip.Cards;
        chipKinds[3] = 2 == current.tab ? EChip.Cards : EChip.Max;

        for (int i = 0; ChipCount > i; i++)
        {
            if (null != chipRects[i] && chipRects[i].gameObject.activeSelf != bValid) chipRects[i].gameObject.SetActive(bValid);
            if (false == bValid) continue;

            EChip kind = chipKinds[i];
            chipTargets[i] = ChipValue(kind);
            Image icon = chipIcons[i];
            if (null == icon) continue;

            Sprite sprite = ChipSprite(kind);
            icon.sprite = sprite;
            if (null == sprite) continue;

            // 아이콘은 원본 크기로 칩 높이(16) 세로 가운데에 내림으로
            Vector2 size = sprite.rect.size;
            icon.rectTransform.sizeDelta = size;
            icon.rectTransform.anchoredPosition = new Vector2(0f, -Mathf.Floor((16f - size.y) * 0.5f));
        }
    }

    private int ChipValue(EChip _kind)
    {
        switch (_kind)
        {
            case EChip.Trees: return current.trees;
            case EChip.PlayTime: return current.playSeconds;
            case EChip.ClearTime: return current.clearSeconds;
            case EChip.Cards: return current.cardCount;
            default: return current.maxCount;
        }
    }

    private Sprite ChipSprite(EChip _kind)
    {
        switch (_kind)
        {
            case EChip.Trees: return treeSprite;
            case EChip.PlayTime: return clockSprite;
            case EChip.ClearTime: return clearTimeSprite;
            case EChip.Cards: return cardSprite;
            default: return maxSprite;
        }
    }

    private void SetChipValue(int _index, int _value)
    {
        UI_RoguelikeSmallNumber number = chipNumbers[_index];
        if (null == number) return;

        switch (chipKinds[_index])
        {
            case EChip.Trees: number.SetThousands(_value, UI_RoguelikeSmallNumber.EPalette.Cream); break;
            case EChip.PlayTime:
            case EChip.ClearTime: number.SetTime(_value, UI_RoguelikeSmallNumber.EPalette.Cream); break;
            case EChip.Cards: number.SetInteger(_value, UI_RoguelikeSmallNumber.EPalette.Cream); break;
            default: number.SetPrefixed(UI_RoguelikeSmallNumber.CodeTimes, _value, 0 < current.maxCount ? UI_RoguelikeSmallNumber.EPalette.Gold : UI_RoguelikeSmallNumber.EPalette.Muted); break;
        }
    }

    /// <summary>결과 도장: 큰 숫자(최종값) 끝과 칩 묶음(212) 사이 가운데, 머리 줄(12~44) 세로 가운데. 홀수 여백은 내림.</summary>
    private void ApplyStamp()
    {
        if (null == stampRect) return;

        stampRect.gameObject.SetActive(false);
        if (false == current.bValid || null == stampImage) return;

        int index = UI_RoguelikeWaveTrail.EEnd.Clear == current.end ? 0 : (UI_RoguelikeWaveTrail.EEnd.Over == current.end ? 1 : 2);
        Sprite stamp = stampSprites[index];
        stampImage.sprite = stamp;
        if (null == stamp) return;

        Vector2 size = stamp.rect.size;
        float keyEnd = KeyNumberX + (null != bigNumber ? bigNumber.Measure(current.keyValue, current.bKeyTime) : 0);
        stampRect.sizeDelta = size;
        stampBase = new Vector2(keyEnd + Mathf.Floor((ChipBlockLeft - keyEnd - size.x) * 0.5f), -(HeaderTop + Mathf.Floor((HeaderHeight - size.y) * 0.5f)));
        stampRect.anchoredPosition = stampBase;
    }

    /// <summary>매 프레임: 연출 시간을 진행하고 움직이는 부분만 그린다.</summary>
    private void Tick()
    {
        float now = Time.unscaledTime;
        float elapsed = now - startTime;

        if (EPlayMode.Selection == mode)
        {
            if (ShutterCloseSeconds > elapsed)
            {
                RenderShutter(elapsed / ShutterCloseSeconds);
                return;
            }

            if (false == bSwapped)
            {
                bSwapped = true;
                current = pending;
                ApplyContent(null);
                ResetOnceFlags();
                bLanded = true;
                ContentSwappedEvent?.Invoke();
            }

            float open = (elapsed - ShutterCloseSeconds) / ShutterOpenSeconds;
            RenderShutter(1f - Mathf.Clamp01(open));
            LocalTime = (elapsed - ShutterCloseSeconds) * SelectionSpeed;
        }
        else LocalTime = elapsed;

        RenderFrame();
    }

    private void RenderFrame()
    {
        float lt = LocalTime;
        bool bSelection = EPlayMode.Selection == mode;
        float now = Time.unscaledTime;

        if (null != group) group.alpha = true == bSelection ? 1f : Mathf.Clamp01((lt - FrameFadeDelay) / FrameFadeSeconds);

        RenderMedal(lt, bSelection, now);
        if (false == current.bValid)
        {
            if (null != trail) trail.Render(0, UI_RoguelikeWaveTrail.EEnd.None, false, -1, false);
            return;
        }

        RenderNumber(lt, bSelection, now);
        RenderChips(lt);
        float reach = RenderTrail(lt, bSelection, now);
        RenderFlag(lt, reach);
        if (null != unlimitGroup && 0 < current.unlimit) unlimitGroup.alpha = Mathf.Clamp01((lt - reach - UnlimitDelay) / UnlimitSeconds);
        RenderStamp(lt);
    }

    private void RenderMedal(float _lt, bool _bSelection, float _now)
    {
        if (null == medalRect) return;

        float p = true == _bSelection ? 1f : Mathf.Clamp01((_lt - MedalDelay) / MedalSeconds);
        bool bVisible = 0f < p;
        if (medalRect.gameObject.activeSelf != bVisible) medalRect.gameObject.SetActive(bVisible);
        if (false == bVisible) return;

        float drop = Mathf.Round(-MedalDropHeight * (1f - OutBounce(p)));
        medalRect.anchoredPosition = new Vector2(MedalPosition.x, -(MedalPosition.y + drop));

        if (1f <= p && false == bLanded)
        {
            bLanded = true;
            Sound.PlayUI(SoundID.AbilityHUDDownTick);
            EmitDust(new Vector2(MedalPosition.x + 14f, MedalPosition.y + 28f), 6, -Mathf.PI * 0.5f, 2.6f, 30f, 90f, 0.35f);
        }

        // 1 · 2위 메달: 일정 간격마다 테두리 왼쪽 위에서 반짝임 하나.
        if (false == current.bValid || 0 == current.tab || 1 < current.tier || 1f > _lt || null == fx) return;

        float period = 0 == current.tier ? GoldShinePeriod : SilverShinePeriod;
        int cycle = Mathf.FloorToInt((_now - startTime) / period);
        if (cycle == lastShineCycle) return;

        lastShineCycle = cycle;
        UI_RoguelikeRecordFx.EmitSettings shine = new UI_RoguelikeRecordFx.EmitSettings
        {
            angle = 0f, spread = 0f, speed = 0f, gravity = 0f, life = 0.2f,
            shapeA = UI_RoguelikeRecordFx.EShape.Plus, shapeB = UI_RoguelikeRecordFx.EShape.Plus, shapeC = UI_RoguelikeRecordFx.EShape.Plus,
            colorA = Color.white, colorB = Color.white, lateColor = SparkHi,
        };
        fx.Emit(new Vector2(MedalPosition.x + 5f, MedalPosition.y + 5f), 1, in shine);
    }

    private void RenderNumber(float _lt, bool _bSelection, float _now)
    {
        if (null != keyGroup) keyGroup.alpha = Mathf.Clamp01((_lt - KeyDelay) / KeyFadeSeconds);
        if (null == bigNumber) return;

        bool bSameKind = true == _bSelection && true == previous.bValid && previous.bKeyTime == current.bKeyTime && previous.keyIcon == current.keyIcon;
        float from = true == bSameKind ? previous.keyValue : 0f;
        float p = OutCubic(Mathf.Clamp01((_lt - NumberDelay) / NumberSeconds));
        float value = from + (current.keyValue - from) * p;
        int whole = Mathf.FloorToInt(value);
        float roll = 1f > p ? value - whole : 0f;
        bigNumber.Show(whole, current.bKeyTime, current.palette, roll);

        if (0f < p && 1f > p && whole != lastTickValue)
        {
            lastTickValue = whole;
            if (TickInterval <= _now - lastTickTime)
            {
                lastTickTime = _now;
                Sound.PlayUI(SoundID.OptionSFXBarTick);
            }
        }
    }

    /// <summary>칩: 0.05초 간격으로 6px 오른쪽에서 들어오고, 값은 0부터 목표값까지 올라간다(값이 바뀔 때만 다시 그린다).</summary>
    private void RenderChips(float _lt)
    {
        for (int i = 0; ChipCount > i; i++)
        {
            if (null == chipRects[i] || false == chipRects[i].gameObject.activeSelf) continue;

            float p = OutCubic(Mathf.Clamp01((_lt - ChipDelay - i * ChipStagger) / ChipSeconds));
            if (null != chipGroups[i]) chipGroups[i].alpha = p;
            chipRects[i].anchoredPosition = new Vector2(chipBases[i].x + Mathf.Round((1f - p) * ChipSlide), chipBases[i].y);

            float count = OutCubic(Mathf.Clamp01((_lt - ChipCountDelay - i * ChipStagger) / ChipCountSeconds));
            int value = Mathf.FloorToInt(chipTargets[i] * count);
            if (value == chipShown[i]) continue;

            chipShown[i] = value;
            SetChipValue(i, value);
        }
    }

    /// <summary>길을 그리고 끝 깃발이 도착하는 시각(로컬 시간)을 돌려준다.</summary>
    private float RenderTrail(float _lt, bool _bSelection, float _now)
    {
        int lit = current.lit;
        int shown;
        int head = -1;
        float reach;

        if (true == _bSelection)
        {
            int from = true == previous.bValid ? previous.lit : 0;
            int steps = Mathf.FloorToInt(Mathf.Max(0f, _lt - TrailSelectionDelay) / TrailSelectionStep);
            shown = from < lit ? Mathf.Min(lit, from + steps) : Mathf.Max(lit, from - steps);
            reach = TrailSelectionDelay + Mathf.Abs(lit - from) * TrailSelectionStep;
        }
        else
        {
            shown = Mathf.Clamp(Mathf.FloorToInt((_lt - TrailDelay) / TrailStep), 0, lit);
            if (shown < lit && 0 < shown) head = shown - 1;
            reach = TrailDelay + lit * TrailStep;

            // 3칸마다 막 켜진 칸에서 불티 하나
            if (0 <= head && 0 == shown % 3 && shown != lastEmberPip && null != fx)
            {
                lastEmberPip = shown;
                UI_RoguelikeRecordFx.EmitSettings ember = new UI_RoguelikeRecordFx.EmitSettings
                {
                    angle = -Mathf.PI * 0.5f, spread = Mathf.PI, speed = 20f, gravity = 40f, life = 0.3f,
                    shapeA = UI_RoguelikeRecordFx.EShape.Dot, shapeB = UI_RoguelikeRecordFx.EShape.Dot, shapeC = UI_RoguelikeRecordFx.EShape.Dot,
                    colorA = Color.white, colorB = SparkHi, lateColor = SparkBase,
                };
                fx.Emit(new Vector2(TrailX + head * TrailPipStep + 4f, TrailY + 3f), 1, in ember);
            }
        }

        bool bBlinkOff = 1 == Mathf.FloorToInt(_now / BlinkSeconds) % 2;
        if (null != trail) trail.Render(shown, shown == lit ? current.end : UI_RoguelikeWaveTrail.EEnd.Clear, false, head, bBlinkOff);
        return reach;
    }

    private void RenderFlag(float _lt, float _reach)
    {
        if (null == flagRect) return;

        float p = Mathf.Clamp01((_lt - _reach) / FlagSeconds);
        bool bArrived = 0f < p;
        UI_RoguelikeWaveTrail.EEnd end = current.end;
        int index = UI_RoguelikeWaveTrail.EEnd.Clear == end ? 0 : (UI_RoguelikeWaveTrail.EEnd.Over == end ? 1 : 2);
        if (null != flagImage)
        {
            Sprite sprite = true == bArrived ? flagSprites[index] : flagSprites[2];
            if (flagImage.sprite != sprite) flagImage.sprite = sprite;
        }
        if (null != flagGroup) flagGroup.alpha = true == bArrived ? 1f : 0.3f;

        float dx = 0f;
        float dy = 0f;
        if (true == bArrived && 1f > p)
        {
            if (UI_RoguelikeWaveTrail.EEnd.Clear == end) dy = -Mathf.Round(Mathf.Sin(p * Mathf.PI) * 4f);
            else if (UI_RoguelikeWaveTrail.EEnd.Over == end) dx = Mathf.Round(Mathf.Sin(p * Mathf.PI * 6f) * 1.5f);
            else dy = Mathf.Round(Mathf.Sin(p * Mathf.PI) * 2f);
        }
        flagRect.anchoredPosition = new Vector2(FlagX + dx, -(FlagY + dy));

        if (false == bArrived || true == bFlagFired) return;

        bFlagFired = true;
        if (UI_RoguelikeWaveTrail.EEnd.Clear == end)
        {
            Sound.PlayUI(SoundID.LootUIShiny);
            if (null == fx) return;
            UI_RoguelikeRecordFx.EmitSettings spark = new UI_RoguelikeRecordFx.EmitSettings
            {
                angle = 0f, spread = Mathf.PI * 2f, speed = 55f, gravity = 30f, life = 0.6f,
                shapeA = UI_RoguelikeRecordFx.EShape.Plus, shapeB = UI_RoguelikeRecordFx.EShape.Cross, shapeC = UI_RoguelikeRecordFx.EShape.Diamond,
                colorA = SparkHi, colorB = Color.white, lateColor = SparkLate,
            };
            fx.Emit(new Vector2(FlagX + 5f, FlagY + 4f), 10, in spark);
            return;
        }

        Sound.PlayUI(SoundID.ResultUIHover);
        if (null == fx) return;
        Color color = UI_RoguelikeWaveTrail.EEnd.Over == end ? RedSpark : WhiteSpark;
        UI_RoguelikeRecordFx.EmitSettings bits = new UI_RoguelikeRecordFx.EmitSettings
        {
            angle = -Mathf.PI * 0.5f, spread = Mathf.PI * 1.5f, speed = 25f, gravity = 40f, life = 0.35f,
            shapeA = UI_RoguelikeRecordFx.EShape.Dot, shapeB = UI_RoguelikeRecordFx.EShape.Cross, shapeC = UI_RoguelikeRecordFx.EShape.Dot,
            colorA = color, colorB = color, lateColor = color,
        };
        fx.Emit(new Vector2(TrailX + (current.lit - 1) * TrailPipStep + 4f, TrailY + 3f), 4, in bits);
    }

    private void RenderStamp(float _lt)
    {
        if (null == stampRect || null == stampImage || null == stampImage.sprite) return;

        float t = _lt - StampDelay;
        bool bVisible = 0f <= t;
        if (stampRect.gameObject.activeSelf != bVisible) stampRect.gameObject.SetActive(bVisible);
        if (false == bVisible) return;

        float p = Mathf.Clamp01(t / StampSeconds);
        float eased = 1f - (1f - p) * (1f - p);
        stampRect.anchoredPosition = new Vector2(stampBase.x, stampBase.y + Mathf.Round((1f - eased) * StampDrop));

        if (1f > p || true == bStampLanded) return;

        bStampLanded = true;
        Sound.PlayUI(SoundID.SFXInCardSleeve);
        StampLandedEvent?.Invoke();
        Vector2 size = stampRect.sizeDelta;
        float left = stampBase.x;
        float bottom = -stampBase.y + size.y;
        EmitDust(new Vector2(left, bottom), 4, Mathf.PI * 0.75f, 1f, 35f, 50f, 0.35f);
        EmitDust(new Vector2(left + size.x, bottom), 4, Mathf.PI * 0.25f, 1f, 35f, 50f, 0.35f);
    }

    private void EmitDust(Vector2 _position, int _count, float _angle, float _spread, float _speed, float _gravity, float _life)
    {
        if (null == fx) return;

        UI_RoguelikeRecordFx.EmitSettings dust = new UI_RoguelikeRecordFx.EmitSettings
        {
            angle = _angle, spread = _spread, speed = _speed, gravity = _gravity, life = _life,
            shapeA = UI_RoguelikeRecordFx.EShape.Dot, shapeB = UI_RoguelikeRecordFx.EShape.Dot2, shapeC = UI_RoguelikeRecordFx.EShape.Dot,
            colorA = DustColorA, colorB = DustColorC, lateColor = DustColorB,
        };
        fx.Emit(_position, _count, in dust);
    }

    /// <summary>덧문: _closed 0 = 열림, 1 = 다 닫힘. 마스크 높이만 정수로 바꾼다(위 · 아래 절반, 안쪽 높이 홀수면 위가 1px 더).</summary>
    private void RenderShutter(float _closed)
    {
        float closed = Mathf.Clamp01(_closed);
        int topHalf = Mathf.CeilToInt(Mathf.Ceil(innerHeight * 0.5f) * closed);
        int bottomHalf = Mathf.CeilToInt(Mathf.Floor(innerHeight * 0.5f) * closed);
        bool bVisible = 0 < topHalf;
        SetShutterHalf(shutterTop, topHalf, bVisible);
        SetShutterHalf(shutterBottom, bottomHalf, bVisible);

        Color seam = SeamColors[Mathf.Clamp(true == bSwapped || EPlayMode.Selection != mode ? current.tier : previous.tier, 0, TierCount - 1)];
        if (null != seamTop)
        {
            if (seamTop.gameObject.activeSelf != bVisible) seamTop.gameObject.SetActive(bVisible);
            seamTop.color = seam;
            seamTop.rectTransform.anchoredPosition = new Vector2(FrameBorder, -(FrameBorder + topHalf - 1f));
        }
        if (null != seamBottom)
        {
            if (seamBottom.gameObject.activeSelf != bVisible) seamBottom.gameObject.SetActive(bVisible);
            seamBottom.color = seam;
            seamBottom.rectTransform.anchoredPosition = new Vector2(FrameBorder, -(FrameBorder + innerHeight - bottomHalf));
        }
    }

    private static void SetShutterHalf(RectTransform _mask, int _half, bool _bVisible)
    {
        if (null == _mask) return;

        if (_mask.gameObject.activeSelf != _bVisible) _mask.gameObject.SetActive(_bVisible);
        _mask.sizeDelta = new Vector2(_mask.sizeDelta.x, _half);
    }

    private static float OutCubic(float _t)
    {
        float inverse = 1f - _t;
        return 1f - inverse * inverse * inverse;
    }

    private static float OutBounce(float _t)
    {
        const float n = 7.5625f;
        const float d = 2.75f;
        float t = _t;
        if (1f / d > t) return n * t * t;
        if (2f / d > t) { t -= 1.5f / d; return n * t * t + 0.75f; }
        if (2.5f / d > t) { t -= 2.25f / d; return n * t * t + 0.9375f; }
        t -= 2.625f / d;
        return n * t * t + 0.984375f;
    }

    private void OnDestroy()
    {
        ContentSwappedEvent = null;
        StampLandedEvent = null;
    }

    private void Update()
    {
        if (EPlayMode.None == mode) return;

        Tick();
    }
}
