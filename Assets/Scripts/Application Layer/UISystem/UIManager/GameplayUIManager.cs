
public class GameplayUIManager : UIManager
{
    private IInventory inventory;
    private IInventory container;
    private IInDungeonObjProvider inDungeonObjProvider;
    private ILogCutter logCutter;
    private ISkillSystemProvider skillSystemProvider;


    public void Initialize(InputManager _inputManager,IInventory _inventory,IInDungeonObjProvider _inDungeonObjProvider,IInventory _container,
    ILogCutter _logCutter,ISkillSystemProvider _skillSystemProvider)
    {
        base.Initialize(_inputManager);

        inventory = _inventory;
        inDungeonObjProvider = _inDungeonObjProvider;
        container = _container;
        logCutter = _logCutter;
        skillSystemProvider = _skillSystemProvider;
    }

    protected override void DataInjection(UIView view)
    {
        if(view is UIView_Popup invUI)
            invUI.DependencyInjection(inventory);

        if(view is UIView_Unit unitUI)
            unitUI.DependencyInjection(inDungeonObjProvider.trees);

        if(view is UIView_WorldPopup worldUI)
            worldUI.DependencyInjection(container,logCutter);
            
        if(view is UIView_Tent tentUI)
            tentUI.DependencyInjection(skillSystemProvider);
    }
}
