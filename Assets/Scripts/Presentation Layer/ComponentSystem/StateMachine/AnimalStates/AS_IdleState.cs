using UnityEngine;

public class AS_IdleState : AnimalState
{
    private float idleTimer;
    private const float minIdleTime = 1f;
    private const float maxIdleTime = 2f;
    private Vector3Int currentOccupiedPos;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, false);
        idleTimer = Random.Range(minIdleTime, maxIdleTime);

        // PathFindComponent를 통해 현재 위치 점유
        currentOccupiedPos = pathFindComponent.WorldToCell(animal.transform.position);
        pathFindComponent.Occupy(currentOccupiedPos);
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

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);
            Vector3 targetPos = animal.transform.position + randomOffset;

            if (pathFindComponent.FindPath(animal.transform.position, targetPos))
            {
                stateMachine.ChangeState<AS_RunState>();
            }
            else
            {
                idleTimer = 0.2f;
            }
        }
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
