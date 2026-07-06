using UnityEngine;

/// <summary>
/// 인벤토리가 가득 찬 럼버잭 NPC가 오프로드 컨테이너를 향해 길찾기로 이동하다가,
/// 컨테이너의 실제 충돌 반경(collider) 안에 들어오는 순간 바로 그 자리에서 납품하는 상태.
/// 목표 좌표는 컨테이너 중심 하나뿐이지만, 각 NPC가 서로 다른 시작 위치에서 서로 다른 경로로
/// 접근하기 때문에 반경에 걸리는 타일도 자연히 제각각이라 여러 NPC가 한 점에 겹치지 않는다.
/// </summary>
public class LJState_Deliver : LumberjackState
{
    private int pathIndex = 0;

    private bool bIsDepositing = false;
    private float depositTimer = 0f;
    private const float DEPOSIT_TIMEOUT = 8f;

    // DepositAndReturn을 호출할 때마다 증가하는 시도 번호. OffroadContainer 쪽 코루틴이 어떤
    // 이유로든 끝까지 돌지 않아 완료 콜백이 영영 안 오는 경우, 타임아웃으로 이 시도를 포기하고
    // 번호를 다시 증가시켜 둔다. 그 뒤에 원래 콜백이 뒤늦게 와도 자신이 발급받은 번호가 최신이
    // 아님을 확인하고 조용히 무시하게 만들어, 이미 다른 상태로 넘어간 뒤에(혹은 오브젝트 풀링으로
    // 재사용된 뒤에) 상태를 잘못 건드리는 것을 막는다.
    private int depositAttemptId = 0;

    // 납품을 시도했는데 단 하나도 넣지 못한 경우를 나타낸다(자리가 없거나, 들고 있는 로그의
    // 나무종류+등급이 상자 슬롯들과 안 맞아서 중첩이 안 되는 경우 등). 이 경우 이번 던전 안에서는
    // 더 이상 방법이 없다고 보고 재시도 없이 이 자리에 멈춘다. 하나라도 넣는 데 성공했다면
    // 남은 게 있어도 다시 벌목하러 돌아간다.
    private bool bPermanentlyStuck = false;

    public override void Enter()
    {
        base.Enter();
        pathIndex = 0;
        bIsDepositing = false;
        depositTimer = 0f;
        depositAttemptId++; // 풀링으로 재사용된 경우 이전 생애의 늦은 콜백을 무효화
        bPermanentlyStuck = false;
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

        if (bIsDepositing)
        {
            // 방어 코드: OffroadContainer 쪽 코루틴이 중간에 멈추면(예: 도중에 GameObject
            // 비활성화) 완료 콜백이 영영 안 올 수 있다. 그러면 bIsDepositing이 계속 true로 남아
            // 이 NPC가 bPermanentlyStuck 체크도 못 해보고 완전히 얼어붙으므로, 일정 시간 넘게
            // 완료되지 않으면 이 시도를 포기하고 Idle로 되돌려 다시 시도할 기회를 준다.
            depositTimer += Time.deltaTime;
            if (depositTimer > DEPOSIT_TIMEOUT)
            {
                Debug.LogWarning($"[LJState_Deliver] 납품 완료 콜백이 {DEPOSIT_TIMEOUT}초 넘게 오지 않아 강제로 재시도합니다. NPC={npc.name}");
                depositAttemptId++;
                bIsDepositing = false;
                depositTimer = 0f;
                stateMachine.ChangeState<LJState_Idle>();
            }
            return;
        }

        if (npc.offroadContainer == null)
        {
            stateMachine.ChangeState<LJState_Idle>();
            return;
        }

        // 단 하나도 못 넣어서 영구 정지한 상태 - 다음 던전 전까지 상황이 안 바뀌므로 재시도하지 않는다.
        if (bPermanentlyStuck) return;

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
        if (bIsDepositing) return;

        bIsDepositing = true;
        depositTimer = 0f;
        int myAttemptId = ++depositAttemptId;
        npc.SetVisualMoving(false); // 납품 중에는 멈춰 서 있는다

        // 납품 전/후 인벤토리 총량을 비교하지 않는다 - 납품 도중/직후에 흡입 중이던 다른 로그가
        // 뒤늦게 착지해 총량이 바뀌면(문제 3) "하나라도 넣었는지"가 총량 비교만으로는 잘못 판정될 수
        // 있다. 대신 실제로 넣은 개수를 납품 루틴 내부에서 직접 세어 그 결과만 그대로 사용한다.
        npc.DepositInventoryToOffroad((bool _wasDelivered) =>
        {
            // 타임아웃으로 이 시도를 이미 포기한 뒤에 뒤늦게 콜백이 오면 무시한다 - 그 사이 다른
            // 상태로 넘어갔거나(재시도 중 다시 Deliver에 들어와 새 시도가 진행 중일 수 있음),
            // 오브젝트가 풀링으로 재사용되어 완전히 다른 NPC를 대변하고 있을 수도 있다.
            if (myAttemptId != depositAttemptId) return;

            bIsDepositing = false;

            // 하나라도 넣는 데 성공했다면 남은 게 있어도 다시 벌목하러 간다.
            if (_wasDelivered)
            {
                stateMachine.ChangeState<LJState_Idle>();
                return;
            }

            // 단 하나도 넣지 못했다 - 재시도 없이 이 자리에 영구 정지한다.
            bPermanentlyStuck = true;
        });
    }
}
