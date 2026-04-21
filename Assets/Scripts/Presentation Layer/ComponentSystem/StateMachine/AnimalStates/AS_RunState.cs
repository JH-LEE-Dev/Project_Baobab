using UnityEngine;

public class AS_RunState : AnimalState
{
    private int currentPathIndex;
    private const float stopDistance = 0.15f;
    private const float stopDistanceSqr = stopDistance * stopDistance;
    private const float moveSpeed = 2f;

    private Vector3Int currentReservedPos; // 현재 내가 서있는 타일
    private Vector3Int nextReservedPos;    // 내가 이동하기 위해 선점한 다음 타일
    private bool hasNextReservation;

    private float stuckTimer;
    private float stuckThreshold;
    private bool isFleeingPath; // 현재 도망 중인 경로인지 여부

    private Vector3Int cachedTargetCell; // 현재 목표 지점의 셀 좌표 캐시
    private Vector3 cachedTargetWorldPos; // 현재 목표 지점의 월드 좌표 캐시

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, true);
        currentPathIndex = 0;
        hasNextReservation = false;
        stuckTimer = 0f;
        stuckThreshold = Random.Range(0.15f, 0.35f);

        isFleeingPath = animal.bRunAway;

        // 시작 지점 확실히 점유
        Vector3 currentPos = animal.transform.position;
        currentReservedPos = pathFindComponent.WorldToCell(currentPos);
        pathFindComponent.Occupy(currentReservedPos);

        UpdateTargetCache();
    }

    private void UpdateTargetCache()
    {
        var path = pathFindComponent.Path;
        if (path != null && path.Count > 0 && currentPathIndex < path.Count)
        {
            cachedTargetWorldPos = path[currentPathIndex];
            cachedTargetCell = pathFindComponent.WorldToCell(cachedTargetWorldPos);
        }
    }

    public override void Exit()
    {
        bActivated = false;

        // 상태 탈출 시 모든 점유 해제
        pathFindComponent.Release(currentReservedPos);
        if (hasNextReservation)
            pathFindComponent.Release(nextReservedPos);

        animal.anim.SetBool(animal.isMovingHash, false);
        animal.animalAnimValueHandler.RunStartEnd(false);
    }

    public override void Update()
    {
        if (!bActivated) return;

        // 0. 실시간 도망 전환 체크: 일반 이동 중 플레이어가 나타나면 즉시 도망 경로로 갱신
        if (animal.bRunAway && !isFleeingPath)
        {
            if (!PathfindManager.CanRequest()) return;

            isFleeingPath = true;
            HandleStuck();
            return;
        }

        var path = pathFindComponent.Path;

        // 경로가 없거나 끝에 도달했을 때
        if (path == null || path.Count == 0 || currentPathIndex >= path.Count)
        {
            if (animal.bRunAway)
            {
                if (!PathfindManager.CanRequest()) return;
                // 플레이어가 여전히 근처에 있다면 계속해서 멀리 도망
                HandleStuck();
            }
            else
            {
                stateMachine.ChangeState<AS_IdleState>();
            }
            return;
        }

        // 1. 다음 타일 예약 시도 (아직 예약하지 않았을 때만)
        if (!hasNextReservation)
        {
            if (pathFindComponent.Occupy(cachedTargetCell))
            {
                nextReservedPos = cachedTargetCell;
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
                    HandleStuck();
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
                        HandleStuck();
                    }
                    return;
                }
            }
        }

        // 2. 이동 및 도착 판정
        Vector3 currentPos = animal.transform.position;
        if ((currentPos - cachedTargetWorldPos).sqrMagnitude < stopDistanceSqr)
        {
            // 도착 시점: 이전 타일 해제 및 현재 타일 갱신
            pathFindComponent.Release(currentReservedPos);
            currentReservedPos = nextReservedPos;
            hasNextReservation = false; // 다음 타일을 위한 예약 공간 비움

            currentPathIndex++;

            // 다음 목표 타일이 유효한지 미리 체크 (나무 등이 자랐을 경우 대비)
            if (currentPathIndex < path.Count)
            {
                UpdateTargetCache();
                if (!pathFindComponent.IsWalkable(cachedTargetCell))
                {
                    HandleStuck();
                }
            }
        }
    }

    private void HandleStuck()
    {
        Vector3 currentPos = animal.transform.position;
        if (animal.bRunAway)
        {
            isFleeingPath = true;
            Vector3 fleeDir = animal.FleeDirection;
            // 1. 플레이어 반대 방향(FleeDirection)을 기반으로 점진적으로 멀리 탐색 (5칸 ~ 15칸)
            for (int dist = 5; dist <= 15; dist += 3)
            {
                // 약간의 각도 오차(약 -30~30도)를 주어 장애물을 우회할 수 있는 경로를 찾음
                for (int i = 0; i < 3; i++)
                {
                    float randomAngle = Random.Range(-30f, 30f);
                    float rad = randomAngle * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);

                    // Quaternion 연산 대신 수동 회전 (더 가벼움)
                    Vector3 rotatedDir = new Vector3(
                        fleeDir.x * cos - fleeDir.y * sin,
                        fleeDir.x * sin + fleeDir.y * cos,
                        0
                    );

                    Vector3 fleeDest = currentPos + rotatedDir * dist;
                    Vector3Int targetCell = pathFindComponent.WorldToCell(fleeDest);

                    // 사전 검사: 목적지가 유효하지 않거나 이미 점유되어 있으면 A* 시도조차 하지 않음
                    if (!pathFindComponent.IsWalkable(targetCell) || pathFindComponent.IsOccupied(targetCell))
                        continue;

                    if (pathFindComponent.FindPath(currentPos, fleeDest))
                    {
                        ResetPathAndStuckTimer();
                        return;
                    }
                }
            }
        }
        else
        {
            isFleeingPath = false;
            // 일반 이동 중: 주변 무작위 지점 탐색
            Vector3 newDest = GetRandomDestination(currentPos);
            if (pathFindComponent.FindPath(currentPos, newDest))
            {
                ResetPathAndStuckTimer();
                return;
            }
        }

        // 모든 재탐색 시도가 실패하면 Idle 상태로 전환
        stateMachine.ChangeState<AS_IdleState>();
    }

    private void ResetPathAndStuckTimer()
    {
        currentPathIndex = 0;
        stuckTimer = 0f;

        // 기존 예약 초기화하여 다시 점유 시도하게 함
        if (hasNextReservation)
        {
            pathFindComponent.Release(nextReservedPos);
            hasNextReservation = false;
        }

        UpdateTargetCache();
    }

    private Vector3 GetRandomDestination(Vector3 _currentPos)
    {
        Vector3Int currentCell = pathFindComponent.WorldToCell(_currentPos);

        // 주변 10칸 내외의 무작위 지점 탐색
        for (int i = 0; i < 20; i++) // 30회 -> 20회로 단축
        {
            int rx = Random.Range(-10, 11);
            int ry = Random.Range(-10, 11);
            Vector3Int candidate = new Vector3Int(currentCell.x + rx, currentCell.y + ry, 0);

            if (pathFindComponent.IsWalkable(candidate) && !pathFindComponent.IsOccupied(candidate))
            {
                return pathFindComponent.CellToWorld(candidate);
            }
        }

        return _currentPos; // 실패 시 현재 위치
    }

    public override void FixedUpdate()
    {
        if (!bActivated || !hasNextReservation) return;

        var path = pathFindComponent.Path;
        if (currentPathIndex >= path.Count) return;

        Vector2 currentPos = animal.transform.position;
        Vector2 targetPos = (Vector2)cachedTargetWorldPos;
        Vector2 diff = targetPos - currentPos;

        // 거리가 이미 충분히 가까우면 연산 생략
        if (diff.sqrMagnitude < 0.0001f) return;

        Vector2 direction = diff.normalized;

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
