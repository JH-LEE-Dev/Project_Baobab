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

public struct TutorialQuestTransitionCompletedSignal
{
    public TutorialStep step;
    public TutorialQuestTransitionCompletedSignal(TutorialStep _step)
    {
        step = _step;
    }
}
