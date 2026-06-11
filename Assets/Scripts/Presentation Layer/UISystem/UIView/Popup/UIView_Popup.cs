using System;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;

/// <summary>
/// 인벤토리, 팝업 관련 UI들을 총괄 관리하는 UIView 클래스입니다.
/// </summary>
public class UIView_Popup : UIView
{
    // //이벤트
    public event Action<bool> InventoryUIOpendEvent;
    public event Action goHomeButtonClickedEvent;
    public event Action<IInventorySlot> sendDeleteItemEvent;

    // //외부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiInventoryPrefab;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private string mapTransitionMotionTag = "GoDown";

    // //내부 의존성
    private IInventory inventory;
    private IMoneyData moneyData;
    private UI_Inventory uiInventory;

    private const int defaultPopupCap = 12;

    private MapType currentMapType;
    private ForestType currentForestType;
    private bool isAutoOpenedByInteraction = false;

    // //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null != omp)
            omp.Initialize();

        InitInventory();
        BindEvents();
    }

    public void DependencyInjection(IInventory _inventory, IMoneyData _moneyData)
    {
        inventory = _inventory;
        moneyData = _moneyData;

        if (null != uiInventory)
            uiInventory.BindData(inventory, _moneyData);
    }

    public void InventoryShowEvent()
    {
        if (null != uiInventory)
            uiInventory.Refresh();
    }

    public void InventorySpecChanged()
    {
        if (null != uiInventory)
            uiInventory.Refresh();
    }

    public void CharacterEarnMoney(MoneyType _moneyType)
    {
        if (null != uiInventory)
            uiInventory.CharacterEarnMoney(_moneyType);
    }

    public void CharactersMoneyChanged()
    {
        if (null != uiInventory)
            uiInventory.CharactersMoneyChanged();
    }

    public override void Refresh()
    {
        if (null != uiInventory)
            uiInventory.Refresh();
    }

    public void SetCurrentMapType(MapType _currentMapType, ForestType _currentForestType)
    {
        currentMapType = _currentMapType;
        currentForestType = _currentForestType;

        if (null != uiInventory)
            uiInventory.MapChanged(_currentMapType);
    }

    public void LogContainerCanInteract(bool _bCanInteract)
    {
        if (true == _bCanInteract)
        {
            if (null != uiInventory && false == uiInventory.isOpening)
            {
                InventoryUIOpendEvent.Invoke(true);
                isAutoOpenedByInteraction = true;
            }
        }
        else
        {
            if (true == isAutoOpenedByInteraction)
                InventoryUIOpendEvent.Invoke(isAutoOpenedByInteraction = false);
        }
    }

    // //프라이빗 메서드

    private void InitInventory()
    {
        if (null == uiInventoryPrefab)
            return;

        GameObject _invObj = Instantiate(uiInventoryPrefab, uiRoot);
        uiInventory = _invObj.GetComponent<UI_Inventory>();

        if (null == uiInventory)
            return;

        uiInventory.Initialize(uiRoot, HandleHomingButtonClicked, HandleInventoryHover, HandleInventoryUnHover);
        uiInventory.OnHide();
    }

    private void BindEvents()
    {
        if (null != uiInventory)
        {
            uiInventory.sendDeleteItemEvent -= HandleDeleteItem;
            uiInventory.sendDeleteItemEvent += HandleDeleteItem;
        }
    }

    private void ReleaseEvents()
    {
        if (null != uiInventory)
        {
            uiInventory.sendDeleteItemEvent -= HandleDeleteItem;
            uiInventory.inventoryHoverEvent -= HandleInventoryHover;
            uiInventory.inventoryUnHoverEvent -= HandleInventoryUnHover;
        }
    }

    private void HandleDeleteItem(IInventorySlot _inData)
    {
        sendDeleteItemEvent?.Invoke(_inData);
    }

    private void HandleHomingButtonClicked()
    {
        goHomeButtonClickedEvent?.Invoke();
    }

    private void HandleInventoryHover()
    {
        if (null != viewCtx && null != viewCtx.inputManager)
            viewCtx.inputManager.SetCursorHoveredOnUI(true);
    }

    private void HandleInventoryUnHover()
    {
        if (null != viewCtx && null != viewCtx.inputManager)
            viewCtx.inputManager.SetCursorHoveredOnUI(false);
    }

    // //유니티 이벤트 함수

    protected override void OnShow()
    {
        base.OnShow();

        if (null != uiInventory)
            uiInventory.OnShow();
            
        isAutoOpenedByInteraction = false;
    }

    protected override void OnHide()
    {
        if (null != uiInventory)
            uiInventory.OnHide();
            
        isAutoOpenedByInteraction = false;

        base.OnHide();
    }

    public override void OnDestroy()
    {
        ReleaseEvents();

        if (null != uiInventory)
            uiInventory.Release();

        base.OnDestroy();
    }

    public void LoosAllInventoryItems()
    {
        uiInventory?.ClearNotification();
    }

    public void ItemAddedToInventory()
    {
        InventoryShowEvent();
        uiInventory?.UpdateNotification();
    }

    public void ItemRemovedFromInventory()
    {
        InventoryShowEvent();
        uiInventory?.ClearNotification();
    }

    public void InventoryIsFull()
    {
        if (true == uiInventory?.isOpening)
            return;

        InventoryUIOpendEvent?.Invoke(true);
    }

    public void PopupGoDown()
    {
        if (null != omp)
        {
            omp.Play(mapTransitionMotionTag, bReset: true);
        }

        if (null != uiInventory && true == uiInventory.isOpening)
            InventoryUIOpendEvent?.Invoke(false);
    }

    public void PopupGoUp()
    {
        if (null != omp)
        {
            omp.PlayBackward(mapTransitionMotionTag, bReset: true);
        }
    }
}
