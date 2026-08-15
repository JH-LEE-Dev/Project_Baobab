using UnityEngine;

public class UIView_ScreenModal : UIView
{
    [Header("── References ──────────────────────────────────────────────────")]
    [SerializeField] private HUD_LootReveal hudLootReveal;

    private int pendingHideAnimations = 0;

    public LootType CurrentLootType { get; private set; } = LootType.None;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Initialize(UIViewContext ctx)
    {
        base.Initialize(ctx);

        if (null != hudLootReveal)
        {
            hudLootReveal.OnHideCompleted += OnLootRevealHideCompleted;
        }
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public override void Show()
    {
        base.Show();
    }

    // ESC(UIDepthController)로 닫힐 때도 E키로 닫을 때와 동일하게 hudLootReveal 페이드아웃을 거치도록
    // 한다. 실제 등록 해제(base.Hide)는 CheckAndCompleteHide에서 애니메이션 완료 후에 이뤄진다.
    public override void Hide()
    {
        if (false == IsVisible)
        {
            base.Hide();
            return;
        }

        InteractionStateChange(false, CurrentLootType);
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

        if (null != hudLootReveal)
        {
            hudLootReveal.OnHideCompleted -= OnLootRevealHideCompleted;
        }
    }

    public void LootPillarInteractStateChanged(bool _state, LootType _lootType)
    {
        InteractionStateChange(_state, _lootType);
    }

    private void InteractionStateChange(bool _state, LootType _lootType)
    {
        Debug.Log("InteractionStateChange: " + _state + ", LootType: " + _lootType);

        if (true == _state)
        {
            CurrentLootType = _lootType;
            base.Show();
            
            if (null != hudLootReveal)
            {
                hudLootReveal.Show(CurrentLootType, viewCtx?.localizationManager);
            }
        }
        else
        {
            CurrentLootType = LootType.None;
            BeginHideAnimations();
        }
    }

    private void BeginHideAnimations()
    {
        pendingHideAnimations = 0;

        if (null != hudLootReveal)
        {
            pendingHideAnimations++;
            hudLootReveal.Hide();
        }

        CheckAndCompleteHide();
    }

    private void OnLootRevealHideCompleted()
    {
        pendingHideAnimations--;
        CheckAndCompleteHide();
    }

    private void CheckAndCompleteHide()
    {
        if (0 >= pendingHideAnimations)
        {
            pendingHideAnimations = 0;
            base.Hide();
        }
    }
}
