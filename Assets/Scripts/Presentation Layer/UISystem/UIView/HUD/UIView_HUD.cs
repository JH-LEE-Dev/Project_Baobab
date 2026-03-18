using System.Collections.Generic;
using UnityEngine;

public class UIView_HUD : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

    }

    public override void OnDestroy()
    {

    }

    protected override void OnShow() //이 UI가 켜졌을 때 호출 됨.
    {
        base.OnShow();

        
    }

    protected override void OnHide() //이 UI가 꺼졌을 때 호출 됨.
    {


        base.OnHide();
    }
}
