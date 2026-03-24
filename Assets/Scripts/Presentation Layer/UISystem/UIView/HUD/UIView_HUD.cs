using System;
using UnityEngine;

public class UIView_HUD : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject hudEquipmentPrefab;

    private HUD_Equipment hudEquipment;

    private ICharacter character;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        Init_HUDEquipment();
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

    private void Init_HUDEquipment()
    {
        hudEquipment = Instantiate(hudEquipmentPrefab, this.transform).GetComponent<HUD_Equipment>();

        if (null != hudEquipment)
            hudEquipment.Initialize();
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;
    }
}
