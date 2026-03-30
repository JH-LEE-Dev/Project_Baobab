using UnityEngine;

public class AS_IdleState : AnimalState
{
    private Vector3Int currentOccupiedPos;
    private float rePathTimer;
    private float rePathInterval;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, false);

        // PathFindComponent를 통해 현재 위치 점유
        currentOccupiedPos = pathFindComponent.WorldToCell(animal.transform.position);
        pathFindComponent.Occupy(currentOccupiedPos);

        // 만약 도착하지 않은 상태에서 Idle로 왔다면 (장애물이나 데드락 등)
        // 잠시 대기 후 다시 길을 찾기 위해 타이머 설정
        if (!animal.bArrived)
        {
            rePathTimer = 0f;
            rePathInterval = Random.Range(0.5f, 1.5f);
        }
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

        // 도착하지 않은 경우 주기적으로 재탐색 시도
        if (!animal.bArrived)
        {
            rePathTimer += Time.deltaTime;
            if (rePathTimer >= rePathInterval)
            {
                rePathTimer = 0f;
                Vector3 newDest = GetNewDestinationNearCenter();
                
                // 새로운 경로를 찾았다면 다시 RunState로 전환
                if (pathFindComponent.FindPath(animal.transform.position, newDest))
                {
                    stateMachine.ChangeState<AS_RunState>();
                }
                else
                {
                    // 실패 시 대기 시간 갱신 후 다음 프레임들에서 재시도
                    rePathInterval = Random.Range(0.5f, 1.5f);
                }
            }
        }
    }

    private Vector3 GetNewDestinationNearCenter()
    {
        Vector3Int centerCell = pathFindComponent.WorldToCell(animal.centerPos);
        int radius = Mathf.RoundToInt(animal.scatterRadius);

        // 무작위 순서로 주변 타일 탐색하여 하나 선택
        for (int i = 0; i < 10; i++) // 최대 10번 시도
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
        if (!bActivated) return;

        animal.rb.linearVelocity = Vector2.MoveTowards(
            animal.rb.linearVelocity,
            Vector2.zero,
            animal.currentGroundData.deceleration * Time.fixedDeltaTime
        );
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
