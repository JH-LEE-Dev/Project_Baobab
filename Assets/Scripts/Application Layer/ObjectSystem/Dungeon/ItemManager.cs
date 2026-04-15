using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    // 내부 의존성
    private LogItemController logItemController;
    private CarrotItemController carrrotItemController;
    private IInventoryChecker inventoryChecker;

    public void Initialize(IInventoryChecker _inventoryChecker)
    {
        inventoryChecker = _inventoryChecker;

        logItemController = GetComponentInChildren<LogItemController>();
        carrrotItemController = GetComponentInChildren<CarrotItemController>();

        if (logItemController != null)
        {
            logItemController.Initialize(inventoryChecker);
        }

        if (carrrotItemController != null)
        {
            carrrotItemController.Initialize();
        }
        
        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {

    }

    private void ReleaseEvents()
    {

    }

    // 외부에서 접근하기 위한 래퍼 메서드 (필요한 경우)
    public void SpawnLogItem(TreeObj _treeObj)
    {
        logItemController?.SpawnLogItem(_treeObj);
    }

    public void ReturnLogToPool(LogItem _item)
    {
        logItemController?.ReturnToPool(_item);
    }

    public void SpawnCarrotItem(Vector3 _position)
    {
        carrrotItemController?.SpawnCarrotItem(_position);
    }

    public void ReturnCarrotToPool(CarrotItem _item)
    {
        carrrotItemController?.ReturnToPool(_item);
    }

    // 이벤트 구독을 위한 프로퍼티 중계
    public event Action<Item> LogItemAcquiredEvent
    {
        add { if (logItemController != null) logItemController.LogItemAcquiredEvent += value; }
        remove { if (logItemController != null) logItemController.LogItemAcquiredEvent -= value; }
    }

    public event Action<Item> CarrotItemAcquiredEvent
    {
        add { if (carrrotItemController != null) carrrotItemController.CarrotItemAcquiredEvent += value; }
        remove { if (carrrotItemController != null) carrrotItemController.CarrotItemAcquiredEvent -= value; }
    }
}
