using System;
using PresentationLayer.UISystem.CustomNumber;
using UnityEngine;

public class UIView_Tent : UIView
{
    private static readonly MoneyType[] MoneyDisplayOrder =
    {
        MoneyType.Coin,
        MoneyType.Carrot,
        MoneyType.SunEssence,
        MoneyType.MoonEssence,
        MoneyType.LightningEssnece,
    };

    private ISkillSystemProvider skillSystemProvider;
    private IMoneyData moneyData;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private UI_TentAbilityComponent abilityUIComponent;
    [SerializeField] private RectTransform moneyPivot;
    [SerializeField] private GameObject currencyCounterHUDPrefab;
    [SerializeField] private float moneyCounterSpacing = 20.0f;

    private CurrencyCounterHUD[] moneyCounters;
    private bool[] moneyVisibleOnce;

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

        if (moneyPivot == null)
            moneyPivot = FindChildByName(transform, "MoneyPivot") as RectTransform;
    }

    // 능력창이 열려 있는 동안 입력 처리와 상태 갱신을 진행한다.
    public override void Update()
    {
        abilityUIComponent?.Tick();
    }

    // Tent와 상호작용하면 곧바로 능력창을 열고, 상호작용이 끝나면 닫는다.
    public void TentInteract(bool _bInteract)
    {
        if (_bInteract)
        {
            RefreshMoneyTexts(false);
            abilityUIComponent?.Open();
        }
        else
        {
            abilityUIComponent?.Close();
        }
    }

    #endregion


    #region Money UI

    // 캐릭터가 특정 재화를 획득했을 때 현재 재화 텍스트를 갱신한다.
    public void CharacterEarnMoney(MoneyType _moneyType)
    {
        InitializeMoneyCounters();

        if (moneyVisibleOnce != null && (int)MoneyType.None < (int)_moneyType && (int)_moneyType < (int)MoneyType.Max)
            moneyVisibleOnce[(int)_moneyType] = true;

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

        if (moneyCounters == null)
            return;

        for (int i = 0; i < MoneyDisplayOrder.Length; i++)
        {
            MoneyType _moneyType = MoneyDisplayOrder[i];
            int _value = GetMoneyValue(_moneyType);

            if (MoneyType.Coin == _moneyType || 0 < _value)
                moneyVisibleOnce[(int)_moneyType] = true;

            CurrencyCounterHUD _counter = moneyCounters[i];
            if (null == _counter)
                continue;

            _counter.gameObject.SetActive(moneyVisibleOnce[(int)_moneyType]);

            if (_withMotion)
                _counter.SetNumberAnimated(_value);
            else
                _counter.SetNumber(_value);
        }

        RefreshMoneyCounterLayout();
    }

    private void InitializeMoneyCounters()
    {
        if (moneyCounters != null)
            return;

        moneyVisibleOnce = new bool[(int)MoneyType.Max];
        moneyVisibleOnce[(int)MoneyType.Coin] = true;
        moneyCounters = new CurrencyCounterHUD[MoneyDisplayOrder.Length];

        if (moneyPivot == null)
            moneyPivot = FindChildByName(transform, "MoneyPivot") as RectTransform;

        if (moneyPivot == null || currencyCounterHUDPrefab == null)
            return;

        for (int i = 0; i < MoneyDisplayOrder.Length; i++)
        {
            GameObject _counterObject = Instantiate(currencyCounterHUDPrefab, moneyPivot);
            _counterObject.name = $"CurrencyCounterHUD_{MoneyDisplayOrder[i]}";

            RectTransform _counterRect = _counterObject.GetComponent<RectTransform>();
            if (null != _counterRect)
            {
                _counterRect.anchorMin = new Vector2(0.0f, 1.0f);
                _counterRect.anchorMax = new Vector2(0.0f, 1.0f);
                _counterRect.pivot = new Vector2(0.0f, 0.5f);
                _counterRect.anchoredPosition = Vector2.zero;
            }

            CurrencyCounterHUD _counter = _counterObject.GetComponent<CurrencyCounterHUD>();
            if (null == _counter)
                continue;

            _counter.Initialize();
            _counter.SetMoneyType(MoneyDisplayOrder[i]);
            _counter.SetNumber(0);
            _counter.gameObject.SetActive(MoneyType.Coin == MoneyDisplayOrder[i]);

            moneyCounters[i] = _counter;
        }

        RefreshMoneyCounterLayout();
    }

    private void RefreshMoneyCounterLayout()
    {
        if (moneyCounters == null)
            return;

        int _visibleIndex = 0;
        for (int i = 0; i < moneyCounters.Length; i++)
        {
            CurrencyCounterHUD _counter = moneyCounters[i];
            if (null == _counter || false == _counter.gameObject.activeSelf)
                continue;

            RectTransform _counterRect = _counter.GetComponent<RectTransform>();
            if (null != _counterRect)
                _counterRect.anchoredPosition = new Vector2(0.0f, -moneyCounterSpacing * _visibleIndex);

            _visibleIndex++;
        }
    }

    private int GetMoneyValue(MoneyType _moneyType)
    {
        if (moneyData == null)
            return 0;

        switch (_moneyType)
        {
            case MoneyType.Coin:
                return moneyData.money;
            case MoneyType.Carrot:
                return moneyData.carrot;
            case MoneyType.SunEssence:
                return moneyData.sunEssence;
            case MoneyType.MoonEssence:
                return moneyData.moonEssence;
            case MoneyType.LightningEssnece:
                return moneyData.lightningEssence;
            default:
                return 0;
        }
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
}
