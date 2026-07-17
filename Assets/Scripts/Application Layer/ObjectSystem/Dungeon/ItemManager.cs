using System;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    // 내부 의존성
    public LogItemController logItemController { get; private set; }
    public CarrotItemController carrrotItemController { get; private set; }
    private IInventoryChecker inventoryChecker;
    private ICharacter character;

    public void Initialize(IInventoryChecker _inventoryChecker, ICharacter _character)
    {
        inventoryChecker = _inventoryChecker;
        character = _character;

        logItemController = GetComponentInChildren<LogItemController>();
        carrrotItemController = GetComponentInChildren<CarrotItemController>();

        if (logItemController != null)
        {
            logItemController.Initialize(inventoryChecker, character);
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

    public void SetupCulling()
    {
        if (logItemController != null)
        {
            logItemController.SetupCullingGroup();
        }

        if (carrrotItemController != null)
        {
            carrrotItemController.SetupCullingGroup();
        }
    }

    // 외부에서 접근하기 위한 래퍼 메서드 (필요한 경우)
    public void SpawnLogItem(TreeObj _treeObj, float _multiplier)
    {
        logItemController?.SpawnLogItem(_treeObj, _multiplier);
    }

    public void ReturnLogToPool(LogItem _item)
    {
        logItemController?.ReturnToPool(_item);
    }

    public void SpawnCarrotItem(Vector3 _position, AnimalType _animalType)
    {
        carrrotItemController?.SpawnCarrotItem(_position, _animalType);
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

    public event Action<CarrotItem> CarrotItemAcquiredEvent
    {
        add { if (carrrotItemController != null) carrrotItemController.CarrotItemAcquiredEvent += value; }
        remove { if (carrrotItemController != null) carrrotItemController.CarrotItemAcquiredEvent -= value; }
    }

    public void ReleaseAllItems()
    {
        //carrrotItemController.ClearAll();
        logItemController.ClearAll();
    }

    public void CancelActiveSucking()
    {
        logItemController?.CancelActiveSucking();
    }
}
