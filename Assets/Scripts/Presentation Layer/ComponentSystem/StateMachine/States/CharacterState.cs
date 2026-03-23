using UnityEngine;

public abstract class CharacterState : State
{
    protected Character character;
    public void Initialize(StateMachine _stateMachine, Character _character)
    {
        stateMachine = _stateMachine;
        character = _character;

        SubscribeEvents();
    }
}