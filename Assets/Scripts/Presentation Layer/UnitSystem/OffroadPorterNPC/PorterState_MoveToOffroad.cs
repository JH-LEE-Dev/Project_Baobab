/// <summary>
/// 오프로드 컨테이너를 향해 길찾기로 이동하다가, 컨테이너의 실제 충돌 반경(collider) 안에 들어오는
/// 순간 바로 그 자리에서 로그를 수령하는 상태. (LJState_Deliver와 대칭되는 구조)
/// </summary>
public class PorterState_MoveToOffroad : PorterState
{
    private int pathIndex = 0;
    private bool bIsWithdrawing = false;

    public override void Enter()
    {
        base.Enter();
        pathIndex = 0;
        bIsWithdrawing = false;
        npc.SetVisualMoving(true);

        if (npc.offroadContainer == null)
        {
            stateMachine.ChangeState<PorterState_Idle>();
            return;
        }

        if (npc.offroadContainer.IsWithinInteractRadius(npc.transform.position))
        {
            WithdrawAndProceed();
            return;
        }

        // 컨테이너 발밑 타일은 길찾기 이동 불가 타일로 등록돼 있으므로, 그 주변에서 가장 가까운
        // 걸을 수 있는 타일까지의 경로를 찾는다.
        bool found = npc.FindPathNear(npc.offroadContainer.transform.position);
        if (!found)
        {
            stateMachine.ChangeState<PorterState_Idle>();
            return;
        }

        if (npc.currentPath == null || npc.currentPath.Count == 0)
        {
            WithdrawAndProceed();
        }
    }

    public override void Update()
    {
        base.Update();

        if (bIsWithdrawing) return;

        if (npc.offroadContainer == null)
        {
            stateMachine.ChangeState<PorterState_Idle>();
            return;
        }

        if (npc.offroadContainer.IsWithinInteractRadius(npc.transform.position))
        {
            WithdrawAndProceed();
            return;
        }

        if (StepAlongPath(ref pathIndex))
        {
            WithdrawAndProceed();
        }
    }

    private void WithdrawAndProceed()
    {
        if (bIsWithdrawing) return;

        bIsWithdrawing = true;
        npc.SetVisualMoving(false);
        npc.WithdrawFromOffroad(() =>
        {
            bIsWithdrawing = false;

            if (npc.inventory != null && !npc.inventory.bInventoryIsEmpty)
            {
                stateMachine.ChangeState<PorterState_MoveToLogContainer>();
            }
            else
            {
                // 컨테이너가 이미 비어있어서(다른 NPC가 먼저 가져갔다든지) 하나도 못 받은 경우
                stateMachine.ChangeState<PorterState_Idle>();
            }
        });
    }
}
