
public class GameplayUICoordinator
{
    private UIView_Inventory inventoryUI;
    private InputManager inputManager;

    private UIView_HUD hudUI;

    private bool bInventoryOpened = false;

    public void Initialize(InputManager _inputManager, UIView_Inventory _inventoryUI, UIView_HUD _hudUI)
    {
        inputManager = _inputManager;
        inventoryUI = _inventoryUI;
        hudUI = _hudUI;


        BindEvents();
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
}
