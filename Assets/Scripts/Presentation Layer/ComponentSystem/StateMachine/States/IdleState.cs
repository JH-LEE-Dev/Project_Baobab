using UnityEngine;

public class IdleState : CharacterState
{
    private Vector3Int currentReservedPos;

    public override void Enter()
    {
        bActivated = true;
        character.anim.SetBool(character.isMovingHash, false);

        // 현재 위치 타일 점유
        currentReservedPos = ctx.tilemapDataProvider.WorldToCell(character.transform.position);
        ctx.pathfindGridProvider.Occupy(currentReservedPos);
    }

    public override void Exit()
    {
        // 점유 해제
        ctx.pathfindGridProvider.Release(currentReservedPos);

        bActivated = false;
    }

    public override void Update()
    {
    }

    public override void FixedUpdate()
    {
        if (ctx.moveInput.sqrMagnitude != 0 && character.bCanAction == true)
            stateMachine.ChangeState<RunState>();

        ApplyDeceleration();
        UpdateOccupation();
    }

    private void UpdateOccupation()
    {
        Vector3Int newCell = ctx.tilemapDataProvider.WorldToCell(character.transform.position);
        if (newCell != currentReservedPos)
        {
            ctx.pathfindGridProvider.Release(currentReservedPos);
            ctx.pathfindGridProvider.Occupy(newCell);
            currentReservedPos = newCell;
        }
    }

    protected override void SubscribeEvents()
    {
        character.inputManager.inputReader.MoveEvent += OnMove;
    }

    protected override void UnSubscribeEvents()
    {
        if (character != null && character.inputManager != null && character.inputManager.inputReader != null)
        {
            character.inputManager.inputReader.MoveEvent -= OnMove;
        }
    }

    private void OnMove(Vector2 _input)
    {
        if (bActivated == false)
            return;

        ctx.moveInput = _input;

        // 키보드 입력이 발생하면 즉시 방향 설정 후 RunState로 전환
        if (_input != Vector2.zero)
        {
            if (character.bCanAction == true)
                stateMachine.ChangeState<RunState>();
        }
    }

    private void ApplyDeceleration()
    {
        var groundData = character.currentGroundData;

        // 입력이 없는 상태이므로 속도를 0으로 수렴시킴 (감속도 적용)
        character.rb.linearVelocity = Vector2.MoveTowards(
            character.rb.linearVelocity,
            Vector2.zero,
            groundData.deceleration * Time.fixedDeltaTime
        );
    }
}
