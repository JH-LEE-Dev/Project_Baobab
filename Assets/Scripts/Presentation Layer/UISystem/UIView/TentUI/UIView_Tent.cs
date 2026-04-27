using System;
using TMPro;
using UnityEngine;

public class UIView_Tent : UIView
{
    private ISkillSystemProvider skillSystemProvider;
    private IMoneyData moneyData;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private UI_TentAbilityComponent abilityUIComponent;
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text carrotText;


    #region Default Logic

    // Tent UI 초기 설정을 진행한다.
    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        InitializeComponents();
        RefreshMoneyTexts();
    }

    // Tent UI가 사용하는 하위 컴포넌트들을 초기화한다.
    private void InitializeComponents()
    {
        abilityUIComponent?.Initialize(skillSystemProvider);
    }

    // 외부에서 전달된 스킬 시스템과 재화 데이터를 보관한다.
    public void DependencyInjection(ISkillSystemProvider _skillSystemProvider, IMoneyData _moneyData)
    {
        skillSystemProvider = _skillSystemProvider;
        moneyData = _moneyData;
        abilityUIComponent?.Initialize(skillSystemProvider);
        RefreshMoneyTexts();
    }

    // Tent UI가 사용할 루트 Transform을 찾는다.
    public override void SetupUI()
    {
        base.SetupUI();

        if (uiRoot == null)
            uiRoot = transform;
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
            RefreshMoneyTexts();
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
        RefreshMoneyTexts();
    }

    // 캐릭터의 전체 재화 값이 바뀌었을 때 현재 재화 텍스트를 갱신한다.
    public void CharactersMoneyChanged()
    {
        RefreshMoneyTexts();
    }

    // 현재 보유 중인 코인과 당근 수치를 텍스트로 갱신한다.
    private void RefreshMoneyTexts()
    {
        if (moneyData == null)
            return;

        if (coinText != null)
            coinText.text = moneyData.money.ToString();

        if (carrotText != null)
            carrotText.text = moneyData.carrot.ToString();
    }

    #endregion


    // Tent UI 정리 시 확장 포인트로 남겨둔다.
    public override void OnDestroy()
    {
    }

    public override void Refresh() //저장 파일 로드할 때 호출됨.
    {
       
    }
}
