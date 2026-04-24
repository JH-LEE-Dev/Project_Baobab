using System;
using UnityEngine;

public class UIView_HUD : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject hudEquipmentPrefab;
    [SerializeField] private GameObject hudSteminaBarPrefab;

    private HUD_Equipment hudEquipment;
    private HUD_ProgressBar hudSteminaBar;

    private ICharacter character;


    #region Default Logic

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_HUDEquipment();
        Init_HUDSteminaBar();
    }

    public override void OnDestroy()
    {
        hudEquipment?.OnDestroy();
    }

    protected override void OnShow() //이 UI가 켜졌을 때 호출 됨.
    {
        base.OnShow();

        hudEquipment?.OnShow();
    }

    protected override void OnHide() //이 UI가 꺼졌을 때 호출 됨.
    {
        hudEquipment?.OnHide();

        base.OnHide();
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;
    }

    public void DependencyInjection()
    {

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

    public void InventorySpecChanged() //인벤토리 스펙 변동 시 호출
    {

    }
}
