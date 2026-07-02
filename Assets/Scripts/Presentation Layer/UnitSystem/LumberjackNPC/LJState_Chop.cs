using UnityEngine;

public class LJState_Chop : LumberjackState
{
    private float chopTimer = 0f;

    public override void Enter()
    {
        base.Enter();
        chopTimer = npc.chopInterval; // 입장하자마자 1회 타격하기 위해 타이머 꽉 채움
        npc.SetVisualMoving(false);

        if (npc.targetTree == null || !npc.targetTree.GetTransform().gameObject.activeInHierarchy)
        {
            npc.ReleaseTargetTree();
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        // 나무 방향 바라보기
        Vector2 dirToTree = ((Vector2)npc.targetTree.GetTransform().position - (Vector2)npc.transform.position).normalized;
        npc.SetVisualFacing(dirToTree);
        npc.SetArmDirection(dirToTree);
    }

    public override void Update()
    {
        base.Update();

        // 나무가 파괴되었거나 비활성화되었는지 검사
        if (npc.targetTree == null || !npc.targetTree.GetTransform().gameObject.activeInHierarchy)
        {
            npc.ReleaseTargetTree();
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        chopTimer += Time.deltaTime;
        if (chopTimer >= npc.chopInterval)
        {
            chopTimer = 0f;
            npc.SwingAxe();
        }
    }
}
