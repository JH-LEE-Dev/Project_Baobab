using System;
using System.Collections.Generic;

public class LumberjackStateMachine
{
    private Dictionary<Type, LumberjackState> states = new Dictionary<Type, LumberjackState>();
    public LumberjackState CurrentState { get; private set; }

    public void AddState(LumberjackState _state)
    {
        states[_state.GetType()] = _state;
    }

    public void ChangeState<T>() where T : LumberjackState
    {
        if (CurrentState != null)
        {
            CurrentState.Exit();
        }

        if (states.TryGetValue(typeof(T), out LumberjackState nextState))
        {
            CurrentState = nextState;
            CurrentState.Enter();
        }
    }

    public void Update()
    {
        CurrentState?.Update();
    }

    public void FixedUpdate()
    {
        CurrentState?.FixedUpdate();
    }

    public void ReleaseAllState()
    {
        CurrentState = null;
        states.Clear();
    }
}
