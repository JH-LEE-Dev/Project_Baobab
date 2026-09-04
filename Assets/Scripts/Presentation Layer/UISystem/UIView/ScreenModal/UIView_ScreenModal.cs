using System;
using UnityEngine;
using UnityEngine.UI;

public class UIView_ScreenModal : UIView
{
    // ESC/패드 Cancel로 닫힐 때는 UIDepthController가 Hide()를 직접 호출해 LootPillarInteractSignal(false)
    // 경로를 거치지 않으므로, 닫힘을 항상 감지하려면 상호작용 토글이 아니라 이 UI 자체의 Hide 시점을 봐야 한다.
    // (UIView_Tent.TentUIClosedEvent와 동일한 역할)
    public event Action ScreenModalClosedEvent;

    [Header("── References ──────────────────────────────────────────────────")]
    [SerializeField] private HUD_LootReveal hudLootReveal;

    private int pendingHideAnimations = 0;

    // 이 창을 열기 직전의 입력 모드/이동 잠금 상태. 닫을 때 Gameplay·false를 박는 대신 이 값으로 되돌린다.
    //
    // 입력 모드는 스택이 아니라 단일 값이고 PauseMove도 소유자별 잠금이 아니라 단일 bool이라
    // (InputReader.IsMovePaused 참고), 무조건 되돌리면 아직 잠겨 있어야 하는 다른 시스템의 잠금까지
    // 함께 풀린다. UIView_Tent·UIView_ESC와 같은 방식으로 열기 직전 값을 보존한다.
    //
    // 잠금을 걸고 푸는 것은 OnShow()/OnHide()에서만 한다. 둘 다 base.Show()/base.Hide()가 실제로 표시
    // 상태를 바꿀 때만 호출되므로(중복 호출은 base에서 조기 return된다) 걸기/풀기가 정확히 1:1로 짝지어진다.
    private EInputMode inputModeBeforeShow = EInputMode.Gameplay;
    private bool bMovePausedBeforeShow = false;

    // 화면 전체를 덮는 투명 레이캐스트 차단막. SetupUI()에서 한 번 만들어 이 뷰의 첫 번째 자식으로 깔아둔다.
    //
    // 입력 모드(UI)와 PauseMove는 키/패드 입력만 막을 뿐 EventSystem 포인터 입력까지는 막지 못한다.
    // 그런데 전리품 연출(HUD_LootReveal)은 화면 전체가 아니라 가로 띠 형태라, 띠 바깥 영역은 뒤에
    // 열려 있던 UI(특히 인벤토리 슬롯 - UI_InventorySlot은 IPointerClickHandler다)로 마우스 클릭이
    // 그대로 통과한다. 이 창이 떠 있는 동안에는 마우스 조작도 함께 막아야 하므로 차단막을 깐다.
    //
    // 이 뷰의 자식이므로 OnHide()의 gameObject.SetActive(false)와 함께 자동으로 꺼진다.
    private Image raycastBlocker;

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

        EnsureRaycastBlocker();
    }

    // (raycastBlocker 주석 참고)
    private void EnsureRaycastBlocker()
    {
        if (null != raycastBlocker)
            return;

        GameObject _blockerObject = new GameObject("RaycastBlocker", typeof(RectTransform), typeof(Image));
        _blockerObject.layer = gameObject.layer;

        RectTransform _blockerRect = _blockerObject.GetComponent<RectTransform>();
        _blockerRect.SetParent(transform, false);

        // 연출 요소들보다 뒤에 깔아 그리기 순서를 건드리지 않는다. 레이캐스트는 위에 아무것도 없는
        // 영역에서만 이 차단막에 닿으므로 연출 쪽 동작에는 영향이 없다.
        _blockerRect.SetAsFirstSibling();
        SetAnchorToCanvas(_blockerRect);

        raycastBlocker = _blockerObject.GetComponent<Image>();
        raycastBlocker.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        raycastBlocker.raycastTarget = true;

        // 알파 0인 그래픽은 cullTransparentMesh가 켜져 있으면 canvasRenderer.cull이 서고, GraphicRaycaster가
        // cull된 그래픽을 통째로 건너뛴다(= 차단이 아예 동작하지 않는다). 명시적으로 꺼둔다.
        raycastBlocker.canvasRenderer.cullTransparentMesh = false;
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

        // 반드시 아래의 SetInputMode(UI)/PauseMove(true)보다 먼저 읽는다. (inputModeBeforeShow 주석 참고)
        inputModeBeforeShow = viewCtx?.inputManager?.CurrentInputMode ?? EInputMode.Gameplay;
        bMovePausedBeforeShow = null != viewCtx?.inputManager && viewCtx.inputManager.IsMovePaused;

        // 전리품 연출이 화면을 덮는 동안에는 다른 전체 UI와 마찬가지로 캐릭터 조작과 인벤토리 조작을 막는다.
        // 입력 모드를 UI로 바꾸면 이동/공격/포션/상호작용은 물론 인벤토리 키까지 한 번에 걸러지고
        // (InputReader.CanDispatchGameplay), PauseMove는 키를 누른 채 창이 열렸을 때 캐릭터가 계속
        // 걸어가는 것을 막는다.
        viewCtx?.inputManager?.SetInputMode(EInputMode.UI);
        viewCtx?.inputManager?.PauseMove(true);
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);

        // Gameplay/false를 박지 않고 열기 직전 값으로 되돌린다. (inputModeBeforeShow 주석 참고)
        viewCtx?.inputManager?.SetInputMode(inputModeBeforeShow);
        viewCtx?.inputManager?.PauseMove(bMovePausedBeforeShow);

        ScreenModalClosedEvent?.Invoke();
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

            // 닫힘 애니메이션이 아직 진행 중인데 다시 열리면 hudLootReveal.Show()가 그 페이드 트윈을
            // 죽여버려 OnHideCompleted가 영영 오지 않는다. 그러면 pendingHideAnimations에 1이 남아
            // 다음 닫기에서 카운트가 0으로 떨어지지 않고, base.Hide()가 호출되지 않아 OnHide에서 푸는
            // 입력 잠금(SetInputMode/PauseMove)까지 그대로 굳는다. 여는 시점에 카운터를 비워 끊는다.
            pendingHideAnimations = 0;

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
