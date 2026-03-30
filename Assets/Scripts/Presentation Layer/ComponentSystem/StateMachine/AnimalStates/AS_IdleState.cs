using UnityEngine;

public class AS_IdleState : AnimalState
{
    private Vector3Int currentOccupiedPos;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, false);

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
