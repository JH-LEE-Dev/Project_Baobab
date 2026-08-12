using UnityEngine;

public class UIView_ScreenModal : UIView
{
    protected override void Awake()
    {
        base.Awake();
    }

    public override void Initialize(UIViewContext ctx)
    {
        base.Initialize(ctx);
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public override void Show()
    {
        base.Show();
    }

    public override void Hide()
    {
        base.Hide();
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
    }

    public override void Update()
    {
        base.Update();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Release()
    {
        base.Release();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }
}
