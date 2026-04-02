using System;
using UnityEngine;

public class LogProcessingManager : MonoBehaviour
{
    public event Action ContainerUpdatedEvent;
    public event Action<bool> InteractStateChangedEvent;

    [SerializeField] GameObject shopPrefab;
    [SerializeField] GameObject shopSpawnPoint;

    private GameObject shopObj;

    private IInventory inventory;
    private InputManager inputManager;
    private LogItemPoolingManager logItemPoolingManager;
    private LogInBelt logInBelt;

    public LogContainer logContainer { get; private set; }

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        shopObj = Instantiate(shopPrefab,shopSpawnPoint.transform.position,
        Quaternion.identity, this.transform);

        logContainer = shopObj.GetComponentInChildren<LogContainer>();
        logContainer.Initialize(inputManager);

        logInBelt = shopObj.GetComponentInChildren<LogInBelt>();
        logInBelt.Initialize();

        logItemPoolingManager = GetComponentInChildren<LogItemPoolingManager>();
        logItemPoolingManager.Initialize();

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
        logContainer.ContainerUpdatedEvent -= ContainerUpdated;
        logContainer.ContainerUpdatedEvent += ContainerUpdated;

        logContainer.InteractStateEvent -= InteractStateChanged;
        logContainer.InteractStateEvent += InteractStateChanged;

        logContainer.LogOutEvent -= LogOutFromContainer;
        logContainer.LogOutEvent += LogOutFromContainer;
    }

    private void ReleaseEvents()
    {
        logContainer.ContainerUpdatedEvent -= ContainerUpdated;
        logContainer.InteractStateEvent -= InteractStateChanged;
        logContainer.LogOutEvent -= LogOutFromContainer;
    }

    private void ContainerUpdated()
    {
        ContainerUpdatedEvent.Invoke();
    }

    private void InteractStateChanged(bool _boolean)
    {
        InteractStateChangedEvent.Invoke(_boolean);
    }

    private void LogOutFromContainer(LogItemData _itemData)
    {
        logItemPoolingManager.GetLogItem(_itemData);
    }
}
