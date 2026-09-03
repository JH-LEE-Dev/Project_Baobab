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

    // TutorialIntroEndedSignal(= MainMenu → Dungeon 튜토리얼 진입)로만 true가 된다. 이어하기(GoToTownScene(false))나
    // Town/Dungeon 왕복으로 재생성된 세션에서는 이 신호가 발행되지 않으므로 계속 false로 남아, currentStep/bStepActive의
    // 기본값(각각 CutTree/false)이 "CutTree를 막 끝낸 상태"와 우연히 같아지더라도 아래 스텝 로직이 절대 실행되지 않는다.
    private bool bTutorialSessionActive;

    // 마지막 스텝(StartNewLogging)까지 완료되면 true로 고정된다. 이후 같은 세션에서 Town↔Dungeon을 오가도
    // 튜토리얼 로직이 다시는 실행되지 않도록 완전히 차단한다.
    private bool bTutorialCompleted;

    // 튜토리얼이 시작됐지만 아직 끝나지 않은 구간인지. SaveManager가 이 구간 동안 자동/종료 저장을
    // 막는 데 사용한다(마지막 스텝 완료로 bTutorialCompleted가 true가 되는 순간 곧바로 false로 바뀐다).
    public bool IsTutorialInProgress => CanProcessTutorialLogic();

    // FillOffroadContainer 스텝 동안 OffroadContainer에 "실제로 적재된" 원목 누적 개수.
    // 이 값이 아래 기준치(RequiredItemsForFillOffroadContainer)에 도달하면 해당 단계가 완료된다.
    private int itemsTransferredToOffroadContainer;
    private const int RequiredItemsForFillOffroadContainer = 2;

    // UpgradeAxe 완료 ~ 마지막 스텝(StartNewLogging) 시작 사이에 이미 마을 차량에 탑승했는지.
    // 차량 잠금은 UpgradeAxe 완료 즉시 풀리는 반면 마지막 스텝은 안내 UI 퇴장 연출이 끝나야 시작되므로,
    // 그 빈틈에 들어온 탑승을 놓치지 않기 위해 기억해둔다.
    private bool bTownVehicleActivatedBeforeLastStep;

    // CutTree 완료(나무 벌목) 이후 FillOffroadContainer가 시작되기 전까지 플레이어가 직접 주운
    // 원목 개수. 이 값이 아래 기준치에 도달해야 FillOffroadContainer가 시작된다("나무 한 그루만
    // 베고 바로 다음 퀘스트로" 넘어가면 정작 컨테이너에 넣을 원목이 부족한 경우가 있어,
    // 최소한의 물량을 확보한 뒤 다음 단계를 열어준다).
    private int logItemsAcquiredSinceCutTree;
    private const int RequiredLogItemsForFillOffroadContainer = 2;

    // ReceiveMoney 완료 이후 UpgradeAxe가 시작되기 전까지 정산받은 금액의 누적합. 이 값이 아래
    // 기준치에 도달해야 UpgradeAxe가 시작된다. characterInventory.money를 그대로 읽지 않는 이유는,
    // SignalHub.Publish가 구독자를 등록 역순으로 호출해 UnitSystem이 실제로 money를 반영하기 전에
    // 이 시스템의 MoneyEarned가 먼저 실행될 수 있기 때문이다 - 신호에 담긴 금액을 직접 누적해야
    // 이번 정산이 곧바로 반영된 값으로 정확히 판정할 수 있다.
    private int moneyEarnedSinceReceiveMoney;
    private const int RequiredMoneyForUpgradeAxe = 5;

    // UpgradeAxe는 "돈이 기준치에 도달"과 "ReceiveMoney 안내 UI가 완전히 사라짐" 두 조건을 모두
    // 만족해야 시작된다. 어느 쪽이 먼저 만족될지 알 수 없으므로(정산액이 한 번에 기준치를 넘기면
    // 금액 조건이 먼저, 안내 UI 퇴장 연출이 더 빠르면 UI 조건이 먼저 만족된다) 먼저 만족된 쪽을
    // 여기 기억해뒀다가 나머지 조건도 만족되는 즉시 시작한다.
    private bool bMoneyThresholdReachedForUpgradeAxe;
    private bool bReceiveMoneyUIHideCompleted;

    // ReceiveMoney 완료 ~ UpgradeAxe 시작 사이(안내 UI 퇴장 연출 약 1.7초)에 이미 도끼를 강화했는지.
    // 이 구간은 TownSystem이 집(Tent) 상호작용을 잠가 애초에 들어갈 수 없지만, 그 잠금이 어떤 이유로든
    // 걸리지 않은 경로를 대비한 안전망이다. 놓치면 UpgradeAxe를 완료시킬 방법이 사라지는데, 마을 차량은
    // 그 완료로만 풀리므로 플레이어가 마을에 갇힌다.
    private bool bAxeUpgradedBeforeUpgradeAxeStep;

    // TentUI(집)가 현재 열려 있는지. StartNewLogging은 "UpgradeAxe 안내 UI가 완전히 사라짐"과
    // "TentUI가 닫혀 있음" 두 조건이 모두 만족돼야 시작된다 - 도끼를 강화한 뒤에도 플레이어가
    // 다른 스킬을 마저 둘러보다가 나중에 TentUI를 닫을 수 있어서, 안내 UI가 먼저 사라지더라도
    // TentUI가 열려 있는 동안에는 대기해야 한다(아래 TryStartNewLogging 참고).
    private bool bTentUIOpen;
    private bool bUpgradeAxeUIHideCompleted;

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
        signalHub.Subscribe<ItemAcquiredSignal>(ItemAcquired);
        signalHub.Subscribe<ItemStoredInOffroadContainerSignal>(ItemStoredInOffroadContainer);
        signalHub.Subscribe<TutorialStaminaReachedFloorSignal>(TutorialStaminaReachedFloor);
        signalHub.Subscribe<CharacterRideStartSignal>(CharacterRideStart);
        signalHub.Subscribe<ReturnToTownCameraDownEndedSignal>(ReturnToTownCameraDownEnded);
        signalHub.Subscribe<ItemAddedToLogContainerSignal>(ItemAddedToLogContainer);
        signalHub.Subscribe<ShopMoneyUpdatedSignal>(ShopMoneyUpdated);
        signalHub.Subscribe<MoneyEarnedSignal>(MoneyEarned);
        signalHub.Subscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.Subscribe<TownOffroadVehicleActivatedSignal>(TownOffroadVehicleActivated);
        signalHub.Subscribe<TutorialQuestHideCompletedSignal>(TutorialQuestHideCompleted);
        signalHub.Subscribe<TentInteractSignal>(TentInteracted);
        signalHub.Subscribe<TentUIClosedSignal>(TentUIClosed);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<TutorialIntroEndedSignal>(TutorialIntroEnded);
        signalHub.UnSubscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.UnSubscribe<ItemAcquiredSignal>(ItemAcquired);
        signalHub.UnSubscribe<ItemStoredInOffroadContainerSignal>(ItemStoredInOffroadContainer);
        signalHub.UnSubscribe<TutorialStaminaReachedFloorSignal>(TutorialStaminaReachedFloor);
        signalHub.UnSubscribe<CharacterRideStartSignal>(CharacterRideStart);
        signalHub.UnSubscribe<ReturnToTownCameraDownEndedSignal>(ReturnToTownCameraDownEnded);
        signalHub.UnSubscribe<ItemAddedToLogContainerSignal>(ItemAddedToLogContainer);
        signalHub.UnSubscribe<ShopMoneyUpdatedSignal>(ShopMoneyUpdated);
        signalHub.UnSubscribe<MoneyEarnedSignal>(MoneyEarned);
        signalHub.UnSubscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.UnSubscribe<TownOffroadVehicleActivatedSignal>(TownOffroadVehicleActivated);
        signalHub.UnSubscribe<TutorialQuestHideCompletedSignal>(TutorialQuestHideCompleted);
        signalHub.UnSubscribe<TentInteractSignal>(TentInteracted);
        signalHub.UnSubscribe<TentUIClosedSignal>(TentUIClosed);
    }

    // 튜토리얼 세션인지 + 아직 완료 전인지. 아래 모든 스텝 핸들러는 진입 즉시 이 확인부터 거친다.
    private bool CanProcessTutorialLogic()
    {
        return bTutorialSessionActive && bTutorialCompleted == false;
    }

    private void TutorialIntroEnded(TutorialIntroEndedSignal _signal)
    {
        // 이 신호로 세션이 열리므로 다른 핸들러들처럼 CanProcessTutorialLogic()으로 시작할 수는 없지만,
        // "이미 열려 있으면 무시"는 반드시 필요하다. 이 신호가 두 번 오면 진행 중이던 튜토리얼이
        // CutTree부터 통째로 리셋되기 때문이다(currentStep/bStepActive를 덮어쓰고, bTutorialCompleted도
        // 보지 않는다). bTutorialSessionActive는 false로 돌아가는 곳이 없고 세션마다 이 객체가 새로
        // 만들어지므로, 이 한 줄이 "진행 중"과 "이미 완료" 두 경우를 모두 막는다.
        //
        // 지금은 발행 체인(스튜디오 로고 연출 종료 → 하차 → HUD 상승 → TutorialIntroEndedSignal)이
        // MainMenu → Dungeon 최초 진입에서만 예약되어 두 번 올 일이 없지만, 그 체인은 이 시스템 밖의
        // 여러 단계에 걸쳐 있어 여기서 스스로를 지켜두는 편이 안전하다.
        if (bTutorialSessionActive)
            return;

        bTutorialSessionActive = true;
        StartStep(TutorialStep.CutTree);
    }

    // 1단계: 플레이어가 나무를 벌목하면 완료. NPC(럼버잭)가 죽인 나무는 카운트하지 않는다.
    // 다음 단계(FillOffroadContainer)는 여기서 곧바로 시작하지 않고, 원목을 충분히 주울 때까지
    // 기다린다(아래 ItemAcquired 참고).
    private void TreeIsDead(TreeIsDeadSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        if (bStepActive == false || currentStep != TutorialStep.CutTree || _signal.isPlayerKilled == false)
            return;

        CompleteStep();
    }

    // CutTree 완료 이후, 플레이어가 원목을 RequiredLogItemsForFillOffroadContainer개 이상 직접
    // 주우면 FillOffroadContainer를 시작한다. ItemAcquiredSignal은 LogItemController가 "커스텀
    // 습득자(럼버잭 NPC 등)가 없는" 원목, 즉 플레이어가 직접 주운 원목에 대해서만 발행하므로
    // NPC가 주운 물량은 자연히 제외된다.
    private void ItemAcquired(ItemAcquiredSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        if (bStepActive || currentStep != TutorialStep.CutTree)
            return;

        if (_signal.item is not LogItem)
            return;

        logItemsAcquiredSinceCutTree++;
        if (logItemsAcquiredSinceCutTree < RequiredLogItemsForFillOffroadContainer)
            return;

        StartStep(TutorialStep.FillOffroadContainer);
    }

    // 2단계 완료 판정. 플레이어가 넣은 원목이 날아가는 연출을 끝내고 OffroadContainer에 실제로
    // 적재된 순간에만, 적재된 개수만큼 발행되는 신호를 센다.
    //
    // 예전에는 ItemRemovedFromInventorySignal(인벤토리에서 아이템이 빠짐)을 셌는데, 그 신호에는
    // 어떤 아이템이 어디로 왜 빠졌는지가 전혀 담겨 있지 않아 다음을 구분할 수 없었다.
    //   - 컨테이너에 넣어서 빠진 것 / 버려서 빠진 것 / 탈진으로 유실된 것
    //   - 무엇보다, 슬롯의 마지막 아이템을 꺼내면 아이템 제거로 한 번, 빈 슬롯을 정리하는
    //     InventoryManager.ItemDeleted()에서 다시 한 번 - 총 두 번 울린다.
    // 그래서 1개짜리 슬롯을 이관하면 실제로는 원목 1개만 들어갔는데 카운트가 2가 되어 이 단계가
    // 조기 완료됐다. 이 신호는 착지 커밋이 성공한 경우에만 발행되므로 그런 오차가 없다.
    private void ItemStoredInOffroadContainer(ItemStoredInOffroadContainerSignal _signal)
    {
        if (false == CanProcessTutorialLogic())
            return;

        if (false == bStepActive || TutorialStep.FillOffroadContainer != currentStep)
            return;

        if (ItemType.Log != _signal.itemType)
            return;

        itemsTransferredToOffroadContainer += _signal.count;
        if (itemsTransferredToOffroadContainer < RequiredItemsForFillOffroadContainer)
            return;

        CompleteStep();
    }

    private void TutorialStaminaReachedFloor(TutorialStaminaReachedFloorSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        // FillOffroadContainer 퀘스트가 완료된 이후에 피로도가 19%에 도달했을 때 마지막 퀘스트를 시작한다.
        if (bStepActive == false && currentStep == TutorialStep.FillOffroadContainer)
        {
            StartStep(TutorialStep.GoHomeBeforeExhausted);
        }
    }

    private void CharacterRideStart(CharacterRideStartSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        if (bStepActive && currentStep == TutorialStep.GoHomeBeforeExhausted)
        {
            CompleteStep();
        }
    }

    private void ReturnToTownCameraDownEnded(ReturnToTownCameraDownEndedSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

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
        if (CanProcessTutorialLogic() == false)
            return;

        if (bStepActive && currentStep == TutorialStep.PutItemsInLogContainer)
        {
            CompleteStep();
        }
    }

    private void ShopMoneyUpdated(ShopMoneyUpdatedSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        // 이전 퀘스트(PutItems)가 아이템을 넣어 이미 완료되어 bStepActive가 false인 상태에서,
        // 가공이 끝나 돈이 들어오면 다음 퀘스트(ReceiveMoney)를 시작한다.
        if (bStepActive == false && currentStep == TutorialStep.PutItemsInLogContainer && _signal.money > 0)
        {
            StartStep(TutorialStep.ReceiveMoney);
        }
    }

    // ReceiveMoney는 첫 정산을 받으면 완료된다(기존과 동일). 다음 단계(UpgradeAxe)는 여기서
    // 곧바로 시작하지 않고, 캐릭터의 돈이 RequiredMoneyForUpgradeAxe원 이상이 되고 ReceiveMoney
    // 안내 UI가 완전히 사라질 때까지 기다린다(아래 TryStartUpgradeAxe 참고).
    private void MoneyEarned(MoneyEarnedSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        if (bStepActive && currentStep == TutorialStep.ReceiveMoney)
        {
            CompleteStep();
        }

        if (bStepActive || currentStep != TutorialStep.ReceiveMoney)
            return;

        moneyEarnedSinceReceiveMoney += _signal.money;
        if (moneyEarnedSinceReceiveMoney < RequiredMoneyForUpgradeAxe)
            return;

        bMoneyThresholdReachedForUpgradeAxe = true;
        TryStartUpgradeAxe();
    }

    // 돈 기준치 도달과 ReceiveMoney UI 퇴장 연출 완료, 두 조건이 모두 만족됐을 때만 UpgradeAxe를 시작한다.
    private void TryStartUpgradeAxe()
    {
        if (bStepActive || currentStep != TutorialStep.ReceiveMoney)
            return;

        if (bMoneyThresholdReachedForUpgradeAxe == false || bReceiveMoneyUIHideCompleted == false)
            return;

        bMoneyThresholdReachedForUpgradeAxe = false;
        bReceiveMoneyUIHideCompleted = false;
        StartStep(TutorialStep.UpgradeAxe);

        // 안내 UI가 뜨기 전에 이미 도끼를 강화했다면 조건은 이미 만족된 것이므로 곧바로 완료 처리한다.
        // (아래 TownOffroadVehicleActivated의 빈틈 처리와 같은 방식)
        if (bAxeUpgradedBeforeUpgradeAxeStep)
        {
            bAxeUpgradedBeforeUpgradeAxeStep = false;
            CompleteStep();
        }
    }

    private void SkillDispatched(SkillDispatchedSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        if (_signal.commandType != SkillCommandType.AxeDamage)
            return;

        if (bStepActive && currentStep == TutorialStep.UpgradeAxe)
        {
            // 마지막 스텝(StartNewLogging)은 여기서 곧바로 시작하지 않는다. "도끼를 강화하세요"
            // 안내 UI가 사라지는 연출이 실제로 끝난 뒤(TutorialQuestHideCompletedSignal)에 시작한다.
            CompleteStep();
            return;
        }

        // UpgradeAxe가 아직 시작되지 않은 빈틈에서 강화한 경우. 그대로 두면 스텝이 시작된 뒤에는
        // 완료 조건을 다시 만족시킬 수 없으므로(남은 돈이 재강화 비용에 못 미친다) 기억해뒀다가
        // 스텝이 시작되는 즉시 완료시킨다.
        if (bStepActive == false && currentStep == TutorialStep.ReceiveMoney)
        {
            bAxeUpgradedBeforeUpgradeAxeStep = true;
        }
    }

    // UpgradeAxe의 완료 연출(안내 UI가 사라지는 애니메이션)이 실제로 끝난 시점에 마지막 스텝을 시작한다.
    private void TutorialQuestHideCompleted(TutorialQuestHideCompletedSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        if (_signal.step == TutorialStep.ReceiveMoney)
        {
            bReceiveMoneyUIHideCompleted = true;
            TryStartUpgradeAxe();
            return;
        }

        if (bStepActive == false && currentStep == TutorialStep.UpgradeAxe && _signal.step == TutorialStep.UpgradeAxe)
        {
            bUpgradeAxeUIHideCompleted = true;
            TryStartNewLogging();
        }
    }

    // TentUI(집) 상호작용 토글. 여기서는 "현재 열려 있는지"만 추적한다 - ESC로 닫히는 경로는
    // 이 신호를 거치지 않으므로 닫힘 판정은 아래 TentUIClosed(TentUIClosedSignal)에서 담당한다.
    private void TentInteracted(TentInteractSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        bTentUIOpen = _signal.bInteract;
    }

    // TentUI가 실제로 닫혔음을 알리는 신호. 상호작용 토글(TentInteractSignal)뿐 아니라 ESC로
    // 닫힌 경우에도 항상 발행되므로, "닫혀 있음" 판정은 이 신호를 기준으로 확정한다.
    private void TentUIClosed(TentUIClosedSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        bTentUIOpen = false;
        TryStartNewLogging();
    }

    // UpgradeAxe 안내 UI가 완전히 사라진 것과 TentUI가 닫혀 있는 것, 두 조건이 모두 만족됐을 때만
    // StartNewLogging을 시작한다.
    private void TryStartNewLogging()
    {
        if (bStepActive || currentStep != TutorialStep.UpgradeAxe)
            return;

        if (bUpgradeAxeUIHideCompleted == false || bTentUIOpen)
            return;

        bUpgradeAxeUIHideCompleted = false;
        StartStep(TutorialStep.StartNewLogging);

        // 연출이 흐르는 동안 이미 차량에 탑승했다면 조건은 이미 만족된 것이므로 곧바로 완료 처리한다.
        // TownOffroadVehicleActivated의 정상 완료 분기와 동일하게 여기서도 튜토리얼 전체 완료를 확정해야
        // bTutorialCompleted가 세워지지 않아 IsTutorialInProgress가 계속 true로 남는 것을 막을 수 있다.
        if (bTownVehicleActivatedBeforeLastStep)
        {
            bTownVehicleActivatedBeforeLastStep = false;
            CompleteStep();
            bTutorialCompleted = true;
        }
    }

    // 마지막 스텝: 마을에서 OffroadVehicle에 상호작용(다시 던전으로 향함)하면 완료된다.
    private void TownOffroadVehicleActivated(TownOffroadVehicleActivatedSignal _signal)
    {
        if (CanProcessTutorialLogic() == false)
            return;

        if (bStepActive && currentStep == TutorialStep.StartNewLogging)
        {
            CompleteStep();
            // 튜토리얼의 마지막 스텝이 끝났다 - 이 세션에서는 이후 어떤 신호가 와도 튜토리얼 로직이
            // 다시 실행되지 않도록 완전히 잠근다.
            bTutorialCompleted = true;
            return;
        }

        // UpgradeAxe가 완료되면 차량 잠금은 그 즉시 풀리지만, 마지막 스텝은 안내 UI가 사라지는 연출이
        // 끝난 뒤에야 시작된다. 그 사이(약 1.9초)에 탑승하면 위 조건에 걸리지 않아 스텝이 영영 완료되지
        // 않으므로, 여기서 기억해뒀다가 스텝이 시작될 때 즉시 완료시킨다.
        if (bStepActive == false && currentStep == TutorialStep.UpgradeAxe)
        {
            bTownVehicleActivatedBeforeLastStep = true;
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
