using UnityEngine;

public class UIView_CursorBox : UIView, ICursorBoxUI
{
    protected override void Awake()
    {
        base.Awake();
        // Overlay 캔버스의 자식이 되도록 설정
        bOverlay = true;
    }

    public override void Initialize(UIViewContext ctx)
    {
        base.Initialize(ctx);
        // 초기화 로직 구현
    }

    public override void SetupUI()
    {
        base.SetupUI();
        // UI 컴포넌트 세팅 로직 구현
    }

    protected override void OnShow()
    {
        base.OnShow();
        // 뷰가 보여질 때 실행될 로직 구현
    }

    protected override void OnHide()
    {
        base.OnHide();
        // 뷰가 숨겨질 때 실행될 로직 구현
    }

    public override void Refresh()
    {
        base.Refresh();
        // UI 갱신 로직 구현
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        // 파괴 시 정리 로직 구현
    }
}
