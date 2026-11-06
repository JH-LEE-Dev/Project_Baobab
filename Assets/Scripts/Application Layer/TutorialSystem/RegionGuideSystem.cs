// 2·3·4 대지역(몽글포자숲 / 별뿌리숲 / 용암숲) 첫 입장 시, 튜토리얼 Quest UI로 그 대지역의 기믹을 안내한다.
// 이 시스템은 "언제 띄울지"와 "언제 클리어할지"만 판정해 RegionGuideStarted/CompletedSignal로 알린다.
// 실제 표시/완료 연출은 GameplayUICoordinator → UIView_OverUIPopup → UI_TutorialQuest가 담당한다.
//
// 띄우는 조건: 본편 던전에서 대상 대지역에 들어와 캐릭터가 조작 가능해진 순간(ActivateCharacterSignal),
//             그 대지역 안내를 아직 클리어한 적이 없을 때.
// 클리어 조건: 안내가 떠 있는 동안 (대지역 기믹 트리거 | 탈진 | 오프로드 차량 상호작용).
//             몽글포자숲의 기믹 트리거는 플레이어의 첫 벌목, 별뿌리숲은 별자리 발현, 용암숲은 불씨 획득.
//             클리어 기록은 클리어 시점에만 남기므로, 클리어 전에 이탈하면 다음 입장에서 다시 안내한다.
public class RegionGuideSystem
{
    // 외부 의존성
    private SignalHub signalHub;

    // 상태 변수
    private const int GuideCount = 3;
    private const int SporeForestIndex = 0;
    private const int StarrootForestIndex = 1;
    private const int MagmaForestIndex = 2;

    // 대지역별 안내 클리어 여부(세이브 대상). 인덱스는 위 상수를 따른다.
    private readonly bool[] completedGuides = new bool[GuideCount];

    // 지금 들어와 있는 던전의 대지역. 마을에 도착하면 None으로 돌린다.
    private MapType currentMapType = MapType.None;

    // 현재 Quest UI에 떠 있는 안내. bGuideActive가 false면 의미 없다.
    private TutorialStep activeStep;
    private bool bGuideActive;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(SignalHub _signalHub)
    {
        signalHub = _signalHub;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();

        bGuideActive = false;
        currentMapType = MapType.None;
    }

    public bool IsSporeForestGuideDone => completedGuides[SporeForestIndex];
    public bool IsStarrootForestGuideDone => completedGuides[StarrootForestIndex];
    public bool IsMagmaForestGuideDone => completedGuides[MagmaForestIndex];

    /// <summary>
    /// 세이브 로드 시 대지역별 안내 클리어 여부를 복원한다. 키가 없는 예전 세이브는 false로 들어온다.
    /// </summary>
    public void LoadSaveData(bool _bSporeForestGuideDone, bool _bStarrootForestGuideDone, bool _bMagmaForestGuideDone)
    {
        completedGuides[SporeForestIndex] = _bSporeForestGuideDone;
        completedGuides[StarrootForestIndex] = _bStarrootForestGuideDone;
        completedGuides[MagmaForestIndex] = _bMagmaForestGuideDone;
    }

    // 일반 비즈니스 로직 및 내부 메서드
    private void SubscribeSignals()
    {
        signalHub.Subscribe<DecalreDungeonTypeSignal>(DungeonTypeDeclared);
        signalHub.Subscribe<ActivateCharacterSignal>(CharacterActivated);
        signalHub.Subscribe<TownStartedSignal>(TownStarted);
        signalHub.Subscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.Subscribe<ConstellationManifestedSignal>(ConstellationManifested);
        signalHub.Subscribe<EmberAcquiredSignal>(EmberAcquired);
        signalHub.Subscribe<CharacterStaminaIsEmptySignal>(CharacterStaminaIsEmpty);
        signalHub.Subscribe<OffroadVehicleInteractedSignal>(OffroadVehicleInteracted);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<DecalreDungeonTypeSignal>(DungeonTypeDeclared);
        signalHub.UnSubscribe<ActivateCharacterSignal>(CharacterActivated);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
        signalHub.UnSubscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.UnSubscribe<ConstellationManifestedSignal>(ConstellationManifested);
        signalHub.UnSubscribe<EmberAcquiredSignal>(EmberAcquired);
        signalHub.UnSubscribe<CharacterStaminaIsEmptySignal>(CharacterStaminaIsEmpty);
        signalHub.UnSubscribe<OffroadVehicleInteractedSignal>(OffroadVehicleInteracted);
    }

    private void DungeonTypeDeclared(DecalreDungeonTypeSignal _signal)
    {
        currentMapType = _signal.mapType;
    }

    // 캐릭터가 조작 가능해지고 HUD가 올라온 시점. 던전/마을 양쪽에서 발행되지만, 마을에서는
    // TownStarted가 currentMapType을 None으로 돌려두므로 아래 대상 판정에서 걸러진다.
    private void CharacterActivated(ActivateCharacterSignal _signal)
    {
        // 로그라이크는 본편 대지역 안내 대상이 아니다.
        if (true == GameModeContext.IsRoguelike)
            return;

        if (true == bGuideActive)
            return;

        if (false == TryGetGuideIndex(currentMapType, out int _index, out TutorialStep _step))
            return;

        if (true == completedGuides[_index])
            return;

        activeStep = _step;
        bGuideActive = true;

        signalHub.Publish(new RegionGuideStartedSignal(_step));
    }

    private void TownStarted(TownStartedSignal _signal)
    {
        // 원정이 끝났다. 클리어되지 않은 안내는 기록하지 않고 내려두며(UI는 씬 전환 시 ResetQuest로 정리된다),
        // 다음 입장에서 다시 안내한다.
        bGuideActive = false;
        currentMapType = MapType.None;
    }

    // 몽글포자숲 안내는 플레이어가 나무를 처음 벤 순간 클리어된다(TutorialSystem.CutTree와 같은 판정).
    private void TreeIsDead(TreeIsDeadSignal _signal)
    {
        if (false == _signal.isPlayerKilled)
            return;

        TryCompleteGuide(TutorialStep.SporeForestGuide);
    }

    private void ConstellationManifested(ConstellationManifestedSignal _signal)
    {
        TryCompleteGuide(TutorialStep.StarrootForestGuide);
    }

    private void EmberAcquired(EmberAcquiredSignal _signal)
    {
        TryCompleteGuide(TutorialStep.MagmaForestGuide);
    }

    // 탈진 / 차량 상호작용은 어느 대지역 안내든 클리어시킨다.
    private void CharacterStaminaIsEmpty(CharacterStaminaIsEmptySignal _signal)
    {
        TryCompleteGuide(activeStep);
    }

    private void OffroadVehicleInteracted(OffroadVehicleInteractedSignal _signal)
    {
        TryCompleteGuide(activeStep);
    }

    private void TryCompleteGuide(TutorialStep _step)
    {
        if (false == bGuideActive || _step != activeStep)
            return;

        if (false == TryGetGuideIndex(currentMapType, out int _index, out TutorialStep _currentStep) || _currentStep != _step)
            return;

        bGuideActive = false;
        completedGuides[_index] = true;

        signalHub.Publish(new RegionGuideCompletedSignal(_step));
    }

    private static bool TryGetGuideIndex(MapType _mapType, out int _index, out TutorialStep _step)
    {
        switch (_mapType)
        {
            case MapType.FluffySporeForest:
                _index = SporeForestIndex;
                _step = TutorialStep.SporeForestGuide;
                return true;
            case MapType.StarrootForest:
                _index = StarrootForestIndex;
                _step = TutorialStep.StarrootForestGuide;
                return true;
            case MapType.MagmaForest:
                _index = MagmaForestIndex;
                _step = TutorialStep.MagmaForestGuide;
                return true;
            default:
                _index = -1;
                _step = TutorialStep.CutTree;
                return false;
        }
    }
}
