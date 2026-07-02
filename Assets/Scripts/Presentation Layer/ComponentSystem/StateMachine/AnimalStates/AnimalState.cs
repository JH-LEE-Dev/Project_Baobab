using UnityEngine;

public abstract class AnimalState : State
{
    protected Animal animal;
    
    public void Initialize(StateMachine _stateMachine, Animal _animal)
    {
        stateMachine = _stateMachine;
        animal = _animal;

        SubscribeEvents();
    }
}
