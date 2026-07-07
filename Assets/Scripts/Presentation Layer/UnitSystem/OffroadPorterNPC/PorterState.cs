using UnityEngine;

public abstract class PorterState
{
    protected const float WAYPOINT_TOLERANCE = 0.05f;

    protected PorterStateMachine stateMachine;
    protected OffroadPorterNPC npc;

    public virtual void Initialize(PorterStateMachine _stateMachine, OffroadPorterNPC _npc)
    {
        stateMachine = _stateMachine;
        npc = _npc;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }

    /// <summary>
    /// npc.currentPath를 따라 한 스텝 이동합니다. 경로 끝에 도달했으면(더 갈 곳이 없으면) true를 반환합니다.
    /// LumberjackState.StepAlongPath와 동일한 로직입니다.
    /// </summary>
    protected bool StepAlongPath(ref int _pathIndex)
    {
        if (npc.currentPath == null || _pathIndex >= npc.currentPath.Count)
        {
            return true;
        }

        Vector3 currentPos = npc.transform.position;
        Vector3 targetPos = npc.currentPath[_pathIndex];

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
        }

        return false;
    }
}
