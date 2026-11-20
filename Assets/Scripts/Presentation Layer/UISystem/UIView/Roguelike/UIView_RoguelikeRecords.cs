using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 순위표(런 기록 3탭) - 나이테 기록판. 기획서 3-6 - 최근 기록 / 클리어 랭킹(1~5위) / 언리밋 랭킹(1~5위).
/// 메인메뉴의 로그라이크 하위 메뉴 "기록"에서 연다. 계획서 Docs/Roguelike/_Work/RecordUI_Redesign/RecordUI_Implementation_Plan.md.
///
/// [구성] 어두운 딤 위 가운데 한 장(Panel_Book 360x282) - 판 · 탭 명판 "◀ 현재 탭 ▶" · 점 3개 · 뒤로 버튼은 이전과 같다.
///   본문(340x249, contentRoot)은 위쪽 스포트라이트(UI_RoguelikeRecordSpotlight 340x109, 최근 탭 65)와 아래 4px 간격의 순위 줄 5개(UI_RoguelikeRecordRow 340x24, 간격 4)다.
///   5줄이 본문 안에 다 들어가 세로 스크롤은 없다. 기록이 없는 자리는 빈칸 모양으로 두고 고를 수 없다.
///   랭킹 탭: 스포트라이트 아래칸에 선택한 기록의 카드 띠(가로 스크롤, 8칸 보임, 넘치면 양 끝 주황 화살표가 깜빡인다).
///   최근 기록 탭: 줄 대신 카드 보관함(UI_RoguelikeRecordCardGrid 8x4=32칸, 스포트라이트 아래 4px)을 보이고, 메달은 이번 판이 든 랭킹 순위(없으면 빈 그루터기)다.
///   카드 순서는 표시할 때만 액티브 -> 패시브 -> 계약 -> 프리즘(ECardKind 값 순, 같은 종류 안에서는 저장 순서)으로 나눈다. 저장 데이터는 건드리지 않는다.
///
/// [픽셀 규칙 - 계획서 1장] 모든 위치는 정수 픽셀, 크기 변화 · 회전 연출 없음, 그림은 물들이지 않고 스프라이트를 바꾼다.
///   탭 전환 슬라이드 · 줄 입장 · 카드 띠 칸 등장 · 판 흔들림은 매 프레임 Mathf.Round로 놓는다.
///
/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-7 · 3-8]
///   패드: 위 / 아래 = 순위 줄 이동과 동시에 스포트라이트 갱신(A 불필요). 1위 줄에서 위 = 카드 띠 첫 칸(보이는 첫 칸), 띠에서 좌 / 우 = 칸 이동, 아래 = 선택 줄로 복귀.
///     최근 기록 탭은 보관함 칸끼리 상하좌우. LB / RB = 탭(UITabShiftEvent, 키보드 PageUp/PageDown은 받지 않는다). B = 닫기.
///   마우스: 줄 클릭 = 선택, 호버 = 커서 상자 + 호버음(줄에 들어올 때 1번), 카드 띠 위 휠 = 한 칸, 명판 ◀ ▶ 클릭 = 탭, 딤 · 뒤로 버튼 = 닫기.
///   패드 포커스: 패드 모드로 열릴 때 · 패드로 바뀔 때만 첫 대상 선택, 선택이 null이 되면 복구, 마우스 모드로 바뀌면 선택 해제, 닫을 때 선택 비움.
///   닫기 = 딤 클릭 · 뒤로 버튼 · 패드 B(UICancelEvent). 키보드 Esc는 로그라이크 씬에서는 프리팹 bCloseableByESC(깊이 관리)가 닫고,
///   메인메뉴처럼 깊이 관리가 없는 곳에서 열 때는 SetStandaloneClose(true)로 ESC를 직접 받는다. 입력 모드 UI는 모달 공통(RoguelikeModalInput)이 건다.
/// </summary>
public class UIView_RoguelikeRecords : UIView_RoguelikeModalBase
{
    // 판 높이(탭 명판 · 점 3개 · 본문 250).
    public const float PanelHeight = 282f;

    private const int TabCount = 3;
    private const int MaxRows = 5;

    // 18개 언어 중 가장 긴 탭 이름으로 잰 명판 최소 폭(px, 시안 v3 측정: 가장 긴 탭 183 + 화살표 · 여백 54 + LB · RB 아이콘 자리 34).
    private const float SpecTabPlateWidth = 272f;
    private const float TabArrowWidth = 22f;
    // 화살표 안쪽에 붙는 LB · RB 아이콘 한쪽 자리(아이콘 16 + 간격 1).
    private const float TabKeyIconWidth = 17f;
    // 순위 줄(v2): 본문 위쪽에서 스포트라이트(109) + 간격 4 아래부터 높이 24 · 간격 4.
    private const float RowTop = 113f;
    private const float RowHeight = 24f;
    private const float RowGap = 4f;

    // 카드 띠(v2): 칸 36 + 간격 4 -> 폭 316(8칸 x 40 - 4)에 8칸이 딱 보인다.
    private const float StripHeight = 36f;
    private const float StripSlotStep = 40f;
    private const float StripSlotSize = UI_RoguelikeCardIconSlot.SlotSize;
    private const int StripKindCount = 4;
    private const int StripMaxCards = 32;
    // 칸 맞춤 · 자동 스크롤 미끄러짐(unscaled). 관성이 이 속도(px/s)보다 느려지면 가장 가까운 칸 경계로 맞춘다.
    private const float SlideSeconds = 0.12f;
    private const float StripSnapSpeed = 60f;
    private const float SlideEpsilon = 0.01f;
    // 커서 상자는 대상보다 사방 2px 크다.
    private const float CursorPadding = 4f;

    private const float DimFadeSeconds = 0.18f;
    private const float PanelEnterSeconds = 0.2f;
    private const float PanelEnterOffset = 6f;

    // 연출 타임라인(초, unscaled) - 시뮬레이션과 같은 값. 등장 기준점(introStart)은 열기 0.12초 뒤 · 탭 전환의 들어오기 시작.
    private const float OpenIntroDelay = 0.12f;
    private const float RowIntroDelay = 0.30f;
    private const float RowIntroStagger = 0.06f;
    private const float CursorReadyDelay = 0.56f;
    private const float StripPopDelay = 0.40f;
    private const float StripPopSelectionDelay = 0.12f;
    private const float StripPopStagger = 0.035f;
    private const float StripPopSeconds = 0.12f;
    private const float StripPopDrop = 4f;
    private const float StripArrowDelay = 0.6f;
    private const float StripArrowSelectionDelay = 0.2f;
    private const float StripArrowBlinkSeconds = 0.4f;
    private const float TabOutSeconds = 0.10f;
    private const float TabInSeconds = 0.16f;
    private const float TabSlideContent = 12f;
    private const float ShakeSeconds = 0.1f;

    /// <summary>칸 맞춤 · 자동 스크롤의 짧은 미끄러짐(OutCubic, unscaled 시간). 값 형식이라 할당이 없다.</summary>
    private struct ScrollSlide
    {
        public bool bActive;
        public float from;
        public float to;
        public float elapsed;

        public void Begin(float _from, float _to)
        {
            bActive = true;
            from = _from;
            to = _to;
            elapsed = 0f;
        }

        public void Cancel()
        {
            bActive = false;
        }

        /// <summary>_deltaTime만큼 진행한 현재 값. 끝에 닿으면 bActive가 꺼지고 도착점을 그대로 돌려준다.</summary>
        public float Advance(float _deltaTime)
        {
            elapsed += _deltaTime;
            float t = Mathf.Clamp01(elapsed / SlideSeconds);
            if (1f <= t)
            {
                bActive = false;
                return to;
            }

            float inverse = 1f - t;
            return from + (to - from) * (1f - inverse * inverse * inverse);
        }
    }

    private enum ETabPhase
    {
        None = 0,
        Out = 1,
        In = 2,
    }

    // 문구 키(RoguelikeUI.json)
    private static readonly string[] TabKeys = { "Roguelike_Records_TabRecent", "Roguelike_Records_TabClear", "Roguelike_Records_TabUnlimit" };
    private const string EmptyKey = "Roguelike_Records_Empty";

    // 탭 넘김 피드백 - 첫 실행 언어 설정 팝업(UI_InitialSetupPopup)의 언어 넘김과 같은 값이다.
    //   점: 현재 점만 흰색, 나머지 회청색. 5x5 커서가 새 점으로 0.12초 OutBack으로 미끄러진다.
    //   화살표: 누른 쪽 화살표가 3px 밀렸다 돌아온다(옵션 선택기와 같다).
    //   이름: 누른 방향에서 12px 정수 픽셀로 미끄러져 들어오며(0.14초 OutBack) 금색에서 원래 색으로(0.22초 OutQuad). 스케일 · 알파는 쓰지 않는다.
    private static readonly Color DotOnColor = Color.white;
    private static readonly Color DotOffColor = new Color(0.35f, 0.36f, 0.45f, 1f);
    private static readonly Color TabFlashColor = new Color(1f, 0.835f, 0.31f, 1f);
    private const float TabSlideDistance = 12f;
    private const float TabSlideSeconds = 0.14f;
    private const float TabColorSeconds = 0.22f;
    private const float DotCursorSeconds = 0.12f;
    // 옵션 선택기(UI_OptionSelector.PlayArrowNudge)와 같은 값: 누른 쪽 화살표가 바깥으로 3px 밀렸다가 0.1초 OutQuad로 돌아온다.
    private const float ArrowNudgeDistance = 3f;
    private const float ArrowNudgeSeconds = 0.1f;

    public event Action ClosedEvent;

    public RoguelikeRecordViewData Recent { get; private set; }
    protected readonly List<RoguelikeRecordViewData> clearRanking = new List<RoguelikeRecordViewData>(5);
    protected readonly List<RoguelikeRecordViewData> unlimitRanking = new List<RoguelikeRecordViewData>(5);


    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Records)
    [Header("화면")]
    [SerializeField] private CanvasGroup dimGroup;
    [SerializeField] private Button dimButton;
    [SerializeField] private RectTransform panelRoot;

    [Header("탭 명판")]
    [SerializeField] private RectTransform tabPlate;
    [SerializeField] private TMP_Text tabText;
    [SerializeField] private Button prevTabButton;
    [SerializeField] private Button nextTabButton;
    [SerializeField, Tooltip("이전 탭 버튼의 화살표 그림(옵션 화살표). 탭을 넘길 때 밀림 연출에 쓴다.")]
    private RectTransform prevArrowIcon;
    [SerializeField] private RectTransform nextArrowIcon;
    [SerializeField] private Image[] tabDots = new Image[TabCount];
    [SerializeField, Tooltip("현재 탭의 점 위에서 미끄러지는 5x5 커서(언어 설정 팝업과 같은 모양).")]
    private RectTransform tabDotCursor;

    [Header("본문")]
    [SerializeField, Tooltip("스포트라이트 · 순위 줄 · 보관함을 묶은 본문(340x249). 탭 전환 때 통째로 밀린다.")] private RectTransform contentRoot;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private UI_RoguelikeRecordSpotlight spotlight;
    [SerializeField] private RectTransform rowRoot;
    [SerializeField] private UI_RoguelikeRecordRow rowTemplate;
    [SerializeField, Tooltip("최근 기록 탭의 카드 보관함(8x4).")] private UI_RoguelikeRecordCardGrid cardGrid;
    [SerializeField] private UI_RoguelikeRecordFx fx;

    [Header("카드 띠")]
    [SerializeField, Tooltip("스포트라이트 아래칸의 카드 띠 묶음(가로 스크롤). 랭킹 탭에서 카드가 있을 때만 켠다.")] private RectTransform stripRoot;
    [SerializeField] private UI_RoguelikeStripScroll stripScroll;
    [SerializeField] private RectTransform stripViewport;
    [SerializeField] private RectTransform stripContent;
    [SerializeField, Tooltip("카드 띠 칸 견본(선택 대상 + CanvasGroup이 있는 UI_RoguelikeCardIconSlot). 꺼 둔다.")] private UI_RoguelikeCardIconSlot stripSlotTemplate;
    [SerializeField, Tooltip("띠 왼쪽에 더 있음을 알리는 주황 화살표(3x5).")] private Image stripArrowLeft;
    [SerializeField] private Image stripArrowRight;

    [Header("뒤로 · 패드 아이콘")]
    [SerializeField, Tooltip("판 아래 왼쪽의 뒤로 버튼(옵션 BTN_Close와 같은 UI_OptionButton). 마우스 클릭용 - 패드는 B 단축키.")]
    private UI_OptionButton backButton;
    [SerializeField, Tooltip("패드 모드에서만 켜는 아이콘 묶음(명판 LB · RB, 뒤로 버튼의 B). 키보드 모드에서는 끈다.")]
    private RectTransform[] padOnlyItems = new RectTransform[0];
    [SerializeField] private UI_KeyboardImage[] keyGuideImages = new UI_KeyboardImage[0];

    //내부 의존성
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private readonly List<UI_RoguelikeRecordRow> rows = new List<UI_RoguelikeRecordRow>(MaxRows);
    private readonly List<UI_RoguelikeCardIconSlot> stripSlots = new List<UI_RoguelikeCardIconSlot>(StripMaxCards);
    private readonly List<CanvasGroup> stripSlotGroups = new List<CanvasGroup>(StripMaxCards);
    private readonly List<RoguelikeOwnedCardViewData> stripCards = new List<RoguelikeOwnedCardViewData>(StripMaxCards);
    private CursorMotionSettings rowCursorMotion;
    private CursorMotionSettings slotCursorMotion;

    //상태 변수
    private int currentTab;
    private int selectedIndex;
    private bool bStandaloneClose;
    private Vector2 panelBasePosition;
    private bool bPanelBaseCaptured;
    private Vector2 contentBasePosition;
    private bool bContentBaseCaptured;
    private Vector2 tabTextBasePosition;
    private Color tabTextBaseColor;
    private bool bTabTextBaseCaptured;
    private Vector2 prevArrowBasePosition;
    private Vector2 nextArrowBasePosition;
    private bool bArrowBaseCaptured;
    private int stripCount;
    private int stripPopBase;
    private ScrollSlide stripSlide;
    private float introStart;
    private ETabPhase tabPhase;
    private float tabPhaseStart;
    private int tabDirection;
    private float shakeStart = -1f;
    private bool bShaking;
    private UI_RoguelikeRecordRow focusedRow;
    private UI_RoguelikeCardIconSlot focusedSlot;
    private UI_RoguelikeRecordRow hoveredRow;
    private RectTransform cursorTarget;
    private CursorMotionSettings cursorMotion;
    private bool bCursorClipped;
    private readonly Vector3[] targetCorners = new Vector3[4];
    private readonly Vector3[] clipCorners = new Vector3[4];

    // 캐싱 델리게이트
    private Action<int> tabShiftHandler;
    private Action cancelHandler;
    private Action escHandler;
    private Action languageChangedHandler;
    private Action<EInputDeviceType> deviceChangedHandler;
    private Action<UI_RoguelikeRecordRow> rowClickedHandler;
    private Action<UI_RoguelikeRecordRow, bool> rowFocusedHandler;
    private Action<UI_RoguelikeRecordRow> rowExitedHandler;
    private Action<UI_RoguelikeCardIconSlot> slotSelectedHandler;
    private Action<UI_RoguelikeCardIconSlot, MoveDirection> slotMovedHandler;
    private Action<UI_RoguelikeCardIconSlot> gridSlotSelectedHandler;
    private Action contentSwappedHandler;
    private Action stampLandedHandler;
    private UnityAction<Vector2> stripScrolledHandler;
    private Action<int> stripWheelHandler;

    private bool IsGamepadMode => null != inputManager && true == inputManager.IsGamepadMode;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = viewCtx?.localizationManager;
        inputManager = viewCtx?.inputManager;
        CaptureContentBase();

        tabShiftHandler = OnTabShift;
        cancelHandler = OnCancelPressed;
        escHandler = OnCancelPressed;
        rowClickedHandler = OnRowClicked;
        rowFocusedHandler = OnRowFocused;
        rowExitedHandler = OnRowExited;
        slotSelectedHandler = OnSlotSelected;
        slotMovedHandler = OnSlotMoved;
        gridSlotSelectedHandler = OnGridSlotSelected;
        contentSwappedHandler = OnSpotlightContentSwapped;
        stampLandedHandler = OnStampLanded;
        stripScrolledHandler = OnStripScrolled;
        stripWheelHandler = OnStripWheelStep;
        rowCursorMotion = CursorMotionSettings.Still;
        slotCursorMotion = CursorMotionSettings.Card;
        languageChangedHandler = OnLanguageChanged;
        deviceChangedHandler = OnDeviceChanged;

        if (null != dimButton)
        {
            dimButton.onClick.RemoveListener(RequestClose);
            dimButton.onClick.AddListener(RequestClose);
        }
        if (null != prevTabButton)
        {
            prevTabButton.onClick.RemoveListener(ShowPrevTab);
            prevTabButton.onClick.AddListener(ShowPrevTab);
        }
        if (null != nextTabButton)
        {
            nextTabButton.onClick.RemoveListener(ShowNextTab);
            nextTabButton.onClick.AddListener(ShowNextTab);
        }

        for (int i = 0; keyGuideImages.Length > i; i++)
        {
            if (null != keyGuideImages[i]) keyGuideImages[i].Initialize(inputManager);
        }

        if (null != backButton) backButton.Initialize(RequestClose, SoundID.MainMenuDot01, SoundID.None);

        if (null != rowTemplate) rowTemplate.gameObject.SetActive(false);
        if (null != stripSlotTemplate) stripSlotTemplate.gameObject.SetActive(false);
        if (null != stripRoot) stripRoot.gameObject.SetActive(false);
        if (null != stripScroll)
        {
            stripScroll.onValueChanged.RemoveListener(stripScrolledHandler);
            stripScroll.onValueChanged.AddListener(stripScrolledHandler);
            stripScroll.WheelStepEvent -= stripWheelHandler;
            stripScroll.WheelStepEvent += stripWheelHandler;
        }
        if (null != spotlight)
        {
            spotlight.ContentSwappedEvent -= contentSwappedHandler;
            spotlight.ContentSwappedEvent += contentSwappedHandler;
            spotlight.StampLandedEvent -= stampLandedHandler;
            spotlight.StampLandedEvent += stampLandedHandler;
        }
        if (null != cardGrid)
        {
            cardGrid.Initialize();
            cardGrid.SlotSelectedEvent -= gridSlotSelectedHandler;
            cardGrid.SlotSelectedEvent += gridSlotSelectedHandler;
        }
    }

    /// <summary>깊이 관리(ESC 닫기)가 없는 화면(메인메뉴)에서 열 때 켠다. 켜면 키보드 Esc를 이 창이 직접 받아 닫는다.</summary>
    public void SetStandaloneClose(bool _bStandalone)
    {
        bStandaloneClose = _bStandalone;
    }

    public virtual void OpenRecords(in RoguelikeRecordViewData _recent, IReadOnlyList<RoguelikeRecordViewData> _clearRanking,
        IReadOnlyList<RoguelikeRecordViewData> _unlimitRanking)
    {
        Recent = _recent;
        Copy(_clearRanking, clearRanking);
        Copy(_unlimitRanking, unlimitRanking);

        Show();
        Sound.PlayUI(SoundID.ResultUIOpen);
        OnRecordsPresented();
    }

    /// <summary>첫 탭(최근 기록)을 그리고 등장 연출을 시작한다.</summary>
    protected virtual void OnRecordsPresented()
    {
        currentTab = 0;
        tabPhase = ETabPhase.None;
        ResetHover();
        ApplyStaticTexts();
        RefreshTabGuide();
        RefreshTab();
        introStart = Time.unscaledTime + OpenIntroDelay;
        if (null != spotlight) spotlight.ShiftStart(OpenIntroDelay);
        ResetTabFeedback();
        SnapDotCursor();
        PlayEnter();
        if (true == IsGamepadMode) SelectFirstTarget();
    }

    protected override void OnShowContent()
    {
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.UITabShiftEvent -= tabShiftHandler;
            inputManager.inputReader.UITabShiftEvent += tabShiftHandler;
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
        ResetStripState();
        ReleaseSelection();
        ResetHover();
        HideCursor();
        tabPhase = ETabPhase.None;
        StopShake();
        if (null != spotlight) spotlight.Stop();
        if (null != fx) fx.Clear();
        if (null != dimGroup) dimGroup.DOKill();
        if (null != panelRoot) panelRoot.DOKill();
        ResetTabFeedback();
        if (null != tabDotCursor) tabDotCursor.DOKill();
        if (null != prevArrowIcon) prevArrowIcon.DOKill();
        if (null != nextArrowIcon) nextArrowIcon.DOKill();
        ClosedEvent?.Invoke();
    }

    // // 탭

    private void OnTabShift(int _direction)
    {
        // 키보드 PageUp/PageDown 탭 전환은 두지 않는다(키보드·마우스 모드는 ◀ ▶ 클릭). 패드 LB/RB만 받는다.
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        if (0 > _direction) ShowPrevTab();
        else if (0 < _direction) ShowNextTab();
    }

    private void ShowPrevTab()
    {
        BeginTabSwitch(-1);
    }

    private void ShowNextTab()
    {
        BeginTabSwitch(1);
    }

    /// <summary>
    /// 탭 전환: 명판 · 점 피드백은 바로, 본문은 누른 방향으로 12px 밀려 나가며(0.1초) 사라진 뒤 새 탭으로 바뀌어 반대쪽에서 들어온다(0.16초).
    /// 밀려 나가는 중에 또 누르면 그 자리에서 다음 탭으로 이어서 바꾼다.
    /// </summary>
    private void BeginTabSwitch(int _direction)
    {
        currentTab = (currentTab + TabCount + _direction) % TabCount;
        tabDirection = 0 < _direction ? 1 : -1;
        ResetHover();
        HideCursor();
        ReleaseSelection();
        if (null != spotlight) spotlight.Stop();
        if (null != fx) fx.Clear();
        PlayTabSwitchFeedback(tabDirection);
        if (null != tabText) tabText.text = Localize(TabKeys[currentTab]);
        RefreshDots();
        LayoutTabPlate();
        Sound.PlayUI(SoundID.OptionClick);

        if (ETabPhase.Out != tabPhase) tabPhaseStart = Time.unscaledTime;
        tabPhase = ETabPhase.Out;
    }

    private void AdvanceTabSwitch()
    {
        if (ETabPhase.None == tabPhase || null == contentRoot) return;

        float elapsed = Time.unscaledTime - tabPhaseStart;
        if (ETabPhase.Out == tabPhase)
        {
            float e = Mathf.Clamp01(elapsed / TabOutSeconds);
            SetContentOffset(-tabDirection * TabSlideContent * e, 1f - e);
            if (1f > e) return;

            RefreshTab();
            // RefreshTab이 본문을 제자리(알파 1)에 두므로 같은 프레임에 들어오기 시작 위치로 돌려 한 프레임 깜빡임을 막는다.
            SetContentOffset(tabDirection * TabSlideContent, 0f);
            tabPhase = ETabPhase.In;
            tabPhaseStart = Time.unscaledTime;
            introStart = tabPhaseStart;
            if (true == IsGamepadMode) SelectFirstTarget();
            return;
        }

        float p = Mathf.Clamp01(elapsed / TabInSeconds);
        float eased = 1f - (1f - p) * (1f - p) * (1f - p);
        SetContentOffset(tabDirection * TabSlideContent * (1f - eased), eased);
        if (1f <= p) tabPhase = ETabPhase.None;
    }

    /// <summary>본문을 제자리(프리팹 위치, 판 x 10)에서 _x만큼 밀어 놓는다. 기준 위치를 더하지 않으면 본문이 판 테두리에 붙고 오른쪽이 16px 빈다.</summary>
    private void SetContentOffset(float _x, float _alpha)
    {
        CaptureContentBase();
        if (null != contentRoot) contentRoot.anchoredPosition = new Vector2(contentBasePosition.x + Mathf.Round(_x), contentBasePosition.y);
        if (null != contentGroup) contentGroup.alpha = _alpha;
    }

    /// <summary>본문 제자리를 처음 한 번 기억한다(Initialize 없이 RefreshTab을 부르는 QA 툴도 이 경로로 들어온다).</summary>
    private void CaptureContentBase()
    {
        if (true == bContentBaseCaptured || null == contentRoot) return;

        contentBasePosition = contentRoot.anchoredPosition;
        bContentBaseCaptured = true;
    }

    /// <summary>
    /// 현재 탭 내용을 처음부터 다시 그린다(등장 연출 시작). 언어 변경 · Localization QA 툴(리플렉션)도 이 이름을 부른다 - 이름을 바꾸지 않는다.
    /// 에디터에서 플레이하지 않고 부르면(QA 툴) 연출 없이 끝 상태로 둔다.
    /// </summary>
    private void RefreshTab()
    {
        if (null != tabText) tabText.text = Localize(TabKeys[currentTab]);
        RefreshDots();
        LayoutTabPlate();
        SetContentOffset(0f, 1f);

        selectedIndex = 0;
        bool bRecentTab = 0 == currentTab;
        List<RoguelikeRecordViewData> source = 1 == currentTab ? clearRanking : unlimitRanking;
        int count = true == bRecentTab ? 0 : Mathf.Min(MaxRows, source.Count);
        int unlimitBest = 0;
        if (2 == currentTab)
        {
            for (int i = 0; count > i; i++) unlimitBest = Mathf.Max(unlimitBest, source[i].unlimitWave);
        }

        long recentTicks = true == Recent.bValid ? Recent.recordedAtUtcTicks : long.MinValue;
        for (int i = 0; MaxRows > i; i++)
        {
            UI_RoguelikeRecordRow row = GetRow(i);
            row.gameObject.SetActive(false == bRecentTab);
            if (true == bRecentTab) continue;

            if (i < count)
            {
                RoguelikeRecordViewData record = source[i];
                row.SetRecord(i, in record, currentTab, recentTicks == record.recordedAtUtcTicks, unlimitBest);
            }
            else row.SetEmpty(i);
            row.SetOpenBackground(i == selectedIndex);
            row.RenderIntro(-1f, 0f);
        }
        LayoutRows();

        RoguelikeRecordViewData shown = default;
        bool bValid = false;
        if (true == bRecentTab && true == Recent.bValid)
        {
            shown = Recent;
            bValid = true;
        }
        else if (false == bRecentTab && 0 < count)
        {
            shown = source[0];
            bValid = true;
        }

        int spotlightRank = true == bRecentTab ? FindRecentRank() : 0;
        if (null != spotlight) spotlight.PlayIntro(in shown, bValid, spotlightRank, currentTab, Localize(EmptyKey));

        if (null != cardGrid)
        {
            cardGrid.gameObject.SetActive(bRecentTab);
            if (true == bRecentTab)
            {
                SortCards(true == bValid ? shown.cards : null);
                cardGrid.SetCards(stripCards);
                cardGrid.RenderIntro(-1f);
            }
        }

        ResetStripState();
        if (false == bRecentTab && true == bValid) OpenStrip(shown.cards);

        introStart = Time.unscaledTime;
        if (false == Application.isPlaying) SnapIntro();
    }

    /// <summary>최근 기록이 든 랭킹 순위(0부터): 클리어 랭킹 먼저, 없으면 언리밋 랭킹, 둘 다 없으면 -1(빈 그루터기). 기록 시각(틱)으로 같은 판을 찾는다.</summary>
    private int FindRecentRank()
    {
        if (false == Recent.bValid) return -1;

        long ticks = Recent.recordedAtUtcTicks;
        int clearCount = Mathf.Min(MaxRows, clearRanking.Count);
        for (int i = 0; clearCount > i; i++)
        {
            if (ticks == clearRanking[i].recordedAtUtcTicks) return i;
        }

        int unlimitCount = Mathf.Min(MaxRows, unlimitRanking.Count);
        for (int i = 0; unlimitCount > i; i++)
        {
            if (ticks == unlimitRanking[i].recordedAtUtcTicks) return i;
        }
        return -1;
    }

    private void RefreshDots()
    {
        for (int i = 0; tabDots.Length > i; i++)
        {
            if (null != tabDots[i]) tabDots[i].color = i == currentTab ? DotOnColor : DotOffColor;
        }
    }

    /// <summary>등장 연출 없이 끝 상태로 둔다(언어 변경 · QA 툴).</summary>
    private void SnapIntro()
    {
        introStart = Time.unscaledTime - 99f;
        if (null != spotlight) spotlight.SnapToEnd();
        for (int i = 0; rows.Count > i; i++)
        {
            if (true == rows[i].gameObject.activeSelf) rows[i].SnapIntro(Time.unscaledTime);
        }
        if (null != cardGrid && true == cardGrid.gameObject.activeSelf) cardGrid.SnapIntro();
        RenderStripIntro(99f, false);
    }

    // // 탭 넘김 피드백 (언어 설정 팝업과 같은 연출)

    private void CaptureTabTextBase()
    {
        if (true == bTabTextBaseCaptured || null == tabText) return;
        tabTextBasePosition = tabText.rectTransform.anchoredPosition;
        tabTextBaseColor = tabText.color;
        bTabTextBaseCaptured = true;
    }

    private void CaptureArrowBase()
    {
        if (true == bArrowBaseCaptured) return;
        if (null != prevArrowIcon) prevArrowBasePosition = prevArrowIcon.anchoredPosition;
        if (null != nextArrowIcon) nextArrowBasePosition = nextArrowIcon.anchoredPosition;
        bArrowBaseCaptured = true;
    }

    private void ResetTabFeedback()
    {
        CaptureTabTextBase();
        CaptureArrowBase();
        if (null != prevArrowIcon)
        {
            prevArrowIcon.DOKill();
            prevArrowIcon.anchoredPosition = prevArrowBasePosition;
        }
        if (null != nextArrowIcon)
        {
            nextArrowIcon.DOKill();
            nextArrowIcon.anchoredPosition = nextArrowBasePosition;
        }
        if (null == tabText) return;

        tabText.rectTransform.DOKill();
        tabText.DOKill();
        tabText.rectTransform.anchoredPosition = tabTextBasePosition;
        tabText.color = tabTextBaseColor;
    }

    /// <summary>탭 이름은 누른 방향에서 정수 픽셀로 미끄러져 들어오며 금색에서 원래 색으로 돌아오고, 점 커서는 새 점으로 미끄러진다.</summary>
    private void PlayTabSwitchFeedback(int _direction)
    {
        ResetTabFeedback();

        // 누른 쪽 화살표가 바깥으로 밀렸다가 돌아온다(옵션 선택기와 같은 반응). 정수 픽셀, unscaled.
        RectTransform arrow = 0 < _direction ? nextArrowIcon : prevArrowIcon;
        if (null != arrow)
        {
            Vector2 arrowBase = 0 < _direction ? nextArrowBasePosition : prevArrowBasePosition;
            float outward = 0 < _direction ? 1f : -1f;
            arrow.anchoredPosition = new Vector2(arrowBase.x + outward * ArrowNudgeDistance, arrowBase.y);
            arrow.DOAnchorPosX(arrowBase.x, ArrowNudgeSeconds, true).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        if (null != tabText)
        {
            float dir = 0 < _direction ? 1f : -1f;
            tabText.rectTransform.anchoredPosition = new Vector2(tabTextBasePosition.x + dir * TabSlideDistance, tabTextBasePosition.y);
            tabText.rectTransform.DOAnchorPosX(tabTextBasePosition.x, TabSlideSeconds, true).SetEase(Ease.OutBack).SetUpdate(true);

            Color from = TabFlashColor;
            from.a = tabTextBaseColor.a;
            tabText.color = from;
            tabText.DOColor(tabTextBaseColor, TabColorSeconds).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // 커서: 새 점의 자식으로 옮기되 월드 위치를 유지해 이전 자리에서 출발하고, 로컬 (0, 0)으로 미끄러진다.
        if (true == HasDotCursorTarget())
        {
            tabDotCursor.DOKill();
            tabDotCursor.SetParent(tabDots[currentTab].rectTransform, true);
            tabDotCursor.DOAnchorPos(Vector2.zero, DotCursorSeconds, true).SetEase(Ease.OutBack).SetUpdate(true);
        }
    }

    private bool HasDotCursorTarget()
    {
        return null != tabDotCursor && tabDots.Length > currentTab && null != tabDots[currentTab];
    }

    /// <summary>커서를 현재 점에 바로 붙인다(연출 없이). 점 중심이 .5 픽셀이라 5x5 커서도 같은 중심에 놓여 가장자리가 정수 픽셀에 떨어진다.</summary>
    private void SnapDotCursor()
    {
        if (false == HasDotCursorTarget()) return;

        tabDotCursor.DOKill();
        tabDotCursor.SetParent(tabDots[currentTab].rectTransform, false);
        tabDotCursor.anchoredPosition = Vector2.zero;
    }

    private UI_RoguelikeRecordRow GetRow(int _index)
    {
        while (rows.Count <= _index && null != rowTemplate && null != rowRoot)
        {
            UI_RoguelikeRecordRow row = Instantiate(rowTemplate, rowRoot);
            row.name = "Row" + rows.Count;
            row.Initialize();
            row.ClickedEvent += rowClickedHandler;
            row.FocusedEvent += rowFocusedHandler;
            row.ExitedEvent += rowExitedHandler;
            rows.Add(row);
        }
        return rows[_index];
    }

    // // 배치 (정수 픽셀)

    private void LayoutTabPlate()
    {
        if (null == tabPlate || null == tabText) return;

        float textWidth = Mathf.Ceil(tabText.GetPreferredValues(tabText.text, 10000f, 10000f).x);
        float width = Mathf.Max(SpecTabPlateWidth, textWidth + (TabArrowWidth + TabKeyIconWidth) * 2f + 10f);
        int even = Mathf.CeilToInt(width);
        if (0 != even % 2) even++;
        tabPlate.sizeDelta = new Vector2(even, tabPlate.sizeDelta.y);
    }

    /// <summary>순위 줄 5개를 본문 위쪽 94부터 28 + 3 간격으로 놓고, 기록이 있는 줄끼리 위 / 아래 Explicit 탐색으로 잇는다(1위 줄 위 = 카드 띠).</summary>
    private void LayoutRows()
    {
        if (null == rowRoot) return;

        float width = Mathf.Round(rowRoot.rect.width);
        UI_RoguelikeRecordRow previous = null;
        for (int i = 0; rows.Count > i; i++)
        {
            RectTransform rect = rows[i].Rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -(RowTop + i * (RowHeight + RowGap)));

            if (false == rows[i].gameObject.activeSelf || false == rows[i].HasRecord) continue;

            SetVerticalNavigation(previous, rows[i]);
            previous = rows[i];
        }

        if (null != previous) SetVerticalNavigation(previous, null);
        LinkTopRowToStrip();
    }

    private static void SetVerticalNavigation(UI_RoguelikeRecordRow _upper, UI_RoguelikeRecordRow _lower)
    {
        if (null != _upper && null != _upper.SelectButton)
        {
            Navigation navigation = _upper.SelectButton.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnDown = null != _lower ? _lower.SelectButton : null;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            _upper.SelectButton.navigation = navigation;
        }

        if (null != _lower && null != _lower.SelectButton)
        {
            Navigation navigation = _lower.SelectButton.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = null != _upper ? _upper.SelectButton : null;
            navigation.selectOnDown = null;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            _lower.SelectButton.navigation = navigation;
        }
    }

    /// <summary>맨 위 기록 줄에서 위를 누르면 카드 띠의 보이는 첫 칸으로 간다(띠가 없으면 아무 데도 가지 않는다).</summary>
    private void LinkTopRowToStrip()
    {
        UI_RoguelikeRecordRow top = null;
        for (int i = 0; rows.Count > i; i++)
        {
            if (true == rows[i].gameObject.activeSelf && true == rows[i].HasRecord)
            {
                top = rows[i];
                break;
            }
        }
        if (null == top || null == top.SelectButton) return;

        Navigation navigation = top.SelectButton.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        int visible = Mathf.Clamp(Mathf.RoundToInt(GetStripScroll() / StripSlotStep), 0, Mathf.Max(0, stripCount - 1));
        navigation.selectOnUp = 0 < stripCount && null != stripRoot && true == stripRoot.gameObject.activeSelf ? stripSlots[visible].SelectTarget : null;
        top.SelectButton.navigation = navigation;
    }

    private void CapturePanelBase()
    {
        if (true == bPanelBaseCaptured || null == panelRoot) return;

        panelBasePosition = panelRoot.anchoredPosition;
        bPanelBaseCaptured = true;
    }

    private void PlayEnter()
    {
        if (null != dimGroup)
        {
            dimGroup.DOKill();
            dimGroup.alpha = 0f;
            dimGroup.DOFade(1f, DimFadeSeconds).SetUpdate(true);
        }

        if (null != panelRoot)
        {
            CapturePanelBase();
            StopShake();
            panelRoot.DOKill();
            panelRoot.anchoredPosition = panelBasePosition + new Vector2(0f, -PanelEnterOffset);
            panelRoot.DOAnchorPos(panelBasePosition, PanelEnterSeconds, true).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    // // 판 흔들림 (결과 도장)

    private void OnStampLanded()
    {
        if (null == panelRoot) return;

        CapturePanelBase();
        shakeStart = Time.unscaledTime;
        bShaking = true;
    }

    /// <summary>도장이 꽂힌 뒤 0.1초 동안 판을 1px 범위에서 정수로 흔들고, 끝나면 원래 자리로 돌린다.</summary>
    private void AdvanceShake()
    {
        if (false == bShaking || null == panelRoot) return;

        float elapsed = Time.unscaledTime - shakeStart;
        if (ShakeSeconds <= elapsed)
        {
            StopShake();
            return;
        }

        float x = Mathf.Round(Mathf.Sin(elapsed * 90f));
        float y = Mathf.Round(Mathf.Cos(elapsed * 70f));
        panelRoot.anchoredPosition = panelBasePosition + new Vector2(x, y);
    }

    private void StopShake()
    {
        if (false == bShaking) return;

        bShaking = false;
        if (null != panelRoot && true == bPanelBaseCaptured) panelRoot.anchoredPosition = panelBasePosition;
    }

    // // 닫기 · 문구

    /// <summary>딤 클릭 · 뒤로 버튼: 창을 닫는다.</summary>
    private void RequestClose()
    {
        if (false == IsVisible) return;

        Hide();
    }

    /// <summary>
    /// 패드 B(UICancelEvent) · 메인메뉴의 Esc. 창을 바로 닫는다.
    /// 깊이 관리(UIDepthController.TryCloseTopView)의 Hide()가 같은 프레임에 와도 두 번째는 IsVisible이 이미 꺼져 있어 한 번만 닫힌다.
    /// </summary>
    private void OnCancelPressed()
    {
        if (false == IsVisible) return;

        RequestClose();
    }

    /// <summary>
    /// 깊이 관리(bCloseableByESC)의 ESC · 패드 B 경로는 UIDepthController.TryCloseTopView가 이 Hide()를 바로 부른다.
    /// 닫힘 효과음은 여기서 낸다(깊이 관리 경로가 RequestClose를 거치지 않기 때문). 이미 닫혀 있으면 소리 없이 base.Hide()가 무시한다.
    /// </summary>
    public override void Hide()
    {
        if (true == IsVisible) Sound.PlayUI(SoundID.MainClick);
        base.Hide();
    }

    /// <summary>언어에 따라 바뀌는 고정 문구. 빈 기록 안내는 스포트라이트가 RefreshTab에서 받는다. QA 툴(리플렉션)이 이 이름을 부른다.</summary>
    private void ApplyStaticTexts()
    {
        if (null != tabText) tabText.text = Localize(TabKeys[currentTab]);
    }

    /// <summary>명판 LB · RB와 뒤로 버튼 B 아이콘은 패드 모드에서만 보인다(키보드는 ◀ ▶ 클릭 · Esc). 판 · 명판 크기는 그대로 둔다.</summary>
    private void RefreshTabGuide()
    {
        bool bPad = null != inputManager && true == inputManager.IsGamepadMode;
        for (int i = 0; padOnlyItems.Length > i; i++)
        {
            if (null != padOnlyItems[i]) padOnlyItems[i].gameObject.SetActive(bPad);
        }
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        if (false == IsVisible) return;
        RefreshTabGuide();

        if (EInputDeviceType.Gamepad == _device)
        {
            ResetHover();
            RestoreSelection();
            return;
        }

        ReleaseSelection();
        HideCursor();
    }

    /// <summary>언어가 바뀌면 문구와 내용을 다시 그리되 등장 연출은 다시 하지 않는다.</summary>
    private void OnLanguageChanged()
    {
        if (false == IsVisible) return;

        int keepSelected = selectedIndex;
        ApplyStaticTexts();
        RefreshTab();
        SnapIntro();
        if (0 != currentTab && 0 < keepSelected) SelectRow(keepSelected, false);
    }

    private string Localize(string _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
    }

    private void UnbindInput()
    {
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.UITabShiftEvent -= tabShiftHandler;
            inputManager.inputReader.UICancelEvent -= cancelHandler;
            inputManager.inputReader.ESCButtonPressedEvent -= escHandler;
            inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
        }

        if (null != localizationManager) localizationManager.OnLanguageChanged -= languageChangedHandler;
    }

    private static void Copy(IReadOnlyList<RoguelikeRecordViewData> _from, List<RoguelikeRecordViewData> _to)
    {
        _to.Clear();
        if (null == _from) return;

        for (int i = 0; i < _from.Count; i++) _to.Add(_from[i]);
    }

    // // 선택 (순위 줄 -> 스포트라이트)

    private void OnRowClicked(UI_RoguelikeRecordRow _row)
    {
        int index = rows.IndexOf(_row);
        if (0 > index || false == _row.HasRecord) return;

        SelectRow(index, true);
    }

    /// <summary>
    /// _index번 줄을 고른다: 줄 바탕을 바꾸고 스포트라이트가 덧문을 닫았다 열며 새 기록을 보인다. 카드 띠는 덧문이 닫힌 순간(OnSpotlightContentSwapped) 새 기록으로 채운다.
    /// _bAnimate가 거짓이면(언어 변경 복원) 연출 없이 바로 바꾼다.
    /// </summary>
    private void SelectRow(int _index, bool _bAnimate)
    {
        if (0 == currentTab || 0 > _index || rows.Count <= _index || false == rows[_index].HasRecord) return;
        if (_index == selectedIndex && true == _bAnimate) return;

        selectedIndex = _index;
        for (int i = 0; rows.Count > i; i++) rows[i].SetOpenBackground(i == selectedIndex);

        List<RoguelikeRecordViewData> source = 1 == currentTab ? clearRanking : unlimitRanking;
        if (source.Count <= _index || null == spotlight) return;

        RoguelikeRecordViewData record = source[_index];
        if (true == _bAnimate)
        {
            spotlight.PlaySelection(in record, _index, currentTab);
            return;
        }

        spotlight.PlayIntro(in record, true, _index, currentTab, Localize(EmptyKey));
        spotlight.SnapToEnd();
        ResetStripState();
        OpenStrip(record.cards);
        RenderStripIntro(99f, false);
    }

    /// <summary>스포트라이트 덧문이 다 닫혔다: 카드 띠를 새로 고른 기록으로 바꾼다(띠 칸은 덧문이 열리며 다시 떨어진다).</summary>
    private void OnSpotlightContentSwapped()
    {
        if (0 == currentTab || rows.Count <= selectedIndex) return;

        bool bFocusInStrip = null != focusedSlot;
        ResetStripState();
        OpenStrip(rows[selectedIndex].Cards);
        LinkTopRowToStrip();
        if (true == bFocusInStrip && true == IsGamepadMode) FocusRow(rows[selectedIndex]);
    }

    // // 카드 띠

    /// <summary>카드 띠를 채우고 켠다(카드가 0장이면 끈 채로 둔다). 스크롤은 맨 앞에서 시작한다.</summary>
    private void OpenStrip(IReadOnlyList<RoguelikeOwnedCardViewData> _cards)
    {
        if (null == stripRoot || null == stripContent || null == stripSlotTemplate) return;

        FillStrip(_cards);
        stripRoot.gameObject.SetActive(0 < stripCount);
        SetStripScroll(0f);
        stripPopBase = 0;
        RefreshStripArrows(false);
    }

    /// <summary>창이 닫히거나 탭 · 선택이 바뀔 때 띠 · 포커스 · 스크롤 상태를 처음으로 돌린다.</summary>
    private void ResetStripState()
    {
        stripCount = 0;
        focusedSlot = null;
        stripSlide.Cancel();
        if (null != stripScroll) stripScroll.StopMovement();
        if (null != stripRoot) stripRoot.gameObject.SetActive(false);
        if (null != stripContent) SetStripScroll(0f);
        if (null != stripArrowLeft) stripArrowLeft.gameObject.SetActive(false);
        if (null != stripArrowRight) stripArrowRight.gameObject.SetActive(false);
    }

    /// <summary>_cards를 종류 순(액티브 -> 패시브 -> 계약 -> 프리즘, 같은 종류 안에서는 저장 순서)으로 stripCards에 나눠 담는다.</summary>
    private void SortCards(IReadOnlyList<RoguelikeOwnedCardViewData> _cards)
    {
        stripCards.Clear();
        int total = null != _cards ? _cards.Count : 0;
        for (int kind = 0; StripKindCount > kind; kind++)
        {
            for (int i = 0; total > i; i++)
            {
                if (kind == (int)_cards[i].kind) stripCards.Add(_cards[i]);
            }
        }

        // 4종 밖의 값(데이터 오류)도 빠지지 않게 뒤에 붙인다.
        for (int i = 0; total > i; i++)
        {
            int kind = (int)_cards[i].kind;
            if (0 > kind || StripKindCount <= kind) stripCards.Add(_cards[i]);
        }
    }

    /// <summary>
    /// 그 판의 카드 전부를 종류 순으로 띠 칸에 채우고 칸 · 내용 크기를 정한다.
    /// 칸은 풀로 돌려 쓴다(모자랄 때만 만들고 남는 칸은 끈다).
    /// </summary>
    private void FillStrip(IReadOnlyList<RoguelikeOwnedCardViewData> _cards)
    {
        SortCards(_cards);
        stripCount = stripCards.Count;

        while (stripSlots.Count < stripCount)
        {
            UI_RoguelikeCardIconSlot slot = Instantiate(stripSlotTemplate, stripContent);
            slot.name = "StripSlot" + stripSlots.Count;
            slot.SelectedEvent += slotSelectedHandler;
            slot.MovedEvent += slotMovedHandler;
            stripSlots.Add(slot);
            stripSlotGroups.Add(slot.GetComponent<CanvasGroup>());
        }

        for (int i = 0; stripSlots.Count > i; i++)
        {
            UI_RoguelikeCardIconSlot slot = stripSlots[i];
            bool bUsed = i < stripCount;
            slot.gameObject.SetActive(bUsed);
            if (false == bUsed) continue;

            RoguelikeOwnedCardViewData data = stripCards[i];
            slot.SetCard(in data);

            RectTransform rect = slot.Rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(StripSlotSize, StripSlotSize);
            rect.anchoredPosition = new Vector2(i * StripSlotStep, 0f);

            // 좌 / 우로만 잇는다(Explicit). 위 / 아래는 MovedEvent로 받는다(아래 = 선택 줄).
            if (null == slot.SelectTarget) continue;

            Navigation navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = 0 < i ? stripSlots[i - 1].SelectTarget : null,
                selectOnRight = i + 1 < stripCount ? stripSlots[i + 1].SelectTarget : null,
            };
            slot.SelectTarget.navigation = navigation;
        }

        float viewportWidth = null != stripViewport ? Mathf.Round(stripViewport.rect.width) : 0f;
        float contentWidth = 0 < stripCount ? stripCount * StripSlotStep - (StripSlotStep - StripSlotSize) : 0f;
        stripContent.anchorMin = new Vector2(0f, 1f);
        stripContent.anchorMax = new Vector2(0f, 1f);
        stripContent.pivot = new Vector2(0f, 1f);
        stripContent.sizeDelta = new Vector2(Mathf.Max(contentWidth, viewportWidth), StripHeight);
        stripContent.anchoredPosition = Vector2.zero;

        focusedSlot = null;
        stripSlide.Cancel();
        if (null != stripScroll) stripScroll.StopMovement();
    }

    /// <summary>
    /// 띠 칸 등장(스포트라이트 시간 기준): 처음 보이는 칸부터 0.035초 간격으로 4px 위에서 OutBack으로 떨어지며 나타난다(위치만 정수로, 크기 변화 없음).
    /// 선택 변경 중 덧문이 닫히기 전에는 이전 칸을 그대로 둔다.
    /// </summary>
    private void RenderStripIntro(float _localTime, bool _bSelection)
    {
        if (0 >= stripCount) return;

        float delay = true == _bSelection ? StripPopSelectionDelay : StripPopDelay;
        for (int i = 0; stripCount > i; i++)
        {
            int order = Mathf.Max(0, i - stripPopBase);
            float p = Mathf.Clamp01((_localTime - delay - order * StripPopStagger) / StripPopSeconds);
            CanvasGroup group = stripSlotGroups.Count > i ? stripSlotGroups[i] : null;
            if (null != group) group.alpha = Mathf.Clamp01(p * 2f);

            RectTransform rect = stripSlots[i].Rect;
            rect.anchoredPosition = new Vector2(i * StripSlotStep, Mathf.Round((1f - OutBack(p)) * StripPopDrop));
        }
    }

    /// <summary>띠 양 끝의 주황 화살표: 그쪽으로 칸이 더 있을 때만 0.4초마다 깜빡인다. 등장 연출이 칸을 다 내린 뒤부터 보인다.</summary>
    private void RefreshStripArrows(bool _bReady)
    {
        bool bActive = true == _bReady && 0 < stripCount && null != stripRoot && true == stripRoot.gameObject.activeSelf;
        bool bBlinkOn = 0 == Mathf.FloorToInt(Time.unscaledTime / StripArrowBlinkSeconds) % 2;
        float scroll = GetStripScroll();
        float max = GetStripMax();
        SetActive(stripArrowLeft, bActive && bBlinkOn && SlideEpsilon < scroll);
        SetActive(stripArrowRight, bActive && bBlinkOn && scroll < max - SlideEpsilon);
    }

    private static void SetActive(Component _component, bool _bActive)
    {
        if (null != _component && _component.gameObject.activeSelf != _bActive) _component.gameObject.SetActive(_bActive);
    }

    /// <summary>카드 띠 스크롤이 바뀌었다(끌기 · 관성 · 휠 · 미끄러짐). 위치 반올림은 하지 않는다(맞춤은 멈춘 뒤 TrySnapStrip이 한다).</summary>
    private void OnStripScrolled(Vector2 _normalized)
    {
        LinkTopRowToStrip();
    }

    /// <summary>휠 한 칸(_direction -1 / +1): 가장 가까운 다음 칸 경계로 미끄러진다. 미끄러지는 중이면 그 도착점에서 이어서 한 칸.</summary>
    private void OnStripWheelStep(int _direction)
    {
        if (0 >= stripCount || null == stripContent) return;

        float max = GetStripMax();
        float basePosition = true == stripSlide.bActive ? stripSlide.to : GetStripScroll();
        StartStripSlide(basePosition, StepStripStop(basePosition, _direction, max));
    }

    private void SetStripScroll(float _scroll)
    {
        if (null == stripContent) return;

        stripContent.anchoredPosition = new Vector2(-_scroll, 0f);
    }

    private float GetStripScroll()
    {
        return null != stripContent ? -stripContent.anchoredPosition.x : 0f;
    }

    private float GetStripMax()
    {
        if (null == stripContent || null == stripViewport) return 0f;

        return Mathf.Max(0f, stripContent.rect.width - stripViewport.rect.width);
    }

    /// <summary>패드로 고른 칸이 띠 밖에 있으면 보이도록 칸 경계로 미끄러져 스크롤한다.</summary>
    private void ScrollToSlot(UI_RoguelikeCardIconSlot _slot)
    {
        if (null == stripContent || null == stripViewport) return;

        int index = stripSlots.IndexOf(_slot);
        if (0 > index) return;

        float left = index * StripSlotStep;
        float right = left + StripSlotSize;
        float viewportWidth = stripViewport.rect.width;
        float max = GetStripMax();
        float basePosition = true == stripSlide.bActive ? stripSlide.to : GetStripScroll();
        float target = basePosition;

        if (left < basePosition - SlideEpsilon) target = Mathf.Min(left, max);
        else if (basePosition + viewportWidth < right - SlideEpsilon) target = Mathf.Min(Mathf.Ceil((right - viewportWidth - SlideEpsilon) / StripSlotStep) * StripSlotStep, max);

        StartStripSlide(basePosition, target);
    }

    /// <summary>카드 띠를 _to로 미끄러뜨린다. _basePosition(진행 중인 미끄러짐의 도착점 또는 현재 위치)과 같으면 아무것도 하지 않는다.</summary>
    private void StartStripSlide(float _basePosition, float _to)
    {
        if (Mathf.Abs(_to - _basePosition) < SlideEpsilon) return;

        if (null != stripScroll) stripScroll.StopMovement();
        stripSlide.Begin(GetStripScroll(), _to);
    }

    /// <summary>스크롤 위치 _scroll에서 가장 가까운 칸 경계(40px 단위) 또는 끝. 끝은 칸 경계가 아니어도 도착점이 된다.</summary>
    private static float NearestStripStop(float _scroll, float _max)
    {
        float lower = Mathf.Floor(_scroll / StripSlotStep) * StripSlotStep;
        float upper = Mathf.Min(lower + StripSlotStep, _max);
        float best = lower;
        if (Mathf.Abs(upper - _scroll) < Mathf.Abs(best - _scroll)) best = upper;
        if (Mathf.Abs(_max - _scroll) < Mathf.Abs(best - _scroll)) best = _max;
        return Mathf.Min(best, _max);
    }

    /// <summary>_base에서 _direction(-1 / +1)쪽으로 한 칸 옆의 칸 경계. 끝을 넘지 않는다.</summary>
    private static float StepStripStop(float _base, int _direction, float _max)
    {
        if (0 < _direction) return Mathf.Min((Mathf.Floor((_base + SlideEpsilon) / StripSlotStep) + 1f) * StripSlotStep, _max);

        return Mathf.Max((Mathf.Ceil((_base - SlideEpsilon) / StripSlotStep) - 1f) * StripSlotStep, 0f);
    }

    /// <summary>
    /// 띠를 잡고 있지 않고 관성이 충분히 느려졌으면(범위 밖이면 기다린다 - Clamped라 평소에는 없다) 가장 가까운 칸 경계로 미끄러져 맞춘다.
    /// 칸 경계는 40px 정수라 멈춘 띠는 항상 정수 픽셀에 놓인다.
    /// </summary>
    private void TrySnapStrip()
    {
        if (null == stripScroll || null == stripContent) return;
        if (true == stripScroll.IsDragging) return;

        float max = GetStripMax();
        if (SlideEpsilon >= max) return;

        float scroll = GetStripScroll();
        if (-SlideEpsilon > scroll || scroll > max + SlideEpsilon) return;
        if (StripSnapSpeed < Mathf.Abs(stripScroll.velocity.x)) return;

        float target = NearestStripStop(scroll, max);
        if (SlideEpsilon > Mathf.Abs(target - scroll))
        {
            if (target != scroll) SetStripScroll(target);
            return;
        }

        StartStripSlide(scroll, target);
    }

    /// <summary>매 프레임(unscaled): 띠 미끄러짐을 진행하고, 멈춘 뒤 칸에 맞춘다. 끌기 · 관성 중에는 위치를 건드리지 않아 포인터를 1:1로 따라간다.</summary>
    private void AdvanceScroll()
    {
        if (0 >= stripCount) return;

        if (true == stripSlide.bActive) SetStripScroll(stripSlide.Advance(Time.unscaledDeltaTime));
        else TrySnapStrip();
    }

    // // 포커스 · 선택

    /// <summary>
    /// 줄에 마우스가 올라왔거나(_bPointer) 패드 선택이 옮겨 왔다.
    /// 마우스: 마우스 모드에서만 커서 상자 + 호버음(줄에 들어올 때 1번). 패드 모드에서는 포인터 아래로 줄이 지나가도 커서를 빼앗지 않는다.
    /// 패드: 그 줄을 고르고(스포트라이트 갱신) 커서를 올린다.
    /// </summary>
    private void OnRowFocused(UI_RoguelikeRecordRow _row, bool _bPointer)
    {
        if (true == _bPointer)
        {
            if (true == IsGamepadMode || _row == hoveredRow) return;

            hoveredRow = _row;
            Sound.PlayUI(SoundID.ResultUIHover);
            ShowCursor(_row.Rect, rowCursorMotion);
            return;
        }

        if (false == IsGamepadMode) return;

        ApplyRowFocus(_row);
        SelectRow(rows.IndexOf(_row), true);
    }

    /// <summary>마우스가 줄에서 나갔다: 커서를 숨긴다(다시 들어오면 호버음이 난다).</summary>
    private void OnRowExited(UI_RoguelikeRecordRow _row)
    {
        if (_row != hoveredRow) return;

        hoveredRow = null;
        if (false == IsGamepadMode) HideCursor();
    }

    /// <summary>마우스 호버 기록을 비운다(창 열기 · 창 닫기 · 탭 전환 · 패드로 바뀔 때).</summary>
    private void ResetHover()
    {
        hoveredRow = null;
    }

    private void OnSlotSelected(UI_RoguelikeCardIconSlot _slot)
    {
        if (false == IsGamepadMode) return;

        ApplyStripFocus(_slot);
    }

    /// <summary>띠 칸에서 아래를 누르면 선택 줄로 돌아간다(위는 아무 일 없음). 좌 / 우는 Explicit 탐색이 처리한다.</summary>
    private void OnSlotMoved(UI_RoguelikeCardIconSlot _slot, MoveDirection _direction)
    {
        if (false == IsGamepadMode || MoveDirection.Down != _direction) return;
        if (rows.Count <= selectedIndex || false == rows[selectedIndex].HasRecord) return;

        Sound.PlayUI(SoundID.ResultUIHover);
        FocusRow(rows[selectedIndex]);
    }

    private void OnGridSlotSelected(UI_RoguelikeCardIconSlot _slot)
    {
        if (false == IsGamepadMode) return;

        if (_slot != focusedSlot) Sound.PlayUI(SoundID.ResultUIHover);
        focusedSlot = _slot;
        focusedRow = null;
        ShowCursor(_slot.Rect, slotCursorMotion);
    }

    private void ApplyRowFocus(UI_RoguelikeRecordRow _row)
    {
        if (_row != focusedRow) Sound.PlayUI(SoundID.ResultUIHover);
        focusedRow = _row;
        focusedSlot = null;
        ShowCursor(_row.Rect, rowCursorMotion);
    }

    private void ApplyStripFocus(UI_RoguelikeCardIconSlot _slot)
    {
        if (_slot != focusedSlot) Sound.PlayUI(SoundID.ResultUIHover);
        focusedSlot = _slot;
        focusedRow = null;
        ScrollToSlot(_slot);
        ShowCursor(_slot.Rect, slotCursorMotion);
    }

    /// <summary>줄을 선택하고 커서를 올린다(이미 같은 줄이 선택되어 있으면 커서만).</summary>
    private void FocusRow(UI_RoguelikeRecordRow _row)
    {
        if (null == _row) return;

        focusedRow = _row;
        focusedSlot = null;
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && eventSystem.currentSelectedGameObject != _row.gameObject) eventSystem.SetSelectedGameObject(_row.gameObject);
        ShowCursor(_row.Rect, rowCursorMotion);
    }

    private void FocusSlot(UI_RoguelikeCardIconSlot _slot)
    {
        if (null == _slot || null == _slot.SelectTarget) return;

        focusedSlot = _slot;
        focusedRow = null;
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && eventSystem.currentSelectedGameObject != _slot.gameObject) eventSystem.SetSelectedGameObject(_slot.gameObject);
        ShowCursor(_slot.Rect, slotCursorMotion);
    }

    /// <summary>패드 첫 대상: 랭킹 탭은 선택 줄(없으면 첫 기록 줄), 최근 기록 탭은 보관함 첫 칸.</summary>
    private void SelectFirstTarget()
    {
        if (0 == currentTab)
        {
            if (null != cardGrid) FocusSlot(cardGrid.GetSlot(0));
            return;
        }

        if (rows.Count > selectedIndex && true == rows[selectedIndex].gameObject.activeSelf && true == rows[selectedIndex].HasRecord)
        {
            FocusRow(rows[selectedIndex]);
            return;
        }

        for (int i = 0; rows.Count > i; i++)
        {
            if (false == rows[i].gameObject.activeSelf || false == rows[i].HasRecord) continue;

            FocusRow(rows[i]);
            return;
        }
    }

    /// <summary>
    /// 패드 선택이 사라졌을 때(가이드 3-8 "가장 흔한 버그") · 패드로 바뀔 때의 복구: 마지막 띠 칸 · 보관함 칸이 살아 있으면 그 칸, 아니면 마지막 줄, 없으면 첫 대상.
    /// </summary>
    private void RestoreSelection()
    {
        if (ETabPhase.Out == tabPhase) return;

        if (null != focusedSlot && true == focusedSlot.gameObject.activeInHierarchy)
        {
            FocusSlot(focusedSlot);
            return;
        }

        if (null != focusedRow && true == focusedRow.gameObject.activeInHierarchy && true == focusedRow.HasRecord)
        {
            FocusRow(focusedRow);
            return;
        }

        SelectFirstTarget();
    }

    /// <summary>EventSystem 선택이 이 창 안의 것이면 푼다. 마우스 모드에서 줄 · 칸을 눌러 생긴 선택이 방향키 이동으로 이어지지 않게 한다.</summary>
    private void ReleaseSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (null == eventSystem || null == eventSystem.currentSelectedGameObject) return;
        if (false == eventSystem.currentSelectedGameObject.transform.IsChildOf(transform)) return;

        eventSystem.SetSelectedGameObject(null);
    }

    private void ShowCursor(RectTransform _target, CursorMotionSettings _motion)
    {
        ICursorBoxUI cursor = viewCtx?.cursorBoxUI;
        if (null == cursor || null == _target) return;

        cursorTarget = _target;
        cursorMotion = _motion;

        // 대상이 잘려 있거나(띠 밖) 줄이 아직 들어오는 중이면 띄우지 않고, RefreshCursorClip이 온전해질 때 띄운다.
        if (false == IsCursorReady(_target))
        {
            cursor.Hide();
            bCursorClipped = true;
            return;
        }

        bCursorClipped = false;
        if (true == cursor.IsTarget(_target)) return;

        cursor.Show(_target, _target.rect.size + new Vector2(CursorPadding, CursorPadding), Vector2.zero, _motion);
    }

    private void HideCursor()
    {
        cursorTarget = null;
        bCursorClipped = false;
        viewCtx?.cursorBoxUI?.Hide();
    }

    /// <summary>커서를 띄워도 되는지: 탭이 바뀌는 중이 아니고, 첫 줄이 다 들어왔고(열기 · 탭 전환 0.56초 뒤), 띠 칸이면 띠 뷰포트 안에 온전히 보일 때.</summary>
    private bool IsCursorReady(RectTransform _target)
    {
        if (ETabPhase.None != tabPhase || CursorReadyDelay > Time.unscaledTime - introStart) return false;
        if (null == stripViewport || false == _target.IsChildOf(stripViewport)) return true;

        float epsilon = 0.5f * Mathf.Abs(stripViewport.lossyScale.x);
        _target.GetWorldCorners(targetCorners);
        stripViewport.GetWorldCorners(clipCorners);
        return targetCorners[0].x >= clipCorners[0].x - epsilon && targetCorners[2].x <= clipCorners[2].x + epsilon;
    }

    /// <summary>띠 스크롤로 커서 대상이 잘리면 커서를 숨기고, 다시 온전해지면(또는 등장이 끝나면) 띄운다.</summary>
    private void RefreshCursorClip()
    {
        if (null == cursorTarget) return;

        ICursorBoxUI cursor = viewCtx?.cursorBoxUI;
        if (null == cursor) return;

        if (false == cursorTarget.gameObject.activeInHierarchy)
        {
            cursorTarget = null;
            bCursorClipped = false;
            cursor.Hide();
            return;
        }

        bool bReady = IsCursorReady(cursorTarget);
        if (true == bCursorClipped)
        {
            if (false == bReady) return;

            bCursorClipped = false;
            cursor.Show(cursorTarget, cursorTarget.rect.size + new Vector2(CursorPadding, CursorPadding), Vector2.zero, cursorMotion);
        }
        else if (false == bReady && true == cursor.IsTarget(cursorTarget))
        {
            cursor.Hide();
            bCursorClipped = true;
        }
    }

    /// <summary>등장 연출 한 프레임: 순위 줄 입장 · 보관함 · 카드 띠 칸 등장 · 띠 화살표.</summary>
    private void AdvanceIntro()
    {
        if (ETabPhase.Out == tabPhase) return;

        float now = Time.unscaledTime;
        float local = now - introStart;
        if (0 != currentTab)
        {
            for (int i = 0; rows.Count > i; i++)
            {
                if (true == rows[i].gameObject.activeSelf) rows[i].RenderIntro(local - RowIntroDelay - i * RowIntroStagger, now);
            }
        }

        if (null == spotlight) return;

        float spotTime = spotlight.LocalTime;
        bool bSelection = spotlight.IsSelectionPlay;
        if (0 == currentTab)
        {
            if (null != cardGrid && true == cardGrid.gameObject.activeSelf) cardGrid.RenderIntro(spotTime);
            return;
        }

        if (true == spotlight.IsBeforeSwap) return;

        RenderStripIntro(spotTime, bSelection);
        RefreshStripArrows(spotTime >= (true == bSelection ? StripArrowSelectionDelay : StripArrowDelay));
    }

    private static float OutBack(float _t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = _t - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    // // 유니티 라이프사이클

    public override void Update()
    {
        base.Update();

        if (false == IsVisible) return;

        AdvanceTabSwitch();
        AdvanceIntro();
        AdvanceScroll();
        AdvanceShake();
        RefreshCursorClip();

        // 키보드/마우스 모드는 마우스 전용이다. 줄 · 칸을 눌러도 UGUI가 그 대상을 선택하므로 선택이 생기면 풀어 방향키가 먹지 않게 한다.
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
        if (null != dimGroup) dimGroup.DOKill();
        if (null != panelRoot) panelRoot.DOKill();
        if (null != tabText) { tabText.rectTransform.DOKill(); tabText.DOKill(); }
        if (null != tabDotCursor) tabDotCursor.DOKill();
        if (null != prevArrowIcon) prevArrowIcon.DOKill();
        if (null != nextArrowIcon) nextArrowIcon.DOKill();
        if (null != stripScroll)
        {
            if (null != stripScrolledHandler) stripScroll.onValueChanged.RemoveListener(stripScrolledHandler);
            stripScroll.WheelStepEvent -= stripWheelHandler;
        }
        if (null != spotlight)
        {
            spotlight.ContentSwappedEvent -= contentSwappedHandler;
            spotlight.StampLandedEvent -= stampLandedHandler;
        }
        if (null != cardGrid) cardGrid.SlotSelectedEvent -= gridSlotSelectedHandler;
        if (null != dimButton) dimButton.onClick.RemoveListener(RequestClose);
        if (null != prevTabButton) prevTabButton.onClick.RemoveListener(ShowPrevTab);
        if (null != nextTabButton) nextTabButton.onClick.RemoveListener(ShowNextTab);
        ClosedEvent = null;
        base.OnDestroy();
    }
}
