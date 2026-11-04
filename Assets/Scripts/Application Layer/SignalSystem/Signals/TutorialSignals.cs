public enum TutorialStep
{
    CutTree,
    FillOffroadContainer,
    // "탈진하기 전에 집으로 돌아가세요" - OffroadContainer에 원목을 다 넣으면 시작된다.
    GoHomeBeforeExhausted,
    // 마을로 복귀 후, 인벤토리의 아이템을 LogContainer에 넣으라는 퀘스트
    PutItemsInLogContainer,
    ReceiveMoney,
    UpgradeAxe,
    // 마지막 스텝 - "새로운 벌목을 시작하세요". 도끼 강화가 끝나면 시작되고,
    // 마을에서 OffroadVehicle에 상호작용하면(다시 던전으로 향하면) 완료된다.
    StartNewLogging,

    // 대지역 기믹 안내(RegionGuideSystem) - 2·3·4 대지역 첫 입장 시 Quest UI로 기믹을 알려준다.
    // 튜토리얼 스텝이 아니지만 UI_TutorialQuest의 연출 파이프라인을 그대로 쓰기 위해 같은 enum에 둔다.
    // 시작/완료는 TutorialStep*Signal이 아닌 RegionGuide*Signal로만 발행된다.
    SporeForestGuide,
    StarrootForestGuide,
    MagmaForestGuide,
}

// MainMenu → Dungeon 튜토리얼: 인트로 연출(로고 → 하차 → HUD 복귀)이 전부 끝난 시점.
// TutorialSystem이 이 신호를 받아 첫 번째 튜토리얼 스텝을 시작한다.
public struct TutorialIntroEndedSignal { }

// 특정 튜토리얼 스텝이 시작됨 - UI는 이 신호를 받아 해당 스텝의 안내 UI를 띄운다.
public struct TutorialStepStartedSignal
{
    public TutorialStep step;
    public TutorialStepStartedSignal(TutorialStep _step)
    {
        step = _step;
    }
}

// 특정 튜토리얼 스텝이 완료됨 - UI는 이 신호를 받아 해당 스텝의 안내 UI를 내린다.
public struct TutorialStepCompletedSignal
{
    public TutorialStep step;
    public TutorialStepCompletedSignal(TutorialStep _step)
    {
        step = _step;
    }
}

// 튜토리얼 전용: 피로도가 19% 바닥값에 도달했을 때 발생하는 신호
public struct TutorialStaminaReachedFloorSignal { }

public struct TutorialQuestHideCompletedSignal
{
    public TutorialStep step;
    public TutorialQuestHideCompletedSignal(TutorialStep _step)
    {
        step = _step;
    }
}

// 대지역 기믹 안내가 시작됨 - UI는 이 신호를 받아 해당 안내 Quest UI를 띄운다.
public struct RegionGuideStartedSignal
{
    public TutorialStep step;
    public RegionGuideStartedSignal(TutorialStep _step)
    {
        step = _step;
    }
}

// 대지역 기믹 안내의 클리어 조건이 충족됨 - UI는 이 신호를 받아 완료 연출 후 안내를 내린다.
public struct RegionGuideCompletedSignal
{
    public TutorialStep step;
    public RegionGuideCompletedSignal(TutorialStep _step)
    {
        step = _step;
    }
}

public struct TutorialQuestTransitionCompletedSignal
{
    public TutorialStep step;
    public TutorialQuestTransitionCompletedSignal(TutorialStep _step)
    {
        step = _step;
    }
}
