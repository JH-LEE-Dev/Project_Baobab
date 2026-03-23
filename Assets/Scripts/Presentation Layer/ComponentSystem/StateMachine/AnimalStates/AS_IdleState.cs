using UnityEngine;
using System.Collections.Generic;

public class AS_IdleState : AnimalState
{
    private float idleTimer;
    private const float minIdleTime = 1f;
    private const float maxIdleTime = 3f;

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, false);
        
        // 정지 시 속도 초기화
        animal.rb.linearVelocity = Vector2.zero;

        // 랜덤 대기 시간 설정
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
        if (idleTimer <= 0)
        {
            TryFindPath();
        }
    }

    public override void FixedUpdate()
    {
         animal.rb.linearVelocity = Vector2.MoveTowards(
                animal.rb.linearVelocity,
                Vector2.zero,
                animal.currentGroundData.deceleration * Time.fixedDeltaTime
            );
    }

    private void TryFindPath()
    {
        // 10~20 유닛 사이의 무작위 경로 탐색
        List<Vector3> path = pathFindComponent.FindPath(animal.transform.position, Random.Range(10f, 20f));

        if (path != null && path.Count > 0)
        {
            var runState = stateMachine.GetState<AS_RunState>();
            if (runState != null)
            {
                runState.SetPath(path);
                stateMachine.ChangeState<AS_RunState>();
            }
        }
        else
        {
            // 경로를 못 찾았으면 다시 대기
            idleTimer = Random.Range(minIdleTime, maxIdleTime);
        }
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
