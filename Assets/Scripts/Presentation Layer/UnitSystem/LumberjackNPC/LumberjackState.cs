using UnityEngine;

public abstract class LumberjackState
{
    protected LumberjackStateMachine stateMachine;
    protected LumberjackNPC npc;

    public virtual void Initialize(LumberjackStateMachine _stateMachine, LumberjackNPC _npc)
    {
        stateMachine = _stateMachine;
        npc = _npc;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}
