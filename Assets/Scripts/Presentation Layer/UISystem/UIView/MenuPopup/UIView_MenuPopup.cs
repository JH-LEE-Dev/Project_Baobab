using UnityEngine;

public class UIView_MenuPopup : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

    }

    public void DependencyInjection()
    {

    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    public override void Update()
    {
        base.Update();

    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }
}
