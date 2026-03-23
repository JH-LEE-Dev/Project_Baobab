using UnityEngine;

public class AS_RunState : AnimalState
{
    private int currentPathIndex;
    private const float stopDistance = 0.1f;
    private const float moveSpeed = 2f;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, true);
        currentPathIndex = 0;
    }

    public override void Exit()
    {
        bActivated = false;
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

        // 도달 판정 및 다음 노드 설정
        if (Vector2.Distance(animal.transform.position, path[currentPathIndex]) < stopDistance)
        {
            currentPathIndex++;
        }
    }

    public override void FixedUpdate()
    {
        if (!bActivated) return;

        var path = pathFindComponent.Path;
        if (currentPathIndex >= path.Count) return;

        Vector2 targetPos = path[currentPathIndex];
        Vector2 currentPos = animal.transform.position;
        Vector2 direction = (targetPos - currentPos).normalized;

        // 이동 및 바라보는 방향 설정
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
