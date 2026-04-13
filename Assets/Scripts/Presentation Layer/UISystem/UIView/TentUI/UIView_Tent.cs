using UnityEngine;

public class UIView_Tent : UIView
{
    //외부 의존성
    private ISkillSystemProvider skillSystemProvider;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;

    #region Default Logic

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
    }

    public void DependencyInjection(ISkillSystemProvider _skillSystemProvider) //Initialize 직후 호출됨.
    {
        skillSystemProvider = _skillSystemProvider;
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

    public override void Update()
    {

    }

    public void TentInteract(bool _bInteract) // true -> tentUI Open, false -> tentUI Close
    {

    }

    #endregion

}
