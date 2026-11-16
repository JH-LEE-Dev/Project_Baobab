using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 순위표(런 기록 3탭). 기획서 3-6 - 최근 기록 / 클리어 랭킹(1~5위) / 언리밋 랭킹(1~5위).
/// 메인메뉴의 로그라이크 하위 메뉴 "기록"에서 연다(로비 폐지 - LobbyRemoval_AppLayer_Request.md). 로그라이크 씬에서도 같은 프리팹이 그대로 동작한다.
///
/// [구성 - 시안 v3] 어두운 딤 위 가운데 한 장(Panel_Book):
///   위쪽 가장자리의 탭 명판 "◀ 현재 탭 ▶" + 아래 점 3개와 점 커서(첫 실행 언어 설정 팝업과 같은 넘김 연출). 탭 이름이 언어마다 길이가 크게 달라 세 개를 나란히 두지 않는다.
///   패드 모드에서는 명판 화살표 바로 안쪽에 LB(◀ 옆) · RB(▶ 옆) 아이콘이 붙는다(16x16, 간격 1). 키보드 모드에서는 숨긴다.
///   명판 폭은 18개 언어 중 가장 긴 탭 이름 기준(SpecTabPlateWidth, 아이콘 자리 포함)과 현재 언어 폭 중 큰 값이라 탭을 넘기거나 장치를 바꿔도 크기가 흔들리지 않는다.
///   본문은 UI_RoguelikeRecordRow 최대 5줄(최근 기록은 1줄). 기록이 없으면 안내 문구. 하단 키 안내 줄은 없다.
///   뒤로 버튼(BTN_Back)은 옵션 창 BTN_Close와 같은 모양(32x32, UI_OptionButton)으로 판 아래 테두리 바깥, 판 왼쪽 끝에 놓는다. 패드 모드에서만 버튼 오른쪽에 B 아이콘이 뜬다.
///
/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-7] 탭 전환 = 패드 LB/RB(UITabShiftEvent - 키보드 PageUp/PageDown은 받지 않는다) 또는 명판의 ◀ ▶ 클릭.
///   패드 전용 아이콘(padOnlyItems: 명판 LB · RB, 뒤로 버튼의 B)은 패드 모드에서만 켠다. 뒤로 버튼은 마우스 클릭용이고 패드는 B 단축키를 쓴다(패드 선택 없음).
///   닫기 = 딤 클릭 · 뒤로 버튼 · 패드 B(UICancelEvent). 키보드 Esc는 로그라이크 씬에서는 프리팹 bCloseableByESC(깊이 관리)가 닫고,
///   메인메뉴처럼 깊이 관리가 없는 곳에서 열 때는 SetStandaloneClose(true)로 ESC를 직접 받는다. 입력 모드 UI는 모달 공통(RoguelikeModalInput)이 건다.
///
/// [카드 띠 - 계획표 7장 · 10장] 줄(UI_RoguelikeRecordRow)을 클릭하거나 패드 A로 고르면 그 줄 바로 아래에 그 판의 카드 전부가 든 띠 한 줄(폭 302, 높이 36, 8칸 보임)이 열린다.
///   한 번에 한 줄만 열리고, 아래 줄들이 띠 높이 + 간격(40, 카드가 8장을 넘으면 가로 스크롤바 자리 6 추가)만큼 내려간다. 판 크기와 위치는 늘 그대로다(360x282).
///   카드가 0장인 줄은 열리지 않는다. 클리어 · 언리밋 랭킹 탭은 들어오면 1위 줄의 띠가 열려 있고(고정 아님, 본문 스크롤 맨 위, 패드 커서는 1위 줄) 다시 누르면 닫힌다. 최근 기록 탭(1줄)은 탭에 들어오면 띠가 항상 열려 있고(bStripPinned) 닫을 수 없다: 줄을 눌러도 닫히지 않고, 패드 A는 커서를 띠로 보내며 띠에서 B는 커서만 줄로 돌려준다(줄에서 B는 창 닫기).
///   띠가 어느 줄의 것인지는 열린 줄의 바탕을 금색 테두리(Panel_Info_Gold)로 바꾸고, 줄 왼쪽 아래에서 띠 왼쪽으로 꺾이는 주황(#FF9D2E) 2px ㄴ자 연결선(단색 Image 2개)을 그려 보인다.
///   띠는 줄보다 14px 들여 놓는다(폭 302로 8칸이 딱 보이고 칸이 더 있으면 가로로 스크롤된다). 카드 순서는 표시할 때만 액티브 -> 패시브 -> 계약 -> 프리즘(ECardKind 값 순, 같은 종류 안에서는 저장 순서)으로 나눈다. 저장 데이터는 건드리지 않는다.
///   본문(340x250 마스크)은 세로 ScrollRect다: 내용이 250을 넘을 때만(5줄 탭에서 띠를 열었을 때) 오른쪽 여백에 세로 스크롤바가 뜨고 스크롤된다.
///   띠를 열면 열린 줄 + 띠가 보이도록 짧게 미끄러져 자동 스크롤하고, 패드로 줄 · 칸을 옮기면 고른 대상이 보이게 따라간다. 띠를 닫으면 맨 위로 돌아간다.
///   띠는 가로 ScrollRect(UI_RoguelikeStripScroll)다: 마우스 왼쪽 버튼으로 잡고 끌어 스크롤하고(세로로 끌면 본문으로 넘어간다), 관성이 있고 끝에서 더 밀리지 않는다(Clamped).
///   손을 떼거나 관성이 느려지면 가장 가까운 칸 경계(38px 단위, 끝은 끝)로 0.12초 미끄러져 맞는다. 휠 · 패드 좌우는 한 칸씩 같은 방식으로 미끄러진다.
///   두 스크롤바(UI_RoguelikeScrollbar)는 보유 카드 창과 같은 그림이며 막대를 끌어 스크롤할 수 있다.
///   패드: 위 / 아래로 줄 이동(Explicit), A로 띠를 열면 커서가 띠의 첫 칸으로 넘어가 좌 / 우로 칸을 옮기고(보이도록 띠가 한 칸씩 미끄러져 스크롤), B는 띠를 닫고 커서를 그 줄로 돌려준다.
///   줄에 커서가 있을 때(띠가 닫혀 있을 때)의 B는 창 닫기다. LB / RB 탭 전환은 띠를 닫고 새 탭의 첫 줄을 고른다.
///   포커스 표시는 로그라이크의 다른 화면과 같은 커서 상자(viewCtx.cursorBoxUI)다. 마우스 모드의 호버는 두 단계다: 포인터가 올라간 줄은 hoveredRow에 기록만 하고, 그 줄이 본문 뷰포트 안에 온전히 보이며 본문이 멈춰 있을 때만 "인정"해 효과음 · 포커스 · 커서를 낸다(걸쳐 보이는 줄 · 스크롤 중에는 반응 없음, 클릭은 그대로 받는다). 패드의 커서는 대상이 일부라도 잘려 있으면 숨었다가 온전해지면 뜬다.
///   B 한 번이 "띠 닫기"와 "창 닫기"를 함께 하지 않게 한다: UICancelEvent(이 창)와 깊이 관리(UIDepthController.TryCloseTopView가 부르는 Hide) 두 경로가 같은 프레임에 오므로,
///   Hide()는 띠가 열려 있으면 띠만 닫고, 띠를 방금(같은 프레임) 닫았으면 무시한다. 딤 클릭 · 뒤로 버튼은 띠와 상관없이 창을 닫는다(RequestClose).
///   패드 포커스는 ControllerSupport_UI가이드.md 3-8을 따른다: 패드 모드로 열릴 때 · 패드로 바뀔 때만 첫 줄 선택, Explicit 탐색, 선택이 null이 되면 복구, 마우스 모드로 바뀌면 선택 해제, 닫을 때 선택 비움.
/// </summary>
public class UIView_RoguelikeRecords : UIView_RoguelikeModalBase
{
    // 판 높이(탭 명판 · 점 3개 · 본문 250). 카드 띠가 열려도 늘리지 않는다(본문이 세로로 스크롤된다).
    public const float PanelHeight = 282f;

    private const int TabCount = 3;
    private const int MaxRows = 5;

    // 18개 언어 중 가장 긴 탭 이름으로 잰 명판 최소 폭(px, 시안 v3 측정: 가장 긴 탭 183 + 화살표 · 여백 54 + LB · RB 아이콘 자리 34).
    private const float SpecTabPlateWidth = 272f;
    private const float TabArrowWidth = 22f;
    // 화살표 안쪽에 붙는 LB · RB 아이콘 한쪽 자리(아이콘 16 + 간격 1).
    private const float TabKeyIconWidth = 17f;
    private const float RowHeight = 46f;
    private const float RowGap = 4f;

    // 카드 띠: 칸 36 + 간격 2 -> 폭 302(8칸 x 38 - 2)에 8칸이 딱 보인다.
    private const float StripHeight = 36f;
    private const float StripSlotStep = 38f;
    private const float StripSlotSize = UI_RoguelikeCardIconSlot.SlotSize;
    private const float StripViewportWidth = 302f;
    private const int StripKindCount = 4;
    private const int StripMaxCards = 32;
    // 카드가 8장을 넘을 때만 띠 아래에 두는 가로 스크롤바: 칸과의 간격 2 + 두께 4(그림 3열 + 투명 1열).
    private const float StripBarGap = 2f;
    private const float StripBarThickness = 3f;
    // 띠 들여쓰기와 연결선(줄 기준): 줄 왼쪽에서 4px 안쪽(굵기 2)에서 줄 아래 끝부터 내려와 띠의 세로 가운데에서 띠 왼쪽 2px 앞까지 꺾인다.
    private const float StripIndent = 14f;
    private const float LinkInset = 4f;
    private const float LinkThickness = 2f;
    private const float LinkGapToStrip = 2f;
    // 칸 맞춤 · 자동 스크롤 미끄러짐(unscaled). 관성이 이 속도(px/s)보다 느려지면 가장 가까운 칸 경계로 맞춘다.
    private const float SlideSeconds = 0.12f;
    private const float StripSnapSpeed = 60f;
    private const float SlideEpsilon = 0.01f;
    // 본문 세로 휠 감도: 입력 모듈(InputSystemUIInputModule)은 휠 한 칸을 scrollDelta 6으로 주므로 6 x 8/3 = 한 칸 16px. 플레이 모드에서 실제 이동량을 재서 확인했다.
    private const float BodyWheelSensitivity = 8f / 3f;
    // 커서 상자는 대상보다 사방 2px 크다.
    private const float CursorPadding = 4f;

    private const float DimFadeSeconds = 0.18f;
    private const float PanelEnterSeconds = 0.2f;
    private const float PanelEnterOffset = 6f;

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

    public IReadOnlyList<RoguelikeRecordViewData> ClearRanking => clearRanking;
    public IReadOnlyList<RoguelikeRecordViewData> UnlimitRanking => unlimitRanking;

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
    [SerializeField] private RectTransform rowRoot;
    [SerializeField] private UI_RoguelikeRecordRow rowTemplate;
    [SerializeField] private TMP_Text emptyText;

    [Header("본문 세로 스크롤")]
    [SerializeField, Tooltip("본문(340x250 마스크)의 세로 ScrollRect. rowRoot(줄 + 카드 띠)가 content다.")] private ScrollRect bodyScroll;
    [SerializeField] private RectTransform bodyViewport;
    [SerializeField, Tooltip("본문 오른쪽 여백의 세로 스크롤바(보유 카드 창과 같은 그림). 내용이 넘칠 때만 보인다.")] private UI_RoguelikeScrollbar bodyBar;

    [Header("카드 띠")]
    [SerializeField, Tooltip("줄 아래에 열리는 카드 띠 묶음(가로 스크롤). 평소에는 꺼 둔다.")] private RectTransform stripRoot;
    [SerializeField] private UI_RoguelikeStripScroll stripScroll;
    [SerializeField] private RectTransform stripViewport;
    [SerializeField] private RectTransform stripContent;
    [SerializeField, Tooltip("카드 띠 칸 견본(선택 대상이 있는 UI_RoguelikeCardIconSlot). 꺼 둔다.")] private UI_RoguelikeCardIconSlot stripSlotTemplate;
    [SerializeField, Tooltip("카드 띠 아래 가로 스크롤바. 카드가 8장을 넘을 때만 보인다.")] private UI_RoguelikeScrollbar stripBar;
    [SerializeField, Tooltip("열린 줄에서 띠로 내려오는 연결선의 세로 조각(주황 단색 Image). 띠가 닫히면 숨긴다. 마우스를 받지 않는다.")] private RectTransform stripLinkVertical;
    [SerializeField, Tooltip("연결선의 가로 조각(띠 세로 가운데에서 띠 왼쪽 앞까지).")] private RectTransform stripLinkHorizontal;

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
    private readonly List<RoguelikeOwnedCardViewData> stripCards = new List<RoguelikeOwnedCardViewData>(StripMaxCards);
    private CursorMotionSettings rowCursorMotion;
    private CursorMotionSettings slotCursorMotion;

    //상태 변수
    private int currentTab;
    private bool bStandaloneClose;
    private Vector2 panelBasePosition;
    private bool bPanelBaseCaptured;
    private Vector2 tabTextBasePosition;
    private Color tabTextBaseColor;
    private bool bTabTextBaseCaptured;
    private Vector2 prevArrowBasePosition;
    private Vector2 nextArrowBasePosition;
    private bool bArrowBaseCaptured;
    private int stripRowIndex = -1;
    private int stripCount;
    private int stripClosedFrame = -1;
    private bool bWindowCloseForced;
    private bool bStripBar;
    private bool bStripPinned;
    private float bodyContentHeight;
    private ScrollSlide stripSlide;
    private ScrollSlide bodySlide;
    private UI_RoguelikeRecordRow focusedRow;
    private UI_RoguelikeCardIconSlot focusedSlot;
    private UI_RoguelikeRecordRow hoveredRow;
    private UI_RoguelikeRecordRow acceptedRow;
    private float lastBodyScroll;
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
    private UnityAction<Vector2> stripScrolledHandler;
    private UnityAction<Vector2> bodyScrolledHandler;
    private Action<float> bodyBarHandler;
    private Action<float> stripBarHandler;
    private Action<int> stripWheelHandler;

    private bool IsGamepadMode => null != inputManager && true == inputManager.IsGamepadMode;

    /// <summary>마우스 호버로 인정된 줄(마우스 모드에서 줄이 온전히 보이고 본문이 멈춰 있을 때만 값이 있다). 확인용 읽기 전용.</summary>
    public UI_RoguelikeRecordRow AcceptedHoverRow => acceptedRow;

    /// <summary>마지막으로 포커스를 받은 줄(마우스는 인정된 줄, 패드는 선택한 줄). 확인용 읽기 전용.</summary>
    public UI_RoguelikeRecordRow FocusedRow => focusedRow;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = viewCtx?.localizationManager;
        inputManager = viewCtx?.inputManager;

        tabShiftHandler = OnTabShift;
        cancelHandler = OnCancelPressed;
        escHandler = OnCancelPressed;
        rowClickedHandler = OnRowClicked;
        rowFocusedHandler = OnRowFocused;
        rowExitedHandler = OnRowExited;
        slotSelectedHandler = OnSlotSelected;
        stripScrolledHandler = OnStripScrolled;
        bodyScrolledHandler = OnBodyScrolled;
        bodyBarHandler = OnBodyBarRequested;
        stripBarHandler = OnStripBarRequested;
        stripWheelHandler = OnStripWheelStep;
        rowCursorMotion = CursorMotionSettings.RowSubtle;
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
        if (null != bodyScroll)
        {
            bodyScroll.scrollSensitivity = BodyWheelSensitivity;
            bodyScroll.onValueChanged.RemoveListener(bodyScrolledHandler);
            bodyScroll.onValueChanged.AddListener(bodyScrolledHandler);
        }
        if (null != stripScroll)
        {
            stripScroll.onValueChanged.RemoveListener(stripScrolledHandler);
            stripScroll.onValueChanged.AddListener(stripScrolledHandler);
            stripScroll.WheelStepEvent -= stripWheelHandler;
            stripScroll.WheelStepEvent += stripWheelHandler;
        }
        if (null != bodyBar)
        {
            bodyBar.ScrollRequestedEvent -= bodyBarHandler;
            bodyBar.ScrollRequestedEvent += bodyBarHandler;
        }
        if (null != stripBar)
        {
            stripBar.ScrollRequestedEvent -= stripBarHandler;
            stripBar.ScrollRequestedEvent += stripBarHandler;
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
        ResetHover();
        CloseStrip(false);
        ApplyStaticTexts();
        RefreshTabGuide();
        RefreshTab();
        OpenTopStrip();
        ResetTabFeedback();
        SnapDotCursor();
        PlayEnter();
        if (true == IsGamepadMode) SelectFirstRow();
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

    /// <summary>탭을 직접 고른다(0 최근 기록 · 1 클리어 랭킹 · 2 언리밋 랭킹). 창이 열린 뒤에 부른다.</summary>
    public void ShowTab(int _tab)
    {
        currentTab = Mathf.Clamp(_tab, 0, TabCount - 1);
        ResetAcceptedHover();
        CloseStrip(false);
        RefreshTab();
        OpenTopStrip();
        ResetTabFeedback();
        SnapDotCursor();
        if (true == IsVisible && true == IsGamepadMode) SelectFirstRow();
    }

    private void ShowPrevTab()
    {
        currentTab = (currentTab + TabCount - 1) % TabCount;
        ResetAcceptedHover();
        CloseStrip(false);
        RefreshTab();
        OpenTopStrip();
        PlayTabSwitchFeedback(-1);
        Sound.PlayUI(SoundID.OptionClick);
        if (true == IsGamepadMode) SelectFirstRow();
    }

    private void ShowNextTab()
    {
        currentTab = (currentTab + 1) % TabCount;
        ResetAcceptedHover();
        CloseStrip(false);
        RefreshTab();
        OpenTopStrip();
        PlayTabSwitchFeedback(1);
        Sound.PlayUI(SoundID.OptionClick);
        if (true == IsGamepadMode) SelectFirstRow();
    }

    private void RefreshTab()
    {
        if (null != tabText) tabText.text = Localize(TabKeys[currentTab]);
        for (int i = 0; tabDots.Length > i; i++)
        {
            if (null != tabDots[i]) tabDots[i].color = i == currentTab ? DotOnColor : DotOffColor;
        }

        LayoutTabPlate();

        int count = 0;

        if (0 == currentTab)
        {
            if (true == Recent.bValid)
            {
                RoguelikeRecordViewData recent = Recent;
                GetRow(0).SetRecord(0, in recent);
                count = 1;
            }
        }
        else
        {
            List<RoguelikeRecordViewData> source = 1 == currentTab ? clearRanking : unlimitRanking;
            count = Mathf.Min(MaxRows, source.Count);
            for (int i = 0; count > i; i++)
            {
                RoguelikeRecordViewData record = source[i];
                GetRow(i).SetRecord(i + 1, in record);
            }
        }

        for (int i = 0; rows.Count > i; i++) rows[i].gameObject.SetActive(i < count);
        if (null != emptyText) emptyText.gameObject.SetActive(0 == count);

        LayoutRows();
    }

    /// <summary>
    /// 탭에 들어올 때 첫 줄(최근 기록 1줄 · 랭킹 1위)의 카드 띠를 연다(카드가 0장이면 열지 않는다). 본문 스크롤은 맨 위에서 시작한다.
    /// 최근 기록 탭은 띠를 고정하고(bStripPinned) 랭킹 탭은 고정하지 않는다. 언어 변경으로 다시 그릴 때는 부르지 않으므로 사용자가 닫은 띠가 다시 열리지 않는다.
    /// </summary>
    private void OpenTopStrip()
    {
        bodySlide.Cancel();
        if (null != bodyScroll) bodyScroll.StopMovement();
        if (null != rowRoot) SetBodyScroll(0f);

        if (0 == rows.Count || false == rows[0].gameObject.activeSelf || 0 >= rows[0].CardCount) return;

        OpenStrip(0, false);
        bStripPinned = 0 == currentTab && 0 == stripRowIndex;
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
            tabDotCursor.DOAnchorPos(Vector2.zero, DotCursorSeconds).SetEase(Ease.OutBack).SetUpdate(true);
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

    private void LayoutRows()
    {
        if (null == rowRoot) return;

        float width = Mathf.Round(rowRoot.rect.width);
        float y = 0f;
        UI_RoguelikeRecordRow previous = null;
        for (int i = 0; rows.Count > i; i++)
        {
            if (false == rows[i].gameObject.activeSelf) continue;

            RectTransform rect = rows[i].Rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -y);
            rows[i].SetOpenBackground(i == stripRowIndex);
            float rowTop = y;
            y += RowHeight + RowGap;

            // 열린 줄 바로 아래에 카드 띠를 놓고 아래 줄들을 띠 높이 + 간격만큼 민다. 카드가 8장을 넘으면 띠 아래에 가로 스크롤바 자리(간격 2 + 두께 4)가 더해진다.
            if (i == stripRowIndex && null != stripRoot)
            {
                float stripHeight = StripHeight + (true == bStripBar ? StripBarGap + StripBarThickness : 0f);
                stripRoot.anchorMin = new Vector2(0f, 1f);
                stripRoot.anchorMax = new Vector2(0f, 1f);
                stripRoot.pivot = new Vector2(0f, 1f);
                stripRoot.sizeDelta = new Vector2(StripViewportWidth, stripHeight);
                stripRoot.anchoredPosition = new Vector2(StripIndent, -y);
                PlaceStripLinks(rowTop + RowHeight, y);
                y += stripHeight + RowGap;
            }

            SetVerticalNavigation(previous, rows[i]);
            previous = rows[i];
        }

        if (null != previous) SetVerticalNavigation(previous, null);

        // 판은 항상 같은 크기다. 내용이 본문 높이를 넘을 때만 세로 스크롤 + 스크롤바가 켜진다.
        if (0 > stripRowIndex) HideStripLinks();
        bodyContentHeight = Mathf.Max(0f, y - RowGap);
        float viewportHeight = null != bodyViewport ? bodyViewport.rect.height : bodyContentHeight;
        rowRoot.sizeDelta = new Vector2(width, Mathf.Max(bodyContentHeight, viewportHeight));
        if (null != bodyScroll) bodyScroll.vertical = bodyContentHeight > viewportHeight + SlideEpsilon;
        ClampBodyScroll();
        UpdateBodyBar();
    }

    /// <summary>
    /// 열린 줄 왼쪽 아래에서 띠로 꺾이는 ㄴ자 연결선을 놓는다(본문 content 안이라 함께 스크롤된다).
    /// 세로 조각은 줄 아래 끝(_rowBottom)부터 띠 세로 가운데 아래 끝까지, 가로 조각은 그 가운데 높이에서 띠 왼쪽 2px 앞까지다.
    /// </summary>
    private void PlaceStripLinks(float _rowBottom, float _stripTop)
    {
        float centerTop = _stripTop + (StripHeight - LinkThickness) * 0.5f;
        PlaceLink(stripLinkVertical, LinkInset, _rowBottom, LinkThickness, centerTop + LinkThickness - _rowBottom);
        PlaceLink(stripLinkHorizontal, LinkInset, centerTop, StripIndent - LinkGapToStrip - LinkInset, LinkThickness);
    }

    private static void PlaceLink(RectTransform _link, float _x, float _top, float _width, float _height)
    {
        if (null == _link) return;

        _link.gameObject.SetActive(true);
        _link.anchorMin = new Vector2(0f, 1f);
        _link.anchorMax = new Vector2(0f, 1f);
        _link.pivot = new Vector2(0f, 1f);
        _link.sizeDelta = new Vector2(_width, _height);
        _link.anchoredPosition = new Vector2(_x, -_top);
    }

    private void HideStripLinks()
    {
        if (null != stripLinkVertical) stripLinkVertical.gameObject.SetActive(false);
        if (null != stripLinkHorizontal) stripLinkHorizontal.gameObject.SetActive(false);
    }

    /// <summary>줄 탐색(Explicit): 보이는 줄끼리 위 / 아래로만 잇는다. _upper의 아래는 _lower, _lower의 위는 _upper다. 목록의 양 끝은 null.</summary>
    private static void SetVerticalNavigation(UI_RoguelikeRecordRow _upper, UI_RoguelikeRecordRow _lower)
    {
        if (null != _upper && null != _upper.SelectButton)
        {
            Navigation navigation = _upper.SelectButton.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnDown = null != _lower ? _lower.SelectButton : null;
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
            panelRoot.DOKill();
            panelRoot.anchoredPosition = panelBasePosition + new Vector2(0f, -PanelEnterOffset);
            panelRoot.DOAnchorPos(panelBasePosition, PanelEnterSeconds, true).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    // // 닫기 · 문구

    /// <summary>딤 클릭 · 뒤로 버튼: 카드 띠가 열려 있어도 창을 닫는다.</summary>
    private void RequestClose()
    {
        if (false == IsVisible) return;

        bWindowCloseForced = true;
        Hide();
        bWindowCloseForced = false;
    }

    /// <summary>패드 B(UICancelEvent) · 메인메뉴의 Esc. 띠가 열려 있으면 띠만 닫고, 아니면 창을 닫는다. 같은 프레임에 띠를 이미 닫았으면(깊이 관리 경로) 무시한다.</summary>
    private void OnCancelPressed()
    {
        if (false == IsVisible) return;
        if (stripClosedFrame == Time.frameCount) return;
        if (true == TryStepBackFromStrip()) return;

        RequestClose();
    }

    /// <summary>
    /// 패드 B의 "한 단계 뒤로". 일반 띠는 닫고 커서를 그 줄로 돌려준다. 항상 열린 띠(최근 기록 탭)는 닫지 않고 커서가 띠 칸에 있을 때만 줄로 돌려준다.
    /// 처리했으면 같은 프레임의 중복 처리를 막는 표시를 남기고 참을 돌려준다. 거짓이면 호출한 쪽이 창을 닫는다.
    /// </summary>
    private bool TryStepBackFromStrip()
    {
        if (0 > stripRowIndex) return false;

        if (true == bStripPinned)
        {
            if (null == focusedSlot) return false;

            stripClosedFrame = Time.frameCount;
            FocusRow(rows[stripRowIndex]);
            Sound.PlayUI(SoundID.OptionClick);
            return true;
        }

        CloseStrip(true);
        Sound.PlayUI(SoundID.OptionClick);
        return true;
    }

    /// <summary>
    /// 깊이 관리(bCloseableByESC)의 ESC · 패드 B 경로는 UIDepthController.TryCloseTopView가 이 Hide()를 바로 부른다.
    /// 그 경로로 왔을 때: 띠가 열려 있으면 띠만 닫고(항상 열린 띠는 커서만 줄로), 이번 프레임에 이미 한 단계 뒤로 갔으면 창을 닫지 않는다(B 한 번에 두 단계가 처리되지 않게).
    /// RequestClose(딤 · 뒤로 버튼)는 bWindowCloseForced로 이 검사를 건너뛴다.
    /// 닫힘 효과음은 여기서 낸다(깊이 관리 경로가 RequestClose를 거치지 않기 때문).
    /// </summary>
    public override void Hide()
    {
        if (true == IsVisible && false == bWindowCloseForced)
        {
            if (stripClosedFrame == Time.frameCount) return;
            if (true == TryStepBackFromStrip()) return;
        }

        if (true == IsVisible) Sound.PlayUI(SoundID.MainClick);
        base.Hide();
    }

    private void ApplyStaticTexts()
    {
        if (null != emptyText) emptyText.text = Localize(EmptyKey);
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

    private void OnLanguageChanged()
    {
        if (false == IsVisible) return;
        ApplyStaticTexts();
        RefreshTab();
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

    // // 카드 띠

    private void OnRowClicked(UI_RoguelikeRecordRow _row)
    {
        int index = rows.IndexOf(_row);
        if (0 > index) return;

        if (index == stripRowIndex)
        {
            // 항상 열린 띠(최근 기록 탭)는 닫지 않는다: 패드 A는 커서를 띠로 보내고, 마우스 클릭은 아무것도 하지 않는다.
            if (true == bStripPinned)
            {
                if (true == IsGamepadMode && 0 < stripCount) FocusSlot(stripSlots[0]);
                return;
            }

            CloseStrip(true);
            Sound.PlayUI(SoundID.OptionClick);
            return;
        }

        if (0 == _row.CardCount) return;

        OpenStrip(index, true);
        Sound.PlayUI(SoundID.OptionClick);
    }

    /// <summary>
    /// _index번 줄 아래에 카드 띠를 연다(이미 열린 줄이 있으면 먼저 닫는다). 열린 줄 + 띠가 본문 밖이면 세로로 스크롤한다.
    /// _bFocusSlot이 참이고 패드 모드면 커서가 띠의 첫 칸으로 넘어간다(최근 기록 탭이 자동으로 여는 경우는 거짓).
    /// </summary>
    private void OpenStrip(int _index, bool _bFocusSlot)
    {
        if (null == stripRoot || null == stripContent || null == stripSlotTemplate) return;

        CloseStrip(false);
        FillStrip(rows[_index].Cards);
        stripRowIndex = _index;
        stripRoot.gameObject.SetActive(true);
        LayoutRows();
        UpdateStripBar();
        ScrollBodyToStrip();

        if (true == _bFocusSlot && true == IsGamepadMode && 0 < stripCount) FocusSlot(stripSlots[0]);
    }

    /// <summary>
    /// 카드 띠를 닫는다. _bRestoreRow가 참이면(사용자가 닫은 경우) 같은 프레임의 중복 닫기를 막는 표시를 남기고, 패드 모드면 커서를 열었던 줄로 돌려준다.
    /// 거짓이면(탭 전환 · 다른 줄을 열기 직전) 조용히 닫는다. 내용이 줄어 본문 스크롤은 맨 위로 돌아온다.
    /// </summary>
    private void CloseStrip(bool _bRestoreRow)
    {
        if (0 > stripRowIndex) return;

        int closedIndex = stripRowIndex;
        stripRowIndex = -1;
        bStripPinned = false;
        focusedSlot = null;
        stripSlide.Cancel();
        bodySlide.Cancel();
        if (null != stripScroll) stripScroll.StopMovement();
        if (null != stripRoot) stripRoot.gameObject.SetActive(false);
        bStripBar = false;
        LayoutRows();

        if (false == _bRestoreRow) return;

        stripClosedFrame = Time.frameCount;
        if (true == IsGamepadMode && rows.Count > closedIndex && true == rows[closedIndex].gameObject.activeSelf) FocusRow(rows[closedIndex]);
    }

    /// <summary>창이 닫힐 때 띠 · 포커스 · 스크롤 상태를 처음으로 돌린다.</summary>
    private void ResetStripState()
    {
        stripRowIndex = -1;
        stripClosedFrame = -1;
        bStripPinned = false;
        focusedRow = null;
        focusedSlot = null;
        bStripBar = false;
        HideStripLinks();
        for (int i = 0; rows.Count > i; i++) rows[i].SetOpenBackground(false);
        stripSlide.Cancel();
        bodySlide.Cancel();
        if (null != stripScroll) stripScroll.StopMovement();
        if (null != bodyScroll) bodyScroll.StopMovement();
        if (null != stripRoot) stripRoot.gameObject.SetActive(false);
        if (null != rowRoot) SetBodyScroll(0f);
    }

    /// <summary>
    /// 그 판의 카드 전부를 종류 순(ECardKind 값 순: 액티브 -> 패시브 -> 계약 -> 프리즘, 같은 종류 안에서는 저장 순서)으로 띠 칸에 채우고 칸 · 내용 크기를 정한다.
    /// 칸은 풀로 돌려 쓴다(모자랄 때만 만들고 남는 칸은 끈다). 8장을 넘으면 가로 스크롤바를 켜도록 bStripBar를 정한다.
    /// </summary>
    private void FillStrip(IReadOnlyList<RoguelikeOwnedCardViewData> _cards)
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

        stripCount = stripCards.Count;

        while (stripSlots.Count < stripCount)
        {
            UI_RoguelikeCardIconSlot slot = Instantiate(stripSlotTemplate, stripContent);
            slot.name = "StripSlot" + stripSlots.Count;
            slot.SelectedEvent += slotSelectedHandler;
            stripSlots.Add(slot);
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

            // 좌 / 우로만 잇는다(Explicit).
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
        bStripBar = contentWidth > viewportWidth + SlideEpsilon;
        stripContent.anchorMin = new Vector2(0f, 1f);
        stripContent.anchorMax = new Vector2(0f, 1f);
        stripContent.pivot = new Vector2(0f, 1f);
        stripContent.sizeDelta = new Vector2(Mathf.Max(contentWidth, viewportWidth), StripHeight);
        stripContent.anchoredPosition = Vector2.zero;

        focusedSlot = null;
        stripSlide.Cancel();
        if (null != stripScroll) stripScroll.StopMovement();
    }

    // // 스크롤 (본문 세로 · 카드 띠 가로)

    /// <summary>카드 띠 스크롤이 바뀌었다(끌기 · 관성 · 휠 · 미끄러짐). 가로 스크롤바를 갱신한다. 위치 반올림은 하지 않는다(맞춤은 멈춘 뒤 TrySnapStrip이 한다).</summary>
    private void OnStripScrolled(Vector2 _normalized)
    {
        UpdateStripBar();
    }

    /// <summary>본문 세로 스크롤이 바뀌었다(끌기 · 휠 · 관성 · 스크롤바).</summary>
    private void OnBodyScrolled(Vector2 _normalized)
    {
        UpdateBodyBar();
    }

    private void UpdateBodyBar()
    {
        if (null == bodyBar || null == rowRoot || null == bodyViewport) return;

        bodyBar.Refresh(bodyViewport.rect.height, bodyContentHeight, rowRoot.anchoredPosition.y);
    }

    private void UpdateStripBar()
    {
        if (null == stripBar || null == stripContent || null == stripViewport) return;

        stripBar.Refresh(stripViewport.rect.width, stripContent.rect.width, GetStripScroll());
    }

    private void OnBodyBarRequested(float _ratio)
    {
        if (null == rowRoot || null == bodyViewport) return;

        float max = Mathf.Max(0f, bodyContentHeight - bodyViewport.rect.height);
        bodySlide.Cancel();
        if (null != bodyScroll) bodyScroll.StopMovement();
        SetBodyScroll(Mathf.Round(_ratio * max));
    }

    private void OnStripBarRequested(float _ratio)
    {
        if (null == stripContent) return;

        stripSlide.Cancel();
        if (null != stripScroll) stripScroll.StopMovement();
        SetStripScroll(Mathf.Round(_ratio * GetStripMax()));
    }

    /// <summary>휠 한 칸(_direction -1 / +1): 가장 가까운 다음 칸 경계로 미끄러진다. 미끄러지는 중이면 그 도착점에서 이어서 한 칸.</summary>
    private void OnStripWheelStep(int _direction)
    {
        if (0 > stripRowIndex || null == stripContent) return;

        float max = GetStripMax();
        float basePosition = true == stripSlide.bActive ? stripSlide.to : GetStripScroll();
        StartStripSlide(basePosition, StepStripStop(basePosition, _direction, max));
    }

    private void SetBodyScroll(float _scroll)
    {
        if (null == rowRoot) return;

        rowRoot.anchoredPosition = new Vector2(rowRoot.anchoredPosition.x, _scroll);
        UpdateBodyBar();
    }

    private void SetStripScroll(float _scroll)
    {
        if (null == stripContent) return;

        stripContent.anchoredPosition = new Vector2(-_scroll, 0f);
        UpdateStripBar();
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

    /// <summary>내용이 줄어 스크롤 범위를 벗어난 본문 위치를 범위 안으로 돌린다(띠를 닫으면 맨 위).</summary>
    private void ClampBodyScroll()
    {
        if (null == rowRoot) return;

        float viewportHeight = null != bodyViewport ? bodyViewport.rect.height : bodyContentHeight;
        float max = Mathf.Max(0f, bodyContentHeight - viewportHeight);
        float scroll = rowRoot.anchoredPosition.y;
        float clamped = Mathf.Clamp(scroll, 0f, max);
        if (clamped == scroll) return;

        bodySlide.Cancel();
        if (null != bodyScroll) bodyScroll.StopMovement();
        SetBodyScroll(clamped);
    }

    /// <summary>
    /// 본문의 _top ~ _bottom(내용 위쪽 기준 px) 구간이 보이도록 정수 픽셀 위치로 짧게 미끄러져 스크롤한다. 이미 보이면 가만히 둔다.
    /// 보이는 높이보다 구간이 크면 위쪽을 우선한다.
    /// </summary>
    private void ScrollBodyTo(float _top, float _bottom)
    {
        if (null == rowRoot || null == bodyViewport) return;

        float viewportHeight = bodyViewport.rect.height;
        float max = Mathf.Max(0f, bodyContentHeight - viewportHeight);
        float basePosition = true == bodySlide.bActive ? bodySlide.to : rowRoot.anchoredPosition.y;
        float target = basePosition;
        if (basePosition + viewportHeight < _bottom - SlideEpsilon) target = _bottom - viewportHeight;
        if (_top < target - SlideEpsilon) target = _top;

        target = Mathf.Round(Mathf.Clamp(target, 0f, max));
        if (Mathf.Abs(target - basePosition) < SlideEpsilon) return;

        if (null != bodyScroll) bodyScroll.StopMovement();
        bodySlide.Begin(rowRoot.anchoredPosition.y, target);
    }

    /// <summary>열린 줄 + 카드 띠(스크롤바 포함)가 본문 안에 들어오게 스크롤한다.</summary>
    private void ScrollBodyToStrip()
    {
        if (0 > stripRowIndex || null == stripRoot) return;

        ScrollBodyTo(-rows[stripRowIndex].Rect.anchoredPosition.y, -stripRoot.anchoredPosition.y + stripRoot.sizeDelta.y);
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

    /// <summary>스크롤 위치 _scroll에서 가장 가까운 칸 경계(38px 단위) 또는 끝. 끝은 칸 경계가 아니어도 도착점이 된다.</summary>
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
    /// 스크롤바를 잡고 있는 동안에도 기다리고, 놓으면 맞춘다.
    /// </summary>
    private void TrySnapStrip()
    {
        if (null == stripScroll || null == stripContent) return;
        if (true == stripScroll.IsDragging || (null != stripBar && true == stripBar.IsHeld)) return;

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

    /// <summary>
    /// 매 프레임(unscaled): 미끄러짐을 진행하고, 띠는 멈춘 뒤 칸에 맞추며, 본문은 멈춘 뒤 정수 픽셀로 맞춘다.
    /// 끌기 · 관성 중에는 위치를 건드리지 않아 포인터를 1:1로 따라간다.
    /// </summary>
    private void AdvanceScroll()
    {
        float deltaTime = Time.unscaledDeltaTime;

        if (true == bodySlide.bActive) SetBodyScroll(bodySlide.Advance(deltaTime));
        else if (null != bodyScroll && null != rowRoot && 1f > bodyScroll.velocity.sqrMagnitude)
        {
            float scroll = rowRoot.anchoredPosition.y;
            float rounded = Mathf.Round(scroll);
            if (rounded != scroll && 0f <= scroll && scroll <= Mathf.Max(0f, bodyContentHeight - (null != bodyViewport ? bodyViewport.rect.height : 0f))) SetBodyScroll(rounded);
        }

        if (0 > stripRowIndex) return;

        if (true == stripSlide.bActive) SetStripScroll(stripSlide.Advance(deltaTime));
        else TrySnapStrip();
    }

    // // 포커스 · 선택

    /// <summary>
    /// 줄에 마우스가 올라왔거나(_bPointer) 패드 선택이 옮겨 왔다.
    /// 마우스가 올라온 것은 hoveredRow로만 기록하고 반응(효과음 · 포커스 · 커서)은 하지 않는다: 마우스 모드의 반응은 UpdateMouseHover가 줄이 온전히 보이고 본문이 멈췄을 때 한다.
    /// 마우스 모드에서 클릭으로 생긴 선택은 커서를 띄우지 않는다.
    /// </summary>
    private void OnRowFocused(UI_RoguelikeRecordRow _row, bool _bPointer)
    {
        if (true == _bPointer) hoveredRow = _row;
        if (false == IsGamepadMode) return;

        ApplyRowFocus(_row);
    }

    private void OnRowExited(UI_RoguelikeRecordRow _row)
    {
        if (_row == hoveredRow) hoveredRow = null;
    }

    /// <summary>
    /// 마우스 모드의 호버 인정. 마우스가 올라간 줄이 본문 뷰포트 안에 온전히 보이고, 본문이 미끄러지거나 끌리거나 관성으로 움직이는 중이 아니며, 줄이 켜져 있을 때만 그 줄을 인정한다.
    /// 인정된 줄이 바뀔 때만 반응한다: 새로 인정되면 효과음 1번 + 포커스 줄 갱신 + 커서 상자, 인정이 풀리면 커서 상자만 숨긴다(효과음 없음).
    /// </summary>
    private void UpdateMouseHover()
    {
        float scroll = null != rowRoot ? rowRoot.anchoredPosition.y : 0f;
        bool bBodyMoving = true == bodySlide.bActive || lastBodyScroll != scroll
            || (null != bodyScroll && 1f <= bodyScroll.velocity.sqrMagnitude) || (null != bodyBar && true == bodyBar.IsHeld);
        lastBodyScroll = scroll;

        // 꺼진 줄은 PointerExit을 받지 못하므로 기록을 여기서 지운다(다시 켜졌을 때 마우스가 없는데도 반응하지 않게).
        if (null != hoveredRow && false == hoveredRow.gameObject.activeInHierarchy) hoveredRow = null;

        UI_RoguelikeRecordRow candidate = null;
        if (null != hoveredRow && true == hoveredRow.gameObject.activeInHierarchy && false == bBodyMoving && true == IsFullyInView(hoveredRow.Rect)) candidate = hoveredRow;

        if (candidate == acceptedRow) return;

        acceptedRow = candidate;
        if (null == candidate)
        {
            HideCursor();
            return;
        }

        Sound.PlayUI(SoundID.ResultUIHover);
        focusedRow = candidate;
        focusedSlot = null;
        ShowCursor(candidate.Rect, rowCursorMotion);
    }

    /// <summary>
    /// 탭을 넘길 때: 인정 상태만 비우고 커서를 숨긴다. 줄 오브젝트는 탭끼리 돌려 쓰므로 마우스가 올라가 있는 줄(hoveredRow)은 남겨 둔다 -
    /// 새 PointerEnter가 오지 않아도 다음 프레임에 UpdateMouseHover가 새 내용의 줄을 다시 인정한다.
    /// </summary>
    private void ResetAcceptedHover()
    {
        acceptedRow = null;
        if (false == IsGamepadMode) HideCursor();
    }

    /// <summary>마우스 호버 기록과 인정 상태를 비운다(창 열기 · 창 닫기 · 패드로 바뀔 때). 남은 참조로 엉뚱한 줄이 반응하지 않게 한다.</summary>
    private void ResetHover()
    {
        hoveredRow = null;
        acceptedRow = null;
    }

    private void OnSlotSelected(UI_RoguelikeCardIconSlot _slot)
    {
        if (false == IsGamepadMode) return;

        ApplyStripFocus(_slot);
    }

    private void ApplyRowFocus(UI_RoguelikeRecordRow _row)
    {
        if (_row != focusedRow) Sound.PlayUI(SoundID.ResultUIHover);
        focusedRow = _row;
        focusedSlot = null;
        if (true == IsGamepadMode) ScrollBodyTo(-_row.Rect.anchoredPosition.y, -_row.Rect.anchoredPosition.y + RowHeight);
        ShowCursor(_row.Rect, rowCursorMotion);
    }

    private void ApplyStripFocus(UI_RoguelikeCardIconSlot _slot)
    {
        if (_slot != focusedSlot) Sound.PlayUI(SoundID.ResultUIHover);
        focusedSlot = _slot;
        ScrollToSlot(_slot);
        ScrollBodyToStrip();
        ShowCursor(_slot.Rect, slotCursorMotion);
    }

    /// <summary>줄을 선택하고 커서를 올린다(이미 같은 줄이 선택되어 있으면 커서만).</summary>
    private void FocusRow(UI_RoguelikeRecordRow _row)
    {
        if (null == _row) return;

        focusedRow = _row;
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && eventSystem.currentSelectedGameObject != _row.gameObject) eventSystem.SetSelectedGameObject(_row.gameObject);
        ApplyRowFocus(_row);
    }

    private void FocusSlot(UI_RoguelikeCardIconSlot _slot)
    {
        if (null == _slot || null == _slot.SelectTarget) return;

        focusedSlot = _slot;
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && eventSystem.currentSelectedGameObject != _slot.gameObject) eventSystem.SetSelectedGameObject(_slot.gameObject);
        ApplyStripFocus(_slot);
    }

    private void SelectFirstRow()
    {
        for (int i = 0; rows.Count > i; i++)
        {
            if (false == rows[i].gameObject.activeSelf) continue;

            FocusRow(rows[i]);
            return;
        }
    }

    /// <summary>
    /// 패드 선택이 사라졌을 때(가이드 3-8 "가장 흔한 버그") · 패드로 바뀔 때의 복구: 띠가 열려 있으면 마지막 칸(없으면 첫 칸), 아니면 마지막 줄(없으면 첫 줄).
    /// </summary>
    private void RestoreSelection()
    {
        if (0 <= stripRowIndex && 0 < stripCount && (false == bStripPinned || null != focusedSlot))
        {
            UI_RoguelikeCardIconSlot slot = null != focusedSlot && true == focusedSlot.gameObject.activeInHierarchy ? focusedSlot : stripSlots[0];
            FocusSlot(slot);
            return;
        }

        if (null != focusedRow && true == focusedRow.gameObject.activeInHierarchy)
        {
            FocusRow(focusedRow);
            return;
        }

        SelectFirstRow();
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

        // 커서 상자는 본문 마스크에 잘리지 않으므로 대상이 뷰포트 안에 온전히 들어와 있을 때만 띄운다. 잘려 있으면 RefreshCursorClip이 온전해질 때 띄운다.
        if (false == IsFullyInView(_target))
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

    /// <summary>
    /// 대상(줄 또는 띠 칸)이 본문 뷰포트 안에 온전히 들어와 있는지 본다. 띠 칸은 띠 뷰포트(가로)도 함께 본다. 월드 좌표의 축 정렬 사각형으로 비교하고 코너 배열을 재사용한다.
    /// </summary>
    private bool IsFullyInView(RectTransform _target)
    {
        if (null == bodyViewport) return true;

        float epsilon = 0.5f * Mathf.Abs(bodyViewport.lossyScale.x);
        _target.GetWorldCorners(targetCorners);

        bodyViewport.GetWorldCorners(clipCorners);
        if (targetCorners[0].x < clipCorners[0].x - epsilon || targetCorners[2].x > clipCorners[2].x + epsilon) return false;
        if (targetCorners[0].y < clipCorners[0].y - epsilon || targetCorners[2].y > clipCorners[2].y + epsilon) return false;

        if (null == stripViewport || false == _target.IsChildOf(stripViewport)) return true;

        stripViewport.GetWorldCorners(clipCorners);
        return targetCorners[0].x >= clipCorners[0].x - epsilon && targetCorners[2].x <= clipCorners[2].x + epsilon;
    }

    /// <summary>
    /// 스크롤(휠 · 끌기 · 패드 이동의 미끄러짐)로 커서 대상이 일부라도 잘리면 커서 상자를 바로 숨기고, 다시 온전히 들어오면 띄운다.
    /// 마우스 모드에서 호버가 끝나면 OnRowExited가 대상을 비우므로 마우스가 올라가 있는 줄에만 다시 뜬다.
    /// </summary>
    private void RefreshCursorClip()
    {
        if (null == cursorTarget) return;

        ICursorBoxUI cursor = viewCtx?.cursorBoxUI;
        if (null == cursor) return;

        if (false == cursorTarget.gameObject.activeInHierarchy)
        {
            cursorTarget = null;
            bCursorClipped = false;
            return;
        }

        bool bFull = IsFullyInView(cursorTarget);
        if (true == bCursorClipped)
        {
            if (false == bFull) return;

            bCursorClipped = false;
            cursor.Show(cursorTarget, cursorTarget.rect.size + new Vector2(CursorPadding, CursorPadding), Vector2.zero, cursorMotion);
        }
        else if (false == bFull && true == cursor.IsTarget(cursorTarget))
        {
            cursor.Hide();
            bCursorClipped = true;
        }
    }

    // // 유니티 라이프사이클

    public override void Update()
    {
        base.Update();

        if (false == IsVisible) return;

        AdvanceScroll();
        if (false == IsGamepadMode) UpdateMouseHover();
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
        if (null != bodyScroll && null != bodyScrolledHandler) bodyScroll.onValueChanged.RemoveListener(bodyScrolledHandler);
        if (null != bodyBar) bodyBar.ScrollRequestedEvent -= bodyBarHandler;
        if (null != stripBar) stripBar.ScrollRequestedEvent -= stripBarHandler;
        if (null != dimButton) dimButton.onClick.RemoveListener(RequestClose);
        if (null != prevTabButton) prevTabButton.onClick.RemoveListener(ShowPrevTab);
        if (null != nextTabButton) nextTabButton.onClick.RemoveListener(ShowNextTab);
        ClosedEvent = null;
        base.OnDestroy();
    }
}
