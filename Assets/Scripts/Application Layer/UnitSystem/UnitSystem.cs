

public class UnitSystem
{
    //외부 의존성
    private SignalHub signalHub;
    private UnitSpawner unitSpawner;
    private UnitLogicManager unitLogicManager;
    private InventoryManager inventoryManager;
    private OffroadContainer offroadContainer;
    private InDungeonResultManager inDungeonResultManager;
    private IEnvironmentProvider environmentProvider;
    
    //내부 의존성

    public void Initialize(SignalHub _signalHub, UnitSpawner _unitSpawner, UnitLogicManager _unitLogicManager, InventoryManager _inventoryManager,
    OffroadContainer _offroadContainer, InDungeonResultManager _inDungeonResultManager, IEnvironmentProvider _environmentProvider)
    {
        signalHub = _signalHub;
        unitSpawner = _unitSpawner;
        unitLogicManager = _unitLogicManager;
        inventoryManager = _inventoryManager;
        offroadContainer = _offroadContainer;
        inDungeonResultManager = _inDungeonResultManager;
        environmentProvider = _environmentProvider;

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
        offroadContainer.SetCharacterTransform(unitSpawner.character.centerTransform);
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
        signalHub.Subscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.Subscribe<StartDecreaseStaminaSignal>(StartDecreaseStamina);
        signalHub.Subscribe<DropAllItemSignal>(DropAllItem);
        signalHub.Subscribe<LostAndFoundBoxAcquiredSignal>(LostAndFoundBoxAcquired);
        signalHub.Subscribe<RetryButtonClickedSignal>(RetryGame);
        signalHub.Subscribe<ActivateCharacterSignal>(ActivateCharacter);
        signalHub.Subscribe<TreeIsDeadSignal>(TreeIsDead);
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
        signalHub.UnSubscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.UnSubscribe<StartDecreaseStaminaSignal>(StartDecreaseStamina);
        signalHub.UnSubscribe<DropAllItemSignal>(DropAllItem);
        signalHub.UnSubscribe<LostAndFoundBoxAcquiredSignal>(LostAndFoundBoxAcquired);
        signalHub.UnSubscribe<RetryButtonClickedSignal>(RetryGame);
        signalHub.UnSubscribe<ActivateCharacterSignal>(ActivateCharacter);
        signalHub.UnSubscribe<TreeIsDeadSignal>(TreeIsDead);
    }

    private void BindEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
        unitSpawner.CharacterSpawnedEvent += CharacterSpawned;

        unitLogicManager.WeaponModeChangedEvent -= WeaponModeChanged;
        unitLogicManager.WeaponModeChangedEvent += WeaponModeChanged;

        inventoryManager.InventorySpecChangedEvent -= InventorySpecChanged;
        inventoryManager.InventorySpecChangedEvent += InventorySpecChanged;

        unitLogicManager.CharacterStaminaIsEmptyEvent -= CharacterStaminaIsEmpty;
        unitLogicManager.CharacterStaminaIsEmptyEvent += CharacterStaminaIsEmpty;

        inventoryManager.SpendMoneyEvent -= SpendMoney;
        inventoryManager.SpendMoneyEvent += SpendMoney;

        offroadContainer.InteractStateEvent -= OffroadContainerInteractStateChanged;
        offroadContainer.InteractStateEvent += OffroadContainerInteractStateChanged;

        inventoryManager.LoosAllInventoryItemEvent -= LoosAllInventoryItem;
        inventoryManager.LoosAllInventoryItemEvent += LoosAllInventoryItem;

        offroadContainer.ContainerUpdatedEvent -= OffroadContainerUpdated;
        offroadContainer.ContainerUpdatedEvent += OffroadContainerUpdated;

        inventoryManager.InventoryIsFullEvent -= InventoryIsFull;
        inventoryManager.InventoryIsFullEvent += InventoryIsFull;

        inventoryManager.ItemAddedEvent -= ItemAdded;
        inventoryManager.ItemAddedEvent += ItemAdded;

        inventoryManager.ItemRemovedEvent -= ItemRemoved;
        inventoryManager.ItemRemovedEvent += ItemRemoved;

        inventoryManager.ItemCantAcquiedEvent -= ItemCantAcquied;
        inventoryManager.ItemCantAcquiedEvent += ItemCantAcquied;

        unitLogicManager.GameEndEvent -= GameEnd;
        unitLogicManager.GameEndEvent += GameEnd;

        offroadContainer.ItemTransferToContainerEvent -= InventoryItemTransferToOffroadContainer;
        offroadContainer.ItemTransferToContainerEvent += InventoryItemTransferToOffroadContainer;
    }

    private void ReleaseEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
        unitLogicManager.WeaponModeChangedEvent -= WeaponModeChanged;
        inventoryManager.InventorySpecChangedEvent -= InventorySpecChanged;
        unitLogicManager.CharacterStaminaIsEmptyEvent -= CharacterStaminaIsEmpty;
        inventoryManager.SpendMoneyEvent -= SpendMoney;
        offroadContainer.InteractStateEvent -= OffroadContainerInteractStateChanged;
        inventoryManager.LoosAllInventoryItemEvent -= LoosAllInventoryItem;
        offroadContainer.ContainerUpdatedEvent -= OffroadContainerUpdated;
        inventoryManager.InventoryIsFullEvent -= InventoryIsFull;
        inventoryManager.ItemAddedEvent -= ItemAdded;
        inventoryManager.ItemRemovedEvent -= ItemRemoved;
        inventoryManager.ItemCantAcquiedEvent -= ItemCantAcquied;
        unitLogicManager.GameEndEvent -= GameEnd;
        offroadContainer.ItemTransferToContainerEvent -= InventoryItemTransferToOffroadContainer;
    }

    private void CharacterSpawned(Character _character)
    {
        signalHub.Publish(new CharacterSpawnedSignal(_character));
        unitLogicManager.SetCharacter(_character);
        inventoryManager.SetMoney(_character.statComponent.money);
    }

    private void DungeonReady(DungeonReadySignal dungeonReadySignal)
    {
        unitLogicManager.CharacterIsInDungeon(dungeonReadySignal.forestType);
    }

    private void DungeonStarted(DungeonStartSignal dungeonStartSignal)
    {
        inventoryManager.ReleaseAllDroppedItem();
        unitLogicManager.SetCharacterPos(dungeonStartSignal.characterPos);
        offroadContainer.SetInTown(false);

        // 마을에서 인벤토리에 원목을 든 채로 던전에 입장하면, 조작 가능해지기 전에 오프로드
        // 컨테이너로 미리 옮겨 인벤토리 슬롯을 비워둔다.
        inventoryManager.TransferAllLogItemsToOffroadContainer(offroadContainer);
    }

    private void TownStarted(TownStartedSignal townStartedSignal)
    {
        inventoryManager.ReleaseAllDroppedItem();
        unitLogicManager.SetCharacterStaminaState(true, 0, 1f);
        unitLogicManager.SetCharacterTransform(townStartedSignal.characterPos);
        offroadContainer.SetInTown(true);
    }

    private void ItemAcquired(ItemAcquiredSignal itemAcquiredSignal)
    {
        inventoryManager.ItemAcquired(itemAcquiredSignal.item);
        Sound.PlayUI(SoundID.GetItem);
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
        inventoryManager.CarrotEarned(carrotItemAcquiredSignal.amount);
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

    private void OffraodContainerSpecChanged()
    {
        signalHub.Publish(new OffraodContainerSpecChangedSignal());
    }

    private void CharacterStaminaIsEmpty()
    {
        // "분실물 보관함" 효과: 유실 처리(DropAllItem) 전에 먼저 일부를 오프로드 컨테이너로 구제한다.
        inventoryManager.RescueItemsToOffroadContainer(offroadContainer);
        inDungeonResultManager.IncreaseLostLogItemCnt(inventoryManager.DropAllItem(unitSpawner.character.centerTransform));
        signalHub.Publish(new PopupUIDownSignal());
    }

    private void LostAndFoundBoxAcquired(LostAndFoundBoxAcquiredSignal _signal)
    {
        inventoryManager.SetLostAndFoundBoxEffect(true);
    }

    private void SpendMoney()
    {
        signalHub.Publish(new SpendMoneySignal());
    }

    private void SkillDispatched(SkillDispatchedSignal skillDispatchedSignal)
    {
        unitLogicManager.RefreshCharacter();
    }

    private void OffroadContainerInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new OffroadContainerInteractStateChangedSignal(_boolean));
    }

    private void LoosAllInventoryItem()
    {
        signalHub.Publish(new LoosAllInventoryItemSignal());
    }

    private void OffroadContainerUpdated()
    {
        signalHub.Publish(new OffroadContainerUpdatedSignal());
    }

    private void InventoryIsFull()
    {
        signalHub.Publish(new InventoryIsFullSignal());
    }

    private void ItemAdded()
    {
        signalHub.Publish(new ItemAddedToInventorySignal());
    }

    private void ItemRemoved()
    {
        signalHub.Publish(new ItemRemovedFromInventorySignal());
    }

    private void ItemCantAcquied()
    {
        signalHub.Publish(new ItemCantAcquiedSignal());
    }

    private void StartDecreaseStamina(StartDecreaseStaminaSignal _startDecreaseStaminaSignal)
    {
        unitLogicManager.StartDecreaseStamina();
    }

    private void GameEnd()
    {
        signalHub.Publish(new GameEndSignal());
    }

    private void DropAllItem(DropAllItemSignal _signal)
    {
        inDungeonResultManager.IncreaseLostLogItemCnt(inventoryManager.DropAllItem(unitSpawner.character.centerTransform));
        signalHub.Publish(new PopupUIDownSignal());
    }

    private void RetryGame(RetryButtonClickedSignal _retryButtonClickedSignal)
    {
        unitLogicManager.ResetCharacterStatus();
    }

    private void InventoryItemTransferToOffroadContainer()
    {
        signalHub.Publish(new InventoryItemTransferToOffroadContainerSignal());
    }

    private void ActivateCharacter(ActivateCharacterSignal _activateCharacterSignal)
    {
        unitLogicManager.ActivateCharacter();
    }

    private void TreeIsDead(TreeIsDeadSignal _signal)
    {
        if (_signal.isPlayerKilled)
        {
            unitLogicManager.SourceOfStaminaRecover();
        }
    }
}
