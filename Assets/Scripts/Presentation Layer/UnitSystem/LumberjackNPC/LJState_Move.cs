using UnityEngine;

public class LJState_Move : LumberjackState
{
    private int pathIndex = 0;
    private float treeCheckTimer = 0f;
    private const float TREE_CHECK_INTERVAL = 0.5f;

    public override void Enter()
    {
        base.Enter();
        pathIndex = 0;
        treeCheckTimer = 0f;
        npc.SetVisualMoving(true);

        if (npc.currentPath == null || npc.currentPath.Count == 0)
        {
            // 빈 경로 = 이미 타겟 나무 바로 옆에 있다는 뜻 (PathFindComponent.FindNearestTreePath 참고)
            if (npc.targetTree != null)
            {
                stateMachine.ChangeState<LJState_Chop>();
            }
            else
            {
                stateMachine.ChangeState<LJState_Idle>();
            }
        }
    }

    public override void Update()
    {
        base.Update();

        // 0.5초마다 목표 나무 생존 여부 확인
        treeCheckTimer += Time.deltaTime;
        if (treeCheckTimer >= TREE_CHECK_INTERVAL)
        {
            treeCheckTimer = 0f;
            if (npc.targetTree == null || !npc.targetTree.GetTransform().gameObject.activeInHierarchy || npc.targetTree.bDead)
            {
                npc.ReleaseTargetTree();
                stateMachine.ChangeState<LJState_Idle>();
                return;
            }
        }

        if (StepAlongPath(ref pathIndex))
        {
            // 경로의 끝에 도달 = 나무 바로 옆
            stateMachine.ChangeState<LJState_Chop>();
        }
    }
}
