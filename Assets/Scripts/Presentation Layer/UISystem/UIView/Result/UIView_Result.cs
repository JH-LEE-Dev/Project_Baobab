using UnityEngine;
using System;
public class UIView_Result : UIView
{
    // // 이벤트
    public event Action GoHomeButtonClickedEvent;
    public event Action RetryButtonClickedEvent;
     
    // //외부 의존성

    // //내부 의존성

    #region Public Override Methods

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

    #endregion

    #region Protected Override Methods

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    #endregion

    #region Unity Event Functions

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    #endregion

    public void OpenResultUI()
    {
        
    }
}

