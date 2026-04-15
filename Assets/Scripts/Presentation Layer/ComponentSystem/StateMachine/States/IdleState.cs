using UnityEngine;

public class IdleState : CharacterState
{
    private Vector3Int currentReservedPos;
    private const float pixelsPerUnit = 32f; // 640*360 해상도 기준 픽셀 밀도

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

        // 속도가 어느 정도 있을 때는 감속 적용
        if (character.rb.linearVelocity.sqrMagnitude > 0.001f)
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
        Vector2 currentPos = character.rb.position;
        
        // 픽셀 그리드 좌표 계산 (Round를 사용하여 가장 가까운 픽셀로)
        float snapX = Mathf.Round(currentPos.x * pixelsPerUnit) / pixelsPerUnit;
        float snapY = Mathf.Round(currentPos.y * pixelsPerUnit) / pixelsPerUnit;
        Vector2 snappedPos = new Vector2(snapX, snapY);

        // 현재 위치와 스냅 위치의 차이가 미세하게라도 있으면 이동
        if (Vector2.SqrMagnitude(currentPos - snappedPos) > 0.00001f)
        {
            // 부드러운 스냅을 위해 MoveTowards 사용 (필요 시 즉시 할당 rb.position = snappedPos 로 변경 가능)
            character.rb.position = Vector2.MoveTowards(currentPos, snappedPos, Time.fixedDeltaTime);
        }
    }
}
