using UnityEngine;

public class AS_IdleState : AnimalState
{
    private Vector3Int currentOccupiedPos;
    private float idleTimer;
    private float nextMoveTime;
    private bool isFleeing; // 도망 시도 여부 플래그

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, false);
        animal.rb.linearVelocity = Vector2.zero;

        // 현재 위치 타일 점유
        currentOccupiedPos = pathFindComponent.WorldToCell(animal.transform.position);
        pathFindComponent.Occupy(currentOccupiedPos);

        idleTimer = 0f;
        nextMoveTime = Random.Range(2f, 5f); // 2~5초 사이 무작위 대기
        isFleeing = false;
    }

    public override void Exit()
    {
        bActivated = false;
        // 상태 탈출 시 점유 해제
        pathFindComponent.Release(currentOccupiedPos);
    }

    public override void Update()
    {
        if (!bActivated) return;

        Vector3 currentPos = animal.transform.position;

        // 플레이어 감지 시 한 번만 도망 시도
        if (animal.bRunAway && !isFleeing)
        {
            if (!PathfindManager.CanRequest()) return;

            isFleeing = true;
            TryFlee(currentPos);
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer >= nextMoveTime)
        {
            if (!PathfindManager.CanRequest()) return;
            TryStartMoving(currentPos);
        }
    }

    private void TryFlee(Vector3 _currentPos)
    {
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

                // 수동 회전 연산
                Vector3 rotatedDir = new Vector3(
                    fleeDir.x * cos - fleeDir.y * sin,
                    fleeDir.x * sin + fleeDir.y * cos,
                    0
                );

                Vector3 fleeDest = _currentPos + rotatedDir * dist;
                Vector3Int targetCell = pathFindComponent.WorldToCell(fleeDest);

                // 사전 검사
                if (!pathFindComponent.IsWalkable(targetCell) || pathFindComponent.IsOccupied(targetCell))
                    continue;

                if (pathFindComponent.FindPath(_currentPos, fleeDest))
                {
                    stateMachine.ChangeState<AS_RunState>();
                    return;
                }
            }
        }

        // 2. 모든 도망 경로 탐색 실패 시 무작위 배회 시도
        if (!TryStartMoving(_currentPos))
        {
            // 완전히 고립된 경우 다음 프레임에 다시 시도할 수 있도록 플래그 초기화
            isFleeing = false;
            idleTimer = 0f;
            nextMoveTime = 0.2f; // 짧은 시간 후 재시도
        }
    }

    private bool TryStartMoving(Vector3 _currentPos)
    {
        Vector3 randomDest = GetRandomWalkablePos(_currentPos);
        
        // 현재 위치와 같으면 길찾기 생략
        if ((randomDest - _currentPos).sqrMagnitude < 0.01f)
        {
            idleTimer = 0f;
            nextMoveTime = Random.Range(1f, 3f);
            return false;
        }

        if (pathFindComponent.FindPath(_currentPos, randomDest))
        {
            stateMachine.ChangeState<AS_RunState>();
            return true;
        }
        else
        {
            // 길 찾기 실패 시 대기 시간 초기화 후 다시 시도
            idleTimer = 0f;
            nextMoveTime = Random.Range(1f, 3f);
            return false;
        }
    }

    private Vector3 GetRandomWalkablePos(Vector3 _currentPos)
    {
        Vector3Int currentCell = pathFindComponent.WorldToCell(_currentPos);
        
        // 주변 5~10칸 내외의 무작위 지점 탐색
        for (int i = 0; i < 20; i++) // 30회 -> 20회로 단축
        {
            int rx = Random.Range(-8, 9);
            int ry = Random.Range(-8, 9);
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
        if (!bActivated) return;

        Vector2 velocity = animal.rb.linearVelocity;
        float sqrSpeed = velocity.sqrMagnitude;

        // 감속 로직
        if (sqrSpeed > 0.001f)
        {
            animal.rb.linearVelocity = Vector2.MoveTowards(
                velocity,
                Vector2.zero,
                animal.currentGroundData.deceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            // 속도가 거의 없으면 물리 속도 고정 및 픽셀 스냅
            animal.rb.linearVelocity = Vector2.zero;
            SnapToPixel();
        }
    }

    private void SnapToPixel()
    {
        // 전역 픽셀 스냅 유틸리티 사용
        GlobalPixelSnapper.SnapRigidbody(animal.rb, Time.fixedDeltaTime);
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
