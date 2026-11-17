using System;
using System.Collections.Generic;
using DG.Tweening;
using PresentationLayer.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 도전과제 창. 메인메뉴의 로그라이크 하위 메뉴 "도전과제"에서 연다. 기록 창(UIView_RoguelikeRecords)과 같은 판 디자인이다.
/// 창은 RoguelikeChallengeViewData 목록을 받아 그리기만 한다(정의 · 달성 판정 · 저장은 Application Layer 몫). 목록이 비면 안내 문구를 보인다.
///
/// [구성] 어두운 딤 위 가운데 한 장(Panel_Book 360x282): 위쪽 가장자리의 명판(폭 272, "도전과제") + 본문 340x250(세로 ScrollRect) + 오른쪽 여백의 세로 스크롤바.
///   탭이 하나뿐이라 화살표 · LB/RB · 점 · 카드 띠는 없다. 한 칸은 UI_RoguelikeChallengeRow(높이 62, 간격 4 - 한 화면 약 4칸)이다.
///   뒤로 버튼(BTN_Back)은 옵션 창 BTN_Close와 같은 모양(UI_OptionButton)으로 판 아래 테두리 바깥, 판 왼쪽 끝에 놓는다. 패드 모드에서만 버튼 오른쪽에 B 아이콘이 뜬다.
///
/// [조작 - ControllerSupport_UI가이드.md 3-7 · 3-8] 닫기 = 딤 클릭 · 뒤로 버튼 · 패드 B(UICancelEvent) · Esc(SetStandaloneClose(true)로 직접 받는다 - 메인메뉴는 깊이 관리가 없다).
///   입력 모드 UI는 모달 공통(RoguelikeModalInput)이 건다. 패드: 패드 모드로 열릴 때 · 패드로 바뀔 때만 첫 칸 선택, 위 / 아래 Explicit 탐색, 선택된 칸이 보이도록 스크롤,
///   선택이 null이 되면 복구, 마우스 모드로 바뀌면 선택 해제, 닫을 때 선택 비움. 포커스 표시는 커서 상자(viewCtx.cursorBoxUI)이고 칸을 눌러도 하는 일은 없다.
///   마우스 모드의 호버는 기록 창과 같다: 포인터가 올라간 칸은 hoveredRow에 기록만 하고, 그 칸이 본문 뷰포트 안에 온전히 보이며 본문이 멈춰 있을 때만 "인정"해 효과음 · 커서를 낸다(이미 인정된 칸은 온전히 보이는 동안 스크롤 중에도 유지하고, 효과음은 마우스가 그 칸에 들어온 뒤 1번만 낸다).
/// </summary>
public class UIView_RoguelikeChallenges : UIView_RoguelikeModalBase
{
    // 판 크기는 기록 창과 같다. 칸 높이는 최소 62(빌더의 ChallengeRowHeight와 같은 값)이고, 제목 높이 · 팁 줄 수에 맞춰 칸마다 늘어난다(UI_RoguelikeChallengeRow.FitHeight).
    private const float RowHeight = 62f;
    private const float RowGap = 4f;
    // 18개 언어 규격 명판 최소 폭(기록 창 탭 명판과 같은 272). 글자가 더 넓으면 글자 폭 + 양쪽 여백 만큼 넓힌다.
    private const float SpecPlateWidth = 272f;
    private const float PlatePadding = 24f;

    // 칸 맞춤 · 자동 스크롤 미끄러짐(unscaled).
    private const float SlideSeconds = 0.12f;
    private const float SlideEpsilon = 0.01f;
    // 본문 세로 휠 감도: 입력 모듈은 휠 한 칸을 scrollDelta 6으로 주므로 6 x 8/3 = 한 칸 16px(기록 창과 같다).
    private const float BodyWheelSensitivity = 8f / 3f;
    // 커서 상자는 대상보다 사방 2px 크다.
    private const float CursorPadding = 4f;

    private const float DimFadeSeconds = 0.18f;
    private const float PanelEnterSeconds = 0.2f;
    private const float PanelEnterOffset = 6f;
    private const int InitialRowCapacity = 8;

    // 달성 칸 파티클 연출: 첫 달성 칸은 0.25초 뒤, 이후 달성 칸마다 0.14초씩 늦게 시작한다.
    private const float ClearGlowFirstDelay = 0.25f;
    private const float ClearGlowStagger = 0.14f;
    private const int ClearShardSeedStep = 977;
    private const int ClearShardSeedBase = 13;

    /// <summary>자동 스크롤의 짧은 미끄러짐(OutCubic, unscaled 시간). 값 형식이라 할당이 없다.</summary>
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
    private const string TitleKey = "Roguelike_Menu_Challenges";
    private const string EmptyKey = "Roguelike_Challenges_Empty";

    public event Action ClosedEvent;

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Challenges)
    [Header("화면")]
    [SerializeField] private CanvasGroup dimGroup;
    [SerializeField] private Button dimButton;
    [SerializeField] private RectTransform panelRoot;

    [Header("명판")]
    [SerializeField] private RectTransform titlePlate;
    [SerializeField] private TMP_Text titleText;

    [Header("본문")]
    [SerializeField] private RectTransform rowRoot;
    [SerializeField] private UI_RoguelikeChallengeRow rowTemplate;
    [SerializeField] private TMP_Text emptyText;

    [Header("본문 세로 스크롤")]
    [SerializeField, Tooltip("본문(340x250 마스크)의 세로 ScrollRect. rowRoot(칸 묶음)가 content다.")] private ScrollRect bodyScroll;
    [SerializeField] private RectTransform bodyViewport;
    [SerializeField, Tooltip("본문 오른쪽 여백의 세로 스크롤바(보유 카드 창과 같은 그림). 내용이 넘칠 때만 보인다.")] private UI_RoguelikeScrollbar bodyBar;
    [SerializeField, Tooltip("달성 칸에서 튀는 조각 공용 층(판 아래, 본문 마스크 밖).")] private UI_ChallengeClearShards clearShards;

    [Header("뒤로 · 패드 아이콘")]
    [SerializeField, Tooltip("판 아래 왼쪽의 뒤로 버튼(옵션 BTN_Close와 같은 UI_OptionButton). 마우스 클릭용 - 패드는 B 단축키.")]
    private UI_OptionButton backButton;
    [SerializeField, Tooltip("패드 모드에서만 켜는 아이콘 묶음(뒤로 버튼의 B). 키보드 모드에서는 끈다.")]
    private RectTransform[] padOnlyItems = new RectTransform[0];
    [SerializeField] private UI_KeyboardImage[] keyGuideImages = new UI_KeyboardImage[0];

    //내부 의존성
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private readonly List<RoguelikeChallengeViewData> challenges = new List<RoguelikeChallengeViewData>(InitialRowCapacity);
    private readonly List<UI_RoguelikeChallengeRow> rows = new List<UI_RoguelikeChallengeRow>(InitialRowCapacity);
    private CursorMotionSettings rowCursorMotion;

    //상태 변수
    private bool bStandaloneClose;
    private Vector2 panelBasePosition;
    private bool bPanelBaseCaptured;
    private float bodyContentHeight;
    private ScrollSlide bodySlide;
    private UI_RoguelikeChallengeRow focusedRow;
    private UI_RoguelikeChallengeRow hoveredRow;
    private UI_RoguelikeChallengeRow acceptedRow;
    private UI_RoguelikeChallengeRow soundedRow;
    private float lastBodyScroll;
    private RectTransform cursorTarget;
    private CursorMotionSettings cursorMotion;
    private bool bCursorClipped;
    private readonly Vector3[] targetCorners = new Vector3[4];
    private readonly Vector3[] clipCorners = new Vector3[4];

    // 캐싱 델리게이트
    private Action cancelHandler;
    private Action escHandler;
    private Action languageChangedHandler;
    private Action<EInputDeviceType> deviceChangedHandler;
    private Action<UI_RoguelikeChallengeRow, bool> rowFocusedHandler;
    private Action<UI_RoguelikeChallengeRow> rowExitedHandler;
    private UnityEngine.Events.UnityAction<Vector2> bodyScrolledHandler;
    private Action<float> bodyBarHandler;

    private bool IsGamepadMode => null != inputManager && true == inputManager.IsGamepadMode;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = viewCtx?.localizationManager;
        inputManager = viewCtx?.inputManager;

        cancelHandler = RequestClose;
        escHandler = RequestClose;
        rowFocusedHandler = OnRowFocused;
        rowExitedHandler = OnRowExited;
        bodyScrolledHandler = OnBodyScrolled;
        bodyBarHandler = OnBodyBarRequested;
        rowCursorMotion = CursorMotionSettings.RowSubtle;
        languageChangedHandler = OnLanguageChanged;
        deviceChangedHandler = OnDeviceChanged;

        if (null != dimButton)
        {
            dimButton.onClick.RemoveListener(RequestClose);
            dimButton.onClick.AddListener(RequestClose);
        }

        for (int i = 0; keyGuideImages.Length > i; i++)
        {
            if (null != keyGuideImages[i]) keyGuideImages[i].Initialize(inputManager);
        }

        if (null != backButton) backButton.Initialize(RequestClose, SoundID.MainMenuDot01, SoundID.None);

        if (null != rowTemplate) rowTemplate.gameObject.SetActive(false);
        if (null != bodyScroll)
        {
            bodyScroll.scrollSensitivity = BodyWheelSensitivity;
            bodyScroll.onValueChanged.RemoveListener(bodyScrolledHandler);
            bodyScroll.onValueChanged.AddListener(bodyScrolledHandler);
        }
        if (null != bodyBar)
        {
            bodyBar.ScrollRequestedEvent -= bodyBarHandler;
            bodyBar.ScrollRequestedEvent += bodyBarHandler;
        }
    }

    /// <summary>깊이 관리(ESC 닫기)가 없는 화면(메인메뉴)에서 열 때 켠다. 켜면 키보드 Esc를 이 창이 직접 받아 닫는다.</summary>
    public void SetStandaloneClose(bool _bStandalone)
    {
        bStandaloneClose = _bStandalone;
    }

    /// <summary>목록을 복사해 창을 열고 등장 연출을 시작한다. 목록이 비면(null 포함) 안내 문구가 나온다.</summary>
    public void OpenChallenges(IReadOnlyList<RoguelikeChallengeViewData> _challenges)
    {
        challenges.Clear();
        if (null != _challenges)
        {
            for (int i = 0; _challenges.Count > i; i++) challenges.Add(_challenges[i]);
        }

        Show();
        Sound.PlayUI(SoundID.ResultUIOpen);

        ResetHover();
        ApplyStaticTexts();
        RefreshPadGuide();
        RefreshRows();
        ResetBodyScroll();
        PlayClearGlows();
        PlayEnter();
        if (true == IsGamepadMode) SelectFirstRow();
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
        focusedRow = null;
        ReleaseSelection();
        ResetHover();
        HideCursor();
        bodySlide.Cancel();
        if (null != bodyScroll) bodyScroll.StopMovement();
        if (null != rowRoot) SetBodyScroll(0f);
        if (null != dimGroup) dimGroup.DOKill();
        if (null != panelRoot) panelRoot.DOKill();
        StopClearGlows();
        ClosedEvent?.Invoke();
    }

    // // 달성 칸 파티클 연출

    /// <summary>
    /// 창이 열릴 때 1회: 켜진 칸을 위에서부터 훑어 달성 칸마다 순번(order)대로 지연을 주어 연출을 켜고,
    /// 그 칸이 본문 뷰포트에 일부라도 보이면 튀는 조각을 예약한다(조각 층은 판 아래라 본문 마스크에 잘리지 않는다).
    /// </summary>
    private void PlayClearGlows()
    {
        if (null != clearShards) clearShards.Clear();

        int order = 0;
        for (int i = 0; rows.Count > i; i++)
        {
            UI_RoguelikeChallengeRow row = rows[i];
            if (false == row.gameObject.activeSelf || false == row.IsCleared)
            {
                row.StopClearGlow();
                continue;
            }

            float delay = ClearGlowFirstDelay + order * ClearGlowStagger;
            row.PlayClearGlow(delay, order, true);

            if (null != clearShards && true == IsRowOverlappingBody(row.Rect))
            {
                Vector3 local = clearShards.transform.InverseTransformPoint(row.Rect.position);
                clearShards.Burst(new Vector2(Mathf.Round(local.x), Mathf.Round(local.y)), Mathf.Round(row.Rect.rect.width), Mathf.Round(row.Rect.rect.height), delay, ClearShardSeedBase + order * ClearShardSeedStep);
            }

            order++;
        }
    }

    private void StopClearGlows()
    {
        for (int i = 0; rows.Count > i; i++) rows[i].StopClearGlow();
        if (null != clearShards) clearShards.Clear();
    }

    /// <summary>매 프레임(unscaled): 조각 층과, 본문 뷰포트와 세로로 겹치는 달성 칸만 30fps로 진행한다. 가려진 칸은 멈췄다가 다시 보이면 이어서 돈다.</summary>
    private void TickClearGlows()
    {
        float deltaTime = Time.unscaledDeltaTime;
        if (null != clearShards) clearShards.Tick(deltaTime);

        for (int i = 0; rows.Count > i; i++)
        {
            UI_RoguelikeChallengeRow row = rows[i];
            if (false == row.gameObject.activeSelf || false == row.IsCleared) continue;
            if (false == IsRowOverlappingBody(row.Rect)) continue;

            row.TickClearGlow(deltaTime);
        }
    }

    /// <summary>칸이 본문 뷰포트와 세로로 겹치는지 본다(칸 위치와 스크롤 값으로 계산, 월드 코너를 쓰지 않는다).</summary>
    private bool IsRowOverlappingBody(RectTransform _row)
    {
        if (null == rowRoot || null == bodyViewport) return true;

        float top = rowRoot.anchoredPosition.y + _row.anchoredPosition.y;
        return top > -bodyViewport.rect.height && top - _row.rect.height < 0f;
    }

    // // 칸 · 배치 (정수 픽셀)

    private UI_RoguelikeChallengeRow GetRow(int _index)
    {
        while (rows.Count <= _index && null != rowTemplate && null != rowRoot)
        {
            UI_RoguelikeChallengeRow row = Instantiate(rowTemplate, rowRoot);
            row.Initialize();
            row.FocusedEvent += rowFocusedHandler;
            row.ExitedEvent += rowExitedHandler;
            rows.Add(row);
        }
        return rows[_index];
    }

    /// <summary>목록대로 칸을 채운다. 칸은 풀로 돌려 쓰고(모자랄 때만 만들고 남는 칸은 끈다) 비면 안내 문구를 켠다.</summary>
    private void RefreshRows()
    {
        int count = challenges.Count;
        for (int i = 0; count > i; i++)
        {
            UI_RoguelikeChallengeRow row = GetRow(i);
            if (null == row) { count = i; break; }

            RoguelikeChallengeViewData data = challenges[i];
            row.SetChallenge(in data);
        }

        for (int i = 0; rows.Count > i; i++) rows[i].gameObject.SetActive(i < count);
        if (null != emptyText) emptyText.gameObject.SetActive(0 == count);

        LayoutRows();
    }

    private void LayoutRows()
    {
        if (null == rowRoot) return;

        float width = Mathf.Round(rowRoot.rect.width);
        float y = 0f;
        UI_RoguelikeChallengeRow previous = null;
        for (int i = 0; rows.Count > i; i++)
        {
            if (false == rows[i].gameObject.activeSelf) continue;

            RectTransform rect = rows[i].Rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            float height = rows[i].FitHeight(RowHeight);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, -y);
            y += height + RowGap;

            SetVerticalNavigation(previous, rows[i]);
            previous = rows[i];
        }

        if (null != previous) SetVerticalNavigation(previous, null);

        // 판은 항상 같은 크기다. 내용이 본문 높이를 넘을 때만 세로 스크롤 + 스크롤바가 켜진다.
        bodyContentHeight = Mathf.Max(0f, y - RowGap);
        float viewportHeight = null != bodyViewport ? bodyViewport.rect.height : bodyContentHeight;
        rowRoot.sizeDelta = new Vector2(width, Mathf.Max(bodyContentHeight, viewportHeight));
        if (null != bodyScroll) bodyScroll.vertical = bodyContentHeight > viewportHeight + SlideEpsilon;
        ClampBodyScroll();
        UpdateBodyBar();
    }

    /// <summary>칸 탐색(Explicit): 보이는 칸끼리 위 / 아래로만 잇는다. _upper의 아래는 _lower, _lower의 위는 _upper다. 목록의 양 끝은 null.</summary>
    private static void SetVerticalNavigation(UI_RoguelikeChallengeRow _upper, UI_RoguelikeChallengeRow _lower)
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

    private void LayoutTitlePlate()
    {
        if (null == titlePlate || null == titleText) return;

        float textWidth = Mathf.Ceil(titleText.GetPreferredValues(titleText.text, 10000f, 10000f).x);
        float width = Mathf.Max(SpecPlateWidth, textWidth + PlatePadding * 2f);
        int even = Mathf.CeilToInt(width);
        if (0 != even % 2) even++;
        titlePlate.sizeDelta = new Vector2(even, titlePlate.sizeDelta.y);
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

    /// <summary>딤 클릭 · 뒤로 버튼 · 패드 B(UICancelEvent) · 메인메뉴의 Esc.</summary>
    private void RequestClose()
    {
        if (false == IsVisible) return;

        Hide();
    }

    /// <summary>닫힘 효과음은 여기서 낸다(어떤 경로로 닫아도 한 번).</summary>
    public override void Hide()
    {
        if (true == IsVisible) Sound.PlayUI(SoundID.MainClick);
        base.Hide();
    }

    private void ApplyStaticTexts()
    {
        if (null != titleText) titleText.text = Localize(TitleKey);
        if (null != emptyText) emptyText.text = Localize(EmptyKey);
        LayoutTitlePlate();
    }

    /// <summary>뒤로 버튼의 B 아이콘은 패드 모드에서만 보인다. 판 · 명판 크기는 그대로 둔다.</summary>
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
        if (false == IsVisible) return;
        RefreshPadGuide();

        if (EInputDeviceType.Gamepad == _device)
        {
            ResetHover();
            RestoreSelection();
            return;
        }

        ReleaseSelection();
        HideCursor();
    }

    /// <summary>
    /// 제목 · 팁은 호출한 쪽이 번역해 넘긴 문자열이라 여기서는 명판과 안내 문구만 새 언어로 바꾸고, 칸 높이를 다시 맞춘다.
    /// LocalizationManager.SetLanguage가 폰트 교체(FontLocalizer.SetLanguage)를 마친 뒤 이 이벤트를 부르므로 새 폰트 기준으로 잴 수 있다.
    /// </summary>
    private void OnLanguageChanged()
    {
        if (false == IsVisible) return;
        ApplyStaticTexts();

        // 칸 높이가 바뀌면 커서 상자 크기도 달라지므로 숨긴 뒤 다시 띄운다(마우스는 UpdateMouseHover가 다시 인정한다).
        LayoutRows();
        acceptedRow = null;
        HideCursor();
        if (true == IsGamepadMode) RestoreSelection();
    }

    private string Localize(string _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
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

    // // 스크롤 (본문 세로)

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

    private void OnBodyBarRequested(float _ratio)
    {
        if (null == rowRoot || null == bodyViewport) return;

        float max = Mathf.Max(0f, bodyContentHeight - bodyViewport.rect.height);
        bodySlide.Cancel();
        if (null != bodyScroll) bodyScroll.StopMovement();
        SetBodyScroll(Mathf.Round(_ratio * max));
    }

    private void SetBodyScroll(float _scroll)
    {
        if (null == rowRoot) return;

        rowRoot.anchoredPosition = new Vector2(rowRoot.anchoredPosition.x, _scroll);
        UpdateBodyBar();
    }

    /// <summary>본문 스크롤을 맨 위로 되돌린다(창을 열 때).</summary>
    private void ResetBodyScroll()
    {
        bodySlide.Cancel();
        if (null != bodyScroll) bodyScroll.StopMovement();
        SetBodyScroll(0f);
    }

    /// <summary>내용이 줄어 스크롤 범위를 벗어난 본문 위치를 범위 안으로 돌린다.</summary>
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

    /// <summary>매 프레임(unscaled): 미끄러짐을 진행하고, 본문은 멈춘 뒤 정수 픽셀로 맞춘다. 끌기 · 관성 중에는 위치를 건드리지 않아 포인터를 1:1로 따라간다.</summary>
    private void AdvanceScroll()
    {
        if (true == bodySlide.bActive)
        {
            SetBodyScroll(bodySlide.Advance(Time.unscaledDeltaTime));
            return;
        }

        if (null == bodyScroll || null == rowRoot || 1f <= bodyScroll.velocity.sqrMagnitude) return;

        float scroll = rowRoot.anchoredPosition.y;
        float rounded = Mathf.Round(scroll);
        if (rounded != scroll && 0f <= scroll && scroll <= Mathf.Max(0f, bodyContentHeight - (null != bodyViewport ? bodyViewport.rect.height : 0f))) SetBodyScroll(rounded);
    }

    // // 포커스 · 선택

    /// <summary>
    /// 칸에 마우스가 올라왔거나(_bPointer) 패드 선택이 옮겨 왔다.
    /// 마우스가 올라온 것은 hoveredRow로만 기록하고 반응(효과음 · 커서)은 UpdateMouseHover가 칸이 온전히 보이고 본문이 멈췄을 때 한다.
    /// </summary>
    private void OnRowFocused(UI_RoguelikeChallengeRow _row, bool _bPointer)
    {
        if (true == _bPointer) hoveredRow = _row;
        if (false == IsGamepadMode) return;

        ApplyRowFocus(_row);
    }

    /// <summary>마우스가 칸에서 나갔다: 호버 기록과 효과음을 낸 칸 기록을 비운다(다시 들어오면 효과음이 난다).</summary>
    private void OnRowExited(UI_RoguelikeChallengeRow _row)
    {
        if (_row == hoveredRow) hoveredRow = null;
        if (_row == soundedRow) soundedRow = null;
    }

    /// <summary>
    /// 마우스 모드의 호버 인정. 마우스가 올라간 칸이 본문 뷰포트 안에 온전히 보이고 칸이 켜져 있을 때만 그 칸을 인정한다.
    /// 새 칸은 본문이 미끄러지거나 끌리거나 관성으로 움직이는 중에는 인정하지 않지만, 이미 인정된 칸은 온전히 보이는 동안 스크롤 중에도 유지한다.
    /// 인정된 칸이 바뀔 때만 반응한다: 새로 인정되면 커서 상자, 효과음은 마우스가 그 칸에 들어온 뒤 1번만(soundedRow), 인정이 풀리면 커서 상자만 숨긴다(효과음 없음).
    /// </summary>
    private void UpdateMouseHover()
    {
        float scroll = null != rowRoot ? rowRoot.anchoredPosition.y : 0f;
        bool bBodyMoving = true == bodySlide.bActive || lastBodyScroll != scroll
            || (null != bodyScroll && 1f <= bodyScroll.velocity.sqrMagnitude) || (null != bodyBar && true == bodyBar.IsHeld);
        lastBodyScroll = scroll;

        // 꺼진 칸은 PointerExit을 받지 못하므로 기록을 여기서 지운다.
        if (null != hoveredRow && false == hoveredRow.gameObject.activeInHierarchy)
        {
            if (hoveredRow == soundedRow) soundedRow = null;
            hoveredRow = null;
        }

        UI_RoguelikeChallengeRow candidate = null;
        if (null != hoveredRow && (false == bBodyMoving || hoveredRow == acceptedRow) && true == IsFullyInView(hoveredRow.Rect)) candidate = hoveredRow;

        if (candidate == acceptedRow) return;

        acceptedRow = candidate;
        if (null == candidate)
        {
            HideCursor();
            return;
        }

        if (candidate != soundedRow)
        {
            Sound.PlayUI(SoundID.ResultUIHover);
            soundedRow = candidate;
        }

        focusedRow = candidate;
        ShowCursor(candidate.Rect, rowCursorMotion);
    }

    /// <summary>마우스 호버 기록 · 인정 상태 · 효과음을 낸 칸 기록을 비운다(창 열기 · 창 닫기 · 패드로 바뀔 때). 남은 참조로 엉뚱한 칸이 반응하지 않게 한다.</summary>
    private void ResetHover()
    {
        hoveredRow = null;
        acceptedRow = null;
        soundedRow = null;
    }

    private void ApplyRowFocus(UI_RoguelikeChallengeRow _row)
    {
        if (_row != focusedRow) Sound.PlayUI(SoundID.ResultUIHover);
        focusedRow = _row;
        if (true == IsGamepadMode) ScrollBodyTo(-_row.Rect.anchoredPosition.y, -_row.Rect.anchoredPosition.y + _row.Rect.rect.height);
        ShowCursor(_row.Rect, rowCursorMotion);
    }

    /// <summary>칸을 선택하고 커서를 올린다(이미 같은 칸이 선택되어 있으면 커서만).</summary>
    private void FocusRow(UI_RoguelikeChallengeRow _row)
    {
        if (null == _row) return;

        focusedRow = _row;
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && eventSystem.currentSelectedGameObject != _row.gameObject) eventSystem.SetSelectedGameObject(_row.gameObject);
        ApplyRowFocus(_row);
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

    /// <summary>패드 선택이 사라졌을 때(가이드 3-8 "가장 흔한 버그") · 패드로 바뀔 때의 복구: 마지막 칸(없으면 첫 칸).</summary>
    private void RestoreSelection()
    {
        if (null != focusedRow && true == focusedRow.gameObject.activeInHierarchy)
        {
            FocusRow(focusedRow);
            return;
        }

        SelectFirstRow();
    }

    /// <summary>EventSystem 선택이 이 창 안의 것이면 푼다. 마우스 모드에서 칸을 눌러 생긴 선택이 방향키 이동으로 이어지지 않게 한다.</summary>
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

    /// <summary>대상(칸)이 본문 뷰포트 안에 온전히 들어와 있는지 본다. 월드 좌표의 축 정렬 사각형으로 비교하고 코너 배열을 재사용한다.</summary>
    private bool IsFullyInView(RectTransform _target)
    {
        if (null == bodyViewport) return true;

        float epsilon = 0.5f * Mathf.Abs(bodyViewport.lossyScale.x);
        _target.GetWorldCorners(targetCorners);

        bodyViewport.GetWorldCorners(clipCorners);
        if (targetCorners[0].x < clipCorners[0].x - epsilon || targetCorners[2].x > clipCorners[2].x + epsilon) return false;
        return targetCorners[0].y >= clipCorners[0].y - epsilon && targetCorners[2].y <= clipCorners[2].y + epsilon;
    }

    /// <summary>
    /// 스크롤(휠 · 끌기 · 패드 이동의 미끄러짐)로 커서 대상이 일부라도 잘리면 커서 상자를 바로 숨기고, 다시 온전히 들어오면 띄운다.
    /// 마우스 모드에서 호버가 끝나면 OnRowExited가 대상을 비우므로 마우스가 올라가 있는 칸에만 다시 뜬다.
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
        TickClearGlows();

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
        if (null != dimGroup) dimGroup.DOKill();
        if (null != panelRoot) panelRoot.DOKill();
        if (null != bodyScroll && null != bodyScrolledHandler) bodyScroll.onValueChanged.RemoveListener(bodyScrolledHandler);
        if (null != bodyBar) bodyBar.ScrollRequestedEvent -= bodyBarHandler;
        if (null != dimButton) dimButton.onClick.RemoveListener(RequestClose);
        ClosedEvent = null;
        base.OnDestroy();
    }
}
