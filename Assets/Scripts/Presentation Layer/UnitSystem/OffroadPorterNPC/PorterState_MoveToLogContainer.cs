using UnityEngine;

/// <summary>
/// LogContainer(상점 보관함)를 향해 길찾기로 이동하다가, 실제 충돌 반경(collider) 안에 들어오는
/// 순간 바로 그 자리에서 납품하는 상태.
///
/// LogContainer는 LogCutter/LogEvaluator가 계속 소비해서 시간이 지나면 자리가 다시 생기므로
/// (한 번 가득 차면 다음 던전 전까지 절대 안 비는 오프로드 컨테이너와 다름), 가득 찬 경우 벌목 NPC처럼
/// 영구히 멈추지 않고 그 자리에서 주기적으로 납품을 재시도한다.
/// </summary>
public class PorterState_MoveToLogContainer : PorterState
{
    private const float RETRY_INTERVAL = 1.0f;

    private int pathIndex = 0;
    private bool bIsDepositing = false;
    private bool bWaitingForSpace = false;
    private float retryTimer = 0f;

    public override void Enter()
    {
        base.Enter();
        pathIndex = 0;
        bIsDepositing = false;
        bWaitingForSpace = false;
        retryTimer = 0f;
        npc.SetVisualMoving(true);

        if (npc.logContainer == null)
        {
            stateMachine.ChangeState<PorterState_Idle>();
            return;
        }

        if (npc.logContainer.IsWithinInteractRadius(npc.transform.position))
        {
            DepositAndReturn();
            return;
        }

        bool found = npc.FindPathNear(npc.logContainer.transform.position);
        if (!found)
        {
            stateMachine.ChangeState<PorterState_Idle>();
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

        if (bIsDepositing) return;

        if (npc.logContainer == null)
        {
            stateMachine.ChangeState<PorterState_Idle>();
            return;
        }

        if (bWaitingForSpace)
        {
            retryTimer += Time.deltaTime;
            if (retryTimer >= RETRY_INTERVAL)
            {
                retryTimer = 0f;
                DepositAndReturn();
            }
            return;
        }

        if (npc.logContainer.IsWithinInteractRadius(npc.transform.position))
        {
            DepositAndReturn();
            return;
        }

        if (StepAlongPath(ref pathIndex))
        {
            DepositAndReturn();
        }
    }

    private void DepositAndReturn()
    {
        if (bIsDepositing) return;

        bIsDepositing = true;
        npc.SetVisualMoving(false);
        npc.DepositToLogContainer(() =>
        {
            bIsDepositing = false;

            if (npc.inventory != null && !npc.inventory.bInventoryIsEmpty)
            {
                // 상자가 가득 차 미처 납품하지 못한 로그가 남아있다 - 이 자리에서 재시도 대기
                bWaitingForSpace = true;
                retryTimer = 0f;
                return;
            }

            bWaitingForSpace = false;
            stateMachine.ChangeState<PorterState_Idle>();
        });
    }
}
