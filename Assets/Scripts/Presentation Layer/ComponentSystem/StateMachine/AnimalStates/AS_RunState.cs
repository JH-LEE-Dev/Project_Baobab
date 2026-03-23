using UnityEngine;
using System.Collections.Generic;

public class AS_RunState : AnimalState
{
    private List<Vector3> path;
    private int currentPathIndex;
    private const float reachThreshold = 0.1f;

    public void SetPath(List<Vector3> _path)
    {
        path = _path;
        currentPathIndex = 0;
    }

    public override void Enter()
    {
        bActivated = true;
        animal.anim.SetBool(animal.isMovingHash, true);
    }

    public override void Exit()
    {
        bActivated = false;
        path = null;
    }

    public override void Update()
    {
        if (!bActivated || path == null || currentPathIndex >= path.Count)
        {
            stateMachine.ChangeState<AS_IdleState>();
            return;
        }

        // 현재 목표 지점 도달 여부 확인
        Vector3 targetPos = path[currentPathIndex];
        float distSq = (targetPos - animal.transform.position).sqrMagnitude;

        if (distSq < reachThreshold * reachThreshold)
        {
            currentPathIndex++;
        }
    }

    public override void FixedUpdate()
    {
        if (!bActivated || path == null || currentPathIndex >= path.Count) return;

        ApplyMovement();
    }

    private void ApplyMovement()
    {
        Vector3 targetPos = path[currentPathIndex];
        Vector3 direction = (targetPos - animal.transform.position).normalized;

        // 아이소매트릭 이동 방향 변환 (2:1 비율 반영을 위한 Y축 보정)
        // 실제 월드상에서의 타겟 방향을 그대로 사용하되, 캐릭터/동물 설정에 따라 보정이 필요할 수 있음
        // 여기서는 이미 path가 월드 좌표(IsWalkable 기반)이므로 해당 방향으로 직진
        
        Vector2 moveDir = new Vector2(direction.x, direction.y);
        
        // 바라보는 방향 업데이트
        animal.SetFacingDirection(moveDir);

        animal.rb.linearVelocity = Vector2.MoveTowards(
                animal.rb.linearVelocity,
                moveDir * animal.currentGroundData.maxSpeed,
                animal.currentGroundData.acceleration * Time.fixedDeltaTime
            );
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
