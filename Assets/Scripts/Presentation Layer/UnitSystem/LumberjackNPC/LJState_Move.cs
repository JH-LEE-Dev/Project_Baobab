using UnityEngine;
using System.Collections.Generic;

public class LJState_Move : LumberjackState
{
    private int pathIndex = 0;
    private const float WAYPOINT_TOLERANCE = 0.05f;

    public override void Enter()
    {
        base.Enter();
        pathIndex = 0;
        npc.SetVisualMoving(true);
        
        if (npc.currentPath == null || npc.currentPath.Count == 0)
        {
            stateMachine.ChangeState<LJState_Idle>();
        }
    }

    public override void Update()
    {
        base.Update();

        if (npc.currentPath == null || pathIndex >= npc.currentPath.Count)
        {
            // 경로의 끝에 도달 = 나무 바로 옆
            stateMachine.ChangeState<LJState_Chop>();
            return;
        }

        Vector3 currentPos = npc.transform.position;
        Vector3 targetPos = npc.currentPath[pathIndex];
        
        // y 오프셋 등 높이 차이 무시 (2D 평면 기준)
        targetPos.z = currentPos.z;

        float distance = Vector2.Distance(currentPos, targetPos);

        if (distance <= WAYPOINT_TOLERANCE)
        {
            pathIndex++;
        }
        else
        {
            Vector2 direction = ((Vector2)targetPos - (Vector2)currentPos).normalized;
            npc.transform.position = Vector3.MoveTowards(currentPos, targetPos, npc.moveSpeed * Time.deltaTime);
            
            // 이동 방향 시각적 업데이트
            npc.SetVisualFacing(direction);
            npc.SetArmDirection(direction);
        }
    }
}
