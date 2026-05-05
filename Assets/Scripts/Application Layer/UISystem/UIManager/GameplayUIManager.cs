
public class GameplayUIManager : UIManager
{
    private IInventory inventory;
    private IInventory container;
    private IInDungeonObjProvider inDungeonObjProvider;
    private ILogCutter logCutter;
    private ISkillSystemProvider skillSystemProvider;
    private IShopNPC shopNPC;
    private IMoneyData moneyData;
    private LocalizationManager localizationManager;
    private IMapDataProvider mapDataProvider;
    private IWeatherProvider weatherProvider;
    private ITimeDataProvider timeDataProvider;


    public void Initialize(InputManager _inputManager, IInventory _inventory, IInDungeonObjProvider _inDungeonObjProvider, IInventory _container,
    ILogCutter _logCutter, ISkillSystemProvider _skillSystemProvider, IShopNPC _shopNPC, IMoneyData _moneyData, LocalizationManager _localizeManager,
    IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider)
    {
        base.Initialize(_inputManager, _localizeManager);

        weatherProvider = _weatherProvider;
        timeDataProvider = _timeDataProvider;
        mapDataProvider = _mapDataProvider;
        localizationManager = _localizeManager;
        inventory = _inventory;
        inDungeonObjProvider = _inDungeonObjProvider;
        container = _container;
        logCutter = _logCutter;
        skillSystemProvider = _skillSystemProvider;
        shopNPC = _shopNPC;
        moneyData = _moneyData;
    }

    protected override void DataInjection(UIView view)
    {
        if (view is UIView_Popup invUI)
            invUI.DependencyInjection(inventory, moneyData);

        if (view is UIView_Unit unitUI)
            unitUI.DependencyInjection(inDungeonObjProvider.trees);

        if (view is UIView_WorldPopup worldUI)
            worldUI.DependencyInjection(container, logCutter, shopNPC);

        if (view is UIView_Tent tentUI)
            tentUI.DependencyInjection(skillSystemProvider, moneyData);

        if (view is UIView_HUD hudUI)
            hudUI.DependencyInjection();

        if (view is UIView_MenuPopup menuPopupUI)
            menuPopupUI.DependencyInjection(mapDataProvider, weatherProvider, timeDataProvider);
    }
}
