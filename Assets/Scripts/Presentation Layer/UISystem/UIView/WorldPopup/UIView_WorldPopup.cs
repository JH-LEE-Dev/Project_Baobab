using UnityEngine;

public class UIView_WorldPopup : UIView
{
    private IInventory container;

    //내부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;

    //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
    }

    public void DependencyInjection(IInventory _container)
    {
        container = _container;
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
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
}