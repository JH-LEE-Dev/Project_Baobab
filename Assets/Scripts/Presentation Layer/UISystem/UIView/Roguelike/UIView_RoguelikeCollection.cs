using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PresentationLayer.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 카드 도감 창. 메인메뉴의 로그라이크 하위 메뉴 "카드 도감"에서 연다(기획서 부록 C 카드 도감 계획 Docs/Roguelike/_Work/CardCollection_Plan.md 7장).
/// 창은 RoguelikeCollectionViewData 목록(분류 순서대로 정렬된 도감 칸)을 받아 그리기만 한다 - 발견 · MAX · NEW 판정과 저장은 Application Layer 몫이다.
///
/// [구성] 640x360 좌표(SimRoot, 왼쪽 위 원점 · 아래쪽 +)에 시뮬레이션(Docs/Roguelike/_Work/CardCollection/template.html)과 같은 수치로 놓는다.
///   제목판(272x24) + 판 Panel_Book 528x300(진행도 · 분류 머리줄 4 · 칸 26 · 상세 패널 218x250 · 뒤로 버튼) + 완성 연출 층(제목판 뒤 / 앞, UI_RoguelikeCollectionPlateFx).
///
/// [시간] 연출 전체가 "열린 뒤 지난 프레임 n(1/60초, unscaled)"의 함수다. 프레임이 바뀔 때마다 정수 픽셀 · 지난 값 캐시로만 갱신한다(스케일 1, 뒤집힘 폭만 예외).
///   E1 열기 · E2 칸 등장 · E3 진행도 · E4 새로 발견(뒤집힘 + 입자 + NEW) · E5 커서 · E6 상세 카드 · E7 완성 유지 · E8 닫기 · E9 최초 완성 축하.
///   닫는 동안에는 내용을 닫는 순간 프레임에 얼리고 딤 · 판 · 제목판의 페이드만 진행한다.
///
/// [조작 - ControllerSupport_UI가이드.md 3-7 · 3-8] 닫기 = 딤 클릭 · 뒤로 버튼 · 패드 B(UICancelEvent) · Esc(SetStandaloneClose(true)로 직접 받는다).
///   패드: 열릴 때 · 패드로 바뀔 때 선택 칸 복구, 칸 사이는 시뮬레이션 행 규칙(Explicit 탐색), 선택이 null이 되면 복구, 마우스 모드로 바뀌면 선택 해제.
///   마우스: 선택 · 커서 없이 열리고, 칸에 올리면 그 칸이 선택된다(0.92초부터). 포커스 표시는 커서 상자(viewCtx.cursorBoxUI)다.
///   E9 도중 0.6초 뒤부터 A · 클릭은 끝 상태로 건너뛴다.
/// </summary>
public class UIView_RoguelikeCollection : UIView_RoguelikeModalBase
{
    /// <summary>분류 머리줄 하나의 참조 묶음(프리팹에서 연결). 아래 금빛 광택용 복사본(sheen*)은 광택 띠 안에서만 보인다.</summary>
    [Serializable]
    private class HeaderRefs
    {
        public RectTransform root;
        public CanvasGroup group;
        public TMP_Text title;
        public TMP_Text count;
        public RectTransform line;
        public RectTransform sheenMask;
        public RectTransform sheenContent;
        public TMP_Text sheenTitle;
        public TMP_Text sheenCount;
        public RectTransform sheenLine;
    }

    /// <summary>E4 입자 하나(칸을 뒤집을 때 터지는 1~2px 조각). 값 형식 배열이라 할당이 없다.</summary>
    private struct BurstParticle
    {
        public float x;
        public float y;
        public float vx;
        public float vy;
        public int age;
        public int life;
        public int size;
        public Color32 color;
    }

    // ---------- 레이아웃(판 안 좌표 = 시뮬레이션 좌표 - (56, 18), 왼쪽 위 원점 · 아래쪽 +) ----------
    private const int PanelWidth = 528;
    private const int PanelHeight = 300;
    private const int SimPlateTop = 12;
    private const int SimPlateCenterX = 320;
    private const int SimPlateTextTop = 18;
    private const int SpecPlateWidth = 272;
    private const int PlatePadding = 24;
    private const int TextBaseline = 11;            // 시뮬레이션 글자 윗변 + 11 = 기준선
    private const float TextWidthSlack = 2f;
    private const float Unbounded = 10000f;

    private const int GridX = 12;
    private const int SectionStartY = 40;
    private const int SlotStep = 40;
    private const int SlotRowStep = 38;
    private const int Columns = 7;
    private const int SectionHeaderTop = 2;          // 머리줄 컨테이너 윗변 = 분류 시작 y - 2
    private const int SectionSlotTop = 14;
    private const int SectionGap = 2;
    private const int HeaderRight = 7 * SlotStep - 4;
    private const int HeaderSheenSpan = HeaderRight + 12;
    private const int HeaderLineY = 8;
    private const int HeaderTextTop = 1;
    private const int MaxSections = 4;

    private const int ProgressTextTop = 22;
    private const int ProgressBarY = 28;
    private const int ProgressBarHeight = 4;
    private const int ProgressRight = PanelWidth - 12;
    private const int ProgressGap = 8;

    private const int DetailWidth = 218;
    private const int DetailHeight = 250;
    private const int DetailMaskTop = 112;
    private const int DetailTextX = 10;
    private const int DetailKindTop = 8;
    private const int DetailNameTop = 23;
    private const int DetailDescTop = 41;
    private const int DetailMaskWidth = DetailWidth - 20;
    private const int DetailMaskHeight = DetailHeight - DetailMaskTop;
    private const int DetailBadgeTop = DetailHeight - 16 - DetailMaskTop;
    private const int DetailBadgeLabelTop = DetailHeight - 18 - DetailMaskTop;
    private const int DetailBadgeGap = 4;
    private const int DetailMaxLevel = 5;
    private const float DetailDescWidth = 182f;

    // ---------- 연출 값(시뮬레이션 K · E9K, 시간은 unscaled) ----------
    private const float FrameSeconds = 1f / 60f;
    private const float MaxCatchUpSeconds = 0.1f;
    private const float DimSeconds = 0.18f;
    private const float PlateInSeconds = 0.2f;
    private const float PanelDelaySeconds = 0.06f;
    private const float PanelInSeconds = 0.2f;
    private const float RiseDistance = 6f;
    private const float CloseSeconds = 0.15f;
    private const float SlotStart = 0.26f;
    private const float SlotGap = 0.02f;
    private const float SlotDrop = 0.14f;
    private const float SlotDy = 4f;
    private const float HeaderFadeSeconds = 0.1f;
    private const float ProgressStart = 0.70f;
    private const float ProgressSeconds = 0.5f;
    private const float ProgressSecondsCelebrate = 1.0f;
    private const int PlusFlashFrames = 8;
    private const int SheenWidth = 6;
    private const float SheenSeconds = 0.3f;
    private const float SheenGap = 0.12f;
    private const float NewStart = 1.7f;
    private const float NewGap = 0.3f;
    private const int NewSwapOffset = 6;
    private const int ParticleMin = 8;
    private const int ParticleMax = 12;
    private const int ParticleLife = 18;
    private const int ParticleCapacity = 128;
    private const int CursorStartFrame = 56;         // 0.92초 이후 첫 프레임(T >= 0.92)
    private const int LiftStartFrame = 55;           // 시뮬레이션 selTick = round(0.92 x 60)
    private const float CursorMoveSeconds = 0.08f;
    private const int LiftPixels = 3;
    private const float CursorSize = 40f;
    private const float CursorOffsetY = 3f;
    private const int DetailStartFrame = 30;         // 0.5초
    private const int RevealFrames = 8;
    private const int BreathFrames = 120;
    private const int GlintPeriod = 150;
    private const int GlintFrames = 30;
    private const int GlintWidth = 6;
    private const float SkipAfterSeconds = 0.6f;
    private const int CelebrateStartFrame = 102;     // 열기 후 102프레임(1.7초)째가 ev = 0
    private const int TotalFlipFrames = 13;
    private const float GlowFadeDelay = 0.3f;
    private const float GlowFadeSeconds = 0.5f;
    private const int PlateDimFrames = 6;
    private const int ReactionFrames = 10;
    private const int PopFrames = 4;
    private const int ShakeFrames = 6;

    private static readonly int[] FlipWidths = { 36, 30, 24, 18, 12, 6, 2, 6, 12, 18, 24, 30, 36 };
    private static readonly int[] DetailFrontOffsets = { 2, -1, 0 };
    private static readonly int[] DetailBackOffsets = { 1, -1, 1, -1, 0 };
    private static readonly int[] PopOffsets = { -4, -3, -1, 0 };
    private static readonly int[] ShakeOffsets = { 1, -1, 0 };
    private static readonly int[] TraceHop = { -2, -2, -1, 0, 0 };
    private static readonly float[] GoldAlphas = { 0.7f, 0.5f, 0.3f, 0.15f, 0.08f };
    private static readonly int[] BundleStarts = { 0, 3, 7, 10, 13, 16, 20, 23, 26 };
    private const int BundleReferenceCount = 26;

    // ---------- 색(mock.py) ----------
    private static readonly Color32 CreamColor = new Color32(0xF1, 0xE4, 0xC8, 255);
    private static readonly Color32 MutedColor = new Color32(0x9A, 0x82, 0x6A, 255);
    private static readonly Color32 DimTextColor = new Color32(0x6B, 0x55, 0x45, 255);
    private static readonly Color32 GoldColor = new Color32(0xF3, 0xCC, 0x59, 255);
    private static readonly Color32 GoldHighColor = new Color32(0xFF, 0xE0, 0x8A, 255);
    private static readonly Color32 LineColor = new Color32(0x3A, 0x29, 0x1F, 255);
    private static readonly Color32 SheenColor = new Color32(0xFF, 0xF6, 0xC8, 255);
    private static readonly Color32 PlateInkColor = new Color32(0x2A, 0x1A, 0x12, 255);
    private static readonly Color32 BorderGoldColor = new Color32(0xF3, 0xCC, 0x59, 255);
    private static readonly Color32 BorderDeepColor = new Color32(0xD9, 0xA6, 0x40, 255);
    private static readonly Color32 DimPanelColor = new Color32(8, 6, 10, 255);
    private static readonly Color32 ActiveColor = new Color32(0xE8, 0xB0, 0x7A, 255);
    private static readonly Color32 PassiveColor = new Color32(0xA8, 0xD8, 0x8A, 255);
    private static readonly Color32 PrismColor = new Color32(0xC3, 0xA8, 0xFF, 255);
    private static readonly Color32 RubyColor = new Color32(0xF5, 0x8D, 0x8D, 255);
    private static readonly Color32 WhiteColor = new Color32(255, 255, 255, 255);

    // E4 입자 색(등급색 3개: 앞 2개 + 흰색)
    private static readonly Color32[] CommonParticleColors = { new Color32(0xF1, 0xE4, 0xC8, 255), new Color32(0xF3, 0xCC, 0x59, 255), WhiteColor };
    private static readonly Color32[] PrismParticleColors = { new Color32(0xC3, 0xA8, 0xFF, 255), new Color32(0x7D, 0x5C, 0xDC, 255), WhiteColor };
    private static readonly Color32[] RubyParticleColors = { new Color32(0xF5, 0x8D, 0x8D, 255), new Color32(0xC4, 0x3C, 0x4E, 255), WhiteColor };
    private static readonly Color32[] MaxParticleColors = { new Color32(0x9C, 0xF0, 0xC0, 255), new Color32(0x2F, 0x9E, 0x68, 255), WhiteColor };

    // 문구 키(RoguelikeUI.json)
    private const string TitleKey = "Roguelike_Collection_Title";
    private const string CompleteTitleKey = "Roguelike_Collection_CompleteTitle";
    private const string FoundKey = "Roguelike_Collection_Found";
    private const string CompleteKey = "Roguelike_Collection_Complete";
    private const string KindActiveKey = "Roguelike_Collection_KindActive";
    private const string KindPassiveKey = "Roguelike_Collection_KindPassive";
    private const string KindPrismKey = "Roguelike_Collection_KindPrism";
    private const string KindRubyKey = "Roguelike_Collection_KindRuby";
    private const string UnfoundKey = "Roguelike_Collection_Unfound";
    private const string MaxReachedKey = "Roguelike_Collection_MaxReached";
    private const string UnknownName = "? ? ?";

    // 설명 색 마커
    private const string BenefitColorTag = "<color=#F2D27A>";
    private const string DemeritColorTag = "<color=#E86A6A>";
    private const string ColorEndTag = "</color>";
    private const string BenefitMarker = "{+}";
    private const string DemeritMarker = "{-}";
    private const string EndMarker = "{/}";
    private const char MarkerStartChar = '{';

    public event Action ClosedEvent;

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Collection)
    [Header("도감 순서")]
    [SerializeField, Tooltip("도감 칸 순서(cardId). 메인메뉴가 이 순서로 칸 데이터를 만든다.")] private string[] collectionCardIds = new string[0];

    [Header("화면")]
    [SerializeField] private CanvasGroup dimGroup;
    [SerializeField] private Button dimButton;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField, Tooltip("판 바탕을 눌러도 E9 건너뛰기가 되도록 하는 버튼(전환 없음).")] private Button panelButton;
    [SerializeField, Tooltip("E9 창 안 어둡게(판 모양 Image).")] private Image panelDim;

    [Header("제목판")]
    [SerializeField] private RectTransform plateRoot;
    [SerializeField] private CanvasGroup plateGroup;
    [SerializeField] private Image plateImage;
    [SerializeField] private TMP_Text plateText;
    [SerializeField] private Sprite plateBrownSprite;
    [SerializeField] private Sprite plateGoldSprite;
    [SerializeField] private Sprite plateWhiteSprite;
    [SerializeField] private UI_RoguelikeCollectionPlateFx plateFx;

    [Header("진행도")]
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text progressCount;
    [SerializeField] private TMP_Text progressPercent;
    [SerializeField, Tooltip("진행 막대 · 막대 끝 반짝 · 완성 광택(기본 UI 머티리얼, 원점 = 판 왼쪽 아래).")] private UI_LootGlowParticles progressBar;

    [Header("분류 머리줄 · 칸")]
    [SerializeField] private HeaderRefs[] headers = new HeaderRefs[MaxSections];
    [SerializeField] private UI_RoguelikeCollectionSlot[] slots = new UI_RoguelikeCollectionSlot[0];
    [SerializeField, Tooltip("칸이 뒤집힐 때 터지는 입자(기본 UI 머티리얼, 원점 = 판 왼쪽 아래).")] private UI_LootGlowParticles burstGraphic;

    [Header("상세 패널")]
    [SerializeField] private CanvasGroup detailGroup;
    [SerializeField] private RectTransform detailContent;
    [SerializeField] private RectTransform ringsRect;
    [SerializeField] private UI_RoguelikeCard detailCard;
    [SerializeField] private RectTransform detailMask;
    [SerializeField] private TMP_Text detailKind;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDesc;
    [SerializeField] private Image detailMaxBadge;
    [SerializeField] private TMP_Text detailMaxLabel;

    [Header("뒤로 · 패드 아이콘")]
    [SerializeField] private UI_OptionButton backButton;
    [SerializeField] private RectTransform[] padOnlyItems = new RectTransform[0];
    [SerializeField] private UI_KeyboardImage[] keyGuideImages = new UI_KeyboardImage[0];

#if UNITY_EDITOR
    // 실제 기록이 없어도 연출(발견 · NEW 뒤집힘 · E9)을 눈으로 확인하려고 남겨 둔 에디터 전용 토글이다. 켜 두면 닫을 때 기록을 저장하지 않는다(IsDebugOverridden).
    [Header("에디터 시험 토글 (연출 확인용 - 빌드에는 없다)")]
    [SerializeField, Tooltip("모든 칸을 발견한 상태로 연다.")] private bool debugAllFound;
    [SerializeField, Tooltip("최초 완성 축하(E9)를 재생한다(모든 칸 발견 포함).")] private bool debugCelebrate;
    [SerializeField, Range(0, 8), Tooltip("발견한 칸 앞에서부터 이 수만큼을 NEW(뒤집힘 + 배지)로 만든다. E9와는 함께 쓰지 않는다.")] private int debugNewCount;
#endif

    //내부 의존성
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private CursorMotionSettings cursorMotion;
    private readonly List<RoguelikeCollectionViewData> items = new List<RoguelikeCollectionViewData>(32);
    private readonly StringBuilder markerBuilder = new StringBuilder(256);
    private readonly BurstParticle[] particles = new BurstParticle[ParticleCapacity];

    //상태 변수
    private bool bStandaloneClose;
    private bool bClosing;
    private int closeFrames;
    private float accumulator;
    private int frame;
    private bool bCelebrate;
    private bool bComplete;
    private bool bDirty;
    private int particleCount;
    private Vector2 panelBasePosition;
    private Vector2 ringsBasePosition;
    private bool bBaseCaptured;

    // 데이터 / 배치
    private int slotCount;
    private int foundCount;
    private int sectionCount;
    private readonly int[] sectionFirst = new int[MaxSections];
    private readonly int[] sectionLength = new int[MaxSections];
    private readonly int[] sectionFound = new int[MaxSections];
    private readonly ECardKind[] sectionKind = new ECardKind[MaxSections];
    private readonly int[] sectionRank = new int[MaxSections];
    private readonly int[] slotX = new int[32];
    private readonly int[] slotY = new int[32];
    private readonly int[] slotRow = new int[32];
    private readonly int[] slotCol = new int[32];
    private readonly int[] slotLand = new int[32];
    private readonly bool[] slotNew = new bool[32];
    private readonly bool[] slotSeen = new bool[32];
    private readonly int[] slotFlip = new int[32];
    private readonly int[] rowFirst = new int[32];
    private readonly int[] rowLength = new int[32];
    private int rowCount;

    // 선택 / 포커스
    private int selectedIndex;
    private int hoveredIndex = -1;
    private bool bSelectionActive;
    private int liftIndex = -1;
    private int prevLiftIndex = -1;
    private int liftFrame;
    private int cursorIndex = -1;

    // 문구 캐시
    private string titleString = string.Empty;
    private string completeTitleString = string.Empty;
    private string completeString = string.Empty;
    private string unfoundString = string.Empty;
    private float spaceWidth;
    private int barX;
    private int barWidth;
    private int percentRight;

    // 지난 값 캐시(바뀔 때만 갱신)
    private float lastDimAlpha = -1f;
    private float lastPanelAlpha = -1f;
    private float lastPlateAlpha = -1f;
    private int lastPanelDy = int.MinValue;
    private int lastPlateDy = int.MinValue;
    private int lastPlateKind = -1;
    private int lastProgressDisplay = -1;
    private int lastProgressPercentState = int.MinValue;
    private int lastProgressPop = int.MinValue;
    private int lastBarFill = -1;
    private int lastBarPlus = -1;
    private int lastBarGlint = int.MinValue;
    private float lastPanelDimAlpha = -1f;
    private int lastDetailKey = int.MinValue;
    private int lastDetailFrame;
    private int lastDetailOffsetX = int.MinValue;
    private int lastDetailOffsetY = int.MinValue;
    private int lastDetailBreath = int.MinValue;
    private int lastDetailClip = -1;
    private bool lastDetailVisible;
    private readonly float[] lastHeaderAlpha = new float[MaxSections];
    private readonly int[] lastHeaderGold = new int[MaxSections];
    private readonly int[] lastHeaderSheen = new int[MaxSections];
    private bool bBurstDrawn;

    // 캐싱 델리게이트
    private Action cancelHandler;
    private Action escHandler;
    private Action languageChangedHandler;
    private Action<EInputDeviceType> deviceChangedHandler;
    private Action<UI_RoguelikeCollectionSlot, bool> slotFocusedHandler;
    private Action<UI_RoguelikeCollectionSlot> slotExitedHandler;
    private Action<UI_RoguelikeCollectionSlot> slotClickedHandler;

    public IReadOnlyList<string> CollectionCardIds => collectionCardIds;

    private bool IsGamepadMode => null != inputManager && true == inputManager.IsGamepadMode;
    private float Seconds => frame * FrameSeconds;

    // 퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = viewCtx?.localizationManager;
        inputManager = viewCtx?.inputManager;
        cursorMotion = CursorMotionSettings.Card;

        cancelHandler = RequestClose;
        escHandler = RequestClose;
        languageChangedHandler = OnLanguageChanged;
        deviceChangedHandler = OnDeviceChanged;
        slotFocusedHandler = OnSlotFocused;
        slotExitedHandler = OnSlotExited;
        slotClickedHandler = OnSlotClicked;

        if (null != dimButton)
        {
            dimButton.onClick.RemoveListener(OnDimClicked);
            dimButton.onClick.AddListener(OnDimClicked);
        }

        if (null != panelButton)
        {
            panelButton.onClick.RemoveListener(OnPanelClicked);
            panelButton.onClick.AddListener(OnPanelClicked);
        }

        for (int i = 0; keyGuideImages.Length > i; i++)
        {
            if (null != keyGuideImages[i]) keyGuideImages[i].Initialize(inputManager);
        }

        if (null != backButton) backButton.Initialize(RequestClose, SoundID.MainMenuDot01, SoundID.None);

        for (int i = 0; slots.Length > i; i++)
        {
            UI_RoguelikeCollectionSlot slot = slots[i];
            if (null == slot) continue;

            slot.Initialize();
            slot.FocusedEvent -= slotFocusedHandler;
            slot.FocusedEvent += slotFocusedHandler;
            slot.ExitedEvent -= slotExitedHandler;
            slot.ExitedEvent += slotExitedHandler;
            slot.ClickedEvent -= slotClickedHandler;
            slot.ClickedEvent += slotClickedHandler;
        }
    }

    /// <summary>깊이 관리(ESC 닫기)가 없는 화면(메인메뉴)에서 열 때 켠다. 켜면 키보드 Esc를 이 창이 직접 받아 닫는다.</summary>
    public void SetStandaloneClose(bool _bStandalone)
    {
        bStandaloneClose = _bStandalone;
    }

    /// <summary>이번에 최초 완성 축하(E9)를 재생했다. 닫힌 뒤(ClosedEvent)에도 다음 OpenCollection까지 값이 남는다.</summary>
    public bool IsCelebrated => bCelebrate;

    /// <summary>
    /// 에디터 시험 토글(모든 칸 발견 · 축하 · NEW 강제) 중이라 화면이 실제 기록과 다르다. 이때는 닫을 때 기록을 저장하지 않는다.
    /// 빌드에서는 항상 false다.
    /// </summary>
    public bool IsDebugOverridden
    {
        get
        {
#if UNITY_EDITOR
            return true == debugAllFound || true == debugCelebrate || 0 < debugNewCount;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 이번에 앞면으로 보여 준 칸의 cardId를 _output에 더한다(지우지 않는다). 발견한 칸 중 NEW 뒤집힘이 끝나기 전에 닫은 칸은 뺀다.
    /// 닫힌 뒤(ClosedEvent)에도 읽을 수 있다. 저장은 호출한 쪽(메인메뉴)이 RoguelikeRecordRepository.SaveCollectionSeen으로 한다.
    /// </summary>
    public void CollectShownCardIds(List<string> _output)
    {
        if (null == _output) return;

        for (int i = 0; slotCount > i; i++)
        {
            if (false == items[i].bFound) continue;
            if (true == slotNew[i] && frame < slotFlip[i] + NewSwapOffset) continue;

            _output.Add(items[i].cardId);
        }
    }

    /// <summary>
    /// 도감 칸 목록(분류 순서대로)을 복사해 창을 열고 등장 연출을 시작한다.
    /// _bCelebrate = 이번에 처음 도감을 완성했다(E9 최초 완성 축하). 모든 칸이 발견 상태가 아니면 무시한다. 이미 완성한 뒤의 재방문은 false로 열면 E7(금색 제목판 + 유지 연출)이 된다.
    /// </summary>
    public void OpenCollection(IReadOnlyList<RoguelikeCollectionViewData> _items, bool _bCelebrate = false)
    {
        items.Clear();
        if (null != _items)
        {
            int capacity = Mathf.Min(_items.Count, slots.Length);
            for (int i = 0; capacity > i; i++) items.Add(_items[i]);
        }

        bool bRequestCelebrate = _bCelebrate;
        ApplyDebugOverrides(ref bRequestCelebrate);

        Show();
        Sound.PlayUI(SoundID.ResultUIOpen);

        PrepareOpen(bRequestCelebrate);
        RenderFrame();
        if (true == IsGamepadMode) SelectSlot(selectedIndex);
    }

    protected override void OnShowContent()
    {
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.UICancelEvent -= cancelHandler;
            inputManager.inputReader.UICancelEvent += cancelHandler;
            inputManager.inputReader.ESCButtonPressedEvent -= escHandler;
            if (true == bStandaloneClose) inputManager.inputReader.ESCButtonPressedEvent += escHandler;
            inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
            inputManager.inputReader.InputDeviceChangedEvent += deviceChangedHandler;
        }

        if (null != localizationManager)
        {
            localizationManager.OnLanguageChanged -= languageChangedHandler;
            localizationManager.OnLanguageChanged += languageChangedHandler;
        }
    }

    protected override void OnHideContent()
    {
        UnbindInput();
        ReleaseSelection();
        HideCursor();
        bClosing = false;
        hoveredIndex = -1;
        bSelectionActive = false;
        ClearParticles();
        if (null != plateFx) plateFx.HideAll();
        ClosedEvent?.Invoke();
    }

    /// <summary>닫힘 연출(딤 0.18초 · 판 / 제목판 0.15초)을 거쳐 닫는다. 연출이 끝나면 base.Hide가 불려 ClosedEvent가 나온다.</summary>
    public override void Hide()
    {
        if (false == IsVisible || true == bClosing) return;

        if (false == isActiveAndEnabled)
        {
            base.Hide();
            return;
        }

        bClosing = true;
        closeFrames = 0;
        accumulator = 0f;
        Sound.PlayUI(SoundID.MainClick);
        HideCursor();
        ReleaseSelection();
        if (null != panelGroup) panelGroup.blocksRaycasts = false;
    }

    // 일반 비즈니스 로직 및 내부 메서드

    // // 열기 준비

    private void ApplyDebugOverrides(ref bool _bCelebrate)
    {
#if UNITY_EDITOR
        if (true == debugAllFound || true == debugCelebrate)
        {
            for (int i = 0; items.Count > i; i++)
            {
                RoguelikeCollectionViewData data = items[i];
                data.bFound = true;
                items[i] = data;
            }
        }

        if (true == debugCelebrate) _bCelebrate = true;

        if (0 < debugNewCount && false == _bCelebrate)
        {
            int remain = debugNewCount;
            for (int i = 0; items.Count > i && 0 < remain; i++)
            {
                RoguelikeCollectionViewData data = items[i];
                if (false == data.bFound) continue;

                data.bNew = true;
                items[i] = data;
                remain--;
            }
        }
#endif
    }

    private void PrepareOpen(bool _bCelebrateRequest)
    {
        CaptureBase();

        slotCount = items.Count;
        foundCount = 0;
        for (int i = 0; slotCount > i; i++)
        {
            if (true == items[i].bFound) foundCount++;
        }

        bComplete = 0 < slotCount && foundCount == slotCount;
        bCelebrate = true == _bCelebrateRequest && true == bComplete;

        frame = 0;
        accumulator = 0f;
        bClosing = false;
        closeFrames = 0;
        selectedIndex = 0;
        hoveredIndex = -1;
        liftIndex = -1;
        prevLiftIndex = -1;
        liftFrame = 0;
        cursorIndex = -1;
        bSelectionActive = false;
        ClearParticles();
        InvalidateCaches();

        if (null != panelGroup) panelGroup.blocksRaycasts = true;

        BuildLayout();
        AssignNewInfo();
        ApplyStaticTexts();
        LayoutAll();

        for (int i = 0; slots.Length > i; i++)
        {
            UI_RoguelikeCollectionSlot slot = slots[i];
            if (null == slot) continue;

            bool bActive = slotCount > i;
            slot.gameObject.SetActive(bActive);
            if (false == bActive) continue;

            slot.ResetVisual();
            RoguelikeCollectionViewData data = items[i];
            slot.SetData(in data, false == slotNew[i]);
            slot.SetAlpha(0f);
        }

        if (null != plateFx) plateFx.HideAll();

        if (true == IsGamepadMode)
        {
            bSelectionActive = true;
            liftIndex = selectedIndex;
            liftFrame = LiftStartFrame;
        }
    }

    private void CaptureBase()
    {
        if (true == bBaseCaptured) return;

        if (null != panelRoot) panelBasePosition = panelRoot.anchoredPosition;
        if (null != ringsRect) ringsBasePosition = ringsRect.anchoredPosition;
        bBaseCaptured = true;
    }

    /// <summary>분류(액티브 → 패시브 → 프리즘 → 루비)별 시작 · 칸 위치 · 행 목록 · 착지 프레임을 정한다. 칸 데이터가 이미 분류 순서대로 정렬되어 있다고 본다.</summary>
    private void BuildLayout()
    {
        sectionCount = 0;
        rowCount = 0;
        for (int s = 0; MaxSections > s; s++)
        {
            sectionLength[s] = 0;
            sectionFound[s] = 0;
            sectionRank[s] = -1;
        }

        int y = SectionStartY;
        int i = 0;
        while (slotCount > i && MaxSections > sectionCount)
        {
            ECardKind kind = items[i].kind;
            int s = sectionCount;
            sectionCount++;
            sectionKind[s] = kind;
            sectionFirst[s] = i;

            int end = i;
            while (slotCount > end && kind == items[end].kind) end++;
            sectionLength[s] = end - i;

            int slotTop = y + SectionSlotTop;
            int rows = (sectionLength[s] + Columns - 1) / Columns;
            int rowBase = rowCount;
            for (int r = 0; rows > r; r++)
            {
                rowFirst[rowCount] = i + r * Columns;
                rowLength[rowCount] = Mathf.Min(Columns, end - (i + r * Columns));
                rowCount++;
            }

            for (int k = i; end > k; k++)
            {
                int local = k - i;
                slotX[k] = GridX + (local % Columns) * SlotStep;
                slotY[k] = slotTop + (local / Columns) * SlotRowStep;
                slotRow[k] = rowBase + local / Columns;
                slotCol[k] = local % Columns;
                if (true == items[k].bFound) sectionFound[s]++;
            }

            y = slotTop + rows * SlotRowStep + SectionGap;
            i = end;
        }

        int rank = 0;
        for (int s = 0; sectionCount > s; s++)
        {
            if (sectionFound[s] == sectionLength[s] && 0 < sectionLength[s]) { sectionRank[s] = rank; rank++; }
        }

        for (int k = 0; slotCount > k; k++)
        {
            slotLand[k] = (int)Math.Ceiling((SlotStart + k * (double)SlotGap + SlotDrop) * 60.0 - 1e-9);

            UI_RoguelikeCollectionSlot slot = slots[k];
            if (null == slot) continue;

            RectTransform rect = slot.Rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(UI_RoguelikeCollectionSlot.SlotSize, UI_RoguelikeCollectionSlot.SlotSize);
            rect.anchoredPosition = new Vector2(slotX[k], -slotY[k]);
            slotSeen[k] = false;
        }

        SetupNavigation();
    }

    /// <summary>NEW 칸: 발견했고 bNew인 칸을 도감 순서대로 1.7초부터 0.3초 간격으로 뒤집는다. 최초 완성 축하(E9)에서는 쓰지 않는다.</summary>
    private void AssignNewInfo()
    {
        int order = 0;
        for (int i = 0; slotCount > i; i++)
        {
            bool bNew = false == bCelebrate && true == items[i].bNew && true == items[i].bFound;
            slotNew[i] = bNew;
            slotFlip[i] = bNew ? Mathf.RoundToInt((NewStart + order * NewGap) * 60f) : -1;
            if (true == bNew) order++;
        }
    }

    /// <summary>패드 탐색(Explicit): 시뮬레이션 행 규칙. 좌 / 우는 같은 행 안, 위 / 아래는 인접 행의 같은 열(행이 짧으면 마지막 칸).</summary>
    private void SetupNavigation()
    {
        for (int i = 0; slotCount > i; i++)
        {
            UI_RoguelikeCollectionSlot slot = slots[i];
            if (null == slot || null == slot.SelectButton) continue;

            int row = slotRow[i];
            int col = slotCol[i];
            Navigation navigation = slot.SelectButton.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = 0 < col ? slots[i - 1].SelectButton : null;
            navigation.selectOnRight = rowLength[row] - 1 > col ? slots[i + 1].SelectButton : null;
            navigation.selectOnUp = 0 < row ? slots[rowFirst[row - 1] + Mathf.Min(col, rowLength[row - 1] - 1)].SelectButton : null;
            navigation.selectOnDown = rowCount - 1 > row ? slots[rowFirst[row + 1] + Mathf.Min(col, rowLength[row + 1] - 1)].SelectButton : null;
            slot.SelectButton.navigation = navigation;
        }
    }

    // // 문구 · 글자 배치

    private string Localize(string _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
    }

    private void ApplyStaticTexts()
    {
        titleString = Localize(TitleKey);
        completeTitleString = Localize(CompleteTitleKey);
        completeString = Localize(CompleteKey);
        unfoundString = Localize(UnfoundKey);

        if (null != progressLabel) progressLabel.text = Localize(FoundKey);
        if (null != detailMaxLabel) detailMaxLabel.text = Localize(MaxReachedKey);

        for (int s = 0; sectionCount > s; s++)
        {
            HeaderRefs h = headers[s];
            if (null == h) continue;

            string title = Localize(KindKey(sectionKind[s]));
            Color32 color = KindColor(sectionKind[s]);
            h.title.text = title;
            h.title.color = color;
            if (null != h.sheenTitle) h.sheenTitle.text = title;
        }
    }

    private static string KindKey(ECardKind _kind)
    {
        switch (_kind)
        {
            case ECardKind.Active: return KindActiveKey;
            case ECardKind.Passive: return KindPassiveKey;
            case ECardKind.Prism: return KindPrismKey;
            default: return KindRubyKey;
        }
    }

    private static Color32 KindColor(ECardKind _kind)
    {
        switch (_kind)
        {
            case ECardKind.Active: return ActiveColor;
            case ECardKind.Passive: return PassiveColor;
            case ECardKind.Prism: return PrismColor;
            default: return RubyColor;
        }
    }

    private static float MeasureWidth(TMP_Text _text, string _value)
    {
        return Mathf.Ceil(_text.GetPreferredValues(_value, Unbounded, Unbounded).x);
    }

    /// <summary>시뮬레이션 글자 윗변(_top) 기준으로 TMP 상자를 놓는다(기준선 = 윗변 + 11 = 상자 윗변 + 반올림 ascent). 왼쪽 위 앵커 · 피벗이어야 한다.</summary>
    private static void PlaceText(TMP_Text _text, float _x, float _top, float _width)
    {
        float lineHeight = Mathf.Ceil(UI_RoguelikeChallengeRow.MeasureLineHeight(_text));
        float rectTop = _top + TextBaseline - Mathf.Round(UI_RoguelikeChallengeRow.MeasureAscent(_text));
        RectTransform rect = _text.rectTransform;
        rect.sizeDelta = new Vector2(_width, lineHeight);
        rect.anchoredPosition = new Vector2(_x, -rectTop);
    }

    /// <summary>언어 · 폰트가 바뀌었을 때 글자 폭에 의존하는 배치(제목판 폭 · 진행도 · 머리줄 · 상세)를 다시 잡는다.</summary>
    private void LayoutAll()
    {
        LayoutPlate();
        LayoutProgress();
        LayoutHeaders();
        lastDetailKey = int.MinValue;
        lastDetailClip = -1;
    }

    private void LayoutPlate()
    {
        if (null == plateText || null == plateRoot) return;

        float widest = Mathf.Max(MeasureWidth(plateText, titleString), MeasureWidth(plateText, completeTitleString));
        int width = Mathf.Max(SpecPlateWidth, Mathf.CeilToInt(widest) + PlatePadding * 2);
        if (0 != (width & 1)) width++;

        plateRoot.sizeDelta = new Vector2(width, plateRoot.sizeDelta.y);
        if (null != plateFx) plateFx.SetPlateWidth(width);
        lastPlateKind = -1;
        lastPlateDy = int.MinValue;
    }

    private int PlateWidth => null != plateRoot ? Mathf.RoundToInt(plateRoot.sizeDelta.x) : SpecPlateWidth;

    private void LayoutProgress()
    {
        if (null == progressLabel || null == progressCount || null == progressPercent) return;

        spaceWidth = MeasureWidth(progressLabel, "a a") - MeasureWidth(progressLabel, "aa");
        float labelWidth = MeasureWidth(progressLabel, progressLabel.text);
        PlaceText(progressLabel, GridX, ProgressTextTop, labelWidth + TextWidthSlack);

        string numberFinal = string.Concat(foundCount.ToString(CultureInfo.InvariantCulture), " / ", slotCount.ToString(CultureInfo.InvariantCulture));
        string percentFinal = true == bComplete ? completeString : string.Concat(PercentOf(foundCount).ToString(CultureInfo.InvariantCulture), "%");
        float numberWidth = MeasureWidth(progressCount, numberFinal);
        float percentWidth = MeasureWidth(progressPercent, percentFinal);

        percentRight = ProgressRight;
        barX = Mathf.RoundToInt(GridX + labelWidth + spaceWidth + numberWidth + ProgressGap);
        barWidth = Mathf.Max(0, Mathf.RoundToInt(ProgressRight - percentWidth - ProgressGap - barX));

        lastProgressDisplay = -1;
        lastProgressPercentState = int.MinValue;
        lastProgressPop = int.MinValue;
        lastBarFill = -1;
        lastBarPlus = -1;
        lastBarGlint = int.MinValue;
    }

    private int PercentOf(int _count)
    {
        return 0 < slotCount ? Mathf.FloorToInt(_count * 100f / slotCount) : 0;
    }

    private void LayoutHeaders()
    {
        for (int s = 0; sectionCount > s; s++)
        {
            HeaderRefs h = headers[s];
            if (null == h || null == h.root) continue;

            h.root.gameObject.SetActive(true);
            h.root.anchoredPosition = new Vector2(GridX, -(SectionTopOf(s) - SectionHeaderTop));

            string countText = string.Concat(sectionFound[s].ToString(CultureInfo.InvariantCulture), "/", sectionLength[s].ToString(CultureInfo.InvariantCulture));
            float titleWidth = MeasureWidth(h.title, h.title.text);
            float countWidth = MeasureWidth(h.count, countText);
            h.count.text = countText;
            PlaceText(h.title, 0f, HeaderTextTop, titleWidth + TextWidthSlack);
            PlaceText(h.count, HeaderRight - countWidth, HeaderTextTop, countWidth + TextWidthSlack);

            float lineX = titleWidth + 4f;
            float lineWidth = Mathf.Max(0f, HeaderRight - titleWidth - countWidth - 8f);
            h.line.sizeDelta = new Vector2(lineWidth, 1f);
            h.line.anchoredPosition = new Vector2(lineX, -HeaderLineY);

            if (null != h.sheenTitle && null != h.sheenCount && null != h.sheenLine)
            {
                h.sheenCount.text = countText;
                PlaceText(h.sheenTitle, 0f, HeaderTextTop, titleWidth + TextWidthSlack);
                PlaceText(h.sheenCount, HeaderRight - countWidth, HeaderTextTop, countWidth + TextWidthSlack);
                h.sheenLine.sizeDelta = new Vector2(lineWidth, 1f);
                h.sheenLine.anchoredPosition = new Vector2(lineX, -HeaderLineY);
            }

            lastHeaderGold[s] = -1;
            lastHeaderSheen[s] = int.MinValue;
            lastHeaderAlpha[s] = -1f;
        }

        for (int s = sectionCount; MaxSections > s; s++)
        {
            if (null != headers[s] && null != headers[s].root) headers[s].root.gameObject.SetActive(false);
        }
    }

    /// <summary>분류 s의 시작 y(머리줄 글자 윗변이 아니라 슬롯 행 윗변 - 14).</summary>
    private int SectionTopOf(int _section)
    {
        return slotY[sectionFirst[_section]] - SectionSlotTop;
    }

    private void InvalidateCaches()
    {
        lastDimAlpha = -1f;
        lastPanelAlpha = -1f;
        lastPlateAlpha = -1f;
        lastPanelDy = int.MinValue;
        lastPlateDy = int.MinValue;
        lastPlateKind = -1;
        lastProgressDisplay = -1;
        lastProgressPercentState = int.MinValue;
        lastProgressPop = int.MinValue;
        lastBarFill = -1;
        lastBarPlus = -1;
        lastBarGlint = int.MinValue;
        lastPanelDimAlpha = -1f;
        lastDetailKey = int.MinValue;
        lastDetailOffsetX = int.MinValue;
        lastDetailOffsetY = int.MinValue;
        lastDetailBreath = int.MinValue;
        lastDetailClip = -1;
        lastDetailVisible = true;
        for (int s = 0; MaxSections > s; s++)
        {
            lastHeaderAlpha[s] = -1f;
            lastHeaderGold[s] = -1;
            lastHeaderSheen[s] = int.MinValue;
        }
    }

    // // 한 프레임(1/60초) 진행 · 그리기

    private static float OutCubic(float _t)
    {
        float inverse = 1f - _t;
        return 1f - inverse * inverse * inverse;
    }

    private static int RoundHalfUp(float _value)
    {
        return Mathf.FloorToInt(_value + 0.5f);
    }

    /// <summary>30fps 틱 값 배열을 60fps 프레임 _frame에서 선형 보간한 값(시뮬레이션 trkf).</summary>
    private static float Track(int[] _values, int _frame)
    {
        float p = _frame * 0.5f;
        int i = Mathf.FloorToInt(p);
        if (_values.Length - 1 <= i) return _values[_values.Length - 1];
        return _values[i] + (_values[i + 1] - _values[i]) * (p - i);
    }

    private static float Track(float[] _values, int _frame)
    {
        float p = _frame * 0.5f;
        int i = Mathf.FloorToInt(p);
        if (_values.Length - 1 <= i) return _values[_values.Length - 1];
        return _values[i] + (_values[i + 1] - _values[i]) * (p - i);
    }

    /// <summary>1프레임 진행(값은 모두 frame의 함수이고, 입자 · 소리 · 선택 표시만 상태가 있다).</summary>
    private void StepFrame()
    {
        if (true == bClosing)
        {
            closeFrames++;
            return;
        }

        frame++;

        // E4: 뒤집힘 시작 소리, 폭 2가 되는 프레임에 입자
        for (int i = 0; slotCount > i; i++)
        {
            if (false == slotNew[i]) continue;

            if (frame == slotFlip[i]) Sound.PlayUI(SoundID.SFXCardSelectNormal);
            if (frame == slotFlip[i] + NewSwapOffset) SpawnBurst(i);
        }

        // 커서가 올라가는 순간부터 NEW 배지는 사라진다(E5)
        if (CursorStartFrame <= frame && true == bSelectionActive && 0 <= selectedIndex && slotCount > selectedIndex)
        {
            if (true == slotNew[selectedIndex] && frame >= slotFlip[selectedIndex] + NewSwapOffset) slotSeen[selectedIndex] = true;
        }

        if (CursorStartFrame == frame && true == bSelectionActive && true == IsGamepadMode) ShowCursor(selectedIndex);

        StepParticles();
    }

    /// <summary>E9 건너뛰기: 0.6초 뒤부터 끝 상태(ev 210)로 시간을 당긴다. 건너뛰었으면 true.</summary>
    private bool TrySkipCelebration()
    {
        if (false == bCelebrate || true == bClosing || false == IsVisible) return false;
        if (SkipAfterSeconds > Seconds) return false;

        int ev = frame - CelebrateStartFrame;
        if (UI_RoguelikeCollectionPlateFx.EndFrame <= ev) return false;

        frame += UI_RoguelikeCollectionPlateFx.EndFrame - ev;
        ClearParticles();
        bDirty = true;
        return true;
    }

    private void RenderFrame()
    {
        bDirty = false;
        if (true == bClosing)
        {
            RenderEnvelope(Seconds, true);
            RenderPlate(frame);
            return;
        }

        float t = Seconds;
        RenderEnvelope(t, false);
        int ev = true == bCelebrate ? frame - CelebrateStartFrame : -99;
        RenderProgress(frame, t, ev);
        RenderHeaders(t);
        RenderSlots(frame, t, ev);
        RenderDetail(frame, t);
        RenderPanelDim(ev);
        RenderParticles();
        RenderPlate(frame);
    }

    /// <summary>E1 열기 / E8 닫기: 딤 · 판 · 제목판의 알파와 6px 오르내림.</summary>
    private void RenderEnvelope(float _t, bool _bClosing)
    {
        float dimAlpha;
        float panelAlpha;
        float plateAlpha;
        int panelDy;
        int plateDy;
        if (false == _bClosing)
        {
            dimAlpha = Mathf.Clamp01(_t / DimSeconds);
            float plateProgress = Mathf.Clamp01(_t / PlateInSeconds);
            float panelProgress = Mathf.Clamp01((_t - PanelDelaySeconds) / PanelInSeconds);
            plateAlpha = OutCubic(plateProgress);
            plateDy = RoundHalfUp(RiseDistance * (1f - OutCubic(plateProgress)));
            panelAlpha = OutCubic(panelProgress);
            panelDy = RoundHalfUp(RiseDistance * (1f - OutCubic(panelProgress)));
        }
        else
        {
            float closeTime = closeFrames * FrameSeconds;
            float progress = Mathf.Clamp01(closeTime / CloseSeconds);
            dimAlpha = 1f - Mathf.Clamp01(closeTime / DimSeconds);
            panelAlpha = 1f - progress;
            plateAlpha = 1f - progress;
            panelDy = RoundHalfUp(RiseDistance * progress);
            plateDy = panelDy;
        }

        if (dimAlpha != lastDimAlpha)
        {
            lastDimAlpha = dimAlpha;
            if (null != dimGroup) dimGroup.alpha = dimAlpha;
        }

        if (panelAlpha != lastPanelAlpha)
        {
            lastPanelAlpha = panelAlpha;
            if (null != panelGroup) panelGroup.alpha = panelAlpha;
        }

        if (plateAlpha != lastPlateAlpha)
        {
            lastPlateAlpha = plateAlpha;
            if (null != plateGroup) plateGroup.alpha = plateAlpha;
        }

        if (panelDy != lastPanelDy)
        {
            lastPanelDy = panelDy;
            if (null != panelRoot) panelRoot.anchoredPosition = new Vector2(panelBasePosition.x, panelBasePosition.y - panelDy);
        }

        lastPlateDy = plateDy;
    }

    // E9 제목판 두근 흔들림(박자마다 가로 +1, -1, 0)
    private int PlateShake(int _ev)
    {
        if (0 > _ev) return 0;

        for (int b = 0; UI_RoguelikeCollectionPlateFx.BeatCount > b; b++)
        {
            int a = _ev - UI_RoguelikeCollectionPlateFx.BeatFrames[b];
            if (0 <= a && ShakeFrames > a) return RoundHalfUp(Track(ShakeOffsets, a));
        }

        return 0;
    }

    /// <summary>제목판 그림 · 글자 · 완성 연출(E7 / E9). 제목판 알파 · 세로 위치는 RenderEnvelope가 이미 정했고(lastPlate*), 여기서는 모양과 연출 층을 그린다.</summary>
    private void RenderPlate(int _n)
    {
        if (null == plateRoot) return;

        int ev = true == bCelebrate ? _n - CelebrateStartFrame : -99;
        int kind = 0;                 // 0 갈색 · 1 금색 · 2 흰색
        bool bCompleteLabel = false;
        if (true == bCelebrate)
        {
            if (UI_RoguelikeCollectionPlateFx.PangFrame + UI_RoguelikeCollectionPlateFx.WhiteFrames <= ev)
            {
                kind = 1;
                bCompleteLabel = UI_RoguelikeCollectionPlateFx.SwapFrame > ev;
            }
            else if (UI_RoguelikeCollectionPlateFx.PangFrame <= ev)
            {
                kind = 2;
            }
        }
        else if (true == bComplete)
        {
            kind = 1;
        }

        int plateState = kind * 2 + (true == bCompleteLabel ? 1 : 0);
        if (plateState != lastPlateKind)
        {
            lastPlateKind = plateState;
            string label = true == bCompleteLabel ? completeTitleString : titleString;
            if (null != plateImage)
            {
                plateImage.sprite = 0 == kind ? plateBrownSprite : (1 == kind ? plateGoldSprite : plateWhiteSprite);
            }

            if (null != plateText)
            {
                plateText.gameObject.SetActive(2 != kind);
                plateText.color = 1 == kind ? PlateInkColor : CreamColor;
                plateText.text = label;
                float width = MeasureWidth(plateText, label);
                float x = Mathf.Floor(PlateWidth * 0.5f - width * 0.5f);
                PlaceText(plateText, x, SimPlateTextTop - SimPlateTop, width + TextWidthSlack);
            }
        }

        int dx = PlateShake(ev);
        int plateWidth = PlateWidth;
        plateRoot.anchoredPosition = new Vector2(SimPlateCenterX - (plateWidth >> 1) + dx, -(SimPlateTop + lastPlateDy));

        if (null == plateFx) return;

        if (true == bCelebrate)
        {
            if (0 > ev) plateFx.HideAll();
            else plateFx.Render(ev, ev * FrameSeconds, (ev - UI_RoguelikeCollectionPlateFx.IdleFrame) * FrameSeconds, ev * FrameSeconds, 1f, lastPlateAlpha, lastPlateDy, dx);
        }
        else if (true == bComplete)
        {
            float t = _n * FrameSeconds;
            plateFx.Render(9999, 99f, t - GlowFadeDelay, t, Mathf.Clamp01((t - GlowFadeDelay) / GlowFadeSeconds), lastPlateAlpha, lastPlateDy, 0);
        }
        else
        {
            plateFx.HideAll();
        }
    }

    // // E3 진행도

    private void RenderProgress(int _n, float _t, int _ev)
    {
        if (null == progressCount || null == progressPercent || null == progressBar) return;

        bool bE9 = true == bCelebrate;
        float duration = bE9 ? ProgressSecondsCelebrate : ProgressSeconds;
        float ratio = Mathf.Clamp01((_t - ProgressStart) / duration);
        float cont = bE9 ? foundCount * ratio * ratio : foundCount * OutCubic(ratio);
        int display = Mathf.FloorToInt(cont + 1e-5f);
        float doneTime = ProgressStart + ProgressSeconds;
        bool bDone = bE9 ? 0 <= _ev : _t >= doneTime;
        int percentState = true == bComplete && true == bDone ? -1 : PercentOf(display);
        int pop = bE9 && 0 <= _ev && 8 > _ev ? RoundHalfUp(Track(PopOffsets, _ev)) : 0;

        if (display != lastProgressDisplay || pop != lastProgressPop)
        {
            if (display != lastProgressDisplay)
            {
                lastProgressDisplay = display;
                progressCount.text = string.Concat(display.ToString(CultureInfo.InvariantCulture), " / ", slotCount.ToString(CultureInfo.InvariantCulture));
            }

            lastProgressPop = pop;
            float labelWidth = MeasureWidth(progressLabel, progressLabel.text);
            PlaceText(progressCount, GridX + labelWidth + spaceWidth, ProgressTextTop + pop, MeasureWidth(progressCount, progressCount.text) + TextWidthSlack);
        }

        if (percentState != lastProgressPercentState)
        {
            lastProgressPercentState = percentState;
            progressPercent.text = -1 == percentState ? completeString : string.Concat(percentState.ToString(CultureInfo.InvariantCulture), "%");
            float width = MeasureWidth(progressPercent, progressPercent.text);
            PlaceText(progressPercent, percentRight - width, ProgressTextTop, width + TextWidthSlack);
        }

        int fill = 0 < slotCount ? Mathf.FloorToInt(barWidth * cont / slotCount) : 0;
        int plusFrame = _n - Mathf.RoundToInt(doneTime * 60f);
        int plus = false == bE9 && 0 <= plusFrame && PlusFlashFrames > plusFrame ? (plusFrame >> 1) : -1;
        int glint = int.MinValue;
        if (true == bComplete && (bE9 ? UI_RoguelikeCollectionPlateFx.EndFrame <= _ev : _t >= doneTime))
        {
            int a = _n % GlintPeriod;
            if (GlintFrames > a) glint = RoundHalfUp(barX - GlintWidth + (barWidth + GlintWidth) * (a / (float)GlintFrames));
        }

        if (fill == lastBarFill && plus == lastBarPlus && glint == lastBarGlint) return;

        lastBarFill = fill;
        lastBarPlus = plus;
        lastBarGlint = glint;
        DrawBar(fill, plus, glint);
    }

    // 판 안 y(아래쪽 +) 구간 [_top, _bottom)을 그래픽(원점 = 판 왼쪽 아래, 위쪽 +) 사각형으로 쌓는다.
    private void BarRect(UI_LootGlowParticles _graphic, int _x0, int _top, int _x1, int _bottom, Color32 _color)
    {
        _graphic.Rect(_x0, PanelHeight - _bottom, _x1, PanelHeight - _top, _color);
    }

    private void DrawBar(int _fill, int _plus, int _glint)
    {
        progressBar.BeginQuads();
        BarRect(progressBar, barX, ProgressBarY, barX + barWidth, ProgressBarY + ProgressBarHeight, LineColor);
        if (0 < _fill)
        {
            BarRect(progressBar, barX, ProgressBarY, barX + _fill, ProgressBarY + ProgressBarHeight, GoldColor);
            BarRect(progressBar, barX, ProgressBarY, barX + _fill, ProgressBarY + 1, GoldHighColor);
        }

        if (0 <= _plus)
        {
            int cx = barX + _fill - 1;
            int cy = ProgressBarY + 1;
            if (0 == _plus || 3 == _plus)
            {
                BarRect(progressBar, cx, cy, cx + 1, cy + 1, WhiteColor);
            }
            else
            {
                BarRect(progressBar, cx - 1, cy, cx + 2, cy + 1, WhiteColor);
                BarRect(progressBar, cx, cy - 1, cx + 1, cy + 2, WhiteColor);
            }
        }

        if (int.MinValue != _glint)
        {
            int x0 = Mathf.Max(barX, _glint);
            int x1 = Mathf.Min(barX + barWidth, _glint + GlintWidth);
            if (x0 < x1) BarRect(progressBar, x0, ProgressBarY, x1, ProgressBarY + ProgressBarHeight, SheenColor);
        }

        progressBar.Commit();
    }

    // // 분류 머리줄

    private void RenderHeaders(float _t)
    {
        for (int s = 0; sectionCount > s; s++)
        {
            HeaderRefs h = headers[s];
            if (null == h || null == h.group) continue;

            float start = SlotStart + sectionFirst[s] * SlotGap;
            float alpha = Mathf.Clamp01((_t - start) / HeaderFadeSeconds);
            if (alpha != lastHeaderAlpha[s])
            {
                lastHeaderAlpha[s] = alpha;
                h.group.alpha = alpha;
            }

            if (0f >= alpha) continue;

            bool bFull = sectionFound[s] == sectionLength[s];
            int rank = sectionRank[s];
            float sheenStart = true == bCelebrate ? (CelebrateStartFrame + UI_RoguelikeCollectionPlateFx.MeetFrame) * FrameSeconds : ProgressStart + ProgressSeconds + rank * SheenGap;
            int gold = true == bFull && _t >= sheenStart + SheenSeconds ? 1 : 0;
            if (gold != lastHeaderGold[s])
            {
                lastHeaderGold[s] = gold;
                h.count.color = 1 == gold ? GoldColor : MutedColor;
            }

            int sheenX = int.MinValue;
            if (true == bFull && 0 <= rank && _t >= sheenStart && _t < sheenStart + SheenSeconds)
            {
                sheenX = RoundHalfUp(-SheenWidth + HeaderSheenSpan * Mathf.Clamp01((_t - sheenStart) / SheenSeconds));
            }

            if (sheenX != lastHeaderSheen[s])
            {
                lastHeaderSheen[s] = sheenX;
                if (null != h.sheenMask)
                {
                    bool bShow = int.MinValue != sheenX;
                    h.sheenMask.gameObject.SetActive(bShow);
                    if (true == bShow)
                    {
                        h.sheenMask.anchoredPosition = new Vector2(sheenX, 0f);
                        h.sheenContent.anchoredPosition = new Vector2(-sheenX, 0f);
                    }
                }
            }
        }
    }

    // // 칸 (E2 등장 · E4 뒤집힘 · E5 들림 · E9 반응)

    private int LiftOf(int _index, int _n)
    {
        float ease = OutCubic(Mathf.Clamp01((_n - liftFrame) * FrameSeconds / CursorMoveSeconds));
        if (_index == liftIndex) return RoundHalfUp(LiftPixels * ease);
        if (_index == prevLiftIndex) return RoundHalfUp(LiftPixels * (1f - ease));
        return 0;
    }

    private int BundleOf(int _index)
    {
        int reference = slotCount == BundleReferenceCount ? _index : Mathf.Min(BundleReferenceCount - 1, _index * BundleReferenceCount / Mathf.Max(1, slotCount));
        for (int k = 0; 8 > k; k++)
        {
            if (reference < BundleStarts[k + 1]) return k;
        }

        return 7;
    }

    private void RenderSlots(int _n, float _t, int _ev)
    {
        for (int i = 0; slotCount > i; i++)
        {
            UI_RoguelikeCollectionSlot slot = slots[i];
            float age = _t - (SlotStart + i * SlotGap);
            if (0f > age)
            {
                slot.SetAlpha(0f);
                continue;
            }

            float p = Mathf.Clamp01(age / SlotDrop);
            int dy = RoundHalfUp(-SlotDy * (1f - OutCubic(p)));
            int lift = LiftOf(i, _n);

            bool bFront = true;
            int width = UI_RoguelikeCollectionSlot.SlotSize;
            if (true == slotNew[i])
            {
                bFront = _n >= slotFlip[i] + NewSwapOffset;
                int step = _n - slotFlip[i];
                if (0 <= step && TotalFlipFrames > step) width = FlipWidths[step];
            }

            int popDy = 0;
            bool bWhite = false;
            float gold = 0f;
            int border = 0;                // 0 없음 · 1 밝은 금 · 2 짙은 금
            if (0 <= _ev) ReactE9(i, _ev, out popDy, out bWhite, out gold, out border);

            int offsetY = dy - lift + popDy;
            slot.SetFront(bFront);
            slot.SetFlipWidth(width);
            slot.SetOffsetY(offsetY);
            slot.SetAlpha(Mathf.Clamp01(p * 2f));

            bool bBack = false == bFront || false == items[i].bFound;
            slot.SetBackSpark(true == bBack && _n >= slotLand[i] && _n < slotLand[i] + 2);
            slot.SetNewBadge(true == slotNew[i] && _n >= slotFlip[i] + NewSwapOffset && false == slotSeen[i]);

            Color flash = Color.clear;
            if (true == bWhite) flash = Color.white;
            else if (0f < gold) flash = new Color(GoldColor.r / 255f, GoldColor.g / 255f, GoldColor.b / 255f, gold);

            Color outline = Color.clear;
            if (1 == border) outline = new Color(BorderGoldColor.r / 255f, BorderGoldColor.g / 255f, BorderGoldColor.b / 255f, 1f);
            else if (2 == border) outline = new Color(BorderDeepColor.r / 255f, BorderDeepColor.g / 255f, BorderDeepColor.b / 255f, 1f);

            slot.SetFlash(flash, outline, new Vector2(slotX[i], slotY[i] + offsetY));
        }
    }

    /// <summary>E9 칸 반응(프레임): 두근 박자마다 1px 내려앉음 / 타다다닥 박자마다 한 묶음이 흰색 2프레임 → 금 테두리 + 2px 튐 / 딱에 칸 전체 금빛.</summary>
    private void ReactE9(int _index, int _ev, out int _dy, out bool _bWhite, out float _gold, out int _border)
    {
        _dy = 0;
        _bWhite = false;
        _gold = 0f;
        _border = 0;

        for (int b = 0; UI_RoguelikeCollectionPlateFx.BeatCount > b; b++)
        {
            int a = _ev - UI_RoguelikeCollectionPlateFx.BeatFrames[b];
            if (0 <= a && PopFrames > a) _dy = 1;
        }

        int trace = _ev - (UI_RoguelikeCollectionPlateFx.TraceStartFrame + UI_RoguelikeCollectionPlateFx.StepFrames[BundleOf(_index)]);
        if (0 <= trace && ReactionFrames > trace)
        {
            _dy = RoundHalfUp(Track(TraceHop, trace));
            if (2 > trace) _bWhite = true;
            else _border = 6 > trace ? 1 : 2;
        }

        int meet = _ev - UI_RoguelikeCollectionPlateFx.MeetFrame;
        if (0 <= meet && ReactionFrames > meet)
        {
            _gold = Track(GoldAlphas, meet);
            if (6 > meet) _border = 1;
            else if (0 == _border) _border = 2;
        }
    }

    /// <summary>E9 창 안 어둡게: 알파 0.35까지 6프레임에 어두워졌다가 유지 시작(ev 84)부터 18프레임에 걷힌다.</summary>
    private void RenderPanelDim(int _ev)
    {
        if (null == panelDim) return;

        float alpha = 0f;
        if (0 <= _ev)
        {
            alpha = UI_RoguelikeCollectionPlateFx.DimAlpha * Mathf.Clamp01((_ev + 1) / (float)PlateDimFrames)
                * (1f - Mathf.Clamp01((_ev - UI_RoguelikeCollectionPlateFx.IdleFrame) / UI_RoguelikeCollectionPlateFx.IdleFadeFrames));
        }

        if (alpha == lastPanelDimAlpha) return;

        lastPanelDimAlpha = alpha;
        panelDim.enabled = 0f < alpha;
        panelDim.color = new Color(DimPanelColor.r / 255f, DimPanelColor.g / 255f, DimPanelColor.b / 255f, alpha);
    }

    // // E4 입자

    private static float NextRandom(ref uint _state)
    {
        unchecked
        {
            _state += 0x6D2B79F5u;
            uint t = _state;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return (float)((t ^ (t >> 14)) / 4294967296.0);
        }
    }

    private static Color32[] ParticleColorsOf(in RoguelikeCollectionViewData _data)
    {
        if (ECardKind.Prism == _data.kind) return PrismParticleColors;
        if (ECardKind.Ruby == _data.kind) return RubyParticleColors;
        if (true == _data.bMaxed) return MaxParticleColors;
        return CommonParticleColors;
    }

    private void SpawnBurst(int _index)
    {
        uint seed = unchecked((uint)(_index * 7919 + 13));
        int count = ParticleMin + Mathf.FloorToInt(NextRandom(ref seed) * (ParticleMax - ParticleMin + 1));
        RoguelikeCollectionViewData data = items[_index];
        Color32[] colors = ParticleColorsOf(in data);
        float cx = slotX[_index] + UI_RoguelikeCollectionSlot.SlotSize * 0.5f;
        float cy = slotY[_index] + UI_RoguelikeCollectionSlot.SlotSize * 0.5f - LiftOf(_index, frame);

        for (int i = 0; count > i && ParticleCapacity > particleCount; i++)
        {
            float angle = NextRandom(ref seed) * Mathf.PI * 2f;
            float speed = 1.0f + NextRandom(ref seed) * 1.6f;
            BurstParticle particle = new BurstParticle
            {
                x = cx,
                y = cy,
                vx = Mathf.Cos(angle) * speed,
                vy = Mathf.Sin(angle) * speed - 0.4f,
                age = 0,
                life = ParticleLife,
                size = 0 == i % 3 ? 2 : 1,
                color = colors[i % 3],
            };
            particles[particleCount] = particle;
            particleCount++;
        }
    }

    private void StepParticles()
    {
        int write = 0;
        for (int i = 0; particleCount > i; i++)
        {
            BurstParticle p = particles[i];
            p.age++;
            p.x += p.vx * 0.5f;
            p.y += p.vy * 0.5f;
            p.vy += 0.06f;
            if (p.age < p.life)
            {
                particles[write] = p;
                write++;
            }
        }

        particleCount = write;
    }

    private void ClearParticles()
    {
        particleCount = 0;
        if (true == bBurstDrawn && null != burstGraphic)
        {
            burstGraphic.ClearQuads();
            bBurstDrawn = false;
        }
    }

    private void RenderParticles()
    {
        if (null == burstGraphic) return;

        if (0 >= particleCount)
        {
            if (true == bBurstDrawn)
            {
                burstGraphic.ClearQuads();
                bBurstDrawn = false;
            }

            return;
        }

        bBurstDrawn = true;
        burstGraphic.BeginQuads();
        for (int i = 0; particleCount > i; i++)
        {
            BurstParticle p = particles[i];
            int x = RoundHalfUp(p.x);
            int y = RoundHalfUp(p.y);
            BarRect(burstGraphic, x, y, x + p.size, y + p.size, p.color);
        }

        burstGraphic.Commit();
    }

    // // E6 상세 패널

    private void RenderDetail(int _n, float _t)
    {
        if (null == detailGroup) return;

        float panelAlpha = Mathf.Clamp01((_t - SlotStart) / HeaderFadeSeconds);
        if (panelAlpha != detailGroup.alpha) detailGroup.alpha = panelAlpha;

        bool bVisible = DetailStartFrame <= _n && 0 < slotCount;
        if (bVisible != lastDetailVisible)
        {
            lastDetailVisible = bVisible;
            if (null != detailContent) detailContent.gameObject.SetActive(bVisible);
        }

        if (false == bVisible) return;

        int index = Mathf.Clamp(selectedIndex, 0, slotCount - 1);
        RoguelikeCollectionViewData data = items[index];
        bool bFront = true == data.bFound && (false == slotNew[index] || _n >= slotFlip[index] + NewSwapOffset);
        int key = index * 2 + (true == bFront ? 1 : 0);
        if (key != lastDetailKey)
        {
            lastDetailKey = key;
            lastDetailFrame = _n;
            lastDetailOffsetX = int.MinValue;
            lastDetailOffsetY = int.MinValue;
            lastDetailClip = -1;
            ApplyDetail(in data, bFront);
        }

        int age = _n - lastDetailFrame;

        // 스포트라이트 고리 숨쉬기 0 / -1 / 0 / +1(30프레임씩, 크기 변화 없음)
        int phase = _n % BreathFrames;
        int breath = phase < 30 ? 0 : (phase < 60 ? -1 : (phase < 90 ? 0 : 1));
        if (breath != lastDetailBreath && null != ringsRect)
        {
            lastDetailBreath = breath;
            ringsRect.anchoredPosition = new Vector2(ringsBasePosition.x, ringsBasePosition.y - breath);
        }

        int offsetX = 0;
        int offsetY = 0;
        if (true == bFront) offsetY = RoundHalfUp(Track(DetailFrontOffsets, age));
        else offsetX = RoundHalfUp(Track(DetailBackOffsets, age));

        if ((offsetX != lastDetailOffsetX || offsetY != lastDetailOffsetY) && null != detailCard)
        {
            lastDetailOffsetX = offsetX;
            lastDetailOffsetY = offsetY;
            detailCard.SetShakeOffset(offsetX);
            detailCard.SetFlipLift(-offsetY);
        }

        int clip = true == bFront ? RoundHalfUp(DetailMaskWidth * Mathf.Clamp01((age + 1) / (float)RevealFrames)) : DetailMaskWidth;
        if (clip != lastDetailClip && null != detailMask)
        {
            lastDetailClip = clip;
            detailMask.sizeDelta = new Vector2(clip, DetailMaskHeight);
        }
    }

    /// <summary>선택한 칸의 상세(카드 그림 · 종류 · 이름 · 설명 · MAX 표시)를 채운다. 앞면이 아니면 뒷면 카드와 "? ? ?"를 보인다.</summary>
    private void ApplyDetail(in RoguelikeCollectionViewData _data, bool _bFront)
    {
        if (null != detailCard)
        {
            detailCard.ResetMotion();
            if (true == _bFront)
            {
                ECardDisplayGrade grade = ECardDisplayGrade.Bronze;
                if (true == _data.bMaxed) grade = ECardDisplayGrade.Emerald;
                else if (ECardKind.Prism == _data.kind) grade = ECardDisplayGrade.Prism;
                else if (ECardKind.Ruby == _data.kind) grade = ECardDisplayGrade.Ruby;

                int level = true == _data.bMaxed ? DetailMaxLevel : 0;
                detailCard.SetCard(_data.icon, _data.kind, grade, level, DetailMaxLevel, ECardKind.Ruby == _data.kind);
                detailCard.SetFaceDown(false);
            }
            else
            {
                detailCard.SetFaceDown(true);
            }
        }

        string kindName = Localize(KindKey(_data.kind));
        if (true == _bFront)
        {
            SetDetailText(detailKind, kindName, KindColor(_data.kind), DetailKindTop);
            int nameExtra = SetDetailName(Localize(_data.nameKey), true == _data.bMaxed ? GoldColor : CreamColor);
            string formattedDesc = RoguelikeCardEffectText.FormatDescription(_data.card, 1, false, localizationManager, Localize(_data.descKey));
            SetDetailText(detailDesc, ApplyColorMarkers(formattedDesc), CreamColor, DetailDescTop + nameExtra);
        }
        else
        {
            SetDetailText(detailKind, kindName, DimTextColor, DetailKindTop);
            int nameExtra = SetDetailName(UnknownName, MutedColor);
            SetDetailText(detailDesc, unfoundString, DimTextColor, DetailDescTop + nameExtra);
        }

        bool bMaxBadge = true == _bFront && true == _data.bMaxed && (ECardKind.Active == _data.kind || ECardKind.Passive == _data.kind);
        if (null != detailMaxBadge)
        {
            detailMaxBadge.gameObject.SetActive(bMaxBadge);
            if (true == bMaxBadge && null != detailMaxBadge.sprite)
            {
                Vector2 size = detailMaxBadge.sprite.rect.size;
                RectTransform badgeRect = detailMaxBadge.rectTransform;
                badgeRect.sizeDelta = size;
                badgeRect.anchoredPosition = new Vector2(0f, -DetailBadgeTop);
                if (null != detailMaxLabel)
                {
                    detailMaxLabel.gameObject.SetActive(true);
                    float labelWidth = MeasureWidth(detailMaxLabel, detailMaxLabel.text);
                    PlaceText(detailMaxLabel, size.x + DetailBadgeGap, DetailBadgeLabelTop, labelWidth + TextWidthSlack);
                }
            }
            else if (null != detailMaxLabel)
            {
                detailMaxLabel.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>이름: 폭 182에서 줄바꿈한다(기획 최대 2줄). 두 번째 줄부터 늘어난 높이를 돌려줘 설명을 그만큼 아래로 민다.</summary>
    private int SetDetailName(string _value, Color32 _color)
    {
        if (null == detailName) return 0;

        detailName.text = _value;
        detailName.color = _color;
        PlaceText(detailName, 0f, DetailNameTop, DetailDescWidth);
        float lineHeight = detailName.rectTransform.sizeDelta.y;
        float height = Mathf.Max(lineHeight, Mathf.Ceil(detailName.GetPreferredValues(_value, DetailDescWidth, Unbounded).y));
        detailName.rectTransform.sizeDelta = new Vector2(DetailDescWidth, height);
        return Mathf.RoundToInt(height - lineHeight);
    }

    private void SetDetailText(TMP_Text _text, string _value, Color32 _color, int _top)
    {
        if (null == _text) return;

        _text.text = _value;
        _text.color = _color;
        float width = _text == detailDesc ? DetailDescWidth : Mathf.Min(DetailMaskWidth, MeasureWidth(_text, _value) + TextWidthSlack);
        PlaceText(_text, 0f, _top, width);
        if (_text == detailDesc)
        {
            // 설명은 줄바꿈이 있는 여러 줄이라 높이를 글자 크기에 맞춘다.
            float height = Mathf.Ceil(_text.GetPreferredValues(_value, DetailDescWidth, Unbounded).y);
            _text.rectTransform.sizeDelta = new Vector2(DetailDescWidth, height);
        }
    }

    /// <summary>설명 안의 색 마커 {+}...{/}(강화 구절, 금색) · {-}...{/}(약화 구절, 적색)를 리치텍스트 색으로 바꾼다. 마커가 없으면 원문 그대로다.</summary>
    private string ApplyColorMarkers(string _text)
    {
        if (string.IsNullOrEmpty(_text) || 0 > _text.IndexOf(MarkerStartChar)) return _text;

        markerBuilder.Clear();
        bool bOpen = false;
        int i = 0;
        while (i < _text.Length)
        {
            if (MarkerStartChar == _text[i])
            {
                if (0 == string.CompareOrdinal(_text, i, BenefitMarker, 0, BenefitMarker.Length))
                {
                    if (true == bOpen) markerBuilder.Append(ColorEndTag);
                    markerBuilder.Append(BenefitColorTag);
                    bOpen = true;
                    i += BenefitMarker.Length;
                    continue;
                }

                if (0 == string.CompareOrdinal(_text, i, DemeritMarker, 0, DemeritMarker.Length))
                {
                    if (true == bOpen) markerBuilder.Append(ColorEndTag);
                    markerBuilder.Append(DemeritColorTag);
                    bOpen = true;
                    i += DemeritMarker.Length;
                    continue;
                }

                if (0 == string.CompareOrdinal(_text, i, EndMarker, 0, EndMarker.Length))
                {
                    if (true == bOpen) markerBuilder.Append(ColorEndTag);
                    bOpen = false;
                    i += EndMarker.Length;
                    continue;
                }
            }

            markerBuilder.Append(_text[i]);
            i++;
        }

        if (true == bOpen) markerBuilder.Append(ColorEndTag);
        return markerBuilder.ToString();
    }

    // // 닫기 · 입력

    private void RequestClose()
    {
        if (false == IsVisible || true == bClosing) return;

        Hide();
    }

    private void OnDimClicked()
    {
        if (true == TrySkipCelebration()) return;

        RequestClose();
    }

    private void OnPanelClicked()
    {
        TrySkipCelebration();
    }

    private void OnSlotClicked(UI_RoguelikeCollectionSlot _slot)
    {
        TrySkipCelebration();
    }

    private void UnbindInput()
    {
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.UICancelEvent -= cancelHandler;
            inputManager.inputReader.ESCButtonPressedEvent -= escHandler;
            inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
        }

        if (null != localizationManager) localizationManager.OnLanguageChanged -= languageChangedHandler;
    }

    private void RefreshPadGuide()
    {
        bool bPad = true == IsGamepadMode;
        for (int i = 0; padOnlyItems.Length > i; i++)
        {
            if (null != padOnlyItems[i]) padOnlyItems[i].gameObject.SetActive(bPad);
        }
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        if (false == IsVisible || true == bClosing) return;

        RefreshPadGuide();

        if (EInputDeviceType.Gamepad == _device)
        {
            hoveredIndex = -1;
            RestoreSelection();
            return;
        }

        ReleaseSelection();
        HideCursor();
        bSelectionActive = false;
        SetLift(-1);
    }

    /// <summary>제목 · 칸 이름은 이 창이 번역하므로 새 언어로 다시 읽고 글자 폭에 의존하는 배치를 다시 잡는다(폰트 교체가 끝난 뒤 불린다).</summary>
    private void OnLanguageChanged()
    {
        if (false == IsVisible) return;

        ApplyStaticTexts();
        LayoutAll();
        InvalidateCaches();
        bDirty = true;
    }

    // // 포커스 · 선택

    private int IndexOfSlot(UI_RoguelikeCollectionSlot _slot)
    {
        for (int i = 0; slotCount > i; i++)
        {
            if (slots[i] == _slot) return i;
        }

        return -1;
    }

    private void OnSlotFocused(UI_RoguelikeCollectionSlot _slot, bool _bPointer)
    {
        if (true == bClosing) return;

        int index = IndexOfSlot(_slot);
        if (0 > index) return;

        if (true == _bPointer)
        {
            hoveredIndex = index;
            if (false == IsGamepadMode && CursorStartFrame <= frame) ApplySelection(index);
            return;
        }

        if (false == IsGamepadMode) return;

        ApplySelection(index);
    }

    private void OnSlotExited(UI_RoguelikeCollectionSlot _slot)
    {
        int index = IndexOfSlot(_slot);
        if (index != hoveredIndex) return;

        hoveredIndex = -1;
        if (true == IsGamepadMode || true == bClosing) return;

        HideCursor();
        bSelectionActive = false;
        SetLift(-1);
    }

    private void ApplySelection(int _index)
    {
        if (_index != selectedIndex || false == bSelectionActive) Sound.PlayUI(SoundID.ResultUIHover);

        selectedIndex = _index;
        bSelectionActive = true;
        SetLift(_index);
        if (CursorStartFrame <= frame) ShowCursor(_index);
        bDirty = true;
    }

    private void SetLift(int _index)
    {
        if (_index == liftIndex) return;

        prevLiftIndex = liftIndex;
        liftIndex = _index;
        liftFrame = frame;
        bDirty = true;
    }

    private void SelectSlot(int _index)
    {
        if (0 > _index || slotCount <= _index) return;

        selectedIndex = _index;
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && null != slots[_index].SelectButton && eventSystem.currentSelectedGameObject != slots[_index].SelectButton.gameObject)
        {
            eventSystem.SetSelectedGameObject(slots[_index].SelectButton.gameObject);
        }

        bSelectionActive = true;
        if (liftIndex != _index) SetLift(_index);
        if (CursorStartFrame <= frame) ShowCursor(_index);
    }

    /// <summary>패드 선택이 사라졌을 때 · 패드로 바뀔 때의 복구: 마지막 선택 칸.</summary>
    private void RestoreSelection()
    {
        SelectSlot(selectedIndex);
    }

    private void ReleaseSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (null == eventSystem || null == eventSystem.currentSelectedGameObject) return;
        if (false == eventSystem.currentSelectedGameObject.transform.IsChildOf(transform)) return;

        eventSystem.SetSelectedGameObject(null);
    }

    private void ShowCursor(int _index)
    {
        ICursorBoxUI cursor = viewCtx?.cursorBoxUI;
        if (null == cursor || 0 > _index || slotCount <= _index) return;

        RectTransform target = slots[_index].Rect;
        if (_index == cursorIndex && true == cursor.IsTarget(target)) return;

        cursorIndex = _index;
        cursor.Show(target, new Vector2(CursorSize, CursorSize), new Vector2(0f, CursorOffsetY), cursorMotion);
    }

    private void HideCursor()
    {
        cursorIndex = -1;
        viewCtx?.cursorBoxUI?.Hide();
    }

    // 유니티 라이프사이클 이벤트 함수

    public override void Update()
    {
        base.Update();

        if (false == IsVisible) return;

        accumulator += Mathf.Min(MaxCatchUpSeconds, Time.unscaledDeltaTime);
        bool bStepped = false;
        while (FrameSeconds <= accumulator)
        {
            accumulator -= FrameSeconds;
            StepFrame();
            bStepped = true;
        }

        if (true == bClosing && DimSeconds <= closeFrames * FrameSeconds)
        {
            base.Hide();
            return;
        }

        if (true == bStepped || true == bDirty) RenderFrame();

        if (true == bClosing) return;

        // 키보드/마우스 모드는 마우스 전용이다. 칸을 눌러도 UGUI가 그 대상을 선택하므로 선택이 생기면 풀어 방향키가 먹지 않게 한다.
        if (false == IsGamepadMode)
        {
            ReleaseSelection();
            return;
        }

        // 패드 모드: 선택 중이던 것이 꺼져 EventSystem 선택이 null이 되면 되돌린다. 이 창 밖의 UI가 선택을 쥔 동안에는 빼앗지 않는다(CanRestoreSelection).
        if (true == CanRestoreSelection(EventSystem.current)) RestoreSelection();
    }

    public override void OnDestroy()
    {
        UnbindInput();
        if (null != dimButton) dimButton.onClick.RemoveListener(OnDimClicked);
        if (null != panelButton) panelButton.onClick.RemoveListener(OnPanelClicked);
        ClosedEvent = null;
        base.OnDestroy();
    }
}
