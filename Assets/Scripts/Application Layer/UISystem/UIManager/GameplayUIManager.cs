
public class GameplayUIManager : UIManager
{
    private IInventory inventory;

    public void Initialize(InputManager _inputManager,IInventory _inventory)
    {
        base.Initialize(_inputManager);

        inventory = _inventory;
    }

    protected override void DataInjection(UIView view)
    {
        if(view is UIView_Inventory invUI)
            invUI.DependencyInjection(inventory);
    }
}
