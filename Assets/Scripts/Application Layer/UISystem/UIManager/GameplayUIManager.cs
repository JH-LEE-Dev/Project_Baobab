using PresentationLayer.UISystem.View;

public class GameplayUIManager : UIManager
{
    private IInventory inventory;
    private IInventory container;
    private IInDungeonObjProvider inDungeonObjProvider;

    public void Initialize(InputManager _inputManager,IInventory _inventory,IInDungeonObjProvider _inDungeonObjProvider,IInventory _container)
    {
        base.Initialize(_inputManager);

        inventory = _inventory;
        inDungeonObjProvider = _inDungeonObjProvider;
        container = _container;
    }

    protected override void DataInjection(UIView view)
    {
        if(view is UIView_Popup invUI)
            invUI.DependencyInjection(inventory);

        if(view is UIView_Unit unitUI)
            unitUI.DependencyInjection(inDungeonObjProvider.trees);

        if(view is UIView_WorldPopup worldUI)
            worldUI.DependencyInjection(container);
    }
}
