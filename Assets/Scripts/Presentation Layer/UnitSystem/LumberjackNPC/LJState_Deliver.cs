/// <summary>
/// 인벤토리가 가득 찬 럼버잭 NPC가 오프로드 컨테이너를 향해 길찾기로 이동하다가,
/// 컨테이너의 실제 충돌 반경(collider) 안에 들어오는 순간 바로 그 자리에서 납품하는 상태.
/// 목표 좌표는 컨테이너 중심 하나뿐이지만, 각 NPC가 서로 다른 시작 위치에서 서로 다른 경로로
/// 접근하기 때문에 반경에 걸리는 타일도 자연히 제각각이라 여러 NPC가 한 점에 겹치지 않는다.
/// </summary>
public class LJState_Deliver : LumberjackState
{
    private int pathIndex = 0;

    public override void Enter()
    {
        base.Enter();
        pathIndex = 0;
        npc.SetVisualMoving(true);

        if (npc.offroadContainer == null)
        {
            // 컨테이너가 아직 없다면(오프로드 차량 미스폰 등) 안전하게 대기 상태로 복귀
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        // 이미 반경 안에 서 있는 상태로 진입했다면 굳이 이동하지 않고 바로 납품
        if (npc.offroadContainer.IsWithinInteractRadius(npc.transform.position))
        {
            DepositAndReturn();
            return;
        }

        // 컨테이너 발밑 타일은 길찾기 이동 불가 타일로 등록돼 있으므로, 그 주변에서 가장 가까운
        // 걸을 수 있는 타일까지의 경로를 찾는다 (FindPathTo였다면 목표 타일 자체가 막혀 있어 항상 실패한다)
        bool found = npc.FindPathNear(npc.offroadContainer.transform.position);
        if (!found)
        {
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        if (npc.currentPath == null || npc.currentPath.Count == 0)
        {
            DepositAndReturn();
        }
    }

    public override void Update()
    {
        base.Update();

        if (npc.offroadContainer == null)
        {
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        // 경로를 따라가는 중 컨테이너의 충돌 반경에 들어오면 남은 경로와 무관하게 즉시 납품
        if (npc.offroadContainer.IsWithinInteractRadius(npc.transform.position))
        {
            DepositAndReturn();
            return;
        }

        if (StepAlongPath(ref pathIndex))
        {
            // 반경엔 못 들어갔지만 목적지(컨테이너 좌표)까지의 경로는 다 걸었다 - 그래도 여기서 납품 시도
            DepositAndReturn();
        }
    }

    private void DepositAndReturn()
    {
        npc.DepositInventoryToOffroad();
        stateMachine.ChangeState<LJState_Idle>();
    }
}
