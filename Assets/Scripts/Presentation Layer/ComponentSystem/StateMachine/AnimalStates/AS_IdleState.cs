using UnityEngine;

public class AS_IdleState : AnimalState
{
    private float idleTimer;
    private float nextMoveTime;
    private bool isFleeing; // 도망 시도 여부 플래그

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, false);
        animal.rb.linearVelocity = Vector2.zero;

        idleTimer = 0f;
        nextMoveTime = Random.Range(2f, 5f); // 2~5초 사이 무작위 대기
        isFleeing = false;

        if (animal.bActivated == true)
            animal.feetShadowObject.SetActive(true);
    }

    public override void Exit()
    {
        bActivated = false;

        if (animal.feetShadowObject != null)
            animal.feetShadowObject.SetActive(false);
    }

    public override void Update()
    {
        if (!bActivated || !animal.bActivated) return;

        // 플레이어 감지 시 한 번만 도망 시도
        if (animal.bRunAway && !isFleeing)
        {
            isFleeing = true;
            TryFlee();
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer >= nextMoveTime)
        {
            TryStartMoving();
        }
    }

    private void TryFlee()
    {
        Vector3 currentPos = animal.GetTransform().position;
        animal.targetPos = currentPos + animal.FleeDirection * 5f;
        stateMachine.ChangeState<AS_RunState>();
    }

    private void TryStartMoving()
    {
        Vector3 currentPos = animal.GetTransform().position;
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
        animal.targetPos = currentPos + randomDir * Random.Range(2f, 5f);
        stateMachine.ChangeState<AS_RunState>();
    }

    public override void FixedUpdate()
    {
        if (!bActivated || !animal.bActivated) return;

        Vector2 velocity = animal.rb.linearVelocity;
        float sqrSpeed = velocity.sqrMagnitude;

        // 감속 로직
        if (sqrSpeed > 0.001f)
        {
            animal.rb.linearVelocity = Vector2.MoveTowards(
                velocity,
                Vector2.zero,
                animal.currentGroundData.deceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            // 속도가 거의 없으면 물리 속도 고정 및 픽셀 스냅
            animal.rb.linearVelocity = Vector2.zero;
            SnapToPixel();
        }
    }

    private void SnapToPixel()
    {
        // 전역 픽셀 스냅 유틸리티 사용
        GlobalPixelSnapper.SnapRigidbody(animal.rb, Time.fixedDeltaTime);
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
