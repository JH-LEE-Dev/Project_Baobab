using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 대시. 정해진 거리를 짧은 시간에 밀고 나가며, 그동안 도끼 공격 판정을 함께 낸다(Character.TryStartDash 참고).
///
/// 이동은 넉백과 같은 방식(Rigidbody2D.MovePosition)이지만, 넉백과 달리 거리가 길어(3칸) 물리 해결만으로는
/// 얇은 충돌체를 뚫을 수 있다. 그래서 매 물리 틱마다 이번 틱에 갈 만큼 캐릭터 원형 콜라이더를 미리 쏴 보고
/// (Physics2D.CircleCast), 확인된 거리만큼만 움직인다.
///  - 비스듬히 부딪히면 걸을 때처럼 벽을 타고 미끄러진다. 남은 거리에서 벽을 향하는 성분만 버리고 벽을 따라가는
///    성분만큼 한 번 더 쏴 본다.
///  - 거의 정면으로 부딪히거나(SlideStopAngle 이상) 미끄러진 쪽도 막히면(모서리) 그 자리에서 대시를 끝낸다.
///  - 매 틱 다시 쏘고, 쏘기 전에 타일맵 충돌을 최신으로 맞추므로 대시 도중 앞의 나무가 쓰러지면 길이 열린다.
/// 매 틱 움직일 수 있는 길이는 시간에 따른 진행량으로만 정한다. 그래서 미끄러지며 돌아가도 실제로 지나간 길은
/// 대시 거리를 넘지 않는다.
/// </summary>
public class DashState : CharacterState
{
    // 이동 경로를 미리 쏴 볼 때 벽에서 띄울 여유. 딱 붙여 멈추면 다음 틱의 Cast가 시작부터 겹친 것으로 잡힌다.
    private const float CastSkin = 0.01f;

    // 벽과 이 각도 이상으로 부딪히면 미끄러지지 않고 멈춘다(정면 충돌로 본다). 70도면 벽을 따라 남는 거리가
    // 원래의 약 1/3 미만인 경우다.
    private const float SlideStopAngle = 70f;
    private static readonly float SlideMinRatio = Mathf.Cos(SlideStopAngle * Mathf.Deg2Rad);

    private Vector2 dashDirection = Vector2.right;
    private Vector2 lastCommandedPos;
    private float dashDistance;
    private float dashDuration;
    private float timer;
    private float lastProgress;

    // 끝까지(또는 장애물에 막혀) 정상적으로 끝났는지. 넉백·사망 등으로 다른 상태가 끼어들면 false로 Exit한다.
    private bool bCompleted;

    // IdleState/RunState와 같은 이유로 대시 중에도 선 칸을 점유한다(KnockBackState.currentReservedPos 참고).
    private Vector3Int currentReservedPos;

    private ContactFilter2D contactFilter;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];

    // 캐릭터 원형 콜라이더의 월드 기준 반지름과, 리지드바디 위치에서 원 중심까지의 거리.
    private float castRadius;
    private Vector2 castCenterOffset;

    /// <summary>
    /// 다음 대시를 준비한다. ChangeState&lt;DashState&gt;() 직전에 불러야 한다.
    /// </summary>
    /// <param name="_direction">월드 기준 단위 방향</param>
    /// <param name="_distance">월드 기준 이동 거리</param>
    /// <param name="_duration">이동에 걸리는 시간(초)</param>
    public void Setup(Vector2 _direction, float _distance, float _duration)
    {
        dashDirection = (_direction.sqrMagnitude > 0.0001f) ? _direction.normalized : Vector2.right;
        dashDistance = Mathf.Max(0f, _distance);
        dashDuration = Mathf.Max(0.01f, _duration);
    }

    public override void Enter()
    {
        bActivated = true;
        bCompleted = false;
        timer = 0f;
        lastProgress = 0f;

        currentReservedPos = ctx.tilemapDataProvider.WorldToCell(character.transform.position);
        ctx.pathfindGridProvider.Occupy(currentReservedPos);

        lastCommandedPos = character.rb.position;
        character.rb.linearVelocity = Vector2.zero;

        // 캐릭터를 막는 것은 전부 막는다 - 충돌 매트릭스가 캐릭터와 부딪히게 해 둔 레이어를 그대로 쓴다.
        contactFilter = new ContactFilter2D();
        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(character.gameObject.layer));

        // 캐릭터는 회전하지 않으므로(FreezeRotation) 스케일만 반영하면 된다.
        Vector2 scale = character.transform.lossyScale;
        CircleCollider2D col = character.col;
        castRadius = col.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        castCenterOffset = new Vector2(col.offset.x * scale.x, col.offset.y * scale.y);

        character.bMoving = true;
    }

    public override void Exit()
    {
        ctx.pathfindGridProvider.Release(currentReservedPos);

        character.rb.linearVelocity = Vector2.zero;
        character.bMoving = false;
        bActivated = false;

        // 어느 경로로 나가든(만료·장애물·넉백·사망·리셋) 공격 정산은 반드시 한 번 일어나야 한다.
        character.OnDashFinished(lastCommandedPos, bCompleted);
    }

    public override void Update()
    {
    }

    public override void FixedUpdate()
    {
        if (false == bActivated) return;

        // 대시 도중 귀환 경고창·연출 등으로 이동이 잠기면 그 자리에서 멈춘다. 정상 종료가 아니므로
        // 마지막 타격 판정과 충격파는 내지 않는다(bCompleted = false로 Exit).
        if (true == character.inputManager.IsMovePaused)
        {
            stateMachine.ChangeState<IdleState>();
            return;
        }

        timer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(timer / dashDuration);

        float progress = EaseOutCubic(t);
        float stepLength = dashDistance * (progress - lastProgress);
        lastProgress = progress;

        bool bStopped = false;
        if (0.0001f < stepLength)
        {
            lastCommandedPos = MoveWithSlide(character.rb.position, stepLength, out bStopped);
            character.rb.MovePosition(lastCommandedPos);
        }

        // 남은 관성으로 미끄러지지 않게 매 틱 속도를 비운다(이동은 MovePosition만 한다).
        character.rb.linearVelocity = Vector2.zero;

        character.TickDashStrike(lastCommandedPos);
        UpdateOccupation();

        if (bStopped || 1f <= t)
        {
            bCompleted = true;

            // 대시 중에 누르고 있던 방향키가 있으면 곧바로 이어서 달린다.
            if (0f != ctx.moveInput.sqrMagnitude) stateMachine.ChangeState<RunState>();
            else stateMachine.ChangeState<IdleState>();
        }
    }

    /// <summary>
    /// _from에서 대시 방향으로 _length만큼 가 보고, 막히면 벽을 따라 한 번 미끄러진 위치를 돌려준다.
    /// 거의 정면으로 막혔거나 미끄러진 쪽도 막혔으면 _bStopped가 true다.
    /// </summary>
    private Vector2 MoveWithSlide(Vector2 _from, float _length, out bool _bStopped)
    {
        _bStopped = false;

        Vector2 position = _from;
        Vector2 direction = dashDirection;
        float remaining = _length;

        for (int i = 0; i < 2; i++)
        {
            if (false == CastBody(position, direction, remaining, out float hitDistance, out Vector2 hitNormal))
            {
                return position + direction * remaining;
            }

            float advance = Mathf.Max(0f, hitDistance - CastSkin);
            position += direction * advance;
            remaining -= advance;

            // 두 번째(미끄러진 쪽)도 막혔으면 모서리다.
            if (1 == i)
            {
                _bStopped = true;
                return position;
            }

            // 벽을 향하는 성분을 버린다. 남는 비율은 벽과 이루는 각도의 cos이다.
            Vector2 tangent = direction - hitNormal * Vector2.Dot(direction, hitNormal);
            float tangentRatio = tangent.magnitude;
            if (tangentRatio < SlideMinRatio || remaining <= 0.0001f)
            {
                _bStopped = tangentRatio < SlideMinRatio;
                return position;
            }

            direction = tangent / tangentRatio;
            remaining *= tangentRatio;
        }

        return position;
    }

    /// <summary>
    /// 캐릭터 원형 콜라이더를 _rootPosition 기준으로 _direction 쪽으로 쏴서, 진행을 막는 가장 가까운 충돌을 찾는다.
    /// </summary>
    private bool CastBody(Vector2 _rootPosition, Vector2 _direction, float _length, out float _hitDistance, out Vector2 _hitNormal)
    {
        Vector2 origin = _rootPosition + castCenterOffset;
        float castLength = _length + CastSkin;

        int hitCount = Physics2D.CircleCast(origin, castRadius, _direction, contactFilter, castHits, castLength);

        // 방금 쓰러진 나무의 충돌 타일은 지워졌어도 TilemapCollider2D가 모양을 다시 만들기 전까지 남아 있다.
        // 그 전에 쏘면 이미 없는 나무에 막히므로, 걸린 타일맵에 밀린 변경이 있으면 지금 반영하고 다시 쏜다.
        bool bRecast = false;
        for (int i = 0; i < hitCount; i++)
        {
            if (castHits[i].collider is TilemapCollider2D tilemapCollider && true == tilemapCollider.hasTilemapChanges)
            {
                tilemapCollider.ProcessTilemapChanges();
                bRecast = true;
            }
        }

        if (bRecast)
        {
            hitCount = Physics2D.CircleCast(origin, castRadius, _direction, contactFilter, castHits, castLength);
        }

        _hitDistance = float.MaxValue;
        _hitNormal = Vector2.zero;
        bool bHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (null == hit.collider || hit.rigidbody == character.rb) continue;

            // 시작부터 겹쳐 있으면 거리 0으로 잡히고 법선을 믿을 수 없다. 겹친 지점에서 원 중심 쪽을 법선으로 본다.
            Vector2 normal = hit.normal;
            if (0f >= hit.distance)
            {
                Vector2 away = origin - hit.point;
                if (0.000001f < away.sqrMagnitude) normal = away.normalized;
            }

            // 옆에 붙어 있는 벽처럼 진행 방향을 막지 않는 접촉은 무시한다.
            if (Vector2.Dot(normal, _direction) >= -0.0001f) continue;

            if (hit.distance < _hitDistance)
            {
                _hitDistance = hit.distance;
                _hitNormal = normal;
                bHit = true;
            }
        }

        return bHit;
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

    private float EaseOutCubic(float _t)
    {
        return 1f - Mathf.Pow(1f - _t, 3);
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

    // 대시 중에는 이동 입력으로 방향을 바꾸지 않는다. 대시가 끝난 뒤 이어 달릴지 판단하려고 값만 기억한다.
    private void OnMove(Vector2 _input)
    {
        if (false == bActivated) return;

        ctx.moveInput = _input;
    }
}
