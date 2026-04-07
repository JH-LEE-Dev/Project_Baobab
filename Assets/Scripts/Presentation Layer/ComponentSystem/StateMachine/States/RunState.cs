using UnityEngine;
using UnityEngine.Tilemaps;

public class RunState : CharacterState
{
    private Vector2 moveInput;
    private Vector2 lastVisualInput;

    private Vector2 pendingDirection;
    private float directionUpdateTimer;
    private const float graceDuration = 0.05f;

    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[5];

    public override void Enter()
    {
        bActivated = true;
        character.anim.SetBool(character.isMovingHash, true);
    }

    public override void Exit()
    {
        moveInput = Vector2.zero;
        directionUpdateTimer = 0f;
        pendingDirection = Vector2.zero;

        bActivated = false;
    }

    public override void Update()
    {
        HandleDelayedDirectionUpdate();
    }

    public override void FixedUpdate()
    {
        ApplyMovement();
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

        moveInput = _input;

        if (_input == Vector2.zero)
        {
            stateMachine.ChangeState<IdleState>();
            return;
        }

        int lastAxisCount = GetActiveAxisCount(lastVisualInput);
        int currentAxisCount = GetActiveAxisCount(_input);

        // 축이 늘어나거나 동일한 경우 (이동 시작, 대각선 진입, 방향 전환 등): 즉시 업데이트
        if (currentAxisCount >= lastAxisCount)
        {
            UpdateFacingDirection(_input);
        }
        // 축이 줄어드는 경우 (대각선 -> 단일축): 키를 떼는 과정으로 판단하여 유예 시간 부여
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
        if (Mathf.Abs(_v.x) > 0.01f) count++;
        if (Mathf.Abs(_v.y) > 0.01f) count++;
        return count;
    }

    private void UpdateFacingDirection(Vector2 _input)
    {
        character.SetFacingDirection(GetIsometricVector(_input));
        lastVisualInput = _input;
        directionUpdateTimer = 0f;
    }

    private void HandleDelayedDirectionUpdate()
    {
        if (directionUpdateTimer > 0)
        {
            directionUpdateTimer -= Time.deltaTime;
            if (directionUpdateTimer <= 0)
            {
                UpdateFacingDirection(pendingDirection);
            }
        }
    }

    private void ApplyMovement()
    {
        var groundData = character.currentGroundData;
        Vector2 inputDir = GetIsometricVector(moveInput);

        if (inputDir.sqrMagnitude > 0.001f)
            inputDir.Normalize();

        float speed = groundData.maxSpeed;
        Vector2 targetVel = inputDir * speed;
        CircleCollider2D circleCol = character.col;
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
