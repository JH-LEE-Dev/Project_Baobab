using UnityEngine;

public class RunState : CharacterState
{
    private Vector2 lastVisualInput;

    private Vector2 pendingDirection;
    private float directionUpdateTimer;
    private const float graceDuration = 0.025f;

    private Vector3Int currentReservedPos;

    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[5];

    public override void Enter()
    {
        bActivated = true;

        // 현재 위치 타일 점유
        currentReservedPos = ctx.tilemapDataProvider.WorldToCell(character.transform.position);
        ctx.pathfindGridProvider.Occupy(currentReservedPos);

        character.bMoving = true;

        // 상태 진입 시 현재 입력이 유효하다면 즉시 방향 갱신 (패드 아날로그 유지 시 이벤트 유실로 인한 문워크 방지)
        if (Vector2.zero != ctx.moveInput)
        {
            UpdateFacingDirection(ctx.moveInput);
        }
    }

    public override void Exit()
    {
        // 점유 해제
        ctx.pathfindGridProvider.Release(currentReservedPos);

        directionUpdateTimer = 0f;
        pendingDirection = Vector2.zero;
        lastVisualInput = Vector2.zero;

        character.bMoving = false;
        bActivated = false;
    }

    public override void Update()
    {
        if (false == bActivated)
            return;

        HandleDelayedDirectionUpdate();
    }

    public override void FixedUpdate()
    {
        if (false == bActivated)
            return;

        ApplyMovement();
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

        if (Vector2.zero == _input)
        {
            stateMachine.ChangeState<IdleState>();
            return;
        }

        bool isGamepad = (null != character.inputManager && EInputDeviceType.Gamepad == character.inputManager.CurrentDevice);
        int currentAxisCount = GetActiveAxisCount(_input);

        // 패드 조작이거나 대각선 입력 (축이 2개 이상)인 경우에는 즉시 방향 변경 적용
        if (isGamepad || 2 <= currentAxisCount)
        {
            UpdateFacingDirection(_input);
        }
        // 키보드 단일축 입력인 경우에는 대각선 입력 대기 및 떼기 보정을 위해 유예 시간 적용
        else
        {
            pendingDirection = _input;
            directionUpdateTimer = graceDuration;
        }
    }

    private int GetActiveAxisCount(Vector2 _v)
    {
        int count = 0;
        // 키보드 대각선 입력 시 0.707... 과 같은 부동 소수점 오차를 고려
        if (0.01f < Mathf.Abs(_v.x)) count++;
        if (0.01f < Mathf.Abs(_v.y)) count++;
        return count;
    }

    private void UpdateFacingDirection(Vector2 _input)
    {
        if (false == character.bInDungeon)
        {
            character.SetFacingDirection(GetIsometricVector(_input));
        }

        lastVisualInput = _input;
        directionUpdateTimer = 0f;
    }

    private void HandleDelayedDirectionUpdate()
    {
        if (0f < directionUpdateTimer)
        {
            directionUpdateTimer -= Time.deltaTime;
            if (0f >= directionUpdateTimer)
            {
                UpdateFacingDirection(pendingDirection);
            }
        }
    }

    private void ApplyMovement()
    {
        var groundData = character.currentGroundData;
        Vector2 inputDir = GetIsometricVector(ctx.moveInput);

        if (0.0001f < inputDir.sqrMagnitude)
            inputDir.Normalize();

        float speed = groundData.maxSpeed * ctx.characterStat.speed;

        Vector2 targetVel = inputDir * speed;

        character.rb.linearVelocity = Vector2.MoveTowards(
          character.rb.linearVelocity,
          targetVel,
          groundData.acceleration * Time.fixedDeltaTime
      );

        return;
    }

    /// <summary>
    /// 일반 입력을 아이소매트릭 타일의 변 방향으로 변환합니다.
    /// </summary>
    private Vector2 GetIsometricVector(Vector2 _input)
    {
        // ISO X = (X - Y), ISO Y = (X + Y) * 0.5 (2:1 비율 반영)
        return new Vector2(_input.x, _input.y * 0.5f);
    }
}
