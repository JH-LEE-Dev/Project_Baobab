using UnityEngine;

public class AS_RunState : AnimalState
{
    private const float stopDistance = 0.15f;
    private const float stopDistanceSqr = stopDistance * stopDistance;
    private const float moveSpeed = 2f;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, true);
    }

    public override void Exit()
    {
        bActivated = false;
        animal.anim.SetBool(animal.isMovingHash, false);
        animal.animalAnimValueHandler.RunStartEnd(false);
    }

    public override void Update()
    {
        if (!bActivated || !animal.bActivated) return;

        // 0. 실시간 도망 전환 체크
        if (animal.bRunAway)
        {
            animal.targetPos = animal.GetTransform().position + animal.FleeDirection * 5f;
        }

        Vector3 currentPos = animal.GetTransform().position;
        if ((currentPos - animal.targetPos).sqrMagnitude < stopDistanceSqr)
        {
            if (animal.bRunAway)
            {
                animal.targetPos = animal.GetTransform().position + animal.FleeDirection * 5f;
            }
            else
            {
                stateMachine.ChangeState<AS_IdleState>();
            }
        }
    }

    public override void FixedUpdate()
    {
        if (!bActivated || !animal.bActivated) return;

        Vector2 currentPos = (Vector2)animal.GetTransform().position;
        Vector2 targetPos = (Vector2)animal.targetPos;
        Vector2 diff = targetPos - currentPos;

        if (diff.sqrMagnitude < 0.0001f) return;

        Vector2 direction = diff.normalized;
        Vector2 velocity = animal.rb.linearVelocity;

        animal.rb.linearVelocity = Vector2.MoveTowards(
           velocity,
           direction * moveSpeed,
           animal.currentGroundData.deceleration * Time.fixedDeltaTime
       );

        animal.SetFacingDirection(direction);
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
