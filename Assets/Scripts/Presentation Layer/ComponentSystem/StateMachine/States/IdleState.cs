using UnityEngine;

public class IdleState : CharacterState
{
    private Vector3Int currentReservedPos;

    public override void Enter()
    {
        bActivated = true;

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
        if (false == bActivated)
            return;
    }

    public override void FixedUpdate()
    {
        if (false == bActivated)
            return;

        if (0f != ctx.moveInput.sqrMagnitude)
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
        if (null != character && null != character.inputManager && null != character.inputManager.inputReader)
        {
            character.inputManager.inputReader.MoveEvent -= OnMove;
        }
    }

    private void OnMove(Vector2 _input)
    {
        if (false == bActivated)
            return;

        ctx.moveInput = _input;

        // 입력이 발생하면 마을 상태일 때 즉시 방향 설정 후 RunState로 전환
        if (Vector2.zero != _input)
        {
            if (false == character.bInDungeon)
            {
                character.SetFacingDirection(new Vector2(_input.x, _input.y * 0.5f));
            }

            stateMachine.ChangeState<RunState>();
        }
    }

    private void ApplyDeceleration()
    {
        var groundData = character.currentGroundData;

        // 속도가 어느 정도 있을 때는 감속 적용
        if (0.001f < character.rb.linearVelocity.sqrMagnitude)
        {
            character.rb.linearVelocity = Vector2.MoveTowards(
                character.rb.linearVelocity,
                Vector2.zero,
                groundData.deceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            // 속도가 거의 없으면 물리 속도를 0으로 고정하고 픽셀 스냅 수행
            character.rb.linearVelocity = Vector2.zero;
            SnapToPixel();
        }
    }

    private void SnapToPixel()
    {
        // 전역 픽셀 스냅 유틸리티 사용
        //GlobalPixelSnapper.SnapRigidbody(character.rb, Time.fixedDeltaTime);
    }
}
