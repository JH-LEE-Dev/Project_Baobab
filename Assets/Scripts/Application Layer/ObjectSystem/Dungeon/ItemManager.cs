using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    // 내부 의존성
    private LogItemController logItemController;
    private CarrrotItemController carrrotItemController;

    public void Initialize()
    {
        logItemController = GetComponentInChildren<LogItemController>();
        carrrotItemController = GetComponentInChildren<CarrrotItemController>();

        if (logItemController != null)
        {
            logItemController.Initialize();
        }

        // CarrotItemController 초기화 로직도 필요하다면 여기에 추가
        
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

    // 이벤트 구독을 위한 프로퍼티 중계
    public event Action<Item> LogItemAcquiredEvent
    {
        add { if (logItemController != null) logItemController.LogItemAcquiredEvent += value; }
        remove { if (logItemController != null) logItemController.LogItemAcquiredEvent -= value; }
    }
}
