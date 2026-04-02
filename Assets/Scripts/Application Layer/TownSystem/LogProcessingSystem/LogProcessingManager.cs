using System;
using UnityEngine;

public class LogProcessingManager : MonoBehaviour
{
    public event Action ContainerUpdatedEvent;
    public event Action<bool> InteractStateChangedEvent;


    [SerializeField] GameObject logContainerPrefab;

    private GameObject logContainerObj;

    private IInventory inventory;
    private InputManager inputManager;
    public LogContainer logContainer { get; private set; }

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
        logContainer.ContainerUpdatedEvent -= ContainerUpdated;
        logContainer.ContainerUpdatedEvent += ContainerUpdated;

        logContainer.InteractStateEvent -= InteractStateChanged;
        logContainer.InteractStateEvent += InteractStateChanged;
    }

    private void ReleaseEvents()
    {
        logContainer.ContainerUpdatedEvent -= ContainerUpdated;
        logContainer.InteractStateEvent -= InteractStateChanged;
    }

    private void ContainerUpdated()
    {
        ContainerUpdatedEvent.Invoke();
    }

    private void InteractStateChanged(bool _boolean)
    {
        InteractStateChangedEvent.Invoke(_boolean);
    }
}
