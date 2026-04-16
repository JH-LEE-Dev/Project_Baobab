
public class GameplayUIManager : UIManager
{
    private IInventory inventory;
    private IInventory container;
    private IInDungeonObjProvider inDungeonObjProvider;
    private ILogCutter logCutter;
    private ISkillSystemProvider skillSystemProvider;
    private IShopNPC shopNPC;


    public void Initialize(InputManager _inputManager,IInventory _inventory,IInDungeonObjProvider _inDungeonObjProvider,IInventory _container,
    ILogCutter _logCutter,ISkillSystemProvider _skillSystemProvider,IShopNPC _shopNPC)
    {
        base.Initialize(_inputManager);

        inventory = _inventory;
        inDungeonObjProvider = _inDungeonObjProvider;
        container = _container;
        logCutter = _logCutter;
        skillSystemProvider = _skillSystemProvider;
        shopNPC = _shopNPC;
    }

    protected override void DataInjection(UIView view)
    {
        if(view is UIView_Popup invUI)
            invUI.DependencyInjection(inventory);

        if(view is UIView_Unit unitUI)
            unitUI.DependencyInjection(inDungeonObjProvider.trees);

        if(view is UIView_WorldPopup worldUI)
            worldUI.DependencyInjection(container,logCutter,shopNPC);
            
        if(view is UIView_Tent tentUI)
            tentUI.DependencyInjection(skillSystemProvider);
    }
}
