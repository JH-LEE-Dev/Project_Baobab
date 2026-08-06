// MainMenu → Dungeon 튜토리얼 전용. 인트로 연출이 끝난 뒤 세 단계(나무 벌목 → OffroadContainer에 아이템 이관
// → 탈진 전 귀환)를 순서대로 진행시키며, 각 단계의 시작/완료를 신호로만 알린다. 실제 튜토리얼 UI 표시/숨김,
// 상호작용 게이팅(OffroadContainer/OffroadVehicle), 스태미나 바닥값 설정은 각각 이 신호를 구독하는
// UI/InDungeonSystem/UnitSystem 쪽에서 담당한다.
public class TutorialSystem
{
    private SignalHub signalHub;
    private IInventory characterInventory;

    private TutorialStep currentStep;
    private bool bStepActive;

    // 2단계에서 플레이어가 실제로 OffroadContainer에 원목을 넣기 시작했는지.
    // 인벤토리가 비는 것만으로는 "컨테이너에 넣어서 비운 것"인지 구분할 수 없어(버리기 등) 함께 확인한다.
    private bool bContainerTransferStarted;

    public void Initialize(SignalHub _signalHub, IInventory _characterInventory)
    {
        signalHub = _signalHub;
        characterInventory = _characterInventory;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<TutorialIntroEndedSignal>(TutorialIntroEnded);
        signalHub.Subscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.Subscribe<InventoryItemTransferToOffroadContainerSignal>(ItemTransferredToOffroadContainer);
        signalHub.Subscribe<ItemRemovedFromInventorySignal>(ItemRemovedFromInventory);
        signalHub.Subscribe<TutorialStaminaReachedFloorSignal>(TutorialStaminaReachedFloor);
        signalHub.Subscribe<CharacterRideStartSignal>(CharacterRideStart);
        signalHub.Subscribe<ReturnToTownCameraDownEndedSignal>(ReturnToTownCameraDownEnded);
        signalHub.Subscribe<ItemAddedToLogContainerSignal>(ItemAddedToLogContainer);
        signalHub.Subscribe<ShopMoneyUpdatedSignal>(ShopMoneyUpdated);
        signalHub.Subscribe<MoneyEarnedSignal>(MoneyEarned);
        signalHub.Subscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.Subscribe<TownOffroadVehicleActivatedSignal>(TownOffroadVehicleActivated);
        signalHub.Subscribe<TutorialQuestHideCompletedSignal>(TutorialQuestHideCompleted);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<TutorialIntroEndedSignal>(TutorialIntroEnded);
        signalHub.UnSubscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.UnSubscribe<InventoryItemTransferToOffroadContainerSignal>(ItemTransferredToOffroadContainer);
        signalHub.UnSubscribe<ItemRemovedFromInventorySignal>(ItemRemovedFromInventory);
        signalHub.UnSubscribe<TutorialStaminaReachedFloorSignal>(TutorialStaminaReachedFloor);
        signalHub.UnSubscribe<CharacterRideStartSignal>(CharacterRideStart);
        signalHub.UnSubscribe<ReturnToTownCameraDownEndedSignal>(ReturnToTownCameraDownEnded);
        signalHub.UnSubscribe<ItemAddedToLogContainerSignal>(ItemAddedToLogContainer);
        signalHub.UnSubscribe<ShopMoneyUpdatedSignal>(ShopMoneyUpdated);
        signalHub.UnSubscribe<MoneyEarnedSignal>(MoneyEarned);
        signalHub.UnSubscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.UnSubscribe<TownOffroadVehicleActivatedSignal>(TownOffroadVehicleActivated);
        signalHub.UnSubscribe<TutorialQuestHideCompletedSignal>(TutorialQuestHideCompleted);
    }

    private void TutorialIntroEnded(TutorialIntroEndedSignal _signal)
    {
        StartStep(TutorialStep.CutTree);
    }

    // 1단계: 플레이어가 나무를 벌목하면 완료. NPC(럼버잭)가 죽인 나무는 카운트하지 않는다.
    private void TreeIsDead(TreeIsDeadSignal _signal)
    {
        if (bStepActive == false || currentStep != TutorialStep.CutTree || _signal.isPlayerKilled == false)
            return;

        CompleteStep();
        StartStep(TutorialStep.FillOffroadContainer);
    }

    // 2단계: 이관이 시작됐다는 것만 기록한다. 이 신호는 슬롯 전송을 "시작"할 때 발행되어
    // (OffroadContainer.TransferOneSlotVisualRoutine에서 아이템을 실제로 빼내기 전에 호출된다)
    // 이 시점엔 인벤토리가 아직 비어 있지 않으므로, 여기서 완료를 판정하면 영원히 완료되지 않는다.
    private void ItemTransferredToOffroadContainer(InventoryItemTransferToOffroadContainerSignal _signal)
    {
        if (bStepActive == false || currentStep != TutorialStep.FillOffroadContainer)
            return;

        bContainerTransferStarted = true;
    }

    // 실제 완료 판정 지점. 아이템이 인벤토리에서 빠져나간 직후에 발행되는 신호라
    // 여기서 확인해야 마지막 원목까지 넣은 상태를 정확히 잡아낼 수 있다.
    private void ItemRemovedFromInventory(ItemRemovedFromInventorySignal _signal)
    {
        if (bStepActive == false || currentStep != TutorialStep.FillOffroadContainer)
            return;

        // 컨테이너에 한 번도 넣지 않았는데 인벤토리가 빈 경우(아이템 유실 등)는 완료로 치지 않는다.
        if (bContainerTransferStarted == false)
            return;

        if (characterInventory.currentItemCount > 0)
            return;

        CompleteStep();
    }

    private void TutorialStaminaReachedFloor(TutorialStaminaReachedFloorSignal _signal)
    {
        // FillOffroadContainer 퀘스트가 완료된 이후에 피로도가 19%에 도달했을 때 마지막 퀘스트를 시작한다.
        if (bStepActive == false && currentStep == TutorialStep.FillOffroadContainer)
        {
            StartStep(TutorialStep.GoHomeBeforeExhausted);
        }
    }

    private void CharacterRideStart(CharacterRideStartSignal _signal)
    {
        if (bStepActive && currentStep == TutorialStep.GoHomeBeforeExhausted)
        {
            CompleteStep();
        }
    }

    private void ReturnToTownCameraDownEnded(ReturnToTownCameraDownEndedSignal _signal)
    {
        // GoHomeBeforeExhausted는 피로도가 바닥값에 도달하기 전에 플레이어가 스스로 차량을 타고 귀환하면
        // 아예 시작되지 않고 건너뛰어질 수 있는 퀘스트다(차량 상호작용은 FillOffroadContainer 완료와 함께
        // 이미 열려있어, 피로도와 무관하게 그 즉시 탈 수 있다). 이 경우 currentStep은 FillOffroadContainer에
        // 머문 채로 타운에 도착한다. 마을 쪽 퀘스트 체인(PutItemsInLogContainer 이후)은 이 던전 퀘스트의
        // 완료 여부에 영향을 받아서는 안 되므로, 두 경우(정상 완료/스킵) 모두 여기서 다음 단계로 넘긴다.
        if (bStepActive == false &&
            (currentStep == TutorialStep.GoHomeBeforeExhausted || currentStep == TutorialStep.FillOffroadContainer))
        {
            StartStep(TutorialStep.PutItemsInLogContainer);
        }
    }

    private void ItemAddedToLogContainer(ItemAddedToLogContainerSignal _signal)
    {
        if (bStepActive && currentStep == TutorialStep.PutItemsInLogContainer)
        {
            CompleteStep();
        }
    }

    private void ShopMoneyUpdated(ShopMoneyUpdatedSignal _signal)
    {
        // 이전 퀘스트(PutItems)가 아이템을 넣어 이미 완료되어 bStepActive가 false인 상태에서,
        // 가공이 끝나 돈이 들어오면 다음 퀘스트(ReceiveMoney)를 시작한다.
        if (bStepActive == false && currentStep == TutorialStep.PutItemsInLogContainer && _signal.money > 0)
        {
            StartStep(TutorialStep.ReceiveMoney);
        }
    }

    private void MoneyEarned(MoneyEarnedSignal _signal)
    {
        if (bStepActive && currentStep == TutorialStep.ReceiveMoney)
        {
            CompleteStep();
            StartStep(TutorialStep.UpgradeAxe);
        }
    }

    private void SkillDispatched(SkillDispatchedSignal _signal)
    {
        if (bStepActive && currentStep == TutorialStep.UpgradeAxe)
        {
            if (_signal.commandType == SkillCommandType.AxeDamage)
            {
                // 마지막 스텝(StartNewLogging)은 여기서 곧바로 시작하지 않는다. "도끼를 강화하세요"
                // 안내 UI가 사라지는 연출이 실제로 끝난 뒤(TutorialQuestHideCompletedSignal)에 시작한다.
                CompleteStep();
            }
        }
    }

    // UpgradeAxe의 완료 연출(안내 UI가 사라지는 애니메이션)이 실제로 끝난 시점에 마지막 스텝을 시작한다.
    private void TutorialQuestHideCompleted(TutorialQuestHideCompletedSignal _signal)
    {
        if (bStepActive == false && currentStep == TutorialStep.UpgradeAxe && _signal.step == TutorialStep.UpgradeAxe)
        {
            StartStep(TutorialStep.StartNewLogging);
        }
    }

    // 마지막 스텝: 마을에서 OffroadVehicle에 상호작용(다시 던전으로 향함)하면 완료된다.
    private void TownOffroadVehicleActivated(TownOffroadVehicleActivatedSignal _signal)
    {
        if (bStepActive && currentStep == TutorialStep.StartNewLogging)
        {
            CompleteStep();
        }
    }

    private void StartStep(TutorialStep _step)
    {
        currentStep = _step;
        bStepActive = true;

        signalHub.Publish(new TutorialStepStartedSignal(_step));
    }

    private void CompleteStep()
    {
        bStepActive = false;

        signalHub.Publish(new TutorialStepCompletedSignal(currentStep));
    }
}
