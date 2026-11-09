using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 로그라이크 결과 화면. 기획서 3-7 "결과 화면과 런 종료".
///
/// [흐름] OpenResult → 등장 연출 → 플레이어가 다시 도전 / 메인메뉴를 고른다.
///   RetryRequestedEvent / MainMenuRequestedEvent를 올린다. 시스템이 아직 두 이벤트를 구독하지 않았을 때를 위해
///   ConfirmRequestedEvent도 함께 올린다(기존 흐름: 확인 → 디스토션 → 로비). 연결은 LobbyRemoval_AppLayer_Request.md S2.
///   이 화면은 ESC로 닫히지 않는다(프리팹 bCloseableByESC = false). 시스템이 ESC 키도 잠근다.
///
/// [구성 - 시안 v3] 어두운 딤 위 가운데 한 장(Panel_Book):
///   제목 명판(게임 오버 / 포기, 판 위쪽 가장자리에 걸침) → WAVE(언리밋은 UNLIMIT) + 도달 숫자(2배 글자) → 최고 웨이브(신기록이면 금색 "신기록")
///   → 플레이 시간 · 부순 나무 행 → 보유 카드 칸(아이콘 + 레벨, 넘치면 다음 줄) → 다시 도전 / 메인메뉴 버튼 → 키 안내(패드 모드에서만).
///   뒤쪽 HUD를 화면 밖으로 빼는 것은 시스템이 이 화면을 열 때 HUD에 요청한다(같은 문서 S5, UIView_RoguelikeHUD.SetCombatVisible).
///
/// [18개 언어 규격] 폭은 18개 언어 중 가장 긴 문구로 정한 최소값(Spec*)과 현재 언어의 실제 폭 중 큰 값이다 - 언어를 바꿔도 판 크기가 같다.
///   높이는 구성기가 글자 높이 + 위아래 여백 2px로 정한 줄 높이를 쓴다. 검수는 Localization QA 툴.
///
/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-7 · 3-8] 입력 모드 UI는 모달 공통(RoguelikeModalInput)이 건다.
///   패드 모드면 등장이 끝난 뒤 다시 도전을 선택해 둔다. 마우스 모드면 선택하지 않고 호버가 포커스다. 장치가 바뀌면 그에 맞춘다.
///   선택이 사라지면(null) 패드 모드에서 마지막 포커스로 되돌린다. 시간은 unscaled다.
/// </summary>
public class UIView_RoguelikeResult : UIView_RoguelikeModalBase
{
    // 18개 언어 중 가장 긴 문구로 잰 최소 폭(px, 640×360 기준). 시안 v3에서 언어별 폰트로 측정한 값이다.
    private const float SpecPanelWidth = 420f;
    private const float SpecButtonWidth = 186f;
    private const float SpecTitlePlateWidth = 170f;
    private const float ButtonTextPadding = 32f;
    private const float ButtonGap = 16f;
    private const float PanelPaddingX = 14f;
    private const float SlotGap = 4f;
    private const float SectionGap = 8f;
    private const float ScreenHeight = 360f;

    // 등장 연출(unscaled)
    private const float DimFadeSeconds = 0.2f;
    private const float PanelEnterSeconds = 0.24f;
    private const float PanelEnterOffset = 8f;
    private const float PanelEnterScale = 0.94f;
    private const float InputUnlockSeconds = 0.26f;

    // 문구 키(RoguelikeUI.json)
    private const string GameOverKey = "Roguelike_Result_GameOver";
    private const string AbandonedKey = "Roguelike_Result_Abandoned";
    private const string BestWaveKey = "Roguelike_Result_BestWave";
    private const string NewRecordKey = "Roguelike_Result_NewRecord";
    private const string PlayTimeKey = "Roguelike_Result_PlayTime";
    private const string TreesFelledKey = "Roguelike_Result_TreesFelled";
    private const string OwnedCardsKey = "Roguelike_Result_OwnedCards";
    private const string RetryKey = "Roguelike_Result_Retry";
    private const string MainMenuKey = "Roguelike_Result_MainMenu";
    private const string HintMoveKey = "Roguelike_Hint_Move";
    private const string HintSelectKey = "Roguelike_Hint_Select";
    private const string MaxKey = "Roguelike_CardLevel_Max";
    private const string WaveLabel = "WAVE";
    private const string UnlimitLabel = "UNLIMIT";
    private static readonly Color NewRecordColor = new Color(1f, 0.83f, 0.35f, 1f);
    private static readonly Color BestColor = new Color(0.73f, 0.65f, 0.54f, 1f);

    /// <summary>기존 확인(시스템 연결 전 호환용). 다시 도전 · 메인메뉴 어느 쪽을 눌러도 함께 올라간다.</summary>
    public event Action ConfirmRequestedEvent;
    public event Action RetryRequestedEvent;
    public event Action MainMenuRequestedEvent;

    public RoguelikeResultViewData Data { get; private set; }

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Result)
    [Header("화면")]
    [SerializeField] private CanvasGroup dimGroup;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private RectTransform panelVisual;

    [Header("제목")]
    [SerializeField] private RectTransform titlePlate;
    [SerializeField] private TMP_Text titleText;

    [Header("웨이브")]
    [SerializeField] private TMP_Text waveLabelText;
    [SerializeField] private TMP_Text waveValueText;
    [SerializeField] private TMP_Text bestText;

    [Header("행")]
    [SerializeField] private RectTransform rowsRoot;
    [SerializeField] private TMP_Text playTimeLabel;
    [SerializeField] private TMP_Text playTimeValue;
    [SerializeField] private TMP_Text treesLabel;
    [SerializeField] private TMP_Text treesValue;
    [SerializeField] private RectTransform[] rowSeparators = new RectTransform[0];

    [Header("보유 카드")]
    [SerializeField] private TMP_Text ownedLabel;
    [SerializeField] private RectTransform slotRoot;
    [SerializeField] private UI_RoguelikeCardIconSlot slotTemplate;

    [Header("버튼 · 키 안내")]
    [SerializeField] private UI_RoguelikeTextButton retryButton;
    [SerializeField] private UI_RoguelikeTextButton mainMenuButton;
    [SerializeField] private RectTransform guideRow;
    [SerializeField] private TMP_Text hintMoveText;
    [SerializeField] private TMP_Text hintSelectText;
    [SerializeField] private UI_KeyboardImage[] keyGuideImages = new UI_KeyboardImage[0];

    [Header("줄 높이(구성기가 글자 높이 + 여백 2px로 정한다)")]
    [SerializeField] private float plateHeight = 29f;
    [SerializeField] private float smallLineHeight = 18f;
    [SerializeField] private float bigLineHeight = 30f;
    [SerializeField] private float rowHeight = 22f;
    [SerializeField] private float buttonHeight = 30f;
    [SerializeField] private float guideHeight = 18f;

    //내부 의존성
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private readonly List<UI_RoguelikeCardIconSlot> slots = new List<UI_RoguelikeCardIconSlot>(16);
    private readonly StringBuilder textBuilder = new StringBuilder(32);

    //상태 변수
    private UI_RoguelikeTextButton focusedButton;
    private bool bInputLocked;
    private bool bRequested;
    private Vector2 panelBasePosition;

    // 캐싱 델리게이트
    private Action<UI_RoguelikeTextButton> buttonFocusedHandler;
    private Action<UI_RoguelikeTextButton> buttonClickedHandler;
    private Action<EInputDeviceType> deviceChangedHandler;
    private TweenCallback unlockInputCallback;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = viewCtx?.localizationManager;
        inputManager = viewCtx?.inputManager;

        buttonFocusedHandler = OnButtonFocused;
        buttonClickedHandler = OnButtonClicked;
        deviceChangedHandler = OnDeviceChanged;
        unlockInputCallback = UnlockInput;

        BindButton(retryButton);
        BindButton(mainMenuButton);
        if (null != retryButton && null != mainMenuButton)
        {
            retryButton.SetNavigation(null, mainMenuButton.Selectable);
            mainMenuButton.SetNavigation(retryButton.Selectable, null);
        }

        for (int i = 0; keyGuideImages.Length > i; i++)
        {
            if (null != keyGuideImages[i]) keyGuideImages[i].Initialize(inputManager);
        }

        if (null != slotTemplate) slotTemplate.gameObject.SetActive(false);
    }

    public virtual void OpenResult(in RoguelikeResultViewData _data)
    {
        Data = _data;
        Show();
        OnResultPresented();
    }

    /// <summary>결과를 그리고 판을 다시 배치한 뒤 등장 연출을 시작한다.</summary>
    protected virtual void OnResultPresented()
    {
        bRequested = false;
        ApplyTexts();
        Layout();
        PlayEnter();
    }

    protected override void OnShowContent()
    {
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
            inputManager.inputReader.InputDeviceChangedEvent += deviceChangedHandler;
        }
    }

    protected override void OnHideContent()
    {
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
        }

        KillTweens();
        focusedButton = null;
        if (null != EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>[호환] 예전 확인 버튼 경로. 다시 도전과 같다.</summary>
    protected void RequestConfirm() => Request(true);

    // // 내용

    private void ApplyTexts()
    {
        RoguelikeResultViewData data = Data;

        SetText(titleText, Localize(data.bAbandoned ? AbandonedKey : GameOverKey));
        SetText(waveLabelText, data.bUnlimit ? UnlimitLabel : WaveLabel);
        SetText(waveValueText, (data.bUnlimit ? data.unlimitWave : data.reachedWave).ToString());

        bool bNewRecord = 0 == data.clearRank || 0 == data.unlimitRank;
        if (true == bNewRecord)
        {
            SetText(bestText, Localize(NewRecordKey));
            if (null != bestText) bestText.color = NewRecordColor;
        }
        else
        {
            textBuilder.Clear();
            textBuilder.Append(Localize(BestWaveKey)).Append("  ").Append(RoguelikeRecordViewBuilder.GetBestWave(RoguelikeRecordRepository.Load()));
            SetText(bestText, textBuilder.ToString());
            if (null != bestText) bestText.color = BestColor;
        }

        SetText(playTimeLabel, Localize(PlayTimeKey));
        SetText(playTimeValue, FormatTime(data.playTimeSeconds));
        SetText(treesLabel, Localize(TreesFelledKey));
        SetText(treesValue, data.treesFelled.ToString());
        SetText(ownedLabel, Localize(OwnedCardsKey));

        if (null != retryButton) retryButton.SetText(Localize(RetryKey));
        if (null != mainMenuButton) mainMenuButton.SetText(Localize(MainMenuKey));
        SetText(hintMoveText, Localize(HintMoveKey));
        SetText(hintSelectText, Localize(HintSelectKey));

        FillSlots(data.cards);
    }

    private void FillSlots(List<RoguelikeOwnedCardViewData> _cards)
    {
        int count = null != _cards ? _cards.Count : 0;
        string maxLabel = Localize(MaxKey);

        while (slots.Count < count && null != slotTemplate && null != slotRoot)
        {
            UI_RoguelikeCardIconSlot slot = Instantiate(slotTemplate, slotRoot);
            slots.Add(slot);
        }

        for (int i = 0; slots.Count > i; i++)
        {
            bool bUsed = i < count;
            slots[i].gameObject.SetActive(bUsed);
            if (true == bUsed) slots[i].SetCard(_cards[i], maxLabel);
        }
    }

    private string FormatTime(float _seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(_seconds));
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int seconds = total % 60;

        // 09:12 / 1:05:09
        textBuilder.Clear();
        if (0 < hours) textBuilder.Append(hours).Append(':');
        if (10 > minutes) textBuilder.Append('0');
        textBuilder.Append(minutes).Append(':');
        if (10 > seconds) textBuilder.Append('0');
        textBuilder.Append(seconds);
        return textBuilder.ToString();
    }

    // // 배치 (정수 픽셀, 판 위쪽 가운데 기준으로 아래로 쌓는다)

    private void Layout()
    {
        if (null == panelRoot) return;

        float buttonWidth = SpecButtonWidth;
        if (null != retryButton) buttonWidth = Mathf.Max(buttonWidth, retryButton.GetPreferredTextWidth() + ButtonTextPadding);
        if (null != mainMenuButton) buttonWidth = Mathf.Max(buttonWidth, mainMenuButton.GetPreferredTextWidth() + ButtonTextPadding);
        buttonWidth = Mathf.Ceil(buttonWidth);

        float panelWidth = Mathf.Max(SpecPanelWidth, buttonWidth * 2f + ButtonGap + PanelPaddingX * 2f);
        panelWidth = Mathf.Max(panelWidth, PreferredWidth(playTimeLabel) + PreferredWidth(playTimeValue) + PanelPaddingX * 2f + 40f);
        panelWidth = Mathf.Max(panelWidth, PreferredWidth(treesLabel) + PreferredWidth(treesValue) + PanelPaddingX * 2f + 40f);
        panelWidth = EvenCeil(panelWidth);

        float innerWidth = panelWidth - PanelPaddingX * 2f;
        float y = Mathf.Floor(plateHeight * 0.5f) + 4f;

        // 제목 명판: 판 위쪽 가장자리에 반쯤 걸친다.
        float plateWidth = EvenCeil(Mathf.Max(SpecTitlePlateWidth, PreferredWidth(titleText) + 32f));
        PlaceTop(titlePlate, 0f, -Mathf.Floor(plateHeight * 0.5f), plateWidth, plateHeight);
        PlaceFill(titleText, titlePlate);

        PlaceTopText(waveLabelText, 0f, y, innerWidth, smallLineHeight);
        y += smallLineHeight;
        PlaceTopText(waveValueText, 0f, y, innerWidth, bigLineHeight);
        y += bigLineHeight;
        PlaceTopText(bestText, 0f, y, innerWidth, smallLineHeight);
        y += smallLineHeight + SectionGap;

        // 행 두 줄(이름 왼쪽 · 값 오른쪽) + 행마다 아래 구분선
        float left = -innerWidth * 0.5f;
        PlaceRow(playTimeLabel, playTimeValue, left, y, innerWidth);
        PlaceSeparator(0, y + rowHeight, innerWidth);
        y += rowHeight + 1f;
        PlaceRow(treesLabel, treesValue, left, y, innerWidth);
        PlaceSeparator(1, y + rowHeight, innerWidth);
        y += rowHeight + 1f + SectionGap;

        // 보유 카드: 이름 줄 + 칸(가운데 정렬, 한 줄에 들어가는 만큼씩)
        PlaceTopTextLeft(ownedLabel, left, y, innerWidth, smallLineHeight);
        y += smallLineHeight + 2f;
        y = PlaceSlots(y, innerWidth);
        y += SectionGap + 2f;

        // 버튼 두 개(가운데 기준 좌우)
        if (null != retryButton)
        {
            retryButton.SetWidth(buttonWidth);
            PlaceTop(retryButton.Rect, -(buttonWidth + ButtonGap) * 0.5f, y, buttonWidth, buttonHeight);
        }
        if (null != mainMenuButton)
        {
            mainMenuButton.SetWidth(buttonWidth);
            PlaceTop(mainMenuButton.Rect, (buttonWidth + ButtonGap) * 0.5f, y, buttonWidth, buttonHeight);
        }
        y += buttonHeight + SectionGap;

        bool bGuide = null != inputManager && true == inputManager.IsGamepadMode;
        if (null != guideRow)
        {
            guideRow.gameObject.SetActive(bGuide);
            if (true == bGuide)
            {
                guideRow.anchorMin = new Vector2(0.5f, 1f);
                guideRow.anchorMax = new Vector2(0.5f, 1f);
                guideRow.pivot = new Vector2(0f, 1f);
                guideRow.anchoredPosition = new Vector2(Mathf.Round(left), -y);
                y += guideHeight;
            }
        }
        y += 10f;

        // 판 크기와 위치: 화면 세로 가운데(명판이 위로 걸친 만큼 아래로).
        float panelHeight = Mathf.Ceil(y);
        panelRoot.sizeDelta = new Vector2(panelWidth, panelHeight);
        float top = Mathf.Round((ScreenHeight - panelHeight + plateHeight * 0.5f) * 0.5f);
        panelBasePosition = new Vector2(0f, -Mathf.Max(Mathf.Ceil(plateHeight * 0.5f) + 2f, top));
        panelRoot.anchoredPosition = panelBasePosition;
    }

    private float PlaceSlots(float _y, float _innerWidth)
    {
        int active = 0;
        for (int i = 0; slots.Count > i; i++)
        {
            if (true == slots[i].gameObject.activeSelf) active++;
        }

        if (0 == active || null == slotRoot) return _y;

        float step = UI_RoguelikeCardIconSlot.SlotSize + SlotGap;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((_innerWidth + SlotGap) / step));
        int rows = (active + perRow - 1) / perRow;

        PlaceTop(slotRoot, 0f, _y, _innerWidth, rows * step - SlotGap);

        int index = 0;
        for (int i = 0; slots.Count > i; i++)
        {
            if (false == slots[i].gameObject.activeSelf) continue;

            int row = index / perRow;
            int column = index % perRow;
            int inRow = Mathf.Min(perRow, active - row * perRow);
            float rowWidth = inRow * step - SlotGap;
            float x = Mathf.Round(-rowWidth * 0.5f + column * step);

            RectTransform rect = slots[i].Rect;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -row * step);
            index++;
        }

        return _y + rows * step - SlotGap;
    }

    private void PlaceRow(TMP_Text _label, TMP_Text _value, float _left, float _y, float _width)
    {
        if (null != _label)
        {
            RectTransform rect = _label.rectTransform;
            SetTopAnchors(rect, new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(Mathf.Round(_left), -_y);
            rect.sizeDelta = new Vector2(Mathf.Round(_width - 60f), rowHeight);
        }

        if (null != _value)
        {
            RectTransform rect = _value.rectTransform;
            SetTopAnchors(rect, new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(Mathf.Round(_left + _width), -_y);
            rect.sizeDelta = new Vector2(60f, rowHeight);
        }
    }

    private void PlaceSeparator(int _index, float _y, float _width)
    {
        if (rowSeparators.Length <= _index || null == rowSeparators[_index]) return;
        PlaceTop(rowSeparators[_index], 0f, _y, _width, 1f);
    }

    private static void PlaceTop(RectTransform _rect, float _centerX, float _y, float _width, float _height)
    {
        if (null == _rect) return;
        SetTopAnchors(_rect, new Vector2(0.5f, 1f));
        _rect.sizeDelta = new Vector2(Mathf.Round(_width), Mathf.Round(_height));
        _rect.anchoredPosition = new Vector2(Mathf.Round(_centerX), -Mathf.Round(_y));
    }

    private static void PlaceTopText(TMP_Text _text, float _centerX, float _y, float _width, float _height)
    {
        if (null == _text) return;
        PlaceTop(_text.rectTransform, _centerX, _y, _width, _height);
    }

    private static void PlaceTopTextLeft(TMP_Text _text, float _left, float _y, float _width, float _height)
    {
        if (null == _text) return;
        RectTransform rect = _text.rectTransform;
        SetTopAnchors(rect, new Vector2(0f, 1f));
        rect.sizeDelta = new Vector2(Mathf.Round(_width), Mathf.Round(_height));
        rect.anchoredPosition = new Vector2(Mathf.Round(_left), -Mathf.Round(_y));
    }

    private static void PlaceFill(TMP_Text _text, RectTransform _parent)
    {
        if (null == _text || null == _parent || _text.rectTransform.parent != _parent) return;
        RectTransform rect = _text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetTopAnchors(RectTransform _rect, Vector2 _pivot)
    {
        _rect.anchorMin = new Vector2(0.5f, 1f);
        _rect.anchorMax = new Vector2(0.5f, 1f);
        _rect.pivot = _pivot;
    }

    private static float PreferredWidth(TMP_Text _text)
    {
        if (null == _text) return 0f;
        return Mathf.Ceil(_text.GetPreferredValues(_text.text, 10000f, 10000f).x);
    }

    private static float EvenCeil(float _value)
    {
        int v = Mathf.CeilToInt(_value);
        return 0 == v % 2 ? v : v + 1;
    }

    // // 등장 연출

    private void PlayEnter()
    {
        KillTweens();
        bInputLocked = true;
        SetButtonsInteractable(false);
        SetFocus(null);

        if (null != dimGroup)
        {
            dimGroup.alpha = 0f;
            dimGroup.DOFade(1f, DimFadeSeconds).SetUpdate(true);
        }

        if (null != panelRoot)
        {
            // 일시적으로 작게 시작해 1배로 돌아온다(크기가 바뀐 채 남지 않는다). 위치는 정수 픽셀로 따라 올라온다.
            panelRoot.anchoredPosition = panelBasePosition + new Vector2(0f, -PanelEnterOffset);
            panelRoot.DOAnchorPos(panelBasePosition, PanelEnterSeconds, true).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        if (null != panelVisual)
        {
            panelVisual.localScale = new Vector3(PanelEnterScale, PanelEnterScale, 1f);
            panelVisual.DOScale(Vector3.one, PanelEnterSeconds).SetEase(Ease.OutBack).SetUpdate(true);
        }

        DOVirtual.DelayedCall(InputUnlockSeconds, unlockInputCallback, true).SetId(this);
    }

    private void UnlockInput()
    {
        bInputLocked = false;
        SetButtonsInteractable(true);

        if (null != panelVisual) panelVisual.localScale = Vector3.one;
        if (null != panelRoot) panelRoot.anchoredPosition = panelBasePosition;

        // 패드 모드면 다시 도전을 선택해 둔다(3-8). 마우스 모드면 아무것도 선택하지 않는다.
        if (null != inputManager && true == inputManager.IsGamepadMode) SelectButton(retryButton);
    }

    private void KillTweens()
    {
        if (null != dimGroup) dimGroup.DOKill();
        if (null != panelRoot) panelRoot.DOKill();
        if (null != panelVisual) panelVisual.DOKill();
        DOTween.Kill(this);
    }

    // // 버튼 · 포커스

    private void BindButton(UI_RoguelikeTextButton _button)
    {
        if (null == _button) return;
        _button.FocusedEvent -= buttonFocusedHandler;
        _button.FocusedEvent += buttonFocusedHandler;
        _button.ClickedEvent -= buttonClickedHandler;
        _button.ClickedEvent += buttonClickedHandler;
    }

    private void SetButtonsInteractable(bool _bInteractable)
    {
        if (null != retryButton) retryButton.SetInteractable(_bInteractable);
        if (null != mainMenuButton) mainMenuButton.SetInteractable(_bInteractable);
    }

    private void OnButtonFocused(UI_RoguelikeTextButton _button)
    {
        SetFocus(_button);
    }

    private void SetFocus(UI_RoguelikeTextButton _button)
    {
        focusedButton = _button;
        if (null != retryButton) retryButton.SetFocused(retryButton == _button);
        if (null != mainMenuButton) mainMenuButton.SetFocused(mainMenuButton == _button);
    }

    private void SelectButton(UI_RoguelikeTextButton _button)
    {
        if (null == _button) return;
        SetFocus(_button);
        if (null != EventSystem.current && null != _button.Selectable) EventSystem.current.SetSelectedGameObject(_button.Selectable.gameObject);
    }

    private void OnButtonClicked(UI_RoguelikeTextButton _button)
    {
        if (true == bInputLocked) return;
        Request(_button == retryButton);
    }

    private void Request(bool _bRetry)
    {
        if (true == bRequested) return;
        bRequested = true;
        bInputLocked = true;
        SetButtonsInteractable(false);

        if (true == _bRetry) RetryRequestedEvent?.Invoke();
        else MainMenuRequestedEvent?.Invoke();

        ConfirmRequestedEvent?.Invoke();
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        if (true == bInputLocked) return;

        if (EInputDeviceType.Gamepad == _device)
        {
            SelectButton(null != focusedButton ? focusedButton : retryButton);
        }
        else
        {
            if (null != EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        // 키 안내 줄은 패드 모드에서만 보인다 - 판 높이가 바뀌므로 다시 배치한다.
        Layout();
    }

    // // 공통

    private string Localize(string _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
    }

    private static void SetText(TMP_Text _text, string _value)
    {
        if (null != _text) _text.text = _value;
    }

    public override void Update()
    {
        base.Update();

        // 패드 모드에서 선택이 사라지면(다른 곳 클릭 등) 마지막 포커스로 되돌린다(가이드 3-8 "가장 흔한 버그").
        if (true == bInputLocked || false == IsVisible) return;
        if (null == inputManager || false == inputManager.IsGamepadMode || null == EventSystem.current) return;
        if (null != EventSystem.current.currentSelectedGameObject) return;

        SelectButton(null != focusedButton ? focusedButton : retryButton);
    }

    public override void OnDestroy()
    {
        KillTweens();
        if (null != inputManager && null != inputManager.inputReader) inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
        ConfirmRequestedEvent = null;
        RetryRequestedEvent = null;
        MainMenuRequestedEvent = null;
        base.OnDestroy();
    }
}
