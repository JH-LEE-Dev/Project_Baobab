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
///   오른쪽    포커스한 카드의 설명 패널(이름 · 레벨 · 설명 · "현재 효과"). 카드 그림은 목록의 카드가 이미 보여 주므로 따로 그리지 않는다.
///   아래      키 안내 한 줄. 아이콘은 현재 입력 장치(키보드/패드)를 따라 바뀐다(UI_KeyboardImage - 옵션·특성 UI와 같은 방식).
///   설명은 잘라 쓰지 않는다: 모든 보유 카드의 설명 높이를 열 때 한 번 재서 패널 크기를 고정하고(호버를 옮겨도 화면이 움직이지 않는다),
///   화면 높이에 들어가지 않는 카드가 있으면 경고를 남긴다.
///
/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-8] 키보드/마우스 모드는 마우스 전용이다: 호버가 포커스(카드가 올라가고 CursorBox가 따라간다)이고,
///   EventSystem 선택은 잡지 않아 방향키가 아무 일도 하지 않는다. 패드 모드는 첫 칸을 선택한 채 열리고 스틱·십자키로 격자를 이동한다.
///   장치는 열려 있는 중에도 InputDeviceChangedEvent로 바뀐다. 설명은 마지막으로 포커스한 칸의 것이 남는다.
///   패드 선택이 가려진 칸으로 가면 그 칸이 보이게 스크롤한다(마우스 호버로는 스크롤하지 않는다). 카드를 누르는 동작은 없다(설명은 포커스만으로 나온다).
///   키 안내 줄: 키보드 = [Esc] 닫기, 패드 = [스틱] 이동 [B] 닫기(이동 안내는 패드 모드에서만 보인다).
/// </summary>
public class UIView_RoguelikeOwnedCards : UIView_RoguelikeModalBase
{
    // 화면 배치(Content 왼쪽 위 기준 정수 픽셀). 640×360 기준: 가장자리 8 + 목록 288 + 8 + 설명 328 + 가장자리 8.
    private const float GroupMaxWidth = 624f;
    private const float BookMaxWidth = 288f;
    private const float MinBookWidth = 120f;
    private const float PanelGap = 8f;
    private const float MinInfoWidth = 190f;
    private const float ScreenMargin = 8f;
    private const float VerticalGap = 4f;
    private const float TitleHeight = 20f;
    private const float KeyGuideHeight = 18f;
    private const float MaxMainHeight = 300f;
    private const float MinMainHeight = 120f;
    private const float TitleGap = 8f;
    private const float TitlePlatePadding = 8f;

    // 카드 목록(왼쪽)
    private const float BookPadding = 8f;
    private const float ScrollbarWidth = 3f;
    private const float ScrollbarGap = 3f;
    private const float MinThumbHeight = 8f;
    private const float CellGap = 4f;
    private const float RowGap = 8f;
    private const float ScrollMargin = 4f;
    // 마우스 휠 한 칸의 스크롤 양. 프로젝트의 다른 ScrollRect(UI_Option)와 같은 값이다.
    private const float WheelSensitivity = 2f;

    // 설명(오른쪽)
    private const float InfoPadding = 6f;
    private const float InfoPaddingX = 8f;
    private const float FooterReserve = 26f;
    private const int CursorPadding = 4;
    private const float Unbounded = 10000f;

    // 문구 키(RoguelikeUI.json)
    private const string TitleKey = "Roguelike_OwnedCards_Title";
    private const string RareShortKey = "Roguelike_OwnedCards_RareShort";
    private const string NormalShortKey = "Roguelike_OwnedCards_NormalShort";
    private const string NextLevelHintKey = "Roguelike_OwnedCards_NextLevelHint";
    private const string EmptyKey = "Roguelike_OwnedCards_Empty";
    private const string HintMoveKey = "Roguelike_OwnedCards_HintMove";
    private const string HintCloseKey = "Roguelike_OwnedCards_HintClose";

    public event Action ClosedEvent;

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
    [SerializeField] private TMP_Text footerText;

    [Header("키 안내 줄")]
    [SerializeField] private RectTransform keyGuideRoot;
    [SerializeField] private RectTransform moveGuideRow;
    [SerializeField] private TMP_Text hintMoveText;
    [SerializeField] private TMP_Text hintCloseText;
    [SerializeField] private UI_KeyboardImage[] keyGuideImages = new UI_KeyboardImage[0];

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
        float bookWidth = BookMaxWidth;
        infoWidth = groupWidth - bookWidth - PanelGap;
        if (infoWidth < MinInfoWidth)
        {
            infoWidth = MinInfoWidth;
            bookWidth = Mathf.Max(MinBookWidth, groupWidth - PanelGap - infoWidth);
        }

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
        PlaceTopLeft(infoRoot, bookWidth + PanelGap, mainTop, infoWidth, mainHeight);
        PlaceTopLeft(keyGuideRoot, 0f, mainTop + mainHeight + VerticalGap, groupWidth, KeyGuideHeight);

        ApplyStaticTexts();
        LayoutTitleBar();
        LayoutBook(bookWidth);
        PrepareDescriptionPanel();
        RefreshInfo();
        LayoutRebuilder.ForceRebuildLayoutImmediate(keyGuideRoot);
    }

    private void ApplyStaticTexts()
    {
        titleText.text = Localize(TitleKey);
        hintMoveText.text = Localize(HintMoveKey);
        hintCloseText.text = Localize(HintCloseKey);
        emptyText.text = Localize(EmptyKey);
        footerText.text = Localize(NextLevelHintKey);

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
        viewportWidth = _bookWidth - BookPadding * 2f - ScrollbarWidth - ScrollbarGap;
        viewportHeight = mainHeight - BookPadding * 2f;
        PlaceTopLeft(viewport, BookPadding, BookPadding, viewportWidth, viewportHeight);
        PlaceTopLeft(scrollTrack, _bookWidth - BookPadding - ScrollbarWidth, BookPadding, ScrollbarWidth, viewportHeight);

        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(0f, 1f);
        scrollContent.pivot = new Vector2(0f, 1f);

        columns = Mathf.Max(1, Mathf.FloorToInt((viewportWidth + CellGap) / (UI_RoguelikeOwnedCardCell.CardWidth + CellGap)));
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
    /// 푸터("같은 카드를 다시 고르면…")는 레벨이 더 오를 수 있는 카드에만 뜨므로 그 카드의 필요 높이에만 더한다.
    /// 끝나면 패널을 비운다(호출한 쪽이 RefreshInfo로 현재 포커스를 다시 채운다).
    /// </summary>
    private void PrepareDescriptionPanel()
    {
        float panelWidth = infoWidth - InfoPaddingX * 2f;
        descPanel.SetMaxWidth(panelWidth);
        descPanel.SetFixedWidth(panelWidth);

        float requiredHeight = InfoPadding * 2f + UI_RoguelikeCardDescPanel.MinPanelHeight;
        for (int i = 0; i < cellCount; i++)
        {
            RoguelikeOwnedCardViewData data = cells[i].Data;
            float height = InfoPadding * 2f + descPanel.MeasureOwnedHeight(in data) + (HasNextLevelHint(in data) ? FooterReserve : 0f);
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

        PlaceTopLeft(footerText.rectTransform, InfoPaddingX, mainHeight - FooterReserve - InfoPadding, panelWidth, FooterReserve);
    }

    // // 설명 패널(오른쪽)

    /// <summary>포커스한 카드(없으면 마지막으로 포커스했던 카드)의 설명을 그린다. 카드가 하나도 없으면 비운다.</summary>
    private void RefreshInfo()
    {
        bool bHasCard = null != focusedCell && focusedCell.gameObject.activeSelf;
        descPanel.gameObject.SetActive(bHasCard);
        footerText.gameObject.SetActive(false);

        if (false == bHasCard)
        {
            descPanel.Clear();
            descPanel.gameObject.SetActive(false);
            return;
        }

        RoguelikeOwnedCardViewData data = focusedCell.Data;
        descPanel.SetOwnedCard(in data);
        footerText.gameObject.SetActive(HasNextLevelHint(in data));
    }

    /// <summary>같은 카드를 다시 고르면 레벨이 오르는 카드인가(액티브·패시브이고 최대 레벨 전).</summary>
    private static bool HasNextLevelHint(in RoguelikeOwnedCardViewData _data)
    {
        bool bLevelable = (ECardKind.Active == _data.kind || ECardKind.Passive == _data.kind) && 1 < _data.maxLevel;
        return bLevelable && _data.level < _data.maxLevel;
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

        if (true == bGamepadMode)
        {
            SetFocusCell(cells[0], false);
            SelectInEventSystem(cells[0].Relay);
            return;
        }

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
            && null != infoRoot && null != descPanel && null != footerText
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

        Rebuild();
    }

    protected override void OnHideContent()
    {
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

        scrollRect.scrollSensitivity = WheelSensitivity;
        scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    public override void Update()
    {
        base.Update();

        if (false == IsVisible) return;

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
        ClosedEvent = null;
        UnsubscribeLanguage();
        UnsubscribeDevice();
        base.OnDestroy();
    }
}
