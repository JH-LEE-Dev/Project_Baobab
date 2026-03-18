
public class GameplayUICoordinator
{
    private UIView_Inventory inventoryUI;
    private InputManager inputManager;

    private SignalHub signalHub;
    private UIView_HUD hudUI;

    private bool bInventoryOpened = false;

    public void Initialize(SignalHub _signalHub,InputManager _inputManager, UIView_Inventory _inventoryUI, UIView_HUD _hudUI)
    {
        inputManager = _inputManager;
        inventoryUI = _inventoryUI;
        hudUI = _hudUI;
        signalHub = _signalHub;

        SubscribeSignals();
        BindEvents();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryUpdatedSignal>(InventoryUpdated);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryUpdatedSignal>(InventoryUpdated);
    }

    private void BindEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        inputManager.inputReader.InventoryKeyEvent += OnInventoryKeyPressed;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
    }

    public void Release()
    {
        UnSubscribeSignals();
        ReleaseEvents();
    }

    private void OnInventoryKeyPressed()
    {
        if (bInventoryOpened == false)
        {
            bInventoryOpened = true;
            inventoryUI.Show();
        }
        else
        {
            bInventoryOpened = false;
            inventoryUI.Hide();
        }
    }

    private void InventoryUpdated(InventoryUpdatedSignal inventoryUpdatedSignal)
    {
        inventoryUI.InventoryShowEvent();
    }
}
