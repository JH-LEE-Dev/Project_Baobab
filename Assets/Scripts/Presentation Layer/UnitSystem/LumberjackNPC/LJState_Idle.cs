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
            
            if (npc.TryFindTree())
            {
                // 나무 찾음!
                stateMachine.ChangeState<LJState_Move>();
            }
        }
    }
}
