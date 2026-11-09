using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 순위표(런 기록 3탭). 기획서 3-6 - 최근 기록 / 클리어 랭킹(1~5위) / 언리밋 랭킹(1~5위).
/// 메인메뉴의 로그라이크 하위 메뉴 "기록"에서 연다(로비 폐지 - LobbyRemoval_AppLayer_Request.md). 로그라이크 씬에서도 같은 프리팹이 그대로 동작한다.
///
/// [구성 - 시안 v3] 어두운 딤 위 가운데 한 장(Panel_Book):
///   위쪽 가장자리의 탭 명판 "◀ 현재 탭 ▶" + 아래 점 3개(현재 탭 금색). 탭 이름이 언어마다 길이가 크게 달라 세 개를 나란히 두지 않는다.
///   명판 폭은 18개 언어 중 가장 긴 탭 이름 기준(SpecTabPlateWidth)과 현재 언어 폭 중 큰 값이라 탭을 넘겨도 크기가 흔들리지 않는다.
///   본문은 UI_RoguelikeRecordRow 최대 5줄(최근 기록은 1줄). 기록이 없으면 안내 문구. 아래에 키 안내(탭 전환 · 뒤로, 아이콘은 입력 장치를 따른다).
///
/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-7] 탭 전환 = UITabShiftEvent(LB/RB · PageUp/PageDown) 또는 명판의 ◀ ▶ 클릭.
///   닫기 = 딤 클릭 · 패드 B(UICancelEvent). 키보드 Esc는 로그라이크 씬에서는 프리팹 bCloseableByESC(깊이 관리)가 닫고,
///   메인메뉴처럼 깊이 관리가 없는 곳에서 열 때는 SetStandaloneClose(true)로 ESC를 직접 받는다. 입력 모드 UI는 모달 공통(RoguelikeModalInput)이 건다.
///   이 창에는 고를 항목이 없어 패드 선택을 잡지 않는다.
/// </summary>
public class UIView_RoguelikeRecords : UIView_RoguelikeModalBase
{
    private const int TabCount = 3;
    private const int MaxRows = 5;

    // 18개 언어 중 가장 긴 탭 이름으로 잰 명판 최소 폭(px, 시안 v3 측정: 가장 긴 탭 183 + 화살표 · 여백 54).
    private const float SpecTabPlateWidth = 238f;
    private const float TabArrowWidth = 22f;
    private const float RowHeight = 46f;
    private const float RowGap = 4f;
    private const float PanelPadding = 10f;

    private const float DimFadeSeconds = 0.18f;
    private const float PanelEnterSeconds = 0.2f;
    private const float PanelEnterOffset = 6f;

    // 문구 키(RoguelikeUI.json)
    private static readonly string[] TabKeys = { "Roguelike_Records_TabRecent", "Roguelike_Records_TabClear", "Roguelike_Records_TabUnlimit" };
    private const string EmptyKey = "Roguelike_Records_Empty";
    private const string HintTabKey = "Roguelike_Hint_Tab";
    private const string HintBackKey = "Roguelike_Hint_Back";
    private const string MaxKey = "Roguelike_CardLevel_Max";

    private static readonly Color DotOnColor = new Color(1f, 0.83f, 0.35f, 1f);
    private static readonly Color DotOffColor = new Color(0.42f, 0.32f, 0.22f, 1f);

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
    [SerializeField] private Image[] tabDots = new Image[TabCount];

    [Header("본문")]
    [SerializeField] private RectTransform rowRoot;
    [SerializeField] private UI_RoguelikeRecordRow rowTemplate;
    [SerializeField] private TMP_Text emptyText;

    [Header("키 안내")]
    [SerializeField] private TMP_Text hintTabText;
    [SerializeField] private TMP_Text hintBackText;
    [SerializeField] private UI_KeyboardImage[] keyGuideImages = new UI_KeyboardImage[0];

    //내부 의존성
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private readonly List<UI_RoguelikeRecordRow> rows = new List<UI_RoguelikeRecordRow>(MaxRows);

    //상태 변수
    private int currentTab;
    private bool bStandaloneClose;
    private Vector2 panelBasePosition;
    private bool bPanelBaseCaptured;

    // 캐싱 델리게이트
    private Action<int> tabShiftHandler;
    private Action cancelHandler;
    private Action escHandler;
    private Action languageChangedHandler;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = viewCtx?.localizationManager;
        inputManager = viewCtx?.inputManager;

        tabShiftHandler = OnTabShift;
        cancelHandler = RequestClose;
        escHandler = RequestClose;
        languageChangedHandler = OnLanguageChanged;

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

        if (null != rowTemplate) rowTemplate.gameObject.SetActive(false);
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
        OnRecordsPresented();
    }

    /// <summary>첫 탭(최근 기록)을 그리고 등장 연출을 시작한다.</summary>
    protected virtual void OnRecordsPresented()
    {
        currentTab = 0;
        ApplyStaticTexts();
        RefreshTab();
        PlayEnter();
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
        if (null != dimGroup) dimGroup.DOKill();
        if (null != panelRoot) panelRoot.DOKill();
        ClosedEvent?.Invoke();
    }

    // // 탭

    private void OnTabShift(int _direction)
    {
        if (0 > _direction) ShowPrevTab();
        else if (0 < _direction) ShowNextTab();
    }

    /// <summary>탭을 직접 고른다(0 최근 기록 · 1 클리어 랭킹 · 2 언리밋 랭킹). 창이 열린 뒤에 부른다.</summary>
    public void ShowTab(int _tab)
    {
        currentTab = Mathf.Clamp(_tab, 0, TabCount - 1);
        RefreshTab();
    }

    private void ShowPrevTab()
    {
        currentTab = (currentTab + TabCount - 1) % TabCount;
        RefreshTab();
        Sound.PlayUI(SoundID.MainClick);
    }

    private void ShowNextTab()
    {
        currentTab = (currentTab + 1) % TabCount;
        RefreshTab();
        Sound.PlayUI(SoundID.MainClick);
    }

    private void RefreshTab()
    {
        if (null != tabText) tabText.text = Localize(TabKeys[currentTab]);
        for (int i = 0; tabDots.Length > i; i++)
        {
            if (null != tabDots[i]) tabDots[i].color = i == currentTab ? DotOnColor : DotOffColor;
        }

        LayoutTabPlate();

        string maxLabel = Localize(MaxKey);
        int count = 0;

        if (0 == currentTab)
        {
            if (true == Recent.bValid)
            {
                RoguelikeRecordViewData recent = Recent;
                GetRow(0).SetRecord(0, in recent, maxLabel);
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
                GetRow(i).SetRecord(i + 1, in record, maxLabel);
            }
        }

        for (int i = 0; rows.Count > i; i++) rows[i].gameObject.SetActive(i < count);
        if (null != emptyText) emptyText.gameObject.SetActive(0 == count);

        LayoutRows();
    }

    private UI_RoguelikeRecordRow GetRow(int _index)
    {
        while (rows.Count <= _index && null != rowTemplate && null != rowRoot)
        {
            UI_RoguelikeRecordRow row = Instantiate(rowTemplate, rowRoot);
            rows.Add(row);
        }
        return rows[_index];
    }

    // // 배치 (정수 픽셀)

    private void LayoutTabPlate()
    {
        if (null == tabPlate || null == tabText) return;

        float textWidth = Mathf.Ceil(tabText.GetPreferredValues(tabText.text, 10000f, 10000f).x);
        float width = Mathf.Max(SpecTabPlateWidth, textWidth + TabArrowWidth * 2f + 10f);
        int even = Mathf.CeilToInt(width);
        if (0 != even % 2) even++;
        tabPlate.sizeDelta = new Vector2(even, tabPlate.sizeDelta.y);
    }

    private void LayoutRows()
    {
        if (null == rowRoot) return;

        float width = rowRoot.rect.width;
        float y = 0f;
        for (int i = 0; rows.Count > i; i++)
        {
            if (false == rows[i].gameObject.activeSelf) continue;

            RectTransform rect = rows[i].Rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(Mathf.Round(width), RowHeight);
            rect.anchoredPosition = new Vector2(0f, -y);
            y += RowHeight + RowGap;
        }
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
            if (false == bPanelBaseCaptured)
            {
                panelBasePosition = panelRoot.anchoredPosition;
                bPanelBaseCaptured = true;
            }

            panelRoot.DOKill();
            panelRoot.anchoredPosition = panelBasePosition + new Vector2(0f, -PanelEnterOffset);
            panelRoot.DOAnchorPos(panelBasePosition, PanelEnterSeconds, true).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    // // 닫기 · 문구

    private void RequestClose()
    {
        if (false == IsVisible) return;
        Sound.PlayUI(SoundID.MainClick);
        Hide();
    }

    private void ApplyStaticTexts()
    {
        if (null != emptyText) emptyText.text = Localize(EmptyKey);
        if (null != hintTabText) hintTabText.text = Localize(HintTabKey);
        if (null != hintBackText) hintBackText.text = Localize(HintBackKey);
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
        }

        if (null != localizationManager) localizationManager.OnLanguageChanged -= languageChangedHandler;
    }

    private static void Copy(IReadOnlyList<RoguelikeRecordViewData> _from, List<RoguelikeRecordViewData> _to)
    {
        _to.Clear();
        if (null == _from) return;

        for (int i = 0; i < _from.Count; i++) _to.Add(_from[i]);
    }

    public override void OnDestroy()
    {
        UnbindInput();
        if (null != dimGroup) dimGroup.DOKill();
        if (null != panelRoot) panelRoot.DOKill();
        if (null != dimButton) dimButton.onClick.RemoveListener(RequestClose);
        if (null != prevTabButton) prevTabButton.onClick.RemoveListener(ShowPrevTab);
        if (null != nextTabButton) nextTabButton.onClick.RemoveListener(ShowNextTab);
        ClosedEvent = null;
        base.OnDestroy();
    }
}
