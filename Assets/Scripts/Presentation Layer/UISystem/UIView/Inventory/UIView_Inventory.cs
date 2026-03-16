using UnityEngine;

public class UIView_Inventory : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject uiPrefab; //생성할 uiPrefab, 여러 개 추가해도 됨.

    private GameObject ui;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (uiPrefab != null)
            ui = Instantiate(uiPrefab, uiRoot);

        if (ui != null)
            ui.SetActive(false);
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
