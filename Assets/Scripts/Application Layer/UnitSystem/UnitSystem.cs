

public class UnitSystem
{
    //외부 의존성
    private SignalHub signalHub;
    private UnitSpawner unitSpawner;
    private UnitLogicManager unitLogicManager;
    private InventoryManager inventoryManager;

    //내부 의존성


    public void Initialize(SignalHub _signalHub, UnitSpawner _unitSpawner, UnitLogicManager _unitLogicManager,InventoryManager _inventoryManager)
    {
        signalHub = _signalHub;
        unitSpawner = _unitSpawner;
        unitLogicManager = _unitLogicManager;
        inventoryManager = _inventoryManager;

        SubscribeSignals();
        BindEvents();
    }

    public void Release()
    {
        UnSubscribeSignals();
        ReleaseEvents();
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
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<DungeonReadySignal>(DungeonReady);
        signalHub.UnSubscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
    }

    private void BindEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
        unitSpawner.CharacterSpawnedEvent += CharacterSpawned;
    }

    private void ReleaseEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
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
        unitLogicManager.SetCharacterStaminaState(true, 0, 0.05f);
        unitLogicManager.SetCharacterTransform(townStartedSignal.characterPos);
    }
}
