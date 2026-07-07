using UnityEngine;

/// <summary>
/// 오프로드 컨테이너를 향해 길찾기로 이동하다가, 컨테이너의 실제 충돌 반경(collider) 안에 들어오는
/// 순간 바로 그 자리에서 로그를 수령하는 상태. (LJState_Deliver와 대칭되는 구조)
/// </summary>
public class PorterState_MoveToOffroad : PorterState
{
    private int pathIndex = 0;
    private bool bIsWithdrawing = false;
    private Coroutine withdrawCoroutine;

    public override void Enter()
    {
        base.Enter();
        pathIndex = 0;
        bIsWithdrawing = false;
        withdrawCoroutine = null;
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
        withdrawCoroutine = npc.WithdrawFromOffroad((_anyWithdrawn) =>
        {
            bIsWithdrawing = false;
            withdrawCoroutine = null;

            // 착지 시점에 커밋되므로, 이 시점엔 방금 발사한 아이템이 아직 인벤토리에 반영되지
            // 않았을 수 있다. bInventoryIsEmpty만 보면 "분명히 받았는데 아직 착지 전이라 비어있다"고
            // 오판해 Idle로 돌아가버리므로, 이번 세션에 하나라도 발사했는지(_anyWithdrawn)도 함께 본다.
            if (_anyWithdrawn || (npc.inventory != null && !npc.inventory.bInventoryIsEmpty))
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

    /// <summary>
    /// 텔레포트 UI가 닫히는 시점에 호출된다(Pause 로직과는 별개). 인출 도중이었다면 코루틴만
    /// 멈춰서 더 가져오려는 시도를 중단시키고, 이미 발사되어 날아오고 있던 아이템은 그대로
    /// 습득되도록 둔다(발사 시점에 데이터가 이미 커밋되는 구조라 안전). 그 뒤 바로 Idle로 전환한다.
    /// </summary>
    public void CancelForTeleport()
    {
        if (bIsWithdrawing && withdrawCoroutine != null)
        {
            npc.offroadContainer?.CancelWithdraw(withdrawCoroutine);
            withdrawCoroutine = null;
            bIsWithdrawing = false;
        }

        stateMachine.ChangeState<PorterState_Idle>();
    }
}
