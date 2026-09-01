using UnityEngine;

public class KnockBackState : CharacterState
{
    private const float TravelDuration = 0.15f;
    private const float TotalLockDuration = 1.0f;

    private Vector2 startPos;
    private Vector2 targetPos;
    private float timer;

    // 넉백에 들어가기 직전의 이동 잠금 상태. 나갈 때 false를 박는 대신 이 값으로 되돌린다.
    //
    // PauseMove는 소유자별 잠금이 아니라 단일 bool이라(InputReader.IsMovePaused 참고), 넉백처럼
    // 남의 잠금 위에 겹쳐 잠그는 쪽이 무조건 풀어버리면 그 잠금까지 함께 열린다.
    // 넉백 중에도 상호작용 키는 막히지 않으므로(OffroadVehicleObj는 넉백 상태를 보지 않는다)
    // 차량에 겹친 채 밀리는 순간 귀환을 시작할 수 있고, 그러면 넉백이 끝나면서 귀환 연출이 걸어둔
    // 이동 잠금을 풀어 아이템 떨구기/탑승 연출 내내 캐릭터가 걸어다니게 된다.
    private bool bMovePausedBeforeKnockBack;

    // 나무→캐릭터 8방향 셀 오프셋을 그대로 넉백 방향으로 사용한다. 등각(2:1) 그리드에 맞춰
    // 방향별 최대 거리가 1타일을 넘지 않도록 타원 공식(CameraBoundsUtil과 동일한 공식)을 적용한다.
    public void SetKnockbackDirection(Vector3Int _cellOffset)
    {
        var tilemap = ctx.tilemapDataProvider;
        Vector2 origin = tilemap.CellToWorld(Vector3Int.zero);
        float semiMajor = ((Vector2)tilemap.CellToWorld(new Vector3Int(1, 0, 0)) - origin).magnitude;
        float semiMinor = ((Vector2)tilemap.CellToWorld(new Vector3Int(0, 1, 0)) - origin).magnitude;

        Vector2 rawDelta = (Vector2)tilemap.CellToWorld(_cellOffset) - origin;
        Vector2 dir = rawDelta.sqrMagnitude > 0.0001f ? rawDelta.normalized : Vector2.right;

        float denom = (dir.x * dir.x) / (semiMajor * semiMajor) + (dir.y * dir.y) / (semiMinor * semiMinor);
        float distance = denom > 0f ? 1f / Mathf.Sqrt(denom) : 0f;

        startPos = character.transform.position;
        targetPos = startPos + dir * distance;
    }

    public override void Enter()
    {
        bActivated = true;
        timer = 0f;

        character.rb.linearVelocity = Vector2.zero;
        ctx.moveInput = Vector2.zero;

        // 반드시 PauseMove(true)보다 먼저 읽는다.
        bMovePausedBeforeKnockBack = character.inputManager.IsMovePaused;

        character.inputManager.PauseMove(true);
        character.SetArmRotationLocked(true);
        character.SetAttackIndicatorLocked(true);
        character.SetFacingLocked(true);
        character.SetTreeHeatImmune(true);
        character.PlayStunVisual();
    }

    public override void Exit()
    {
        bActivated = false;

        // 타이머 자연 만료든, 사망 등으로 다른 State가 강제로 끼어들어 중단되는 경우든 상관없이
        // 항상 여기서 잠금을 풀어야 한다. 그렇지 않으면 넉백 도중 사망 등으로 State가 바뀔 때
        // 회전 잠금/열기 면역이 영구히 풀리지 않는 문제가 생긴다.
        //
        // 다만 이동 잠금만은 false를 박지 않고 넉백 직전 값으로 되돌린다.
        // (bMovePausedBeforeKnockBack 참고. 넉백 전에 안 잠겨 있었다면 결과는 종전과 동일하다)
        character.inputManager.PauseMove(bMovePausedBeforeKnockBack);
        character.SetArmRotationLocked(false);
        character.SetAttackIndicatorLocked(false);
        character.SetFacingLocked(false);
        character.SetTreeHeatImmune(false);
        character.StopStunVisual();
    }

    public override void Update()
    {
    }

    public override void FixedUpdate()
    {
        if (bActivated == false) return;

        timer += Time.fixedDeltaTime;

        if (timer <= TravelDuration)
        {
            float t = EaseOutCubic(Mathf.Clamp01(timer / TravelDuration));
            character.rb.MovePosition(Vector2.Lerp(startPos, targetPos, t));
        }

        if (timer >= TotalLockDuration)
        {
            stateMachine.ChangeState<IdleState>(); // 내부적으로 Exit()이 호출되어 각종 잠금이 풀린다

            // Exit()의 PauseMove(false)는 IdleState.Enter()보다 먼저 실행되어 MoveEvent 구독이 아직
            // 꺼진 상태라 이 시점의 실제 키 입력이 반영되지 않는다. IdleState.Enter()가 끝난 지금
            // 다시 한번 호출해야 그 입력이 유실되지 않고 바로 반영된다.
            // (Exit()과 마찬가지로 false가 아니라 넉백 직전 값으로 되돌린다)
            character.inputManager.PauseMove(bMovePausedBeforeKnockBack);
        }
    }

    private float EaseOutCubic(float _t)
    {
        return 1f - Mathf.Pow(1f - _t, 3);
    }

    protected override void SubscribeEvents()
    {
    }

    protected override void UnSubscribeEvents()
    {
    }
}
