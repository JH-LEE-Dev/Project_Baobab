
public class GameplayUIManager : UIManager
{
    private IInventory inventory;
    private IInDungeonObjProvider inDungeonObjProvider;

    public void Initialize(InputManager _inputManager,IInventory _inventory,IInDungeonObjProvider _inDungeonObjProvider)
    {
        base.Initialize(_inputManager);

        inventory = _inventory;
        inDungeonObjProvider = _inDungeonObjProvider;
    }

    protected override void DataInjection(UIView view)
    {
        if(view is UIView_Inventory invUI)
            invUI.DependencyInjection(inventory);

        if(view is UIView_Unit unitUI)
            unitUI.DependencyInjection(inDungeonObjProvider.trees);
    }
}
