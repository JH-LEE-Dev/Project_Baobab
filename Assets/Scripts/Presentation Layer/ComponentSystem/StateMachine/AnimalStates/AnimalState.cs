using UnityEngine;

public abstract class AnimalState : State
{
    protected Animal animal;
    protected PathFindComponent pathFindComponent;
    
    public void Initialize(StateMachine _stateMachine, Animal _animal,PathFindComponent _pathFindComponent)
    {
        stateMachine = _stateMachine;
        animal = _animal;
        pathFindComponent = _pathFindComponent;

        SubscribeEvents();
    }
}
