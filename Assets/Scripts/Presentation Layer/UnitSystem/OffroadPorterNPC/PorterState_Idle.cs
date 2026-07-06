using UnityEngine;

public class PorterState_Idle : PorterState
{
    private float searchTimer = 0f;
    private const float SEARCH_INTERVAL = 1.0f;

    public override void Enter()
    {
        base.Enter();
        searchTimer = 0f;
        npc.SetVisualMoving(false);
    }

    public override void Update()
    {
        base.Update();

        // 스폰 직후 일정 시간 동안은 움직이지 않도록 대기
        if (!npc.IsSpawnDelayFinished) return;

        searchTimer += Time.deltaTime;
        if (searchTimer < SEARCH_INTERVAL) return;

        searchTimer = 0f;

        // 이미 들고 있는 로그가 있다면 먼저 납품하러 간다 (새로 주우러 가지 않음)
        if (npc.inventory != null && !npc.inventory.bInventoryIsEmpty)
        {
            stateMachine.ChangeState<PorterState_MoveToLogContainer>();
            return;
        }

        // 인벤토리가 비어있고, 오프로드 컨테이너에 옮길 로그가 있으며, 실을 자리가 있다면 수령하러 간다
        if (npc.offroadContainer != null && npc.offroadContainer.currentItemCount > 0 &&
            npc.inventory != null && !npc.inventory.bInventoryIsFull)
        {
            stateMachine.ChangeState<PorterState_MoveToOffroad>();
        }
    }
}
