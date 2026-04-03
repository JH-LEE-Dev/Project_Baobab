using System.Collections.Generic;
using UnityEngine;

public class UIView_WorldPopup : UIView
{
    private IInventory container;
    private ILogCutter logCutter;


    //내부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiStoragePrefab;

    private UI_Storage ui_Storage;

    //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_UIStorage();
    }

    private void BindEvents()
    {
        logCutter.CuttingStartEvent -= LogToCutter;
        logCutter.CuttingStartEvent += LogToCutter;
    }

    private void ReleaseEvents()
    {
        logCutter.CuttingStartEvent -= LogToCutter;
    }

    public override void Release()
    {
        base.Release();

        ReleaseEvents();
    }
    
    private void Init_UIStorage()
    {
        if (null == uiStoragePrefab)
            return;

        ui_Storage = Instantiate(uiStoragePrefab, uiRoot).GetComponent<UI_Storage>();
        if (null == ui_Storage)
            return;

        ui_Storage.Initialize();
    }

    public void DependencyInjection(IInventory _container, ILogCutter _logCutter)
    {
        container = _container;
        logCutter = _logCutter;

        ui_Storage?.BindStorage(container);
        
        BindEvents();
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        ui_Storage?.OnHide();

        base.OnHide();
    }

    public override void Update()
    {
        base.Update();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }
    
    //원목 보관함 최신화됨.
    public void ContainerUpdated()
    {
        if (container == null)
        {
            Debug.LogWarning("[UIView_WorldPopup] Container is null.");
            return;
        }

        ui_Storage?.Refresh();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("--- Container Inventory Status ---");
        
        var slots = container.inventorySlots;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.itemData == null || slot.count <= 0) continue;

            sb.AppendFormat("Item: {0}, Total Count: {1}\n", slot.itemData.itemType, slot.count);

            var logStates = slot.logStateCounts;
            if (logStates != null && logStates.Length > 0)
            {
                for (int j = 0; j < logStates.Length; j++)
                {
                    if (logStates[j].count > 0)
                    {
                        sb.AppendFormat("  - State: {0}, Count: {1}\n", logStates[j].state, logStates[j].count);
                    }
                }
            }
        }
        sb.AppendLine("----------------------------------");
        Debug.Log(sb.ToString());
    }

    // true : 원목 보관함과 상호작용 가능 거리에 들어옴
    // false : 상호작용 거리에서 나감
    public void LogContainerInteractStateChanged(bool _state)
    {
        if (true == _state)
        {
            ui_Storage?.OnShow();
            ui_Storage?.Refresh();
        }
        else
        {
            ui_Storage?.OnHide();
        }
    }

    //원목이 절단기로 들어감.
    private void LogToCutter(ILogItemData _itemData)
    {
        Debug.Log(logCutter.timeRemaining);
        //logCutter.logToCut -> 절단될 원목.
        //logCutter.timeRemaining -> 남은 절단 시간.

        
    }
}