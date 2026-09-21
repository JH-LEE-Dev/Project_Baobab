using UnityEngine;

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
        offroadContainer.SetCharacter(unitSpawner.character);
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
        signalHub.Subscribe<GemOreAcquiredSignal>(GemOreAcquired);
        signalHub.Subscribe<SleepSignal>(CharacterSleep);
        signalHub.Subscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.Subscribe<StartDecreaseStaminaSignal>(StartDecreaseStamina);
        signalHub.Subscribe<DropAllItemSignal>(DropAllItem);
        signalHub.Subscribe<LostAndFoundBoxAcquiredSignal>(LostAndFoundBoxAcquired);
        signalHub.Subscribe<RetryButtonClickedSignal>(RetryGame);
        signalHub.Subscribe<ActivateCharacterSignal>(ActivateCharacter);
        signalHub.Subscribe<EnableCharacterAimSignal>(EnableCharacterAim);
        signalHub.Subscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.Subscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.Subscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.Subscribe<LogSwapRequestedSignal>(LogSwapRequested);
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
        signalHub.UnSubscribe<GemOreAcquiredSignal>(GemOreAcquired);
        signalHub.UnSubscribe<SleepSignal>(CharacterSleep);
        signalHub.UnSubscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.UnSubscribe<StartDecreaseStaminaSignal>(StartDecreaseStamina);
        signalHub.UnSubscribe<DropAllItemSignal>(DropAllItem);
        signalHub.UnSubscribe<LostAndFoundBoxAcquiredSignal>(LostAndFoundBoxAcquired);
        signalHub.UnSubscribe<RetryButtonClickedSignal>(RetryGame);
        signalHub.UnSubscribe<ActivateCharacterSignal>(ActivateCharacter);
        signalHub.UnSubscribe<EnableCharacterAimSignal>(EnableCharacterAim);
        signalHub.UnSubscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.UnSubscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.UnSubscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.UnSubscribe<LogSwapRequestedSignal>(LogSwapRequested);
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

        unitLogicManager.TreeDetectedEvent -= TreeDetected;
        unitLogicManager.TreeDetectedEvent += TreeDetected;

        unitLogicManager.TreeDetectionClearedEvent -= TreeDetectionCleared;
        unitLogicManager.TreeDetectionClearedEvent += TreeDetectionCleared;

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

        offroadContainer.ItemStoredFromCharacterEvent -= ItemStoredInOffroadContainer;
        offroadContainer.ItemStoredFromCharacterEvent += ItemStoredInOffroadContainer;

        inventoryManager.SwapStateChangedEvent -= LogSwapStateChanged;
        inventoryManager.SwapStateChangedEvent += LogSwapStateChanged;

        offroadContainer.SwapStateChangedEvent -= LogSwapStateChanged;
        offroadContainer.SwapStateChangedEvent += LogSwapStateChanged;
    }

    private void ReleaseEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
        unitLogicManager.WeaponModeChangedEvent -= WeaponModeChanged;
        inventoryManager.InventorySpecChangedEvent -= InventorySpecChanged;
        unitLogicManager.CharacterStaminaIsEmptyEvent -= CharacterStaminaIsEmpty;
        unitLogicManager.TreeDetectedEvent -= TreeDetected;
        unitLogicManager.TreeDetectionClearedEvent -= TreeDetectionCleared;
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
        offroadContainer.ItemStoredFromCharacterEvent -= ItemStoredInOffroadContainer;
        inventoryManager.SwapStateChangedEvent -= LogSwapStateChanged;
        offroadContainer.SwapStateChangedEvent -= LogSwapStateChanged;
    }

    private void CharacterSpawned(Character _character)
    {
        signalHub.Publish(new CharacterSpawnedSignal(_character));
        unitLogicManager.SetCharacter(_character);
        ApplyDebugStartMoney();
    }

    /// <summary>
    /// 캐릭터 프리팹의 StatComponent에 지정해 둔 디버그 소지금을 인벤토리에 반영한다.
    /// 새 게임은 캐릭터 스폰 직후 한 번, 이어하기는 세이브 로드가 소지금을 덮어쓴 뒤 GameInstaller가 한 번 더 호출한다.
    /// (호출 순서: UnitSystem.CreateCharacter -> SaveManager.LoadGameData -> GameInstaller.LoadGame)
    /// 에디터 전용 치트라 빌드에서는 아무 일도 하지 않는다.
    /// </summary>
    public void ApplyDebugStartMoney()
    {
#if UNITY_EDITOR
        Character character = unitSpawner.character;
        if (character == null || character.statComponent == null) return;
        if (character.statComponent.bOverrideStartMoney == false) return;

        inventoryManager.SetMoney(character.statComponent.startMoney);
        Debug.Log($"[UnitSystem] 디버그 소지금 적용: {character.statComponent.startMoney}");
#endif
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
        unitSpawner.character.PlayItemAcquireBounce();
        unitSpawner.character.PlayItemAcquireFlash();
    }

    private void ItemDeleted(DeleteItemSignal deleteItemSignal)
    {
        inventoryManager.ItemDeleted(deleteItemSignal.slot);
    }

    // ── 교체 시스템 ─────────────────────────────────────────────────────────────────
    //
    // 인벤토리와 운반 상자가 각자 "지금 내 쪽에서 버릴 슬롯이 있는가"를 판단하고, 여기서는 둘 중
    // 어느 쪽을 쓸지만 정한다. 유저에게 보이는 교체 키는 하나뿐이기 때문이다.

    // 마지막으로 UI에 알린 내용. 같은 내용을 거듭 발행하지 않기 위한 것이다.
    private LogSwapSlotInfo lastNotifiedInventorySwapInfo = LogSwapSlotInfo.None;
    private LogSwapSlotInfo lastNotifiedContainerSwapInfo = LogSwapSlotInfo.None;

    /// <summary>
    /// 지금 교체 키를 누르면 어느 쪽 슬롯이 버려지는지.
    ///
    /// 운반 상자를 먼저 본다 - 상자 앞에 서서 넣으려다 막힌 상황이 더 분명한 의도이고, 그때 가방을
    /// 비워봐야 상자에 넣지 못하는 것은 그대로이기 때문이다.
    /// </summary>
    private ELogSwapTarget GetActiveLogSwapTarget(in LogSwapSlotInfo _inventoryInfo, in LogSwapSlotInfo _containerInfo)
    {
        if (_containerInfo.bHasSlot) return ELogSwapTarget.OffroadContainer;
        if (_inventoryInfo.bHasSlot) return ELogSwapTarget.Inventory;

        return ELogSwapTarget.None;
    }

    /// <summary>
    /// 양쪽에서 "지금 버려질 슬롯"을 모아 UI로 흘려보낸다. 인벤토리/운반 상자 중 한쪽이라도 달라지면
    /// 두 쪽 모두를 실어 한 번에 발행하므로, UI는 이 신호 하나로 표시를 통째로 다시 맞출 수 있다.
    /// </summary>
    private void LogSwapStateChanged()
    {
        LogSwapSlotInfo inventoryInfo = inventoryManager != null ? inventoryManager.GetLogSwapInfo() : LogSwapSlotInfo.None;
        LogSwapSlotInfo containerInfo = offroadContainer != null ? offroadContainer.GetLogSwapInfo() : LogSwapSlotInfo.None;

        if (LogSwapSlotInfo.IsSame(in inventoryInfo, in lastNotifiedInventorySwapInfo) &&
            LogSwapSlotInfo.IsSame(in containerInfo, in lastNotifiedContainerSwapInfo))
        {
            return;
        }

        lastNotifiedInventorySwapInfo = inventoryInfo;
        lastNotifiedContainerSwapInfo = containerInfo;

        signalHub.Publish(new LogSwapAvailabilityChangedSignal(in inventoryInfo, in containerInfo,
            GetActiveLogSwapTarget(in inventoryInfo, in containerInfo)));
    }

    /// <summary>
    /// 교체 키가 눌렸다(인벤토리가 열려 있는 경우에만 여기까지 온다 - GameplayUICoordinator가 거른다).
    /// 버릴 슬롯이 없으면 아무 일도 일어나지 않는다.
    /// </summary>
    private void LogSwapRequested(LogSwapRequestedSignal _logSwapRequestedSignal)
    {
        LogSwapSlotInfo inventoryInfo = inventoryManager.GetLogSwapInfo();
        LogSwapSlotInfo containerInfo = offroadContainer.GetLogSwapInfo();

        LogSwapSlotInfo executed = LogSwapSlotInfo.None;

        switch (GetActiveLogSwapTarget(in inventoryInfo, in containerInfo))
        {
            case ELogSwapTarget.OffroadContainer:
                executed = offroadContainer.ExecuteLogSwap();
                break;

            case ELogSwapTarget.Inventory:
                Character character = unitSpawner.character;
                executed = inventoryManager.ExecuteLogSwap(character != null ? character.centerTransform : null);
                break;
        }

        if (executed.bHasSlot)
        {
            signalHub.Publish(new LogSwapExecutedSignal(in executed));
        }

        // 버린 직후의 상태를 UI에 바로 반영한다(자리가 생겨 교체가 더 이상 필요 없어졌을 수 있다).
        LogSwapStateChanged();
    }

    private void InventoryInitialized()
    {
        signalHub.Publish(new InventoryInitializedSignal(inventoryManager));
    }

    private void MoneyEarned(MoneyEarnedSignal moneyEarnedSignal)
    {
        inventoryManager.MoneyEarned(moneyEarnedSignal.money);
        // 반짝임/카메라 셰이크는 이제 ShopNPC가 코인이 도착할 때마다(짤랑짤랑) 직접 재생한다.
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

    private void TreeDetected()
    {
        signalHub.Publish(new TreeDetectedSignal());
    }

    private void TreeDetectionCleared()
    {
        signalHub.Publish(new TreeDetectionClearedSignal());
    }

    private void CarrotItemAcquired(CarrotItemAcquiredSignal carrotItemAcquiredSignal)
    {
        inventoryManager.CarrotEarned(carrotItemAcquiredSignal.amount);
        signalHub.Publish(new CharacterEarnMoneySignal(MoneyType.Carrot));
    }

    /// <summary>
    /// 보석 원석을 주웠을 때. 인벤토리 슬롯을 쓰지 않고 해당 재화만 올린 뒤,
    /// HUD가 갱신할 수 있도록 CharacterEarnMoneySignal로 어떤 재화가 늘었는지 알린다.
    ///
    /// 주머니 한도 때문에 알갱이가 담고 있던 양을 다 못 받을 수 있으므로, 결과창 집계도
    /// 시그널이 실어온 양이 아니라 <b>실제로 받은 양</b>으로 한다.
    ///
    /// <b>[죽은 코드]</b> SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라 원석 아이템이 아예
    /// 생성되지 않는다. GemOreAcquiredSignal 이 발행될 일이 없어 이 함수는 호출되지 않는다.
    /// 버그 검토 대상이 아니다.
    /// </summary>
    private void GemOreAcquired(GemOreAcquiredSignal gemOreAcquiredSignal)
    {
        long accepted = inventoryManager.GemOreEarned(gemOreAcquiredSignal.gemOreType, gemOreAcquiredSignal.amount);

        // 주머니가 가득 차 한 톨도 못 받았으면 바뀐 것이 없다.
        if (accepted <= 0) return;

        inDungeonResultManager.AddAcquiredGemOre(gemOreAcquiredSignal.gemOreType, accepted);

        signalHub.Publish(new CharacterEarnMoneySignal(
            InventoryManager.GemOreTypeToMoneyType(gemOreAcquiredSignal.gemOreType)));
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
    }

    private void RetryGame(RetryButtonClickedSignal _retryButtonClickedSignal)
    {
        unitLogicManager.ResetCharacterStatus();
    }

    private void InventoryItemTransferToOffroadContainer()
    {
        signalHub.Publish(new InventoryItemTransferToOffroadContainerSignal());
    }

    private void ItemStoredInOffroadContainer(ItemType _itemType, int _count)
    {
        signalHub.Publish(new ItemStoredInOffroadContainerSignal(_itemType, _count));
    }

    private void ActivateCharacter(ActivateCharacterSignal _activateCharacterSignal)
    {
        unitLogicManager.ActivateCharacter();
    }

    private void EnableCharacterAim(EnableCharacterAimSignal _enableCharacterAimSignal)
    {
        unitLogicManager.EnableCharacterAim();
    }

    private void TreeIsDead(TreeIsDeadSignal _signal)
    {
        if (_signal.isPlayerKilled)
        {
            unitLogicManager.SourceOfStaminaRecover();
        }
    }

    // MainMenu → Dungeon 튜토리얼: 벌목/이관 퀘스트를 진행하는 동안(차량이 아직 상호작용 불가라
    // 탈출 수단이 없는 상태)에는 피로도가 21% 아래로는 떨어지지 않게 막아둔다.
    private void TutorialStepStarted(TutorialStepStartedSignal _signal)
    {
        if (_signal.step == TutorialStep.CutTree)
        {
            unitLogicManager.SetMinStaminaPercent(21f);
        }
    }

    // 원목 이관 퀘스트가 끝나 차량 탑승이 가능해지면, 바닥값을 19%로 낮춰 다시 내려갈 수 있게 한다.
    private void TutorialStepCompleted(TutorialStepCompletedSignal _signal)
    {
        if (_signal.step == TutorialStep.FillOffroadContainer)
        {
            unitLogicManager.SetMinStaminaPercent(19f);
        }
    }
}
