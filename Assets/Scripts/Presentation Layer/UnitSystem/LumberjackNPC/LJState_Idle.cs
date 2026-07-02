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
