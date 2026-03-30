using UnityEngine;

public class AS_IdleState : AnimalState
{
    private Vector3Int currentOccupiedPos;
    private float idleTimer;
    private float nextMoveTime;

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

        idleTimer += Time.deltaTime;
        if (idleTimer >= nextMoveTime)
        {
            TryStartMoving();
        }
    }

    private void TryStartMoving()
    {
        Vector3 randomDest = GetRandomWalkablePos();
        
        if (pathFindComponent.FindPath(animal.transform.position, randomDest))
        {
            stateMachine.ChangeState<AS_RunState>();
        }
        else
        {
            // 길 찾기 실패 시 대기 시간 초기화 후 다시 시도
            idleTimer = 0f;
            nextMoveTime = Random.Range(1f, 3f);
        }
    }

    private Vector3 GetRandomWalkablePos()
    {
        Vector3Int currentCell = pathFindComponent.WorldToCell(animal.transform.position);
        
        // 주변 5~10칸 내외의 무작위 지점 탐색
        for (int i = 0; i < 30; i++)
        {
            int rx = Random.Range(-8, 9);
            int ry = Random.Range(-8, 9);
            Vector3Int candidate = new Vector3Int(currentCell.x + rx, currentCell.y + ry, 0);

            if (pathFindComponent.IsWalkable(candidate) && !pathFindComponent.IsOccupied(candidate))
            {
                return pathFindComponent.CellToWorld(candidate);
            }
        }

        return animal.transform.position; // 실패 시 현재 위치
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
