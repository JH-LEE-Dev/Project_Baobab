using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 보유 카드 확인 UI. 기획서 5-2 "보유 카드 확인" - 전투 중에도 보유 카드 종류와 레벨을 볼 수 있다.
/// 프리팹에서 bCloseableByESC를 켜 두면 ESC/패드 B로 닫힌다. 창 바깥(딤)을 눌러도 닫힌다(글자가 든 닫기 버튼은 두지 않는다 - 언어마다 글자 폭이 달라 버튼 크기가 흔들린다).
///
/// 열려 있는 동안 게임이 일시정지된다(Time.timeScale = 0, RoguelikeUICoordinator가 건다). 전투 중에 열어도
/// 나무 스폰·임계치 피해가 진행되지 않는다. 닫히면(어떤 경로든) 원래 속도로 돌아간다.
/// 런이 진행 중인 단계(런 시작~웨이브 클리어·런 클리어)에서만 열리고, 쓰러짐·결과·로비로 넘어가면 시스템이 닫는다.
/// 일시정지 중이므로 이 화면의 움직임은 unscaled 시간으로 돌린다(카드 움직임 · 스크롤 모두 Time.unscaledDeltaTime).
///
/// [구성 - 고정 설명 패널(A안)] 어두운 딤 위에 한 묶음(Content)이 화면 가운데에 놓인다.
///   제목 줄   제목 · 레어/일반 장수
///   왼쪽      카드 목록. 카드 선택 화면과 같은 카드(UI_RoguelikeCard 50x81)를 크기 그대로 격자로 놓는다(레어 먼저, 일반 뒤). 칸 수 제한이 없어 넘치면 안에서 스크롤한다.
///   오른쪽    포커스한 카드의 설명 패널(이름 · 레벨 · 설명 · "현재 효과"). 폭은 고정이고 높이는 내용에 맞춘다. 카드 그림은 목록의 카드가 이미 보여 주므로 따로 그리지 않는다.
///   아래      키 안내 한 줄. 아이콘은 현재 입력 장치(키보드/패드)를 따라 바뀐다(UI_KeyboardImage - 옵션·특성 UI와 같은 방식).
///   설명은 잘라 쓰지 않는다: 모든 보유 카드의 설명 높이를 열 때 한 번 재서 패널 크기를 고정하고(호버를 옮겨도 화면이 움직이지 않는다),
///   화면 높이에 들어가지 않는 카드가 있으면 경고를 남긴다.
///
/// [열기 · 닫기 연출 - RoguelikeOwnedCardsMotion] 우하단 HUD 트레이(SetTray로 연결)에서 책이 펼쳐지고, 닫힐 때 같은 칸으로 접혀 들어간다. 입자 · 탄성 없음, unscaled 시간.
///   열기: 딤 → 책이 트레이 칸에서 확장 → 제목 · 키 안내 · 카드(왼쪽부터 폭이 펼쳐짐) · 설명이 차례로 나타난다.
///   닫기: 설명 · 제목이 먼저 사라지고 카드가 오른쪽부터 접힌 뒤 책이 트레이 칸으로 수렴한다. 수렴이 끝나는 순간 창이 꺼지고 트레이가 도착 반응(UI_RoguelikeOwnedCardTray)을 이어받는다.
///   연출 중에는 카드 · 설명이 마우스를 받지 않고 패드 포커스는 열기가 끝난 뒤에 건다. 닫기를 연출 없이 하려면 HideImmediate(시스템이 강제로 닫을 때), 연출 중 Hide는 한 번만 먹는다(열기 중이면 열기가 끝난 뒤).
///   트레이가 연결되지 않았으면 우하단(가장자리 8칸) 기본 위치로 줄어들고 트레이 반응만 빠진다.
///   창이 HUD보다 위 캔버스라서, 열려 있는 동안 트레이를 창의 맨 위 레이어(trayLayer)로 옮겨 딤 · 책 위에 보이게 하고 닫기 도착 직후 HUD로 돌려보낸다.
///   뷰를 만들며 Show한 바로 그 프레임에 Hide하면(RoguelikeSystem.OpenViews) 연출 없이 즉시 닫는다.
///
/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-8] 키보드/마우스 모드는 마우스 전용이다: 호버가 포커스(카드가 올라가고 CursorBox가 따라간다)이고,
///   EventSystem 선택은 잡지 않아 방향키가 아무 일도 하지 않는다. 패드 모드는 첫 칸을 선택한 채 열리고 스틱·십자키로 격자를 이동한다.
///   장치는 열려 있는 중에도 InputDeviceChangedEvent로 바뀐다. 설명은 마지막으로 포커스한 칸의 것이 남는다.
///   패드 선택이 가려진 칸으로 가면 그 칸이 보이게 스크롤한다(마우스 호버로는 스크롤하지 않는다). 카드를 누르는 동작은 없다(설명은 포커스만으로 나온다).
///   키 안내 줄: 키보드 = [Esc] 닫기, 패드 = [스틱] 이동 [B] 닫기(이동 안내는 패드 모드에서만 보인다).
/// </summary>
public class UIView_RoguelikeOwnedCards : UIView_RoguelikeModalBase
{
    // 화면 배치(Content 왼쪽 위 기준 정수 픽셀). 640×360 기준: 가장자리 8 + 목록 398(카드 7열에 딱 맞춤) + 2 + 설명 224 + 가장자리 8.
    private const float GroupMaxWidth = 624f;
    private const float InfoTargetWidth = 200f;
    private const float PanelGap = 2f;
    private const float ScreenMargin = 8f;
    private const float VerticalGap = 4f;
    // 제목 글자(Galmuri11)는 11칸 높이라, 명판 높이가 홀수여야 위아래 여백이 정수 칸으로 같아진다(짝수면 반 칸씩 어긋난다).
    private const float TitleHeight = 21f;
    private const float KeyGuideHeight = 18f;
    private const float MaxMainHeight = 300f;
    private const float MinMainHeight = 120f;
    private const float TitleGap = 8f;
    private const float TitlePlatePadding = 8f;

    // 카드 목록(왼쪽)
    // 목록 패널 가장자리에서 카드 영역까지 8칸(틀 4칸 + 4칸), 카드 영역 안쪽에 호버 괄호가 삐져나오는 여유 4칸. 스크롤바는 오른쪽 여백 안에 둔다.
    private const float BookPadding = 8f;
    private const float BracketClearance = 4f;
    private const float ScrollbarWidth = 3f;
    private const float ScrollbarGap = 1f;
    private const float MinThumbHeight = 8f;
    private const float CellGap = 4f;
    private const float RowGap = 8f;
    private const float ScrollMargin = 4f;
    // 마우스 휠 한 칸의 스크롤 양. 프로젝트의 다른 ScrollRect(UI_Option)와 같은 값이다.
    private const float WheelSensitivity = 2f;

    // 설명(오른쪽)
    private const float InfoPadding = 4f;
    private const float InfoPaddingX = 4f;
    private const int CursorPadding = 4;
    private const float Unbounded = 10000f;

    // 열기 · 닫기 연출 시간표(연출 단위 - RoguelikeOwnedCardsMotion.Speed로 나눠 실제 시간이 된다)
    private const float OpenDimEnd = 0.3f;
    private const float OpenBookStart = 0.05f;
    private const float OpenBookEnd = 0.55f;
    private const float OpenTitleStart = 0.45f;
    private const float OpenTitleEnd = 0.7f;
    private const float OpenInfoStart = 0.55f;
    private const float OpenInfoEnd = 0.9f;
    private const float OpenCardStart = 0.4f;
    private const float OpenCardStagger = 0.04f;
    private const float OpenCardSpan = 0.22f;
    private const float CloseInfoEnd = 0.2f;
    private const float CloseTitleStart = 0.15f;
    private const float CloseTitleEnd = 0.3f;
    private const float CloseCardStagger = 0.015f;
    private const float CloseCardSpan = 0.12f;
    private const float CloseBookStart = 0.25f;
    private const float CloseDimStart = 0.5f;
    private const float InfoSlidePixels = 8f;
    // 연출에서 순서를 매기는 카드 수(목록 첫 화면에 보이는 만큼). 그 뒤 카드는 같이 움직인다.
    private const int MotionCellLimit = 10;
    // 트레이가 연결되지 않았을 때 줄어들 칸(HUD 트레이 기본 배치: 우하단 가장자리 8칸, 34x19).
    private const float FallbackTrayWidth = 34f;
    private const float FallbackTrayHeight = 19f;
    private const float FallbackTrayMargin = 8f;

    // 문구 키(RoguelikeUI.json)
    private const string TitleKey = "Roguelike_OwnedCards_Title";
    private const string RareShortKey = "Roguelike_OwnedCards_RareShort";
    private const string NormalShortKey = "Roguelike_OwnedCards_NormalShort";
    private const string EmptyKey = "Roguelike_OwnedCards_Empty";
    private const string HintMoveKey = "Roguelike_OwnedCards_HintMove";
    private const string HintCloseKey = "Roguelike_OwnedCards_HintClose";

    public event Action ClosedEvent;

    private enum EMotion
    {
        None,
        Opening,
        Closing,
    }

    protected readonly List<RoguelikeOwnedCardViewData> cards = new List<RoguelikeOwnedCardViewData>(32);

    //외부 의존성 (프리팹에서 연결)
    [Header("화면")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button dimButton;

    [Header("제목 줄")]
    [SerializeField] private RectTransform titlePlate;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text countText;

    [Header("카드 목록 (왼쪽)")]
    [SerializeField] private RectTransform bookRoot;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform scrollTrack;
    [SerializeField] private RectTransform scrollThumb;
    [SerializeField] private UI_RoguelikeOwnedCardCell cellPrefab;
    [SerializeField] private TMP_Text emptyText;

    [Header("설명 (오른쪽)")]
    [SerializeField] private RectTransform infoRoot;
    [SerializeField] private UI_RoguelikeCardDescPanel descPanel;

    [Header("키 안내 줄")]
    [SerializeField] private RectTransform keyGuideRoot;
    [SerializeField] private RectTransform moveGuideRow;
    [SerializeField] private TMP_Text hintMoveText;
    [SerializeField] private TMP_Text hintCloseText;
    [SerializeField] private UI_KeyboardImage[] keyGuideImages = new UI_KeyboardImage[0];

    [Header("열기 · 닫기 연출")]
    [SerializeField] private RectMask2D bookMask;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private CanvasGroup infoGroup;
    [SerializeField] private CanvasGroup keyGuideGroup;
    [SerializeField, Tooltip("창이 열려 있는 동안 HUD 트레이를 올려 두는 맨 위 레이어(화면 전체, 마지막 형제).")] private RectTransform trayLayer;

    //상태 변수
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private Action languageChangedHandler;
    private Action<EInputDeviceType> deviceChangedHandler;
    private CursorMotionSettings cursorMotion;
    private readonly List<UI_RoguelikeOwnedCardCell> cells = new List<UI_RoguelikeOwnedCardCell>(32);
    private readonly StringBuilder labelBuilder = new StringBuilder(64);
    private int cellCount;
    private int rareCount;
    private int normalCount;
    private int columns = 1;
    private UI_RoguelikeOwnedCardCell focusedCell;
    private UI_RoguelikeSelectRelay focusedRelay;
    private float viewportWidth;
    private float viewportHeight;
    private float contentHeight;
    private float infoWidth;
    private float mainHeight;
    private bool bReady;
    private bool bGamepadMode;
    private bool bFocusShown;

    // 열기 · 닫기 연출
    private readonly Vector3[] cornerBuffer = new Vector3[4];
    private UI_RoguelikeOwnedCardTray tray;
    private Graphic dimGraphic;
    private Color dimColor;
    private Rect bookFinalRect;
    private Rect infoFinalRect;
    private Rect trayTargetRect;
    private EMotion motion;
    private float motionClock;
    private bool bMotionReady;
    private bool bCloseQueued;

    public IReadOnlyList<RoguelikeOwnedCardViewData> Cards => cards;

    public virtual void SetCards(IReadOnlyList<RoguelikeOwnedCardViewData> _cards)
    {
        cards.Clear();
        if (null != _cards)
        {
            for (int i = 0; i < _cards.Count; i++) cards.Add(_cards[i]);
        }

        OnCardsChanged();
    }

    /// <summary>
    /// 우하단 HUD 트레이를 연결한다(UIView_RoguelikeHUD.OwnedTray). 연결하면 창이 이 칸에서 펼쳐지고 이 칸으로 접히며, 트레이가 열기 · 닫기 반응을 낸다. null이면 기본 위치를 쓰고 트레이 반응은 없다.
    /// </summary>
    public void SetTray(UI_RoguelikeOwnedCardTray _tray)
    {
        tray = _tray;
    }

    /// <summary>
    /// 닫는다. 연출을 켜 둔 화면이면 닫기 연출을 재생한 뒤 실제로 닫힌다(HiddenEvent · ClosedEvent는 그때 온다).
    /// 닫는 중이면 무시하고, 여는 중이면 열기가 끝난 뒤 닫는다. 시스템이 강제로 닫을 때는 HideImmediate를 쓴다.
    /// </summary>
    public override void Hide()
    {
        if (false == IsVisible) return;

        if (false == bMotionReady)
        {
            base.Hide();
            return;
        }

        if (EMotion.Closing == motion) return;

        if (EMotion.Opening == motion)
        {
            // Show와 같은 프레임의 Hide(뷰 생성 직후 정리)는 연출 없이 닫는다. 연출이 한 프레임이라도 진행됐으면 열기가 끝난 뒤에 닫는다.
            if (0f >= motionClock)
            {
                HideImmediate();
                return;
            }

            bCloseQueued = true;
            return;
        }

        BeginClose();
    }

    /// <summary>연출 없이 바로 닫는다(런 종료 · 쓰러짐 같은 시스템 강제 닫기). 트레이 연출도 멈춘다.</summary>
    public void HideImmediate()
    {
        motion = EMotion.None;
        bCloseQueued = false;
        if (null != tray) tray.ResetEffect();

        base.Hide();
    }

    /// <summary>목록을 다시 그린다. 화면이 아직 꺼져 있으면(SetCards 뒤에 Show가 온다) 열릴 때 그린다.</summary>
    protected virtual void OnCardsChanged()
    {
        if (true == bReady && true == IsVisible) Rebuild();
    }

    // // 다시 그리기

    private void Rebuild()
    {
        localizationManager = viewCtx?.localizationManager;
        descPanel.Initialize(localizationManager);

        BindCells();
        LayoutAll();
        RebuildNavigation();
        FocusFirst();
    }

    /// <summary>레어를 앞에, 일반을 뒤에 두고(각각 획득 순서 유지) 셀을 채운다. 셀은 모자랄 때만 만들고 남는 셀은 끈다.</summary>
    private void BindCells()
    {
        rareCount = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (true == CardRarity.IsRare(cards[i].displayGrade)) rareCount++;
        }

        normalCount = cards.Count - rareCount;
        cellCount = cards.Count;

        while (cells.Count < cellCount)
        {
            UI_RoguelikeOwnedCardCell cell = Instantiate(cellPrefab, scrollContent);
            cell.name = "Cell_" + cells.Count;
            cell.Relay.FocusedEvent += OnRelayFocused;
            cells.Add(cell);
        }

        int next = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (false == CardRarity.IsRare(cards[i].displayGrade)) continue;

            RoguelikeOwnedCardViewData data = cards[i];
            cells[next].Bind(in data);
            next++;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (true == CardRarity.IsRare(cards[i].displayGrade)) continue;

            RoguelikeOwnedCardViewData data = cards[i];
            cells[next].Bind(in data);
            next++;
        }

        for (int i = cellCount; i < cells.Count; i++) cells[i].Unbind();
    }

    // // 배치

    /// <summary>
    /// 화면 크기에 맞춰 모든 것을 놓는다. 화면 폭·높이는 해상도 비율에 따라 달라지므로(폭 기준 스케일) 매번 읽는다.
    /// 위치는 모두 정수 픽셀이다(그룹 크기를 짝수로 맞춰 가운데 정렬이 반 칸에 걸리지 않게 한다).
    /// </summary>
    private void LayoutAll()
    {
        Rect screen = ((RectTransform)contentRoot.parent).rect;

        float groupWidth = Mathf.Floor(Mathf.Min(GroupMaxWidth, screen.width - ScreenMargin * 2f) * 0.5f) * 2f;
        // 목록 폭은 카드 열 수에 딱 맞춘다(남는 폭이 양쪽 여백으로 새지 않게). 남는 폭은 설명 패널이 가진다.
        float cardStep = UI_RoguelikeOwnedCardCell.CardWidth + CellGap;
        float gridSpace = groupWidth - PanelGap - InfoTargetWidth - (BookPadding + BracketClearance) * 2f;
        columns = Mathf.Max(1, Mathf.FloorToInt((gridSpace + CellGap) / cardStep));
        float gridWidth = columns * UI_RoguelikeOwnedCardCell.CardWidth + (columns - 1) * CellGap;
        float bookWidth = gridWidth + (BookPadding + BracketClearance) * 2f;
        infoWidth = groupWidth - PanelGap - bookWidth;

        float maxMain = screen.height - ScreenMargin * 2f - TitleHeight - KeyGuideHeight - VerticalGap * 2f;
        mainHeight = Mathf.Floor(Mathf.Clamp(maxMain, MinMainHeight, MaxMainHeight));
        float groupHeight = Mathf.Ceil((TitleHeight + VerticalGap + mainHeight + VerticalGap + KeyGuideHeight) * 0.5f) * 2f;

        contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.sizeDelta = new Vector2(groupWidth, groupHeight);
        contentRoot.anchoredPosition = Vector2.zero;

        float mainTop = TitleHeight + VerticalGap;
        PlaceTopLeft(bookRoot, 0f, mainTop, bookWidth, mainHeight);
        PlaceTopLeft(keyGuideRoot, 0f, mainTop + mainHeight + VerticalGap, groupWidth, KeyGuideHeight);

        ApplyStaticTexts();
        LayoutTitleBar();
        LayoutBook(bookWidth);
        float infoHeight = PrepareDescriptionPanel();
        PlaceTopLeft(infoRoot, bookWidth + PanelGap, mainTop, infoWidth, infoHeight);
        bookFinalRect = new Rect(0f, mainTop, bookWidth, mainHeight);
        infoFinalRect = new Rect(bookWidth + PanelGap, mainTop, infoWidth, infoHeight);
        RefreshInfo();
        LayoutRebuilder.ForceRebuildLayoutImmediate(keyGuideRoot);
    }

    private void ApplyStaticTexts()
    {
        titleText.text = Localize(TitleKey);
        hintMoveText.text = Localize(HintMoveKey);
        hintCloseText.text = Localize(HintCloseKey);
        emptyText.text = Localize(EmptyKey);

        labelBuilder.Clear();
        labelBuilder.Append(Localize(RareShortKey)).Append(' ').Append(rareCount).Append("  ·  ").Append(Localize(NormalShortKey)).Append(' ').Append(normalCount);
        countText.text = labelBuilder.ToString();
    }

    /// <summary>제목 줄: 왼쪽 명판 위에 제목 · 장수. 명판은 글자 폭에 맞춰 늘어난다(언어마다 길이가 다르다).</summary>
    private void LayoutTitleBar()
    {
        float titleWidth = Mathf.Ceil(titleText.GetPreferredValues(titleText.text, Unbounded, Unbounded).x);
        float countWidth = Mathf.Ceil(countText.GetPreferredValues(countText.text, Unbounded, Unbounded).x);

        PlaceTopLeft(titlePlate, 0f, 0f, TitlePlatePadding * 2f + titleWidth + TitleGap + countWidth, TitleHeight);
        PlaceTopLeft(titleText.rectTransform, TitlePlatePadding, 0f, titleWidth, TitleHeight);
        PlaceTopLeft(countText.rectTransform, TitlePlatePadding + titleWidth + TitleGap, 0f, countWidth, TitleHeight);
    }

    /// <summary>카드 목록: 마스크 영역 · 스크롤바 · 카드 격자를 놓는다. 위쪽에 카드가 올라갈 여유(HoverLiftPixels)를 둔다.</summary>
    private void LayoutBook(float _bookWidth)
    {
        viewportWidth = _bookWidth - BookPadding * 2f;
        viewportHeight = mainHeight - BookPadding * 2f;
        PlaceTopLeft(viewport, BookPadding, BookPadding, viewportWidth, viewportHeight);
        PlaceTopLeft(scrollTrack, _bookWidth - BookPadding + ScrollbarGap, BookPadding, ScrollbarWidth, viewportHeight);

        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(0f, 1f);
        scrollContent.pivot = new Vector2(0f, 1f);

        float gridWidth = columns * UI_RoguelikeOwnedCardCell.CardWidth + (columns - 1) * CellGap;
        float gridLeft = Mathf.Floor((viewportWidth - gridWidth) * 0.5f);

        float lift = UI_RoguelikeOwnedCardCell.HoverLiftPixels;
        float rowPitch = UI_RoguelikeOwnedCardCell.CardHeight + RowGap;
        int rows = (cellCount + columns - 1) / columns;

        for (int i = 0; i < cellCount; i++)
        {
            int row = i / columns;
            int column = i % columns;
            cells[i].SetPosition(new Vector2(gridLeft + column * (UI_RoguelikeOwnedCardCell.CardWidth + CellGap), -(lift + row * rowPitch)));
        }

        contentHeight = 0 < rows ? lift + rows * rowPitch - RowGap : 0f;
        emptyText.gameObject.SetActive(0 == cellCount);

        PlaceTopLeft(scrollContent, 0f, 0f, viewportWidth, Mathf.Max(contentHeight, viewportHeight));
        PlaceTopLeft(emptyText.rectTransform, 0f, 0f, viewportWidth, viewportHeight);

        ScrollTo(0f);
    }

    /// <summary>
    /// 설명 패널이 보여 줄 수 있는 모든 카드의 높이를 한 번 재서 패널 폭을 고정한다. 호버를 옮겨도 패널 위치가 변하지 않는다.
    /// 끝나면 패널을 비운다(호출한 쪽이 RefreshInfo로 현재 포커스를 다시 채운다). 설명 패널(Info)의 높이를 돌려준다 - 내용에 맞추되 목록 높이를 넘지 않는다.
    /// </summary>
    private float PrepareDescriptionPanel()
    {
        float panelWidth = infoWidth - InfoPaddingX * 2f;
        descPanel.SetMaxWidth(panelWidth);
        descPanel.SetFixedWidth(panelWidth);

        float requiredHeight = InfoPadding * 2f + UI_RoguelikeCardDescPanel.MinPanelHeight;
        for (int i = 0; i < cellCount; i++)
        {
            RoguelikeOwnedCardViewData data = cells[i].Data;
            float height = InfoPadding * 2f + descPanel.MeasureOwnedHeight(in data);
            requiredHeight = Mathf.Max(requiredHeight, height);
        }

        descPanel.Clear();

        RectTransform panelRect = (RectTransform)descPanel.transform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(InfoPaddingX, -InfoPadding);

        if (mainHeight < requiredHeight)
        {
            Debug.LogWarning("[UIView_RoguelikeOwnedCards] 설명이 화면 높이(" + mainHeight + ")보다 깁니다. 필요한 높이: " + requiredHeight);
        }

        return Mathf.Min(mainHeight, requiredHeight);
    }

    // // 설명 패널(오른쪽)

    /// <summary>포커스한 카드(없으면 마지막으로 포커스했던 카드)의 설명을 그린다. 카드가 하나도 없으면 비운다.</summary>
    private void RefreshInfo()
    {
        bool bHasCard = null != focusedCell && focusedCell.gameObject.activeSelf;
        descPanel.gameObject.SetActive(bHasCard);

        if (false == bHasCard)
        {
            descPanel.Clear();
            descPanel.gameObject.SetActive(false);
            return;
        }

        RoguelikeOwnedCardViewData data = focusedCell.Data;
        descPanel.SetOwnedCard(in data);
    }

    // // 내비게이션 · 포커스

    /// <summary>
    /// 패드·키보드 이동. Automatic은 픽셀 아트 배치에서 엉뚱한 곳으로 가므로 Explicit으로 직접 잇는다.
    /// 좌우·위아래 격자 이동이고, 마지막 줄이 짧으면 아래로 갈 때 마지막 칸으로 간다.
    /// </summary>
    private void RebuildNavigation()
    {
        for (int i = 0; i < cellCount; i++)
        {
            int row = i / columns;
            int column = i % columns;

            Selectable left = 0 < column ? cells[i - 1].SelectButton : null;
            Selectable right = column < columns - 1 && i + 1 < cellCount ? cells[i + 1].SelectButton : null;
            Selectable up = 0 < row ? cells[i - columns].SelectButton : null;

            Selectable down = null;
            if ((row + 1) * columns < cellCount) down = cells[Mathf.Min(i + columns, cellCount - 1)].SelectButton;

            SetExplicitNavigation(cells[i].SelectButton, left, right, up, down);
        }
    }

    private static void SetExplicitNavigation(Selectable _selectable, Selectable _left, Selectable _right, Selectable _up, Selectable _down)
    {
        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = _left,
            selectOnRight = _right,
            selectOnUp = _up,
            selectOnDown = _down,
        };
        _selectable.navigation = navigation;
    }

    /// <summary>
    /// 열릴 때의 첫 상태(레어가 있으면 레어 첫 칸). 패드 모드면 그 칸이 선택·강조된 채 시작한다(가이드 3-8: 패드는 선택이 있어야 조작할 수 있다).
    /// 키보드/마우스 모드면 선택하지 않는다 - 설명 패널만 첫 칸의 것을 보여 주고, 카드를 올리거나 CursorBox를 띄우지 않는다(마우스 유저에게 엉뚱한 하이라이트가 남지 않게).
    /// </summary>
    private void FocusFirst()
    {
        if (null != focusedCell) focusedCell.SetFocused(false);
        focusedCell = null;
        focusedRelay = null;
        bFocusShown = false;

        if (0 == cellCount)
        {
            RefreshInfo();
            HideCursor();
            return;
        }

        if (true == bGamepadMode && EMotion.None == motion)
        {
            SetFocusCell(cells[0], false);
            SelectInEventSystem(cells[0].Relay);
            return;
        }

        // 키보드/마우스 모드이거나 열기 연출 중이다. 연출 중 패드 모드의 포커스(선택 · 커서)는 열기가 끝난 뒤에 건다(FinishOpen).
        focusedCell = cells[0];
        focusedRelay = cells[0].Relay;
        RefreshInfo();
        HideCursor();
    }

    private void SetFocusCell(UI_RoguelikeOwnedCardCell _cell, bool _bEnsureVisible)
    {
        if (_cell == focusedCell && true == bFocusShown) return;

        if (null != focusedCell) focusedCell.SetFocused(false);
        focusedCell = _cell;
        focusedRelay = _cell.Relay;
        bFocusShown = true;
        _cell.SetFocused(true);

        RefreshInfo();
        if (true == _bEnsureVisible) ScrollToCell(_cell);
        ShowCursor(_cell.Relay);
    }

    /// <summary>
    /// 입력 장치가 바뀌었다(열려 있는 중에도 온다). 패드로 가면 포커스 칸을 선택해 스틱·십자키가 먹게 하고,
    /// 키보드/마우스로 가면 EventSystem 선택을 풀어 방향키가 아무 일도 하지 않게 한다(마우스 전용).
    /// </summary>
    private void ApplyDevice(bool _bGamepad)
    {
        bGamepadMode = _bGamepad;
        moveGuideRow.gameObject.SetActive(_bGamepad);
        LayoutRebuilder.ForceRebuildLayoutImmediate(keyGuideRoot);

        if (false == IsVisible || 0 == cellCount) return;

        // 연출 중에는 포커스를 걸지 않는다. 장치는 이미 기록했고, 열기가 끝날 때(FinishOpen) 그 장치에 맞춰 건다.
        if (EMotion.None != motion) return;

        if (true == _bGamepad)
        {
            UI_RoguelikeOwnedCardCell cell = null != focusedCell ? focusedCell : cells[0];
            SetFocusCell(cell, true);
            SelectInEventSystem(cell.Relay);
            return;
        }

        ReleaseSelection();
    }

    /// <summary>EventSystem 선택이 이 화면 안의 것이면 푼다. 선택이 없으면 방향키 이동 입력이 어디에도 닿지 않는다.</summary>
    private void ReleaseSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (null == eventSystem || null == eventSystem.currentSelectedGameObject) return;
        if (false == eventSystem.currentSelectedGameObject.transform.IsChildOf(transform)) return;

        eventSystem.SetSelectedGameObject(null);
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        ApplyDevice(EInputDeviceType.Gamepad == _device);
    }

    private void SubscribeDevice()
    {
        UnsubscribeDevice();

        inputManager = viewCtx?.inputManager;
        if (null != inputManager && null != inputManager.inputReader) inputManager.inputReader.InputDeviceChangedEvent += deviceChangedHandler;
    }

    private void UnsubscribeDevice()
    {
        if (null != inputManager && null != inputManager.inputReader) inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
        inputManager = null;
    }

    private void ShowCursor(UI_RoguelikeSelectRelay _relay)
    {
        ICursorBoxUI cursor = viewCtx?.cursorBoxUI;
        if (null == cursor) return;

        RectTransform target = _relay.CursorTarget;
        cursor.Show(target, target.rect.size + new Vector2(CursorPadding, CursorPadding), Vector2.zero, cursorMotion);
    }

    private void HideCursor()
    {
        viewCtx?.cursorBoxUI?.Hide();
    }

    private void SelectInEventSystem(UI_RoguelikeSelectRelay _relay)
    {
        EventSystem eventSystem = EventSystem.current;
        if (null == eventSystem || null == _relay) return;
        if (eventSystem.currentSelectedGameObject == _relay.gameObject) return;

        eventSystem.SetSelectedGameObject(_relay.gameObject);
    }

    private UI_RoguelikeOwnedCardCell FindCell(UI_RoguelikeSelectRelay _relay)
    {
        for (int i = 0; i < cellCount; i++)
        {
            if (cells[i].Relay == _relay) return cells[i];
        }

        return null;
    }

    // // 스크롤

    /// <summary>패드로 선택한 칸이 가려져 있으면 보이게 스크롤한다. 호버로 올라가는 만큼(HoverLiftPixels)이 보이게 위쪽 여유를 둔다.</summary>
    private void ScrollToCell(UI_RoguelikeOwnedCardCell _cell)
    {
        float cellTop = -((RectTransform)_cell.transform).anchoredPosition.y - UI_RoguelikeOwnedCardCell.HoverLiftPixels;
        float cellBottom = -((RectTransform)_cell.transform).anchoredPosition.y + UI_RoguelikeOwnedCardCell.CardHeight;
        float scroll = scrollContent.anchoredPosition.y;

        if (cellTop - ScrollMargin < scroll) ScrollTo(cellTop - ScrollMargin);
        else if (scroll + viewportHeight < cellBottom + ScrollMargin) ScrollTo(cellBottom + ScrollMargin - viewportHeight);
    }

    private void ScrollTo(float _scroll)
    {
        float max = Mathf.Max(0f, contentHeight - viewportHeight);
        scrollContent.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Clamp(_scroll, 0f, max)));
        scrollRect.velocity = Vector2.zero;
        UpdateScrollbar();
    }

    /// <summary>마우스 휠 등으로 스크롤이 바뀌었다. 정수 픽셀로 맞추고 스크롤바를 갱신한다.</summary>
    private void OnScrollChanged(Vector2 _normalized)
    {
        Vector2 position = scrollContent.anchoredPosition;
        float rounded = Mathf.Round(position.y);
        if (rounded != position.y) scrollContent.anchoredPosition = new Vector2(position.x, rounded);

        UpdateScrollbar();
    }

    /// <summary>스크롤할 내용이 없으면 스크롤바를 숨긴다. 막대 길이는 보이는 비율, 위치는 스크롤 비율이다.</summary>
    private void UpdateScrollbar()
    {
        float max = contentHeight - viewportHeight;
        bool bScrollable = 0f < max;
        scrollTrack.gameObject.SetActive(bScrollable);
        if (false == bScrollable) return;

        float trackHeight = scrollTrack.rect.height;
        float thumbHeight = Mathf.Max(MinThumbHeight, Mathf.Floor(trackHeight * viewportHeight / contentHeight));
        float ratio = Mathf.Clamp01(scrollContent.anchoredPosition.y / max);
        float thumbY = Mathf.Round((trackHeight - thumbHeight) * ratio);

        scrollThumb.anchorMin = new Vector2(0f, 1f);
        scrollThumb.anchorMax = new Vector2(0f, 1f);
        scrollThumb.pivot = new Vector2(0f, 1f);
        scrollThumb.sizeDelta = new Vector2(scrollTrack.rect.width, thumbHeight);
        scrollThumb.anchoredPosition = new Vector2(0f, -thumbY);
    }

    // // 열기 · 닫기 연출

    /// <summary>열기 연출을 시작한다. 책이 줄어들 칸(트레이)을 정하고 첫 프레임을 그린다. OnShowContent에서 motion을 먼저 Opening으로 두고 Rebuild한 뒤 부른다.</summary>
    private void BeginOpen()
    {
        motionClock = 0f;
        bCloseQueued = false;

        contentGroup.blocksRaycasts = false;
        bookMask.enabled = true;
        AttachTray();
        ResolveTrayRect();

        if (null != tray) tray.PlayOpenEffect();
        ApplyOpening(0f);
    }

    private void BeginClose()
    {
        motion = EMotion.Closing;
        motionClock = 0f;

        // 연출이 시작되면 카드 호버 · 선택 · 커서를 걷는다(접히는 카드 위에 커서가 남지 않게).
        for (int i = 0; i < cellCount; i++) cells[i].SetFocused(false);
        bFocusShown = false;
        HideCursor();
        ReleaseSelection();

        contentGroup.blocksRaycasts = false;
        bookMask.enabled = true;
        AttachTray();
        ResolveTrayRect();

        if (null != tray) tray.PlayAbsorbEffect();
        ApplyClosing(0f);
    }

    private void FinishOpen()
    {
        motion = EMotion.None;
        ResetMotionVisuals();

        if (true == bCloseQueued)
        {
            bCloseQueued = false;
            BeginClose();
            return;
        }

        // 열기 연출 중 보류해 둔 패드 포커스를 건다.
        if (true == bGamepadMode && 0 < cellCount)
        {
            UI_RoguelikeOwnedCardCell cell = null != focusedCell ? focusedCell : cells[0];
            SetFocusCell(cell, true);
            SelectInEventSystem(cell.Relay);
        }
    }

    private void FinishClose()
    {
        motion = EMotion.None;

        // 창이 꺼지기 전에 트레이를 HUD로 돌려보낸다(창과 함께 꺼지지 않게). 같은 자리라 도착 반응이 끊기지 않고 이어진다.
        DetachTray();
        base.Hide();
    }

    /// <summary>트레이가 연결되어 있고 켜져 있으면 창의 맨 위 레이어로 올린다(딤 · 책 위에 보이게). 꺼져 있으면(로비 등) 올리지 않는다.</summary>
    private void AttachTray()
    {
        if (null == tray || null == trayLayer || false == tray.gameObject.activeInHierarchy) return;

        trayLayer.SetAsLastSibling();
        tray.AttachTo(trayLayer);
    }

    private void DetachTray()
    {
        if (null != tray) tray.Detach();
    }

    private void StepMotion()
    {
        motionClock += Mathf.Min(Time.unscaledDeltaTime, RoguelikeOwnedCardsMotion.MaxFrameSeconds);
        float t = motionClock * RoguelikeOwnedCardsMotion.Speed;

        if (EMotion.Opening == motion)
        {
            if (RoguelikeOwnedCardsMotion.OpenDuration <= t) FinishOpen();
            else ApplyOpening(t);

            return;
        }

        if (RoguelikeOwnedCardsMotion.ArriveTime <= t) FinishClose();
        else ApplyClosing(t);
    }

    /// <summary>열기 _t(연출 단위) 시점의 모습: 딤 → 책 확장 → 제목 · 키 안내 → 카드 펼침 → 설명.</summary>
    private void ApplyOpening(float _t)
    {
        SetDim(RoguelikeOwnedCardsMotion.Segment(_t, 0f, OpenDimEnd));

        float bookK = RoguelikeOwnedCardsMotion.EaseOut(RoguelikeOwnedCardsMotion.Segment(_t, OpenBookStart, OpenBookEnd));
        PlaceRect(bookRoot, LerpRect(trayTargetRect, bookFinalRect, bookK));

        float titleK = RoguelikeOwnedCardsMotion.EaseOut(RoguelikeOwnedCardsMotion.Segment(_t, OpenTitleStart, OpenTitleEnd));
        titleGroup.alpha = titleK;
        keyGuideGroup.alpha = titleK;

        ApplyInfo(RoguelikeOwnedCardsMotion.EaseOut(RoguelikeOwnedCardsMotion.Segment(_t, OpenInfoStart, OpenInfoEnd)));

        for (int i = 0; i < cellCount; i++)
        {
            float from = OpenCardStart + Mathf.Min(i, MotionCellLimit - 1) * OpenCardStagger;
            SetCellReveal(cells[i], RoguelikeOwnedCardsMotion.EaseOut(RoguelikeOwnedCardsMotion.Segment(_t, from, from + OpenCardSpan)));
        }
    }

    /// <summary>닫기 _t(연출 단위) 시점의 모습: 설명 · 제목 사라짐 → 카드가 오른쪽부터 접힘 → 책이 트레이 칸으로 수렴 + 딤 걷힘.</summary>
    private void ApplyClosing(float _t)
    {
        ApplyInfo(1f - RoguelikeOwnedCardsMotion.Segment(_t, 0f, CloseInfoEnd));

        float titleK = 1f - RoguelikeOwnedCardsMotion.Segment(_t, CloseTitleStart, CloseTitleEnd);
        titleGroup.alpha = titleK;
        keyGuideGroup.alpha = titleK;

        for (int i = 0; i < cellCount; i++)
        {
            float from = (MotionCellLimit - 1 - Mathf.Min(i, MotionCellLimit - 1)) * CloseCardStagger;
            SetCellReveal(cells[i], 1f - RoguelikeOwnedCardsMotion.EaseOut(RoguelikeOwnedCardsMotion.Segment(_t, from, from + CloseCardSpan)));
        }

        float bookK = RoguelikeOwnedCardsMotion.EaseInOut(RoguelikeOwnedCardsMotion.Segment(_t, CloseBookStart, RoguelikeOwnedCardsMotion.ArriveTime));
        PlaceRect(bookRoot, LerpRect(bookFinalRect, trayTargetRect, bookK));

        SetDim(1f - RoguelikeOwnedCardsMotion.Segment(_t, CloseDimStart, RoguelikeOwnedCardsMotion.ArriveTime));
    }

    /// <summary>설명 패널의 투명도(_k)와 그에 따른 옆으로 밀림. 정수 픽셀이다.</summary>
    private void ApplyInfo(float _k)
    {
        infoGroup.alpha = _k;

        Rect rect = infoFinalRect;
        rect.x += Mathf.Round((1f - _k) * InfoSlidePixels);
        PlaceRect(infoRoot, rect);
    }

    private void SetDim(float _k)
    {
        Color color = dimColor;
        color.a = dimColor.a * _k;
        dimGraphic.color = color;
    }

    /// <summary>카드 공개 폭. 짝수 픽셀 단계로만 변한다(UI_RoguelikeCard 뒤집힘 폭 규칙).</summary>
    private static void SetCellReveal(UI_RoguelikeOwnedCardCell _cell, float _k)
    {
        int width = 2 * Mathf.RoundToInt(UI_RoguelikeOwnedCardCell.CardWidth * 0.5f * _k);
        _cell.SetRevealWidth(width);
    }

    /// <summary>연출로 바뀐 것을 모두 평상시 값으로 되돌린다(열기가 끝났을 때, 닫혀서 꺼질 때).</summary>
    private void ResetMotionVisuals()
    {
        if (false == bMotionReady) return;

        bookMask.enabled = false;
        contentGroup.blocksRaycasts = true;
        titleGroup.alpha = 1f;
        keyGuideGroup.alpha = 1f;
        infoGroup.alpha = 1f;
        dimGraphic.color = dimColor;

        PlaceRect(bookRoot, bookFinalRect);
        PlaceRect(infoRoot, infoFinalRect);
        for (int i = 0; i < cellCount; i++) cells[i].RevealFull();
    }

    /// <summary>책이 줄어들 칸(Content 왼쪽 위 기준 정수 픽셀)을 정한다. 트레이가 연결되어 있고 켜져 있으면 그 칸, 아니면 우하단 기본 칸.</summary>
    private void ResolveTrayRect()
    {
        RectTransform source = null != tray ? tray.TrayRect : null;
        Rect content = contentRoot.rect;

        if (null != source && true == source.gameObject.activeInHierarchy)
        {
            source.GetWorldCorners(cornerBuffer);
            Vector3 bottomLeft = contentRoot.InverseTransformPoint(cornerBuffer[0]);
            Vector3 topRight = contentRoot.InverseTransformPoint(cornerBuffer[2]);

            trayTargetRect = new Rect(
                Mathf.Round(bottomLeft.x - content.xMin),
                Mathf.Round(content.yMax - topRight.y),
                Mathf.Round(topRight.x - bottomLeft.x),
                Mathf.Round(topRight.y - bottomLeft.y));
            return;
        }

        Rect screen = ((RectTransform)contentRoot.parent).rect;
        float left = (screen.width - content.width) * 0.5f;
        float top = (screen.height - content.height) * 0.5f;

        trayTargetRect = new Rect(
            screen.width - FallbackTrayMargin - FallbackTrayWidth - left,
            screen.height - FallbackTrayMargin - FallbackTrayHeight - top,
            FallbackTrayWidth,
            FallbackTrayHeight);
    }

    private static Rect LerpRect(Rect _from, Rect _to, float _k)
    {
        return new Rect(
            Mathf.Round(Mathf.Lerp(_from.x, _to.x, _k)),
            Mathf.Round(Mathf.Lerp(_from.y, _to.y, _k)),
            Mathf.Round(Mathf.Lerp(_from.width, _to.width, _k)),
            Mathf.Round(Mathf.Lerp(_from.height, _to.height, _k)));
    }

    private static void PlaceRect(RectTransform _rect, Rect _placement)
    {
        PlaceTopLeft(_rect, _placement.x, _placement.y, _placement.width, _placement.height);
    }

    // // 공통

    private string Localize(string _key)
    {
        string text = null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
        return string.IsNullOrEmpty(text) ? _key : text;
    }

    /// <summary>왼쪽 위 기준(부모 왼쪽 위에서 오른쪽으로 _x, 아래로 _y) 정수 픽셀 배치. 앵커·피벗을 왼쪽 위로 맞춘다.</summary>
    private static void PlaceTopLeft(RectTransform _rect, float _x, float _y, float _width, float _height)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.sizeDelta = new Vector2(_width, _height);
        _rect.anchoredPosition = new Vector2(Mathf.Round(_x), -Mathf.Round(_y));
    }

    /// <summary>프리팹에 필요한 참조가 모두 연결되어 있는지. 구성기(LumberBoy/UI/Build Roguelike Owned Cards)를 아직 돌리지 않은 프리팹이면 거짓이다.</summary>
    private bool IsPrefabConfigured()
    {
        return null != contentRoot && null != dimButton && null != titleText && null != countText
            && null != bookRoot && null != viewport && null != scrollContent && null != scrollRect && null != scrollTrack && null != scrollThumb
            && null != cellPrefab && null != emptyText
            && null != infoRoot && null != descPanel
            && null != titlePlate && null != keyGuideRoot && null != moveGuideRow && null != hintMoveText && null != hintCloseText;
    }

    private void UnsubscribeLanguage()
    {
        if (null != localizationManager) localizationManager.OnLanguageChanged -= languageChangedHandler;
    }

    // // 이벤트 핸들러

    private void OnLanguageChanged()
    {
        LayoutAll();
    }

    private void OnRelayFocused(UI_RoguelikeSelectRelay _relay)
    {
        if (EMotion.None != motion) return;

        UI_RoguelikeOwnedCardCell cell = FindCell(_relay);
        if (null == cell) return;

        SetFocusCell(cell, false == _relay.IsPointerFocus);

        // 마우스 호버로는 선택을 잡지 않는다 - 선택이 잡히면 방향키가 움직이기 시작한다. 패드 모드에서만 선택을 맞춘다.
        if (true == bGamepadMode) SelectInEventSystem(_relay);
    }

    private void OnDimClicked()
    {
        Hide();
    }

    protected override void OnShowContent()
    {
        if (false == bReady) return;

        UnsubscribeLanguage();
        localizationManager = viewCtx?.localizationManager;
        if (null != localizationManager) localizationManager.OnLanguageChanged += languageChangedHandler;

        // 키 아이콘이 현재 입력 장치(키보드/패드)를 따라 바뀌도록 입력 매니저를 알려 준다.
        SubscribeDevice();
        if (null != inputManager)
        {
            for (int i = 0; i < keyGuideImages.Length; i++) keyGuideImages[i].Initialize(inputManager);
        }

        // 이벤트는 "변화"만 알려 주므로 열린 시점의 현재 장치를 한 번 직접 반영한다(가이드 3-1).
        bGamepadMode = null != inputManager && inputManager.IsGamepadMode;
        moveGuideRow.gameObject.SetActive(bGamepadMode);

        // 열기 연출: 포커스 · 커서는 연출이 끝난 뒤에 걸리도록 motion을 먼저 정하고 그린다. 책이 줄어들 칸은 배치가 끝난 뒤 정한다.
        if (true == bMotionReady) motion = EMotion.Opening;

        Rebuild();

        if (true == bMotionReady) BeginOpen();
    }

    protected override void OnHideContent()
    {
        motion = EMotion.None;
        bCloseQueued = false;
        DetachTray();
        ResetMotionVisuals();

        for (int i = 0; i < cellCount; i++) cells[i].SetFocused(false);
        focusedCell = null;
        focusedRelay = null;
        bFocusShown = false;
        UnsubscribeLanguage();
        UnsubscribeDevice();

        // 닫힌 화면 안의 오브젝트가 EventSystem 선택으로 남지 않게 정리한다(비활성 오브젝트가 선택된 채로 남으면 다른 UI의 패드 조작이 먹통이 될 수 있다).
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && null != eventSystem.currentSelectedGameObject && eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
        {
            eventSystem.SetSelectedGameObject(null);
        }

        HideCursor();
        ClosedEvent?.Invoke();
    }

    // // 유니티 라이프사이클

    protected override void Awake()
    {
        base.Awake();

        bReady = IsPrefabConfigured();
        if (false == bReady)
        {
            Debug.LogError("[UIView_RoguelikeOwnedCards] 프리팹이 아직 구성되지 않았습니다. 메뉴 LumberBoy/UI/Build Roguelike Owned Cards를 실행하세요.");
            return;
        }

        languageChangedHandler = OnLanguageChanged;
        deviceChangedHandler = OnDeviceChanged;
        cursorMotion = CursorMotionSettings.Card;

        dimButton.onClick.AddListener(OnDimClicked);

        // 연출용 참조가 모두 있으면 연출을 켠다. 옛 프리팹(구성기를 아직 안 돌림)이면 연출 없이 예전처럼 바로 열고 닫힌다.
        dimGraphic = dimButton.targetGraphic;
        bMotionReady = null != dimGraphic && null != bookMask && null != contentGroup && null != titleGroup && null != infoGroup && null != keyGuideGroup;
        if (true == bMotionReady) dimColor = dimGraphic.color;

        scrollRect.scrollSensitivity = WheelSensitivity;
        scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    public override void Update()
    {
        base.Update();

        if (false == IsVisible) return;

        if (EMotion.None != motion)
        {
            StepMotion();
            return;
        }

        // 키보드/마우스 모드는 마우스 전용이다. 카드를 눌러도 UGUI가 그 버튼을 선택하므로, 선택이 생기면 풀어 방향키가 먹지 않게 한다.
        if (false == bGamepadMode)
        {
            ReleaseSelection();
            return;
        }

        // 패드 모드: 선택 중이던 것이 비활성화되면 EventSystem 선택이 null이 되어 패드 조작이 먹통이 된다. 포커스가 있으면 되돌려 준다.
        if (null == focusedRelay || false == focusedRelay.gameObject.activeInHierarchy) return;

        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && null == eventSystem.currentSelectedGameObject)
        {
            eventSystem.SetSelectedGameObject(focusedRelay.gameObject);
        }
    }

    public override void OnDestroy()
    {
        DetachTray();
        ClosedEvent = null;
        UnsubscribeLanguage();
        UnsubscribeDevice();
        base.OnDestroy();
    }
}
