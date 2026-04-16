

public class UnitSystem
{
    //외부 의존성
    private SignalHub signalHub;
    private UnitSpawner unitSpawner;
    private UnitLogicManager unitLogicManager;
    private InventoryManager inventoryManager;

    //내부 의존성


    public void Initialize(SignalHub _signalHub, UnitSpawner _unitSpawner, UnitLogicManager _unitLogicManager, InventoryManager _inventoryManager)
    {
        signalHub = _signalHub;
        unitSpawner = _unitSpawner;
        unitLogicManager = _unitLogicManager;
        inventoryManager = _inventoryManager;

        SubscribeSignals();
        BindEvents();

        InventoryInitialized();
    }

    public void Release()
    {
        UnSubscribeSignals();
        ReleaseEvents();
        unitLogicManager.Release();
    }

    public void CreateCharacter()
    {
        unitSpawner.SpawnCharacter();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<DungeonReadySignal>(DungeonReady);
        signalHub.Subscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.Subscribe<TownStartedSignal>(TownStarted);
        signalHub.Subscribe<ItemAcquiredSignal>(ItemAcquired);
        signalHub.Subscribe<DeleteItemSignal>(ItemDeleted);
        signalHub.Subscribe<MoneyEarnedSignal>(MoneyEarned);
        signalHub.Subscribe<CarrotItemAcquiredSignal>(CarrotItemAcquired);
        signalHub.Subscribe<SleepSignal>(CharacterSleep);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<DungeonReadySignal>(DungeonReady);
        signalHub.UnSubscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
        signalHub.UnSubscribe<ItemAcquiredSignal>(ItemAcquired);
        signalHub.UnSubscribe<DeleteItemSignal>(ItemDeleted);
        signalHub.UnSubscribe<MoneyEarnedSignal>(MoneyEarned);
        signalHub.UnSubscribe<CarrotItemAcquiredSignal>(CarrotItemAcquired);
        signalHub.UnSubscribe<SleepSignal>(CharacterSleep);
    }

    private void BindEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
        unitSpawner.CharacterSpawnedEvent += CharacterSpawned;

        unitLogicManager.WeaponModeChangedEvent -= WeaponModeChanged;
        unitLogicManager.WeaponModeChangedEvent += WeaponModeChanged;

        inventoryManager.InventorySpecChangedEvent -= InventorySpecChanged;
        inventoryManager.InventorySpecChangedEvent += InventorySpecChanged;
    }

    private void ReleaseEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
        unitLogicManager.WeaponModeChangedEvent -= WeaponModeChanged;
        inventoryManager.InventorySpecChangedEvent -= InventorySpecChanged;
    }

    private void CharacterSpawned(Character _character)
    {
        signalHub.Publish(new CharacterSpawendSignal(_character));
        unitLogicManager.SetCharacter(_character);
    }

    private void DungeonReady(DungeonReadySignal dungeonReadySignal)
    {
        unitLogicManager.SetCharacterStaminaState(false, dungeonReadySignal.dungeonData.staminaDecAmount, dungeonReadySignal.dungeonData.staminaIncAmount);
    }

    private void DungeonStarted(DungeonStartSignal dungeonStartSignal)
    {
        unitLogicManager.SetCharacterPos(dungeonStartSignal.characterPos);
    }

    private void TownStarted(TownStartedSignal townStartedSignal)
    {
        unitLogicManager.SetCharacterStaminaState(true, 0, 1f);
        unitLogicManager.SetCharacterTransform(townStartedSignal.characterPos);
    }

    private void ItemAcquired(ItemAcquiredSignal itemAcquiredSignal)
    {
        inventoryManager.ItemAcquired(itemAcquiredSignal.item);
        signalHub.Publish(new InventoryUpdatedSignal());
    }

    private void ItemDeleted(DeleteItemSignal deleteItemSignal)
    {
        inventoryManager.ItemDeleted(deleteItemSignal.slot);
    }

    private void InventoryInitialized()
    {
        signalHub.Publish(new InventoryInitializedSignal(inventoryManager));
    }

    private void MoneyEarned(MoneyEarnedSignal moneyEarnedSignal)
    {
        inventoryManager.MoneyEarned(moneyEarnedSignal.money);
        signalHub.Publish(new CharacterEarnMoneySignal(MoneyType.Coin));
    }

    public void SetWhereIsCharacter(bool _bInDungeon)
    {
        unitLogicManager.SetWhereIsCharacter(_bInDungeon);
    }

    private void WeaponModeChanged(WeaponMode _currentMode)
    {
        signalHub.Publish(new WeaponModeChangedSignal(_currentMode));
    }

    private void CarrotItemAcquired(CarrotItemAcquiredSignal carrotItemAcquiredSignal)
    {
        inventoryManager.CarrotEarned();
        signalHub.Publish(new CharacterEarnMoneySignal(MoneyType.Carrot));
    }

    private void CharacterSleep(SleepSignal sleepSignal)
    {
        unitLogicManager.CharacterSleep();
    }

    private void InventorySpecChanged()
    {
        signalHub.Publish(new InventorySpecChangedSignal());
    }
}
