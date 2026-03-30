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

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, true);
        currentPathIndex = 0;
        hasNextReservation = false;
        stuckTimer = 0f;
        stuckThreshold = Random.Range(0.15f, 0.35f);

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
            animal.bArrived = true;
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
                    Vector3 newDest = GetNewDestinationNearCenter();
                    if (!pathFindComponent.FindPath(animal.transform.position, newDest))
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
        else
        {
            // 1-1. 대각선 이동 중 실시간 장애물(나무 등) 감지
            Vector3Int diff = nextReservedPos - currentReservedPos;
            if (diff.x != 0 && diff.y != 0) // 대각선 이동 중인가?
            {
                // 직교 방향 양 옆 타일이 여전히 이동 가능한지 체크
                bool side1 = pathFindComponent.IsWalkable(new Vector3Int(currentReservedPos.x + diff.x, currentReservedPos.y, 0));
                bool side2 = pathFindComponent.IsWalkable(new Vector3Int(currentReservedPos.x, currentReservedPos.y + diff.y, 0));

                if (!side1 || !side2) // 옆에 나무가 자랐다면!
                {
                    animal.rb.linearVelocity = Vector2.zero;
                    animal.anim.SetBool(animal.isMovingHash, false);
                    stuckTimer += Time.deltaTime;

                    if (stuckTimer >= stuckThreshold)
                    {
                        // 경로가 막혔으므로 재탐색
                        Vector3 newDest = GetNewDestinationNearCenter();
                        if (!pathFindComponent.FindPath(animal.transform.position, newDest))
                        {
                            stateMachine.ChangeState<AS_IdleState>();
                        }
                        else
                        {
                            currentPathIndex = 0;
                            stuckTimer = 0f;
                            // 현재 타일만 남기고 다음 예약 해제하여 다시 처음부터 예약하게 함
                            pathFindComponent.Release(nextReservedPos);
                            hasNextReservation = false;
                        }
                    }
                    return;
                }
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

            // 다음 목표 타일이 유효한지 미리 체크 (나무 등이 자랐을 경우 대비)
            if (currentPathIndex < path.Count)
            {
                Vector3Int nextCell = pathFindComponent.WorldToCell(path[currentPathIndex]);
                if (!pathFindComponent.IsWalkable(nextCell))
                {
                    Vector3 newDest = GetNewDestinationNearCenter();
                    if (!pathFindComponent.FindPath(animal.transform.position, newDest))
                    {
                        stateMachine.ChangeState<AS_IdleState>();
                    }
                    else
                    {
                        currentPathIndex = 0;
                    }
                }
            }
        }
    }

    private Vector3 GetNewDestinationNearCenter()
    {
        Vector3Int centerCell = pathFindComponent.WorldToCell(animal.centerPos);
        int radius = Mathf.RoundToInt(animal.scatterRadius);

        // 무작위 순서로 주변 타일 탐색하여 하나 선택
        for (int i = 0; i < 20; i++) // 최대 20번 시도
        {
            int rx = Random.Range(-radius, radius + 1);
            int ry = Random.Range(-radius, radius + 1);
            Vector3Int candidate = new Vector3Int(centerCell.x + rx, centerCell.y + ry, 0);

            if (pathFindComponent.IsWalkable(candidate) && !pathFindComponent.IsOccupied(candidate))
            {
                return pathFindComponent.CellToWorld(candidate);
            }
        }

        return animal.targetPos; // 실패 시 원래 목적지 반환
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
