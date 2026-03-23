using UnityEngine;

public class AS_IdleState : AnimalState
{
    private float idleTimer;
    private const float minIdleTime = 1f;
    private const float maxIdleTime = 2f;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, false);
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    public override void Exit()
    {
        bActivated = false;
    }

    public override void Update()
    {
        if (!bActivated) return;

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            // 주변 5유닛 반경 내의 랜덤한 위치를 목표로 설정
            Vector3 randomOffset = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);
            Vector3 targetPos = animal.transform.position + randomOffset;

            // PathFindComponent를 통해 길찾기 수행 (결과는 컴포넌트 내부 리스트에 저장됨)
            if (pathFindComponent.FindPath(animal.transform.position, targetPos))
            {
                stateMachine.ChangeState<AS_RunState>();
            }
            else
            {
                // 길찾기 실패 시 대기 시간 초기화 후 재시도
                idleTimer = Random.Range(minIdleTime, maxIdleTime);
            }
        }
    }

    public override void FixedUpdate()
    {
        if (!bActivated) return;

        // 서서히 멈추는 물리 처리
        animal.rb.linearVelocity = Vector2.MoveTowards(
            animal.rb.linearVelocity,
            Vector2.zero,
            animal.currentGroundData.deceleration * Time.fixedDeltaTime
        );
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
