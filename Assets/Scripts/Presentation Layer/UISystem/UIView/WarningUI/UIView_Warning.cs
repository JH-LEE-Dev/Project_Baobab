using System;
using UnityEngine;

public class UIView_Warning : UIView
{
    // 이벤트
    public event Action DeActivateWarningUIEvent;

    // 외부 의존성

    // 내부 의존성

    // 속성
    public bool bApproved = false; // 승인 버튼을 누르면 true로 바꿀것.

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Release()
    {
        base.Release();
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
        DeActivateWarningUI();
        bApproved = false;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    private void DeActivateWarningUI() //WarningUI가 닫힐 때 호출할 것.
    {
        DeActivateWarningUIEvent?.Invoke();
    }
}
