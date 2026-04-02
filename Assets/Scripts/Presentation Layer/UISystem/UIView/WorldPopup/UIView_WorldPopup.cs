using System.Collections.Generic;
using UnityEngine;

public class UIView_WorldPopup : UIView
{
    private IInventory container;

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
    
    private void Init_UIStorage()
    {
        if (null == uiStoragePrefab)
            return;

        ui_Storage = Instantiate(uiStoragePrefab, uiRoot).GetComponent<UI_Storage>();
        if (null == ui_Storage)
            return;

        ui_Storage.Initialize();
    }

    public void DependencyInjection(IInventory _container)
    {
        container = _container;
        ui_Storage?.BindStorage(container);
    }

    protected override void OnShow()
    {
        base.OnShow();

        ui_Storage?.OnShow();
        ui_Storage?.Refresh();
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

    }
}