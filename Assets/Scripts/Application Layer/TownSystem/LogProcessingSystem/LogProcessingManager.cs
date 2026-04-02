using System;
using UnityEngine;

public class LogProcessingManager : MonoBehaviour
{
    public event Action InventoryUpdatedEvent;

    [SerializeField] GameObject logContainerPrefab;

    private GameObject logContainerObj;

    private IInventory inventory;
    private InputManager inputManager;
    private LogContainer logContainer;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        logContainerObj = Instantiate(logContainerPrefab, this.transform);

        logContainer = logContainerObj.GetComponentInChildren<LogContainer>();
        logContainer.Initialize(inputManager);

        BindEvents();
    }

    public void Release()
    {
        logContainer.Release();
        ReleaseEvents();
    }

    public void DI_Inventory(IInventory _inventory)
    {
        inventory = _inventory;
        logContainer.DI_Inventory(inventory);
    }

    private void BindEvents()
    {
        logContainer.ContainerUpdatedEvent -= InventoryUpdated;
        logContainer.ContainerUpdatedEvent += InventoryUpdated;
    }

    private void ReleaseEvents()
    {
        logContainer.ContainerUpdatedEvent -= InventoryUpdated;
    }

    private void InventoryUpdated()
    {
        InventoryUpdatedEvent.Invoke();   
    }
}
