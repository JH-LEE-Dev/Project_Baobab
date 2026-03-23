using UnityEngine;

public class AS_RunState : AnimalState
{
    private int currentPathIndex;
    private const float stopDistance = 0.15f;
    private const float moveSpeed = 2f;
    
    private Vector3Int currentReservedPos; // 현재 내가 서있는 타일
    private Vector3Int nextReservedPos;    // 내가 이동하기 위해 선점한 다음 타일
    private bool hasNextReservation;

    private float stuckTimer;
    private float stuckThreshold;
    private Vector3 finalDestination;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, true);
        currentPathIndex = 0;
        hasNextReservation = false;
        stuckTimer = 0f;
        stuckThreshold = Random.Range(0.15f, 0.35f);

        var path = pathFindComponent.Path;
        if (path != null && path.Count > 0)
            finalDestination = path[path.Count - 1];

        // 시작 지점 확실히 점유
        currentReservedPos = pathFindComponent.WorldToCell(animal.transform.position);
        pathFindComponent.Occupy(currentReservedPos);
    }

    public override void Exit()
    {
        bActivated = false;
        
        // 상태 탈출 시 모든 점유 해제
        pathFindComponent.Release(currentReservedPos);
        if (hasNextReservation)
            pathFindComponent.Release(nextReservedPos);
    }

    public override void Update()
    {
        if (!bActivated) return;

        var path = pathFindComponent.Path;
        if (path == null || path.Count == 0 || currentPathIndex >= path.Count)
        {
            stateMachine.ChangeState<AS_IdleState>();
            return;
        }

        Vector3Int targetCell = pathFindComponent.WorldToCell(path[currentPathIndex]);

        // 1. 다음 타일 예약 시도 (아직 예약하지 않았을 때만)
        if (!hasNextReservation)
        {
            if (pathFindComponent.Occupy(targetCell))
            {
                nextReservedPos = targetCell;
                hasNextReservation = true;
                stuckTimer = 0f; // 예약 성공 시 타이머 리셋
                animal.anim.SetBool(animal.isMovingHash, true);
            }
            else
            {
                // 예약 실패 (누군가 이미 점유 중)
                animal.rb.linearVelocity = Vector2.zero;
                animal.anim.SetBool(animal.isMovingHash, false);
                stuckTimer += Time.deltaTime;

                if (stuckTimer >= stuckThreshold)
                {
                    // 데드락 예방: 길을 다시 찾거나 양보를 위해 Idle로 전환
                    // 재탐색 시 현재 점유된 타일들을 자동으로 피해감
                    if (!pathFindComponent.FindPath(animal.transform.position, finalDestination))
                    {
                        stateMachine.ChangeState<AS_IdleState>();
                    }
                    else
                    {
                        currentPathIndex = 0;
                        stuckTimer = 0f;
                    }
                }
                return;
            }
        }

        // 2. 이동 및 도착 판정
        if (Vector2.Distance(animal.transform.position, path[currentPathIndex]) < stopDistance)
        {
            // 도착 시점: 이전 타일 해제 및 현재 타일 갱신
            pathFindComponent.Release(currentReservedPos);
            currentReservedPos = nextReservedPos;
            hasNextReservation = false; // 다음 타일을 위한 예약 공간 비움
            
            currentPathIndex++;
        }
    }

    public override void FixedUpdate()
    {
        if (!bActivated || !hasNextReservation) return;

        var path = pathFindComponent.Path;
        if (currentPathIndex >= path.Count) return;

        Vector2 targetPos = path[currentPathIndex];
        Vector2 currentPos = animal.transform.position;
        Vector2 direction = (targetPos - currentPos).normalized;

         animal.rb.linearVelocity = Vector2.MoveTowards(
            animal.rb.linearVelocity,
            direction * moveSpeed,
            animal.currentGroundData.deceleration * Time.fixedDeltaTime
        );

        animal.SetFacingDirection(direction);
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
