using UnityEngine;

public abstract class UIView : MonoBehaviour
{
    protected UIViewContext viewCtx;

    [Header("UIView Settings")]
    [SerializeField] private UILayer layer = UILayer.None;
    [SerializeField] private bool startHidden = true;
    public bool bWorld = false;

    public UILayer Layer => layer;

    private bool bVisible;

    protected virtual void Awake()
    {
        if (startHidden)
        {
            gameObject.SetActive(false);
            bVisible = false;
        }
        else
        {
            bVisible = gameObject.activeSelf;
        }
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

        // 로컬라이징 컴포넌트 자동 주입
        LocalizedText[] localizers = GetComponentsInChildren<LocalizedText>(true);
        for (int i = 0; i < localizers.Length; i++)
        {
            localizers[i].Initialize(viewCtx.localizationManager);
        }

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
        gameObject.SetActive(true);
        OnShow();
    }

    public virtual void Hide()
    {
        if (!bVisible)
            return;

        bVisible = false;
        OnHide();
        gameObject.SetActive(false);
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
}