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
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<TutorialIntroEndedSignal>(TutorialIntroEnded);
        signalHub.UnSubscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.UnSubscribe<InventoryItemTransferToOffroadContainerSignal>(ItemTransferredToOffroadContainer);
        signalHub.UnSubscribe<ItemRemovedFromInventorySignal>(ItemRemovedFromInventory);
        signalHub.UnSubscribe<TutorialStaminaReachedFloorSignal>(TutorialStaminaReachedFloor);
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
