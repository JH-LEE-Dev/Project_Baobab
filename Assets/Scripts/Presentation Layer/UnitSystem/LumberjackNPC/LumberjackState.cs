using UnityEngine;

public abstract class LumberjackState
{
    protected const float WAYPOINT_TOLERANCE = 0.05f;

    protected LumberjackStateMachine stateMachine;
    protected LumberjackNPC npc;

    // TEMP DEBUG: LumberjackStateMachine이 상태 전환 로그를 찍을 때 어느 NPC인지 식별하기 위해 노출.
    public LumberjackNPC Npc => npc;

    public virtual void Initialize(LumberjackStateMachine _stateMachine, LumberjackNPC _npc)
    {
        stateMachine = _stateMachine;
        npc = _npc;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    /// <summary>
    /// npc.currentPath를 따라 한 스텝 이동합니다. 경로 끝에 도달했으면(더 갈 곳이 없으면) true를 반환합니다.
    /// LJState_Move/LJState_Deliver 등 경로를 따라 걷는 모든 상태가 공유하는 이동 로직입니다.
    /// </summary>
    protected bool StepAlongPath(ref int _pathIndex)
    {
        if (npc.currentPath == null || _pathIndex >= npc.currentPath.Count)
        {
            return true;
        }

        Vector3 currentPos = npc.transform.position;
        Vector3 targetPos = npc.currentPath[_pathIndex];

        // y 오프셋 등 높이 차이 무시 (2D 평면 기준)
        targetPos.z = currentPos.z;

        float distance = Vector2.Distance(currentPos, targetPos);

        if (distance <= WAYPOINT_TOLERANCE)
        {
            _pathIndex++;
        }
        else
        {
            Vector2 direction = ((Vector2)targetPos - (Vector2)currentPos).normalized;
            npc.transform.position = Vector3.MoveTowards(currentPos, targetPos, npc.stat.moveSpeed * Time.deltaTime);

            npc.SetVisualFacing(direction);
            npc.SetArmDirection(direction);
        }

        return false;
    }
}
