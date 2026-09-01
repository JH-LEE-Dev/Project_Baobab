using UnityEngine;

public abstract class UIView : MonoBehaviour, IUIDepthCloseable
{
    protected UIViewContext viewCtx;

    [Header("UIView Settings")]
    [SerializeField] private UILayer layer = UILayer.None;
    //[SerializeField] private bool startHidden = true;
    [SerializeField] private bool bCloseableByESC = false;
    public bool bWorld = false;
    public bool bScreenSpace = false;
    public bool bOverlay = false;

    public UILayer Layer => layer;
    public bool IsCloseableByESC => bCloseableByESC;
    public bool IsVisible => bVisible;
    public bool IsActive => gameObject.activeSelf;

    private bool bVisible;

    protected virtual void Awake()
    {
    }

    public virtual void OnDestroy()
    {
        // 보이는 상태(=뎁스 스택에 등록된 상태)로 파괴되면 스택에 죽은 참조가 남는다.
        // 지금은 뷰와 UIDepthController가 같은 GameplayUIInstaller에 붙어 함께 파괴되므로
        // 실제로 남는 경우가 없지만, 둘의 수명이 갈리는 순간 ESC가 죽는 형태로 터진다.
        // 등록한 쪽이 해제까지 책임지도록 여기서 짝을 맞춘다. (등록된 적이 없으면 무시된다)
        if (bCloseableByESC && null != viewCtx?.depthController)
        {
            viewCtx.depthController.UnregisterView(this);
        }
    }

    public virtual void Update()
    {

    }

    public virtual void Initialize(UIViewContext ctx)
    {
        viewCtx = ctx;

        SetupUI();
    }

    public virtual void SetupUI()
    {

    }

    public virtual void Show()
    {
        if (bVisible)
            return;

        bVisible = true;
        
        if (bCloseableByESC && viewCtx?.depthController != null)
        {
            viewCtx.depthController.RegisterView(this);
        }

        OnShow();
    }

    public virtual void Hide()
    {
        if (!bVisible)
            return;

        bVisible = false;

        if (bCloseableByESC && viewCtx?.depthController != null)
        {
            viewCtx.depthController.UnregisterView(this);
        }

        OnHide();
    }

    protected virtual void OnShow() { }

    protected virtual void OnHide() { }

    protected virtual void SetAnchorToCanvas(Transform transform)
    {
        RectTransform rt = transform.GetComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;   // (0, 0)
        rt.anchorMax = Vector2.one;    // (1, 1)

        rt.offsetMin = Vector2.zero;   // Left, Bottom
        rt.offsetMax = Vector2.zero;   // Right, Top
    }

    public virtual void Release()
    {

    }

    public virtual void Refresh()
    {

    }
}