using System;
using UnityEngine;

public class UIView_OverUIPopup : UIView
{
    // 이벤트
    public event Action CompanyLogoProductionCompletedEvent;

    // 내부 의존성
    [Header("Opening Production")]
    [SerializeField] private UI_OpeningProduction openingProduction;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        if (null != openingProduction)
        {
            openingProduction.Initialize(_ctx?.localizationManager);
        }
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public void PlayCompanyLogo()
    {
        if (null != openingProduction)
        {
            openingProduction.PlayIntroScene(OnIntroSceneCompleted);
        }
    }

    public override void Release()
    {
        base.Release();
        CompanyLogoProductionCompletedEvent = null;
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
        if (null != openingProduction)
        {
            openingProduction.StopOpeningProduction();
        }
    }

    private void OnIntroSceneCompleted()
    {
        CompanyLogoProductionCompletedEvent?.Invoke();
    }

    protected override void Awake()
    {
        base.Awake();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        CompanyLogoProductionCompletedEvent = null;
    }
}
