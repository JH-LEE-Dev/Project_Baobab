using UnityEngine;

public class LJState_Idle : LumberjackState
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

        // 스폰 직후 일정 시간 동안은 나무를 찾지 않고 가만히 대기
        if (!npc.IsSpawnDelayFinished) return;

        searchTimer += Time.deltaTime;
        if (searchTimer >= SEARCH_INTERVAL)
        {
            searchTimer = 0f;

            // 인벤토리가 가득 찼다면 나무를 더 찾지 않고 오프로드 컨테이너로 납품하러 감
            // (1초 간격 재시도라 컨테이너에 도달 불가능해도 매 프레임 재귀호출 없이 안전하게 반복됨)
            if (npc.inventory != null && npc.inventory.bInventoryIsFull && npc.offroadContainer != null)
            {
                // TEMP DEBUG
                LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={npc.name}({npc.GetEntityId()}) LJState_Idle: 인벤토리 가득 참 감지 -> Deliver로 전환 시도");
                stateMachine.ChangeState<LJState_Deliver>();
                return;
            }

            if (npc.TryFindTree())
            {
                // 나무 찾음!
                stateMachine.ChangeState<LJState_Move>();
            }
            else
            {
                // TEMP DEBUG
                int invCount = npc.inventory != null ? npc.inventory.GetTotalItemCount() : -1;
                LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={npc.name}({npc.GetEntityId()}) LJState_Idle: TryFindTree 실패 (나무 없음). 인벤토리={invCount}");
            }
        }
    }
}
