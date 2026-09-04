using System;
using UnityEngine;
using PresentationLayer.UISystem.CustomNumber;

public class GameplayUICoordinator
{
    public event Action SaveGameEvent;
    public event Action GoToMainMenuEvent;
    private UIView_Popup popUpUI;
    private InputManager inputManager;
    private UIView_Unit unitUI;

    private SignalHub signalHub;
    private UIView_HUD hudUI;
    private UIView_WorldPopup worldPopupUI;
    private UIView_MenuPopup menuPopupUI;
    private UIView_Tent tentUI;
    private UIView_ESC escUI;
    private UIView_Result resultUI;
    private UIView_SkyProduction skyProduction;
    private UIView_Warning warningUI;
    private UIView_OverUIPopup overUIPopupUI;
    private UIView_ScreenModal screenModalUI;

    private UIDepthController uiDepthController;

    private bool bInventoryOpened = false;
    private bool bIsTutorialQuestHiding = false;
    private bool bPendingGameEnd = false;

    // ESC 일시정지를 열기 직전의 이동 잠금 상태. 닫을 때 false를 박는 대신 이 값으로 되돌린다.
    //
    // PauseMove는 소유자별 잠금이 아니라 단일 bool이라(InputReader.IsMovePaused 참고), 무조건
    // false로 풀면 다른 시스템이 아직 막아야 하는 구간까지 함께 열어버린다. 연출 도중에 ESC를
    // 여닫으면 캐릭터가 그 연출 위를 걸어다니게 되는 사고가 이 경로로 났다.
    //
    // 잠금을 거는 쪽마다 ESC를 함께 막는 것이 정석이지만(그렇게 고친 구간도 있다), 앞으로 추가될
    // 연출까지 매번 기억해야 하므로 겹쳐 잠그는 이쪽에서도 원래 값을 보존한다.
    private bool bMovePausedBeforeEsc = false;

    // PopupUIDown ~ HUD가 완전히 다 올라오는 시점(HUDGoUpCompleted)까지 true.
    // 이 구간에는 HUD가 내려가 있거나 애니메이션 중이므로 인벤토리 여닫기(Space)를 막는다.
    private bool bHUDDown = false;

    // MainMenu → Dungeon 튜토리얼: 로고 연출이 끝난 뒤 처음으로 HUD가 다 올라오는 시점에만
    // 인트로 종료를 알리기 위한 예약 플래그(일반 던전/타운 전환의 HUD 복귀와 구분한다).
    private bool bWaitingIntroProductionEnd = false;

    // 튜토리얼 퀘스트 체인이 진행 중인 동안(첫 스텝 시작 ~ 마지막 스텝(UpgradeAxe) 완료)만 true.
    // ResultUI가 튜토리얼 중 Retry를 막는 등 자체 판단을 하도록 SetTutorialState()로 넘겨준다.
    private bool bIsTutorialActive = false;

    // TreeDetected()를 unitUI에 전달했는지. bIsTutorialActive일 때만 TreeDetected를 전달하므로,
    // 짝이 되는 TreeDetectionCleared는 이 값이 true일 때만 전달해 on/off 호출이 항상 쌍을 이루게 한다.
    private bool bTreeDetectedNotified = false;

    // bIsTutorialActive 여부와 무관하게, 원본 TreeDetectedSignal/TreeDetectionClearedSignal이
    // 마지막으로 알려온 실제 감지 상태. 하차 직후처럼 튜토리얼이 아직 시작되기 전에 감지 신호가
    // 와서 bTreeDetectedNotified로 전달되지 못하고 씹히는 경우를 대비해, 튜토리얼이 시작되는
    // 순간(TutorialStepStarted) 이 값을 보고 놓친 알림을 한 번 따라잡는다.
    private bool bTreeCurrentlyDetected = false;

    private MapType mapType;
    private ForestType forestType;

    public void Initialize(SignalHub _signalHub, InputManager _inputManager, UIView_Popup _popUpUI, UIView_HUD _hudUI,
     UIView_Unit _unitUI, UIView_WorldPopup _worldPopupUI, UIView_MenuPopup _menuPopupUI, UIView_Tent _tentUI, UIView_ESC _escUI,
     UIDepthController _uiDepthController, UIView_SkyProduction _skyProduction, UIView_Result _resultUI, UIView_Warning _warningUI,
     UIView_OverUIPopup _overUIPopupUI, UIView_ScreenModal _screenModalUI)
    {
        inputManager = _inputManager;
        popUpUI = _popUpUI;
        hudUI = _hudUI;
        signalHub = _signalHub;
        unitUI = _unitUI;
        worldPopupUI = _worldPopupUI;
        menuPopupUI = _menuPopupUI;
        tentUI = _tentUI;
        escUI = _escUI;
        uiDepthController = _uiDepthController;
        skyProduction = _skyProduction;
        resultUI = _resultUI;
        warningUI = _warningUI;
        overUIPopupUI = _overUIPopupUI;
        screenModalUI = _screenModalUI;

        SubscribeSignals();
        BindEvents();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.Subscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.Subscribe<TreeGemTransformedSignal>(TreeGemTransformed);
        signalHub.Subscribe<TreeShieldRecoveringSignal>(TreeShieldRecovering);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<ContainerUpdatedSignal>(ContainerUpdated);
        signalHub.Subscribe<ContainerInteractStateChangedSignal>(ContainerInteractStateChanged);
        signalHub.Subscribe<CharacterEarnMoneySignal>(CharacterEarnMoney);
        signalHub.Subscribe<WeaponModeChangedSignal>(WeaponModeChanged);
        signalHub.Subscribe<TentInteractSignal>(TentInteract);
        signalHub.Subscribe<PortalActivatedSignal>(PortalActivated);
        signalHub.Subscribe<InventorySpecChangedSignal>(InventorySpecChanged);
        signalHub.Subscribe<LogContainerSpecChangedSignal>(LogContainerSpecChanged);
        signalHub.Subscribe<SpendMoneySignal>(SpendMoney);
        signalHub.Subscribe<TownStartedSignal>(TownStarted);
        signalHub.Subscribe<DecalreDungeonTypeSignal>(DungeonStarted);
        signalHub.Subscribe<AnimalHitSignal>(AnimalHit);
        signalHub.Subscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.Subscribe<PrestigeLevelIncreasedSignal>(PrestigeLevelIncreased);
        signalHub.Subscribe<PortalDeActivatedSignal>(PortalDeActivated);
        signalHub.Subscribe<OffraodContainerSpecChangedSignal>(OffraodContainerSpecChanged);
        signalHub.Subscribe<OffroadContainerInteractStateChangedSignal>(OffroadContainerInteractStateChanged);
        signalHub.Subscribe<LoosAllInventoryItemSignal>(LoosAllInventoryItem);
        signalHub.Subscribe<OffroadContainerUpdatedSignal>(OffroadContainerUpdated);
        signalHub.Subscribe<InventoryIsFullSignal>(InventoryIsFull);
        signalHub.Subscribe<OffroadSpawnedSignal>(OffroadSpawned);
        signalHub.Subscribe<ItemAddedToInventorySignal>(ItemAddedToInventory);
        signalHub.Subscribe<ItemRemovedFromInventorySignal>(ItemRemovedFromInventory);
        signalHub.Subscribe<TentInteractStateChangedSignal>(TentInteractStateChanged);
        signalHub.Subscribe<OffroadInteractStateChangedSignal>(OffroadInteractStateChanged);
        signalHub.Subscribe<ShopInteractStateChangedSignal>(ShopInteractStateChanged);
        signalHub.Subscribe<LogItemProcessorActiveStateSignal>(LogItemProcessorIsActive);
        signalHub.Subscribe<ItemCantAcquiedSignal>(ItemCantAcquired_Inventory);
        signalHub.Subscribe<DeclareSkillAccumulatedValueSignal>(DeclareSkillAccumulativeValue);
        signalHub.Subscribe<StartSkyProductionSignal>(StartSkyProduction);
        signalHub.Subscribe<RollbackSkyProductionSignal>(RollbackSkyProduction);
        signalHub.Subscribe<PopupUIDownSignal>(PopupUIDown);
        signalHub.Subscribe<PopupUIUpSignal>(PopupUIUp);
        signalHub.Subscribe<ProvideSkillAccumulatedValueChangeSignal>(ProvideAccumulatedValueChangeEvent);
        signalHub.Subscribe<GameEndSignal>(GameEnd);
        signalHub.Subscribe<ActivateWarningUISignal>(ActivateWarningUI);
        signalHub.Subscribe<InventoryItemTransferToOffroadContainerSignal>(InventoryItemToOffroadContainer);
        signalHub.Subscribe<DeclareDungeonStateSignal>(DeclareDungeonState);
        signalHub.Subscribe<RepairBoxInteractStateChangedSignal>(RepairBoxInteractStateChanged);
        signalHub.Subscribe<StudioLogoRevealSignal>(StudioLogoReveal);
        signalHub.Subscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.Subscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.Subscribe<CompleteDungeonEntrySignal>(CompleteDungeonEntry);
        signalHub.Subscribe<TreeDetectedSignal>(TreeDetected);
        signalHub.Subscribe<TreeDetectionClearedSignal>(TreeDetectionCleared);
        signalHub.Subscribe<LootPillarInteractStateChangedSignal>(LootPillarInteractStateChanged);
        signalHub.Subscribe<LootPillarInteractSignal>(LootPillarInteract);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.UnSubscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.UnSubscribe<TreeGemTransformedSignal>(TreeGemTransformed);
        signalHub.UnSubscribe<TreeShieldRecoveringSignal>(TreeShieldRecovering);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<ContainerUpdatedSignal>(ContainerUpdated);
        signalHub.UnSubscribe<ContainerInteractStateChangedSignal>(ContainerInteractStateChanged);
        signalHub.UnSubscribe<CharacterEarnMoneySignal>(CharacterEarnMoney);
        signalHub.UnSubscribe<WeaponModeChangedSignal>(WeaponModeChanged);
        signalHub.UnSubscribe<TentInteractSignal>(TentInteract);
        signalHub.UnSubscribe<PortalActivatedSignal>(PortalActivated);
        signalHub.UnSubscribe<InventorySpecChangedSignal>(InventorySpecChanged);
        signalHub.UnSubscribe<LogContainerSpecChangedSignal>(LogContainerSpecChanged);
        signalHub.UnSubscribe<SpendMoneySignal>(SpendMoney);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
        signalHub.UnSubscribe<DecalreDungeonTypeSignal>(DungeonStarted);
        signalHub.UnSubscribe<AnimalHitSignal>(AnimalHit);
        signalHub.UnSubscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.UnSubscribe<PrestigeLevelIncreasedSignal>(PrestigeLevelIncreased);
        signalHub.UnSubscribe<PortalDeActivatedSignal>(PortalDeActivated);
        signalHub.UnSubscribe<OffraodContainerSpecChangedSignal>(OffraodContainerSpecChanged);
        signalHub.UnSubscribe<OffroadContainerInteractStateChangedSignal>(OffroadContainerInteractStateChanged);
        signalHub.UnSubscribe<LoosAllInventoryItemSignal>(LoosAllInventoryItem);
        signalHub.UnSubscribe<OffroadContainerUpdatedSignal>(OffroadContainerUpdated);
        signalHub.UnSubscribe<InventoryIsFullSignal>(InventoryIsFull);
        signalHub.UnSubscribe<OffroadSpawnedSignal>(OffroadSpawned);
        signalHub.UnSubscribe<ItemAddedToInventorySignal>(ItemAddedToInventory);
        signalHub.UnSubscribe<ItemRemovedFromInventorySignal>(ItemRemovedFromInventory);
        signalHub.UnSubscribe<TentInteractStateChangedSignal>(TentInteractStateChanged);
        signalHub.UnSubscribe<OffroadInteractStateChangedSignal>(OffroadInteractStateChanged);
        signalHub.UnSubscribe<ShopInteractStateChangedSignal>(ShopInteractStateChanged);
        signalHub.UnSubscribe<LogItemProcessorActiveStateSignal>(LogItemProcessorIsActive);
        signalHub.UnSubscribe<ItemCantAcquiedSignal>(ItemCantAcquired_Inventory);
        signalHub.UnSubscribe<DeclareSkillAccumulatedValueSignal>(DeclareSkillAccumulativeValue);
        signalHub.UnSubscribe<StartSkyProductionSignal>(StartSkyProduction);
        signalHub.UnSubscribe<RollbackSkyProductionSignal>(RollbackSkyProduction);
        signalHub.UnSubscribe<PopupUIDownSignal>(PopupUIDown);
        signalHub.UnSubscribe<PopupUIUpSignal>(PopupUIUp);
        signalHub.UnSubscribe<ProvideSkillAccumulatedValueChangeSignal>(ProvideAccumulatedValueChangeEvent);
        signalHub.UnSubscribe<GameEndSignal>(GameEnd);
        signalHub.UnSubscribe<ActivateWarningUISignal>(ActivateWarningUI);
        signalHub.UnSubscribe<InventoryItemTransferToOffroadContainerSignal>(InventoryItemToOffroadContainer);
        signalHub.UnSubscribe<DeclareDungeonStateSignal>(DeclareDungeonState);
        signalHub.UnSubscribe<RepairBoxInteractStateChangedSignal>(RepairBoxInteractStateChanged);
        signalHub.UnSubscribe<StudioLogoRevealSignal>(StudioLogoReveal);
        signalHub.UnSubscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.UnSubscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.UnSubscribe<CompleteDungeonEntrySignal>(CompleteDungeonEntry);
        signalHub.UnSubscribe<TreeDetectedSignal>(TreeDetected);
        signalHub.UnSubscribe<TreeDetectionClearedSignal>(TreeDetectionCleared);
        signalHub.UnSubscribe<LootPillarInteractStateChangedSignal>(LootPillarInteractStateChanged);
        signalHub.UnSubscribe<LootPillarInteractSignal>(LootPillarInteract);
    }

    private void BindEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        inputManager.inputReader.InventoryKeyEvent += OnInventoryKeyPressed;

        popUpUI.sendDeleteItemEvent -= SendDeleteItem;
        popUpUI.sendDeleteItemEvent += SendDeleteItem;

        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        menuPopupUI.DungeonSelectedEvent += DungeonSelected;

        menuPopupUI.CancelButtonClickedEvent -= CancelMenuPopup;
        menuPopupUI.CancelButtonClickedEvent += CancelMenuPopup;

        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        inputManager.inputReader.ESCButtonPressedEvent += EscButtonPressed;

        inputManager.inputReader.UICancelEvent -= OnUICancelPressed;
        inputManager.inputReader.UICancelEvent += OnUICancelPressed;

        escUI.ResumeButtonClickedEvent -= ResumeGame;
        escUI.ResumeButtonClickedEvent += ResumeGame;

        escUI.SaveGameButtonClickedEvent -= SaveGame;
        escUI.SaveGameButtonClickedEvent += SaveGame;

        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.GoToMainMenuButtonClickedEvent += GoToMainMenu;

        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.ExitButtonClickedEvent += ExitGame;

        escUI.UIInputLockChangedEvent -= ESCUIInputLockChanged;
        escUI.UIInputLockChangedEvent += ESCUIInputLockChanged;

        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        menuPopupUI.TeleportUIClosedEvent += TeleportUIClosed;

        tentUI.TentUIClosedEvent -= TentUIClosed;
        tentUI.TentUIClosedEvent += TentUIClosed;

        if (null != screenModalUI)
        {
            screenModalUI.ScreenModalClosedEvent -= ScreenModalClosed;
            screenModalUI.ScreenModalClosedEvent += ScreenModalClosed;
        }

        menuPopupUI.UnlockProductionStartedEvent -= MenuPopupUnlockProductionStarted;
        menuPopupUI.UnlockProductionStartedEvent += MenuPopupUnlockProductionStarted;

        menuPopupUI.UnlockProductionEndedEvent -= MenuPopupUnlockProductionEnded;
        menuPopupUI.UnlockProductionEndedEvent += MenuPopupUnlockProductionEnded;

        menuPopupUI.DungeonConfirmStartedEvent -= DungeonConfirmStarted;
        menuPopupUI.DungeonConfirmStartedEvent += DungeonConfirmStarted;

        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
        popUpUI.InventoryUIOpendEvent += InventoryUIOpened;

        resultUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        resultUI.GoHomeButtonClickedEvent += GoHomeButtonClicked;

        resultUI.RetryButtonClickedEvent -= RetryGame;
        resultUI.RetryButtonClickedEvent += RetryGame;

        warningUI.DeActivateWarningUIEvent -= DeActivateWarningUI;
        warningUI.DeActivateWarningUIEvent += DeActivateWarningUI;

        overUIPopupUI.CompanyLogoProductionCompletedEvent -= CompanyLogoProductionCompleted;
        overUIPopupUI.CompanyLogoProductionCompletedEvent += CompanyLogoProductionCompleted;

        overUIPopupUI.TutorialQuestHideCompletedEvent -= TutorialQuestHideCompleted;
        overUIPopupUI.TutorialQuestHideCompletedEvent += TutorialQuestHideCompleted;

        overUIPopupUI.TutorialQuestTransitionCompletedEvent -= TutorialQuestTransitionCompleted;
        overUIPopupUI.TutorialQuestTransitionCompletedEvent += TutorialQuestTransitionCompleted;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        popUpUI.sendDeleteItemEvent -= SendDeleteItem;
        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        menuPopupUI.CancelButtonClickedEvent -= CancelMenuPopup;
        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        inputManager.inputReader.UICancelEvent -= OnUICancelPressed;
        escUI.ResumeButtonClickedEvent -= ResumeGame;
        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.SaveGameButtonClickedEvent -= SaveGame;
        escUI.UIInputLockChangedEvent -= ESCUIInputLockChanged;
        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        tentUI.TentUIClosedEvent -= TentUIClosed;
        if (null != screenModalUI)
        {
            screenModalUI.ScreenModalClosedEvent -= ScreenModalClosed;
        }
        menuPopupUI.UnlockProductionStartedEvent -= MenuPopupUnlockProductionStarted;
        menuPopupUI.UnlockProductionEndedEvent -= MenuPopupUnlockProductionEnded;
        menuPopupUI.DungeonConfirmStartedEvent -= DungeonConfirmStarted;
        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
        resultUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        resultUI.RetryButtonClickedEvent -= RetryGame;
        warningUI.DeActivateWarningUIEvent -= DeActivateWarningUI;
        overUIPopupUI.CompanyLogoProductionCompletedEvent -= CompanyLogoProductionCompleted;
        overUIPopupUI.TutorialQuestHideCompletedEvent -= TutorialQuestHideCompleted;
        overUIPopupUI.TutorialQuestTransitionCompletedEvent -= TutorialQuestTransitionCompleted;
    }

    public void Release()
    {
        // 경고창/특성 창이 열린 채로 씬이 정리되면(예: 메인 메뉴 복귀) 잠금만 남아 다음 판에서
        // 인벤토리가 영영 열리지 않는다. InputManager가 이 코디네이터보다 오래 살 수 있으므로
        // 걸어둔 잠금은 여기서 반납한다. (걸린 적이 없으면 무시된다)
        inputManager?.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_WARNINGUI, false);
        inputManager?.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_TENTUI, false);
        inputManager?.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_SCREENMODAL, false);

        UnSubscribeSignals();
        ReleaseEvents();
    }

    public void Refresh()
    {
        hudUI.Refresh();
        unitUI.Refresh();
        popUpUI.Refresh();
        worldPopupUI.Refresh();
        menuPopupUI.Refresh();
        tentUI.Refresh();
        escUI.Refresh();
        menuPopupUI.Refresh();
    }

    private void OnInventoryKeyPressed()
    {
        if (bHUDDown)
            return;

        if (LoadingManager.Instance != null && LoadingManager.Instance.IsLoading)
            return;

        if (bInventoryOpened == false)
        {
            bInventoryOpened = true;
            popUpUI.Show();
        }
        else
        {
            bInventoryOpened = false;
            popUpUI.Hide();
        }
    }

    private void InventoryUpdated(InventoryUpdatedSignal inventoryUpdatedSignal)
    {
        popUpUI.InventoryShowEvent();
    }

    private void TreeGetHit(TreeGetHitSignal treeGetHitSignal)
    {
        unitUI.TreeGetHit(treeGetHitSignal.treeObj);
    }

    private void TreeGemTransformed(TreeGemTransformedSignal treeGemTransformedSignal)
    {
        unitUI.TreeGemTransformed(treeGemTransformedSignal.treeObj);
    }

    private void TreeShieldRecovering(TreeShieldRecoveringSignal treeShieldRecoveringSignal)
    {
        unitUI.TreeShieldRecovering(treeShieldRecoveringSignal.treeObj);
    }

    private void CharacterSpawned(CharacterSpawnedSignal characterSpawendSignal)
    {
        hudUI.SetCharacter(characterSpawendSignal.character);
        unitUI.SetCharacter(characterSpawendSignal.character);
        worldPopupUI.SetCharacter(characterSpawendSignal.character);
    }

    private void GoHomeButtonClicked()
    {
        signalHub.Publish(new GoHomeButtonClickedSignal());
    }

    private void SendDeleteItem(IInventorySlot _inData)
    {
        signalHub.Publish(new DeleteItemSignal(_inData));
    }

    private void ContainerUpdated(ContainerUpdatedSignal containerUpdatedSignal)
    {
        worldPopupUI.ContainerUpdated();
    }

    private void ContainerInteractStateChanged(ContainerInteractStateChangedSignal containerInteractStateChangedSignal)
    {
        worldPopupUI.LogContainerInteractStateChanged(containerInteractStateChangedSignal.state);
        popUpUI.LogContainerCanInteract(containerInteractStateChangedSignal.state);
        unitUI.LogContainerInteractStateChanged(containerInteractStateChangedSignal.state);
    }

    private void CharacterEarnMoney(CharacterEarnMoneySignal characterEarnMoneySignal)
    {
        popUpUI.CharacterEarnMoney(characterEarnMoneySignal.moneyType);
        tentUI.CharacterEarnMoney(characterEarnMoneySignal.moneyType);
    }

    private void WeaponModeChanged(WeaponModeChangedSignal weaponModeChangedSignal)
    {
        hudUI.WeaponModeChanged(weaponModeChangedSignal.weaponMode);
        unitUI.WeaponModeChanged(weaponModeChangedSignal.weaponMode);
    }

    private void TentInteract(TentInteractSignal tentInteractSignal)
    {
        if (tentInteractSignal.bInteract == true)
        {
            // TentUI가 "도끼를 강화하세요" 퀘스트 UI와 동시에 보이면 가시성이 나빠지므로(겹침), 지금
            // 열리는 게 그 튜토리얼 스텝 중인지 미리 알려준다.
            tentUI.SetTutorialState(bIsTutorialActive);
            tentUI.Show();

            // TentUI가 떠 있는 동안에는 인벤토리 키를 막는다. TentUI는 스스로 입력 모드를 UI로 바꾸지만
            // 인벤토리 키는 그와 무관하게 살아 있어서, 특성 창 위로 인벤토리가 겹쳐 열릴 수 있다.
            inputManager.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_TENTUI, true);
        }
        else
        {
            tentUI.Hide();
        }
    }

    private void PortalActivated(PortalActivatedSignal _portalActivatedSignal)
    {
        menuPopupUI.Show();
    }

    private void PortalDeActivated(PortalDeActivatedSignal _portalDeActivatedSignal)
    {
        menuPopupUI.Hide();
    }

    private void DungeonSelected(MapType _type, ForestType _forestType)
    {
        mapType = _type;
        forestType = _forestType;

        signalHub.Publish(new TeleportUIClosedWhileTeleportSignal());
        signalHub.Publish(new DungeonSelectedSignal(_type, _forestType));

        menuPopupUI.ForceHide();
    }

    private void CancelMenuPopup()
    {
        menuPopupUI.ForceHide();
    }

    private void InventorySpecChanged(InventorySpecChangedSignal _inventorySpecChangedSignal)
    {
        popUpUI.InventorySpecChanged();
    }

    private void LogContainerSpecChanged(LogContainerSpecChangedSignal _logContainerSpecChangedSignal)
    {
        worldPopupUI.LogContainerSpecChanged();
    }

    private void OffraodContainerSpecChanged(OffraodContainerSpecChangedSignal offraodContainerSpecChangedSignal)
    {
        worldPopupUI.OffraodContainerSpecChanged();
    }

    private void SpendMoney(SpendMoneySignal spendMoneySignal)
    {
        popUpUI.CharactersMoneyChanged();
        tentUI.CharactersMoneyChanged();
    }

    private void SaveGame()
    {
        SaveGameEvent?.Invoke();
    }

    private void ResumeGame()
    {
        // escUI.OnResumeButtonClicked()가 Hide()를 이미 호출한 뒤 이 이벤트를 발행하므로
        // 여기서는 EscButtonPressed의 ESC 키 종료 경로와 동일하게 이동/시간만 복구한다.
        inputManager.PauseMove(bMovePausedBeforeEsc);
        Time.timeScale = 1f;

        if (null != overUIPopupUI)
        {
            overUIPopupUI.SetPauseState(false);
        }
    }

    private void GoToMainMenu()
    {
        // 카메라 상승 연출이 재생되는 동안 중복 클릭으로 재진입하지 못하도록 즉시 닫는다.
        if (null != escUI)
        {
            escUI.HideImmediately();
        }
        inputManager.PauseMove(false);

        if (null != overUIPopupUI)
        {
            overUIPopupUI.ResetQuest();
        }

        GoToMainMenuEvent?.Invoke();
        Time.timeScale = 1f;
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EscButtonPressed()
    {
        if (null != uiDepthController && uiDepthController.TryCloseTopView())
        {
            return;
        }

        if (null != escUI && true == escUI.IsVisible)
        {
            if (true == escUI.IsOptionOpen)
            {
                escUI.CloseOption();
                return;
            }

            escUI.Hide();
            inputManager.PauseMove(bMovePausedBeforeEsc);
            Time.timeScale = 1f;

            if (null != overUIPopupUI)
            {
                overUIPopupUI.SetPauseState(false);
            }
        }
        else if (null != escUI)
        {
            // 반드시 PauseMove(true)보다 먼저 읽는다.
            bMovePausedBeforeEsc = inputManager.IsMovePaused;

            escUI.ShowPauseMenu();
            inputManager.PauseMove(true);
            Time.timeScale = 0f;

            if (null != overUIPopupUI)
            {
                overUIPopupUI.SetPauseState(true);
            }
        }
    }

    private void OnUICancelPressed()
    {
        if (null != uiDepthController && true == uiDepthController.TryCloseTopView())
        {
            return;
        }

        if (null != escUI && true == escUI.IsVisible)
        {
            if (true == escUI.IsOptionOpen)
            {
                escUI.CloseOption();
                return;
            }

            escUI.Hide();
            inputManager.PauseMove(bMovePausedBeforeEsc);
            Time.timeScale = 1f;

            if (null != overUIPopupUI)
            {
                overUIPopupUI.SetPauseState(false);
            }
            return;
        }

        if (null != popUpUI && true == popUpUI.IsVisible)
        {
            popUpUI.Hide();
            return;
        }
    }

    private void TownStarted(TownStartedSignal townStartedSignal)
    {
        unitUI.Refresh();
        unitUI.TownStarted();

        hudUI.SetCurrentMapType(MapType.Town, ForestType.InTown);
        popUpUI.SetCurrentMapType(MapType.Town, ForestType.InTown);
        worldPopupUI.SetCurrentMapType(MapType.Town, ForestType.InTown);
        CurrencyFontHUD.SetGlobalMapType(MapType.Town);

        bInventoryOpened = false;
        bIsTutorialQuestHiding = false;
        bPendingGameEnd = false;
        popUpUI.Hide();
    }

    private void DungeonStarted(DecalreDungeonTypeSignal decareDungeonTypeSignal)
    {
        unitUI.DungeonStarted();

        // MainMenu → Dungeon 직행 등, DungeonSelected(MapType,ForestType) UI 콜백을 거치지 않고 던전에
        // 들어온 경우에도 DeclareDungeonState()가 올바른 mapType/forestType을 참조하도록 여기서 동기화한다.
        mapType = decareDungeonTypeSignal.mapType;
        forestType = decareDungeonTypeSignal.forestType;

        hudUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        popUpUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        worldPopupUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        CurrencyFontHUD.SetGlobalMapType(decareDungeonTypeSignal.mapType);

        resultUI.DungeonStarted();

        bInventoryOpened = false;
        bIsTutorialQuestHiding = false;
        bPendingGameEnd = false;
        popUpUI.Hide();
    }

    private void AnimalHit(AnimalHitSignal animalHitSignal)
    {
        unitUI.AnimalGetHit(animalHitSignal.animal);
    }

    private void CompleteDungeonEntry(CompleteDungeonEntrySignal completeDungeonEntrySignal)
    {
        unitUI.CompleteDungeonEntry();
    }

    // 무조건 튜토리얼 진행 중(bIsTutorialActive)일 때만 unitUI에 전달한다. 일반 플레이 중의
    // 나무 감지는 UI에 알리지 않는다.
    private void TreeDetected(TreeDetectedSignal treeDetectedSignal)
    {
        bTreeCurrentlyDetected = true;

        if (bIsTutorialActive == false)
            return;

        bTreeDetectedNotified = true;
        unitUI.TreeDetected();
    }

    private void TreeDetectionCleared(TreeDetectionClearedSignal treeDetectionClearedSignal)
    {
        bTreeCurrentlyDetected = false;

        if (bTreeDetectedNotified == false)
            return;

        bTreeDetectedNotified = false;
        unitUI.TreeDetectionCleared();
    }

    private void SkillDispatched(SkillDispatchedSignal skillDispatchedSignal)
    {
        hudUI.Refresh();
        popUpUI.Refresh();
        worldPopupUI.Refresh();
    }

    private void PrestigeLevelIncreased(PrestigeLevelIncreasedSignal _signal)
    {
        overUIPopupUI.PrestigeLevelIncreased(_signal.level);
    }

    private void TeleportUIClosed()
    {
        // 뷰를 닫는 호출(ForceHide/Hide)은 항상 이 이벤트를 발행하는 쪽(ESC의 UIDepthController,
        // PortalDeActivated, DungeonSelected, CancelMenuPopup)에서 이미 끝낸 뒤이므로 여기서는
        // 후속 신호만 발행한다.
        signalHub.Publish(new TeleportUIClosedSignal());
    }

    private void TentUIClosed()
    {
        // ESC/패드 Cancel로 닫히는 경로는 TentInteractSignal(false)를 거치지 않으므로, 잠금 해제는
        // 어떤 경로로 닫히든 항상 발행되는 이 이벤트(UIView_Tent.OnHide)에서 한다.
        inputManager.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_TENTUI, false);

        signalHub.Publish(new TentUIClosedSignal());
    }

    // 지역/서브지역 해금 연출(popupNavMain.OnUnlockProductionStarted/Ended) 재생 중에는 상호작용 키를
    // 잠가서, 그 사이 상호작용 키가 다시 눌려 OffroadVehicleObj의 토글 의도(bUIActivated)가 실제 UI
    // 상태와 어긋나는 것을 막는다.
    // 주의: 이건 해금 연출 구간만 방어한다. 기본 등장/퇴장 트윈(매번 여닫을 때마다 재생)까지 막으려면
    // UIView_MenuPopup/HUD_PopupNav_Main 쪽에 더 넓은 범위의 이벤트가 필요한데, 그 시스템은 건드리지
    // 않기로 했으므로 이번 방어는 해금 연출이 겹치는 케이스로 한정된다.
    private void MenuPopupUnlockProductionStarted()
    {
        inputManager.PauseInteractKey(true);
    }

    private void MenuPopupUnlockProductionEnded()
    {
        inputManager.PauseInteractKey(false);
    }

    // 플레이어가 들어갈 던전을 클릭해 선택을 확정한 바로 그 시점(HUD_PopupNav_Main.HandleSubRegionSelected)에
    // ESC를 막는다. 실제 DungeonSelectedSignal(TownSystem.DungeonSelected)은 내비게이션 UI가 닫히는 연출과
    // dungeonConfirmDelay만큼 늦게 발동되는데, 그 사이 구간도 이미 취소 불가능한 선택이므로 여기서 미리 잠근다.
    // 해제 시점은 기존과 동일하게 TownSystem.CompleteDungeonEntry().
    // 같은 시점부터 Space(인벤토리)도 함께 막는다 - 이미 취소 불가능한 던전 진입 연출 중에 인벤토리를
    // 여닫으면 이후 이어지는 PopupUIDown/HUDGoDown 연출과 겹쳐 조작이 꼬일 수 있다.
    private void DungeonConfirmStarted()
    {
        inputManager.PauseESCKey(true);
        inputManager.PauseInventoryKey(true);
    }

    // ESC 메뉴의 등장/퇴장 연출 중에는 재입력을 막는다.
    //
    // UI 취소(패드 B/○)도 반드시 함께 잠근다. ESC만 막으면 패드 유저는 B로 그대로 뚫고 들어와
    // 연출을 중간에 갈아엎을 수 있는데, 그때 등장 연출의 완료 콜백(=이 잠금을 푸는 콜백)이 함께
    // 죽어 ESC가 영구히 잠긴 채로 남는다.
    //
    // 잠금 소유자를 따로 두는 이유: 던전 진입/귀환 연출도 ESC를 잠그는데, 공용 잠금을 쓰면
    // 한쪽의 해제가 다른 쪽이 아직 막아야 하는 구간까지 같이 풀어버린다.
    private void ESCUIInputLockChanged(bool _isLocked)
    {
        inputManager.SetESCKeyLock(InputReader.ESC_LOCK_OWNER_ESCUI, _isLocked);
        inputManager.PauseUICancelKey(_isLocked);
    }

    private void OffroadContainerInteractStateChanged(OffroadContainerInteractStateChangedSignal _offroadContainerInteractStateChangedSignal)
    {
        worldPopupUI.OffroadContainerInteractStateChanged(_offroadContainerInteractStateChangedSignal.state);
        popUpUI.LogContainerCanInteract(_offroadContainerInteractStateChangedSignal.state);
        unitUI.OffroadContainerInteractStateChanged(_offroadContainerInteractStateChangedSignal.state);
    }

    private void LoosAllInventoryItem(LoosAllInventoryItemSignal _loosAllInventoryItemSignal)
    {
        popUpUI.LoosAllInventoryItems();
    }

    private void OffroadContainerUpdated(OffroadContainerUpdatedSignal offroadContainerUpdatedSignal)
    {
        worldPopupUI.OffroadContainerUpdated();
    }

    private void InventoryIsFull(InventoryIsFullSignal _inventoryIsFullSignal)
    {
        unitUI.InventoryIsFull();
        popUpUI.InventoryIsFull();
    }

    private void OffroadSpawned(OffroadSpawnedSignal _offroadSpawnedSignal)
    {
        hudUI.OffroadSpawned(_offroadSpawnedSignal.offroadVehicleObj);
    }

    private void ItemAddedToInventory(ItemAddedToInventorySignal _itemAddedToInventorySignal)
    {
        popUpUI.ItemAddedToInventory();
    }

    private void ItemRemovedFromInventory(ItemRemovedFromInventorySignal itemRemovedFromInventorySignal)
    {
        popUpUI.ItemRemovedFromInventory();
    }

    private void TentInteractStateChanged(TentInteractStateChangedSignal _tentInteractStateChangedSignal)
    {
        unitUI.TentInteractStateChanged(_tentInteractStateChangedSignal.state);
    }

    private void OffroadInteractStateChanged(OffroadInteractStateChangedSignal _offroadInteractStateChangedSignal)
    {
        unitUI.OffroadInteractStateChanged(_offroadInteractStateChangedSignal.state);
    }

    private void RepairBoxInteractStateChanged(RepairBoxInteractStateChangedSignal _repairBoxInteractStateChangedSignal)
    {
        unitUI.RepairBoxInteractStateChanged(_repairBoxInteractStateChangedSignal.state);
    }

    private void ShopInteractStateChanged(ShopInteractStateChangedSignal _shopInteractStateChangedSignal)
    {
        unitUI.ShopInteractStateChanged(_shopInteractStateChangedSignal.state);
    }

    // 콜라이더 범위 진입/이탈 - 상호작용 가능 아이콘(UIView_Unit)만 갱신한다.
    // UIView_ScreenModal을 실제로 여닫는 건 상호작용 키 입력(LootPillarInteract) 쪽이다.
    private void LootPillarInteractStateChanged(LootPillarInteractStateChangedSignal _lootPillarInteractStateChangedSignal)
    {
        unitUI.LootPillarInteractStateChanged(_lootPillarInteractStateChangedSignal.state);
    }

    // 범위 안에서 상호작용 키를 눌렀을 때만 UIView_ScreenModal을 토글로 여닫는다.
    private void LootPillarInteract(LootPillarInteractSignal _lootPillarInteractSignal)
    {
        if (true == _lootPillarInteractSignal.bInteract)
        {
            // ScreenModal이 떠 있는 동안에는 인벤토리 키를 막는다. ScreenModal도 스스로 입력 모드를
            // UI로 바꾸지만, 그 모드는 이 창을 열기 직전 값으로 되돌려지는 값이라 다른 시스템이 먼저
            // UI 모드를 걸어둔 상황에서는 인벤토리 차단의 근거로 삼기에 불안정하다. TentUI와 같은
            // 방식으로 소유자별 잠금을 따로 못 박는다. (해제는 ScreenModalClosed에서)
            inputManager.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_SCREENMODAL, true);
        }

        screenModalUI.LootPillarInteractStateChanged(_lootPillarInteractSignal.bInteract, _lootPillarInteractSignal.lootType);
    }

    // ESC/패드 Cancel로 닫히는 경로는 LootPillarInteractSignal(false)를 거치지 않으므로, 잠금 해제는
    // 어떤 경로로 닫히든 항상 발행되는 이 이벤트(UIView_ScreenModal.OnHide)에서 한다.
    private void ScreenModalClosed()
    {
        inputManager.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_SCREENMODAL, false);

        signalHub.Publish(new LootPillarUIClosedSignal());
    }

    private void LogItemProcessorIsActive(LogItemProcessorActiveStateSignal _logItemProcessorActiveStateSignal)
    {
        worldPopupUI.LogItemProcessorActiveStateChange(_logItemProcessorActiveStateSignal.state);
    }

    private void InventoryUIOpened(bool _boolean)
    {
        bInventoryOpened = _boolean;

        if (_boolean == true)
        {
            popUpUI.Show();
        }
        else
        {
            popUpUI.Hide();
        }
    }

    private void ItemCantAcquired_Inventory(ItemCantAcquiedSignal _itemCantAcquiedSignal)
    {
        unitUI.ItemCantAcquired_Inventory();
        popUpUI.ItemCantAcquired_Inventory();
    }

    private void DeclareSkillAccumulativeValue(DeclareSkillAccumulatedValueSignal _declareSkillAccumulativeValueSignal)
    {

    }

    private void StartSkyProduction(StartSkyProductionSignal _startSkyProductionSignal)
    {
        skyProduction.SetMainMenuMode(_startSkyProductionSignal.isMainMenuRelated);
        skyProduction.StartSkyProduction();
    }

    private void RollbackSkyProduction(RollbackSkyProductionSignal _rollbackSkyProductionSignal)
    {
        skyProduction.StartSkyProduction();
    }

    private void PopupUIDown(PopupUIDownSignal _popupUIDownSignal)
    {
        bHUDDown = true;

        hudUI.HUDGoDown();
        popUpUI.PopupGoDown();
        worldPopupUI.WorldPopupGoDown();
    }

    private void PopupUIUp(PopupUIUpSignal _popupUIUpSignal)
    {
        // bWaitingIntroProductionEnd는 MainMenu → Dungeon 튜토리얼의 스튜디오 로고 연출이 끝난 뒤부터
        // 이번 HUDGoUp의 완료 콜백(HUDGoUpCompleted)에서 꺼질 때까지만 true이므로, 튜토리얼 최초 HUD
        // 노출인지 여기서 정확히 판별할 수 있다. 이 진입에서는 던전 상태 배너를 띄우지 않는다.
        hudUI.HUDGoUp(HUDGoUpCompleted, bWaitingIntroProductionEnd);
        popUpUI.PopupGoUp();
        worldPopupUI.WorldPopupGoUp();
    }

    // HUD가 완전히 다 올라온 시점. 튜토리얼 인트로 중이었다면 그 종료를 UI에 알린다.
    private void HUDGoUpCompleted()
    {
        bHUDDown = false;

        if (bWaitingIntroProductionEnd == false)
            return;

        bWaitingIntroProductionEnd = false;
        overUIPopupUI.IntroProductionEnded();

        // 튜토리얼 로직(TutorialSystem)이 첫 스텝을 시작할 수 있도록 같은 시점에 알린다.
        signalHub.Publish(new TutorialIntroEndedSignal());
    }

    private void TutorialStepStarted(TutorialStepStartedSignal _signal)
    {
        bIsTutorialActive = true;

        // 튜토리얼이 시작되기 전(하차 직후 등)에 이미 TreeDetectedSignal이 와서 씹혔다면,
        // 여기서 놓친 알림을 한 번 따라잡는다.
        if (bTreeCurrentlyDetected && bTreeDetectedNotified == false)
        {
            bTreeDetectedNotified = true;
            unitUI.TreeDetected();
        }

        overUIPopupUI.TutorialStepStarted(_signal.step);
    }

    private void TutorialStepCompleted(TutorialStepCompletedSignal _signal)
    {
        overUIPopupUI.TutorialStepCompleted(_signal.step);

        // 튜토리얼 첫 나무 벌목(CutTree) 완료 즉시 알린다. 실제 ResultUI가 열리는 타이밍보다
        // 훨씬 이르므로 벌목 직후 연출과 함께 처리가 필요한 로직에 활용한다.
        if (_signal.step == TutorialStep.CutTree)
        {
            unitUI.TutorialOffroadResultUIOpened();
        }

        if (_signal.step == TutorialStep.GoHomeBeforeExhausted || _signal.step == TutorialStep.FillOffroadContainer)
        {
            bIsTutorialQuestHiding = true;
        }

        // 튜토리얼 마지막 스텝(StartNewLogging)이 끝나면 이후 결과창은 더 이상 튜토리얼 상태로 취급하지 않는다.
        if (_signal.step == TutorialStep.StartNewLogging)
        {
            bIsTutorialActive = false;

            // 튜토리얼이 끝나는 시점에 나무가 아직 감지된 상태로 남아있다면, 짝이 되는
            // TreeDetectionCleared를 놓치지 않도록 여기서 직접 닫아준다.
            if (bTreeDetectedNotified)
            {
                bTreeDetectedNotified = false;
                unitUI.TreeDetectionCleared();
            }
        }
    }

    private void TutorialQuestHideCompleted(TutorialStep _step)
    {
        bIsTutorialQuestHiding = false;

        if (_step == TutorialStep.GoHomeBeforeExhausted)
        {
            if (bPendingGameEnd)
            {
                bPendingGameEnd = false;
                resultUI.SetTutorialState(bIsTutorialActive);
                resultUI.OpenResultUI();
            }
        }
        else if (_step == TutorialStep.CutTree || _step == TutorialStep.FillOffroadContainer
            || _step == TutorialStep.UpgradeAxe || _step == TutorialStep.ReceiveMoney)
        {
            // CutTree는 InDungeonSystem이 이 신호로 OffroadContainer 상호작용 잠금을 풀고,
            // FillOffroadContainer는 InDungeonSystem이 이 신호로 차량 상호작용 잠금을 풀고,
            // UpgradeAxe는 TutorialSystem이 이 신호로 다음(마지막) 스텝인 StartNewLogging을 시작하며,
            // ReceiveMoney는 TutorialSystem이 이 신호로 UpgradeAxe를 시작한다. 네 경우 모두 "완료
            // 연출(안내 UI가 사라지는 애니메이션)이 실제로 끝난 뒤"에 다음 로직이 이어져야 하므로
            // 스텝 완료 시점이 아니라 이 콜백에서 발행한다.
            signalHub.Publish(new TutorialQuestHideCompletedSignal(_step));

            if (_step == TutorialStep.UpgradeAxe)
            {
                // TentUI(특성 화면)가 이 타이밍을 알아야 퀘스트 UI와 겹치지 않게 특성HUD 노출을
                // 조절할 수 있다(실제 지연/노출 로직은 TentUI 쪽에서 처리).
                tentUI.NotifyTutorialUpgradeAxeQuestUIHidden();
            }
        }
    }

    private void TutorialQuestTransitionCompleted(TutorialStep _step)
    {
        if (_step == TutorialStep.FillOffroadContainer)
        {
            signalHub.Publish(new TutorialQuestTransitionCompletedSignal(_step));
        }
    }

    private void ProvideAccumulatedValueChangeEvent(ProvideSkillAccumulatedValueChangeSignal _signal)
    {
        tentUI.SkillAccumulatedValuePreviewProvided(_signal.data);
    }

    private void GameEnd(GameEndSignal _gameEndSignal)
    {
        if (bIsTutorialQuestHiding)
        {
            bPendingGameEnd = true;
        }
        else
        {
            resultUI.SetTutorialState(bIsTutorialActive);
            resultUI.OpenResultUI();
        }
    }

    private void RetryGame()
    {
        signalHub.Publish(new RetryButtonClickedSignal());
    }

    private void ActivateWarningUI(ActivateWarningUISignal _activateWarningUISignal)
    {
        // WarningUI가 떠 있는 동안에는 인벤토리 키를 막는다. WarningUI는 ESC 메뉴/내비게이션 팝업과
        // 달리 입력 모드를 UI로 바꾸지 않아(InputReader.CanDispatchGameplay 가드에 걸리지 않는다),
        // 확인 팝업 위로 가방이 겹쳐 열리고 그대로 조작까지 되는 문제가 있었다.
        // 해제는 어떤 경로로 닫히든 항상 발행되는 DeActivateWarningUIEvent(=UIView_Warning.OnHide)에서.
        inputManager.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_WARNINGUI, true);

        warningUI.ShowWarning();
    }

    private void DeActivateWarningUI()
    {
        if (warningUI.IsVisible == true)
        {
            // 아직 닫히지 않았다면 닫기만 시킨다. 잠금 해제는 실제로 닫힌 뒤 다시 들어오는
            // 이 콜백(아래 경로)에서 한다.
            warningUI.Hide();
            return;
        }

        inputManager.SetInventoryKeyLock(InputReader.INVENTORY_LOCK_OWNER_WARNINGUI, false);

        signalHub.Publish(new WarningUIClosedSignal(warningUI.bApproved));
    }

    private void InventoryItemToOffroadContainer(InventoryItemTransferToOffroadContainerSignal _inventoryItemToOffroadContainerSignal)
    {
        unitUI.InventoryItemToOffroadContainer();
    }

    private void DeclareDungeonState(DeclareDungeonStateSignal _declareDungeonStateSignal)
    {
        hudUI.DungeonStateDeclared(mapType, forestType, _declareDungeonStateSignal.dungeonState);
    }

    private void StudioLogoReveal(StudioLogoRevealSignal _studioLogoRevealSignal)
    {
        overUIPopupUI.PlayCompanyLogo();
    }

    // 스튜디오 로고 UI 연출이 끝난 시점 - 이후 캐릭터 하차 연출(InDungeonSystem)이 이 신호를 받는다.
    // 하차 뒤 HUD가 다 올라오면 인트로 전체가 끝난 것이므로, 그때까지 종료 통보를 예약해둔다.
    private void CompanyLogoProductionCompleted()
    {
        bWaitingIntroProductionEnd = true;

        signalHub.Publish(new CompanyLogoProductionCompletedSignal());
    }
}
