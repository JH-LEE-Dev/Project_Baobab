using PresentationLayer.UISystem.CustomNumber;
using UnityEngine;

public class UIView_Tent : UIView
{
    private ISkillSystemProvider skillSystemProvider;
    private IMoneyData moneyData;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private UI_TentAbilityComponent abilityUIComponent;
    [SerializeField] private AbilityNoticeStackPresenter abilityNoticePresenter;
    [SerializeField] private RectTransform moneyPivot;
    [SerializeField] private GameObject currencyCounterHUDPrefab;

    private CurrencyCounterHUD coinCounter;

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
        abilityUIComponent?.Initialize(skillSystemProvider);
        InitializeMoneyCounters();
    }

    // 외부에서 전달된 스킬 시스템과 재화 데이터를 보관한다.
    public void DependencyInjection(ISkillSystemProvider _skillSystemProvider, IMoneyData _moneyData)
    {
        skillSystemProvider = _skillSystemProvider;
        moneyData = _moneyData;
        abilityUIComponent?.Initialize(skillSystemProvider);
        RefreshMoneyTexts(false);
    }

    // Tent UI가 사용할 루트 Transform을 찾는다.
    public override void SetupUI()
    {
        base.SetupUI();

        if (uiRoot == null)
            uiRoot = transform;

        if (abilityNoticePresenter == null)
            abilityNoticePresenter = GetComponent<AbilityNoticeStackPresenter>();

        if (moneyPivot == null)
            moneyPivot = FindChildByName(transform, "MoneyPivot") as RectTransform;
    }

    // 능력창이 열려 있는 동안 입력 처리와 상태 갱신을 진행한다.
    public override void Update()
    {
        abilityUIComponent?.Tick();
    }

    protected override void OnShow()
    {
        base.OnShow();
        viewCtx.inputManager.PauseMove(true);

        RefreshMoneyTexts(false);
        abilityUIComponent?.Open();
    }


    protected override void OnHide()
    {
        base.OnHide();
        viewCtx.inputManager.PauseMove(false);

        abilityUIComponent?.Close();
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

    //특성 찍기 성공했을 때 누적 값 제공 함수.
    public void DeclareSkillAccumulativeValue(SkillAccumulatedValueData _declareSkillAccumulativeValueSignal)
    {
        if (abilityNoticePresenter == null)
            abilityNoticePresenter = GetComponent<AbilityNoticeStackPresenter>();

        if (abilityNoticePresenter == null)
            return;

        abilityNoticePresenter.ShowNotice(FormatSkillAccumulatedValue(_declareSkillAccumulativeValueSignal));
    }

    private string FormatSkillAccumulatedValue(SkillAccumulatedValueData _data)
    {
        return _data.type + " " + FormatAccumulatedAmount(_data.amount);
    }

    private string FormatAccumulatedAmount(float _amount)
    {
        if (Mathf.Approximately(_amount, Mathf.Round(_amount)))
            return Mathf.RoundToInt(_amount).ToString();

        return _amount.ToString("0.##");
    }
}
