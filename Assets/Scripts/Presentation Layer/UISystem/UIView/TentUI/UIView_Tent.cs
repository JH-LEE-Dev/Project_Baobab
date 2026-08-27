using System;
using PresentationLayer.UISystem.CustomNumber;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIView_Tent : UIView
{
    // ESC로 닫힐 때는 UIDepthController가 Hide()를 직접 호출해 TentInteractSignal(false) 경로를
    // 거치지 않으므로, 닫힘을 항상 감지하려면 상호작용 토글이 아니라 이 UI 자체의 Hide 시점을 봐야 한다.
    public event Action TentUIClosedEvent;

    private ISkillSystemProvider skillSystemProvider;
    private IMoneyData moneyData;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private UI_TentAbilityComponent abilityUIComponent;
    [SerializeField] private RectTransform moneyPivot;
    [SerializeField] private GameObject currencyCounterHUDPrefab;

    [Header("Tutorial HUD Presentation")]
    [SerializeField, Min(0.0f)] private float tutorialHUDRevealDelay = 0.2f;
    [SerializeField, Min(0.0f)] private float tutorialHUDFadeDuration = 0.5f;

    private CurrencyCounterHUD coinCounter;
    private AbilityHUD abilityHUD;
    private CanvasGroup abilityHUDCanvasGroup;
    private CanvasGroup coinCounterCanvasGroup;

    private bool isInitialOpen = false;
    private bool playSoundsForCurrentPresentation;
    private bool isTutorialState;
    private bool isTutorialUpgradeAxeQuestUIHidden;
    private bool isTutorialHUDRevealPlaying;
    private float tutorialHUDRevealElapsed;
    private float tutorialHUDAlpha = 1.0f;

    #region Default Logic

    // Tent UI 초기 설정을 진행한다.
    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        InitializeComponents();
        RefreshMoneyTexts(false);
    }

    // Tent UI가 사용하는 하위 컴포넌트들을 초기화한다.
    private void InitializeComponents()
    {
        abilityUIComponent?.Initialize(skillSystemProvider, viewCtx?.inputManager, viewCtx?.localizationManager);
        InitializeMoneyCounters();
        ApplyTutorialHUDAlpha(tutorialHUDAlpha);
    }

    // 외부에서 전달된 스킬 시스템과 재화 데이터를 보관한다.
    public void DependencyInjection(ISkillSystemProvider _skillSystemProvider, IMoneyData _moneyData)
    {
        skillSystemProvider = _skillSystemProvider;
        moneyData = _moneyData;
        abilityUIComponent?.Initialize(skillSystemProvider, viewCtx?.inputManager, viewCtx?.localizationManager);
        RefreshMoneyTexts(false);
    }

    // Tent UI가 사용할 루트 Transform을 찾는다.
    public override void SetupUI()
    {
        base.SetupUI();

        if (uiRoot == null)
            uiRoot = transform;

        if (moneyPivot == null)
            moneyPivot = FindChildByName(transform, "MoneyPivot") as RectTransform;
    }

    // 능력창이 열려 있는 동안 입력 처리와 상태 갱신을 진행한다.
    public override void Update()
    {
        abilityUIComponent?.Tick();
        UpdateTutorialHUDPresentation();
    }

    public override void Hide()
    {
        // UI Cancel 이벤트는 입력 장치 자동 판별보다 먼저 전달될 수 있다. 키마 모드에서 처음 누른
        // B/○라면 이 TentUI 안에서 즉시 패드 모드로 바꾸고, 닫기 요청은 한 번만 소비한다.
        Gamepad _gamepad = Gamepad.current;
        if (true == IsVisible &&
            null != viewCtx?.inputManager &&
            false == viewCtx.inputManager.IsGamepadMode &&
            null != _gamepad &&
            true == _gamepad.buttonEast.wasPressedThisFrame)
        {
            viewCtx.inputManager.ForceInputDevice(EInputDeviceType.Gamepad);
            return;
        }

        base.Hide();
    }

    protected override void OnShow()
    {
        base.OnShow();

        Sound.RequestAudioDuck();

        if (false == isInitialOpen)
        {
            isInitialOpen = true;
            return;
        }

        playSoundsForCurrentPresentation = true;
        Sound.PlayUI(SoundID.AbilityOpen);

        viewCtx.inputManager.SetInputMode(EInputMode.UI);
        viewCtx.inputManager.PauseMove(true);

        // 패드에는 포인터가 없어서 특성 노드를 찍을 수단이 없다. 여기서 가상 커서를 요청하면
        // 패드를 쓰는 중일 때만 화면 중앙에 나타난다. (마우스 유저에게는 나오지 않는다)
        viewCtx.inputManager.SetVirtualCursorRequested(true);

        RefreshMoneyTexts(false);
        ApplyTutorialHUDAlpha(tutorialHUDAlpha);
        abilityUIComponent?.Open();
    }


    protected override void OnHide()
    {
        base.OnHide();

        Sound.ReleaseAudioDuck();

        if (playSoundsForCurrentPresentation)
        {
            Sound.PlayUI(SoundID.ResultUIClose);
            playSoundsForCurrentPresentation = false;
        }

        viewCtx.inputManager.SetVirtualCursorRequested(false);
        viewCtx.inputManager.SetInputMode(EInputMode.Gameplay);
        viewCtx.inputManager.PauseMove(false);

        abilityUIComponent?.Close();

        TentUIClosedEvent?.Invoke();
    }

    #endregion


    #region Money UI

    // 캐릭터가 특정 재화를 획득했을 때 현재 재화 텍스트를 갱신한다.
    public void CharacterEarnMoney(MoneyType _moneyType)
    {
        if (_moneyType != MoneyType.Coin)
            return;

        RefreshMoneyTexts(true);
    }

    // 캐릭터의 전체 재화 값이 바뀌었을 때 현재 재화 텍스트를 갱신한다.
    public void CharactersMoneyChanged()
    {
        RefreshMoneyTexts(true);
    }

    // 현재 보유 중인 코인과 당근 수치를 텍스트로 갱신한다.
    private void RefreshMoneyTexts(bool _withMotion)
    {
        InitializeMoneyCounters();

        if (coinCounter == null)
            return;

        long _coinValue = moneyData?.money ?? 0L;
        coinCounter.gameObject.SetActive(true);

        if (_withMotion)
            coinCounter.SetNumberAnimated(_coinValue);
        else
            coinCounter.SetNumber(_coinValue);
    }

    private void InitializeMoneyCounters()
    {
        if (coinCounter != null)
            return;

        if (moneyPivot == null)
            moneyPivot = FindChildByName(transform, "MoneyPivot") as RectTransform;

        if (moneyPivot == null || currencyCounterHUDPrefab == null)
            return;

        GameObject _counterObject = Instantiate(currencyCounterHUDPrefab, moneyPivot);
        _counterObject.name = "CurrencyCounterHUD_Coin";
        SetLayerRecursive(_counterObject, gameObject.layer);

        RectTransform _counterRect = _counterObject.GetComponent<RectTransform>();
        if (null != _counterRect)
        {
            _counterRect.anchorMin = new Vector2(0.0f, 1.0f);
            _counterRect.anchorMax = new Vector2(0.0f, 1.0f);
            _counterRect.pivot = new Vector2(0.0f, 0.5f);
            _counterRect.anchoredPosition = Vector2.zero;
        }

        coinCounter = _counterObject.GetComponent<CurrencyCounterHUD>();
        if (coinCounter == null)
            return;

        coinCounter.Initialize();
        coinCounter.SetMoneyType(MoneyType.Coin);
        coinCounter.SetNumber(0);
        coinCounter.gameObject.SetActive(true);
        coinCounterCanvasGroup = GetOrAddCanvasGroup(coinCounter.gameObject);
        ApplyCanvasGroupAlpha(coinCounterCanvasGroup, tutorialHUDAlpha);
    }

    private Transform FindChildByName(Transform _root, string _name)
    {
        if (_root == null)
            return null;

        if (_root.name == _name)
            return _root;

        for (int i = 0; i < _root.childCount; i++)
        {
            Transform _found = FindChildByName(_root.GetChild(i), _name);
            if (_found != null)
                return _found;
        }

        return null;
    }

    private void SetLayerRecursive(GameObject _target, int _layer)
    {
        if (_target == null)
            return;

        _target.layer = _layer;

        Transform _targetTransform = _target.transform;
        for (int i = 0; i < _targetTransform.childCount; i++)
        {
            SetLayerRecursive(_targetTransform.GetChild(i).gameObject, _layer);
        }
    }

    #endregion


    // Tent UI 정리 시 확장 포인트로 남겨둔다.
    public override void OnDestroy()
    {
    }

    public override void Refresh() //저장 파일 로드할 때 호출됨.
    {
        RefreshMoneyTexts(false);
        abilityUIComponent?.Refresh();
    }

    // Legacy accumulated-value notice is intentionally ignored after AbilityNotice removal.
    public void DeclareSkillAccumulativeValue(SkillAccumulatedValueData _data)
    {
    }

    public void SkillAccumulatedValuePreviewProvided(SkillAccumulatedValueChangeData _data)
    {
        abilityUIComponent?.SkillAccumulatedValuePreviewProvided(_data);
    }

    // 이 TentUI 오픈이 튜토리얼 "도끼를 강화하세요" 스텝 중인지 전달한다. GameplayUICoordinator가
    // TentUI를 열기(Show) 직전에 호출한다.
    public void SetTutorialState(bool _bIsTutorial)
    {
        isTutorialState = _bIsTutorial;
        abilityUIComponent?.SetTutorialState(_bIsTutorial);

        if (false == isTutorialState)
        {
            isTutorialUpgradeAxeQuestUIHidden = false;
            StopTutorialHUDReveal();
            ApplyTutorialHUDAlpha(1.0f);
            return;
        }

        if (isTutorialUpgradeAxeQuestUIHidden)
        {
            if (false == isTutorialHUDRevealPlaying)
                ApplyTutorialHUDAlpha(1.0f);

            return;
        }

        StopTutorialHUDReveal();
        ApplyTutorialHUDAlpha(0.0f);
    }

    // "도끼를 강화하세요" 퀘스트 안내 UI가 화면에서 완전히 사라진 시점에 GameplayUICoordinator가 호출한다.
    public void NotifyTutorialUpgradeAxeQuestUIHidden()
    {
        abilityUIComponent?.NotifyTutorialUpgradeAxeQuestUIHidden();

        if (isTutorialUpgradeAxeQuestUIHidden)
            return;

        isTutorialUpgradeAxeQuestUIHidden = true;

        if (false == isTutorialState)
        {
            ApplyTutorialHUDAlpha(1.0f);
            return;
        }

        tutorialHUDRevealElapsed = 0.0f;
        isTutorialHUDRevealPlaying = true;
        ApplyTutorialHUDAlpha(0.0f);
    }

    private void UpdateTutorialHUDPresentation()
    {
        if (false == isTutorialHUDRevealPlaying)
            return;

        tutorialHUDRevealElapsed += Time.unscaledDeltaTime;

        float _delay = Mathf.Max(0.0f, tutorialHUDRevealDelay);
        if (tutorialHUDRevealElapsed < _delay)
        {
            ApplyTutorialHUDAlpha(0.0f);
            return;
        }

        float _fadeDuration = Mathf.Max(0.0f, tutorialHUDFadeDuration);
        float _fadeElapsed = tutorialHUDRevealElapsed - _delay;
        float _progress = _fadeDuration <= 0.0f
            ? 1.0f
            : Mathf.Clamp01(_fadeElapsed / _fadeDuration);

        ApplyTutorialHUDAlpha(_progress);

        if (_progress < 1.0f)
            return;

        isTutorialHUDRevealPlaying = false;
        tutorialHUDRevealElapsed = 0.0f;
    }

    private void StopTutorialHUDReveal()
    {
        isTutorialHUDRevealPlaying = false;
        tutorialHUDRevealElapsed = 0.0f;
    }

    private void ApplyTutorialHUDAlpha(float _alpha)
    {
        tutorialHUDAlpha = Mathf.Clamp01(_alpha);

        if (abilityHUD == null)
            abilityHUD = GetComponentInChildren<AbilityHUD>(true);

        if (abilityHUD != null && abilityHUDCanvasGroup == null)
            abilityHUDCanvasGroup = GetOrAddCanvasGroup(abilityHUD.gameObject);

        if (coinCounter != null && coinCounterCanvasGroup == null)
            coinCounterCanvasGroup = GetOrAddCanvasGroup(coinCounter.gameObject);

        ApplyCanvasGroupAlpha(abilityHUDCanvasGroup, tutorialHUDAlpha);
        ApplyCanvasGroupAlpha(coinCounterCanvasGroup, tutorialHUDAlpha);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject _target)
    {
        if (_target == null)
            return null;

        CanvasGroup _canvasGroup = _target.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _target.AddComponent<CanvasGroup>();

        return _canvasGroup;
    }

    private void ApplyCanvasGroupAlpha(CanvasGroup _canvasGroup, float _alpha)
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = Mathf.Clamp01(_alpha);
    }
}
