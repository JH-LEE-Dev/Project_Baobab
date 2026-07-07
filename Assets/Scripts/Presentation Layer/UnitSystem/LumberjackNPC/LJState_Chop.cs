using UnityEngine;

public class LJState_Chop : LumberjackState
{
    private float chopTimer = 0f;

    public override void Enter()
    {
        base.Enter();
        chopTimer = npc.stat.attackInterval; // 입장하자마자 1회 타격하기 위해 타이머 꽉 채움
        npc.SetVisualMoving(false);

        if (npc.targetTree == null || !npc.targetTree.GetTransform().gameObject.activeInHierarchy)
        {
            // TEMP DEBUG
            LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={npc.name}({npc.GetEntityId()}) LJState_Chop.Enter: 타겟 나무가 이미 null/비활성 -> Idle로 복귀");
            npc.ReleaseTargetTree();
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        // TEMP DEBUG: 실제 나무와의 거리가 비정상적으로 멀면(=풀링 재사용된 다른 나무일 가능성) 경고
        float distToTree = Vector2.Distance(npc.transform.position, npc.targetTree.GetTransform().position);
        if (distToTree > 3f)
        {
            LJDebugLog.LogWarning($"[LJDebug] t={Time.time:F2} npc={npc.name}({npc.GetEntityId()}) LJState_Chop.Enter: 타겟 나무와의 거리가 비정상적으로 멉니다! dist={distToTree:F2}, npcPos={npc.transform.position}, treePos={npc.targetTree.GetTransform().position}, tree={npc.targetTree.GetTransform().name}");
        }
        else
        {
            LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={npc.name}({npc.GetEntityId()}) LJState_Chop.Enter: dist={distToTree:F2}");
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
            // TEMP DEBUG
            LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={npc.name}({npc.GetEntityId()}) LJState_Chop.Update: 타겟 나무 죽음/비활성 감지 -> Idle로 복귀");
            npc.ReleaseTargetTree();
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        chopTimer += Time.deltaTime;
        if (chopTimer >= npc.stat.attackInterval)
        {
            chopTimer = 0f;
            npc.SwingAxe();
        }
    }
}
