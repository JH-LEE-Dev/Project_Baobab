using UnityEngine;

public abstract class UIView : MonoBehaviour
{
    protected UIViewContext viewCtx;

    [Header("UIView Settings")]
    [SerializeField] private UILayer layer = UILayer.None;
    //[SerializeField] private bool startHidden = true;
    [SerializeField] private bool bCloseableByESC = false;
    public bool bWorld = false;
    public bool bOverlay = false;

    public UILayer Layer => layer;
    public bool IsCloseableByESC => bCloseableByESC;
    public bool IsVisible => bVisible;

    private bool bVisible;

    protected virtual void Awake()
    {
    }

    public virtual void OnDestroy()
    {

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