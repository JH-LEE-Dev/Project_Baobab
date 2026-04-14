using UnityEngine;

public abstract class CharacterState : State
{
    protected ComponentCtx ctx;
    protected Character character;
    public void Initialize(StateMachine _stateMachine, Character _character,ComponentCtx _ctx)
    {
        stateMachine = _stateMachine;
        character = _character;
        ctx = _ctx;

        SubscribeEvents();
    }
}