using System;
using PresentationLayer.UISystem.HUD;
using UnityEngine;

public class UIView_HUD : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject hudEquipmentPrefab;
    [SerializeField] private GameObject hudSteminaBarPrefab;
    [SerializeField] private GameObject uiCoinPrefab;
    [SerializeField] private GameObject uiCarrotCoinPrefab;

    private HUD_Equipment hudEquipment;
    private HUD_ProgressBar hudSteminaBar;
    private UI_Coin uI_Coin;
    private UI_Coin uI_CarrotCoin;

    private ICharacter character;
    private IMoneyData moneyData;


#region Default Logic

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_HUDEquipment();
        Init_HUDSteminaBar();
        Init_Coin();
        Init_CarrotCoin();
    }

    public override void OnDestroy()
    {
        hudEquipment?.OnDestroy();
    }

    protected override void OnShow() //이 UI가 켜졌을 때 호출 됨.
    {
        base.OnShow();

        hudEquipment?.OnShow();
        uI_Coin?.OnShow();
        uI_CarrotCoin?.OnShow();
    }

    protected override void OnHide() //이 UI가 꺼졌을 때 호출 됨.
    {
        hudEquipment?.OnHide();
        uI_Coin?.OnHide();
        uI_CarrotCoin?.OnHide();

        base.OnHide();
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;
    }

    public void DependencyInjection(IMoneyData _moneyData)
    {
        moneyData = _moneyData;

        uI_Coin?.BindMoneyData(moneyData, MoneyType.Coin);
        uI_CarrotCoin?.BindMoneyData(moneyData, MoneyType.Carrot);
    }

    public override void Update()
    {
        if (null != character && null != character.pHealthComponent)
            UsedSteminaEvent(character.pHealthComponent.GetCurrentStamina(), character.pHealthComponent.GetMaxStamina());
    }

#endregion

#region HUD_Equipment Logic

    private void Init_HUDEquipment()
    {
        hudEquipment = Instantiate(hudEquipmentPrefab, this.transform).GetComponent<HUD_Equipment>();

        if (null != hudEquipment)
        {
            hudEquipment.Initialize();
            hudEquipment.UpdateState(WeaponMode.Axe, 0);
        }
    }

#endregion
    
#region HUD_Stemina Logic

    private void Init_HUDSteminaBar()
    {
        hudSteminaBar = Instantiate(hudSteminaBarPrefab, this.transform).GetComponent<HUD_ProgressBar>();

        if (null != hudSteminaBar)
            hudSteminaBar.Initialize();
    }

    private void UsedSteminaEvent(float _currentStemina, float _maxStemina)
    {
        float newRatio = _currentStemina / _maxStemina;
        hudSteminaBar?.UpdateValue(Mathf.Clamp01(newRatio));
    }

#endregion

    //무기 모드 변환 시 호출. 기본값은 Axe
    public void WeaponModeChanged(WeaponMode _currentWeaponMode)
    {
        hudEquipment?.UpdateState(_currentWeaponMode, 0);
    }

    public void CharacterEarnMoney(MoneyType _moneyType) //캐릭터가 돈을 얻었을 때,
    {
        if (MoneyType.Coin == _moneyType)
            uI_Coin?.UpdateMoneyText();
        else if (MoneyType.Carrot == _moneyType)
            uI_CarrotCoin?.UpdateMoneyText();
    }

#region Coin UI

    private void Init_Coin()
    {
        if (null == uiCoinPrefab)
            return;

        uI_Coin = Instantiate(uiCoinPrefab, this.transform).GetComponent<UI_Coin>();

        if (null == uI_Coin)
            return;

        uI_Coin.Initialize();
    }

    private void Init_CarrotCoin()
    {
        if (null == uiCarrotCoinPrefab)
            return;

        uI_CarrotCoin = Instantiate(uiCarrotCoinPrefab, this.transform).GetComponent<UI_Coin>();

        if (null == uI_CarrotCoin)
            return;

        uI_CarrotCoin.Initialize();
    }

    public void InventorySpecChanged() //인벤토리 스펙 변동 시 호출
    {

    }

#endregion
}
